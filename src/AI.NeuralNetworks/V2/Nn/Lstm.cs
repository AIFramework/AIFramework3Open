using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// LSTM-ячейка (стандартная PyTorch формулировка с двумя bias).
/// </summary>
public sealed class LSTMCell : Module
{
    /// <summary>Размер входа.</summary>
    public int InputSize { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int HiddenSize { get; }

    /// <summary>W_ih (4H, I): порядок гейтов (i, f, g, o).</summary>
    public Parameter WeightIH { get; }
    /// <summary>W_hh (4H, H).</summary>
    public Parameter WeightHH { get; }
    /// <summary>b_ih (4H).</summary>
    public Parameter BiasIH { get; }
    /// <summary>b_hh (4H).</summary>
    public Parameter BiasHH { get; }

    /// <summary>Создать LSTM-ячейку.</summary>
    public LSTMCell(int inputSize, int hiddenSize, bool bias = true, Random rng = null)
    {
        InputSize = inputSize; HiddenSize = hiddenSize;
        float bound = 1f / MathF.Sqrt(hiddenSize);
        int H4 = 4 * hiddenSize;

        WeightIH = RegisterParameter("weight_ih", Init.Uniform_(Tensor.Empty(new Shape(H4, inputSize)), -bound, bound, rng));
        WeightHH = RegisterParameter("weight_hh", Init.Uniform_(Tensor.Empty(new Shape(H4, hiddenSize)), -bound, bound, rng));
        if (bias)
        {
            BiasIH = RegisterParameter("bias_ih", Init.Uniform_(Tensor.Empty(new Shape(H4)), -bound, bound, rng));
            BiasHH = RegisterParameter("bias_hh", Init.Uniform_(Tensor.Empty(new Shape(H4)), -bound, bound, rng));
        }
    }

    /// <summary>Один шаг LSTM: (x (B,I), (h, c)) -> (h', c').</summary>
    public (Tensor h, Tensor c) Step(Tensor x, Tensor h, Tensor c)
    {
        var gates = x.MatMul(WeightIH.Tensor.Transpose(0, 1)) +
                    h.MatMul(WeightHH.Tensor.Transpose(0, 1));
        if (BiasIH != null) gates = gates + BiasIH.Tensor + BiasHH.Tensor;

        int H = HiddenSize;
        var i = IndexingOps.Narrow(gates, 1, 0 * H, H).Sigmoid();
        var f = IndexingOps.Narrow(gates, 1, 1 * H, H).Sigmoid();
        var g = IndexingOps.Narrow(gates, 1, 2 * H, H).Tanh();
        var o = IndexingOps.Narrow(gates, 1, 3 * H, H).Sigmoid();

        var cNew = f * c + i * g;
        var hNew = o * cNew.Tanh();
        return (hNew, cNew);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        throw new NotSupportedException(
            "LSTMCell.Forward(input) не поддерживается; используйте Step(x, h, c).");
}

/// <summary>Многошаговый LSTM. (B, T, I) -> (B, T, H), (h_T, c_T).</summary>
/// <remarks>
/// Оптимизация: x@W_ih^T вычисляется для всех T одним GEMM (B*T × I × 4H).
/// В цикле остаётся только h@W_hh^T (B × H × 4H).
/// </remarks>
public sealed class LSTM : Module
{
    /// <summary>Ячейка.</summary>
    public LSTMCell Cell { get; }
    /// <summary>(B, T, *) если true.</summary>
    public bool BatchFirst { get; }

    /// <summary>Создать LSTM.</summary>
    public LSTM(int inputSize, int hiddenSize, bool bias = true, bool batchFirst = true, Random rng = null)
    {
        Cell = RegisterModule("cell", new LSTMCell(inputSize, hiddenSize, bias, rng));
        BatchFirst = batchFirst;
    }

    /// <summary>Forward последовательности.</summary>
    /// <remarks>
    /// <para>
    /// Внутренний layout — <c>(T, B, *)</c>: per-step Select(axis=0) -> contiguous (B, 4H).
    /// </para>
    /// <para>
    /// На CPU per-step активации/multi/sum выполняются одним fused-проходом через
    /// <c>RecurrentFused.LstmStep</c> — это сворачивает ~10 tensor-ops в один Function-узел
    /// и устраняет 4 Contiguous-копии гейтов после <see cref="IndexingOps.Narrow"/>.
    /// На GPU и для не-Float32 — composed-путь через <see cref="Ops.TensorOps"/>.
    /// </para>
    /// </remarks>
    public (Tensor outputs, Tensor hN, Tensor cN) ForwardSeq(Tensor x, Tensor h0 = null, Tensor c0 = null)
    {
        if (x.Rank != 3) throw new ArgumentException("LSTM: вход (B, T, I) или (T, B, I).");

        Tensor xTb = BatchFirst ? x.Permute(1, 0, 2).Contiguous() : x.Contiguous();
        int T = xTb.Shape[0], B = xTb.Shape[1];
        int H = Cell.HiddenSize;
        int H4 = 4 * H;

        var xFlat = xTb.Reshape(T * B, Cell.InputSize);
        var xProj = xFlat.MatMul(Cell.WeightIH.Tensor.Transpose(0, 1));
        if (Cell.BiasIH != null) xProj = xProj + Cell.BiasIH.Tensor;
        if (Cell.BiasHH != null) xProj = xProj + Cell.BiasHH.Tensor;
        xProj = xProj.Reshape(T, B, H4);

        var wHhT = Cell.WeightHH.Tensor.Transpose(0, 1);

        bool disabled = Environment.GetEnvironmentVariable("AI_NN_DISABLE_RECURRENT_FUSED") == "1";
        bool fastCpu = x.Device.Type == DeviceType.Cpu && x.DType == DType.Float32 && !disabled;

        // GPU full-sequence fast-path: ОДИН autograd-Function на все T шагов.
        var seqKernel = (!fastCpu && !disabled && x.DType == DType.Float32
                         && x.Device.Type != DeviceType.Cpu)
            ? OpRegistry.TryGet(OpCode.LstmSeq, DType.Float32, x.Device)
            : null;

        if (seqKernel != null)
        {
            var xProjC = xProj.IsContiguous ? xProj : xProj.Contiguous();
            var attrs = new LstmSeqAttrs(T, B, H, h0 != null, c0 != null);
            var h0In = h0 ?? Tensor.Empty(new Shape(0), DType.Float32, x.Device);
            var c0In = c0 ?? Tensor.Empty(new Shape(0), DType.Float32, x.Device);
            // Output: packed (T+1, B, H). Plane t (для t<T) = h_t; plane T = c_T.
            var packed = seqKernel(new[] { xProjC, wHhT, h0In, c0In }, attrs)[0];
            var stackedTBH = IndexingOps.Narrow(packed, 0, 0, T);
            var hNFused = IndexingOps.Select(stackedTBH, 0, T - 1);
            var cNFused = IndexingOps.Select(packed, 0, T);
            var outFused = BatchFirst ? stackedTBH.Permute(1, 0, 2).Contiguous() : stackedTBH;
            return (outFused, hNFused, cNFused);
        }

        var h = h0 ?? Tensor.Zeros(new Shape(B, H), x.DType, x.Device);
        var c = c0 ?? Tensor.Zeros(new Shape(B, H), x.DType, x.Device);
        var outs = new List<Tensor>(T);

        var lstmKernel = (!fastCpu && !disabled && x.DType == DType.Float32 && x.Device.Type != DeviceType.Cpu)
            ? OpRegistry.TryGet(OpCode.LstmStep, DType.Float32, x.Device)
            : null;
        var lstmAttrs = lstmKernel != null ? new LstmStepAttrs(B, H) : null;

        for (int t = 0; t < T; t++)
        {
            var xPt    = IndexingOps.Select(xProj, 0, t);
            var preact = xPt + h.MatMul(wHhT);

            if (fastCpu)
            {
                // Один проход: forward + autograd-узел вместо ~10 элементов графа.
                var packed = RecurrentFused.LstmStep(preact, c);
                h = IndexingOps.Select(packed, 0, 0);
                c = IndexingOps.Select(packed, 0, 1);
            }
            else if (lstmKernel != null)
            {
                var packed = lstmKernel(new[] { preact, c }, lstmAttrs)[0];
                h = IndexingOps.Select(packed, 0, 0);
                c = IndexingOps.Select(packed, 0, 1);
            }
            else
            {
                var gi = IndexingOps.Narrow(preact, 1, 0, H).Sigmoid();
                var gf = IndexingOps.Narrow(preact, 1, H, H).Sigmoid();
                var gg = IndexingOps.Narrow(preact, 1, 2 * H, H).Tanh();
                var go = IndexingOps.Narrow(preact, 1, 3 * H, H).Sigmoid();
                c = gf * c + gi * gg;
                h = go * c.Tanh();
            }
            outs.Add(h);
        }

        var stacked = IndexingOps.Stack(outs, axis: 0);
        var output = BatchFirst ? stacked.Permute(1, 0, 2).Contiguous() : stacked;
        return (output, h, c);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => ForwardSeq(input).outputs;
}

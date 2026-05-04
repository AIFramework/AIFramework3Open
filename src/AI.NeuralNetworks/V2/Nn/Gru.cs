using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>GRU-ячейка (PyTorch формулировка).</summary>
public sealed class GRUCell : Module
{
    /// <summary>Размер входа.</summary>
    public int InputSize { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int HiddenSize { get; }

    /// <summary>W_ih (3H, I): (r, z, n).</summary>
    public Parameter WeightIH { get; }
    /// <summary>W_hh (3H, H).</summary>
    public Parameter WeightHH { get; }
    /// <summary>b_ih (3H).</summary>
    public Parameter BiasIH { get; }
    /// <summary>b_hh (3H).</summary>
    public Parameter BiasHH { get; }

    /// <summary>Создать GRU-ячейку.</summary>
    public GRUCell(int inputSize, int hiddenSize, bool bias = true, Random rng = null)
    {
        InputSize = inputSize; HiddenSize = hiddenSize;
        float bound = 1f / MathF.Sqrt(hiddenSize);
        int H3 = 3 * hiddenSize;
        WeightIH = RegisterParameter("weight_ih", Init.Uniform_(Tensor.Empty(new Shape(H3, inputSize)), -bound, bound, rng));
        WeightHH = RegisterParameter("weight_hh", Init.Uniform_(Tensor.Empty(new Shape(H3, hiddenSize)), -bound, bound, rng));
        if (bias)
        {
            BiasIH = RegisterParameter("bias_ih", Init.Uniform_(Tensor.Empty(new Shape(H3)), -bound, bound, rng));
            BiasHH = RegisterParameter("bias_hh", Init.Uniform_(Tensor.Empty(new Shape(H3)), -bound, bound, rng));
        }
    }

    /// <summary>Один шаг GRU.</summary>
    public Tensor Step(Tensor x, Tensor h)
    {
        var gx = x.MatMul(WeightIH.Tensor.Transpose(0, 1));
        if (BiasIH != null) gx = gx + BiasIH.Tensor;
        var gh = h.MatMul(WeightHH.Tensor.Transpose(0, 1));
        if (BiasHH != null) gh = gh + BiasHH.Tensor;

        int H = HiddenSize;
        var rx = IndexingOps.Narrow(gx, 1, 0 * H, H);
        var zx = IndexingOps.Narrow(gx, 1, 1 * H, H);
        var nx = IndexingOps.Narrow(gx, 1, 2 * H, H);
        var rh = IndexingOps.Narrow(gh, 1, 0 * H, H);
        var zh = IndexingOps.Narrow(gh, 1, 1 * H, H);
        var nh = IndexingOps.Narrow(gh, 1, 2 * H, H);

        var r = (rx + rh).Sigmoid();
        var z = (zx + zh).Sigmoid();
        var n = (nx + r * nh).Tanh();
        return (Tensor.Scalar(1f) - z) * n + z * h;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        throw new NotSupportedException("GRUCell.Forward(input) не поддерживается; используйте Step(x, h).");
}

/// <summary>Многошаговый GRU. (B, T, I) -> (B, T, H), h_T.</summary>
/// <remarks>
/// Оптимизация: x@W_ih^T вычисляется для всех T одним GEMM (B*T × I × 3H).
/// В цикле остаётся только h@W_hh^T (B × H × 3H).
/// Гейт r модулирует nh-компоненту gh, поэтому gh вычисляется в цикле.
/// </remarks>
public sealed class GRU : Module
{
    /// <summary>Ячейка.</summary>
    public GRUCell Cell { get; }
    /// <summary>(B, T, *).</summary>
    public bool BatchFirst { get; }

    /// <summary>Создать GRU.</summary>
    public GRU(int inputSize, int hiddenSize, bool bias = true, bool batchFirst = true, Random rng = null)
    {
        Cell = RegisterModule("cell", new GRUCell(inputSize, hiddenSize, bias, rng));
        BatchFirst = batchFirst;
    }

    /// <summary>Forward последовательности.</summary>
    /// <remarks>
    /// <para>
    /// Внутренний layout — <c>(T, B, *)</c>. На CPU per-step gates+activation+update
    /// выполняются одним fused-проходом (<c>RecurrentFused.GruStep</c>), что
    /// убирает ~12 поэлементных tensor-ops в одну Function-узел и устраняет
    /// 6 Contiguous-копий после <see cref="IndexingOps.Narrow"/>.
    /// </para>
    /// </remarks>
    public (Tensor outputs, Tensor hN) ForwardSeq(Tensor x, Tensor h0 = null)
    {
        if (x.Rank != 3) throw new ArgumentException("GRU: вход (B, T, I) или (T, B, I).");

        Tensor xTb = BatchFirst ? x.Permute(1, 0, 2).Contiguous() : x.Contiguous();
        int T = xTb.Shape[0], B = xTb.Shape[1];
        int H = Cell.HiddenSize;
        int H3 = 3 * H;

        var xFlat = xTb.Reshape(T * B, Cell.InputSize);
        var gxAll = xFlat.MatMul(Cell.WeightIH.Tensor.Transpose(0, 1));
        if (Cell.BiasIH != null) gxAll = gxAll + Cell.BiasIH.Tensor;
        gxAll = gxAll.Reshape(T, B, H3);

        var wHhT = Cell.WeightHH.Tensor.Transpose(0, 1);

        bool disabled = Environment.GetEnvironmentVariable("AI_NN_DISABLE_RECURRENT_FUSED") == "1";
        bool fastCpu = x.Device.Type == DeviceType.Cpu && x.DType == DType.Float32 && !disabled;

        // GPU full-sequence fast-path. Аналогично LSTM/RNN.
        var seqKernel = (!fastCpu && !disabled && x.DType == DType.Float32
                         && x.Device.Type != DeviceType.Cpu)
            ? OpRegistry.TryGet(OpCode.GruSeq, DType.Float32, x.Device)
            : null;
        if (seqKernel != null)
        {
            var gxAllC = gxAll.IsContiguous ? gxAll : gxAll.Contiguous();
            var attrs = new GruSeqAttrs(T, B, H, Cell.BiasHH != null, h0 != null);
            var bHhIn = Cell.BiasHH != null ? Cell.BiasHH.Tensor : Tensor.Empty(new Shape(0), DType.Float32, x.Device);
            var h0In = h0 ?? Tensor.Empty(new Shape(0), DType.Float32, x.Device);
            var stackedTBH = seqKernel(new[] { gxAllC, wHhT, bHhIn, h0In }, attrs)[0];
            var hNFused = IndexingOps.Select(stackedTBH, 0, T - 1);
            var outFused = BatchFirst ? stackedTBH.Permute(1, 0, 2).Contiguous() : stackedTBH;
            return (outFused, hNFused);
        }

        var h = h0 ?? Tensor.Zeros(new Shape(B, H), x.DType, x.Device);
        var outs = new List<Tensor>(T);

        var gruKernel = (!fastCpu && !disabled && x.DType == DType.Float32 && x.Device.Type != DeviceType.Cpu)
            ? OpRegistry.TryGet(OpCode.GruStep, DType.Float32, x.Device)
            : null;
        var gruAttrs = gruKernel != null ? new GruStepAttrs(B, H) : null;
        Tensor oneScalar = (fastCpu || gruKernel != null) ? null : Tensor.Full(new Shape(1), 1f, x.DType, x.Device);

        for (int t = 0; t < T; t++)
        {
            var gxT = IndexingOps.Select(gxAll, 0, t);
            var gh = h.MatMul(wHhT);
            if (Cell.BiasHH != null) gh = gh + Cell.BiasHH.Tensor;

            if (fastCpu)
                h = RecurrentFused.GruStep(gxT, gh, h);
            else if (gruKernel != null)
                h = gruKernel(new[] { gxT, gh, h }, gruAttrs)[0];
            else
            {
                var rx = IndexingOps.Narrow(gxT, 1, 0, H);
                var zx = IndexingOps.Narrow(gxT, 1, H, H);
                var nx = IndexingOps.Narrow(gxT, 1, 2 * H, H);
                var rh = IndexingOps.Narrow(gh, 1, 0, H);
                var zh = IndexingOps.Narrow(gh, 1, H, H);
                var nh = IndexingOps.Narrow(gh, 1, 2 * H, H);

                var r = (rx + rh).Sigmoid();
                var z = (zx + zh).Sigmoid();
                var n = (nx + r * nh).Tanh();
                h = (oneScalar - z) * n + z * h;
            }
            outs.Add(h);
        }

        var stacked = IndexingOps.Stack(outs, axis: 0);
        var output = BatchFirst ? stacked.Permute(1, 0, 2).Contiguous() : stacked;
        return (output, h);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => ForwardSeq(input).outputs;
}

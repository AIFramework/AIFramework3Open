using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Базовая RNN-ячейка: h' = activation(W_ih @ x + b_ih + W_hh @ h + b_hh).
/// </summary>
public sealed class RNNCell : Module
{
    /// <summary>Размер входа.</summary>
    public int InputSize { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int HiddenSize { get; }
    /// <summary>Активация: "tanh" или "relu".</summary>
    public string Nonlinearity { get; }

    /// <summary>W_ih (H, I).</summary>
    public Parameter WeightIH { get; }
    /// <summary>W_hh (H, H).</summary>
    public Parameter WeightHH { get; }
    /// <summary>b_ih (H).</summary>
    public Parameter BiasIH { get; }
    /// <summary>b_hh (H).</summary>
    public Parameter BiasHH { get; }

    /// <summary>Создать RNN-ячейку.</summary>
    public RNNCell(int inputSize, int hiddenSize, string nonlinearity = "tanh", bool bias = true,
        Random rng = null)
    {
        if (nonlinearity != "tanh" && nonlinearity != "relu")
            throw new ArgumentException("nonlinearity: 'tanh' или 'relu'.");
        InputSize = inputSize;
        HiddenSize = hiddenSize;
        Nonlinearity = nonlinearity;

        float bound = 1f / MathF.Sqrt(hiddenSize);
        WeightIH = RegisterParameter("weight_ih", Init.Uniform_(Tensor.Empty(new Shape(hiddenSize, inputSize)), -bound, bound, rng));
        WeightHH = RegisterParameter("weight_hh", Init.Uniform_(Tensor.Empty(new Shape(hiddenSize, hiddenSize)), -bound, bound, rng));
        if (bias)
        {
            BiasIH = RegisterParameter("bias_ih", Init.Uniform_(Tensor.Empty(new Shape(hiddenSize)), -bound, bound, rng));
            BiasHH = RegisterParameter("bias_hh", Init.Uniform_(Tensor.Empty(new Shape(hiddenSize)), -bound, bound, rng));
        }
    }

    /// <summary>Forward один шаг: x (B, I), h (B, H) -> h' (B, H).</summary>
    public Tensor Step(Tensor x, Tensor h)
    {
        var preact = x.MatMul(WeightIH.Tensor.Transpose(0, 1)) +
                     h.MatMul(WeightHH.Tensor.Transpose(0, 1));
        if (BiasIH != null) preact = preact + BiasIH.Tensor + BiasHH.Tensor;
        return Nonlinearity == "tanh" ? preact.Tanh() : preact.Relu();
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        throw new NotSupportedException(
            "RNNCell.Forward(input) не поддерживается; используйте Step(input, hidden).");
}

/// <summary>
/// Многошаговая RNN: применяет <see cref="RNNCell"/> по временной оси
/// последовательности (B, T, I) -> (B, T, H).
/// </summary>
/// <remarks>
/// Оптимизация: x@W_ih^T вычисляется для всех T одним GEMM.
/// В цикле остаётся только h@W_hh^T (hidden-to-hidden).
/// </remarks>
public sealed class RNN : Module
{
    /// <summary>Ячейка.</summary>
    public RNNCell Cell { get; }
    /// <summary>Возвращать ли выход сразу как (B, T, H) (true) или (T, B, H) (false).</summary>
    public bool BatchFirst { get; }

    /// <summary>Создать RNN.</summary>
    public RNN(int inputSize, int hiddenSize, string nonlinearity = "tanh", bool bias = true,
        bool batchFirst = true, Random rng = null)
    {
        Cell = RegisterModule("cell", new RNNCell(inputSize, hiddenSize, nonlinearity, bias, rng));
        BatchFirst = batchFirst;
    }

    /// <summary>
    /// Forward последовательности. <paramref name="h0"/> — начальное скрытое
    /// состояние (B, H); если null — нули.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Внутренний layout — <c>(T, B, *)</c>: последовательное чтение временных срезов
    /// через <see cref="IndexingOps.Select"/>(axis=0) даёт contiguous (B, *)-тензоры,
    /// благодаря чему все per-step ops идут по fast-path (без N-D индексирования).
    /// </para>
    /// <para>
    /// На GPU при Float32 весь T-шаговый цикл уходит в один <see cref="OpCode.RnnSeq"/>-Function:
    /// одна аллокация на всю backward-фазу (вместо T копий xShape) и в ~3-5 раз
    /// меньше autograd-узлов. Включается, если зарегистрирован kernel; иначе —
    /// composed-путь (CPU и legacy-GPU).
    /// </para>
    /// </remarks>
    public (Tensor outputs, Tensor hN) ForwardSeq(Tensor x, Tensor h0 = null)
    {
        if (x.Rank != 3) throw new ArgumentException("RNN: вход (B, T, I) или (T, B, I).");

        Tensor xTb = BatchFirst ? x.Permute(1, 0, 2).Contiguous() : x.Contiguous();
        int T = xTb.Shape[0], B = xTb.Shape[1];
        int H = Cell.HiddenSize;
        if (xTb.Shape[2] != Cell.InputSize) throw new ArgumentException("RNN: размер входа не совпадает.");

        var xFlat = xTb.Reshape(T * B, Cell.InputSize);
        var xProj = xFlat.MatMul(Cell.WeightIH.Tensor.Transpose(0, 1));
        if (Cell.BiasIH != null) xProj = xProj + Cell.BiasIH.Tensor;
        if (Cell.BiasHH != null) xProj = xProj + Cell.BiasHH.Tensor;
        xProj = xProj.Reshape(T, B, H);

        var wHhT = Cell.WeightHH.Tensor.Transpose(0, 1);

        bool disabled = Environment.GetEnvironmentVariable("AI_NN_DISABLE_RECURRENT_FUSED") == "1";
        var seqKernel = (!disabled && x.DType == DType.Float32 && x.Device.Type != DeviceType.Cpu)
            ? OpRegistry.TryGet(OpCode.RnnSeq, DType.Float32, x.Device)
            : null;

        if (seqKernel != null)
        {
            int nonlin = Cell.Nonlinearity == "tanh" ? 0 : 1;
            var xProjC = xProj.IsContiguous ? xProj : xProj.Contiguous();
            var attrs = new RnnSeqAttrs(T, B, H, nonlin, h0 != null);
            var h0In = h0 ?? Tensor.Empty(new Shape(0), DType.Float32, x.Device);
            var stackedTBH = seqKernel(new[] { xProjC, wHhT, h0In }, attrs)[0];
            var hNFused = IndexingOps.Select(stackedTBH, 0, T - 1);
            var outFused = BatchFirst ? stackedTBH.Permute(1, 0, 2).Contiguous() : stackedTBH;
            return (outFused, hNFused);
        }

        var h = h0 ?? Tensor.Zeros(new Shape(B, H), x.DType, x.Device);
        var outs = new List<Tensor>(T);

        for (int t = 0; t < T; t++)
        {
            var xPt = IndexingOps.Select(xProj, 0, t);
            var preact = xPt + h.MatMul(wHhT);
            h = Cell.Nonlinearity == "tanh" ? preact.Tanh() : preact.Relu();
            outs.Add(h);
        }

        var stacked = IndexingOps.Stack(outs, axis: 0);
        var output = BatchFirst ? stacked.Permute(1, 0, 2).Contiguous() : stacked;
        return (output, h);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => ForwardSeq(input).outputs;
}

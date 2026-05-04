using System;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// 2D Transposed (deconvolution) свёртка. Часто используется для upsampling.
/// </summary>
/// <remarks>
/// <para>
/// Вход: (N, C_in, H, W); Веса: (C_in, C_out/groups, kH, kW); Выход: (N, C_out, H_out, W_out).
/// </para>
/// <para>
/// H_out = (H-1) * stride - 2*padding + dilation*(kH-1) + output_padding + 1.
/// </para>
/// <para>
/// CPU-реализация: SGEMM + col2im для forward, im2col + SGEMM для backward
/// (через OpenBLAS при доступности). Прямые циклы для depthwise и микро-ядер.
/// </para>
/// </remarks>
public sealed class ConvTranspose2d : Module
{
    /// <summary>Каналы входа.</summary>
    public int InChannels { get; }
    /// <summary>Каналы выхода.</summary>
    public int OutChannels { get; }
    /// <summary>(kH, kW).</summary>
    public (int H, int W) KernelSize { get; }
    /// <summary>Шаг.</summary>
    public (int H, int W) Stride { get; }
    /// <summary>Padding (вычитается из выхода).</summary>
    public (int H, int W) Padding { get; }
    /// <summary>OutputPadding.</summary>
    public (int H, int W) OutputPadding { get; }
    /// <summary>Dilation.</summary>
    public (int H, int W) Dilation { get; }
    /// <summary>Groups.</summary>
    public int Groups { get; }

    /// <summary>weight (C_in, C_out/groups, kH, kW).</summary>
    public Parameter Weight { get; }
    /// <summary>bias (C_out) или null.</summary>
    public Parameter Bias { get; }

    /// <summary>Создать ConvTranspose2d.</summary>
    public ConvTranspose2d(int inChannels, int outChannels, int kernelSize,
        int stride = 1, int padding = 0, int outputPadding = 0,
        int dilation = 1, int groups = 1, bool bias = true, Random rng = null)
        : this(inChannels, outChannels, (kernelSize, kernelSize), (stride, stride),
               (padding, padding), (outputPadding, outputPadding),
               (dilation, dilation), groups, bias, rng) { }

    /// <summary>Создать с кортежами.</summary>
    public ConvTranspose2d(int inChannels, int outChannels,
        (int H, int W) kernelSize, (int H, int W) stride,
        (int H, int W) padding, (int H, int W) outputPadding,
        (int H, int W) dilation, int groups, bool bias, Random rng)
    {
        if (inChannels <= 0 || outChannels <= 0) throw new ArgumentOutOfRangeException();
        if (groups <= 0) throw new ArgumentOutOfRangeException(nameof(groups));
        if (inChannels % groups != 0) throw new ArgumentException("in_channels % groups != 0.");
        if (outChannels % groups != 0) throw new ArgumentException("out_channels % groups != 0.");

        InChannels = inChannels; OutChannels = outChannels;
        KernelSize = kernelSize; Stride = stride; Padding = padding;
        OutputPadding = outputPadding; Dilation = dilation; Groups = groups;

        var w = Tensor.Empty(new Shape(inChannels, outChannels / groups, kernelSize.H, kernelSize.W));
        Init.KaimingUniform_(w, a: MathF.Sqrt(5f), rng: rng, groups: groups);
        Weight = RegisterParameter("weight", w);

        if (bias)
        {
            int fanIn = (outChannels / groups) * kernelSize.H * kernelSize.W;
            float bound = 1f / MathF.Sqrt(fanIn);
            var b = Tensor.Empty(new Shape(outChannels));
            Init.Uniform_(b, -bound, bound, rng);
            Bias = RegisterParameter("bias", b);
        }
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (input.Rank != 4)
            throw new ArgumentException("ConvTranspose2d ожидает (N, C, H, W).");
        if (input.Shape[1] != InChannels)
            throw new ArgumentException($"ConvTranspose2d: канал={input.Shape[1]}, ожидалось {InChannels}.");
        return Apply(input, Weight.Tensor, Bias?.Tensor,
            Stride, Padding, OutputPadding, Dilation, Groups);
    }

    /// <summary>Функциональная форма.</summary>
    public static Tensor Apply(Tensor input, Tensor weight, Tensor bias,
        (int H, int W) stride, (int H, int W) padding, (int H, int W) outputPadding,
        (int H, int W) dilation, int groups)
    {
        int N = input.Shape[0], Cin = input.Shape[1], H = input.Shape[2], W = input.Shape[3];
        int CoutPerG = weight.Shape[1];
        int Cout = CoutPerG * groups;
        int kH = weight.Shape[2], kW = weight.Shape[3];
        int CinPerG = Cin / groups;

        int Hout = (H - 1) * stride.H - 2 * padding.H + dilation.H * (kH - 1) + outputPadding.H + 1;
        int Wout = (W - 1) * stride.W - 2 * padding.W + dilation.W * (kW - 1) + outputPadding.W + 1;
        if (Hout <= 0 || Wout <= 0)
            throw new ArgumentException($"ConvTranspose2d: некорректный выход {Hout}x{Wout}.");

        var x = input.Contiguous();
        var w = weight.Contiguous();
        var y = Tensor.Zeros(new Shape(N, Cout, Hout, Wout), input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ws = w.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();

        bool isDepthwise = groups > 1 && CinPerG == 1;
        bool useBlas = !isDepthwise &&
            CpuBlas.ShouldUseBlas((long)CoutPerG * kH * kW, (long)H * W, CinPerG);

        if (useBlas)
        {
            // Forward: cols = weight_group^T @ input_flat, then col2im -> y
            // weight_group: (CinPerG, CoutPerG*kH*kW)
            // input_flat:   (CinPerG, H*W)
            // cols:          (CoutPerG*kH*kW, H*W)
            int colLen = CoutPerG * kH * kW * H * W;
            var colsBuf = new float[colLen];

            for (int n = 0; n < N; n++)
            for (int g = 0; g < groups; g++)
            {
                int xOff = n * Cin * H * W + g * CinPerG * H * W;
                int yOff = n * Cout * Hout * Wout + g * CoutPerG * Hout * Wout;
                int wOff = g * CinPerG * CoutPerG * kH * kW;

                Array.Clear(colsBuf, 0, colLen);
                CpuBlas.Sgemm(
                    ws.Slice(wOff, CinPerG * CoutPerG * kH * kW),
                    xs.Slice(xOff, CinPerG * H * W),
                    colsBuf,
                    CoutPerG * kH * kW, H * W, CinPerG,
                    transA: true);

                CpuBlas.Col2Im(new ReadOnlySpan<float>(colsBuf),
                    ys.Slice(yOff, CoutPerG * Hout * Wout),
                    CoutPerG, Hout, Wout, kH, kW,
                    stride.H, stride.W, padding.H, padding.W, dilation.H, dilation.W);
            }
        }
        else
        {
            for (int n = 0; n < N; n++)
            {
                int xN = n * Cin * H * W;
                int yN = n * Cout * Hout * Wout;
                for (int g = 0; g < groups; g++)
                {
                    int xG = xN + g * CinPerG * H * W;
                    int yG = yN + g * CoutPerG * Hout * Wout;
                    int wG = g * CinPerG * CoutPerG * kH * kW;
                    for (int ci = 0; ci < CinPerG; ci++)
                    {
                        int xC = xG + ci * H * W;
                        int wC = wG + ci * CoutPerG * kH * kW;
                        for (int hi = 0; hi < H; hi++)
                        {
                            int xRow = xC + hi * W;
                            for (int wi = 0; wi < W; wi++)
                            {
                                float xv = xs[xRow + wi];
                                int hStart = hi * stride.H - padding.H;
                                int wStart = wi * stride.W - padding.W;
                                for (int co = 0; co < CoutPerG; co++)
                                {
                                    int yC = yG + co * Hout * Wout;
                                    int wCo = wC + co * kH * kW;
                                    for (int kh = 0; kh < kH; kh++)
                                    {
                                        int oh = hStart + kh * dilation.H;
                                        if ((uint)oh >= (uint)Hout) continue;
                                        int wRow = wCo + kh * kW;
                                        int yRow = yC + oh * Wout;
                                        for (int kw = 0; kw < kW; kw++)
                                        {
                                            int ow = wStart + kw * dilation.W;
                                            if ((uint)ow >= (uint)Wout) continue;
                                            ys[yRow + ow] += xv * ws[wRow + kw];
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (bias != null)
        {
            var bs = bias.Contiguous().AsReadOnlySpan<float>();
            for (int n = 0; n < N; n++)
            for (int co = 0; co < Cout; co++)
            {
                int yoff = n * Cout * Hout * Wout + co * Hout * Wout;
                float bv = bs[co];
                for (int k = 0; k < Hout * Wout; k++) ys[yoff + k] += bv;
            }
        }

        bool requiresGrad = TapeContext.IsGradEnabled &&
                            (input.RequiresGrad || weight.RequiresGrad || (bias?.RequiresGrad ?? false));
        if (requiresGrad)
        {
            var fn = new ConvTranspose2dFunction(x, w, bias,
                stride, padding, outputPadding, dilation, groups,
                N, Cin, H, W, Cout, CoutPerG, kH, kW, CinPerG, Hout, Wout);
            fn.RegisterInput(input);
            fn.RegisterInput(weight);
            if (bias != null) fn.RegisterInput(bias);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class ConvTranspose2dFunction : Function
    {
        private readonly Tensor _x, _w, _b;
        private readonly (int H, int W) _stride, _padding, _outputPadding, _dilation;
        private readonly int _groups, _N, _Cin, _H, _W, _Cout, _CoutPerG, _kH, _kW, _CinPerG, _Hout, _Wout;

        public ConvTranspose2dFunction(Tensor x, Tensor w, Tensor b,
            (int H, int W) stride, (int H, int W) padding, (int H, int W) outputPadding,
            (int H, int W) dilation, int groups,
            int N, int Cin, int H, int W, int Cout, int CoutPerG, int kH, int kW, int CinPerG, int Hout, int Wout)
        {
            _x = x; _w = w; _b = b;
            _stride = stride; _padding = padding; _outputPadding = outputPadding; _dilation = dilation;
            _groups = groups; _N = N; _Cin = Cin; _H = H; _W = W; _Cout = Cout;
            _CoutPerG = CoutPerG; _kH = kH; _kW = kW; _CinPerG = CinPerG; _Hout = Hout; _Wout = Wout;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();

            Tensor dx = null, dw = null, db = null;
            Span<float> dxs = default, dws = default, dbs = default;
            if (_x.RequiresGrad) { dx = Tensor.Zeros(_x.Shape, _x.DType, _x.Device); dxs = dx.AsSpan<float>(); }
            if (_w.RequiresGrad) { dw = Tensor.Zeros(_w.Shape, _w.DType, _w.Device); dws = dw.AsSpan<float>(); }
            if (_b != null && _b.RequiresGrad) { db = Tensor.Zeros(_b.Shape, _b.DType, _b.Device); dbs = db.AsSpan<float>(); }

            bool isDepthwise = _groups > 1 && _CinPerG == 1;
            bool useBlas = !isDepthwise &&
                CpuBlas.ShouldUseBlas((long)_CoutPerG * _kH * _kW, (long)_H * _W, _CinPerG);

            if (useBlas)
            {
                // Backward uses im2col on gradOutput, then GEMM for dX and dW.
                // im2col(gy_group) -> gy_cols: (CoutPerG*kH*kW, H*W)
                // dX_flat = weight_group @ gy_cols
                // dW_group += input_flat @ gy_cols^T
                var xs = _x.AsReadOnlySpan<float>();
                var ws = _w.AsReadOnlySpan<float>();
                int colLen = _CoutPerG * _kH * _kW * _H * _W;
                var gyColsBuf = new float[colLen];

                for (int n = 0; n < _N; n++)
                for (int g = 0; g < _groups; g++)
                {
                    int xOff = n * _Cin * _H * _W + g * _CinPerG * _H * _W;
                    int yOff = n * _Cout * _Hout * _Wout + g * _CoutPerG * _Hout * _Wout;
                    int wOff = g * _CinPerG * _CoutPerG * _kH * _kW;

                    CpuBlas.Im2Col(gys.Slice(yOff, _CoutPerG * _Hout * _Wout), gyColsBuf,
                        _CoutPerG, _Hout, _Wout, _kH, _kW,
                        _stride.H, _stride.W, _padding.H, _padding.W, _dilation.H, _dilation.W);

                    if (!dxs.IsEmpty)
                    {
                        CpuBlas.Sgemm(
                            ws.Slice(wOff, _CinPerG * _CoutPerG * _kH * _kW),
                            new ReadOnlySpan<float>(gyColsBuf),
                            dxs.Slice(xOff, _CinPerG * _H * _W),
                            _CinPerG, _H * _W, _CoutPerG * _kH * _kW);
                    }

                    if (!dws.IsEmpty)
                    {
                        CpuBlas.Sgemm(
                            xs.Slice(xOff, _CinPerG * _H * _W),
                            new ReadOnlySpan<float>(gyColsBuf),
                            dws.Slice(wOff, _CinPerG * _CoutPerG * _kH * _kW),
                            _CinPerG, _CoutPerG * _kH * _kW, _H * _W,
                            transB: true, beta: 1f);
                    }
                }

                if (!dbs.IsEmpty)
                {
                    for (int n = 0; n < _N; n++)
                    for (int co = 0; co < _Cout; co++)
                    {
                        int yC = n * _Cout * _Hout * _Wout + co * _Hout * _Wout;
                        for (int k = 0; k < _Hout * _Wout; k++)
                            dbs[co] += gys[yC + k];
                    }
                }
            }
            else
            {
                var xs = _x.AsReadOnlySpan<float>();
                var ws = _w.AsReadOnlySpan<float>();

                for (int n = 0; n < _N; n++)
                {
                    int xN = n * _Cin * _H * _W;
                    int yN = n * _Cout * _Hout * _Wout;
                    for (int g = 0; g < _groups; g++)
                    {
                        int xG = xN + g * _CinPerG * _H * _W;
                        int yG = yN + g * _CoutPerG * _Hout * _Wout;
                        int wG = g * _CinPerG * _CoutPerG * _kH * _kW;
                        for (int ci = 0; ci < _CinPerG; ci++)
                        {
                            int xC = xG + ci * _H * _W;
                            int wC = wG + ci * _CoutPerG * _kH * _kW;
                            for (int hi = 0; hi < _H; hi++)
                            {
                                int xRow = xC + hi * _W;
                                for (int wi = 0; wi < _W; wi++)
                                {
                                    float xv = xs[xRow + wi];
                                    int hStart = hi * _stride.H - _padding.H;
                                    int wStart = wi * _stride.W - _padding.W;
                                    for (int co = 0; co < _CoutPerG; co++)
                                    {
                                        int yC = yG + co * _Hout * _Wout;
                                        int wCo = wC + co * _kH * _kW;
                                        for (int kh = 0; kh < _kH; kh++)
                                        {
                                            int oh = hStart + kh * _dilation.H;
                                            if ((uint)oh >= (uint)_Hout) continue;
                                            int wRow = wCo + kh * _kW;
                                            int yRow = yC + oh * _Wout;
                                            for (int kw = 0; kw < _kW; kw++)
                                            {
                                                int ow = wStart + kw * _dilation.W;
                                                if ((uint)ow >= (uint)_Wout) continue;
                                                float gyv = gys[yRow + ow];
                                                float wv = ws[wRow + kw];
                                                if (!dxs.IsEmpty) dxs[xRow + wi] += gyv * wv;
                                                if (!dws.IsEmpty) dws[wRow + kw] += xv * gyv;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (!dbs.IsEmpty)
                {
                    for (int n = 0; n < _N; n++)
                    for (int co = 0; co < _Cout; co++)
                    {
                        int yC = n * _Cout * _Hout * _Wout + co * _Hout * _Wout;
                        for (int k = 0; k < _Hout * _Wout; k++)
                            dbs[co] += gys[yC + k];
                    }
                }
            }

            int outArity = 2 + (_b != null ? 1 : 0);
            var grads = new Tensor[outArity];
            grads[0] = dx;
            grads[1] = dw;
            if (_b != null) grads[2] = db;
            return grads;
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"ConvTranspose2d({InChannels}, {OutChannels}, k={KernelSize}, s={Stride}, p={Padding}, o={OutputPadding})";
}

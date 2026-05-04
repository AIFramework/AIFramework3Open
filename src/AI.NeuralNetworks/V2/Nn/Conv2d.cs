using System;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// 2D-свёртка (PyTorch-стиль) с поддержкой stride, padding, dilation, groups.
/// </summary>
/// <remarks>
/// <para>Вход: (N, C_in, H, W); веса: (C_out, C_in/groups, kH, kW); выход: (N, C_out, H_out, W_out).</para>
/// <para>
/// CPU-реализация: im2col + OpenBLAS SGEMM для стандартных свёрток,
/// прямые циклы для depthwise и микро-ядер (ниже GEMM-порога).
/// Backward аналогично через SGEMM + col2im.
/// </para>
/// </remarks>
public sealed class Conv2d : Module
{
    /// <summary>Каналы входа.</summary>
    public int InChannels { get; }
    /// <summary>Каналы выхода.</summary>
    public int OutChannels { get; }
    /// <summary>(kH, kW).</summary>
    public (int H, int W) KernelSize { get; }
    /// <summary>(sH, sW).</summary>
    public (int H, int W) Stride { get; }
    /// <summary>(padH, padW).</summary>
    public (int H, int W) Padding { get; }
    /// <summary>(dH, dW).</summary>
    public (int H, int W) Dilation { get; }
    /// <summary>Число групп (in/out должны быть кратны).</summary>
    public int Groups { get; }

    /// <summary>weight (C_out, C_in/groups, kH, kW).</summary>
    public Parameter Weight { get; }
    /// <summary>bias (C_out) или null.</summary>
    public Parameter Bias { get; }

    /// <summary>Создать Conv2d.</summary>
    public Conv2d(int inChannels, int outChannels, int kernelSize,
        int stride = 1, int padding = 0, int dilation = 1, int groups = 1, bool bias = true,
        Random rng = null)
        : this(inChannels, outChannels, (kernelSize, kernelSize), (stride, stride),
               (padding, padding), (dilation, dilation), groups, bias, rng) { }

    /// <summary>Создать Conv2d c полным набором гиперпараметров.</summary>
    public Conv2d(int inChannels, int outChannels,
        (int H, int W) kernelSize, (int H, int W) stride, (int H, int W) padding,
        (int H, int W) dilation, int groups, bool bias, Random rng)
    {
        if (inChannels <= 0 || outChannels <= 0) throw new ArgumentOutOfRangeException();
        if (groups <= 0) throw new ArgumentOutOfRangeException(nameof(groups));
        if (inChannels % groups != 0) throw new ArgumentException("in_channels % groups != 0.");
        if (outChannels % groups != 0) throw new ArgumentException("out_channels % groups != 0.");
        if (kernelSize.H <= 0 || kernelSize.W <= 0) throw new ArgumentException("kernelSize > 0.");
        if (stride.H <= 0 || stride.W <= 0) throw new ArgumentException("stride > 0.");
        if (padding.H < 0 || padding.W < 0) throw new ArgumentException("padding >= 0.");
        if (dilation.H <= 0 || dilation.W <= 0) throw new ArgumentException("dilation > 0.");

        InChannels = inChannels;
        OutChannels = outChannels;
        KernelSize = kernelSize;
        Stride = stride;
        Padding = padding;
        Dilation = dilation;
        Groups = groups;

        var w = Tensor.Empty(new Shape(outChannels, inChannels / groups, kernelSize.H, kernelSize.W));
        Init.KaimingUniform_(w, a: MathF.Sqrt(5f), rng: rng, groups: groups);
        Weight = RegisterParameter("weight", w);

        if (bias)
        {
            int fanIn = (inChannels / groups) * kernelSize.H * kernelSize.W;
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
            throw new ArgumentException($"Conv2d ожидает (N, C, H, W), получено rank={input.Rank}.");
        if (input.Shape[1] != InChannels)
            throw new ArgumentException(
                $"Conv2d: канал={input.Shape[1]}, ожидалось {InChannels}.");

        return Apply(input, Weight.Tensor, Bias?.Tensor,
            Stride, Padding, Dilation, Groups);
    }

    /// <summary>Функциональная форма свёртки.</summary>
    public static Tensor Apply(Tensor input, Tensor weight, Tensor bias,
        (int H, int W) stride, (int H, int W) padding, (int H, int W) dilation, int groups)
    {
        int N = input.Shape[0], C = input.Shape[1], H = input.Shape[2], W = input.Shape[3];
        int Cout = weight.Shape[0];
        int CinPerG = weight.Shape[1];
        int kH = weight.Shape[2], kW = weight.Shape[3];
        int CoutPerG = Cout / groups;

        int Hout = (H + 2 * padding.H - dilation.H * (kH - 1) - 1) / stride.H + 1;
        int Wout = (W + 2 * padding.W - dilation.W * (kW - 1) - 1) / stride.W + 1;
        if (Hout <= 0 || Wout <= 0)
            throw new ArgumentException(
                $"Conv2d: некорректный выход H_out={Hout}, W_out={Wout}.");

        var x = input.Contiguous();
        var w = weight.Contiguous();
        var y = Tensor.Zeros(new Shape(N, Cout, Hout, Wout), input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ws = w.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();

        bool isDepthwise = groups > 1 && CinPerG == 1;
        bool useBlas = !isDepthwise &&
            CpuBlas.ShouldUseBlas(CoutPerG, (long)Hout * Wout, (long)CinPerG * kH * kW);

        if (useBlas)
        {
            int colLen = CinPerG * kH * kW * Hout * Wout;
            var colsBuf = new float[colLen];

            for (int n = 0; n < N; n++)
            for (int g = 0; g < groups; g++)
            {
                int xOff = n * C * H * W + g * CinPerG * H * W;
                int yOff = n * Cout * Hout * Wout + g * CoutPerG * Hout * Wout;
                int wOff = g * CoutPerG * CinPerG * kH * kW;

                CpuBlas.Im2Col(xs.Slice(xOff, CinPerG * H * W), colsBuf,
                    CinPerG, H, W, kH, kW,
                    stride.H, stride.W, padding.H, padding.W, dilation.H, dilation.W);

                CpuBlas.Sgemm(
                    ws.Slice(wOff, CoutPerG * CinPerG * kH * kW),
                    new ReadOnlySpan<float>(colsBuf),
                    ys.Slice(yOff, CoutPerG * Hout * Wout),
                    CoutPerG, Hout * Wout, CinPerG * kH * kW);
            }
        }
        else
        {
            for (int n = 0; n < N; n++)
            {
                int xN = n * C * H * W;
                int yN = n * Cout * Hout * Wout;
                for (int g = 0; g < groups; g++)
                {
                    int xG = xN + g * CinPerG * H * W;
                    int yG = yN + g * CoutPerG * Hout * Wout;
                    int wG = g * CoutPerG * CinPerG * kH * kW;

                    for (int co = 0; co < CoutPerG; co++)
                    {
                        int yC = yG + co * Hout * Wout;
                        int wC = wG + co * CinPerG * kH * kW;
                        for (int ho = 0; ho < Hout; ho++)
                        {
                            int hStart = ho * stride.H - padding.H;
                            for (int wo = 0; wo < Wout; wo++)
                            {
                                int wStart = wo * stride.W - padding.W;
                                float acc = 0f;
                                for (int ci = 0; ci < CinPerG; ci++)
                                {
                                    int xCh = xG + ci * H * W;
                                    int wCh = wC + ci * kH * kW;
                                    for (int kh = 0; kh < kH; kh++)
                                    {
                                        int ih = hStart + kh * dilation.H;
                                        if ((uint)ih >= (uint)H) continue;
                                        int xRow = xCh + ih * W;
                                        int wRow = wCh + kh * kW;
                                        for (int kw = 0; kw < kW; kw++)
                                        {
                                            int iw = wStart + kw * dilation.W;
                                            if ((uint)iw >= (uint)W) continue;
                                            acc += xs[xRow + iw] * ws[wRow + kw];
                                        }
                                    }
                                }
                                ys[yC + ho * Wout + wo] = acc;
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
                float bv = bs[co];
                int yOff = n * Cout * Hout * Wout + co * Hout * Wout;
                for (int j = 0; j < Hout * Wout; j++)
                    ys[yOff + j] += bv;
            }
        }

        bool requiresGrad = TapeContext.IsGradEnabled &&
                            (input.RequiresGrad || weight.RequiresGrad || (bias?.RequiresGrad ?? false));
        if (requiresGrad)
        {
            var fn = new Conv2dFunction(x, w, bias,
                stride, padding, dilation, groups,
                N, C, H, W, Cout, CinPerG, kH, kW, CoutPerG, Hout, Wout);
            fn.RegisterInput(input);
            fn.RegisterInput(weight);
            if (bias != null) fn.RegisterInput(bias);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class Conv2dFunction : Function
    {
        private readonly Tensor _x, _w, _b;
        private readonly (int H, int W) _stride, _padding, _dilation;
        private readonly int _groups, _N, _C, _H, _W, _Cout, _CinPerG, _kH, _kW, _CoutPerG, _Hout, _Wout;

        public Conv2dFunction(Tensor x, Tensor w, Tensor b,
            (int H, int W) stride, (int H, int W) padding, (int H, int W) dilation, int groups,
            int N, int C, int H, int W, int Cout, int CinPerG, int kH, int kW, int CoutPerG, int Hout, int Wout)
        {
            _x = x; _w = w; _b = b;
            _stride = stride; _padding = padding; _dilation = dilation; _groups = groups;
            _N = N; _C = C; _H = H; _W = W; _Cout = Cout; _CinPerG = CinPerG;
            _kH = kH; _kW = kW; _CoutPerG = CoutPerG; _Hout = Hout; _Wout = Wout;
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
                CpuBlas.ShouldUseBlas(_CoutPerG, (long)_Hout * _Wout, (long)_CinPerG * _kH * _kW);

            if (useBlas)
            {
                var xs = _x.AsReadOnlySpan<float>();
                var ws = _w.AsReadOnlySpan<float>();
                int colLen = _CinPerG * _kH * _kW * _Hout * _Wout;
                var colsBuf = new float[colLen];

                for (int n = 0; n < _N; n++)
                for (int g = 0; g < _groups; g++)
                {
                    int xOff = n * _C * _H * _W + g * _CinPerG * _H * _W;
                    int yOff = n * _Cout * _Hout * _Wout + g * _CoutPerG * _Hout * _Wout;
                    int wOff = g * _CoutPerG * _CinPerG * _kH * _kW;

                    CpuBlas.Im2Col(xs.Slice(xOff, _CinPerG * _H * _W), colsBuf,
                        _CinPerG, _H, _W, _kH, _kW,
                        _stride.H, _stride.W, _padding.H, _padding.W, _dilation.H, _dilation.W);

                    if (!dws.IsEmpty)
                    {
                        CpuBlas.Sgemm(
                            gys.Slice(yOff, _CoutPerG * _Hout * _Wout),
                            new ReadOnlySpan<float>(colsBuf),
                            dws.Slice(wOff, _CoutPerG * _CinPerG * _kH * _kW),
                            _CoutPerG, _CinPerG * _kH * _kW, _Hout * _Wout,
                            transB: true, beta: 1f);
                    }

                    if (!dxs.IsEmpty)
                    {
                        var dcolsBuf = new float[colLen];
                        CpuBlas.Sgemm(
                            ws.Slice(wOff, _CoutPerG * _CinPerG * _kH * _kW),
                            gys.Slice(yOff, _CoutPerG * _Hout * _Wout),
                            dcolsBuf,
                            _CinPerG * _kH * _kW, _Hout * _Wout, _CoutPerG,
                            transA: true);

                        CpuBlas.Col2Im(new ReadOnlySpan<float>(dcolsBuf),
                            dxs.Slice(xOff, _CinPerG * _H * _W),
                            _CinPerG, _H, _W, _kH, _kW,
                            _stride.H, _stride.W, _padding.H, _padding.W, _dilation.H, _dilation.W);
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
                    int xN = n * _C * _H * _W;
                    int yN = n * _Cout * _Hout * _Wout;
                    for (int g = 0; g < _groups; g++)
                    {
                        int xG = xN + g * _CinPerG * _H * _W;
                        int yG = yN + g * _CoutPerG * _Hout * _Wout;
                        int wG = g * _CoutPerG * _CinPerG * _kH * _kW;

                        for (int co = 0; co < _CoutPerG; co++)
                        {
                            int yC = yG + co * _Hout * _Wout;
                            int wC = wG + co * _CinPerG * _kH * _kW;
                            for (int ho = 0; ho < _Hout; ho++)
                            {
                                int hStart = ho * _stride.H - _padding.H;
                                for (int wo = 0; wo < _Wout; wo++)
                                {
                                    int wStart = wo * _stride.W - _padding.W;
                                    float gy = gys[yC + ho * _Wout + wo];
                                    if (!dbs.IsEmpty) dbs[g * _CoutPerG + co] += gy;
                                    for (int ci = 0; ci < _CinPerG; ci++)
                                    {
                                        int xCh = xG + ci * _H * _W;
                                        int wCh = wC + ci * _kH * _kW;
                                        for (int kh = 0; kh < _kH; kh++)
                                        {
                                            int ih = hStart + kh * _dilation.H;
                                            if ((uint)ih >= (uint)_H) continue;
                                            int xRow = xCh + ih * _W;
                                            int wRow = wCh + kh * _kW;
                                            for (int kw = 0; kw < _kW; kw++)
                                            {
                                                int iw = wStart + kw * _dilation.W;
                                                if ((uint)iw >= (uint)_W) continue;
                                                float xv = xs[xRow + iw];
                                                float wv = ws[wRow + kw];
                                                if (!dxs.IsEmpty) dxs[xRow + iw] += wv * gy;
                                                if (!dws.IsEmpty) dws[wRow + kw] += xv * gy;
                                            }
                                        }
                                    }
                                }
                            }
                        }
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
        $"Conv2d({InChannels}, {OutChannels}, k={KernelSize.H}x{KernelSize.W}, " +
        $"s={Stride.H}x{Stride.W}, p={Padding.H}x{Padding.W}, d={Dilation.H}x{Dilation.W}, g={Groups})";
}

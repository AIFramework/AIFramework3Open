using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>2D Max Pooling.</summary>
public sealed class MaxPool2d : Module
{
    /// <summary>Размер окна (kH, kW).</summary>
    public (int H, int W) KernelSize { get; }
    /// <summary>Шаг.</summary>
    public (int H, int W) Stride { get; }
    /// <summary>Паддинг.</summary>
    public (int H, int W) Padding { get; }

    /// <summary>Создать MaxPool2d.</summary>
    public MaxPool2d(int kernelSize, int? stride = null, int padding = 0)
        : this((kernelSize, kernelSize),
               stride is int s ? (s, s) : (kernelSize, kernelSize),
               (padding, padding)) { }

    /// <summary>Создать MaxPool2d c кортежами.</summary>
    public MaxPool2d((int H, int W) kernelSize, (int H, int W) stride, (int H, int W) padding)
    {
        if (kernelSize.H <= 0 || kernelSize.W <= 0) throw new ArgumentException();
        if (stride.H <= 0 || stride.W <= 0) throw new ArgumentException();
        if (padding.H < 0 || padding.W < 0) throw new ArgumentException();
        KernelSize = kernelSize; Stride = stride; Padding = padding;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        Pool2dFunctional.MaxPool(input, KernelSize, Stride, Padding);

    /// <inheritdoc/>
    public override string ToString() =>
        $"MaxPool2d(k={KernelSize.H}x{KernelSize.W}, s={Stride.H}x{Stride.W}, p={Padding.H}x{Padding.W})";
}

/// <summary>2D Average Pooling.</summary>
public sealed class AvgPool2d : Module
{
    /// <summary>Размер окна.</summary>
    public (int H, int W) KernelSize { get; }
    /// <summary>Шаг.</summary>
    public (int H, int W) Stride { get; }
    /// <summary>Паддинг.</summary>
    public (int H, int W) Padding { get; }

    /// <summary>Создать AvgPool2d.</summary>
    public AvgPool2d(int kernelSize, int? stride = null, int padding = 0)
        : this((kernelSize, kernelSize),
               stride is int s ? (s, s) : (kernelSize, kernelSize),
               (padding, padding)) { }

    /// <summary>Создать AvgPool2d c кортежами.</summary>
    public AvgPool2d((int H, int W) kernelSize, (int H, int W) stride, (int H, int W) padding)
    {
        if (kernelSize.H <= 0 || kernelSize.W <= 0) throw new ArgumentException();
        if (stride.H <= 0 || stride.W <= 0) throw new ArgumentException();
        if (padding.H < 0 || padding.W < 0) throw new ArgumentException();
        KernelSize = kernelSize; Stride = stride; Padding = padding;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        Pool2dFunctional.AvgPool(input, KernelSize, Stride, Padding);

    /// <inheritdoc/>
    public override string ToString() =>
        $"AvgPool2d(k={KernelSize.H}x{KernelSize.W}, s={Stride.H}x{Stride.W}, p={Padding.H}x{Padding.W})";
}

/// <summary>Адаптивный AvgPool2d к (output_h, output_w).</summary>
public sealed class AdaptiveAvgPool2d : Module
{
    /// <summary>Целевой размер выхода.</summary>
    public (int H, int W) OutputSize { get; }

    /// <summary>Создать AdaptiveAvgPool2d.</summary>
    public AdaptiveAvgPool2d((int H, int W) outputSize) { OutputSize = outputSize; }

    /// <summary>Создать с равными размерами по осям.</summary>
    public AdaptiveAvgPool2d(int output) : this((output, output)) { }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        Pool2dFunctional.AdaptiveAvgPool(input, OutputSize);
}

/// <summary>Низкоуровневый функциональный API пулинга.</summary>
internal static class Pool2dFunctional
{
    public static Tensor MaxPool(Tensor input, (int H, int W) kernel, (int H, int W) stride, (int H, int W) padding)
    {
        if (input.Rank != 4) throw new ArgumentException("MaxPool2d: ожидается (N, C, H, W).");
        int N = input.Shape[0], C = input.Shape[1], H = input.Shape[2], W = input.Shape[3];
        int Hout = (H + 2 * padding.H - kernel.H) / stride.H + 1;
        int Wout = (W + 2 * padding.W - kernel.W) / stride.W + 1;

        var x = input.Contiguous();
        var y = Tensor.Empty(new Shape(N, C, Hout, Wout), input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        // mask[k] = индекс источника в x (для backward).
        var mask = new int[N * C * Hout * Wout];

        for (int n = 0; n < N; n++)
        {
            int xN = n * C * H * W;
            int yN = n * C * Hout * Wout;
            for (int c = 0; c < C; c++)
            {
                int xC = xN + c * H * W;
                int yC = yN + c * Hout * Wout;
                for (int ho = 0; ho < Hout; ho++)
                {
                    int hStart = ho * stride.H - padding.H;
                    for (int wo = 0; wo < Wout; wo++)
                    {
                        int wStart = wo * stride.W - padding.W;
                        float best = float.NegativeInfinity;
                        int bestIdx = -1;
                        for (int kh = 0; kh < kernel.H; kh++)
                        {
                            int ih = hStart + kh;
                            if ((uint)ih >= (uint)H) continue;
                            int xRow = xC + ih * W;
                            for (int kw = 0; kw < kernel.W; kw++)
                            {
                                int iw = wStart + kw;
                                if ((uint)iw >= (uint)W) continue;
                                float v = xs[xRow + iw];
                                if (v > best) { best = v; bestIdx = xRow + iw; }
                            }
                        }
                        ys[yC + ho * Wout + wo] = best == float.NegativeInfinity ? 0f : best;
                        mask[yC + ho * Wout + wo] = bestIdx;
                    }
                }
            }
        }

        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new MaxPoolFunction(x.Shape, mask);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    public static Tensor AvgPool(Tensor input, (int H, int W) kernel, (int H, int W) stride, (int H, int W) padding)
    {
        if (input.Rank != 4) throw new ArgumentException("AvgPool2d: ожидается (N, C, H, W).");
        int N = input.Shape[0], C = input.Shape[1], H = input.Shape[2], W = input.Shape[3];
        int Hout = (H + 2 * padding.H - kernel.H) / stride.H + 1;
        int Wout = (W + 2 * padding.W - kernel.W) / stride.W + 1;

        var x = input.Contiguous();
        var y = Tensor.Empty(new Shape(N, C, Hout, Wout), input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        float invK = 1f / (kernel.H * kernel.W);

        for (int n = 0; n < N; n++)
        {
            int xN = n * C * H * W;
            int yN = n * C * Hout * Wout;
            for (int c = 0; c < C; c++)
            {
                int xC = xN + c * H * W;
                int yC = yN + c * Hout * Wout;
                for (int ho = 0; ho < Hout; ho++)
                {
                    int hStart = ho * stride.H - padding.H;
                    for (int wo = 0; wo < Wout; wo++)
                    {
                        int wStart = wo * stride.W - padding.W;
                        float acc = 0f;
                        for (int kh = 0; kh < kernel.H; kh++)
                        {
                            int ih = hStart + kh;
                            if ((uint)ih >= (uint)H) continue;
                            int xRow = xC + ih * W;
                            for (int kw = 0; kw < kernel.W; kw++)
                            {
                                int iw = wStart + kw;
                                if ((uint)iw >= (uint)W) continue;
                                acc += xs[xRow + iw];
                            }
                        }
                        ys[yC + ho * Wout + wo] = acc * invK;
                    }
                }
            }
        }

        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new AvgPoolFunction(x.Shape, kernel, stride, padding, invK);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    public static Tensor AdaptiveAvgPool(Tensor input, (int H, int W) outSize)
    {
        if (input.Rank != 4) throw new ArgumentException("AdaptiveAvgPool2d: ожидается (N, C, H, W).");
        int N = input.Shape[0], C = input.Shape[1], H = input.Shape[2], W = input.Shape[3];
        int Hout = outSize.H, Wout = outSize.W;

        var x = input.Contiguous();
        var y = Tensor.Empty(new Shape(N, C, Hout, Wout), input.DType, input.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        // Бины: PyTorch использует floor/ceil с приближённым равным распределением.
        var hStarts = new int[Hout]; var hEnds = new int[Hout];
        var wStarts = new int[Wout]; var wEnds = new int[Wout];
        for (int i = 0; i < Hout; i++)
        {
            hStarts[i] = (int)Math.Floor((double)i * H / Hout);
            hEnds[i] = (int)Math.Ceiling((double)(i + 1) * H / Hout);
        }
        for (int j = 0; j < Wout; j++)
        {
            wStarts[j] = (int)Math.Floor((double)j * W / Wout);
            wEnds[j] = (int)Math.Ceiling((double)(j + 1) * W / Wout);
        }

        for (int n = 0; n < N; n++)
        for (int c = 0; c < C; c++)
        {
            int xC = n * C * H * W + c * H * W;
            int yC = n * C * Hout * Wout + c * Hout * Wout;
            for (int ho = 0; ho < Hout; ho++)
            {
                int hS = hStarts[ho], hE = hEnds[ho];
                for (int wo = 0; wo < Wout; wo++)
                {
                    int wS = wStarts[wo], wE = wEnds[wo];
                    float acc = 0f;
                    int cnt = 0;
                    for (int ih = hS; ih < hE; ih++)
                    {
                        int xRow = xC + ih * W;
                        for (int iw = wS; iw < wE; iw++) { acc += xs[xRow + iw]; cnt++; }
                    }
                    ys[yC + ho * Wout + wo] = cnt > 0 ? acc / cnt : 0f;
                }
            }
        }

        if (TapeContext.IsGradEnabled && input.RequiresGrad)
        {
            var fn = new AdaptiveAvgPoolFunction(x.Shape, hStarts, hEnds, wStarts, wEnds);
            fn.RegisterInput(input);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class MaxPoolFunction : Function
    {
        private readonly Shape _xShape;
        private readonly int[] _mask;
        public MaxPoolFunction(Shape xShape, int[] mask) { _xShape = xShape; _mask = mask; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dx = Tensor.Zeros(_xShape);
            var dxs = dx.AsSpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            for (int i = 0; i < _mask.Length; i++)
            {
                int src = _mask[i];
                if (src >= 0) dxs[src] += gys[i];
            }
            return new[] { dx };
        }
    }

    private sealed class AvgPoolFunction : Function
    {
        private readonly Shape _xShape;
        private readonly (int H, int W) _kernel, _stride, _padding;
        private readonly float _invK;
        public AvgPoolFunction(Shape xShape, (int H, int W) k, (int H, int W) s, (int H, int W) p, float invK)
        {
            _xShape = xShape; _kernel = k; _stride = s; _padding = p; _invK = invK;
        }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            int N = _xShape[0], C = _xShape[1], H = _xShape[2], W = _xShape[3];
            var dx = Tensor.Zeros(_xShape);
            var dxs = dx.AsSpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            int Hout = (H + 2 * _padding.H - _kernel.H) / _stride.H + 1;
            int Wout = (W + 2 * _padding.W - _kernel.W) / _stride.W + 1;
            for (int n = 0; n < N; n++)
            for (int c = 0; c < C; c++)
            {
                int xC = n * C * H * W + c * H * W;
                int yC = n * C * Hout * Wout + c * Hout * Wout;
                for (int ho = 0; ho < Hout; ho++)
                {
                    int hStart = ho * _stride.H - _padding.H;
                    for (int wo = 0; wo < Wout; wo++)
                    {
                        int wStart = wo * _stride.W - _padding.W;
                        float gy = gys[yC + ho * Wout + wo] * _invK;
                        for (int kh = 0; kh < _kernel.H; kh++)
                        {
                            int ih = hStart + kh;
                            if ((uint)ih >= (uint)H) continue;
                            int xRow = xC + ih * W;
                            for (int kw = 0; kw < _kernel.W; kw++)
                            {
                                int iw = wStart + kw;
                                if ((uint)iw >= (uint)W) continue;
                                dxs[xRow + iw] += gy;
                            }
                        }
                    }
                }
            }
            return new[] { dx };
        }
    }

    private sealed class AdaptiveAvgPoolFunction : Function
    {
        private readonly Shape _xShape;
        private readonly int[] _hS, _hE, _wS, _wE;
        public AdaptiveAvgPoolFunction(Shape s, int[] hS, int[] hE, int[] wS, int[] wE)
        { _xShape = s; _hS = hS; _hE = hE; _wS = wS; _wE = wE; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            int N = _xShape[0], C = _xShape[1], H = _xShape[2], W = _xShape[3];
            int Hout = _hS.Length, Wout = _wS.Length;
            var dx = Tensor.Zeros(_xShape);
            var dxs = dx.AsSpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            for (int n = 0; n < N; n++)
            for (int c = 0; c < C; c++)
            {
                int xC = n * C * H * W + c * H * W;
                int yC = n * C * Hout * Wout + c * Hout * Wout;
                for (int ho = 0; ho < Hout; ho++)
                {
                    int hS = _hS[ho], hE = _hE[ho];
                    for (int wo = 0; wo < Wout; wo++)
                    {
                        int wS = _wS[wo], wE = _wE[wo];
                        int cnt = (hE - hS) * (wE - wS);
                        float gy = gys[yC + ho * Wout + wo] / cnt;
                        for (int ih = hS; ih < hE; ih++)
                        {
                            int xRow = xC + ih * W;
                            for (int iw = wS; iw < wE; iw++) dxs[xRow + iw] += gy;
                        }
                    }
                }
            }
            return new[] { dx };
        }
    }
}

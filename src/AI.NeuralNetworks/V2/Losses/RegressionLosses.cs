using System;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Losses;

/// <summary>
/// Регрессионные функции потерь: MSE, L1, SmoothL1/Huber.
/// </summary>
/// <remarks>
/// Каждая функция возвращает тензор; редукция выполняется параметром <see cref="Reduction"/>.
/// Все потери — autograd-aware через стандартные <see cref="TensorOps"/>.
/// </remarks>
public static class RegressionLosses
{
    /// <summary>MSE: mean((input − target)^2).</summary>
    public static Tensor MSE(Tensor input, Tensor target, Reduction reduction = Reduction.Mean)
    {
        var diff = input - target;
        var sq = diff * diff;
        return Reduce(sq, reduction);
    }

    /// <summary>L1: |input − target|.</summary>
    public static Tensor L1(Tensor input, Tensor target, Reduction reduction = Reduction.Mean)
    {
        var diff = input - target;
        var abs = TensorOps.Abs(diff);
        return Reduce(abs, reduction);
    }

    /// <summary>
    /// Smooth L1 / Huber: <c>0.5*x²/β if |x|&lt;β else |x|−0.5β</c>.
    /// </summary>
    public static Tensor SmoothL1(Tensor input, Tensor target, float beta = 1f,
        Reduction reduction = Reduction.Mean)
    {
        if (beta <= 0) throw new ArgumentException("beta должен быть > 0.");
        var x = input.Contiguous();
        var t = target.Contiguous();
        if (!x.Shape.Equals(t.Shape))
            throw new ArgumentException($"Shape mismatch: {x.Shape} vs {t.Shape}.");
        var y = Tensor.Empty(x.Shape, x.DType, x.Device);
        var xs = x.AsReadOnlySpan<float>();
        var ts = t.AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        for (int i = 0; i < xs.Length; i++)
        {
            float d = xs[i] - ts[i];
            float ad = MathF.Abs(d);
            ys[i] = ad < beta ? 0.5f * d * d / beta : ad - 0.5f * beta;
        }
        if (TapeContext.IsGradEnabled && (input.RequiresGrad || target.RequiresGrad))
        {
            var fn = new SmoothL1Function(x, t, beta, input.RequiresGrad, target.RequiresGrad);
            fn.RegisterInput(input);
            if (target.RequiresGrad) fn.RegisterInput(target);
            y.GradFn = fn;
        }
        return Reduce(y, reduction);
    }

    /// <summary>Huber loss (alias для SmoothL1 с другим именованием параметра delta).</summary>
    public static Tensor Huber(Tensor input, Tensor target, float delta = 1f,
        Reduction reduction = Reduction.Mean) => SmoothL1(input, target, delta, reduction);

    /// <summary>Свернуть тензор поэлементных потерь до скаляра по правилу <paramref name="r"/>.</summary>
    public static Tensor Reduce(Tensor t, Reduction r) => r switch
    {
        Reduction.None => t,
        Reduction.Sum => TensorOps.Sum(t),
        Reduction.Mean => TensorOps.Mean(t),
        _ => throw new ArgumentException($"Неизвестная редукция {r}.")
    };

    private sealed class SmoothL1Function : Function
    {
        private readonly Tensor _x, _t;
        private readonly float _beta;
        private readonly bool _gx, _gt;
        public SmoothL1Function(Tensor x, Tensor t, float beta, bool gx, bool gt)
        { _x = x; _t = t; _beta = beta; _gx = gx; _gt = gt; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            var xs = _x.AsReadOnlySpan<float>();
            var ts = _t.AsReadOnlySpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            var dx = _gx ? Tensor.Empty(_x.Shape, _x.DType, _x.Device) : null;
            var dt = _gt ? Tensor.Empty(_t.Shape, _t.DType, _t.Device) : null;
            Span<float> dxs = dx != null ? dx.AsSpan<float>() : Span<float>.Empty;
            Span<float> dts = dt != null ? dt.AsSpan<float>() : Span<float>.Empty;
            for (int i = 0; i < xs.Length; i++)
            {
                float d = xs[i] - ts[i];
                float ad = MathF.Abs(d);
                float dLdD = ad < _beta ? d / _beta : MathF.Sign(d);
                if (!dxs.IsEmpty) dxs[i] =  dLdD * gys[i];
                if (!dts.IsEmpty) dts[i] = -dLdD * gys[i];
            }
            int n = (_gx ? 1 : 0) + (_gt ? 1 : 0);
            var grads = new Tensor[n];
            int j = 0;
            if (_gx) grads[j++] = dx;
            if (_gt) grads[j++] = dt;
            return grads;
        }
    }
}

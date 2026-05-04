using System;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Losses;

/// <summary>
/// Классификационные функции потерь: CrossEntropy, NLL, BCEWithLogits, KL.
/// </summary>
public static class ClassificationLosses
{
    /// <summary>
    /// CrossEntropy: <c>-sum(target_smooth * log_softmax(input))</c>.
    /// </summary>
    /// <param name="logits">(N, C) или (N, C, ...).</param>
    /// <param name="targets">(N) с индексами классов [0, C). DType должен быть Int32.</param>
    /// <param name="weight">(C) — per-class веса.</param>
    /// <param name="ignoreIndex">Целевой индекс, который пропускается (например, padding).</param>
    /// <param name="labelSmoothing">Коэффициент сглаживания меток ∈ [0, 1).</param>
    /// <param name="reduction">Способ редукции.</param>
    public static Tensor CrossEntropy(Tensor logits, Tensor targets, Tensor weight = null,
        int? ignoreIndex = null, float labelSmoothing = 0f,
        Reduction reduction = Reduction.Mean)
    {
        if (logits == null) throw new ArgumentNullException(nameof(logits));
        if (targets == null) throw new ArgumentNullException(nameof(targets));
        if (logits.Rank < 2) throw new ArgumentException("CrossEntropy: logits должны быть rank ≥ 2.");
        if (logits.Rank != 2)
            throw new NotSupportedException(
                "CrossEntropy: пока поддерживается только rank=2 (N, C). Используйте Reshape в caller.");
        if (targets.Rank != 1 || targets.Shape[0] != logits.Shape[0])
            throw new ArgumentException("targets должен быть (N,) с тем же N, что у logits.");
        if (targets.DType != DType.Int32)
            throw new ArgumentException(
                $"CrossEntropy: targets.DType должен быть Int32, фактически {targets.DType}.");
        if (labelSmoothing < 0 || labelSmoothing >= 1)
            throw new ArgumentException("labelSmoothing должен быть в [0, 1).");

        int N = logits.Shape[0], C = logits.Shape[1];
        var lsm = SoftmaxOps.LogSoftmax(logits, axis: -1);   // (N, C), autograd-aware
        var lsmS = lsm.Contiguous().AsReadOnlySpan<float>();
        var ts = targets.Contiguous().AsReadOnlySpan<int>();
        ReadOnlySpan<float> ws = default;
        if (weight != null)
        {
            if (weight.Shape[0] != C)
                throw new ArgumentException($"CrossEntropy: weight длина должна быть {C}.");
            ws = weight.Contiguous().AsReadOnlySpan<float>();
        }

        // Поэлементные потери: shape (N).
        var loss = Tensor.Empty(new Shape(N), logits.DType, logits.Device);
        var ls = loss.AsSpan<float>();
        var validMask = new bool[N];

        float smoothEach = labelSmoothing / C;
        for (int n = 0; n < N; n++)
        {
            int t = ts[n];
            if (ignoreIndex.HasValue && t == ignoreIndex.Value)
            {
                ls[n] = 0f;
                validMask[n] = false;
                continue;
            }
            if (t < 0 || t >= C)
                throw new ArgumentException($"Класс {t} вне диапазона [0, {C}).");
            float w = !ws.IsEmpty ? ws[t] : 1f;
            float lt = lsmS[n * C + t];
            float val;
            if (labelSmoothing == 0f)
            {
                val = -lt;
            }
            else
            {
                float sum = 0f;
                for (int c = 0; c < C; c++) sum += lsmS[n * C + c];
                val = -((1f - labelSmoothing) * lt + smoothEach * sum);
            }
            ls[n] = val * w;
            validMask[n] = true;
        }

        // Backward: предсчитываем softmax (= exp(lsm)) один раз и сохраняем в Function,
        // чтобы не пересчитывать его повторно (PERF).
        Tensor softmaxCache = null;
        if (TapeContext.IsGradEnabled && logits.RequiresGrad)
        {
            using (TapeContext.NoGrad())
                softmaxCache = SoftmaxOps.Softmax(logits, -1);
            var fn = new CrossEntropyFunction(softmaxCache, targets, C,
                weight, labelSmoothing, validMask);
            fn.RegisterInput(logits);
            loss.GradFn = fn;
        }

        // Редукция; для mean знаменатель = validCount (а не weightSum), как в плане.
        // Веса применяются только внутри слагаемого (через ls[n] *= w).
        if (reduction == Reduction.None) return loss;
        if (reduction == Reduction.Sum) return TensorOps.Sum(loss);

        int validCount = 0;
        for (int n = 0; n < N; n++) if (validMask[n]) validCount++;
        float denom = validCount > 0 ? validCount : 1f;
        return TensorOps.MulScalar(TensorOps.Sum(loss), 1f / denom);
    }

    /// <summary>
    /// NLL Loss: <c>-input[targets]</c>. Ожидает уже log-probabilities на входе.
    /// </summary>
    public static Tensor NLL(Tensor logProb, Tensor targets, Tensor weight = null,
        int? ignoreIndex = null, Reduction reduction = Reduction.Mean)
    {
        if (logProb == null) throw new ArgumentNullException(nameof(logProb));
        if (targets == null) throw new ArgumentNullException(nameof(targets));
        if (logProb.Rank != 2) throw new ArgumentException("NLL: ожидаются (N, C).");
        if (targets.Rank != 1 || targets.Shape[0] != logProb.Shape[0])
            throw new ArgumentException("targets должен быть (N,).");
        if (targets.DType != DType.Int32)
            throw new ArgumentException(
                $"NLL: targets.DType должен быть Int32, фактически {targets.DType}.");
        int N = logProb.Shape[0], C = logProb.Shape[1];
        var ts = targets.Contiguous().AsReadOnlySpan<int>();
        var ps = logProb.Contiguous().AsReadOnlySpan<float>();
        ReadOnlySpan<float> ws = default;
        if (weight != null)
        {
            if (weight.Shape[0] != C)
                throw new ArgumentException($"NLL: weight длина должна быть {C}.");
            ws = weight.Contiguous().AsReadOnlySpan<float>();
        }
        var loss = Tensor.Empty(new Shape(N), logProb.DType, logProb.Device);
        var ls = loss.AsSpan<float>();
        var validMask = new bool[N];
        for (int n = 0; n < N; n++)
        {
            int t = ts[n];
            if (ignoreIndex.HasValue && t == ignoreIndex.Value)
            {
                ls[n] = 0f;
                validMask[n] = false;
                continue;
            }
            if (t < 0 || t >= C)
                throw new ArgumentException($"Класс {t} вне диапазона [0, {C}).");
            float w = !ws.IsEmpty ? ws[t] : 1f;
            ls[n] = -ps[n * C + t] * w;
            validMask[n] = true;
        }
        if (TapeContext.IsGradEnabled && logProb.RequiresGrad)
        {
            var fn = new NLLFunction(logProb.Shape, targets, C, weight, validMask);
            fn.RegisterInput(logProb);
            loss.GradFn = fn;
        }
        if (reduction == Reduction.None) return loss;
        if (reduction == Reduction.Sum) return TensorOps.Sum(loss);
        int validCount = 0;
        for (int n = 0; n < N; n++) if (validMask[n]) validCount++;
        float denom = validCount > 0 ? validCount : 1f;
        return TensorOps.MulScalar(TensorOps.Sum(loss), 1f / denom);
    }

    /// <summary>
    /// Binary Cross Entropy с логитами: численно стабильная формулировка.
    /// <c>L = max(x,0) − x·y + log(1 + exp(−|x|))</c>
    /// С учётом posWeight: <c>L_pw = (1 + (pw − 1) · y) · L</c>.
    /// </summary>
    public static Tensor BCEWithLogits(Tensor logits, Tensor targets, Tensor posWeight = null,
        Reduction reduction = Reduction.Mean)
    {
        if (logits == null) throw new ArgumentNullException(nameof(logits));
        if (targets == null) throw new ArgumentNullException(nameof(targets));
        if (!logits.Shape.Equals(targets.Shape))
            throw new ArgumentException($"Shape mismatch: {logits.Shape} vs {targets.Shape}.");
        var x = logits.Contiguous().AsReadOnlySpan<float>();
        var y = targets.Contiguous().AsReadOnlySpan<float>();
        ReadOnlySpan<float> pw = default;
        if (posWeight != null) pw = posWeight.Contiguous().AsReadOnlySpan<float>();

        var loss = Tensor.Empty(logits.Shape, logits.DType, logits.Device);
        var ls = loss.AsSpan<float>();
        for (int i = 0; i < x.Length; i++)
        {
            float xi = x[i], yi = y[i];
            float maxX = MathF.Max(xi, 0f);
            float val = maxX - xi * yi + MathF.Log(1 + MathF.Exp(-MathF.Abs(xi)));
            if (!pw.IsEmpty)
            {
                int idx = pw.Length == 1 ? 0 : i % pw.Length;
                val *= 1f + (pw[idx] - 1f) * yi;
            }
            ls[i] = val;
        }
        if (TapeContext.IsGradEnabled && (logits.RequiresGrad || targets.RequiresGrad))
        {
            var fn = new BCEWithLogitsFunction(logits, targets, posWeight,
                logits.RequiresGrad, targets.RequiresGrad);
            fn.RegisterInput(logits);
            if (targets.RequiresGrad) fn.RegisterInput(targets);
            loss.GradFn = fn;
        }
        return RegressionLosses.Reduce(loss, reduction);
    }

    /// <summary>
    /// KL-дивергенция: <c>sum(target * (log(target) − input))</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="input"/> ожидается как log-probabilities (как в torch.nn.KLDivLoss).
    /// <paramref name="target"/> — обычные probabilities.
    /// </remarks>
    public static Tensor KLDiv(Tensor input, Tensor target, Reduction reduction = Reduction.Mean)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (!input.Shape.Equals(target.Shape))
            throw new ArgumentException($"Shape mismatch: {input.Shape} vs {target.Shape}.");
        var loss = Tensor.Empty(input.Shape, input.DType, input.Device);
        var xs = input.Contiguous().AsReadOnlySpan<float>();
        var ts = target.Contiguous().AsReadOnlySpan<float>();
        var ls = loss.AsSpan<float>();
        for (int i = 0; i < xs.Length; i++)
        {
            float t = ts[i];
            ls[i] = t > 0 ? t * (MathF.Log(t) - xs[i]) : 0f;
        }
        if (TapeContext.IsGradEnabled && (input.RequiresGrad || target.RequiresGrad))
        {
            var fn = new KLDivFunction(input, target, input.RequiresGrad, target.RequiresGrad);
            fn.RegisterInput(input);
            if (target.RequiresGrad) fn.RegisterInput(target);
            loss.GradFn = fn;
        }
        return RegressionLosses.Reduce(loss, reduction);
    }

    private sealed class CrossEntropyFunction : Function
    {
        // Сохраняем уже подсчитанный softmax (а не пересчитываем в backward — PERF).
        // Целевой тензор и веса — по ссылке, без копирования в массив.
        private readonly Tensor _softmax;
        private readonly Tensor _targets;
        private readonly int _C;
        private readonly Tensor _w;
        private readonly float _smooth;
        private readonly bool[] _valid;

        public CrossEntropyFunction(Tensor softmax, Tensor targets, int c, Tensor w,
            float smooth, bool[] valid)
        {
            _softmax = softmax; _targets = targets; _C = c; _w = w;
            _smooth = smooth; _valid = valid;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            var ss = _softmax.Contiguous().AsReadOnlySpan<float>();
            var ts = _targets.Contiguous().AsReadOnlySpan<int>();
            int N = _softmax.Shape[0];
            var dx = Tensor.Empty(_softmax.Shape, _softmax.DType, _softmax.Device);
            var dxs = dx.AsSpan<float>();
            ReadOnlySpan<float> ws = default;
            if (_w != null) ws = _w.Contiguous().AsReadOnlySpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            float smoothEach = _smooth / _C;
            for (int n = 0; n < N; n++)
            {
                if (!_valid[n])
                {
                    for (int c = 0; c < _C; c++) dxs[n * _C + c] = 0f;
                    continue;
                }
                int t = ts[n];
                float w = !ws.IsEmpty ? ws[t] : 1f;
                float gy = gys[n] * w;
                for (int c = 0; c < _C; c++)
                {
                    float pi = ss[n * _C + c];
                    float yi = (c == t ? 1f : 0f);
                    float tsmooth = (1f - _smooth) * yi + smoothEach;
                    dxs[n * _C + c] = (pi - tsmooth) * gy;
                }
            }
            return new[] { dx };
        }
    }

    private sealed class NLLFunction : Function
    {
        private readonly Shape _shape;
        private readonly Tensor _targets;
        private readonly int _C;
        private readonly Tensor _w;
        private readonly bool[] _valid;
        public NLLFunction(Shape s, Tensor targets, int c, Tensor w, bool[] valid)
        { _shape = s; _targets = targets; _C = c; _w = w; _valid = valid; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            int N = _shape[0];
            var dx = Tensor.Zeros(_shape, gradOutput.DType, gradOutput.Device);
            var dxs = dx.AsSpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            var ts = _targets.Contiguous().AsReadOnlySpan<int>();
            ReadOnlySpan<float> ws = default;
            if (_w != null) ws = _w.Contiguous().AsReadOnlySpan<float>();
            for (int n = 0; n < N; n++)
            {
                if (!_valid[n]) continue;
                int t = ts[n];
                float w = !ws.IsEmpty ? ws[t] : 1f;
                dxs[n * _C + t] = -gys[n] * w;
            }
            return new[] { dx };
        }
    }

    private sealed class BCEWithLogitsFunction : Function
    {
        private readonly Tensor _x, _y, _pw;
        private readonly bool _gx, _gy;
        public BCEWithLogitsFunction(Tensor x, Tensor y, Tensor pw, bool gx, bool gy)
        { _x = x; _y = y; _pw = pw; _gx = gx; _gy = gy; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            var xs = _x.Contiguous().AsReadOnlySpan<float>();
            var ys = _y.Contiguous().AsReadOnlySpan<float>();
            ReadOnlySpan<float> pws = default;
            if (_pw != null) pws = _pw.Contiguous().AsReadOnlySpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            var dx = _gx ? Tensor.Empty(_x.Shape, _x.DType, _x.Device) : null;
            var dy = _gy ? Tensor.Empty(_y.Shape, _y.DType, _y.Device) : null;
            Span<float> dxs = dx != null ? dx.AsSpan<float>() : Span<float>.Empty;
            Span<float> dys = dy != null ? dy.AsSpan<float>() : Span<float>.Empty;
            for (int i = 0; i < xs.Length; i++)
            {
                float xi = xs[i], yi = ys[i], gi = gys[i];
                float sig = 1f / (1f + MathF.Exp(-xi));
                // Базовая стабильная BCE: L = max(x,0) − x*y + log(1 + exp(−|x|)).
                float maxX = MathF.Max(xi, 0f);
                float baseLoss = maxX - xi * yi + MathF.Log(1f + MathF.Exp(-MathF.Abs(xi)));
                float scale = 1f;
                float pwm1 = 0f;
                if (!pws.IsEmpty)
                {
                    int idx = pws.Length == 1 ? 0 : i % pws.Length;
                    pwm1 = pws[idx] - 1f;
                    scale = 1f + pwm1 * yi;
                }
                // ∂L/∂x = (sig − y); умножаем на scale (если pos_weight).
                if (!dxs.IsEmpty) dxs[i] = (sig - yi) * gi * scale;
                if (!dys.IsEmpty)
                {
                    // ∂L_pw/∂y = ∂scale/∂y * baseLoss + scale * ∂L/∂y
                    //          = pwm1 * baseLoss + scale * (-x)
                    dys[i] = (pwm1 * baseLoss + scale * (-xi)) * gi;
                }
            }
            int n = (_gx ? 1 : 0) + (_gy ? 1 : 0);
            var grads = new Tensor[n];
            int j = 0;
            if (_gx) grads[j++] = dx;
            if (_gy) grads[j++] = dy;
            return grads;
        }
    }

    private sealed class KLDivFunction : Function
    {
        private readonly Tensor _input;
        private readonly Tensor _target;
        private readonly bool _gi, _gt;
        public KLDivFunction(Tensor input, Tensor target, bool gi, bool gt)
        { _input = input; _target = target; _gi = gi; _gt = gt; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            var dx = _gi ? Tensor.Empty(_input.Shape, gradOutput.DType, gradOutput.Device) : null;
            var dt = _gt ? Tensor.Empty(_input.Shape, gradOutput.DType, gradOutput.Device) : null;
            Span<float> dxs = dx != null ? dx.AsSpan<float>() : Span<float>.Empty;
            Span<float> dts = dt != null ? dt.AsSpan<float>() : Span<float>.Empty;
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            var ts = _target.Contiguous().AsReadOnlySpan<float>();
            var xs = _input.Contiguous().AsReadOnlySpan<float>();
            for (int i = 0; i < ts.Length; i++)
            {
                float t = ts[i];
                if (!dxs.IsEmpty) dxs[i] = -t * gys[i];
                // ∂[t * (log t − x)]/∂t = log t − x + 1 (для t > 0).
                if (!dts.IsEmpty) dts[i] = t > 0f ? (MathF.Log(t) - xs[i] + 1f) * gys[i] : 0f;
            }
            int n = (_gi ? 1 : 0) + (_gt ? 1 : 0);
            var grads = new Tensor[n];
            int j = 0;
            if (_gi) grads[j++] = dx;
            if (_gt) grads[j++] = dt;
            return grads;
        }
    }
}

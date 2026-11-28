using System;

namespace AI.DSP.Multirate;

/// <summary>
/// Интерполяция Акимы (H. Akima, J. ACM 17(4), 1970) как ядро фильтра Фэрроу: кубический полином Эрмита
/// на [x[0]; x[1]] с наклонами, взвешенными по разностям соседних наклонов; узлы x[−2] … x[3].
/// </summary>
/// <remarks>
/// Наклон в узле i: t_i = (|m_{i+1} − m_i|·m_{i−1} + |m_{i−1} − m_{i−2}|·m_i) / (|m_{i+1} − m_i| + |m_{i−1} − m_{i−2}|),
/// где m_i = x[i+1] − x[i]. При нулевом знаменателе t_i = (m_{i+1} + m_{i−2})/2, как в
/// scipy.interpolate.Akima1DInterpolator (там порог относителен глобальному максимуму весов; в потоке он
/// недоступен, поэтому здесь знаменатель сравнивается с нулём). Ядро нелинейно, но в остальном работает
/// в фильтре Фэрроу так же, как <see cref="LagrangeKernel"/>.
/// </remarks>
public sealed class AkimaKernel : InterpolationKernel
{
    /// <inheritdoc/>
    public override int FirstNode => -2;

    /// <inheritdoc/>
    public override int LastNode => 3;

    /// <summary>
    /// Затраты: 5 разностей узлов, 4 модуля разностей, два наклона (по 2 умножения, 2 сложения и делению),
    /// коэффициенты полинома (3 умножения, 4 сложения) и схема Горнера (3 умножения, 3 сложения).
    /// </summary>
    public override OperationCount Operations(Arithmetic arithmetic, Q16Options options)
        => new(5 + 4 + 4 + 4 + 3, 4 + 3 + 3, arithmetic == Arithmetic.Q16 ? 3 : 0, 2);

    internal override IKernelEvaluator<T> Bind<T>(ISampleArithmetic<T> math) => new Evaluator<T>(math);

    private sealed class Evaluator<T> : IKernelEvaluator<T>
        where T : struct
    {
        private readonly ISampleArithmetic<T> _math;
        private readonly T[] _m = new T[5];

        public Evaluator(ISampleArithmetic<T> math) => _math = math;

        public T Evaluate(ReadOnlySpan<T> nodes, T mu)
        {
            // nodes: x[−2], x[−1], x[0], x[1], x[2], x[3]; m[i] = x[i−1] − x[i−2] в сдвинутой нумерации
            T[] m = _m;
            for (int i = 0; i < 5; i++)
            {
                m[i] = _math.Subtract(nodes[i + 1], nodes[i]);
            }

            T t0 = Slope(m[0], m[1], m[2], m[3]);
            T t1 = Slope(m[1], m[2], m[3], m[4]);
            T s = m[2];

            // y = x0 + t0·μ + (3s − 2t0 − t1)·μ² + (t0 + t1 − 2s)·μ³
            T c2 = _math.Subtract(_math.Subtract(_math.Scale(s, 3), _math.Scale(t0, 2)), t1);
            T c3 = _math.Subtract(_math.Add(t0, t1), _math.Scale(s, 2));
            T y = _math.Add(_math.Multiply(c3, mu), c2);
            y = _math.Add(_math.Multiply(y, mu), t0);
            return _math.Add(_math.Multiply(y, mu), nodes[2]);
        }

        private T Slope(T mPrev2, T mPrev1, T mNext0, T mNext1)
        {
            T w1 = _math.Abs(_math.Subtract(mNext1, mNext0));
            T w2 = _math.Abs(_math.Subtract(mPrev1, mPrev2));
            return _math.WeightedMean(w1, mPrev1, w2, mNext0) ?? _math.Half(_math.Add(mNext1, mPrev2));
        }
    }
}

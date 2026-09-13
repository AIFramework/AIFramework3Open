using System;
using System.Linq;
using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Internal;
using AI.ControlSystems.Observers;
using AI.DataStructs.Algebraic;
using Complex = System.Numerics.Complex;

namespace AI.ControlSystems.Optimal;

/// <summary>
/// LQG: линейная обратная связь по состоянию u = −K x̂ и фильтр Калмана для оценки x̂ (принцип разделения).
/// Один шаг: предсказание КФ по предыдущему управлению, коррекция по измерению, затем u = −K x̂.
/// </summary>
/// <remarks>
/// <para>
/// Регулятор синтезируется по модели методом <see cref="Design"/>: усиление K — из уравнения
/// Риккати управления, усиление фильтра — из двойственного уравнения для шумов, фильтр стартует
/// с установившейся ковариацией. По принципу разделения полюса замкнутой системы — объединение
/// полюсов A − BK и ошибки оценки (I − LC)A, а средняя стоимость на такте
/// <c>J = tr(P·W) + tr(Kᵀ(R + BᵀPB)K·Σ)</c>: стоимость управления при известном состоянии плюс
/// плата за ошибку оценки Σ. Прежде класс только склеивал готовые K и фильтр.
/// </para>
/// <para>
/// Модель шумов: x[k+1] = Ax + Bu + w, y = Cx + v, где w и v — белые шумы с ковариациями W и V.
/// Прямая связь D предполагается нулевой: иначе текущее измерение зависело бы от ещё не
/// вычисленного управления.
/// </para>
/// </remarks>
[Serializable]
public sealed class LqgRegulator
{
    private readonly KalmanFilter _kf;
    private readonly Matrix _k;

    /// <summary>Матрица усиления LQR (u = −K x̂).</summary>
    public Matrix StateFeedbackGain => _k;

    /// <summary>Фильтр Калмана регулятора</summary>
    public KalmanFilter Filter => _kf;

    /// <summary>Установившееся усиление фильтра L; null, если регулятор собран из готовых частей</summary>
    public Matrix EstimatorGain { get; private init; }

    /// <summary>Матрица стоимости P из уравнения Риккати управления</summary>
    public Matrix CostToGo { get; private init; }

    /// <summary>Установившаяся ковариация ошибки оценки после коррекции Σ</summary>
    public Matrix EstimationCovariance { get; private init; }

    /// <summary>Средняя стоимость E[xᵀQx + uᵀRu] на такте в установившемся режиме; NaN без синтеза</summary>
    public double ExpectedCost { get; private init; } = double.NaN;

    /// <summary>Полюса замкнутой системы: регулятора и ошибки оценки; пусто без синтеза</summary>
    public Complex[] ClosedLoopPoles { get; private init; } = [];

    /// <summary>Собирает регулятор из готового фильтра и усиления</summary>
    public LqgRegulator(KalmanFilter kalmanFilter, Matrix stateFeedbackGain)
    {
        _kf = kalmanFilter ?? throw new ArgumentNullException(nameof(kalmanFilter));
        _k = stateFeedbackGain ?? throw new ArgumentNullException(nameof(stateFeedbackGain));
        int n = _kf.StateDimension;
        int m = _kf.InputDimension;
        if (_k.Height != m || _k.Width != n)
            throw new ArgumentException("K должна быть m×n (u = −K x̂).");
    }

    /// <summary>
    /// Синтез LQG по модели и ковариациям шумов
    /// </summary>
    /// <param name="a">A (n×n)</param>
    /// <param name="b">B (n×m)</param>
    /// <param name="c">C (p×n)</param>
    /// <param name="q">Вес состояния Q ≥ 0</param>
    /// <param name="r">Вес управления R &gt; 0</param>
    /// <param name="processNoise">Ковариация шума процесса W</param>
    /// <param name="measurementNoise">Ковариация шума измерения V &gt; 0</param>
    public static LqgRegulator Design(
        Matrix a, Matrix b, Matrix c, Matrix q, Matrix r, Matrix processNoise, Matrix measurementNoise)
    {
        if (a == null || b == null || c == null || q == null || r == null || processNoise == null || measurementNoise == null)
            throw new ArgumentNullException();

        LqrDesign lqr = DiscreteLqr.Design(a, b, q, r);
        SteadyStateKalman estimator = KalmanFilter.SteadyState(a, c, processNoise, measurementNoise);

        var filter = new KalmanFilter(a, b, c, new Matrix(c.Height, b.Width), processNoise, measurementNoise,
            new Vector(a.Height), estimator.PosteriorCovariance);

        Matrix k = lqr.Gain;
        Matrix p = lqr.CostToGo;
        Matrix estimationWeight = k.Transpose() * (r + (b.Transpose() * p * b)) * k;
        double cost = ControlLinAlg.Trace(p * processNoise) + ControlLinAlg.Trace(estimationWeight * estimator.PosteriorCovariance);

        Complex[] estimatorPoles = Eigen.General(a - (estimator.Gain * c * a));

        return new LqgRegulator(filter, k)
        {
            EstimatorGain = estimator.Gain,
            CostToGo = p,
            EstimationCovariance = estimator.PosteriorCovariance,
            ExpectedCost = cost,
            ClosedLoopPoles = lqr.ClosedLoopPoles.Concat(estimatorPoles).ToArray()
        };
    }

    /// <summary>
    /// Шаг регулятора: u_prev — управление, действовавшее на предыдущем интервале; y — текущее измерение.
    /// Возвращает новое управление u = −K x̂⁺.
    /// </summary>
    public Vector Step(Vector uPrev, Vector y)
    {
        _kf.Predict(uPrev);
        _kf.Update(y, uPrev);
        return ControlLinAlg.Negate(ControlLinAlg.MatVec(_k, _kf.State));
    }
}

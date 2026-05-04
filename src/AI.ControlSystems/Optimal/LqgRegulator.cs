using System;
using AI.ControlSystems.Internal;
using AI.ControlSystems.Observers;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Optimal;

/// <summary>
/// LQG: линейная обратная связь по состоянию u = −K x̂ и фильтр Калмана для оценки x̂ (принцип разделения).
/// Один шаг: предсказание КФ по предыдущему управлению, коррекция по измерению, затем u = −K x̂.
/// </summary>
[Serializable]
public sealed class LqgRegulator
{
    private readonly KalmanFilter _kf;
    private readonly Matrix _k;

    /// <summary>Матрица усиления LQR (u = −K x̂).</summary>
    public Matrix StateFeedbackGain => _k;

    public KalmanFilter Filter => _kf;

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

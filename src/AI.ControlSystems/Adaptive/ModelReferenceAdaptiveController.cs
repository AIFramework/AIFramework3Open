using System;

namespace AI.ControlSystems.Adaptive;

/// <summary>
/// Упрощённый MRAC для объекта первого порядка ẏ = a y + b u и эталона первого порядка.
/// Адаптация одного коэффициента по правилу градиента (учебный пример).
/// </summary>
[Serializable]
public sealed class ModelReferenceAdaptiveController
{
    private double _ym;

    /// <summary>Коэффициент адаптации γ &gt; 0.</summary>
    public double AdaptationGain { get; set; } = 0.1;

    /// <summary>Эталон: ẏ_m = −a_m y_m + a_m r (a_m &gt; 0).</summary>
    public double ReferencePole { get; set; } = 1.0;

    /// <summary>Текущая оценка входного усиления (масштаб u).</summary>
    public double Theta { get; set; } = 1.0;

    /// <summary>Состояние эталонной модели y_m.</summary>
    public double ReferenceOutput => _ym;

    public void Reset(double referenceInitial = 0)
    {
        _ym = referenceInitial;
    }

    /// <summary>
    /// Один шаг: r — задающее воздействие, y — выход объекта, dt — шаг интегрирования эталона.
    /// Возвращает u = θ · r (простая схема с одним параметром).
    /// </summary>
    public double Compute(double r, double y, double dt)
    {
        if (dt <= 0)
            throw new ArgumentOutOfRangeException(nameof(dt));

        double am = ReferencePole;
        _ym += (-am * _ym + am * r) * dt;

        double e = _ym - y;
        Theta += -AdaptationGain * e * r * dt;

        return Theta * r;
    }
}

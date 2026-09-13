using System;

namespace AI.ControlSystems.Adaptive;

/// <summary>
/// MRAC для объекта первого порядка ẏ = a y + b u с неизвестными a и b и эталона
/// ẏ_m = −a_m y_m + a_m r: два адаптируемых коэффициента, закон адаптации по Ляпунову.
/// </summary>
/// <remarks>
/// <para>
/// Управление <c>u = θ_r·r + θ_y·y</c>. Идеальные коэффициенты <c>θ_y* = −(a + a_m)/b</c> и
/// <c>θ_r* = a_m/b</c> делают замкнутую систему точной копией эталона. Законы
/// <c>θ̇_r = −γ·sign(b)·e·r</c> и <c>θ̇_y = −γ·sign(b)·e·y</c> с ошибкой <c>e = y − y_m</c>
/// дают функцию Ляпунова <c>V = e²/2 + |b|/(2γ)·(θ̃_r² + θ̃_y²)</c> с производной
/// <c>−a_m·e² ≤ 0</c>: ошибка слежения стремится к нулю при любых a и b, в том числе для
/// неустойчивого объекта. Коэффициенты сходятся к идеальным, только если задание достаточно
/// богато — например, меняется ступенями.
/// </para>
/// <para>
/// Нужен лишь знак b, а не величина. Прежняя схема подстраивала один коэффициент, не могла
/// сдвинуть полюс объекта и вела его не в ту сторону: при y ниже эталона θ уменьшался, и
/// выход уходил от эталона без предела.
/// </para>
/// <para>
/// Законы интегрируются методом Эйлера с шагом вызова, эталон — точно. Шаг должен быть мал по
/// сравнению с постоянными времени объекта и эталона и с 1/(γ·r²).
/// </para>
/// </remarks>
[Serializable]
public sealed class ModelReferenceAdaptiveController
{
    private double _ym;

    /// <summary>Коэффициент адаптации γ &gt; 0.</summary>
    public double AdaptationGain { get; set; } = 0.1;

    /// <summary>Эталон: ẏ_m = −a_m y_m + a_m r (a_m &gt; 0).</summary>
    public double ReferencePole { get; set; } = 1.0;

    /// <summary>Коэффициент прямой связи по заданию θ_r.</summary>
    public double Theta { get; set; } = 1.0;

    /// <summary>Коэффициент обратной связи по выходу θ_y.</summary>
    public double FeedbackGain { get; set; }

    /// <summary>Знак входного усиления объекта b: +1 или −1.</summary>
    public int PlantGainSign { get; set; } = 1;

    /// <summary>Состояние эталонной модели y_m.</summary>
    public double ReferenceOutput => _ym;

    /// <summary>Ошибка слежения y − y_m на последнем шаге.</summary>
    public double TrackingError { get; private set; }

    /// <summary>Сброс эталонной модели.</summary>
    /// <param name="referenceInitial">Начальное значение эталона</param>
    public void Reset(double referenceInitial = 0)
    {
        _ym = referenceInitial;
        TrackingError = 0;
    }

    /// <summary>
    /// Один шаг: r — задающее воздействие, y — выход объекта, dt — шаг.
    /// Возвращает u = θ_r·r + θ_y·y и подстраивает оба коэффициента.
    /// </summary>
    public double Compute(double r, double y, double dt)
    {
        if (!(dt > 0))
            throw new ArgumentOutOfRangeException(nameof(dt));
        if (PlantGainSign == 0)
            throw new InvalidOperationException("Знак усиления объекта должен быть +1 или −1.");
        if (!(ReferencePole > 0))
            throw new InvalidOperationException("Полюс эталона a_m должен быть положительным.");

        double sign = Math.Sign(PlantGainSign);
        double e = y - _ym;
        double u = (Theta * r) + (FeedbackGain * y);

        Theta -= AdaptationGain * sign * e * r * dt;
        FeedbackGain -= AdaptationGain * sign * e * y * dt;

        // Эталон первого порядка интегрируется точно
        double decay = Math.Exp(-ReferencePole * dt);
        _ym = (decay * _ym) + ((1 - decay) * r);

        TrackingError = e;

        return u;
    }
}

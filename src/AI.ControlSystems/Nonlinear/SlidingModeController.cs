using System;

namespace AI.ControlSystems.Nonlinear;

/// <summary>
/// Скалярный релейный закон по поверхности скольжения s = e + λė (дискретная аппроксимация ė),
/// e = уставка − измерение: u = k·sign(s) или k·sat(s/Φ).
/// </summary>
/// <remarks>
/// <para>
/// Закон рассчитан на объект с положительным входным усилением: рост u увеличивает измерение.
/// Тогда при s &gt; 0 измерение ниже уставки, и u = +k толкает его вверх. Прежде знак был
/// обратным — u = −k·sign(s), — и выход уходил от уставки; в демонстрации это маскировалось
/// объектом с перевёрнутым знаком. Для объекта с отрицательным усилением задайте отрицательное k.
/// </para>
/// <para>
/// Регулятор работает только с ошибкой выхода и не знает модели. Для объекта в пространстве
/// состояний с эквивалентным управлением — <see cref="StateSlidingModeController"/>, для
/// непрерывного управления без дребезга — <see cref="SuperTwistingController"/>.
/// </para>
/// </remarks>
[Serializable]
public sealed class SlidingModeController
{
    private double _prevError;
    private bool _hasHistory;

    /// <summary>Вес на производной ошибки в s (λ ≥ 0).</summary>
    public double Lambda { get; set; } = 1.0;

    /// <summary>Усиление k: больше верхней границы приведённого возмущения.</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>Порог сглаживания Φ: при Φ &gt; 0 используется sat(s/Φ) вместо sign(s).</summary>
    public double SmoothingBoundary { get; set; }

    /// <summary>Сброс истории производной.</summary>
    public void Reset()
    {
        _hasHistory = false;
    }

    /// <summary>Один шаг: e = уставка − измерение.</summary>
    public double Compute(double setpoint, double measured, double dt)
    {
        if (!(dt > 0))
            throw new ArgumentOutOfRangeException(nameof(dt));

        double e = setpoint - measured;
        double de = _hasHistory ? (e - _prevError) / dt : 0;
        double s = e + (Lambda * de);

        double switching;
        if (SmoothingBoundary > 0)
            switching = Math.Clamp(s / SmoothingBoundary, -1, 1);
        else
            switching = s >= 0 ? 1 : -1;

        _prevError = e;
        _hasHistory = true;

        return Gain * switching;
    }
}

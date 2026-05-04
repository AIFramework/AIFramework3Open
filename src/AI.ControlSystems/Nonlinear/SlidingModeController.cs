using System;

namespace AI.ControlSystems.Nonlinear;

/// <summary>
/// Скалярный релейный закон по поверхности скольжения s = e + λė (дискретная аппроксимация ė).
/// u = −k sign(s) (при необходимости сгладить через sat(s/Φ)).
/// </summary>
[Serializable]
public sealed class SlidingModeController
{
    private double _prevError;
    private bool _hasHistory;

    /// <summary>Вес на производной ошибки в s (λ ≥ 0).</summary>
    public double Lambda { get; set; } = 1.0;

    /// <summary>Усиление k &gt; 0.</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>Порог сглаживания Φ: при Φ &gt; 0 используется sat(s/Φ) вместо sign(s).</summary>
    public double SmoothingBoundary { get; set; }

    public void Reset()
    {
        _hasHistory = false;
    }

    /// <summary>Один шаг: e = уставка − измерение.</summary>
    public double Compute(double setpoint, double measured, double dt)
    {
        if (dt <= 0)
            throw new ArgumentOutOfRangeException(nameof(dt));

        double e = setpoint - measured;
        double de = _hasHistory ? (e - _prevError) / dt : 0;
        double s = e + Lambda * de;

        double chattering;
        if (SmoothingBoundary > 0)
        {
            double x = s / SmoothingBoundary;
            if (x > 1) chattering = 1;
            else if (x < -1) chattering = -1;
            else chattering = x;
        }
        else
        {
            chattering = s >= 0 ? 1 : -1;
        }

        _prevError = e;
        _hasHistory = true;

        return -Gain * chattering;
    }
}

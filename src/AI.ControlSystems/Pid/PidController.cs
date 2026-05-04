using System;

namespace AI.ControlSystems.Pid;

/// <summary>
/// Дискретный PID-регулятор в параллельной форме:
/// u = Kp·e + Ki·∫e·dt + Kd·de/dt + Feedforward,
/// где e = уставка − измерение, ∫ накапливается прямоугольником e·Δt.
/// </summary>
[Serializable]
public sealed class PidController
{
    private double _integralOfError;
    private double _prevError;
    private double _prevMeasured;
    private bool _hasHistory;
    private double _dFiltered;
    private bool _dFilterInitialized;

    /// <summary>
    /// Постоянная времени фильтра нижних частот на дифференциальную составляющую (τ ≥ 0).
    /// null — без фильтра (как «сырая» разность).
    /// </summary>
    public double? DerivativeFilterTau { get; set; }

    /// <summary>
    /// Антинасыщение интегратора по схеме tracking: при ограничении выхода добавляется поправка к ∫e dt.
    /// Имеет смысл при заданных <see cref="OutputMin"/> / <see cref="OutputMax"/>.
    /// </summary>
    public bool UseAntiWindupTracking { get; set; }

    /// <summary>Коэффициент tracking (часто порядка 1/Kp); по умолчанию 1.</summary>
    public double AntiWindupTrackingGain { get; set; } = 1.0;

    /// <summary>Коэффициент пропорциональной составляющей.</summary>
    public double Kp { get; set; }

    /// <summary>Коэффициент интегральной составляющей (умножается на ∫e·dt).</summary>
    public double Ki { get; set; }

    /// <summary>Коэффициент дифференциальной составляющей.</summary>
    public double Kd { get; set; }

    /// <summary>
    /// true — производная по измерению: −Kd·(y−y₀)/Δt (меньше «выбросов» при скачке уставки);
    /// false — по ошибке: Kd·(e−e₀)/Δt.
    /// </summary>
    public bool DerivativeOnMeasurement { get; set; }

    /// <summary>
    /// Симметричное ограничение |∫e·dt| (антинасыщение интегратора). null — без ограничения.
    /// </summary>
    public double? IntegralClamp { get; set; }

    /// <summary>Нижняя граница выхода u (включительно). null — нет ограничения.</summary>
    public double? OutputMin { get; set; }

    /// <summary>Верхняя граница выхода u (включительно). null — нет ограничения.</summary>
    public double? OutputMax { get; set; }

    /// <summary>Постоянная добавка к выходу (упреждение, смещение).</summary>
    public double Feedforward { get; set; }

    /// <summary>Создаёт регулятор с нулевыми коэффициентами.</summary>
    public PidController()
    {
    }

    /// <summary>Создаёт регулятор с заданными Kp, Ki, Kd.</summary>
    public PidController(double kp, double ki, double kd)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
    }

    /// <summary>Сброс интеграла и истории для дифференцирования.</summary>
    public void Reset()
    {
        _integralOfError = 0;
        _hasHistory = false;
        _dFilterInitialized = false;
    }

    /// <summary>
    /// Один шаг регулятора.
    /// </summary>
    /// <param name="setpoint">Уставка.</param>
    /// <param name="measured">Измеренное значение.</param>
    /// <param name="dt">Шаг времени Δt &gt; 0 в тех же единицах, что и для Ki/Kd.</param>
    /// <returns>Управляющее воздействие u.</returns>
    public double Compute(double setpoint, double measured, double dt)
    {
        if (dt <= 0)
            throw new ArgumentOutOfRangeException(nameof(dt), "Шаг времени должен быть положительным.");

        double error = setpoint - measured;
        double p = Kp * error;

        _integralOfError += error * dt;
        if (IntegralClamp.HasValue)
        {
            double lim = Math.Abs(IntegralClamp.Value);
            if (_integralOfError > lim)
                _integralOfError = lim;
            else if (_integralOfError < -lim)
                _integralOfError = -lim;
        }

        double i = Ki * _integralOfError;

        double dRaw;
        if (!_hasHistory)
            dRaw = 0;
        else if (DerivativeOnMeasurement)
            dRaw = -Kd * (measured - _prevMeasured) / dt;
        else
            dRaw = Kd * (error - _prevError) / dt;

        double d;
        if (DerivativeFilterTau.HasValue && DerivativeFilterTau.Value > 0)
        {
            double tau = DerivativeFilterTau.Value;
            double alpha = dt / (tau + dt);
            if (!_dFilterInitialized)
            {
                _dFiltered = dRaw;
                _dFilterInitialized = true;
            }
            else
                _dFiltered += alpha * (dRaw - _dFiltered);
            d = _dFiltered;
        }
        else
        {
            d = dRaw;
        }

        double uPre = p + i + d + Feedforward;
        double u = uPre;

        if (OutputMin.HasValue && u < OutputMin.Value)
            u = OutputMin.Value;
        if (OutputMax.HasValue && u > OutputMax.Value)
            u = OutputMax.Value;

        if (UseAntiWindupTracking && Math.Abs(Ki) > 1e-30 && u != uPre)
            _integralOfError += AntiWindupTrackingGain * (u - uPre) / Ki;

        _prevError = error;
        _prevMeasured = measured;
        _hasHistory = true;

        return u;
    }

    /// <summary>Текущее накопленное значение ∫e·dt (после последнего вызова <see cref="Compute"/>).</summary>
    public double IntegralOfError => _integralOfError;
}

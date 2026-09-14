#nullable enable

namespace AI.MathUtils.ODE;

/// <summary>
/// Настройки адаптивного интегратора Дормана-Принса 5(4).
/// </summary>
public sealed class DormandPrinceOptions
{
    /// <summary>
    /// Относительная допустимая ошибка шага.
    /// </summary>
    public double RelativeTolerance { get; set; } = 1e-6;

    /// <summary>
    /// Абсолютная допустимая ошибка шага.
    /// </summary>
    public double AbsoluteTolerance { get; set; } = 1e-9;

    /// <summary>
    /// Начальный шаг; 0 означает автоматический выбор по правилу Хайрера.
    /// </summary>
    public double InitialStep { get; set; }

    /// <summary>
    /// Наибольший допустимый шаг (по модулю).
    /// </summary>
    public double MaxStep { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// Предельное число попыток шага (принятых и отвергнутых).
    /// </summary>
    public int MaxSteps { get; set; } = 100000;

    /// <summary>
    /// Точность нахождения момента события по времени (относительно max(1, |t|)).
    /// </summary>
    public double EventTolerance { get; set; } = 1e-12;
}

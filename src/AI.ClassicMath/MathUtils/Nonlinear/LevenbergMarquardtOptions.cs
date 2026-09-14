#nullable enable

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Настройки метода Левенберга-Марквардта.
/// </summary>
public sealed class LevenbergMarquardtOptions
{
    /// <summary>
    /// Предельное число попыток шага.
    /// </summary>
    public int MaxIterations { get; set; } = 500;

    /// <summary>
    /// Порог сходимости по норме градиента Jᵀr (максимум модуля компонент).
    /// </summary>
    public double GradientTolerance { get; set; } = 1e-10;

    /// <summary>
    /// Порог сходимости по относительному уменьшению суммы квадратов на принятом шаге.
    /// </summary>
    public double CostTolerance { get; set; } = 1e-14;

    /// <summary>
    /// Порог сходимости по длине шага относительно нормы решения.
    /// </summary>
    public double StepTolerance { get; set; } = 1e-12;

    /// <summary>
    /// Начальный коэффициент затухания λ.
    /// </summary>
    public double InitialDamping { get; set; } = 1e-3;

    /// <summary>
    /// Относительный шаг численного дифференцирования; 0 означает автоматический выбор.
    /// </summary>
    public double DerivativeStep { get; set; }
}

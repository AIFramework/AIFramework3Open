#nullable enable

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Настройки метода Ньютона для систем нелинейных уравнений.
/// </summary>
public sealed class NewtonSystemOptions
{
    /// <summary>
    /// Предельное число итераций.
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Порог сходимости по невязке: максимум модуля компонент F(x).
    /// </summary>
    public double FunctionTolerance { get; set; } = 1e-12;

    /// <summary>
    /// Порог остановки по длине шага относительно нормы решения.
    /// </summary>
    public double StepTolerance { get; set; } = 1e-15;

    /// <summary>
    /// Относительный шаг численного дифференцирования; 0 означает автоматический выбор.
    /// </summary>
    public double DerivativeStep { get; set; }

    /// <summary>
    /// Обновлять якобиан по формуле Бройдена вместо пересчета на каждой итерации.
    /// Якобиан пересчитывается заново, если линейный поиск вдоль квазиньютоновского направления не удался.
    /// </summary>
    public bool UseBroyden { get; set; }
}

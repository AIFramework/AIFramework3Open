#nullable enable

using AI.DataStructs.Algebraic;

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Результат решения системы нелинейных уравнений F(x) = 0.
/// </summary>
public sealed class NonlinearSystemResult
{
    /// <summary>
    /// Найденное решение.
    /// </summary>
    public required Vector Solution { get; init; }

    /// <summary>
    /// Значение F(x) в найденном решении.
    /// </summary>
    public required Vector Residual { get; init; }

    /// <summary>
    /// Максимум модуля компонент F(x) в найденном решении.
    /// </summary>
    public double ResidualNorm { get; init; }

    /// <summary>
    /// Число выполненных итераций.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Число вычислений F (включая вычисления для численного якобиана).
    /// </summary>
    public int FunctionEvaluations { get; init; }

    /// <summary>
    /// Достигнута ли требуемая точность по невязке.
    /// </summary>
    public bool Converged { get; init; }
}

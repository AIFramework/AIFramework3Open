#nullable enable

using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Результат нелинейного метода наименьших квадратов.
/// </summary>
public sealed class LeastSquaresResult
{
    /// <summary>
    /// Найденное решение.
    /// </summary>
    public required Vector Solution { get; init; }

    /// <summary>
    /// Вектор невязок r(x) в найденном решении.
    /// </summary>
    public required Vector Residuals { get; init; }

    /// <summary>
    /// Сумма квадратов невязок ‖r(x)‖² в найденном решении.
    /// </summary>
    public double Cost { get; init; }

    /// <summary>
    /// Число выполненных попыток шага.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Число вычислений функции невязок (включая вычисления для численного якобиана).
    /// </summary>
    public int FunctionEvaluations { get; init; }

    /// <summary>
    /// Выполнен ли один из критериев сходимости, а не исчерпан лимит итераций.
    /// </summary>
    public bool Converged { get; init; }

    /// <summary>
    /// Якобиан невязок в найденном решении (m строк, n столбцов).
    /// </summary>
    public required Matrix Jacobian { get; init; }

    /// <summary>
    /// Численный ранг якобиана в решении: число сингулярных чисел больше относительного порога.
    /// Ранг меньше числа неизвестных означает недоопределенную задачу: часть параметров данные не определяют.
    /// </summary>
    /// <param name="relativeTolerance">Порог относительно наибольшего сингулярного числа.</param>
    public int JacobianRank(double relativeTolerance = 1e-10)
    {
        var (_, sigma, _) = Svd.Decompose(Jacobian);
        double largest = 0;

        foreach (double value in sigma)
            largest = Math.Max(largest, value);

        if (largest == 0)
            return 0;

        int rank = 0;

        foreach (double value in sigma)
        {
            if (value > relativeTolerance * largest)
                rank++;
        }

        return rank;
    }
}

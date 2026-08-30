using AI.DataStructs.Algebraic;
using AI.Solvers.Pde.Numerics;

namespace AI.Solvers.Pde.FiniteDifference;

/// <summary>Решение краевой задачи для уравнения Пуассона</summary>
public sealed class PoissonSolution
{
    internal PoissonSolution(Grid2D grid, Matrix values, int iterations, double residual, bool converged)
    {
        Grid = grid;
        Values = values;
        Iterations = iterations;
        Residual = residual;
        Converged = converged;
    }

    /// <summary>Сетка</summary>
    public Grid2D Grid { get; }

    /// <summary>Значения в узлах: строка — постоянное y</summary>
    public Matrix Values { get; }

    /// <summary>Число итераций решателя системы</summary>
    public int Iterations { get; }

    /// <summary>Норма невязки</summary>
    public double Residual { get; }

    /// <summary>Достигнут ли заданный порог</summary>
    public bool Converged { get; }

    /// <summary>Значение в узле</summary>
    /// <param name="i">Номер по x</param>
    /// <param name="j">Номер по y</param>
    public double this[int i, int j] => Values[j, i];

    /// <summary>Краткая запись результата</summary>
    public override string ToString()
        => $"Пуассон: сетка {Grid.CountX}×{Grid.CountY}, итераций {Iterations}, невязка {Residual:E2}";
}

/// <summary>
/// Уравнение Пуассона <c>−Δu = f</c> на прямоугольнике с условием Дирихле на границе.
/// </summary>
/// <remarks>
/// <para>
/// Пятиточечный шаблон даёт симметричную положительно определённую систему, которая
/// решается методом сопряжённых градиентов. Прямое разложение здесь неуместно: матрица
/// сетки 100×100 — это десять тысяч неизвестных, и заполнение при исключении съело бы
/// память ради результата, который итерации дают за сотни шагов.
/// </para>
/// <para>
/// Задача Лапласа — частный случай с нулевой правой частью.
/// </para>
/// </remarks>
public static class Poisson2D
{
    /// <summary>
    /// Решает краевую задачу
    /// </summary>
    /// <param name="grid">Сетка</param>
    /// <param name="source">Правая часть <c>f(x, y)</c> уравнения <c>−Δu = f</c></param>
    /// <param name="boundary">Значение на границе <c>g(x, y)</c></param>
    /// <param name="tolerance">Относительный порог по невязке</param>
    /// <param name="maxIterations">Предел итераций; ноль — по размеру задачи</param>
    public static PoissonSolution Solve(
        Grid2D grid,
        Func<double, double, double> source,
        Func<double, double, double> boundary,
        double tolerance = 1e-10,
        int maxIterations = 0)
    {
        grid.Validate();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(boundary);

        int size = grid.NodeCount;
        double hx = grid.StepX;
        double hy = grid.StepY;
        double cx = 1.0 / (hx * hx);
        double cy = 1.0 / (hy * hy);

        var matrix = new SparseMatrix(size, size);
        var right = new Vector(size);

        for (int j = 1; j < grid.CountY - 1; j++)
        {
            for (int i = 1; i < grid.CountX - 1; i++)
            {
                int index = grid.Index(i, j);

                matrix.Add(index, index, 2 * (cx + cy));
                matrix.Add(index, grid.Index(i - 1, j), -cx);
                matrix.Add(index, grid.Index(i + 1, j), -cx);
                matrix.Add(index, grid.Index(i, j - 1), -cy);
                matrix.Add(index, grid.Index(i, j + 1), -cy);

                right[index] = source(grid.X(i), grid.Y(j));
            }
        }

        // Граничные узлы исключаются как известные: их вклад уходит в правую часть,
        // а столбец обнуляется вместе со строкой. Если оставить только тождественную
        // строку, матрица окажется несимметричной, и метод сопряжённых градиентов
        // разойдётся — при нулевых граничных значениях это незаметно, при ненулевых
        // решение улетает на пятнадцать порядков.
        for (int j = 0; j < grid.CountY; j++)
        {
            for (int i = 0; i < grid.CountX; i++)
            {
                if (!grid.IsBoundary(i, j))
                    continue;

                matrix.EliminateKnown(grid.Index(i, j), boundary(grid.X(i), grid.Y(j)), right);
            }
        }

        IterativeResult result = IterativeSolvers.ConjugateGradient(matrix, right, tolerance, maxIterations);

        var values = new Matrix(grid.CountY, grid.CountX);

        for (int j = 0; j < grid.CountY; j++)
            for (int i = 0; i < grid.CountX; i++)
                values[j, i] = result.Solution[grid.Index(i, j)];

        return new PoissonSolution(grid, values, result.Iterations, result.Residual, result.Converged);
    }

    /// <summary>
    /// Решает задачу Лапласа <c>Δu = 0</c> с заданными значениями на границе
    /// </summary>
    /// <param name="grid">Сетка</param>
    /// <param name="boundary">Значение на границе</param>
    /// <param name="tolerance">Относительный порог по невязке</param>
    public static PoissonSolution SolveLaplace(
        Grid2D grid,
        Func<double, double, double> boundary,
        double tolerance = 1e-10)
        => Solve(grid, static (_, _) => 0.0, boundary, tolerance);
}

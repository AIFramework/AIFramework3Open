#nullable enable

using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.MathUtils.Nonlinear;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Geometry.Constraints;

/// <summary>
/// Решение эскиза методом Левенберга-Марквардта и поиск нескольких ветвей решения.
/// </summary>
public sealed partial class GeometricSketch
{
    /// <summary>
    /// Допуск невязки по умолчанию относительно характерного размера задачи.
    /// </summary>
    public const double DefaultTolerance = 1e-6;

    // Порог ранга якобиана относительно наибольшего сингулярного числа
    private const double RankTolerance = 1e-8;

    // Неизвестная считается неопределенной, если ее компонента в ядре якобиана больше порога
    private const double NullSpaceThreshold = 1e-3;

    /// <summary>
    /// Решает эскиз из начального приближения и анализирует результат: невязки ограничений, число степеней
    /// свободы, избыточность, противоречия.
    /// </summary>
    /// <param name="tolerance">Допуск невязки относительно характерного размера задачи.</param>
    /// <returns>Решение с отчетом.</returns>
    public SketchSolution Solve(double tolerance = DefaultTolerance)
    {
        double[] guess = [.. _guesses];
        return SolveFrom(guess, tolerance, Scale(guess));
    }

    /// <summary>
    /// Ищет несколько решений (ветвей): запускает решатель из начального приближения и из детерминированных
    /// случайных возмущений вокруг него, отбрасывает совпадающие решения.
    /// </summary>
    /// <param name="starts">Число запусков, включая запуск из самого начального приближения.</param>
    /// <param name="seed">Зерно генератора возмущений; одинаковое зерно дает одинаковый результат.</param>
    /// <param name="tolerance">Допуск невязки относительно характерного размера задачи.</param>
    /// <returns>
    /// Различные решения, упорядоченные по удалению от начального приближения. Если ни один запуск не выполнил
    /// ограничения, возвращается одно лучшее приближение со статусом <see cref="SketchStatus.Inconsistent"/>.
    /// Для недоопределенной задачи решений бесконечно много, поэтому возвращается одно, ближайшее к приближению.
    /// </returns>
    public IReadOnlyList<SketchSolution> SolveAll(int starts = 32, int seed = 1, double tolerance = DefaultTolerance)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(starts, 1);

        double[] guess = [.. _guesses];
        double scale = Scale(guess);
        var random = new Random(seed);
        var found = new List<SketchSolution>();
        SketchSolution? best = null;

        for (int attempt = 0; attempt < starts; attempt++)
        {
            double[] start = [.. guess];

            // Размах возмущения растет по кругу: 1, 2, 3 характерных размера
            double spread = scale * (1 + (attempt % 3));

            for (int i = 0; attempt > 0 && i < start.Length; i++)
            {
                if (!_fixed[i])
                    start[i] += spread * ((2 * random.NextDouble()) - 1);
            }

            SketchSolution solution = SolveFrom(start, tolerance, scale);

            if (!solution.Satisfied)
            {
                if (best == null || solution.MaxResidual < best.MaxResidual)
                    best = solution;

                continue;
            }

            if (solution.Status == SketchStatus.UnderConstrained)
                return [solution];

            if (!found.Any(other => MaxDifference(other.FullValues, solution.FullValues) <= 1e-5 * scale))
                found.Add(solution);
        }

        if (found.Count == 0)
            return [best!];

        return found.OrderBy(solution => Distance(solution.FullValues, guess)).ToList();
    }

    private SketchSolution SolveFrom(double[] start, double tolerance, double scale)
    {
        int total = _names.Count;
        int[] free = Enumerable.Range(0, total).Where(i => !_fixed[i]).ToArray();
        var column = Enumerable.Repeat(-1, total).ToArray();

        for (int k = 0; k < free.Length; k++)
            column[free[k]] = k;

        int[][] maps = _constraints.Select(c => c.Unknowns.Select(IndexOf).ToArray()).ToArray();
        int equations = _constraints.Sum(c => c.Count);

        double[] Full(Vector x)
        {
            double[] all = [.. start];

            for (int k = 0; k < free.Length; k++)
                all[free[k]] = x[k];

            return all;
        }

        double[] values = [.. start];
        bool converged = true;
        int iterations = 0;

        if (free.Length > 0 && equations > 0)
        {
            var result = LevenbergMarquardt.Solve(
                x => new Vector(Residuals(Full(x), maps, equations)),
                new Vector(free.Select(i => start[i]).ToArray()),
                x => new Matrix(Jacobian(Full(x), maps, column, equations, free.Length)));

            values = Full(result.Solution);
            converged = result.Converged;
            iterations = result.Iterations;
        }

        var residuals = new List<ConstraintResidual>(_constraints.Count);
        double cost = 0;

        for (int c = 0; c < _constraints.Count; c++)
        {
            double[] block = _constraints[c].Residuals(Local(values, maps[c]));
            double squares = block.Sum(r => r * r);
            cost += squares;
            residuals.Add(new ConstraintResidual(_constraints[c].Name, _constraints[c].Kind, Math.Sqrt(squares)));
        }

        double limit = tolerance * scale;
        bool satisfied = residuals.All(r => r.Value <= limit);
        var (rank, undetermined) = Analyze(Jacobian(values, maps, column, equations, free.Length), free);
        int freedom = free.Length - rank;
        int redundancy = equations - rank;

        SketchStatus status = !satisfied ? SketchStatus.Inconsistent
            : freedom > 0 ? SketchStatus.UnderConstrained
            : redundancy > 0 ? SketchStatus.Redundant
            : SketchStatus.WellConstrained;

        var violated = residuals.Where(r => r.Value > limit).OrderByDescending(r => r.Value).Select(r => r.Name).ToList();

        return new SketchSolution(this, values, residuals, violated, undetermined)
        {
            Converged = converged,
            Satisfied = satisfied,
            Status = status,
            DegreesOfFreedom = freedom,
            Rank = rank,
            Redundancy = redundancy,
            Cost = cost,
            Iterations = iterations
        };
    }

    // Ранг якобиана и неизвестные, которые имеют заметную компоненту в его ядре
    private (int Rank, List<string> Undetermined) Analyze(double[,] jacobian, int[] free)
    {
        int n = free.Length;

        if (n == 0)
            return (0, []);

        if (jacobian.GetLength(0) == 0)
            return (0, free.Select(i => _names[i]).ToList());

        var (_, sigma, v) = Svd.Decompose(new Matrix(jacobian));
        double largest = sigma.Max();
        double threshold = RankTolerance * largest;
        int[] kernel = Enumerable.Range(0, n).Where(k => largest == 0 || sigma[k] <= threshold).ToArray();
        var undetermined = new List<string>();

        for (int j = 0; j < n; j++)
        {
            double weight = kernel.Sum(k => v[j, k] * v[j, k]);

            if (Math.Sqrt(weight) > NullSpaceThreshold)
                undetermined.Add(_names[free[j]]);
        }

        return (n - kernel.Length, undetermined);
    }

    private double[] Residuals(double[] all, int[][] maps, int equations)
    {
        var result = new double[equations];
        int row = 0;

        for (int c = 0; c < _constraints.Count; c++)
        {
            double[] block = _constraints[c].Residuals(Local(all, maps[c]));
            Array.Copy(block, 0, result, row, block.Length);
            row += block.Length;
        }

        return result;
    }

    // Якобиан собирается поблочно: аналитически, где блок это умеет, иначе центральными разностями по его неизвестным
    private double[,] Jacobian(double[] all, int[][] maps, int[] column, int equations, int unknowns)
    {
        var jacobian = new double[equations, unknowns];
        int row = 0;

        for (int c = 0; c < _constraints.Count; c++)
        {
            SketchConstraint constraint = _constraints[c];
            int[] map = maps[c];
            double[] local = Local(all, map);
            double[,] block = constraint.Derivatives(local) ?? NumericalBlock(constraint, local, map, column);

            for (int i = 0; i < constraint.Count; i++)
            {
                for (int j = 0; j < map.Length; j++)
                {
                    if (column[map[j]] >= 0)
                        jacobian[row + i, column[map[j]]] += block[i, j];
                }
            }

            row += constraint.Count;
        }

        return jacobian;
    }

    private static double[,] NumericalBlock(SketchConstraint constraint, double[] local, int[] map, int[] column)
    {
        var block = new double[constraint.Count, local.Length];

        for (int j = 0; j < local.Length; j++)
        {
            if (column[map[j]] < 0)
                continue;

            double value = local[j];
            double step = 1e-6 * Math.Max(1, Math.Abs(value));
            local[j] = value + step;
            double[] plus = constraint.Residuals(local);
            local[j] = value - step;
            double[] minus = constraint.Residuals(local);
            local[j] = value;

            for (int i = 0; i < constraint.Count; i++)
                block[i, j] = (plus[i] - minus[i]) / (2 * step);
        }

        return block;
    }

    private static double[] Local(double[] all, int[] map)
    {
        var local = new double[map.Length];

        for (int j = 0; j < map.Length; j++)
            local[j] = all[map[j]];

        return local;
    }

    // Характерный размер задачи: наибольший модуль начальных значений и чисел в ограничениях, но не меньше 1
    private static double Scale(double[] values) => Math.Max(1, values.Select(Math.Abs).DefaultIfEmpty(0).Max());

    private static double MaxDifference(double[] a, double[] b)
    {
        double max = 0;

        for (int i = 0; i < a.Length; i++)
            max = Math.Max(max, Math.Abs(a[i] - b[i]));

        return max;
    }

    private static double Distance(double[] a, double[] b)
    {
        double sum = 0;

        for (int i = 0; i < a.Length; i++)
            sum += (a[i] - b[i]) * (a[i] - b[i]);

        return Math.Sqrt(sum);
    }
}

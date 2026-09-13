using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AI.Solvers.Optimization;

/// <summary>Настройки решателя квадратичных задач</summary>
public sealed class QpOptions
{
    /// <summary>Предел итераций; нуль — по размеру задачи</summary>
    public int MaxIterations { get; init; }

    /// <summary>Допуск на нарушение ограничений и на знак множителей</summary>
    public double Tolerance { get; init; } = 1e-9;
}

/// <summary>
/// Прямой метод активного множества для строго выпуклой квадратичной задачи.
/// </summary>
/// <remarks>
/// <para>
/// Решатель держит рабочее множество ограничений, выполняемых как равенства. На каждом шаге
/// решается задача с этими равенствами — система Каруша — Куна — Таккера — и находится
/// направление. Если оно нулевое, проверяются знаки множителей: все неотрицательны — оптимум,
/// иначе ограничение с самым отрицательным множителем освобождается. Если направление ненулевое,
/// шаг идёт до первого блокирующего ограничения, и оно добавляется. Метод конечен и даёт точное
/// решение, а не приближение, как методы внутренней точки.
/// </para>
/// <para>
/// Начальная допустимая точка берётся из переданного старта, если он допустим, затем из
/// безусловного минимума, а в последнюю очередь — из вспомогательной линейной задачи, которую
/// решает готовый <see cref="LpSolver"/>: второго симплекса здесь нет. Системы решаются
/// разложениями LU и Холецкого из <c>AI.ClassicMath</c>.
/// </para>
/// <para>
/// Плотная алгебра: каждая итерация — O((n + m)³). Задачи на сотни переменных решаются быстро,
/// на десятки тысяч — нет. Вырожденные задачи с большим числом одновременно активных ограничений
/// теоретически могут зациклиться; от этого защищает предел итераций.
/// </para>
/// </remarks>
public static class QpSolver
{
    /// <summary>Решает задачу</summary>
    /// <param name="program">Задача</param>
    /// <param name="options">Настройки</param>
    /// <param name="start">Начальная точка — например, решение прошлого шага MPC</param>
    /// <exception cref="ArgumentException">Гессиан не положительно определён</exception>
    public static QpSolution Solve(QuadraticProgram program, QpOptions? options = null, Vector? start = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        options ??= new QpOptions();

        int n = program.VariableCount;
        double[,] h = program.HessianArray;
        double[] g = program.LinearArray;
        IReadOnlyList<QpConstraint> constraints = program.Constraints;
        int total = constraints.Count;
        double tolerance = options.Tolerance;
        int limit = options.MaxIterations > 0 ? options.MaxIterations : (50 * (n + total)) + 100;

        try
        {
            _ = Cholesky.Decompose(new Matrix((double[,])h.Clone()));
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException(
                "Гессиан должен быть положительно определённым: задача не строго выпукла. "
                + "Добавьте малую регуляризацию или решайте линейную задачу LpSolver", nameof(program));
        }

        bool Feasible(double[] point) => constraints.All(c =>
        {
            double slack = Dot(c.Row, point) - c.RightHandSide;
            double scale = tolerance * (1 + Math.Abs(c.RightHandSide) + (Norm(c.Row) * MaxAbs(point)));

            return c.IsEquality ? Math.Abs(slack) <= scale : slack <= scale;
        });

        double[]? x = null;

        if (start is not null)
        {
            if (start.Count != n)
                throw new ArgumentException($"Начальная точка должна иметь {n} координат", nameof(start));

            double[] candidate = start.ToArray();

            if (candidate.All(double.IsFinite) && Feasible(candidate))
                x = candidate;
        }

        if (x is null)
        {
            double[] free = Cholesky.Solve(new Matrix((double[,])h.Clone()), new Vector(g.Select(v => -v).ToArray())).ToArray();

            if (Feasible(free))
                x = free;
        }

        x ??= FeasiblePoint(program);

        if (x is null)
        {
            return new QpSolution(program, SolverStatus.Infeasible, Enumerable.Repeat(double.NaN, n).ToArray(),
                double.NaN, new double[total], [], 0);
        }

        var working = new List<int>();

        for (int i = 0; i < total; i++)
        {
            if (constraints[i].IsEquality)
                TryAdd(working, i, constraints);
        }

        for (int i = 0; i < total; i++)
        {
            QpConstraint c = constraints[i];

            if (c.IsEquality)
                continue;

            double slack = c.RightHandSide - Dot(c.Row, x);

            if (Math.Abs(slack) <= tolerance * (1 + Math.Abs(c.RightHandSide)))
                TryAdd(working, i, constraints);
        }

        for (int iteration = 1; iteration <= limit; iteration++)
        {
            (double[] direction, double[] lambda) = SolveKkt(h, g, x, working, constraints);

            if (MaxAbs(direction) <= tolerance * (1 + MaxAbs(x)))
            {
                int release = -1;
                double mostNegative = -tolerance * (1 + MaxAbs(lambda));

                for (int w = 0; w < working.Count; w++)
                {
                    if (!constraints[working[w]].IsEquality && lambda[w] < mostNegative)
                    {
                        mostNegative = lambda[w];
                        release = w;
                    }
                }

                if (release < 0)
                {
                    var multipliers = new double[total];

                    for (int w = 0; w < working.Count; w++)
                        multipliers[working[w]] = lambda[w];

                    return new QpSolution(program, SolverStatus.Optimal, x, program.Evaluate(x), multipliers, working.ToArray(), iteration);
                }

                working.RemoveAt(release);
                continue;
            }

            double step = 1;
            int blocking = -1;
            double directionScale = MaxAbs(direction);

            for (int i = 0; i < total; i++)
            {
                QpConstraint c = constraints[i];

                if (c.IsEquality || working.Contains(i))
                    continue;

                double rate = Dot(c.Row, direction);

                if (rate <= 1e-14 * Norm(c.Row) * directionScale)
                    continue;

                double length = Math.Max(0, c.RightHandSide - Dot(c.Row, x)) / rate;

                if (length < step)
                {
                    step = length;
                    blocking = i;
                }
            }

            for (int j = 0; j < n; j++)
                x[j] += step * direction[j];

            if (blocking >= 0)
                TryAdd(working, blocking, constraints);
        }

        return new QpSolution(program, SolverStatus.LimitReached, x, program.Evaluate(x), new double[total], working.ToArray(), limit);
    }

    // Направление p и множители λ из системы [H Aᵀ; A 0]·[p; λ] = [−(Hx + g); 0]
    private static (double[] Direction, double[] Multipliers) SolveKkt(
        double[,] h, double[] g, double[] x, List<int> working, IReadOnlyList<QpConstraint> constraints)
    {
        int n = x.Length;
        int w = working.Count;
        int size = n + w;
        var kkt = new Matrix(size, size);
        var rhs = new Vector(size);

        for (int i = 0; i < n; i++)
        {
            double gradient = g[i];

            for (int j = 0; j < n; j++)
            {
                kkt[i, j] = h[i, j];
                gradient += h[i, j] * x[j];
            }

            rhs[i] = -gradient;
        }

        for (int k = 0; k < w; k++)
        {
            double[] row = constraints[working[k]].Row;

            for (int j = 0; j < n; j++)
            {
                kkt[n + k, j] = row[j];
                kkt[j, n + k] = row[j];
            }
        }

        double[] solution = LU.Solve(kkt, rhs).ToArray();

        return (solution[..n], solution[n..]);
    }

    // Ограничение добавляется, только если его строка не выражается через строки рабочего множества
    private static void TryAdd(List<int> working, int candidate, IReadOnlyList<QpConstraint> constraints)
    {
        var basis = new List<double[]>();

        foreach (int index in working)
        {
            double[] orthogonal = Orthogonalize(constraints[index].Row, basis);
            double norm = Norm(orthogonal);

            if (norm > 1e-12)
                basis.Add(orthogonal.Select(v => v / norm).ToArray());
        }

        double[] residual = Orthogonalize(constraints[candidate].Row, basis);

        if (Norm(residual) > 1e-9 * Math.Max(1e-300, Norm(constraints[candidate].Row)))
            working.Add(candidate);
    }

    private static double[] Orthogonalize(double[] row, List<double[]> basis)
    {
        double[] result = (double[])row.Clone();

        foreach (double[] q in basis)
        {
            double projection = Dot(result, q);

            for (int j = 0; j < result.Length; j++)
                result[j] -= projection * q[j];
        }

        return result;
    }

    // Допустимая точка из линейной задачи с нулевой целью
    private static double[]? FeasiblePoint(QuadraticProgram program)
    {
        int n = program.VariableCount;
        var lp = new LinearProgram(ObjectiveSense.Minimize, "Поиск допустимой точки");

        for (int i = 0; i < n; i++)
            _ = lp.AddFreeVariable($"x{i}");

        lp.SetObjective(new Vector(n));

        foreach (QpConstraint c in program.Constraints)
        {
            _ = lp.AddConstraint(new Vector((double[])c.Row.Clone()),
                c.IsEquality ? ConstraintSign.Equal : ConstraintSign.LessOrEqual, c.RightHandSide);
        }

        LpSolution solution = LpSolver.Solve(lp);

        return solution.IsOptimal ? solution.Values.ToArray() : null;
    }

    private static double Dot(double[] a, double[] b)
    {
        double sum = 0;

        for (int i = 0; i < a.Length; i++)
            sum += a[i] * b[i];

        return sum;
    }

    private static double Norm(double[] a) => Math.Sqrt(Dot(a, a));

    private static double MaxAbs(double[] a) => a.Length == 0 ? 0 : a.Max(Math.Abs);
}

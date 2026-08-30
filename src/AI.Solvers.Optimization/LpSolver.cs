using AI.DataStructs.Algebraic;

namespace AI.Solvers.Optimization;

/// <summary>
/// Настройки решателя
/// </summary>
public sealed class LpOptions
{
    /// <summary>Предел итераций симплекс-метода на одну задачу</summary>
    public int MaxIterations { get; set; } = 100_000;

    /// <summary>Предел числа узлов дерева ветвления для целочисленной задачи</summary>
    public int MaxNodes { get; set; } = 20_000;

    /// <summary>
    /// Допуск целочисленности: значение считается целым, если отличается от ближайшего целого
    /// не больше чем на эту величину
    /// </summary>
    public double IntegerTolerance { get; set; } = 1e-7;
}

/// <summary>
/// Решатель задач линейного и смешанно-целочисленного программирования.
/// </summary>
/// <remarks>
/// <para>
/// Непрерывная задача решается симплекс-методом; при наличии целочисленных переменных
/// поверх него работает метод ветвей и границ: решается ослабленная задача, дробная
/// переменная делит задачу надвое, ветви с оценкой хуже найденного решения отсекаются.
/// </para>
/// <para>
/// Приведение к стандартному виду делается здесь: сдвиг нижних границ, замена свободных
/// переменных разностью двух неотрицательных, перенос верхних границ в ограничения,
/// добавление балансовых переменных. Симплекс работает только с задачей вида
/// <c>min cᵀy, Ay = b, y ≥ 0</c> и о границах ничего не знает.
/// </para>
/// </remarks>
public static class LpSolver
{
    /// <summary>
    /// Решает задачу
    /// </summary>
    /// <param name="program">Задача</param>
    /// <param name="options">Настройки; по умолчанию стандартные</param>
    public static LpSolution Solve(LinearProgram program, LpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        options ??= new LpOptions();

        double[] lower = program.Variables.Select(v => v.LowerBound).ToArray();
        double[] upper = program.Variables.Select(v => v.UpperBound).ToArray();

        return program.IsMixedInteger
            ? BranchAndBound(program, lower, upper, options)
            : Relax(program, lower, upper, options).ToSolution(program);
    }

    #region Ветвление

    private static LpSolution BranchAndBound(LinearProgram program, double[] lower, double[] upper, LpOptions options)
    {
        Relaxation root = Relax(program, lower, upper, options);

        if (root.Status is SolverStatus.Infeasible or SolverStatus.Unbounded)
            return root.ToSolution(program);

        var stack = new Stack<(double[] Lower, double[] Upper, double Bound)>();
        stack.Push((lower, upper, root.Objective));

        double[]? incumbent = null;
        double incumbentValue = double.PositiveInfinity;
        int iterations = 0;
        int nodes = 0;

        while (stack.Count > 0)
        {
            if (nodes >= options.MaxNodes)
                break;

            (double[] nodeLower, double[] nodeUpper, double parentBound) = stack.Pop();
            nodes++;

            if (parentBound > incumbentValue - Simplex.Tolerance)
                continue;

            Relaxation node = Relax(program, nodeLower, nodeUpper, options);
            iterations += node.Iterations;

            if (node.Status is SolverStatus.Infeasible or SolverStatus.Unbounded)
                continue;

            // Ослабленная задача не может быть лучше уже найденного целого решения
            if (node.Objective > incumbentValue - Simplex.Tolerance)
                continue;

            int branching = ChooseBranchingVariable(program, node.Values, options.IntegerTolerance);

            if (branching < 0)
            {
                incumbent = node.Values;
                incumbentValue = node.Objective;
                continue;
            }

            double value = node.Values[branching];
            double floor = Math.Floor(value);
            double ceiling = Math.Ceiling(value);

            // Ветвь «не больше floor»
            if (floor >= nodeLower[branching] - Simplex.Tolerance)
            {
                double[] leftUpper = (double[])nodeUpper.Clone();
                leftUpper[branching] = Math.Min(leftUpper[branching], floor);
                stack.Push(((double[])nodeLower.Clone(), leftUpper, node.Objective));
            }

            // Ветвь «не меньше ceiling»
            if (ceiling <= nodeUpper[branching] + Simplex.Tolerance)
            {
                double[] rightLower = (double[])nodeLower.Clone();
                rightLower[branching] = Math.Max(rightLower[branching], ceiling);
                stack.Push((rightLower, (double[])nodeUpper.Clone(), node.Objective));
            }
        }

        if (incumbent is null)
        {
            SolverStatus status = nodes >= options.MaxNodes ? SolverStatus.LimitReached : SolverStatus.Infeasible;
            return new LpSolution(program, status, double.NaN, new double[program.Variables.Count], iterations, nodes);
        }

        RoundIntegers(program, incumbent, options.IntegerTolerance);

        bool exhausted = stack.Count == 0;
        double objective = Objective(program, incumbent);

        double? bound = exhausted
            ? null
            : ToExternal(program, stack.Min(node => node.Bound));

        return new LpSolution(
            program,
            exhausted ? SolverStatus.Optimal : SolverStatus.LimitReached,
            objective,
            incumbent,
            iterations,
            nodes,
            bound);
    }

    private static int ChooseBranchingVariable(LinearProgram program, double[] values, double tolerance)
    {
        int chosen = -1;
        double worst = tolerance;

        foreach (Variable variable in program.Variables)
        {
            if (!variable.IsIntegral)
                continue;

            double value = values[variable.Index];
            double distance = Math.Abs(value - Math.Round(value));

            if (distance > worst)
            {
                worst = distance;
                chosen = variable.Index;
            }
        }

        return chosen;
    }

    private static void RoundIntegers(LinearProgram program, double[] values, double tolerance)
    {
        foreach (Variable variable in program.Variables)
        {
            if (!variable.IsIntegral)
                continue;

            double value = values[variable.Index];

            if (Math.Abs(value - Math.Round(value)) <= tolerance)
                values[variable.Index] = Math.Round(value);
        }
    }

    #endregion

    #region Ослабленная задача

    private readonly record struct Relaxation(SolverStatus Status, double[] Values, double Objective, int Iterations)
    {
        internal LpSolution ToSolution(LinearProgram program)
            => new(program, Status, Status == SolverStatus.Optimal ? LpSolver.Objective(program, Values) : double.NaN,
                Values, Iterations);
    }

    /// <summary>
    /// Решает задачу без требования целочисленности при заданных границах переменных.
    /// Целевая функция внутри всегда минимизируется.
    /// </summary>
    private static Relaxation Relax(LinearProgram program, double[] lower, double[] upper, LpOptions options)
    {
        StandardForm form = StandardForm.Build(program, lower, upper);
        Simplex.Result result = Simplex.Solve(form.A, form.B, form.C, options.MaxIterations);

        double[] values = result.Status == SolverStatus.Optimal
            ? form.Recover(result.Values)
            : new double[program.Variables.Count];

        return new Relaxation(result.Status, values, result.Objective + form.ObjectiveShift, result.Iterations);
    }

    /// <summary>Значение целевой функции задачи в её собственном направлении</summary>
    private static double Objective(LinearProgram program, double[] values)
    {
        double sum = 0;

        foreach (Variable variable in program.Variables)
            sum += program.ObjectiveCoefficient(variable) * values[variable.Index];

        return sum;
    }

    /// <summary>Перевод внутреннего значения (минимум) во внешнее направление задачи</summary>
    private static double ToExternal(LinearProgram program, double internalValue)
        => program.Sense == ObjectiveSense.Minimize ? internalValue : -internalValue;

    #endregion

    /// <summary>
    /// Задача в стандартном виде вместе с отображением столбцов на переменные исходной задачи
    /// </summary>
    private sealed class StandardForm
    {
        private readonly List<(int Column, double Factor)>[] _terms;
        private readonly double[] _offsets;

        private StandardForm(double[,] a, double[] b, double[] c, double objectiveShift,
            List<(int Column, double Factor)>[] terms, double[] offsets)
        {
            A = a;
            B = b;
            C = c;
            ObjectiveShift = objectiveShift;
            _terms = terms;
            _offsets = offsets;
        }

        internal double[,] A { get; }

        internal double[] B { get; }

        internal double[] C { get; }

        /// <summary>Свободный член, потерянный при сдвиге границ</summary>
        internal double ObjectiveShift { get; }

        internal static StandardForm Build(LinearProgram program, double[] lower, double[] upper)
        {
            int count = program.Variables.Count;
            var terms = new List<(int Column, double Factor)>[count];
            var offsets = new double[count];
            var boundRows = new List<(int Column, double Limit)>();

            int columns = 0;

            for (int j = 0; j < count; j++)
            {
                double low = lower[j];
                double high = upper[j];

                terms[j] = [];

                if (double.IsNegativeInfinity(low) && double.IsPositiveInfinity(high))
                {
                    // Свободная переменная: разность двух неотрицательных
                    terms[j].Add((columns++, 1.0));
                    terms[j].Add((columns++, -1.0));
                    offsets[j] = 0;
                }
                else if (double.IsNegativeInfinity(low))
                {
                    // Ограничена только сверху: считаем отступ вниз от верхней границы
                    terms[j].Add((columns, -1.0));
                    offsets[j] = high;
                    columns++;
                }
                else
                {
                    terms[j].Add((columns, 1.0));
                    offsets[j] = low;

                    if (!double.IsPositiveInfinity(high))
                        boundRows.Add((columns, high - low));

                    columns++;
                }
            }

            int rows = program.Constraints.Count + boundRows.Count;
            int slackCount = program.Constraints.Count(x => x.Sign != ConstraintSign.Equal) + boundRows.Count;

            var a = new double[rows, columns + slackCount];
            var b = new double[rows];
            var c = new double[columns + slackCount];

            int slack = columns;
            int row = 0;

            foreach (Constraint constraint in program.Constraints)
            {
                double shift = 0;

                for (int j = 0; j < count; j++)
                {
                    double coefficient = constraint.Coefficients[j];

                    if (coefficient == 0)
                        continue;

                    shift += coefficient * offsets[j];

                    foreach ((int column, double factor) in terms[j])
                        a[row, column] += coefficient * factor;
                }

                b[row] = constraint.RightHandSide - shift;

                if (constraint.Sign == ConstraintSign.LessOrEqual)
                    a[row, slack++] = 1.0;
                else if (constraint.Sign == ConstraintSign.GreaterOrEqual)
                    a[row, slack++] = -1.0;

                row++;
            }

            foreach ((int column, double limit) in boundRows)
            {
                a[row, column] = 1.0;
                a[row, slack++] = 1.0;
                b[row] = limit;
                row++;
            }

            double objectiveShift = 0;
            double sign = program.Sense == ObjectiveSense.Minimize ? 1.0 : -1.0;

            for (int j = 0; j < count; j++)
            {
                double coefficient = sign * program.ObjectiveCoefficient(program.Variables[j]);

                if (coefficient == 0)
                    continue;

                objectiveShift += coefficient * offsets[j];

                foreach ((int column, double factor) in terms[j])
                    c[column] += coefficient * factor;
            }

            return new StandardForm(a, b, c, objectiveShift, terms, offsets);
        }

        /// <summary>Восстанавливает значения переменных исходной задачи</summary>
        internal double[] Recover(double[] solution)
        {
            var values = new double[_terms.Length];

            for (int j = 0; j < _terms.Length; j++)
            {
                double value = _offsets[j];

                foreach ((int column, double factor) in _terms[j])
                    value += factor * solution[column];

                values[j] = value;
            }

            return values;
        }
    }

    /// <summary>
    /// Собирает задачу из матрицы ограничений и векторов — краткая запись для случаев,
    /// когда имена переменных не нужны
    /// </summary>
    /// <param name="objective">Коэффициенты целевой функции</param>
    /// <param name="constraints">Матрица ограничений: строка — ограничение</param>
    /// <param name="signs">Знаки ограничений</param>
    /// <param name="rightHandSide">Правые части</param>
    /// <param name="sense">Направление оптимизации</param>
    public static LinearProgram FromMatrix(
        Vector objective,
        Matrix constraints,
        IReadOnlyList<ConstraintSign> signs,
        Vector rightHandSide,
        ObjectiveSense sense = ObjectiveSense.Minimize)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(signs);
        ArgumentNullException.ThrowIfNull(rightHandSide);

        if (constraints.Height != signs.Count || constraints.Height != rightHandSide.Count)
            throw new ArgumentException("Число строк матрицы, знаков и правых частей должно совпадать", nameof(signs));

        if (constraints.Width != objective.Count)
            throw new ArgumentException("Число столбцов матрицы должно совпадать с числом переменных", nameof(constraints));

        var program = new LinearProgram(sense);

        for (int j = 0; j < objective.Count; j++)
            _ = program.AddVariable($"x{j + 1}");

        program.SetObjective(objective);

        for (int i = 0; i < constraints.Height; i++)
        {
            var row = new Vector(constraints.Width);

            for (int j = 0; j < constraints.Width; j++)
                row[j] = constraints[i, j];

            _ = program.AddConstraint(row, signs[i], rightHandSide[i]);
        }

        return program;
    }
}

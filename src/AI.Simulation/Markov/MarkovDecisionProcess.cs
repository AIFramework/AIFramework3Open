using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Simulation.Markov;

/// <summary>Результат решения марковского процесса принятия решений</summary>
/// <param name="Values">Ценность каждого состояния</param>
/// <param name="Policy">Оптимальное действие в каждом состоянии</param>
/// <param name="Iterations">Число итераций до сходимости</param>
/// <param name="Residual">Наибольшее изменение ценности на последней итерации</param>
/// <param name="Converged">Достигнут ли порог сходимости</param>
public sealed record MdpSolution(
    Vector Values, IReadOnlyList<int> Policy, int Iterations, double Residual, bool Converged) : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Марковский процесс принятия решений")
            .Summary(Converged
                ? $"Решение найдено за {Iterations} итераций, остаточное изменение ценности "
                  + $"{Fmt.Num(Residual, 8)}. Стратегия оптимальна с точностью до порога."
                : $"Предел итераций исчерпан на {Iterations} шаге, остаток {Fmt.Num(Residual, 6)}: "
                  + "стратегия может быть не оптимальной.")
            .Metric("Состояний", Values.Count, null, "размер пространства состояний", MetricQuality.Unknown, 0)
            .Metric("Итераций", Iterations, null, "до достижения порога", MetricQuality.Unknown, 0)
            .Metric("Остаток", Fmt.Num(Residual, 8), null, "наибольшее изменение ценности за шаг",
                Converged ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Наибольшая ценность", Fmt.Num(Max(Values), 4), null, "у лучшего состояния")
            .Finding("Ценность состояния — это ожидаемая сумма будущих наград со скидкой, а не награда "
                + "в самом состоянии: выгодным оказывается то, откуда ведут хорошие пути.")
            .WarningIf(!Converged,
                "Итерации не сошлись. Обычные причины: коэффициент дисконтирования слишком близок "
                + "к единице либо предел итераций мал.")
            .Warning("Модель предполагает известными и вероятности переходов, и награды. Когда они "
                + "неизвестны и добываются взаимодействием со средой, нужны методы обучения "
                + "с подкреплением, а не эти уравнения.")
            .Build();

    private static double Max(Vector values)
    {
        double best = double.NegativeInfinity;

        for (int i = 0; i < values.Count; i++)
            best = Math.Max(best, values[i]);

        return best;
    }
}

/// <summary>
/// Марковский процесс принятия решений с конечным числом состояний и действий.
/// </summary>
/// <remarks>
/// <para>
/// Задаётся вероятностями переходов <c>P(s' | s, a)</c> и наградами <c>R(s, a)</c>.
/// Решается итерацией по ценностям либо итерацией по стратегиям: первая проще и сходится
/// линейно, вторая делает меньше итераций, но каждая дороже.
/// </para>
/// <para>
/// Коэффициент дисконтирования меньше единицы — не только предпочтение раннего вознаграждения,
/// но и условие сходимости: при единице сумма будущих наград на бесконечном горизонте
/// может расходиться.
/// </para>
/// </remarks>
public sealed class MarkovDecisionProcess
{
    private readonly double[][][] _transitions;
    private readonly double[][] _rewards;

    /// <summary>
    /// Создаёт процесс
    /// </summary>
    /// <param name="transitions">Вероятности переходов: состояние, действие, следующее состояние</param>
    /// <param name="rewards">Награды: состояние, действие</param>
    /// <param name="discount">Коэффициент дисконтирования от нуля до единицы</param>
    public MarkovDecisionProcess(double[][][] transitions, double[][] rewards, double discount = 0.95)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(rewards);

        if (discount is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(discount),
                "Коэффициент дисконтирования лежит строго между нулём и единицей");

        if (transitions.Length != rewards.Length)
            throw new ArgumentException("Число состояний в переходах и наградах должно совпадать", nameof(rewards));

        for (int state = 0; state < transitions.Length; state++)
        {
            foreach (double[] row in transitions[state])
            {
                double sum = row.Sum();

                if (Math.Abs(sum - 1.0) > 1e-6)
                    throw new ArgumentException(
                        $"Вероятности переходов из состояния {state} дают в сумме {sum:F4} вместо единицы",
                        nameof(transitions));
            }
        }

        _transitions = transitions;
        _rewards = rewards;
        Discount = discount;
    }

    /// <summary>Число состояний</summary>
    public int StateCount => _transitions.Length;

    /// <summary>Коэффициент дисконтирования</summary>
    public double Discount { get; }

    /// <summary>Число действий, доступных в состоянии</summary>
    /// <param name="state">Состояние</param>
    public int ActionCount(int state) => _transitions[state].Length;

    /// <summary>
    /// Итерация по ценностям: уравнение Беллмана применяется до сходимости
    /// </summary>
    /// <param name="tolerance">Порог по наибольшему изменению ценности</param>
    /// <param name="maxIterations">Предел итераций</param>
    public MdpSolution SolveByValueIteration(double tolerance = 1e-10, int maxIterations = 10_000)
    {
        var values = new double[StateCount];
        int iteration = 0;
        double residual = double.PositiveInfinity;

        for (; iteration < maxIterations; iteration++)
        {
            residual = 0;

            for (int state = 0; state < StateCount; state++)
            {
                double best = double.NegativeInfinity;

                for (int action = 0; action < ActionCount(state); action++)
                    best = Math.Max(best, ActionValue(state, action, values));

                residual = Math.Max(residual, Math.Abs(best - values[state]));
                values[state] = best;
            }

            if (residual < tolerance)
            {
                iteration++;
                break;
            }
        }

        return new MdpSolution(new Vector(values), GreedyPolicy(values), iteration, residual, residual < tolerance);
    }

    /// <summary>
    /// Итерация по стратегиям: оценка текущей стратегии чередуется с её улучшением
    /// </summary>
    /// <param name="tolerance">Порог оценки стратегии</param>
    /// <param name="maxIterations">Предел итераций улучшения</param>
    public MdpSolution SolveByPolicyIteration(double tolerance = 1e-10, int maxIterations = 1000)
    {
        var policy = new int[StateCount];
        var values = new double[StateCount];
        int iteration = 0;

        for (; iteration < maxIterations; iteration++)
        {
            values = EvaluatePolicy(policy, tolerance);
            IReadOnlyList<int> improved = GreedyPolicy(values);

            bool stable = true;

            for (int state = 0; state < StateCount; state++)
            {
                if (improved[state] == policy[state])
                    continue;

                policy[state] = improved[state];
                stable = false;
            }

            if (stable)
            {
                iteration++;
                break;
            }
        }

        return new MdpSolution(new Vector(values), policy, iteration, 0, iteration < maxIterations);
    }

    /// <summary>
    /// Ценность состояний при заданной стратегии
    /// </summary>
    /// <param name="policy">Действие в каждом состоянии</param>
    /// <param name="tolerance">Порог сходимости</param>
    /// <param name="maxIterations">Предел итераций</param>
    public double[] EvaluatePolicy(IReadOnlyList<int> policy, double tolerance = 1e-10, int maxIterations = 10_000)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var values = new double[StateCount];

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            double residual = 0;

            for (int state = 0; state < StateCount; state++)
            {
                double updated = ActionValue(state, policy[state], values);
                residual = Math.Max(residual, Math.Abs(updated - values[state]));
                values[state] = updated;
            }

            if (residual < tolerance)
                break;
        }

        return values;
    }

    /// <summary>Ожидаемая ценность действия при текущих оценках состояний</summary>
    /// <param name="state">Состояние</param>
    /// <param name="action">Действие</param>
    /// <param name="values">Текущие ценности состояний</param>
    public double ActionValue(int state, int action, IReadOnlyList<double> values)
    {
        double expected = 0;
        double[] row = _transitions[state][action];

        for (int next = 0; next < row.Length; next++)
        {
            if (row[next] != 0)
                expected += row[next] * values[next];
        }

        return _rewards[state][action] + (Discount * expected);
    }

    private IReadOnlyList<int> GreedyPolicy(IReadOnlyList<double> values)
    {
        var policy = new int[StateCount];

        for (int state = 0; state < StateCount; state++)
        {
            double best = double.NegativeInfinity;

            for (int action = 0; action < ActionCount(state); action++)
            {
                double value = ActionValue(state, action, values);

                if (value <= best)
                    continue;

                best = value;
                policy[state] = action;
            }
        }

        return policy;
    }
}

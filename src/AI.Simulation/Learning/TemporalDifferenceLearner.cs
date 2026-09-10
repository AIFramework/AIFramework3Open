namespace AI.Simulation.Learning;

/// <summary>
/// Табличное обучение с подкреплением по временным разностям: Q-learning и SARSA.
/// </summary>
/// <remarks>
/// <para>
/// Оценка ценности действия сдвигается к цели «награда плюс дисконтированная ценность
/// следующего шага»; методы различаются только тем, какой следующий шаг берётся в цель.
/// </para>
/// <para>
/// Шаг обучения для пары убывает как <c>1 / (1 + n)^ω</c>, где n — сколько раз пару испытали.
/// При <c>0.5 &lt; ω ≤ 1</c> выполнены условия Роббинса — Монро (сумма шагов расходится,
/// сумма квадратов сходится), и оценки Q-learning сходятся к оптимальным. Постоянный шаг
/// удобнее, но сходимости не даёт: оценки навсегда остаются шумными.
/// </para>
/// <para>
/// Исследование ε-жадное с долей <c>ε₀ / (1 + d·k)</c> в эпизоде k: убывает к нулю, но так,
/// что каждая пара испытывается бесконечно часто. Для SARSA это и есть условие сходимости
/// к оптимуму.
/// </para>
/// </remarks>
public sealed class TemporalDifferenceLearner
{
    /// <summary>Метод обучения</summary>
    public LearningMethod Method { get; init; } = LearningMethod.QLearning;

    /// <summary>Коэффициент дисконтирования; для сверки с процессом должен совпадать с его</summary>
    public double Discount { get; init; } = 0.95;

    /// <summary>Начальная доля случайных действий ε₀</summary>
    public double InitialExploration { get; init; } = 0.2;

    /// <summary>Скорость убывания доли случайных действий d</summary>
    public double ExplorationDecay { get; init; } = 0.001;

    /// <summary>Показатель убывания шага обучения ω</summary>
    public double LearningRateExponent { get; init; } = 0.6;

    /// <summary>
    /// Обучает стратегию
    /// </summary>
    /// <param name="environment">Среда</param>
    /// <param name="episodes">Число эпизодов</param>
    /// <param name="stepsPerEpisode">Наибольшее число шагов в эпизоде</param>
    /// <param name="seed">Зерно генератора; нужно для воспроизводимости</param>
    public LearningResult Train(IDiscreteEnvironment environment, int episodes, int stepsPerEpisode, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(episodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepsPerEpisode);

        if (Discount is < 0 or >= 1)
            throw new InvalidOperationException("Коэффициент дисконтирования лежит на промежутке [0, 1)");

        if (LearningRateExponent is <= 0.5 or > 1)
            throw new InvalidOperationException(
                "Показатель шага обучения должен лежать в (0.5, 1]: иначе условия сходимости не выполнены");

        var random = seed is null ? new Random() : new Random(seed.Value);
        int states = environment.StateCount;

        var q = new double[states][];
        var visits = new int[states][];

        for (int state = 0; state < states; state++)
        {
            int actions = environment.ActionCount(state);

            if (actions <= 0)
                throw new InvalidOperationException($"В состоянии {state} нет ни одного действия");

            q[state] = new double[actions];
            visits[state] = new int[actions];
        }

        long steps = 0;
        double exploration = InitialExploration;

        for (int episode = 0; episode < episodes; episode++)
        {
            exploration = InitialExploration / (1 + (ExplorationDecay * episode));

            int state = environment.Reset(random);
            int action = Choose(q[state], exploration, random);

            for (int t = 0; t < stepsPerEpisode; t++)
            {
                Transition outcome = environment.Step(state, action, random);
                steps++;

                int nextAction = Choose(q[outcome.NextState], exploration, random);
                double target = outcome.Reward;

                if (!outcome.Terminal)
                {
                    double next = Method == LearningMethod.QLearning
                        ? q[outcome.NextState].Max()
                        : q[outcome.NextState][nextAction];

                    target += Discount * next;
                }

                visits[state][action]++;
                double rate = 1.0 / Math.Pow(1 + visits[state][action], LearningRateExponent);
                q[state][action] += rate * (target - q[state][action]);

                if (outcome.Terminal)
                    break;

                state = outcome.NextState;
                action = nextAction;
            }
        }

        var policy = new int[states];
        int unvisited = 0;

        for (int state = 0; state < states; state++)
        {
            policy[state] = Greedy(q[state]);
            unvisited += visits[state].Count(v => v == 0);
        }

        return new LearningResult(Method, q, policy, episodes, steps, exploration, unvisited);
    }

    private static int Choose(double[] values, double exploration, Random random)
        => random.NextDouble() < exploration ? random.Next(values.Length) : Greedy(values);

    // При равенстве оценок берётся первое действие: выбор воспроизводим при заданном зерне
    private static int Greedy(double[] values)
    {
        int best = 0;

        for (int action = 1; action < values.Length; action++)
        {
            if (values[action] > values[best])
                best = action;
        }

        return best;
    }
}

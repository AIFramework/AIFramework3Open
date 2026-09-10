using AI.Insights;

namespace AI.Simulation.Learning;

/// <summary>Итог обучения с подкреплением</summary>
/// <param name="Method">Метод обучения</param>
/// <param name="QValues">Оценки ценности действий: состояние, действие</param>
/// <param name="Policy">Жадная стратегия по выученным оценкам</param>
/// <param name="Episodes">Число эпизодов</param>
/// <param name="Steps">Число шагов в среде</param>
/// <param name="FinalExploration">Доля случайных действий в последнем эпизоде</param>
/// <param name="UnvisitedPairs">Сколько пар «состояние — действие» не испытано ни разу</param>
public sealed record LearningResult(
    LearningMethod Method,
    IReadOnlyList<double[]> QValues,
    IReadOnlyList<int> Policy,
    int Episodes,
    long Steps,
    double FinalExploration,
    int UnvisitedPairs) : IInterpretable
{
    /// <summary>Выученная ценность состояния: оценка лучшего действия в нём</summary>
    /// <param name="state">Состояние</param>
    public double Value(int state) => QValues[state].Max();

    /// <summary>Доля состояний, в которых выученная стратегия совпадает с эталонной</summary>
    /// <param name="reference">Эталонная стратегия, например точное решение процесса</param>
    public double PolicyAgreement(IReadOnlyList<int> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Count != Policy.Count)
            throw new ArgumentException("Число состояний в стратегиях не совпадает", nameof(reference));

        int same = 0;

        for (int state = 0; state < Policy.Count; state++)
        {
            if (Policy[state] == reference[state])
                same++;
        }

        return Policy.Count == 0 ? 1 : (double)same / Policy.Count;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string method = Method == LearningMethod.QLearning ? "Q-learning" : "SARSA";

        return new InterpretationBuilder($"Обучение с подкреплением: {method}")
            .Summary($"Стратегия выучена за {Episodes} эпизодов и {Steps} шагов в среде. "
                + (UnvisitedPairs == 0
                    ? "Каждая пара «состояние — действие» испытана хотя бы раз."
                    : $"Не испытано пар «состояние — действие»: {UnvisitedPairs}."))
            .Metric("Состояний", Policy.Count, null, "размер таблицы оценок", MetricQuality.Unknown, 0)
            .Metric("Эпизодов", Episodes, null, null, MetricQuality.Unknown, 0)
            .Metric("Шагов", Steps, null, "испытаний среды", MetricQuality.Unknown, 0)
            .Metric("Исследование в конце", Fmt.Pct(FinalExploration), null, "доля случайных действий")
            .Metric("Непосещённых пар", UnvisitedPairs, null, "их оценки остались начальными",
                UnvisitedPairs == 0 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .FindingIf(Method == LearningMethod.QLearning,
                "Q-learning учит ценность лучшего действия независимо от того, как агент исследует: "
                + "выученная стратегия — оценка оптимальной, а не той, по которой шло обучение.")
            .FindingIf(Method == LearningMethod.Sarsa,
                "SARSA учит ценность той стратегии, по которой агент действительно действует, вместе "
                + "с его случайными шагами. Там, где случайный шаг дорог, она выбирает путь осторожнее оптимального.")
            .WarningIf(UnvisitedPairs > 0,
                "Для непосещённых пар выученной ценности нет — там стоит начальная оценка, и выбор "
                + "действия в таком состоянии ничем не обоснован.")
            .Warning("Сходимость к оптимуму гарантирована только в пределе: каждая пара должна испытываться "
                + "бесконечно часто, а шаг обучения — убывать. Конечное обучение даёт приближение, "
                + "и его качество видно лишь по сверке.")
            .Recommendation("Если модель среды известна, решить её точно и сверить стратегии: "
                + "совпадение — лучшая проверка того, что обучение закончено.")
            .Build();
    }
}

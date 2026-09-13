namespace AI.Psychology.Traits;

/// <summary>
/// Черты личности по пятифакторной модели (Big Five), каждая на [−1; 1]: 0 — средний уровень в популяции.
/// </summary>
/// <remarks>
/// Черты устойчивы годами и задают не сами эмоции, а их фон: насколько легко человека встряхнуть,
/// как быстро он успокаивается, насколько заразительны его чувства для других. Качественно эти связи
/// известны — экстраверты выразительнее, у эмоционально устойчивых короче возврат к фону, — а
/// проверенных числовых коэффициентов для них нет. Поэтому перевод черт в параметры модели здесь
/// не зашит, его задаёт пользователь.
/// </remarks>
/// <param name="Openness">Открытость опыту</param>
/// <param name="Conscientiousness">Добросовестность</param>
/// <param name="Extraversion">Экстраверсия</param>
/// <param name="Agreeableness">Доброжелательность</param>
/// <param name="Neuroticism">Нейротизм — противоположность эмоциональной устойчивости</param>
public sealed record Personality(double Openness, double Conscientiousness, double Extraversion, double Agreeableness, double Neuroticism)
{
    /// <summary>Средний человек: все черты на уровне популяции</summary>
    public static Personality Average { get; } = new(0, 0, 0, 0, 0);

    /// <summary>Лежат ли все черты на [−1; 1]</summary>
    public bool IsValid => Traits.All(value => value >= -1 && value <= 1);

    /// <summary>Черты в порядке O, C, E, A, N</summary>
    public IReadOnlyList<double> Traits => [Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism];

    /// <summary>Черты по баллам опросника: линейный перевод шкалы [min; max] на [−1; 1]</summary>
    /// <param name="scores">Баллы в порядке O, C, E, A, N</param>
    /// <param name="min">Нижний балл шкалы, например 1</param>
    /// <param name="max">Верхний балл шкалы, например 5</param>
    public static Personality FromScale(IReadOnlyList<double> scores, double min = 1, double max = 5)
    {
        ArgumentNullException.ThrowIfNull(scores);

        if (scores.Count != 5)
            throw new ArgumentException("Нужно пять баллов: O, C, E, A, N", nameof(scores));

        if (!(max > min))
            throw new ArgumentException("Верхний балл шкалы должен быть больше нижнего");

        if (scores.Any(score => !(score >= min && score <= max)))
            throw new ArgumentOutOfRangeException(nameof(scores), $"Баллы лежат на [{min}; {max}]");

        double[] traits = [.. scores.Select(score => (2 * (score - min) / (max - min)) - 1)];

        return new Personality(traits[0], traits[1], traits[2], traits[3], traits[4]);
    }
}

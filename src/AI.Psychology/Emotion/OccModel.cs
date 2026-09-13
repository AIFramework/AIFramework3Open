namespace AI.Psychology.Emotion;

/// <summary>Двадцать две эмоции модели OCC (Ортони, Клор и Коллинз, 1988)</summary>
public enum OccEmotion
{
    /// <summary>Радость: желательное событие случилось</summary>
    Joy,

    /// <summary>Огорчение: нежелательное событие случилось</summary>
    Distress,

    /// <summary>Надежда: желательное событие ожидается</summary>
    Hope,

    /// <summary>Страх: нежелательное событие ожидается</summary>
    Fear,

    /// <summary>Удовлетворение: ожидаемое желательное подтвердилось</summary>
    Satisfaction,

    /// <summary>Сбывшиеся опасения: ожидаемое нежелательное подтвердилось</summary>
    FearsConfirmed,

    /// <summary>Облегчение: ожидаемое нежелательное не случилось</summary>
    Relief,

    /// <summary>Разочарование: ожидаемое желательное не случилось</summary>
    Disappointment,

    /// <summary>Радость за другого: хорошее у того, кто нравится</summary>
    HappyFor,

    /// <summary>Жалость: плохое у того, кто нравится</summary>
    Pity,

    /// <summary>Досада на чужую удачу: хорошее у того, кто не нравится</summary>
    Resentment,

    /// <summary>Злорадство: плохое у того, кто не нравится</summary>
    Gloating,

    /// <summary>Гордость: свой поступок, достойный одобрения</summary>
    Pride,

    /// <summary>Стыд: свой поступок, достойный порицания</summary>
    Shame,

    /// <summary>Восхищение: чужой поступок, достойный одобрения</summary>
    Admiration,

    /// <summary>Упрёк: чужой поступок, достойный порицания</summary>
    Reproach,

    /// <summary>Самоудовлетворение: радость от своего достойного поступка</summary>
    Gratification,

    /// <summary>Раскаяние: огорчение от своего недостойного поступка</summary>
    Remorse,

    /// <summary>Благодарность: радость от чужого достойного поступка</summary>
    Gratitude,

    /// <summary>Гнев: огорчение от чужого недостойного поступка</summary>
    Anger,

    /// <summary>Симпатия: привлекательный объект</summary>
    Love,

    /// <summary>Неприязнь: отталкивающий объект</summary>
    Hate
}

/// <summary>Состояние события во времени</summary>
public enum EventStatus
{
    /// <summary>Событие произошло и не ожидалось заранее</summary>
    Present,

    /// <summary>Событие ожидается</summary>
    Prospective,

    /// <summary>Ожидавшееся событие произошло</summary>
    Confirmed,

    /// <summary>Ожидавшееся событие не произошло</summary>
    Disconfirmed
}

/// <summary>Кто совершил поступок, из-за которого случилось событие</summary>
public enum Agency
{
    /// <summary>Ничей поступок: погода, случай</summary>
    None,

    /// <summary>Сам оценивающий</summary>
    Self,

    /// <summary>Другой человек</summary>
    Other
}

/// <summary>Оценка ситуации по переменным модели OCC; все шкалы на [−1; 1], вероятность на [0; 1]</summary>
public sealed record Appraisal
{
    /// <summary>Желательность события для целей оценивающего</summary>
    public double Desirability { get; init; }

    /// <summary>Состояние события: случилось, ожидается, подтвердилось, не подтвердилось</summary>
    public EventStatus Status { get; init; } = EventStatus.Present;

    /// <summary>Вероятность ожидаемого события</summary>
    public double Likelihood { get; init; } = 1;

    /// <summary>Желательность события для другого человека; <c>null</c> — событие другого не касается</summary>
    public double? DesirabilityForOther { get; init; }

    /// <summary>Насколько оценивающему нравится этот другой</summary>
    public double LikingOfOther { get; init; }

    /// <summary>Чей поступок привёл к событию</summary>
    public Agency Agent { get; init; } = Agency.None;

    /// <summary>Похвальность поступка: от порицаемого к одобряемому</summary>
    public double Praiseworthiness { get; init; }

    /// <summary>Привлекательность объекта; <c>null</c> — речь не об объекте</summary>
    public double? Appeal { get; init; }
}

/// <summary>Эмоция, вызванная оценкой, и её сила на [0; 1]</summary>
/// <param name="Emotion">Эмоция</param>
/// <param name="Intensity">Сила</param>
public readonly record struct ElicitedEmotion(OccEmotion Emotion, double Intensity);

/// <summary>
/// Модель OCC: какие эмоции вызывает ситуация в зависимости от того, как человек её оценил.
/// </summary>
/// <remarks>
/// <para>
/// Эмоция здесь — не реакция на событие как таковое, а на его оценку. Одно и то же событие радует
/// одного и огорчает другого, потому что желательно для целей первого и нежелательно для второго.
/// Модель различает три источника эмоций: последствия событий (для себя и для других), поступки
/// людей (свои и чужие) и свойства объектов. Сложные эмоции складываются из простых: гнев — это
/// огорчение плюс упрёк тому, кто виноват, благодарность — радость плюс восхищение.
/// </para>
/// <para>
/// OCC задаёт структуру и переменные оценки, но не формулы силы. Здесь сила — произведение
/// переменных: желательности на вероятность для ожиданий, желательности для другого на симпатию к
/// нему; у сложной эмоции — среднее её составляющих. Это соглашение реализаций, а не часть теории.
/// Перевода эмоций OCC в координаты PAD здесь нет: таблицу Гебхарда (2005) не удалось сверить с
/// первоисточником, и её числа не взяты.
/// </para>
/// </remarks>
public static class OccModel
{
    /// <summary>Названия эмоций по-русски</summary>
    public static IReadOnlyDictionary<OccEmotion, string> Labels { get; } = new Dictionary<OccEmotion, string>
    {
        [OccEmotion.Joy] = "радость",
        [OccEmotion.Distress] = "огорчение",
        [OccEmotion.Hope] = "надежда",
        [OccEmotion.Fear] = "страх",
        [OccEmotion.Satisfaction] = "удовлетворение",
        [OccEmotion.FearsConfirmed] = "сбывшиеся опасения",
        [OccEmotion.Relief] = "облегчение",
        [OccEmotion.Disappointment] = "разочарование",
        [OccEmotion.HappyFor] = "радость за другого",
        [OccEmotion.Pity] = "жалость",
        [OccEmotion.Resentment] = "досада на чужую удачу",
        [OccEmotion.Gloating] = "злорадство",
        [OccEmotion.Pride] = "гордость",
        [OccEmotion.Shame] = "стыд",
        [OccEmotion.Admiration] = "восхищение",
        [OccEmotion.Reproach] = "упрёк",
        [OccEmotion.Gratification] = "самоудовлетворение",
        [OccEmotion.Remorse] = "раскаяние",
        [OccEmotion.Gratitude] = "благодарность",
        [OccEmotion.Anger] = "гнев",
        [OccEmotion.Love] = "симпатия",
        [OccEmotion.Hate] = "неприязнь"
    };

    private static readonly HashSet<OccEmotion> Positive =
    [
        OccEmotion.Joy, OccEmotion.Hope, OccEmotion.Satisfaction, OccEmotion.Relief, OccEmotion.HappyFor,
        OccEmotion.Gloating, OccEmotion.Pride, OccEmotion.Admiration, OccEmotion.Gratification, OccEmotion.Gratitude, OccEmotion.Love
    ];

    /// <summary>Приятна ли эмоция тому, кто её испытывает: злорадство приятно, жалость — нет</summary>
    /// <param name="emotion">Эмоция</param>
    public static bool IsPositive(OccEmotion emotion) => Positive.Contains(emotion);

    /// <summary>Эмоции, которые вызывает оценка, от сильной к слабой</summary>
    /// <param name="appraisal">Оценка ситуации</param>
    /// <param name="threshold">Порог: эмоции не сильнее него отбрасываются</param>
    public static IReadOnlyList<ElicitedEmotion> Appraise(Appraisal appraisal, double threshold = 0)
    {
        ArgumentNullException.ThrowIfNull(appraisal);
        RequireSigned(appraisal.Desirability, nameof(Appraisal.Desirability));
        RequireSigned(appraisal.LikingOfOther, nameof(Appraisal.LikingOfOther));
        RequireSigned(appraisal.Praiseworthiness, nameof(Appraisal.Praiseworthiness));
        Internal.Numerics.RequireProbability(appraisal.Likelihood, nameof(Appraisal.Likelihood));

        if (appraisal.DesirabilityForOther is { } forOther)
            RequireSigned(forOther, nameof(Appraisal.DesirabilityForOther));

        if (appraisal.Appeal is { } appeal)
            RequireSigned(appeal, nameof(Appraisal.Appeal));

        if (!(threshold >= 0))
            throw new ArgumentOutOfRangeException(nameof(threshold), "Порог — неотрицательное число");

        var emotions = new Dictionary<OccEmotion, double>();
        double d = appraisal.Desirability;
        double good = Math.Max(d, 0), bad = Math.Max(-d, 0);

        // Последствия событий для себя: благополучие и ожидания
        switch (appraisal.Status)
        {
            case EventStatus.Present:
                Add(emotions, OccEmotion.Joy, good);
                Add(emotions, OccEmotion.Distress, bad);
                break;
            case EventStatus.Prospective:
                Add(emotions, OccEmotion.Hope, good * appraisal.Likelihood);
                Add(emotions, OccEmotion.Fear, bad * appraisal.Likelihood);
                break;
            case EventStatus.Confirmed:
                Add(emotions, OccEmotion.Satisfaction, good);
                Add(emotions, OccEmotion.FearsConfirmed, bad);
                Add(emotions, OccEmotion.Joy, good);
                Add(emotions, OccEmotion.Distress, bad);
                break;
            case EventStatus.Disconfirmed:
                Add(emotions, OccEmotion.Disappointment, good);
                Add(emotions, OccEmotion.Relief, bad);
                break;
        }

        // Последствия для другого: зависят и от события, и от отношения к нему
        if (appraisal.DesirabilityForOther is { } other && appraisal.LikingOfOther != 0)
        {
            double liking = appraisal.LikingOfOther;
            double strength = Math.Abs(other) * Math.Abs(liking);

            OccEmotion fortune = (other > 0, liking > 0) switch
            {
                (true, true) => OccEmotion.HappyFor,
                (false, true) => OccEmotion.Pity,
                (true, false) => OccEmotion.Resentment,
                (false, false) => OccEmotion.Gloating
            };

            if (other != 0)
                Add(emotions, fortune, strength);
        }

        // Поступки людей: свои и чужие
        double praise = appraisal.Praiseworthiness;

        if (praise != 0 && appraisal.Agent != Agency.None)
        {
            OccEmotion attribution = (appraisal.Agent, praise > 0) switch
            {
                (Agency.Self, true) => OccEmotion.Pride,
                (Agency.Self, false) => OccEmotion.Shame,
                (Agency.Other, true) => OccEmotion.Admiration,
                _ => OccEmotion.Reproach
            };

            Add(emotions, attribution, Math.Abs(praise));
        }

        // Сложные эмоции: благополучие от события плюс оценка поступка, который его вызвал
        Compound(emotions, OccEmotion.Joy, OccEmotion.Pride, OccEmotion.Gratification);
        Compound(emotions, OccEmotion.Distress, OccEmotion.Shame, OccEmotion.Remorse);
        Compound(emotions, OccEmotion.Joy, OccEmotion.Admiration, OccEmotion.Gratitude);
        Compound(emotions, OccEmotion.Distress, OccEmotion.Reproach, OccEmotion.Anger);

        // Объекты: привлекательность
        if (appraisal.Appeal is { } objectAppeal)
        {
            Add(emotions, OccEmotion.Love, Math.Max(objectAppeal, 0));
            Add(emotions, OccEmotion.Hate, Math.Max(-objectAppeal, 0));
        }

        return [.. emotions
            .Where(pair => pair.Value > threshold)
            .Select(pair => new ElicitedEmotion(pair.Key, pair.Value))
            .OrderByDescending(emotion => emotion.Intensity)
            .ThenBy(emotion => emotion.Emotion)];
    }

    private static void Add(Dictionary<OccEmotion, double> emotions, OccEmotion emotion, double intensity)
    {
        if (intensity > 0)
            emotions[emotion] = Math.Max(emotions.GetValueOrDefault(emotion), intensity);
    }

    private static void Compound(Dictionary<OccEmotion, double> emotions, OccEmotion wellBeing, OccEmotion attribution, OccEmotion compound)
    {
        if (emotions.TryGetValue(wellBeing, out double first) && emotions.TryGetValue(attribution, out double second))
            Add(emotions, compound, (first + second) / 2);
    }

    private static void RequireSigned(double value, string name)
    {
        if (!(value >= -1 && value <= 1))
            throw new ArgumentOutOfRangeException(name, "Шкала оценки лежит на [−1; 1]");
    }
}

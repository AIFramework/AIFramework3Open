namespace AI.Psychology.Emotion;

/// <summary>Октант пространства PAD: восемь типов настроения по знакам трёх осей (Мехрабян)</summary>
public enum MoodOctant
{
    /// <summary>+P +A +D: воодушевление</summary>
    Exuberant,

    /// <summary>−P −A −D: скука</summary>
    Bored,

    /// <summary>+P +A −D: зависимость</summary>
    Dependent,

    /// <summary>−P −A +D: презрение</summary>
    Disdainful,

    /// <summary>+P −A +D: расслабленность</summary>
    Relaxed,

    /// <summary>−P +A −D: тревога</summary>
    Anxious,

    /// <summary>+P −A −D: покорность</summary>
    Docile,

    /// <summary>−P +A +D: враждебность</summary>
    Hostile
}

/// <summary>
/// Аффект в пространстве PAD Мехрабяна и Рассела: приятность, возбуждение, доминирование, каждое на [−1; 1].
/// </summary>
/// <remarks>
/// Три почти независимые оси описывают и мимолётную эмоцию, и настроение, и темперамент как среднее
/// состояние человека. Гнев и страх одинаково неприятны и возбуждены, а различает их доминирование:
/// гнев — ощущение власти над ситуацией, страх — бессилия. Октант по знакам осей даёт восемь типов
/// настроения.
/// </remarks>
/// <param name="Pleasure">Приятность P: от неудовольствия к удовольствию</param>
/// <param name="Arousal">Возбуждение A: от вялости к активности</param>
/// <param name="Dominance">Доминирование D: от подчинённости к контролю над ситуацией</param>
public readonly record struct Affect(double Pleasure, double Arousal, double Dominance)
{
    private static readonly IReadOnlyDictionary<MoodOctant, string> OctantLabels = new Dictionary<MoodOctant, string>
    {
        [MoodOctant.Exuberant] = "воодушевление",
        [MoodOctant.Bored] = "скука",
        [MoodOctant.Dependent] = "зависимость",
        [MoodOctant.Disdainful] = "презрение",
        [MoodOctant.Relaxed] = "расслабленность",
        [MoodOctant.Anxious] = "тревога",
        [MoodOctant.Docile] = "покорность",
        [MoodOctant.Hostile] = "враждебность"
    };

    /// <summary>Нейтральное состояние: начало координат</summary>
    public static Affect Neutral => default;

    /// <summary>Сила состояния: расстояние от нейтрального, делённое на √3, от 0 до 1 внутри куба</summary>
    public double Intensity => Math.Sqrt((Pleasure * Pleasure) + (Arousal * Arousal) + (Dominance * Dominance)) / Math.Sqrt(3);

    /// <summary>Октант по знакам осей; нуль считается положительным</summary>
    public MoodOctant Octant => (Pleasure >= 0, Arousal >= 0, Dominance >= 0) switch
    {
        (true, true, true) => MoodOctant.Exuberant,
        (false, false, false) => MoodOctant.Bored,
        (true, true, false) => MoodOctant.Dependent,
        (false, false, true) => MoodOctant.Disdainful,
        (true, false, true) => MoodOctant.Relaxed,
        (false, true, false) => MoodOctant.Anxious,
        (true, false, false) => MoodOctant.Docile,
        (false, true, true) => MoodOctant.Hostile
    };

    /// <summary>Лежит ли состояние внутри куба [−1; 1]³</summary>
    public bool IsWithinBounds => InUnit(Pleasure) && InUnit(Arousal) && InUnit(Dominance);

    /// <summary>Название октанта по-русски</summary>
    /// <param name="octant">Октант</param>
    public static string Label(MoodOctant octant) => OctantLabels[octant];

    /// <summary>Состояние, прижатое к границам куба [−1; 1]³</summary>
    public Affect Clamp() => new(Math.Clamp(Pleasure, -1, 1), Math.Clamp(Arousal, -1, 1), Math.Clamp(Dominance, -1, 1));

    /// <summary>Евклидово расстояние до другого состояния</summary>
    /// <param name="other">Другое состояние</param>
    public double DistanceTo(Affect other) =>
        Math.Sqrt(Square(Pleasure - other.Pleasure) + Square(Arousal - other.Arousal) + Square(Dominance - other.Dominance));

    /// <summary>Покомпонентная сумма</summary>
    public static Affect operator +(Affect left, Affect right) =>
        new(left.Pleasure + right.Pleasure, left.Arousal + right.Arousal, left.Dominance + right.Dominance);

    /// <summary>Покомпонентная разность</summary>
    public static Affect operator -(Affect left, Affect right) =>
        new(left.Pleasure - right.Pleasure, left.Arousal - right.Arousal, left.Dominance - right.Dominance);

    /// <summary>Умножение на число</summary>
    public static Affect operator *(double factor, Affect affect) =>
        new(factor * affect.Pleasure, factor * affect.Arousal, factor * affect.Dominance);

    /// <inheritdoc />
    public override string ToString() => $"P = {Pleasure:0.00}, A = {Arousal:0.00}, D = {Dominance:0.00} ({Label(Octant)})";

    internal static void Require(Affect affect, string name)
    {
        if (!affect.IsWithinBounds)
            throw new ArgumentOutOfRangeException(name, "Координаты PAD лежат на [−1; 1]");
    }

    private static bool InUnit(double value) => value >= -1 && value <= 1;

    private static double Square(double value) => value * value;
}

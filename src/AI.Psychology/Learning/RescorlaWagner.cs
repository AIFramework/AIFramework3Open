using AI.Psychology.Internal;

namespace AI.Psychology.Learning;

/// <summary>Проба обучения: какие стимулы предъявлены и какой исход последовал</summary>
/// <param name="Cues">Предъявленные стимулы</param>
/// <param name="Outcome">Величина подкрепления λ; нуль — подкрепления не было</param>
public sealed record ConditioningTrial(IReadOnlyList<string> Cues, double Outcome);

/// <summary>
/// Модель Рескорлы — Вагнера: обучение на ошибке предсказания.
/// </summary>
/// <remarks>
/// <para>
/// После пробы сила связи каждого предъявленного стимула меняется на ΔVᵢ = αᵢ·β·(λ − ΣV): обучение
/// идёт, пока исход неожидан, и тем быстрее, чем больше ошибка. Главное следствие — стимулы
/// соревнуются за одно подкрепление. Отсюда блокирование (Камин): если A уже предсказывает
/// подкрепление, добавленный к нему B почти ничему не учится; затенение: два стимула вместе делят
/// асимптоту; условное торможение: стимул, сопровождающий отмену ожидаемого подкрепления, получает
/// отрицательную силу.
/// </para>
/// <para>
/// Это та же формула, что в обучении с разницей во времени из <c>AI.Simulation</c>, только без
/// учёта будущих наград: ошибка предсказания сравнивает ожидание с исходом одной пробы. Модель не
/// объясняет латентное торможение и спонтанное восстановление после угасания; для них нужны модели
/// внимания (Пирс — Холл) и контекста.
/// </para>
/// </remarks>
public sealed class RescorlaWagnerModel
{
    private readonly Dictionary<string, double> _strength = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _salience = new(StringComparer.Ordinal);

    /// <summary>Создаёт модель</summary>
    /// <param name="salience">Заметность стимулов α по умолчанию</param>
    /// <param name="learningRate">Скорость обучения β, связанная с подкреплением</param>
    public RescorlaWagnerModel(double salience = 0.3, double learningRate = 1.0)
    {
        RequireRate(salience, nameof(salience));
        RequireRate(learningRate, nameof(learningRate));

        DefaultSalience = salience;
        LearningRate = learningRate;
    }

    /// <summary>Заметность стимула, для которого она не задана отдельно</summary>
    public double DefaultSalience { get; }

    /// <summary>Скорость обучения β</summary>
    public double LearningRate { get; }

    /// <summary>Силы связей всех встреченных стимулов</summary>
    public IReadOnlyDictionary<string, double> Strengths => _strength;

    /// <summary>Задаёт заметность отдельного стимула</summary>
    /// <param name="cue">Стимул</param>
    /// <param name="salience">Заметность α</param>
    public RescorlaWagnerModel WithSalience(string cue, double salience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cue);
        RequireRate(salience, nameof(salience));

        _salience[cue] = salience;
        return this;
    }

    /// <summary>Сила связи стимула; нуль, если стимул не встречался</summary>
    /// <param name="cue">Стимул</param>
    public double Strength(string cue) => _strength.GetValueOrDefault(cue);

    /// <summary>Ожидание подкрепления при предъявлении набора стимулов: ΣV</summary>
    /// <param name="cues">Стимулы</param>
    public double Predict(IEnumerable<string> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);

        return cues.Distinct(StringComparer.Ordinal).Sum(Strength);
    }

    /// <summary>Одна проба обучения</summary>
    /// <param name="cues">Предъявленные стимулы</param>
    /// <param name="outcome">Подкрепление λ</param>
    /// <returns>Ошибка предсказания λ − ΣV до обучения</returns>
    public double Trial(IEnumerable<string> cues, double outcome)
    {
        ArgumentNullException.ThrowIfNull(cues);
        Numerics.RequireFinite(outcome, nameof(outcome));

        string[] present = cues.Distinct(StringComparer.Ordinal).ToArray();

        if (present.Length == 0)
            throw new ArgumentException("В пробе должен быть хотя бы один стимул", nameof(cues));

        double error = outcome - present.Sum(Strength);

        foreach (string cue in present)
            _strength[cue] = Strength(cue) + (_salience.GetValueOrDefault(cue, DefaultSalience) * LearningRate * error);

        return error;
    }

    /// <summary>Серия проб; возвращает ошибку предсказания каждой</summary>
    /// <param name="trials">Пробы по порядку</param>
    public IReadOnlyList<double> Train(IEnumerable<ConditioningTrial> trials)
    {
        ArgumentNullException.ThrowIfNull(trials);

        return trials.Select(t => Trial(t.Cues, t.Outcome)).ToList();
    }

    /// <summary>
    /// Кривая приобретения для одного стимула в замкнутом виде: V(n) = λ·(1 − (1 − α·β)ⁿ)
    /// </summary>
    /// <param name="salience">Заметность α</param>
    /// <param name="learningRate">Скорость β</param>
    /// <param name="outcome">Подкрепление λ</param>
    /// <param name="trials">Число проб</param>
    public static double AcquisitionCurve(double salience, double learningRate, double outcome, int trials)
    {
        RequireRate(salience, nameof(salience));
        RequireRate(learningRate, nameof(learningRate));
        ArgumentOutOfRangeException.ThrowIfNegative(trials);

        return outcome * (1 - Math.Pow(1 - (salience * learningRate), trials));
    }

    private static void RequireRate(double value, string name)
    {
        if (!(value > 0 && value <= 1))
            throw new ArgumentOutOfRangeException(name, "Параметр скорости лежит в (0; 1]: больше единицы обучение раскачивается");
    }
}

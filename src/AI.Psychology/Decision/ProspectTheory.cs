using AI.Psychology.Internal;

namespace AI.Psychology.Decision;

/// <summary>Исход рискованного выбора: величина и вероятность</summary>
/// <param name="Value">Выигрыш (положительный) или проигрыш (отрицательный) относительно точки отсчёта</param>
/// <param name="Probability">Вероятность</param>
public readonly record struct Outcome(double Value, double Probability);

/// <summary>Параметры кумулятивной теории перспектив</summary>
/// <param name="GainCurvature">Кривизна ценности выигрышей α</param>
/// <param name="LossCurvature">Кривизна ценности проигрышей β</param>
/// <param name="LossAversion">Неприятие потерь λ</param>
/// <param name="GainWeighting">Параметр взвешивания вероятностей выигрышей γ</param>
/// <param name="LossWeighting">Параметр взвешивания вероятностей проигрышей δ</param>
public sealed record ProspectTheoryParameters(
    double GainCurvature, double LossCurvature, double LossAversion, double GainWeighting, double LossWeighting)
{
    /// <summary>Медианные оценки Тверски и Канемана (1992): α = β = 0,88, λ = 2,25, γ = 0,61, δ = 0,69</summary>
    public static ProspectTheoryParameters TverskyKahneman1992 { get; } = new(0.88, 0.88, 2.25, 0.61, 0.69);

    /// <summary>Нейтральный к риску: теория перспектив сводится к математическому ожиданию</summary>
    public static ProspectTheoryParameters RiskNeutral { get; } = new(1, 1, 1, 1, 1);

    internal void Validate()
    {
        Numerics.RequirePositive(GainCurvature, nameof(GainCurvature));
        Numerics.RequirePositive(LossCurvature, nameof(LossCurvature));
        Numerics.RequirePositive(LossAversion, nameof(LossAversion));

        // При γ < 0,28 функция взвешивания Тверски — Канемана перестаёт быть монотонной
        if (!(GainWeighting >= 0.28 && GainWeighting <= 2) || !(LossWeighting >= 0.28 && LossWeighting <= 2))
            throw new ArgumentOutOfRangeException(nameof(GainWeighting), "Параметры взвешивания лежат на [0,28; 2]: ниже функция не монотонна");
    }
}

/// <summary>
/// Кумулятивная теория перспектив Тверски и Канемана (1992): выбор при риске так, как его делают люди.
/// </summary>
/// <remarks>
/// <para>
/// Три отличия от ожидаемой полезности. Исходы оцениваются как выигрыши и потери относительно точки
/// отсчёта, а не как итоговое богатство. Потери весят больше выигрышей той же величины — в λ ≈ 2,25
/// раза. Вероятности входят не сами, а через обратную S-образную функцию w(p) = pᵞ/(pᵞ + (1−p)ᵞ)^(1/γ):
/// малые вероятности переоцениваются, большие недооцениваются.
/// </para>
/// <para>
/// Вместе это даёт «четырёхчастный рисунок»: при малой вероятности выигрыша люди ищут риск (лотереи),
/// при большой — избегают его; при малой вероятности потери избегают риска (страхование), при большой —
/// ищут. Веса кумулятивные: вес исхода — прирост w от вероятности получить не меньше него, поэтому
/// веса выигрышей всегда складываются в w(P(выигрыш)), и доминирование не нарушается.
/// </para>
/// <para>
/// Параметры 1992 года — медианы по студентам на гипотетических ставках; у отдельных людей и на
/// реальных деньгах они другие. Парадокс Алле при этих параметрах не воспроизводится: для
/// ставок в миллионах кривизна ценности сильнее взвешивания.
/// </para>
/// </remarks>
public static class ProspectTheory
{
    /// <summary>Функция ценности: xᵅ для выигрышей и −λ·(−x)ᵝ для потерь</summary>
    /// <param name="outcome">Исход относительно точки отсчёта</param>
    /// <param name="parameters">Параметры</param>
    public static double Value(double outcome, ProspectTheoryParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        Numerics.RequireFinite(outcome, nameof(outcome));

        return outcome >= 0
            ? Math.Pow(outcome, parameters.GainCurvature)
            : -parameters.LossAversion * Math.Pow(-outcome, parameters.LossCurvature);
    }

    /// <summary>Функция взвешивания вероятности Тверски — Канемана: pᵞ/(pᵞ + (1−p)ᵞ)^(1/γ)</summary>
    /// <param name="probability">Вероятность</param>
    /// <param name="curvature">Параметр γ</param>
    public static double Weight(double probability, double curvature)
    {
        Numerics.RequireProbability(probability, nameof(probability));
        Numerics.RequirePositive(curvature, nameof(curvature));

        if (probability == 0 || probability == 1)
            return probability;

        double numerator = Math.Pow(probability, curvature);

        return numerator / Math.Pow(numerator + Math.Pow(1 - probability, curvature), 1 / curvature);
    }

    /// <summary>Ценность перспективы по кумулятивной теории</summary>
    /// <param name="prospect">Исходы; вероятности в сумме единица</param>
    /// <param name="parameters">Параметры; по умолчанию Тверски и Канеман 1992</param>
    public static double Evaluate(IEnumerable<Outcome> prospect, ProspectTheoryParameters? parameters = null)
    {
        parameters ??= ProspectTheoryParameters.TverskyKahneman1992;
        parameters.Validate();

        List<Outcome> outcomes = Aggregate(prospect);
        double total = 0;
        double above = 0;

        // Выигрыши от лучшего к худшему: вес — прирост w от вероятности получить не меньше
        foreach (Outcome gain in outcomes.Where(o => o.Value > 0).OrderByDescending(o => o.Value))
        {
            double through = Math.Min(1, above + gain.Probability);
            total += (Weight(through, parameters.GainWeighting) - Weight(above, parameters.GainWeighting)) * Value(gain.Value, parameters);
            above = through;
        }

        double below = 0;

        // Потери от худшей к наименьшей: вес — прирост w от вероятности потерять не меньше
        foreach (Outcome loss in outcomes.Where(o => o.Value < 0).OrderBy(o => o.Value))
        {
            double through = Math.Min(1, below + loss.Probability);
            total += (Weight(through, parameters.LossWeighting) - Weight(below, parameters.LossWeighting)) * Value(loss.Value, parameters);
            below = through;
        }

        return total;
    }

    /// <summary>
    /// Детерминированный эквивалент: верная сумма, которую человек счёл бы равноценной перспективе
    /// </summary>
    /// <param name="prospect">Исходы</param>
    /// <param name="parameters">Параметры</param>
    public static double CertaintyEquivalent(IEnumerable<Outcome> prospect, ProspectTheoryParameters? parameters = null)
    {
        parameters ??= ProspectTheoryParameters.TverskyKahneman1992;
        double value = Evaluate(prospect, parameters);

        return value >= 0
            ? Math.Pow(value, 1 / parameters.GainCurvature)
            : -Math.Pow(-value / parameters.LossAversion, 1 / parameters.LossCurvature);
    }

    /// <summary>Математическое ожидание перспективы</summary>
    /// <param name="prospect">Исходы</param>
    public static double ExpectedValue(IEnumerable<Outcome> prospect) => Aggregate(prospect).Sum(o => o.Value * o.Probability);

    private static List<Outcome> Aggregate(IEnumerable<Outcome> prospect)
    {
        ArgumentNullException.ThrowIfNull(prospect);

        var byValue = new Dictionary<double, double>();

        foreach (Outcome outcome in prospect)
        {
            Numerics.RequireFinite(outcome.Value, nameof(prospect));
            Numerics.RequireProbability(outcome.Probability, nameof(prospect));

            byValue[outcome.Value] = byValue.GetValueOrDefault(outcome.Value) + outcome.Probability;
        }

        if (byValue.Count == 0 || Math.Abs(byValue.Values.Sum() - 1) > 1e-9)
            throw new ArgumentException("Вероятности исходов должны в сумме давать единицу", nameof(prospect));

        return byValue.Select(p => new Outcome(p.Key, p.Value)).ToList();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;
using AI.Statistics;

namespace AI.Economics.Econometrics;

/// <summary>Наблюдение панели для оценки эффекта вмешательства.</summary>
/// <param name="Unit">Идентификатор объекта.</param>
/// <param name="Period">Номер периода.</param>
/// <param name="Outcome">Значение отклика.</param>
/// <param name="FirstTreatedPeriod">Период начала воздействия; ноль означает, что объект не подвергался воздействию.</param>
public sealed record DidObservation(int Unit, int Period, double Outcome, int FirstTreatedPeriod)
{
    /// <summary>Действует ли воздействие в этом наблюдении.</summary>
    public bool IsTreated => FirstTreatedPeriod > 0 && Period >= FirstTreatedPeriod;

    /// <summary>Число периодов от начала воздействия; отрицательное до него.</summary>
    public int RelativePeriod => FirstTreatedPeriod > 0 ? Period - FirstTreatedPeriod : int.MinValue;
}

/// <summary>Оценка эффекта на конкретном расстоянии от момента воздействия.</summary>
/// <param name="RelativePeriod">Число периодов от начала воздействия.</param>
/// <param name="Estimate">Оценка эффекта.</param>
/// <param name="StandardError">Стандартная ошибка.</param>
/// <param name="Observations">Число наблюдений в оценке.</param>
public sealed record EventStudyPoint(
    int RelativePeriod, double Estimate, double StandardError, int Observations)
{
    /// <summary>Нижняя граница 95-процентного интервала.</summary>
    public double ConfidenceLow => Estimate - (1.96 * StandardError);

    /// <summary>Верхняя граница 95-процентного интервала.</summary>
    public double ConfidenceHigh => Estimate + (1.96 * StandardError);

    /// <summary>Значим ли эффект на уровне 5%.</summary>
    public bool IsSignificant => StandardError > 0 && Math.Abs(Estimate / StandardError) > 1.96;
}

/// <summary>Результат оценивания эффекта методом разности разностей.</summary>
public sealed record DidResult : IInterpretable
{
    /// <summary>Эффект по двусторонним фиксированным эффектам.</summary>
    public double TwoWayFixedEffects { get; init; }

    /// <summary>Стандартная ошибка оценки по двусторонним фиксированным эффектам.</summary>
    public double TwoWayStandardError { get; init; }

    /// <summary>Устойчивая к разновременному внедрению агрегированная оценка.</summary>
    public double RobustAtt { get; init; }

    /// <summary>Стандартная ошибка устойчивой оценки.</summary>
    public double RobustStandardError { get; init; }

    /// <summary>Динамика эффекта по периодам относительно внедрения.</summary>
    public IReadOnlyList<EventStudyPoint> EventStudy { get; init; } = [];

    /// <summary>Уровень значимости совместного теста на параллельность трендов до внедрения.</summary>
    public double PreTrendPValue { get; init; } = 1;

    /// <summary>Число когорт с разными датами внедрения.</summary>
    public int Cohorts { get; init; }

    /// <summary>Число объектов, не подвергавшихся воздействию.</summary>
    public int NeverTreated { get; init; }

    /// <summary>Число объектов под воздействием.</summary>
    public int Treated { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Уровень значимости устойчивой оценки.</summary>
    public double PValue =>
        RobustStandardError > 0 ? Distributions.NormalPValue(RobustAtt / RobustStandardError) : 1;

    /// <summary>Расхождение двух оценок: цена неоднородности эффекта по когортам.</summary>
    public double EstimatorGap => TwoWayFixedEffects - RobustAtt;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var leads = EventStudy.Where(p => p.RelativePeriod < 0).ToList();
        var lags = EventStudy.Where(p => p.RelativePeriod >= 0).ToList();

        EventStudyPoint? peak = lags.OrderByDescending(p => Math.Abs(p.Estimate)).FirstOrDefault();
        bool parallelTrends = PreTrendPValue >= 0.05;
        bool staggered = Cohorts > 1;
        bool significant = PValue < 0.05;

        var builder = new InterpretationBuilder("Разность разностей")
            .Summary($"Оценка эффекта {Fmt.Num(RobustAtt, 4)} (ст. ошибка {Fmt.Num(RobustStandardError, 4)}, " +
                     $"p = {Fmt.Num(PValue, 4)}) по {Observations} наблюдениям: {Treated} объектов " +
                     $"под воздействием, {NeverTreated} контрольных, {Cohorts} когорт внедрения. " +
                     $"Двусторонние фиксированные эффекты дают {Fmt.Num(TwoWayFixedEffects, 4)}. " +
                     $"Проверка параллельности трендов: p = {Fmt.Num(PreTrendPValue, 4)}.")
            .Metric("Эффект", RobustAtt, null,
                $"ст. ошибка {Fmt.Num(RobustStandardError, 4)}, p = {Fmt.Num(PValue, 4)}",
                significant ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Двусторонние фиксированные эффекты", TwoWayFixedEffects, null,
                $"ст. ошибка {Fmt.Num(TwoWayStandardError, 4)}", MetricQuality.Neutral, 4)
            .Metric("Расхождение оценок", EstimatorGap, null,
                staggered
                    ? "цена неоднородности эффекта при разновременном внедрении"
                    : "внедрение одновременное, оценки должны совпадать",
                Math.Abs(EstimatorGap) > Math.Abs(RobustAtt) * 0.25 ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Параллельность трендов", PreTrendPValue, null,
                parallelTrends ? "до внедрения группы двигались одинаково" : "тренды расходились до внедрения",
                parallelTrends ? MetricQuality.Good : MetricQuality.Critical, 4)
            .Metric("Когорт внедрения", Cohorts, null,
                staggered ? "внедрение разновременное" : "внедрение одновременное",
                MetricQuality.Neutral, 0)
            .Metric("Контрольных объектов", NeverTreated, null,
                $"под воздействием {Treated}",
                NeverTreated > 0 ? MetricQuality.Good : MetricQuality.Warning, 0);

        foreach (EventStudyPoint point in EventStudy)
        {
            builder.Metric($"Период {point.RelativePeriod:+0;-0;0}", point.Estimate, null,
                $"интервал [{Fmt.Num(point.ConfidenceLow, 3)}; {Fmt.Num(point.ConfidenceHigh, 3)}], " +
                $"наблюдений {point.Observations}",
                point.RelativePeriod < 0
                    ? point.IsSignificant ? MetricQuality.Warning : MetricQuality.Good
                    : point.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        return builder
            .FindingIf(peak is not null,
                $"Максимальный эффект достигается через {peak?.RelativePeriod} периодов после " +
                $"внедрения: {Fmt.Num(peak?.Estimate ?? 0, 4)}. Динамика эффекта важнее " +
                "единственного усреднённого числа: мгновенный скачок и постепенное нарастание " +
                "означают разные механизмы.")
            .FindingIf(staggered && Math.Abs(EstimatorGap) > Math.Abs(RobustAtt) * 0.25,
                $"Двусторонние фиксированные эффекты дают {Fmt.Num(TwoWayFixedEffects, 4)} против " +
                $"{Fmt.Num(RobustAtt, 4)} у устойчивой оценки. При разновременном внедрении " +
                "первая использует уже обработанные объекты как контроль и потому смещена — " +
                "иногда вплоть до смены знака.")
            .FindingIf(parallelTrends && leads.Count > 0,
                $"На {leads.Count} периодах до внедрения различий между группами не обнаружено. " +
                "Это поддерживает, но не доказывает предпосылку параллельных трендов: " +
                "она касается ненаблюдаемого контрфактического периода.")
            .WarningIf(!parallelTrends,
                $"Тренды до внедрения расходятся (p = {Fmt.Num(PreTrendPValue, 4)}). " +
                "Оценка эффекта в этом случае смешана с уже существовавшим различием " +
                "в динамике групп.")
            .WarningIf(NeverTreated == 0,
                "В выборке нет объектов, никогда не подвергавшихся воздействию. Контрольной " +
                "группой служат ещё не обработанные, и оценка эффекта на поздних периодах " +
                "опирается на всё меньшее число сравнений.")
            .WarningIf(leads.Count == 0,
                "Периодов до внедрения нет, проверить параллельность трендов невозможно. " +
                "Оценка целиком опирается на непроверяемое допущение.")
            .Warning("Стандартные ошибки получены кластерным бутстрапом по объектам. " +
                     "При малом числе кластеров (меньше тридцати) они занижены, и " +
                     "доверительные интервалы уже заявленных.")
            .Recommendation("Показывайте график динамики эффекта, а не только среднюю оценку: " +
                            "поведение коэффициентов до внедрения — главный аргумент " +
                            "в пользу дизайна.")
            .Recommendation("При разновременном внедрении опирайтесь на устойчивую оценку. " +
                            "Двусторонние фиксированные эффекты приводите для сравнения, " +
                            "а не как основной результат.")
            .Build();
    }
}

/// <summary>
/// Разность разностей, включая устойчивую к разновременному внедрению оценку и
/// динамику эффекта по периодам.
/// </summary>
/// <remarks>
/// <para>
/// Классический дизайн сравнивает изменение отклика в группе воздействия с
/// изменением в контрольной группе:
/// </para>
/// <code>
/// ATT = (E[Y_after | treated] - E[Y_before | treated])
///     - (E[Y_after | control] - E[Y_before | control])
/// </code>
/// <para>
/// Идентификация опирается на параллельность трендов: без вмешательства обе
/// группы двигались бы одинаково. Проверить это напрямую нельзя, но можно
/// посмотреть на периоды до внедрения — отсюда динамическая спецификация.
/// </para>
/// <para>
/// При разновременном внедрении регрессия с двусторонними фиксированными
/// эффектами перестаёт быть корректной: она неявно использует уже обработанные
/// объекты как контроль для обрабатываемых позже и взвешивает частные эффекты
/// весами, которые могут быть отрицательными. Здесь дополнительно считается
/// устойчивая оценка в духе Каллауэя и Сантанны: для каждой когорты и периода
/// эффект оценивается относительно последнего периода до внедрения и только
/// против ещё не обработанных объектов, после чего агрегируется.
/// </para>
/// </remarks>
public static class DifferenceInDifferences
{
    /// <summary>Оценивает эффект вмешательства.</summary>
    /// <param name="observations">Панель наблюдений с датой начала воздействия.</param>
    /// <param name="bootstrapSamples">Число повторов кластерного бутстрапа.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Оценки эффекта, динамика и проверка параллельности трендов.</returns>
    /// <exception cref="ArgumentNullException">Наблюдения не заданы.</exception>
    /// <exception cref="ArgumentException">Нет объектов под воздействием или контрольной группы.</exception>
    public static DidResult Estimate(
        IReadOnlyList<DidObservation> observations, int bootstrapSamples = 200, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(observations);

        if (observations.Count < 8)
            throw new ArgumentException("Наблюдений недостаточно.", nameof(observations));

        var treatedUnits = observations.Where(o => o.FirstTreatedPeriod > 0)
            .Select(o => o.Unit).Distinct().ToList();
        var controlUnits = observations.Where(o => o.FirstTreatedPeriod <= 0)
            .Select(o => o.Unit).Distinct().ToList();

        if (treatedUnits.Count == 0)
            throw new ArgumentException("Нет объектов под воздействием.", nameof(observations));

        (double twoWay, double twoWayError) = TwoWayEstimate(observations, bootstrapSamples, seed);
        (double robust, double robustError) = RobustEstimate(observations, bootstrapSamples, seed + 1);

        IReadOnlyList<EventStudyPoint> eventStudy = EventStudyPoints(observations, bootstrapSamples, seed + 2);

        var leads = eventStudy.Where(p => p.RelativePeriod < 0).ToList();
        double preTrend = 1;

        if (leads.Count > 0)
        {
            double statistic = leads.Sum(p =>
                p.StandardError > 0 ? Math.Pow(p.Estimate / p.StandardError, 2) : 0);

            preTrend = Distributions.ChiSquarePValue(statistic, leads.Count);
        }

        return new DidResult
        {
            TwoWayFixedEffects = twoWay,
            TwoWayStandardError = twoWayError,
            RobustAtt = robust,
            RobustStandardError = robustError,
            EventStudy = eventStudy,
            PreTrendPValue = preTrend,
            Cohorts = observations.Where(o => o.FirstTreatedPeriod > 0)
                .Select(o => o.FirstTreatedPeriod).Distinct().Count(),
            NeverTreated = controlUnits.Count,
            Treated = treatedUnits.Count,
            Observations = observations.Count,
        };
    }

    /// <summary>Оценка по двусторонним фиксированным эффектам с кластерным бутстрапом.</summary>
    private static (double Estimate, double StandardError) TwoWayEstimate(
        IReadOnlyList<DidObservation> observations, int samples, int seed)
    {
        double point = TwoWayOnce(observations);
        double[] draws = ClusterBootstrap(observations, samples, seed, TwoWayOnce);

        return (point, StandardDeviation(draws));
    }

    /// <summary>Одна оценка по двусторонним фиксированным эффектам.</summary>
    private static double TwoWayOnce(IReadOnlyList<DidObservation> observations)
    {
        var units = observations.Select(o => o.Unit).Distinct().OrderBy(u => u).ToList();
        var periods = observations.Select(o => o.Period).Distinct().OrderBy(p => p).ToList();

        var unitIndex = units.Select((u, i) => (u, i)).ToDictionary(p => p.u, p => p.i);
        var periodIndex = periods.Select((p, i) => (p, i)).ToDictionary(p => p.p, p => p.i);

        int n = observations.Count;
        int k = 1 + (units.Count - 1) + (periods.Count - 1);
        var design = new double[n, k];
        var response = new double[n];

        for (int i = 0; i < n; i++)
        {
            DidObservation o = observations[i];
            design[i, 0] = o.IsTreated ? 1 : 0;

            int unit = unitIndex[o.Unit];
            if (unit > 0) design[i, unit] = 1;

            int period = periodIndex[o.Period];
            if (period > 0) design[i, units.Count - 1 + period] = 1;

            response[i] = o.Outcome;
        }

        var names = new List<string> { "воздействие" };
        for (int j = 1; j < k; j++) names.Add($"fe{j}");

        try
        {
            RegressionResult fit = LinearRegression.FitDesign(
                design, response, names,
                new RegressionOptions { AddIntercept = false, Ridge = 1e-8 }, "TWFE");

            return fit.Coefficients[0].Estimate;
        }
        catch (ArgumentException)
        {
            return 0;
        }
    }

    /// <summary>Устойчивая агрегированная оценка по когортам и периодам.</summary>
    private static (double Estimate, double StandardError) RobustEstimate(
        IReadOnlyList<DidObservation> observations, int samples, int seed)
    {
        double point = RobustOnce(observations);
        double[] draws = ClusterBootstrap(observations, samples, seed, RobustOnce);

        return (point, StandardDeviation(draws));
    }

    /// <summary>Одна устойчивая оценка: средневзвешенный эффект по парам «когорта — период».</summary>
    private static double RobustOnce(IReadOnlyList<DidObservation> observations)
    {
        var byUnit = observations.GroupBy(o => o.Unit)
            .ToDictionary(g => g.Key, g => g.ToDictionary(o => o.Period, o => o.Outcome));

        var firstTreated = observations
            .GroupBy(o => o.Unit)
            .ToDictionary(g => g.Key, g => g.First().FirstTreatedPeriod);

        var cohorts = firstTreated.Values.Where(v => v > 0).Distinct().OrderBy(v => v).ToList();
        var periods = observations.Select(o => o.Period).Distinct().OrderBy(p => p).ToList();

        double weightedSum = 0, weights = 0;

        foreach (int cohort in cohorts)
        {
            int baseline = cohort - 1;
            var treated = firstTreated.Where(p => p.Value == cohort).Select(p => p.Key).ToList();

            foreach (int period in periods.Where(p => p >= cohort))
            {
                var controls = firstTreated
                    .Where(p => p.Value <= 0 || p.Value > period)
                    .Select(p => p.Key)
                    .ToList();

                if (controls.Count == 0) continue;

                double treatedChange = AverageChange(byUnit, treated, baseline, period);
                double controlChange = AverageChange(byUnit, controls, baseline, period);

                if (double.IsNaN(treatedChange) || double.IsNaN(controlChange)) continue;

                double weight = treated.Count;
                weightedSum += weight * (treatedChange - controlChange);
                weights += weight;
            }
        }

        return weights > 0 ? weightedSum / weights : 0;
    }

    /// <summary>Динамика эффекта по расстоянию от момента внедрения.</summary>
    private static IReadOnlyList<EventStudyPoint> EventStudyPoints(
        IReadOnlyList<DidObservation> observations, int samples, int seed)
    {
        var relatives = observations
            .Where(o => o.FirstTreatedPeriod > 0)
            .Select(o => o.RelativePeriod)
            .Where(r => r is >= -5 and <= 5 and not (-1))
            .Distinct()
            .OrderBy(r => r)
            .ToList();

        var points = new List<EventStudyPoint>(relatives.Count);

        foreach (int relative in relatives)
        {
            double point = EventEffect(observations, relative);
            double[] draws = ClusterBootstrap(observations, Math.Min(samples, 120), seed + relative,
                sample => EventEffect(sample, relative));

            int count = observations.Count(o => o.FirstTreatedPeriod > 0 && o.RelativePeriod == relative);
            points.Add(new EventStudyPoint(relative, point, StandardDeviation(draws), count));
        }

        return points;
    }

    /// <summary>Эффект на заданном расстоянии от внедрения относительно периода перед ним.</summary>
    private static double EventEffect(IReadOnlyList<DidObservation> observations, int relative)
    {
        var byUnit = observations.GroupBy(o => o.Unit)
            .ToDictionary(g => g.Key, g => g.ToDictionary(o => o.Period, o => o.Outcome));

        var firstTreated = observations
            .GroupBy(o => o.Unit)
            .ToDictionary(g => g.Key, g => g.First().FirstTreatedPeriod);

        double weightedSum = 0, weights = 0;

        foreach (int cohort in firstTreated.Values.Where(v => v > 0).Distinct())
        {
            int period = cohort + relative;
            int baseline = cohort - 1;

            var treated = firstTreated.Where(p => p.Value == cohort).Select(p => p.Key).ToList();
            var controls = firstTreated
                .Where(p => p.Value <= 0 || p.Value > Math.Max(period, cohort - 1))
                .Select(p => p.Key)
                .ToList();

            if (controls.Count == 0) continue;

            double treatedChange = AverageChange(byUnit, treated, baseline, period);
            double controlChange = AverageChange(byUnit, controls, baseline, period);

            if (double.IsNaN(treatedChange) || double.IsNaN(controlChange)) continue;

            weightedSum += treated.Count * (treatedChange - controlChange);
            weights += treated.Count;
        }

        return weights > 0 ? weightedSum / weights : 0;
    }

    /// <summary>Среднее изменение отклика между базовым и текущим периодом по группе объектов.</summary>
    private static double AverageChange(
        Dictionary<int, Dictionary<int, double>> byUnit,
        IReadOnlyList<int> units, int baseline, int period)
    {
        double sum = 0;
        int count = 0;

        foreach (int unit in units)
        {
            if (!byUnit.TryGetValue(unit, out Dictionary<int, double>? series)) continue;
            if (!series.TryGetValue(baseline, out double before)) continue;
            if (!series.TryGetValue(period, out double after)) continue;

            sum += after - before;
            count++;
        }

        return count > 0 ? sum / count : double.NaN;
    }

    /// <summary>Кластерный бутстрап по объектам.</summary>
    private static double[] ClusterBootstrap(
        IReadOnlyList<DidObservation> observations, int samples, int seed,
        Func<IReadOnlyList<DidObservation>, double> estimator)
    {
        if (samples <= 1) return [];

        Random rng = RandomEngine.Create(seed);
        var byUnit = observations.GroupBy(o => o.Unit).ToDictionary(g => g.Key, g => g.ToList());
        var units = byUnit.Keys.ToList();
        var draws = new List<double>(samples);

        for (int b = 0; b < samples; b++)
        {
            var resample = new List<DidObservation>(observations.Count);

            for (int i = 0; i < units.Count; i++)
            {
                int pick = units[rng.Next(units.Count)];

                // Каждой копии объекта нужен собственный идентификатор,
                // иначе повторные вытягивания сольются в один объект
                foreach (DidObservation o in byUnit[pick])
                    resample.Add(o with { Unit = (i * 1_000_003) + 1 });
            }

            double value = estimator(resample);
            if (double.IsFinite(value)) draws.Add(value);
        }

        return [.. draws];
    }

    /// <summary>Выборочное стандартное отклонение.</summary>
    private static double StandardDeviation(double[] values)
    {
        if (values.Length < 2) return 0;

        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1);

        return Math.Sqrt(Math.Max(variance, 0));
    }
}

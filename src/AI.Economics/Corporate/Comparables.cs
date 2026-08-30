using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Econometrics;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Corporate;

/// <summary>Компания-аналог для сравнительной оценки.</summary>
public sealed record Peer
{
    /// <summary>Название компании.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Стоимость бизнеса.</summary>
    public double EnterpriseValue { get; init; }

    /// <summary>Выручка.</summary>
    public double Revenue { get; init; }

    /// <summary>Прибыль до процентов, налогов и амортизации.</summary>
    public double Ebitda { get; init; }

    /// <summary>Чистая прибыль.</summary>
    public double NetIncome { get; init; }

    /// <summary>Рыночная капитализация.</summary>
    public double MarketCapitalization { get; init; }

    /// <summary>Темп роста выручки.</summary>
    public double Growth { get; init; }

    /// <summary>Рентабельность по прибыли до амортизации.</summary>
    public double Margin => Revenue > 0 ? Ebitda / Revenue : 0;

    /// <summary>Мультипликатор стоимости бизнеса к прибыли до амортизации.</summary>
    public double EvToEbitda => Ebitda > 0 ? EnterpriseValue / Ebitda : double.NaN;

    /// <summary>Мультипликатор стоимости бизнеса к выручке.</summary>
    public double EvToRevenue => Revenue > 0 ? EnterpriseValue / Revenue : double.NaN;

    /// <summary>Мультипликатор капитализации к чистой прибыли.</summary>
    public double PriceToEarnings =>
        NetIncome > 0 ? MarketCapitalization / NetIncome : double.NaN;
}

/// <summary>Статистика одного мультипликатора по группе аналогов.</summary>
/// <param name="Name">Название мультипликатора.</param>
/// <param name="Median">Медиана по группе.</param>
/// <param name="LowerQuartile">Нижний квартиль.</param>
/// <param name="UpperQuartile">Верхний квартиль.</param>
/// <param name="Observations">Число аналогов с определённым значением.</param>
/// <param name="ImpliedValue">Стоимость оцениваемой компании по медиане.</param>
public sealed record MultipleStatistic(
    string Name, double Median, double LowerQuartile, double UpperQuartile,
    int Observations, double ImpliedValue);

/// <summary>Результат сравнительной оценки.</summary>
public sealed record ComparablesResult : IInterpretable
{
    /// <summary>Название оцениваемой компании.</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>Статистика мультипликаторов.</summary>
    public IReadOnlyList<MultipleStatistic> Multiples { get; init; } = [];

    /// <summary>Отобранные аналоги в порядке близости к оцениваемой компании.</summary>
    public IReadOnlyList<(string Peer, double Distance)> SelectedPeers { get; init; } = [];

    /// <summary>Оценка по регрессии мультипликатора на драйверы.</summary>
    public double RegressionValue { get; init; }

    /// <summary>Регрессия мультипликатора на драйверы.</summary>
    public RegressionResult? Regression { get; init; }

    /// <summary>Медианная оценка по мультипликаторам.</summary>
    public double MedianValue =>
        Multiples.Count > 0
            ? Multiples.Where(m => double.IsFinite(m.ImpliedValue)).Select(m => m.ImpliedValue).DefaultIfEmpty(0).Average()
            : 0;

    /// <summary>Разброс оценок между мультипликаторами.</summary>
    public double ValuationSpread
    {
        get
        {
            var values = Multiples.Where(m => double.IsFinite(m.ImpliedValue)).Select(m => m.ImpliedValue).ToList();
            return values.Count > 1 ? values.Max() - values.Min() : 0;
        }
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        MultipleStatistic? widest = Multiples
            .Where(m => double.IsFinite(m.UpperQuartile) && double.IsFinite(m.LowerQuartile))
            .OrderByDescending(m => m.Median > 0 ? (m.UpperQuartile - m.LowerQuartile) / m.Median : 0)
            .FirstOrDefault();

        double dispersion = MedianValue > 0 ? ValuationSpread / MedianValue : 0;
        bool regressionUseful = Regression is not null && Regression.RSquared > 0.4;

        var builder = new InterpretationBuilder($"Сравнительная оценка: {Target}")
            .Summary($"Отобрано {SelectedPeers.Count} аналогов, рассчитано {Multiples.Count} " +
                     $"мультипликаторов. Средняя оценка {Fmt.Money(MedianValue)}, разброс между " +
                     $"мультипликаторами {Fmt.Money(ValuationSpread)} " +
                     $"({Fmt.Pct(dispersion, 0)} от оценки)." +
                     (Regression is not null
                         ? $" Регрессия мультипликатора на драйверы даёт {Fmt.Money(RegressionValue)} " +
                           $"при R² = {Fmt.Num(Regression.RSquared, 2)}."
                         : ""))
            .Metric("Оценка по мультипликаторам", Fmt.Money(MedianValue), null,
                "среднее по медианам мультипликаторов")
            .Metric("Разброс оценок", Fmt.Money(ValuationSpread), null,
                $"{Fmt.Pct(dispersion, 0)} от средней оценки",
                dispersion > 0.5 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Аналогов", SelectedPeers.Count, null,
                "чем однороднее группа, тем уже разброс",
                SelectedPeers.Count >= 5 ? MetricQuality.Good : MetricQuality.Warning, 0);

        if (Regression is not null)
        {
            builder.Metric("Оценка по регрессии", Fmt.Money(RegressionValue), null,
                $"R² = {Fmt.Num(Regression.RSquared, 2)}; поправка на рост и рентабельность",
                regressionUseful ? MetricQuality.Good : MetricQuality.Warning);
        }

        foreach (MultipleStatistic multiple in Multiples)
        {
            builder.Metric(multiple.Name, multiple.Median, "×",
                $"квартили {Fmt.Num(multiple.LowerQuartile, 1)}-{Fmt.Num(multiple.UpperQuartile, 1)}, " +
                $"аналогов {multiple.Observations}, оценка {Fmt.Money(multiple.ImpliedValue)}",
                MetricQuality.Unknown, 2);
        }

        foreach ((string peer, double distance) in SelectedPeers.Take(8))
            builder.Metric($"Аналог: {peer}", distance, null, "расстояние по драйверам стоимости",
                MetricQuality.Unknown, 3);

        return builder
            .Finding("Сравнительный подход отвечает на другой вопрос, чем модель денежных " +
                     "потоков: не «сколько бизнес стоит», а «сколько за такой бизнес платят " +
                     "сейчас». Расхождение между ними — это разница между фундаментальной " +
                     "стоимостью и настроением рынка.")
            .FindingIf(regressionUseful,
                $"Регрессия мультипликатора на рост и рентабельность объясняет " +
                $"{Fmt.Pct(Regression?.RSquared ?? 0, 0)} его разброса. Она корректнее " +
                "медианы: компания с более высоким ростом заслуживает более высокого " +
                "мультипликатора, и регрессия это учитывает.")
            .FindingIf(widest is not null,
                $"Шире всего разброс у мультипликатора «{widest?.Name}». Такой мультипликатор " +
                "хуже других подходит для этой группы: аналоги слишком разнородны " +
                "по стоящему за ним показателю.")
            .WarningIf(SelectedPeers.Count < 5,
                $"В группе всего {SelectedPeers.Count} аналогов. Медиана по такой выборке " +
                "определяется одной-двумя компаниями, и её устойчивость мала.")
            .WarningIf(dispersion > 0.5,
                $"Оценки по разным мультипликаторам расходятся на {Fmt.Pct(dispersion, 0)}. " +
                "Это означает, что аналоги отличаются от компании по структуре — " +
                "по капиталоёмкости, налогам или доле амортизации.")
            .Warning("Мультипликаторы отражают текущее состояние рынка вместе со всеми " +
                     "его перекосами. В периоды переоценки сравнительный подход " +
                     "воспроизводит завышенные цены и выдаёт их за оценку.")
            .Recommendation("Отбирайте аналоги по драйверам стоимости — росту, рентабельности, " +
                            "капиталоёмкости, — а не по коду отрасли. Формальная отраслевая " +
                            "принадлежность даёт худшие группы.")
            .Recommendation("Сверяйте результат с моделью дисконтированных потоков. Совпадение " +
                            "двух независимых подходов — самый сильный аргумент в переговорах.")
            .Build();
    }
}

/// <summary>
/// Сравнительная оценка по мультипликаторам компаний-аналогов.
/// </summary>
/// <remarks>
/// <para>
/// Подход опирается на закон одной цены: похожие бизнесы должны стоить похоже
/// относительно своих показателей. Практическая сложность — в определении
/// «похожести». Формальная отраслевая принадлежность даёт плохие группы:
/// внутри одной отрасли компании различаются ростом, рентабельностью и
/// капиталоёмкостью в разы.
/// </para>
/// <para>
/// Здесь аналоги отбираются по расстоянию в пространстве драйверов стоимости —
/// темпа роста, рентабельности и масштаба, — приведённых к сопоставимым шкалам.
/// </para>
/// <para>
/// Регрессия мультипликатора на драйверы решает ту же задачу иначе: вместо
/// отбора похожих компаний она строит зависимость мультипликатора от
/// показателей и подставляет в неё показатели оцениваемой компании:
/// </para>
/// <code>
/// EV/EBITDA_i = a + b * growth_i + c * margin_i + e_i
/// </code>
/// <para>
/// Этот способ использует всю выборку и явно показывает, за что рынок платит
/// премию. Он же и честнее: коэффициент при росте можно обсуждать, а выбор
/// «похожих» компаний — почти нет.
/// </para>
/// </remarks>
public static class Comparables
{
    /// <summary>Оценивает компанию по группе аналогов.</summary>
    /// <param name="target">Оцениваемая компания.</param>
    /// <param name="peers">Пул потенциальных аналогов.</param>
    /// <param name="peerCount">Сколько ближайших аналогов взять; при нуле берутся все.</param>
    /// <returns>Мультипликаторы, отобранные аналоги и оценка по регрессии.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Пул аналогов пуст.</exception>
    public static ComparablesResult Value(Peer target, IReadOnlyList<Peer> peers, int peerCount = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(peers);

        if (peers.Count == 0) throw new ArgumentException("Пул аналогов пуст.", nameof(peers));

        IReadOnlyList<(string Peer, double Distance)> ranked = Rank(target, peers);
        int take = peerCount > 0 ? Math.Min(peerCount, ranked.Count) : ranked.Count;

        var selected = ranked.Take(take).ToList();
        var group = selected
            .Select(s => peers.First(p => p.Name == s.Peer))
            .ToList();

        var multiples = new List<MultipleStatistic>
        {
            Statistic("EV/EBITDA", group.Select(p => p.EvToEbitda), target.Ebitda),
            Statistic("EV/Выручка", group.Select(p => p.EvToRevenue), target.Revenue),
            Statistic("P/E", group.Select(p => p.PriceToEarnings), target.NetIncome),
        };

        (double regressionValue, RegressionResult? regression) = MultipleRegression(target, group);

        return new ComparablesResult
        {
            Target = target.Name,
            Multiples = multiples,
            SelectedPeers = selected,
            RegressionValue = regressionValue,
            Regression = regression,
        };
    }

    /// <summary>Упорядочивает аналоги по близости к оцениваемой компании.</summary>
    /// <param name="target">Оцениваемая компания.</param>
    /// <param name="peers">Пул аналогов.</param>
    /// <returns>Пары «аналог — расстояние» по возрастанию расстояния.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static IReadOnlyList<(string Peer, double Distance)> Rank(Peer target, IReadOnlyList<Peer> peers)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(peers);

        double growthScale = Spread(peers.Select(p => p.Growth));
        double marginScale = Spread(peers.Select(p => p.Margin));
        double sizeScale = Spread(peers.Select(p => Math.Log(Math.Max(p.Revenue, 1))));

        return
        [
            .. peers
                .Select(p => (p.Name, Distance: Math.Sqrt(
                    Math.Pow((p.Growth - target.Growth) / growthScale, 2)
                    + Math.Pow((p.Margin - target.Margin) / marginScale, 2)
                    + Math.Pow((Math.Log(Math.Max(p.Revenue, 1)) - Math.Log(Math.Max(target.Revenue, 1))) / sizeScale, 2))))
                .OrderBy(p => p.Distance),
        ];
    }

    /// <summary>Статистика мультипликатора по группе и подразумеваемая стоимость.</summary>
    private static MultipleStatistic Statistic(string name, IEnumerable<double> values, double basis)
    {
        double[] clean = [.. values.Where(double.IsFinite).Where(v => v > 0).OrderBy(v => v)];

        if (clean.Length == 0)
            return new MultipleStatistic(name, double.NaN, double.NaN, double.NaN, 0, double.NaN);

        double median = EconMath.Quantile(clean, 0.5);

        return new MultipleStatistic(
            name, median,
            EconMath.Quantile(clean, 0.25),
            EconMath.Quantile(clean, 0.75),
            clean.Length,
            basis > 0 ? median * basis : double.NaN);
    }

    /// <summary>Регрессия мультипликатора к прибыли на рост и рентабельность.</summary>
    private static (double Value, RegressionResult? Regression) MultipleRegression(
        Peer target, IReadOnlyList<Peer> peers)
    {
        var usable = peers.Where(p => double.IsFinite(p.EvToEbitda) && p.EvToEbitda > 0).ToList();
        if (usable.Count < 6) return (double.NaN, null);

        var x = new Matrix(usable.Count, 2);
        var y = new Vector(usable.Count);

        for (int i = 0; i < usable.Count; i++)
        {
            x[i, 0] = usable[i].Growth;
            x[i, 1] = usable[i].Margin;
            y[i] = usable[i].EvToEbitda;
        }

        RegressionResult regression = LinearRegression.Fit(x, y, ["рост", "рентабельность"]);

        double predicted = regression.Coefficients[0].Estimate
            + (regression.Coefficients[1].Estimate * target.Growth)
            + (regression.Coefficients[2].Estimate * target.Margin);

        return (Math.Max(predicted, 0) * target.Ebitda, regression);
    }

    /// <summary>Масштаб разброса величины с защитой от нулевого размаха.</summary>
    private static double Spread(IEnumerable<double> values)
    {
        double[] array = [.. values];
        if (array.Length < 2) return 1;

        double mean = array.Average();
        double variance = array.Sum(v => (v - mean) * (v - mean)) / (array.Length - 1);

        return Math.Max(Math.Sqrt(variance), 1e-6);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Statements;

/// <summary>Зона финансовой устойчивости по модели банкротства.</summary>
public enum DistressZone
{
    /// <summary>Устойчивое положение.</summary>
    Safe,

    /// <summary>Серая зона: модель не даёт однозначного ответа.</summary>
    Grey,

    /// <summary>Зона риска банкротства.</summary>
    Distress,
}

/// <summary>Слагаемое модели банкротства.</summary>
/// <param name="Name">Название показателя.</param>
/// <param name="Value">Значение показателя.</param>
/// <param name="Weight">Вес в модели.</param>
/// <param name="Contribution">Вклад в итоговый балл.</param>
public sealed record ScoreComponent(string Name, double Value, double Weight, double Contribution);

/// <summary>Оценка по одной модели банкротства.</summary>
public sealed record DistressScore
{
    /// <summary>Название модели.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Значение балла.</summary>
    public double Value { get; init; }

    /// <summary>Зона устойчивости.</summary>
    public DistressZone Zone { get; init; }

    /// <summary>Границы зон: нижняя отделяет риск, верхняя — устойчивость.</summary>
    public (double Distress, double Safe) Thresholds { get; init; }

    /// <summary>Вероятность банкротства, если модель её даёт.</summary>
    public double? ProbabilityOfDefault { get; init; }

    /// <summary>Слагаемые модели.</summary>
    public IReadOnlyList<ScoreComponent> Components { get; init; } = [];

    /// <summary>Комментарий к результату.</summary>
    public string Comment { get; init; } = string.Empty;
}

/// <summary>Свод оценок по всем моделям банкротства.</summary>
public sealed record DistressReport : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Оценки по моделям.</summary>
    public IReadOnlyList<DistressScore> Scores { get; init; } = [];

    /// <summary>Балл Пиотроски от нуля до девяти.</summary>
    public int PiotroskiScore { get; init; }

    /// <summary>Выполненные критерии Пиотроски.</summary>
    public IReadOnlyList<(string Criterion, bool Passed, string Comment)> PiotroskiCriteria { get; init; } = [];

    /// <summary>Число моделей, указывающих на риск банкротства.</summary>
    public int DistressVotes => Scores.Count(s => s.Zone == DistressZone.Distress);

    /// <summary>Число моделей, указывающих на устойчивость.</summary>
    public int SafeVotes => Scores.Count(s => s.Zone == DistressZone.Safe);

    /// <summary>Согласованный вывод по большинству моделей.</summary>
    public DistressZone Consensus =>
        DistressVotes > Scores.Count / 2 ? DistressZone.Distress
        : SafeVotes > Scores.Count / 2 ? DistressZone.Safe
        : DistressZone.Grey;

    /// <summary>Средняя вероятность банкротства по моделям, которые её дают.</summary>
    public double AverageProbability
    {
        get
        {
            var withProbability = Scores.Where(s => s.ProbabilityOfDefault.HasValue).ToList();
            return withProbability.Count > 0 ? withProbability.Average(s => s.ProbabilityOfDefault!.Value) : 0;
        }
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string verdict = Consensus switch
        {
            DistressZone.Safe => "модели согласованно указывают на устойчивое положение",
            DistressZone.Distress => "большинство моделей относит компанию к зоне риска банкротства",
            _ => "модели расходятся, компания в серой зоне",
        };

        DistressScore? worst = Scores
            .Where(s => s.Thresholds.Safe > s.Thresholds.Distress)
            .OrderBy(s => (s.Value - s.Thresholds.Distress) /
                          Math.Max(1e-9, s.Thresholds.Safe - s.Thresholds.Distress))
            .FirstOrDefault();

        var builder = new InterpretationBuilder($"Модели банкротства: {Company}, {Period}")
            .Summary($"Рассчитано {Scores.Count} моделей плюс балл Пиотроски. " +
                     $"Риск банкротства видят {DistressVotes} модели, устойчивость — {SafeVotes}: " +
                     $"{verdict}. Средняя вероятность банкротства по моделям, которые её дают, " +
                     $"{Fmt.Pct(AverageProbability, 1)}. Балл Пиотроски {PiotroskiScore} из 9.")
            .Metric("Голосов за риск", DistressVotes, null, $"из {Scores.Count} моделей",
                DistressVotes == 0 ? MetricQuality.Good
                    : DistressVotes < Scores.Count / 2.0 ? MetricQuality.Warning : MetricQuality.Critical, 0)
            .Metric("Балл Пиотроски", PiotroskiScore, "из 9",
                PiotroskiScore >= 7 ? "фундаментально сильная компания"
                    : PiotroskiScore >= 4 ? "среднее качество фундаментальных показателей"
                    : "фундаментально слабая компания",
                PiotroskiScore >= 7 ? MetricQuality.Good
                    : PiotroskiScore >= 4 ? MetricQuality.Neutral : MetricQuality.Critical, 0)
            .Metric("Средняя вероятность банкротства", AverageProbability, null,
                "по моделям с вероятностной шкалой",
                AverageProbability > 0.3 ? MetricQuality.Critical
                    : AverageProbability > 0.1 ? MetricQuality.Warning : MetricQuality.Good, 4);

        foreach (DistressScore score in Scores)
        {
            builder.Metric(score.Model, score.Value, null,
                $"{score.Comment}; границы {Fmt.Num(score.Thresholds.Distress, 2)} и {Fmt.Num(score.Thresholds.Safe, 2)}",
                score.Zone switch
                {
                    DistressZone.Safe => MetricQuality.Good,
                    DistressZone.Distress => MetricQuality.Critical,
                    _ => MetricQuality.Warning,
                }, 3);
        }

        foreach ((string criterion, bool passed, string comment) in PiotroskiCriteria)
        {
            builder.Metric($"Пиотроски: {criterion}", passed ? "да" : "нет", null, comment,
                passed ? MetricQuality.Good : MetricQuality.Warning);
        }

        return builder
            .FindingIf(worst is not null,
                $"Дальше всех от безопасной зоны модель «{worst?.Model}» со значением " +
                $"{Fmt.Num(worst?.Value ?? 0, 2)} при пороге риска {Fmt.Num(worst?.Thresholds.Distress ?? 0, 2)}.")
            .Finding("Модели построены на разных выборках и разных эпохах, поэтому расходятся " +
                     "закономерно. Ценность даёт не отдельный балл, а согласие или расхождение " +
                     "нескольких моделей и направление изменения балла во времени.")
            .FindingIf(PiotroskiScore >= 7,
                $"Балл Пиотроски {PiotroskiScore} — фундаментальные показатели улучшаются " +
                "одновременно по прибыльности, долгу и эффективности. Такое совпадение " +
                "редко бывает случайным.")
            .FindingIf(Consensus == DistressZone.Grey,
                "Компания в серой зоне: модели не дают однозначного ответа. Для решения " +
                "нужны данные, которых нет в отчётности — график погашения долга, " +
                "доступ к рефинансированию, поведение поставщиков.")
            .WarningIf(DistressVotes > 0,
                $"{DistressVotes} модели относят компанию к зоне риска. Отнеситесь к этому " +
                "как к поводу для углублённой проверки, а не как к прогнозу банкротства: " +
                "доля ложных срабатываний у всех этих моделей заметно выше доли пропусков.")
            .Warning("Коэффициенты моделей оценивались на исторических выборках США и " +
                     "Великобритании 1960-1980-х годов и на других стандартах учёта. " +
                     "Абсолютные значения баллов для российской отчётности смещены; " +
                     "надёжнее сравнивать компании между собой и следить за динамикой.")
            .Recommendation("Считайте баллы за несколько периодов подряд: устойчивое снижение " +
                            "балла информативнее его уровня на одну дату.")
            .Recommendation("Дополните модели проверкой качества прибыли и законом Бенфорда " +
                            "по транзакциям: модели банкротства не рассчитаны на выявление " +
                            "манипуляций с отчётностью и на искажённых данных дают ложное спокойствие.")
            .Build();
    }
}

/// <summary>
/// Классические модели прогнозирования банкротства и балл фундаментального
/// качества Пиотроски.
/// </summary>
/// <remarks>
/// <para>
/// Реализованы пять балльных моделей: Альтмана Z для публичных производственных
/// компаний, Альтмана Z'' для непроизводственных и развивающихся рынков,
/// O-score Ольсона с явной вероятностной шкалой, Спрингейта и Таффлера.
/// Отдельно считается F-score Пиотроски — девять бинарных критериев улучшения
/// фундаментальных показателей.
/// </para>
/// <para>
/// Модели различаются выборками и эпохами оценки, поэтому расходятся между
/// собой. Практическая ценность в согласии нескольких моделей и в динамике
/// баллов, а не в точном значении одного из них. Все модели построены на
/// достоверной отчётности, и ни одна не устойчива к её искажению — для этого
/// служат M-score Бениша и закон Бенфорда.
/// </para>
/// </remarks>
public static class DistressScores
{
    /// <summary>Рассчитывает все модели по отчётности.</summary>
    /// <param name="current">Отчётность анализируемого периода.</param>
    /// <param name="previous">Отчётность предыдущего периода; нужна для балла Пиотроски.</param>
    /// <param name="assetDeflator">Масштаб активов в модели Ольсона: активы делятся на эту величину.</param>
    /// <returns>Свод оценок по всем моделям.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Активы неположительны.</exception>
    public static DistressReport Evaluate(
        FinancialStatement current, FinancialStatement? previous = null, double assetDeflator = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.TotalAssets <= 0)
            throw new ArgumentException("Активы должны быть положительными.", nameof(current));

        var scores = new List<DistressScore>
        {
            Altman(current),
            AltmanDoublePrime(current),
            Ohlson(current, previous, assetDeflator),
            Springate(current),
            Taffler(current),
        };

        (int piotroski, var criteria) = Piotroski(current, previous);

        return new DistressReport
        {
            Company = current.Company,
            Period = current.Period,
            Scores = scores,
            PiotroskiScore = piotroski,
            PiotroskiCriteria = criteria,
        };
    }

    /// <summary>Z-счёт Альтмана для публичных производственных компаний.</summary>
    /// <param name="s">Отчётность.</param>
    /// <returns>Балл, зона и слагаемые модели.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    public static DistressScore Altman(FinancialStatement s)
    {
        ArgumentNullException.ThrowIfNull(s);

        double x1 = Div(s.WorkingCapital, s.TotalAssets);
        double x2 = Div(s.RetainedEarnings, s.TotalAssets);
        double x3 = Div(s.OperatingIncome, s.TotalAssets);
        double x4 = Div(s.MarketCapitalization, s.TotalLiabilities);
        double x5 = Div(s.Revenue, s.TotalAssets);

        var components = new List<ScoreComponent>
        {
            new("Рабочий капитал к активам", x1, 1.2, 1.2 * x1),
            new("Нераспределённая прибыль к активам", x2, 1.4, 1.4 * x2),
            new("Операционная прибыль к активам", x3, 3.3, 3.3 * x3),
            new("Капитализация к обязательствам", x4, 0.6, 0.6 * x4),
            new("Оборачиваемость активов", x5, 0.999, 0.999 * x5),
        };

        double z = components.Sum(c => c.Contribution);

        return new DistressScore
        {
            Model = "Альтман Z",
            Value = z,
            Zone = Zone(z, 1.81, 2.99),
            Thresholds = (1.81, 2.99),
            Components = components,
            Comment = s.MarketCapitalization > 0
                ? "классическая модель для публичных производственных компаний"
                : "капитализация не задана, вклад рыночной оценки равен нулю",
        };
    }

    /// <summary>Модифицированный Z''-счёт Альтмана для непроизводственных компаний.</summary>
    /// <param name="s">Отчётность.</param>
    /// <returns>Балл, зона и слагаемые модели.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    public static DistressScore AltmanDoublePrime(FinancialStatement s)
    {
        ArgumentNullException.ThrowIfNull(s);

        double x1 = Div(s.WorkingCapital, s.TotalAssets);
        double x2 = Div(s.RetainedEarnings, s.TotalAssets);
        double x3 = Div(s.OperatingIncome, s.TotalAssets);
        double x4 = Div(s.Equity, s.TotalLiabilities);

        var components = new List<ScoreComponent>
        {
            new("Рабочий капитал к активам", x1, 6.56, 6.56 * x1),
            new("Нераспределённая прибыль к активам", x2, 3.26, 3.26 * x2),
            new("Операционная прибыль к активам", x3, 6.72, 6.72 * x3),
            new("Капитал к обязательствам", x4, 1.05, 1.05 * x4),
        };

        double z = components.Sum(c => c.Contribution);

        return new DistressScore
        {
            Model = "Альтман Z''",
            Value = z,
            Zone = Zone(z, 1.1, 2.6),
            Thresholds = (1.1, 2.6),
            Components = components,
            Comment = "версия без оборачиваемости и рыночной оценки: применима к непубличным " +
                      "и непроизводственным компаниям",
        };
    }

    /// <summary>O-счёт Ольсона с вероятностью банкротства.</summary>
    /// <param name="current">Отчётность анализируемого периода.</param>
    /// <param name="previous">Отчётность предыдущего периода для оценки динамики прибыли.</param>
    /// <param name="assetDeflator">Масштаб активов: активы делятся на эту величину перед логарифмированием.</param>
    /// <returns>Балл, вероятность банкротства и слагаемые модели.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    public static DistressScore Ohlson(
        FinancialStatement current, FinancialStatement? previous = null, double assetDeflator = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(current);

        double size = Math.Log(Math.Max(current.TotalAssets / Math.Max(assetDeflator, 1e-9), 1e-9));
        double tlta = Div(current.TotalLiabilities, current.TotalAssets);
        double wcta = Div(current.WorkingCapital, current.TotalAssets);
        double clca = Div(current.CurrentLiabilities, current.CurrentAssets);
        double oeneg = current.TotalLiabilities > current.TotalAssets ? 1 : 0;
        double nita = Div(current.NetIncome, current.TotalAssets);
        double futl = Div(current.OperatingCashFlow, current.TotalLiabilities);
        double intwo = current.NetIncome < 0 && (previous?.NetIncome ?? 0) < 0 ? 1 : 0;

        double previousIncome = previous?.NetIncome ?? current.NetIncome;
        double chin = Math.Abs(current.NetIncome) + Math.Abs(previousIncome) > 1e-9
            ? (current.NetIncome - previousIncome) / (Math.Abs(current.NetIncome) + Math.Abs(previousIncome))
            : 0;

        var components = new List<ScoreComponent>
        {
            new("Свободный член", 1, -1.32, -1.32),
            new("Масштаб активов", size, -0.407, -0.407 * size),
            new("Обязательства к активам", tlta, 6.03, 6.03 * tlta),
            new("Рабочий капитал к активам", wcta, -1.43, -1.43 * wcta),
            new("Краткосрочные обязательства к оборотным активам", clca, 0.0757, 0.0757 * clca),
            new("Отрицательный капитал", oeneg, -1.72, -1.72 * oeneg),
            new("Чистая прибыль к активам", nita, -2.37, -2.37 * nita),
            new("Денежный поток к обязательствам", futl, -1.83, -1.83 * futl),
            new("Убыток два года подряд", intwo, 0.285, 0.285 * intwo),
            new("Динамика прибыли", chin, -0.521, -0.521 * chin),
        };

        double o = components.Sum(c => c.Contribution);
        double probability = 1.0 / (1.0 + Math.Exp(-o));

        return new DistressScore
        {
            Model = "Ольсон O-score",
            Value = o,
            Zone = probability > 0.5 ? DistressZone.Distress
                : probability > 0.25 ? DistressZone.Grey : DistressZone.Safe,
            Thresholds = (0.38, -0.5),
            ProbabilityOfDefault = probability,
            Components = components,
            Comment = $"вероятность банкротства {Fmt.Pct(probability, 1)}; " +
                      "единственная модель здесь с явной вероятностной шкалой",
        };
    }

    /// <summary>Модель Спрингейта.</summary>
    /// <param name="s">Отчётность.</param>
    /// <returns>Балл, зона и слагаемые модели.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    public static DistressScore Springate(FinancialStatement s)
    {
        ArgumentNullException.ThrowIfNull(s);

        double a = Div(s.WorkingCapital, s.TotalAssets);
        double b = Div(s.OperatingIncome, s.TotalAssets);
        double c = Div(s.PretaxIncome, s.CurrentLiabilities);
        double d = Div(s.Revenue, s.TotalAssets);

        var components = new List<ScoreComponent>
        {
            new("Рабочий капитал к активам", a, 1.03, 1.03 * a),
            new("Операционная прибыль к активам", b, 3.07, 3.07 * b),
            new("Прибыль до налога к краткосрочным обязательствам", c, 0.66, 0.66 * c),
            new("Оборачиваемость активов", d, 0.4, 0.4 * d),
        };

        double score = components.Sum(x => x.Contribution);

        return new DistressScore
        {
            Model = "Спрингейт",
            Value = score,
            Zone = Zone(score, 0.862, 0.862),
            Thresholds = (0.862, 0.862),
            Components = components,
            Comment = "модель с единственным порогом: ниже 0,862 компания относится к банкротам",
        };
    }

    /// <summary>Модель Таффлера.</summary>
    /// <param name="s">Отчётность.</param>
    /// <returns>Балл, зона и слагаемые модели.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    public static DistressScore Taffler(FinancialStatement s)
    {
        ArgumentNullException.ThrowIfNull(s);

        double dailyExpenses = Math.Max((s.Revenue - s.PretaxIncome - s.Depreciation) / 365, 1e-9);
        double quickAssets = s.CurrentAssets - s.Inventory;

        double a = Div(s.PretaxIncome, s.CurrentLiabilities);
        double b = Div(s.CurrentAssets, s.TotalLiabilities);
        double c = Div(s.CurrentLiabilities, s.TotalAssets);

        // Бескредитный интервал измеряется в днях; здесь он приводится к долям
        // года, чтобы слагаемое было соизмеримо с остальными и пороги модели
        // сохраняли смысл.
        double d = (quickAssets - s.CurrentLiabilities) / dailyExpenses / 365;

        var components = new List<ScoreComponent>
        {
            new("Прибыль до налога к краткосрочным обязательствам", a, 0.53, 0.53 * a),
            new("Оборотные активы к обязательствам", b, 0.13, 0.13 * b),
            new("Краткосрочные обязательства к активам", c, 0.18, 0.18 * c),
            new("Бескредитный интервал", d, 0.16, 0.16 * d),
        };

        double score = components.Sum(x => x.Contribution);

        return new DistressScore
        {
            Model = "Таффлер",
            Value = score,
            Zone = Zone(score, 0.2, 0.3),
            Thresholds = (0.2, 0.3),
            Components = components,
            Comment = "британская модель; бескредитный интервал показывает, сколько компания " +
                      "продержится на ликвидных активах без новых поступлений",
        };
    }

    /// <summary>F-счёт Пиотроски: девять критериев фундаментального качества.</summary>
    /// <param name="current">Отчётность анализируемого периода.</param>
    /// <param name="previous">Отчётность предыдущего периода.</param>
    /// <returns>Балл от нуля до девяти и разбор критериев.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    public static (int Score, IReadOnlyList<(string Criterion, bool Passed, string Comment)> Criteria) Piotroski(
        FinancialStatement current, FinancialStatement? previous = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        double roa = Div(current.NetIncome, current.TotalAssets);
        double previousRoa = previous is not null ? Div(previous.NetIncome, previous.TotalAssets) : roa;
        double currentRatio = Div(current.CurrentAssets, current.CurrentLiabilities);
        double previousCurrentRatio = previous is not null
            ? Div(previous.CurrentAssets, previous.CurrentLiabilities)
            : currentRatio;
        double leverage = Div(current.LongTermDebt, current.TotalAssets);
        double previousLeverage = previous is not null ? Div(previous.LongTermDebt, previous.TotalAssets) : leverage;
        double grossMargin = Div(current.GrossProfit, current.Revenue);
        double previousGrossMargin = previous is not null ? Div(previous.GrossProfit, previous.Revenue) : grossMargin;
        double turnover = Div(current.Revenue, current.TotalAssets);
        double previousTurnover = previous is not null ? Div(previous.Revenue, previous.TotalAssets) : turnover;

        // Прокси выпуска акций: рост капитала сверх нераспределённой прибыли периода.
        double equityGrowth = previous is not null ? current.Equity - previous.Equity : 0;
        double retained = current.NetIncome - current.DividendsPaid;
        bool noDilution = previous is null || equityGrowth <= retained + (Math.Abs(retained) * 0.01) + 1e-9;

        var criteria = new List<(string, bool, string)>
        {
            ("Положительная рентабельность активов", roa > 0, $"рентабельность активов {Fmt.Pct(roa, 2)}"),
            ("Положительный операционный поток", current.OperatingCashFlow > 0,
                $"поток {Fmt.Money(current.OperatingCashFlow)}"),
            ("Рост рентабельности активов", roa > previousRoa,
                $"было {Fmt.Pct(previousRoa, 2)}, стало {Fmt.Pct(roa, 2)}"),
            ("Поток выше прибыли", current.OperatingCashFlow > current.NetIncome,
                "прибыль подтверждена деньгами, начисления не раздуты"),
            ("Снижение долгосрочного долга", leverage <= previousLeverage,
                $"долг к активам {Fmt.Pct(previousLeverage, 2)} -> {Fmt.Pct(leverage, 2)}"),
            ("Рост текущей ликвидности", currentRatio > previousCurrentRatio,
                $"ликвидность {Fmt.Num(previousCurrentRatio, 2)} -> {Fmt.Num(currentRatio, 2)}"),
            ("Отсутствие размытия капитала", noDilution,
                "прирост капитала не превышает нераспределённую прибыль"),
            ("Рост валовой рентабельности", grossMargin > previousGrossMargin,
                $"валовая маржа {Fmt.Pct(previousGrossMargin, 2)} -> {Fmt.Pct(grossMargin, 2)}"),
            ("Рост оборачиваемости активов", turnover > previousTurnover,
                $"оборачиваемость {Fmt.Num(previousTurnover, 2)} -> {Fmt.Num(turnover, 2)}"),
        };

        return (criteria.Count(c => c.Item2), [.. criteria]);
    }

    /// <summary>Определяет зону по двум порогам.</summary>
    private static DistressZone Zone(double value, double distress, double safe) =>
        value < distress ? DistressZone.Distress
        : value > safe ? DistressZone.Safe
        : DistressZone.Grey;

    /// <summary>Деление с защитой от нулевого знаменателя.</summary>
    private static double Div(double numerator, double denominator) =>
        Math.Abs(denominator) < 1e-12 ? 0 : numerator / denominator;
}

using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Credit;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категорий «Кредитный риск и скоринг».</summary>
public static partial class EconomicsDemoRunner
{
    #region Скоркарта

    private static string DoScorecard(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 3000);
        double signal = p.GetValueOrDefault("signal", 1.0);
        double badRate = p.GetValueOrDefault("bad_rate", 0.15);
        int maxBins = (int)p.GetValueOrDefault("max_bins", 6);
        double minShare = p.GetValueOrDefault("min_share", 0.05);
        double pdo = p.GetValueOrDefault("pdo", 20);
        double baseScore = p.GetValueOrDefault("base_score", 600);
        int seed = (int)p.GetValueOrDefault("seed", 7);

        (Matrix values, List<bool> defaults) = Applications(n, signal, badRate, seed);

        var scorecard = new Scorecard();
        ScorecardResult result = scorecard.Fit(
            ScoreVariableNames, values, defaults,
            new ScorecardOptions
            {
                MaxBins = maxBins,
                MinBinShare = minShare,
                PointsToDoubleOdds = pdo,
                BaseScore = baseScore,
            });

        VariableBinning strongest = result.Variables.OrderByDescending(v => v.InformationValue).First();

        var centres = new Vector(strongest.Bins.Count);
        var rates = new Vector(strongest.Bins.Count);
        var woes = new Vector(strongest.Bins.Count);

        for (int i = 0; i < strongest.Bins.Count; i++)
        {
            centres[i] = i + 1;
            rates[i] = strongest.Bins[i].BadRate;
            woes[i] = strongest.Bins[i].Woe;
        }

        cv.AddPlot(centres, rates, $"Доля дефолтов: {strongest.Variable}", C(3), 3);
        cv.AddPlot(centres, woes, "Вес доказательства", C(1), 2);
        Segment(cv, 1, 0, strongest.Bins.Count, 0, C(5), "Ноль", 1);
        cv.ChartName = $"Биннинг признака «{strongest.Variable}»: IV = {Num(strongest.InformationValue, 3)}";
        cv.LabelX = "Номер интервала";
        cv.LabelY = "Доля дефолтов / вес доказательства";

        var binTable = rep.Table("Интервалы сильнейшего признака",
            ["Интервал", "Наблюдений", "Дефолтов", "Доля", "WoE", "Вклад в IV"],
            [false, true, true, true, true, true]);

        foreach (ScoreBin bin in strongest.Bins)
        {
            binTable.Row(bin.Label, Int(bin.Total), Int(bin.Bad),
                Pct(bin.BadRate, 1), Num(bin.Woe, 3), Num(bin.IvContribution, 4));
        }

        var pointsTable = rep.Table("Карта баллов",
            ["Признак", "Интервал", "WoE", "Баллы"], [false, false, true, true]);

        foreach (ScorecardPoint point in result.Points)
            pointsTable.Row(point.Variable, point.Bin, Num(point.Woe, 3), Num(point.Points, 1));

        if (result.Rejected.Count > 0)
        {
            var rejected = rep.Table("Отклонённые признаки", ["Признак", "IV", "Причина"], [false, true, false]);
            foreach ((string variable, double iv, string reason) in result.Rejected)
                rejected.Row(variable, Num(iv, 4), reason);
        }

        string log =
            $"Выборка: {Int(n)} заявок, дефолтов {Int(defaults.Count(d => d))} ({Pct((double)defaults.Count(d => d) / n, 1)}).\n" +
            $"Отобрано признаков: {result.Variables.Count}, отклонено {result.Rejected.Count}.\n" +
            $"Джини {Num(result.Quality.Gini, 3)}, KS {Num(result.Quality.Ks, 3)}, Брайер {Num(result.Quality.Brier, 4)}.\n" +
            $"Шкала баллов: {Num(result.ScoreRange.Min, 0)}–{Num(result.ScoreRange.Max, 0)}, " +
            $"удвоение шансов каждые {Num(pdo, 0)} баллов.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Мониторинг модели

    private static string DoScoreMonitoring(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 3000);
        double signal = p.GetValueOrDefault("signal", 1.2);
        double badRate = p.GetValueOrDefault("bad_rate", 0.12);
        double shift = p.GetValueOrDefault("shift", -0.6);
        double spread = p.GetValueOrDefault("spread", 1.0);
        double bias = p.GetValueOrDefault("bias", 1.0);
        int bins = (int)p.GetValueOrDefault("bins", 10);
        int seed = (int)p.GetValueOrDefault("seed", 13);

        Random rng = RandomEngine.Create(seed);
        double intercept = Math.Log(badRate / (1 - badRate));

        var probabilities = new Vector(n);
        var outcomes = new List<bool>(n);
        var reference = new Vector(n);
        var current = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double latent = RandomEngine.NextGaussian(rng);
            double logit = intercept + (signal * latent);
            double probability = 1.0 / (1.0 + Math.Exp(-logit));

            probabilities[i] = Math.Clamp(probability * bias, 1e-6, 1 - 1e-6);
            outcomes.Add(rng.NextDouble() < probability);

            reference[i] = 600 - (40 * latent);
            current[i] = 600 - (40 * ((latent * spread) - shift));
        }

        ScoreQuality quality = ScoreMetrics.Evaluate(probabilities, outcomes);
        PsiResult psi = ScoreMetrics.PopulationStability(reference, current, bins);

        cv.AddPlot(Vec(quality.RocFalsePositive), Vec(quality.RocTruePositive),
            $"ROC: AUC = {Num(quality.Auc, 3)}", C(0), 3);
        Segment(cv, 0, 0, 1, 1, C(5), "Случайная модель", 1);
        cv.ChartName = $"Разделяющая способность: Джини {Num(quality.Gini, 3)}, KS {Num(quality.Ks, 3)}";
        cv.LabelX = "Доля ложных тревог";
        cv.LabelY = "Доля пойманных дефолтов";

        var calibration = rep.Table("Калибровка по децилям",
            ["Прогноз", "Факт"], [true, true]);
        for (int i = 0; i < quality.CalibrationPredicted.Count; i++)
            calibration.Row(Pct(quality.CalibrationPredicted[i], 2), Pct(quality.CalibrationObserved[i], 2));

        var drift = rep.Table("Индекс стабильности популяции",
            ["Интервал", "Было", "Стало", "Вклад"], [false, true, true, true]);

        for (int i = 0; i < psi.ExpectedShares.Count; i++)
        {
            string range = i == 0
                ? $"до {Num(psi.Boundaries[0], 0)}"
                : i == psi.ExpectedShares.Count - 1
                    ? $"от {Num(psi.Boundaries[^1], 0)}"
                    : $"{Num(psi.Boundaries[i - 1], 0)}–{Num(psi.Boundaries[i], 0)}";

            drift.Row(range, Pct(psi.ExpectedShares[i], 1), Pct(psi.ActualShares[i], 1),
                Num(psi.Contributions[i], 4));
        }

        rep.Metric("PSI", Num(psi.Psi, 4), null, psi.Verdict,
            psi.Psi < 0.1 ? MetricTone.Good : psi.Psi < 0.25 ? MetricTone.Warn : MetricTone.Bad);

        string log =
            $"Разделяющая способность: AUC {Num(quality.Auc, 4)}, Джини {Num(quality.Gini, 3)}, " +
            $"KS {Num(quality.Ks, 3)} на балле {Num(quality.KsThreshold, 3)}.\n" +
            $"Калибровка: наклон {Num(quality.CalibrationSlope, 3)}, сдвиг {Num(quality.CalibrationIntercept, 3)}, " +
            $"средний прогноз {Pct(quality.MeanPredicted, 2)} против факта {Pct(quality.MeanObserved, 2)}.\n" +
            $"Дрейф популяции: PSI {Num(psi.Psi, 4)} — {psi.Verdict}.";

        return Narrate(rep, quality, log) + "\n\n" + psi.Interpret().ToLlmText();
    }

    #endregion

    #region Резерв по МСФО 9

    private static string DoIfrs9(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 300);
        double pd = p.GetValueOrDefault("pd", 0.04);
        double lgd = p.GetValueOrDefault("lgd", 0.45);
        double eir = p.GetValueOrDefault("eir", 0.16);
        int months = (int)p.GetValueOrDefault("months", 36);
        double sicrShare = p.GetValueOrDefault("sicr", 0.2);
        double impairedShare = p.GetValueOrDefault("impaired", 0.05);
        double stressPd = p.GetValueOrDefault("stress_pd", 1.8);
        double stressProbability = p.GetValueOrDefault("stress_p", 0.25);
        int seed = (int)p.GetValueOrDefault("seed", 5);

        Random rng = RandomEngine.Create(seed);
        var portfolio = new List<CreditExposure>(n);

        for (int i = 0; i < n; i++)
        {
            double draw = rng.NextDouble();
            bool impaired = draw < impairedShare;
            bool sicr = !impaired && draw < impairedShare + sicrShare;

            double origination = pd * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.3));
            double currentPd = impaired ? Math.Clamp(origination * 15, 0.3, 0.95)
                : sicr ? origination * 3
                : origination;

            portfolio.Add(new CreditExposure
            {
                Id = $"K-{i + 1:0000}",
                Segment = i % 3 == 0 ? "Розница" : i % 3 == 1 ? "МСБ" : "Корпоративный",
                ExposureAtDefault = 1_000_000 * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.6)),
                ProbabilityOfDefault = Math.Clamp(currentPd, 1e-4, 0.99),
                ProbabilityOfDefaultAtOrigination = Math.Clamp(origination, 1e-4, 0.99),
                LossGivenDefault = lgd,
                EffectiveInterestRate = eir,
                RemainingMonths = months,
                DaysPastDue = impaired ? 120 : sicr && draw < impairedShare + (sicrShare / 2) ? 45 : 0,
                IsCreditImpaired = impaired,
            });
        }

        double baseProbability = Math.Max(0.05, 1 - stressProbability - 0.2);
        IReadOnlyList<MacroScenario> scenarios =
        [
            new MacroScenario("Базовый", baseProbability, 1.0),
            new MacroScenario("Оптимистичный", 0.2, 0.7, 0.9),
            new MacroScenario("Стрессовый", stressProbability, stressPd, 1.2),
        ];

        EclResult result = Ifrs9.Compute(portfolio, scenarios);

        ExposureEcl sample = result.Exposures
            .Where(e => e.Stage == CreditStage.Performing)
            .DefaultIfEmpty(result.Exposures[0])
            .First();

        var ages = Axis(sample.MarginalDefaultCurve.Count, 1);
        cv.AddPlot(ages, Vec(sample.MarginalDefaultCurve), "Предельная вероятность дефолта по месяцам", C(1), 3);
        Segment(cv, 12, 0, 12, sample.MarginalDefaultCurve.Count > 0 ? sample.MarginalDefaultCurve.Max() : 1,
            C(3), "Граница 12 месяцев", 2);
        cv.ChartName = "Кривая дефолтов договора: резерв стадии 1 — площадь слева от границы";
        cv.LabelX = "Месяц жизни договора";
        cv.LabelY = "Вероятность дефолта в месяце";

        var stages = rep.Table("Стадии обесценения",
            ["Стадия", "Договоров", "Экспозиция", "Резерв", "Покрытие"],
            [false, true, true, true, true]);

        foreach (StageSummary stage in result.Stages)
        {
            stages.Row(StageName(stage.Stage), Int(stage.Count), Money(stage.ExposureAtDefault),
                Money(stage.Ecl), Pct(stage.CoverageRatio, 2));
        }

        var scenarioTable = rep.Table("Макросценарии",
            ["Сценарий", "Вероятность", "Резерв", "Покрытие"], [false, true, true, true]);

        foreach (ScenarioEcl scenario in result.Scenarios)
        {
            scenarioTable.Row(scenario.Name, Pct(scenario.Probability, 0),
                Money(scenario.Ecl), Pct(scenario.CoverageRatio, 2));
        }

        string log =
            $"Портфель: {Int(result.Exposures.Count)} договоров на {Money(result.TotalExposure)}.\n" +
            $"Резерв {Money(result.TotalEcl)} — покрытие {Pct(result.CoverageRatio, 2)}.\n" +
            $"Если бы весь портфель считался по 12 месяцам: {Money(result.TotalEcl12Month)}; " +
            $"если бы весь на весь срок: {Money(result.TotalEclLifetime)}.\n" +
            $"Вклад стадирования: {Money(result.StagingEffect)}.";

        return Explain(rep, result, log);
    }

    private static string StageName(CreditStage stage) => stage switch
    {
        CreditStage.Performing => "Стадия 1",
        CreditStage.UnderPerforming => "Стадия 2",
        _ => "Стадия 3",
    };

    #endregion

    #region Матрица миграции

    private static string DoMigrationMatrix(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int observations = (int)p.GetValueOrDefault("observations", 8000);
        int grades = (int)p.GetValueOrDefault("grades", 5);
        double stability = p.GetValueOrDefault("stability", 0.85);
        double downgradeBias = p.GetValueOrDefault("downgrade", 2.0);
        int horizon = (int)p.GetValueOrDefault("horizon", 10);
        int seed = (int)p.GetValueOrDefault("seed", 11);

        string[] ratings = RatingScale(grades);
        Random rng = RandomEngine.Create(seed);
        var transitions = new List<RatingTransition>(observations);

        for (int i = 0; i < observations; i++)
        {
            int from = i % (grades - 1);
            double[] row = TruthRow(from, grades, stability, downgradeBias);

            double draw = rng.NextDouble();
            double cumulative = 0;
            int to = grades - 1;

            for (int j = 0; j < grades; j++)
            {
                cumulative += row[j];
                if (draw <= cumulative) { to = j; break; }
            }

            transitions.Add(new RatingTransition(ratings[from], ratings[to]));
        }

        MigrationMatrixResult result = MigrationMatrix.Estimate(ratings, transitions);
        IReadOnlyList<Vector> curves = MigrationMatrix.CumulativeDefault(result, horizon);
        Vector stationary = MigrationMatrix.StationaryDistribution(result);

        var periods = Axis(horizon, 1);
        for (int i = 0; i < grades - 1; i++)
            cv.AddPlot(periods, curves[i], $"Кумулятивная PD: {ratings[i]}", C(i), 2);

        cv.ChartName = $"Кумулятивная вероятность дефолта на горизонте {horizon} периодов";
        cv.LabelX = "Периодов от начала";
        cv.LabelY = "Накопленная вероятность дефолта";

        var matrix = rep.Table("Матрица переходов",
            ["Из \\ В", .. ratings], [false, .. ratings.Select(_ => true)]);

        for (int i = 0; i < grades; i++)
        {
            var cells = new string[grades + 1];
            cells[0] = ratings[i];
            for (int j = 0; j < grades; j++) cells[j + 1] = Pct(result.Transitions[i, j], 2);
            matrix.Row(cells);
        }

        var profiles = rep.Table("Профили рейтингов",
            ["Рейтинг", "Наблюдений", "Устойчивость", "Повышения", "Понижения", "PD за период"],
            [false, true, true, true, true, true]);

        foreach (RatingProfile profile in result.Profiles)
        {
            profiles.Row(profile.Rating, Int(profile.Observations), Pct(profile.Stability, 1),
                Pct(profile.UpgradeRate, 2), Pct(profile.DowngradeRate, 2), Pct(profile.DefaultRate, 2));
        }

        var equilibrium = rep.Table("Стационарное распределение", ["Рейтинг", "Доля"], [false, true]);
        for (int i = 0; i < grades; i++) equilibrium.Row(ratings[i], Pct(stationary[i], 1));

        string log =
            $"Оценено по {Int(observations)} переходам между {grades} классами.\n" +
            $"Средняя устойчивость рейтинга {Pct(result.AverageStability, 1)}, " +
            $"перевес понижений {Pct(result.NetDowngradeDrift, 2)}.\n" +
            $"Кумулятивная PD худшего неденефолтного класса за {horizon} периодов: " +
            $"{Pct(curves[grades - 2][horizon - 1], 2)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Рейтинговая шкала нужной длины с дефолтом в конце.</summary>
    private static string[] RatingScale(int grades)
    {
        string[] names = ["AAA", "AA", "A", "BBB", "BB", "B", "CCC"];
        var scale = new string[grades];

        for (int i = 0; i < grades - 1; i++) scale[i] = i < names.Length ? names[i] : $"R{i + 1}";
        scale[grades - 1] = "D";

        return scale;
    }

    /// <summary>Строка истинной матрицы переходов генерирующей модели.</summary>
    private static double[] TruthRow(int from, int grades, double stability, double downgradeBias)
    {
        var row = new double[grades];
        row[from] = stability;

        double rest = 1 - stability;
        double weightUp = 0, weightDown = 0;

        for (int j = 0; j < grades; j++)
        {
            if (j == from) continue;
            if (j < from) weightUp += 1.0 / (from - j);
            else weightDown += downgradeBias / (j - from);
        }

        double total = weightUp + weightDown;
        if (total <= 0) { row[from] = 1; return row; }

        for (int j = 0; j < grades; j++)
        {
            if (j == from) continue;
            double weight = j < from ? 1.0 / (from - j) : downgradeBias / (j - from);
            row[j] = rest * weight / total;
        }

        return row;
    }

    #endregion

    #region Перетекание просрочки

    private static string DoRollRate(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int periods = (int)p.GetValueOrDefault("periods", 12);
        double portfolio = p.GetValueOrDefault("portfolio", 1_000_000_000);
        double entry = p.GetValueOrDefault("entry", 0.04);
        double roll = p.GetValueOrDefault("roll", 0.55);
        double trend = p.GetValueOrDefault("trend", 0.01);
        double noise = p.GetValueOrDefault("noise", 0.05);
        int seed = (int)p.GetValueOrDefault("seed", 3);

        IReadOnlyList<string> buckets = RollRate.DefaultBuckets();
        Random rng = RandomEngine.Create(seed);
        var balances = new Matrix(periods, buckets.Count);

        for (int t = 0; t < periods; t++)
        {
            double stage = Math.Clamp(roll + (trend * t), 0.02, 0.98);
            balances[t, 0] = portfolio * (1 + (noise * RandomEngine.NextGaussian(rng)));
            balances[t, 1] = balances[t, 0] * entry * (1 + (noise * RandomEngine.NextGaussian(rng)));

            for (int b = 2; b < buckets.Count; b++)
                balances[t, b] = balances[t, b - 1] * stage * (1 + (noise * RandomEngine.NextGaussian(rng)));
        }

        RollRateResult result = RollRate.Analyze(buckets, balances);

        var steps = Axis(result.Steps.Count, 1);
        cv.AddPlot(steps, Vec(result.Steps.Select(s => s.AverageRollRate)),
            "Средняя ставка перетекания", C(0), 3);
        cv.AddPlot(steps, Vec(result.Steps.Select(s => s.LatestRollRate)),
            "Последний период", C(3), 2);
        cv.ChartName = "Скорость перетекания по шагам: от текущей задолженности к списанию";
        cv.LabelX = "Номер шага";
        cv.LabelY = "Доля перетекающего остатка";

        var table = rep.Table("Шаги перетекания",
            ["Переход", "Средняя ставка", "Разброс", "Последний период", "Периодов"],
            [false, true, true, true, true]);

        foreach (RollRateStep step in result.Steps)
        {
            table.Row($"{step.FromBucket} → {step.ToBucket}", Pct(step.AverageRollRate, 1),
                Pct(step.StandardDeviation, 2), Pct(step.LatestRollRate, 1), Int(step.Observations));
        }

        var path = rep.Table("Остатки и путь до списания",
            ["Корзина", "Остаток", "Доля дохода до списания", "Ожидаемые потери"],
            [false, true, true, true]);

        for (int b = 0; b < buckets.Count; b++)
        {
            path.Row(buckets[b], Money(result.LatestBalances[b]), Pct(result.RollToLoss[b], 3),
                Money(result.LatestBalances[b] * result.RollToLoss[b]));
        }

        string log =
            $"История: {periods} периодов, {buckets.Count} корзин.\n" +
            $"Из текущей задолженности до списания доходит {Pct(result.RollToLoss[0], 3)}.\n" +
            $"Ожидаемые потери из сложившихся остатков: {Money(result.ImpliedLoss)} " +
            $"({Pct(result.ImpliedLossRate, 2)} портфеля).";

        return Explain(rep, result, log);
    }

    #endregion

    #region Винтажный анализ

    private static string DoVintage(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int count = (int)p.GetValueOrDefault("vintages", 8);
        int maxAge = (int)p.GetValueOrDefault("max_age", 24);
        double terminal = p.GetValueOrDefault("terminal", 0.06);
        double drift = p.GetValueOrDefault("drift", 0.004);
        double seasoning = p.GetValueOrDefault("seasoning", 8);
        double noise = p.GetValueOrDefault("noise", 0.08);
        int seed = (int)p.GetValueOrDefault("seed", 23);

        Random rng = RandomEngine.Create(seed);
        var cohorts = new List<VintageCohort>(count);

        for (int v = 0; v < count; v++)
        {
            int age = Math.Max(3, maxAge - v);
            double level = Math.Max(0.001, terminal + (drift * v));
            double shock = 1 + (noise * RandomEngine.NextGaussian(rng));
            var curve = new List<double>(age);

            for (int t = 1; t <= age; t++)
                curve.Add(Math.Max(0, level * shock * (1 - Math.Exp(-t / Math.Max(1, seasoning)))));

            cohorts.Add(new VintageCohort($"В-{v + 1:00}", 100_000_000 * (1 + (0.05 * v)), curve));
        }

        VintageResult result = VintageAnalysis.Analyze(cohorts);

        var ages = Axis(result.MaturityCurve.Count, 1);
        cv.AddPlot(ages, Vec(result.MaturityCurve), "Кривая созревания", C(0), 3);

        for (int v = 0; v < Math.Min(count, 4); v++)
        {
            cv.AddPlot(Axis(cohorts[v].CumulativeLossRate.Count, 1),
                Vec(cohorts[v].CumulativeLossRate), $"Винтаж {cohorts[v].Name}", C(v + 2), 1);
        }

        Segment(cv, result.CommonAge, 0, result.CommonAge,
            result.MaturityCurve.Count > 0 ? result.MaturityCurve[^1] : 1, C(5),
            $"Возраст сопоставления: {result.CommonAge} мес.", 2);
        cv.ChartName = "Накопленные потери по возрасту: сравнивать винтажи можно только слева от отсечки";
        cv.LabelX = "Возраст, мес.";
        cv.LabelY = "Накопленная доля потерь";

        var table = rep.Table("Винтажи",
            ["Винтаж", "Выдачи", "Возраст", "Потери на общем возрасте", "Прогноз за срок", "К среднему"],
            [false, true, true, true, true, true]);

        foreach (VintageProfile vintage in result.Vintages)
        {
            table.Row(vintage.Name, Money(vintage.OriginationAmount), Int(vintage.Age),
                Pct(vintage.LossAtCommonAge, 2), Pct(vintage.ProjectedLifetimeLoss, 2),
                Num(vintage.RelativeToAverage, 2));
        }

        var maturity = rep.Table("Кривая созревания",
            ["Возраст", "Накопленные потери", "Прирост за месяц"], [true, true, true]);

        for (int age = 0; age < result.MaturityCurve.Count; age++)
            maturity.Row($"{age + 1} мес.", Pct(result.MaturityCurve[age], 3), Pct(result.MarginalCurve[age], 3));

        string log =
            $"Винтажей: {result.Vintages.Count}, возраст сопоставления {result.CommonAge} мес., " +
            $"максимальный {result.MaxAge} мес.\n" +
            $"Тренд качества выдач: {Pct(result.QualityTrend, 3)} за винтаж " +
            $"(уровень значимости {Num(result.TrendPValue, 4)}).\n" +
            $"Половина потерь реализуется к {result.HalfLossAge} месяцу.\n" +
            $"Прогноз потерь по всем выдачам: {Money(result.ProjectedPortfolioLoss)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Модель Мертона

    private static string DoMerton(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var input = new MertonInput
        {
            Company = "Публичная компания",
            EquityValue = p.GetValueOrDefault("equity", 5_000_000_000),
            EquityVolatility = p.GetValueOrDefault("vol", 0.35),
            ShortTermDebt = p.GetValueOrDefault("short_debt", 1_200_000_000),
            LongTermDebt = p.GetValueOrDefault("long_debt", 2_800_000_000),
            RiskFreeRate = p.GetValueOrDefault("rate", 0.07),
            AssetDrift = p.GetValueOrDefault("drift", 0.09),
            Horizon = p.GetValueOrDefault("horizon", 1),
        };

        MertonResult result = MertonModel.Estimate(input);

        // Зависимость вероятности дефолта от долговой нагрузки при прочих равных
        const int steps = 40;
        var leverage = new Vector(steps);
        var probability = new Vector(steps);
        var distance = new Vector(steps);

        double totalDebt = Math.Max(input.ShortTermDebt + input.LongTermDebt, 1);

        for (int i = 0; i < steps; i++)
        {
            double factor = 0.2 + (i * 2.3 / (steps - 1));
            MertonResult point = MertonModel.Estimate(input with
            {
                ShortTermDebt = input.ShortTermDebt * factor,
                LongTermDebt = input.LongTermDebt * factor,
            });

            leverage[i] = point.Leverage;
            probability[i] = point.ProbabilityOfDefault;
            distance[i] = point.DistanceToDefault;
        }

        cv.AddPlot(leverage, probability, "Вероятность дефолта", C(3), 3);
        Segment(cv, result.Leverage, 0, result.Leverage, probability.Max(), C(0),
            $"Текущая нагрузка: {Pct(result.Leverage, 1)}", 2);
        cv.ChartName = "Вероятность дефолта как функция долговой нагрузки";
        cv.LabelX = "Точка дефолта к активам";
        cv.LabelY = "Вероятность дефолта за горизонт";

        var table = rep.Table("Чувствительность к долгу",
            ["Долг к активам", "Расстояние до дефолта", "Вероятность дефолта"], [true, true, true]);

        for (int i = 0; i < steps; i += 4)
            table.Row(Pct(leverage[i], 1), Num(distance[i], 2), Pct(probability[i], 3));

        string log =
            $"Капитализация {Money(input.EquityValue)} при волатильности {Pct(input.EquityVolatility, 1)}.\n" +
            $"Восстановленные активы: {Money(result.AssetValue)}, волатильность активов " +
            $"{Pct(result.AssetVolatility, 1)} — рычаг увеличивает риск акционера в " +
            $"{Num(input.EquityVolatility / Math.Max(result.AssetVolatility, 1e-9), 2)} раза.\n" +
            $"Точка дефолта {Money(result.DefaultPoint)} ({Pct(result.Leverage, 1)} активов).\n" +
            $"Расстояние до дефолта {Num(result.DistanceToDefault, 2)}, вероятность " +
            $"{Pct(result.ProbabilityOfDefault, 3)}, кредитный спред {Pct(result.ImpliedCreditSpread, 2)}.\n" +
            $"Сходимость: {(result.Converged ? "достигнута" : "не достигнута")} за {result.Iterations} итераций. " +
            $"Совокупный долг {Money(totalDebt)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Скоринг контрагента

    private static string DoCounterparty(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double revenue = p.GetValueOrDefault("revenue", 1_200_000_000);
        double margin = p.GetValueOrDefault("margin", 0.12);
        double equityShare = p.GetValueOrDefault("equity_share", 0.6);
        double currentRatio = p.GetValueOrDefault("current_ratio", 2.0);
        double delay = p.GetValueOrDefault("delay", 5);
        double years = p.GetValueOrDefault("years", 8);
        double concentration = p.GetValueOrDefault("concentration", 0.3);
        double limit = p.GetValueOrDefault("limit", 40_000_000);
        bool taxArrears = p.GetValueOrDefault("tax", 0) > 0.5;

        double capital = revenue * 0.35;
        double debt = equityShare >= 1 ? 0 : capital * (1 - equityShare) / Math.Max(equityShare, 1e-6);
        double currentLiabilities = Math.Max(revenue * 0.2, 1);

        var profile = new CounterpartyProfile
        {
            Name = "Контрагент",
            Revenue = revenue,
            Ebitda = revenue * margin,
            Equity = capital,
            TotalDebt = debt,
            CurrentAssets = currentLiabilities * currentRatio,
            CurrentLiabilities = currentLiabilities,
            RevenueGrowth = 0.1,
            YearsInBusiness = years,
            AveragePaymentDelayDays = delay,
            DisputeRate = 0.02,
            BuyerConcentration = concentration,
            HasTaxArrears = taxArrears,
            RequestedLimit = limit,
        };

        CounterpartyScore score = CounterpartyScoring.Score(profile);

        var index = new Vector(score.Factors.Count);
        var contributions = new Vector(score.Factors.Count);
        var potential = new Vector(score.Factors.Count);

        for (int i = 0; i < score.Factors.Count; i++)
        {
            index[i] = i + 1;
            contributions[i] = score.Factors[i].Contribution;
            potential[i] = score.Factors[i].Weight * 100;
        }

        cv.AddPlot(index, potential, "Максимум по фактору", C(5), 2);
        cv.AddPlot(index, contributions, "Набрано баллов", C(0), 3);
        cv.ChartName = $"Вклад факторов: {Num(score.Score, 1)} из 100, класс {score.Grade}";
        cv.LabelX = "Номер фактора";
        cv.LabelY = "Баллы";

        var table = rep.Table("Факторы скоринга",
            ["Фактор", "Значение", "Оценка", "Вес", "Баллы", "Комментарий"],
            [false, true, true, true, true, false]);

        foreach (CounterpartyFactor factor in score.Factors)
        {
            table.Row(factor.Name, Num(factor.Value, 3), Pct(factor.Score, 0),
                Pct(factor.Weight, 0), Num(factor.Contribution, 1), factor.Comment);
        }

        // Как меняется лимит при разной платёжной дисциплине
        var delays = new Vector(19);
        var limits = new Vector(19);
        for (int i = 0; i < 19; i++)
        {
            double days = i * 5;
            delays[i] = days;
            limits[i] = CounterpartyScoring.Score(profile with { AveragePaymentDelayDays = days }).RecommendedLimit;
        }

        var sensitivity = rep.Table("Лимит и платёжная дисциплина",
            ["Просрочка", "Рекомендованный лимит"], [true, true]);
        for (int i = 0; i < 19; i += 3) sensitivity.Row($"{Num(delays[i], 0)} дн.", Money(limits[i]));

        string log =
            $"Балл {Num(score.Score, 1)} из 100, класс {score.Grade}, вероятность дефолта " +
            $"{Pct(score.ProbabilityOfDefault, 2)}.\n" +
            $"Рекомендованный лимит {Money(score.RecommendedLimit)} при запрошенных {Money(score.RequestedLimit)}.\n" +
            $"Ставка финансирования в факторинге {Pct(score.AdvanceRate, 0)}, " +
            $"ожидаемые потери {Money(score.ExpectedLoss)}.\n" +
            $"Решение: {score.Decision}." +
            (score.StopFactors.Count > 0
                ? $"\nСтоп-факторы: {string.Join(", ", score.StopFactors)}."
                : "\nСтоп-факторов нет.");

        return Explain(rep, score, log);
    }

    #endregion

    #region Синтетические заявки

    /// <summary>Названия признаков синтетической скоринговой выборки.</summary>
    private static readonly string[] ScoreVariableNames = ["доход", "срок_работы", "долговая_нагрузка"];

    /// <summary>
    /// Заявки с известным исходом: логит зависит от дохода, стажа и нагрузки.
    /// </summary>
    /// <remarks>
    /// Свободный член подбирается так, чтобы доля дефолтов совпала с заданной:
    /// иначе ползунок силы связи менял бы сразу и разделимость, и базовую частоту.
    /// </remarks>
    private static (Matrix Values, List<bool> Defaults) Applications(
        int n, double signal, double badRate, int seed)
    {
        Random rng = RandomEngine.Create(seed);
        var values = new Matrix(n, 3);
        var latent = new double[n];

        for (int i = 0; i < n; i++)
        {
            double income = Math.Exp(RandomEngine.NextGaussian(rng, 11, 0.5));
            double tenure = Math.Max(0, RandomEngine.NextGaussian(rng, 5, 3));
            double burden = Math.Clamp(RandomEngine.NextGaussian(rng, 0.35, 0.15), 0.01, 0.95);

            values[i, 0] = income;
            values[i, 1] = tenure;
            values[i, 2] = burden;

            latent[i] = signal * ((-1.5 * (Math.Log(income) - 11)) - (0.22 * tenure) + (4.5 * burden) - 1.075);
        }

        double intercept = Math.Log(badRate / (1 - badRate)) - latent.Average();
        var defaults = new List<bool>(n);

        for (int i = 0; i < n; i++)
        {
            double probability = 1.0 / (1.0 + Math.Exp(-(intercept + latent[i])));
            defaults.Add(rng.NextDouble() < probability);
        }

        return (values, defaults);
    }

    #endregion
}

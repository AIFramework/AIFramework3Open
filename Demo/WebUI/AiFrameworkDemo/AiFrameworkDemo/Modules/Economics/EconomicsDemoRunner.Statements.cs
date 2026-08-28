using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Statements;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категории «Финанализ и форензика».</summary>
public static partial class EconomicsDemoRunner
{
    #region Коэффициентный анализ

    private static string DoFinancialRatios(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (FinancialStatement previous, FinancialStatement current) = Statements(p);
        RatioReport report = FinancialRatios.Compute(current, previous);

        var index = new Vector(report.Ratios.Count);
        var relative = new Vector(report.Ratios.Count);

        for (int i = 0; i < report.Ratios.Count; i++)
        {
            FinancialRatio ratio = report.Ratios[i];
            index[i] = i + 1;

            // Значение в долях от ориентира: единица — ровно по бенчмарку
            double benchmark = Math.Abs(ratio.Benchmark) > 1e-9 ? ratio.Benchmark : 1;
            relative[i] = Math.Clamp(ratio.Value / benchmark, -1, 4);
        }

        cv.AddPlot(index, relative, "Значение к ориентиру", C(0), 3);
        Segment(cv, 1, 1, report.Ratios.Count, 1, C(3), "Ориентир", 2);
        cv.ChartName = "Коэффициенты в долях от отраслевого ориентира";
        cv.LabelX = "Номер коэффициента";
        cv.LabelY = "Значение в долях от ориентира";

        foreach (string group in new[]
                 { "Ликвидность", "Рентабельность", "Оборачиваемость", "Долговая нагрузка", "Денежный поток" })
        {
            var table = rep.Table(group,
                ["Коэффициент", "Значение", "Ориентир", "Смысл"], [false, true, true, false]);

            foreach (FinancialRatio ratio in report.Group(group))
            {
                string value = ratio.Unit == "дн." || ratio.Unit == "раз"
                    ? Num(ratio.Value, 1) + (string.IsNullOrEmpty(ratio.Unit) ? "" : " " + ratio.Unit)
                    : Num(ratio.Value, 3);

                table.Row(ratio.Name, value, Num(ratio.Benchmark, 2), ratio.Comment);
            }
        }

        string log =
            $"Отчётность: выручка {Money(current.Revenue)}, активы {Money(current.TotalAssets)}, " +
            $"капитал {Money(current.Equity)}.\n" +
            $"Рассчитано {report.Ratios.Count} коэффициентов, ориентирам соответствует " +
            $"{Pct(report.BenchmarkPassRate, 0)}.\n" +
            $"Рентабельность капитала {Pct(report.ReturnOnEquity, 2)}, активов {Pct(report.ReturnOnAssets, 2)}, " +
            $"финансовый цикл {Num(report.CashConversionCycle, 0)} дн.";

        return Explain(rep, report, log);
    }

    #endregion

    #region Разложение Дюпона

    private static string DoDuPont(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (FinancialStatement previous, FinancialStatement current) = Statements(p);
        DuPontResult result = DuPontAnalysis.Analyze(current, previous);

        var index = new Vector(result.FiveFactor.Count);
        var contributions = new Vector(result.FiveFactor.Count);
        var cumulative = new Vector(result.FiveFactor.Count);
        double running = 0;

        for (int i = 0; i < result.FiveFactor.Count; i++)
        {
            index[i] = i + 1;
            contributions[i] = result.FiveFactor[i].Contribution;
            running += contributions[i];
            cumulative[i] = result.PreviousReturnOnEquity + running;
        }

        cv.AddPlot(index, contributions, "Вклад множителя", C(0), 3);
        cv.AddPlot(index, cumulative, "Накопленная рентабельность капитала", C(1), 2);
        Segment(cv, 1, result.PreviousReturnOnEquity, result.FiveFactor.Count, result.PreviousReturnOnEquity,
            C(5), "Было", 1);
        Segment(cv, 1, result.ReturnOnEquity, result.FiveFactor.Count, result.ReturnOnEquity,
            C(3), "Стало", 1);
        cv.ChartName = "Последовательная подстановка: сумма вкладов равна изменению рентабельности";
        cv.LabelX = "Номер множителя";
        cv.LabelY = "Рентабельность капитала";

        var five = rep.Table("Пятифакторная модель",
            ["Множитель", "Было", "Стало", "Изменение", "Вклад", "Смысл"],
            [false, true, true, true, true, false]);

        foreach (DuPontFactor factor in result.FiveFactor)
        {
            five.Row(factor.Name, Num(factor.Previous, 3), Num(factor.Value, 3),
                Num(factor.Change, 3), Pct(factor.Contribution, 3), factor.Meaning);
        }

        var three = rep.Table("Трёхфакторная модель",
            ["Множитель", "Было", "Стало", "Вклад"], [false, true, true, true]);

        foreach (DuPontFactor factor in result.ThreeFactor)
            three.Row(factor.Name, Num(factor.Previous, 3), Num(factor.Value, 3), Pct(factor.Contribution, 3));

        string log =
            $"Рентабельность капитала: {Pct(result.PreviousReturnOnEquity, 2)} → {Pct(result.ReturnOnEquity, 2)} " +
            $"(изменение {Pct(result.Change, 2)}).\n" +
            $"Сумма вкладов пяти множителей: {Pct(result.FiveFactor.Sum(f => f.Contribution), 2)} — " +
            "разложение точное по построению.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Модели банкротства

    private static string DoDistressScores(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (FinancialStatement previous, FinancialStatement current) = Statements(p);
        DistressReport report = DistressScores.Evaluate(current, previous);

        // Чувствительность классических моделей к долговой нагрузке
        const int steps = 31;
        var leverage = new Vector(steps);
        var altman = new Vector(steps);
        var altmanDouble = new Vector(steps);
        var springate = new Vector(steps);

        for (int i = 0; i < steps; i++)
        {
            double share = i * 0.9 / (steps - 1);
            var scenario = new Dictionary<string, double>(p) { ["leverage"] = share };
            (_, FinancialStatement variant) = Statements(scenario);

            leverage[i] = share;
            altman[i] = DistressScores.Altman(variant).Value;
            altmanDouble[i] = DistressScores.AltmanDoublePrime(variant).Value;
            springate[i] = DistressScores.Springate(variant).Value;
        }

        cv.AddPlot(leverage, altman, "Альтман Z", C(0), 3);
        cv.AddPlot(leverage, altmanDouble, "Альтман Z''", C(1), 2);
        cv.AddPlot(leverage, springate, "Спрингейт", C(2), 2);
        Segment(cv, 0, 1.81, 0.9, 1.81, C(3), "Порог Z = 1,81", 1);
        Segment(cv, p.GetValueOrDefault("leverage", 0.35), altman.Min(),
            p.GetValueOrDefault("leverage", 0.35), altman.Max(), C(5), "Текущая нагрузка", 2);
        cv.ChartName = "Баллы моделей банкротства как функция долговой нагрузки";
        cv.LabelX = "Долг к активам";
        cv.LabelY = "Балл модели";

        var scores = rep.Table("Модели",
            ["Модель", "Балл", "Зона", "Порог риска", "Порог устойчивости", "Комментарий"],
            [false, true, false, true, true, false]);

        foreach (DistressScore score in report.Scores)
        {
            scores.Row(score.Model, Num(score.Value, 3), ZoneName(score.Zone),
                Num(score.Thresholds.Distress, 2), Num(score.Thresholds.Safe, 2), score.Comment);
        }

        DistressScore altmanScore = report.Scores[0];
        var components = rep.Table("Слагаемые модели Альтмана",
            ["Показатель", "Значение", "Вес", "Вклад"], [false, true, true, true]);

        foreach (ScoreComponent component in altmanScore.Components)
        {
            components.Row(component.Name, Num(component.Value, 3),
                Num(component.Weight, 3), Num(component.Contribution, 3));
        }

        var piotroski = rep.Table("Критерии Пиотроски",
            ["Критерий", "Выполнен", "Комментарий"], [false, false, false]);

        foreach ((string criterion, bool passed, string comment) in report.PiotroskiCriteria)
            piotroski.Row(criterion, passed ? "да" : "нет", comment);

        string log =
            $"Компания: выручка {Money(current.Revenue)}, долг к активам " +
            $"{Pct(current.TotalDebt / current.TotalAssets, 1)}.\n" +
            $"Риск банкротства видят {report.DistressVotes} модели из {report.Scores.Count}, " +
            $"устойчивость — {report.SafeVotes}.\n" +
            $"Балл Пиотроски {report.PiotroskiScore} из 9.";

        return Explain(rep, report, log);
    }

    private static string ZoneName(DistressZone zone) => zone switch
    {
        DistressZone.Safe => "устойчивая",
        DistressZone.Distress => "риск банкротства",
        _ => "серая",
    };

    #endregion

    #region M-score Бениша

    private static string DoBeneish(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (FinancialStatement previous, FinancialStatement current) = Statements(p);
        BeneishResult result = BeneishModel.Compute(current, previous);

        var indices = result.Indices.Where(i => i.Code != "CONST").ToList();
        var index = new Vector(indices.Count);
        var values = new Vector(indices.Count);
        var neutral = new Vector(indices.Count);

        for (int i = 0; i < indices.Count; i++)
        {
            index[i] = i + 1;
            values[i] = Math.Clamp(indices[i].Value, -1, 3);
            neutral[i] = indices[i].Neutral;
        }

        cv.AddPlot(index, values, "Значение индекса", C(0), 3);
        cv.AddPlot(index, neutral, "Нейтральный уровень", C(5), 2);
        cv.ChartName = $"Индексы Бениша: M = {Num(result.MScore, 3)} при пороге {Num(result.Threshold, 2)}";
        cv.LabelX = "Номер индекса";
        cv.LabelY = "Значение индекса";

        var table = rep.Table("Индексы модели",
            ["Код", "Индекс", "Значение", "Нейтраль", "Вес", "Вклад", "Смысл"],
            [false, false, true, true, true, true, false]);

        foreach (BeneishIndex bi in result.Indices)
        {
            table.Row(bi.Code, bi.Name, Num(bi.Value, 3), Num(bi.Neutral, 2),
                Num(bi.Weight, 3), Num(bi.Contribution, 3), bi.Comment);
        }

        // Как быстро балл переходит порог при росте разрыва прибыли и денег
        var gaps = new Vector(21);
        var mscores = new Vector(21);

        for (int i = 0; i < 21; i++)
        {
            double gap = i * 0.01;
            var scenario = new Dictionary<string, double>(p) { ["accruals"] = gap };
            (FinancialStatement basePrevious, FinancialStatement variant) = Statements(scenario);

            gaps[i] = gap;
            mscores[i] = BeneishModel.Compute(variant, basePrevious).MScore;
        }

        var sensitivity = rep.Table("Чувствительность к начислениям",
            ["Разрыв прибыли и денег", "M-балл"], [true, true]);
        for (int i = 0; i < 21; i += 4) sensitivity.Row(Pct(gaps[i], 1), Num(mscores[i], 3));

        string log =
            $"M-балл {Num(result.MScore, 3)} при пороге {Num(result.Threshold, 2)}: " +
            (result.IsLikelyManipulator ? "зона вероятной манипуляции." : "вне зоны манипуляции.") + "\n" +
            $"Вероятность по пробит-шкале {Pct(result.Probability, 2)}.\n" +
            $"Настораживающих индексов: {result.Indices.Count(i => i.Code != "CONST" && i.IsFlagged)} из {indices.Count}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Оборотный капитал

    private static string DoWorkingCapital(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (_, FinancialStatement current) = Statements(p);

        var targets = new WorkingCapitalTargets
        {
            DaysSalesOutstanding = p.GetValueOrDefault("target_dso", 40),
            DaysInventoryOutstanding = p.GetValueOrDefault("target_dio", 45),
            DaysPayablesOutstanding = p.GetValueOrDefault("dpo", 90),
            CostOfFunding = p.GetValueOrDefault("funding", 0.18),
        };

        WorkingCapitalResult result = WorkingCapitalAnalysis.Analyze(current, targets);

        // Сколько денег высвобождает каждый день сокращения сбора дебиторки
        var days = new Vector(31);
        var released = new Vector(31);
        double perDay = current.Revenue / 365;

        for (int i = 0; i < 31; i++)
        {
            days[i] = result.DaysSalesOutstanding - i;
            released[i] = i * perDay;
        }

        cv.AddPlot(days, released, "Высвобождение денег", C(0), 3);
        Segment(cv, targets.DaysSalesOutstanding, 0, targets.DaysSalesOutstanding, released.Max(),
            C(3), $"Цель: {Num(targets.DaysSalesOutstanding, 0)} дн.", 2);
        cv.ChartName = "Цена одного дня сбора дебиторской задолженности";
        cv.LabelX = "Срок сбора, дн.";
        cv.LabelY = "Высвобожденные деньги, руб.";

        var drivers = rep.Table("Драйверы цикла",
            ["Драйвер", "Сейчас", "Цель", "Цена дня", "Эффект", "Смысл"],
            [false, true, true, true, true, false]);

        foreach (WorkingCapitalDriver driver in result.Drivers)
        {
            drivers.Row(driver.Name, $"{Num(driver.Days, 0)} дн.", $"{Num(driver.TargetDays, 0)} дн.",
                Money(driver.AmountPerDay), Money(driver.CashImpact), driver.Comment);
        }

        string log =
            $"Финансовый цикл {Num(result.CashConversionCycle, 0)} дн. " +
            $"(операционный {Num(result.OperatingCycle, 0)} дн.).\n" +
            $"В обороте связано {Money(result.WorkingCapital)} — {Pct(result.WorkingCapitalToRevenue, 1)} выручки.\n" +
            $"Потенциал высвобождения {Money(result.PotentialCashRelease)}, экономия на процентах " +
            $"{Money(result.AnnualFundingSaving)} в год.\n" +
            $"Каждый процент роста выручки требует {Money(result.FundingPerGrowthPoint)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Качество прибыли

    private static string DoEarningsQuality(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (FinancialStatement previous, FinancialStatement current) = Statements(p);
        EarningsQualityResult result = EarningsQuality.Evaluate(current, previous);

        // Как падает сводная оценка с ростом разрыва между прибылью и деньгами
        var gaps = new Vector(21);
        var scores = new Vector(21);
        var accruals = new Vector(21);

        for (int i = 0; i < 21; i++)
        {
            double gap = i * 0.01;
            var scenario = new Dictionary<string, double>(p) { ["accruals"] = gap };
            (FinancialStatement basePrevious, FinancialStatement variant) = Statements(scenario);
            EarningsQualityResult point = EarningsQuality.Evaluate(variant, basePrevious);

            gaps[i] = gap;
            scores[i] = point.QualityScore;
            accruals[i] = point.AccrualRatio * 100;
        }

        cv.AddPlot(gaps, scores, "Сводная оценка качества", C(0), 3);
        cv.AddPlot(gaps, accruals, "Доля начислений, %", C(3), 2);
        Segment(cv, p.GetValueOrDefault("accruals", 0.02), 0, p.GetValueOrDefault("accruals", 0.02),
            100, C(5), "Текущий разрыв", 2);
        cv.ChartName = "Качество прибыли против разрыва между прибылью и денежным потоком";
        cv.LabelX = "Разрыв прибыли и денег, доля выручки";
        cv.LabelY = "Оценка / доля начислений";

        var table = rep.Table("Показатели качества",
            ["Показатель", "Значение", "Порог", "Оценка", "Смысл"], [false, true, true, true, false]);

        foreach (EarningsQualityMetric metric in result.Metrics)
        {
            table.Row(metric.Name, Num(metric.Value, 3), Num(metric.Threshold, 2),
                Pct(metric.Score, 0), metric.Comment);
        }

        string log =
            $"Сводная оценка {Num(result.QualityScore, 0)} из 100 — {result.Verdict}.\n" +
            $"Доля начислений {Pct(result.AccrualRatio, 2)} активов, поток к прибыли " +
            $"{Num(result.CashFlowToNetIncome, 2)}.\n" +
            $"Дебиторская задолженность опережает выручку на {Pct(result.ReceivablesDivergence, 1)}, " +
            $"запасы опережают себестоимость на {Pct(result.InventoryDivergence, 1)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Закон Бенфорда

    private static string DoBenford(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var scope = (int)p.GetValueOrDefault("scope", 0) == 0
            ? BenfordScope.FirstDigit
            : BenfordScope.FirstTwoDigits;

        int pattern = (int)p.GetValueOrDefault("pattern", 0);
        int n = (int)p.GetValueOrDefault("n", 4000);
        double spread = p.GetValueOrDefault("spread", 2.5);
        double contamination = p.GetValueOrDefault("contamination", 0.25);
        double threshold = p.GetValueOrDefault("threshold", 500_000);
        int seed = (int)p.GetValueOrDefault("seed", 31);

        Random rng = RandomEngine.Create(seed);
        var payments = new List<double>(n);
        string dataset = pattern switch
        {
            1 => "платежи с дроблением под порог",
            2 => "придуманные суммы",
            _ => "естественные платежи",
        };

        for (int i = 0; i < n; i++)
        {
            bool tampered = pattern > 0 && rng.NextDouble() < contamination;

            if (!tampered)
            {
                payments.Add(Math.Exp(RandomEngine.NextGaussian(rng, Math.Log(100_000), spread)));
                continue;
            }

            payments.Add(pattern == 1
                // Дробление: суммы жмутся к порогу согласования снизу
                ? threshold * (0.85 + (0.14 * rng.NextDouble()))
                // Придуманные суммы: человек распределяет первые цифры почти равномерно
                : Math.Round((rng.NextDouble() * 900_000) + 100_000));
        }

        BenfordResult result = BenfordAnalysis.Analyze(payments, scope, dataset);

        var digits = new Vector(result.Digits.Count);
        var observed = new Vector(result.Digits.Count);
        var expected = new Vector(result.Digits.Count);

        for (int i = 0; i < result.Digits.Count; i++)
        {
            digits[i] = result.Digits[i].Digit;
            observed[i] = result.Digits[i].ObservedShare;
            expected[i] = result.Digits[i].ExpectedShare;
        }

        cv.AddPlot(digits, expected, "Ожидание по закону Бенфорда", C(5), 3);
        cv.AddPlot(digits, observed, "Наблюдаемая частота", C(0), 3);
        cv.ChartName = $"{dataset}: {result.Conformity}";
        cv.LabelX = "Цифровая группа";
        cv.LabelY = "Доля наблюдений";

        var table = rep.Table("Частоты цифр",
            ["Цифра", "Наблюдений", "Доля", "Ожидание", "Z"], [true, true, true, true, true]);

        foreach (BenfordDigit digit in result.Digits)
        {
            table.Row(digit.Digit.ToString(), Int(digit.Observed), Pct(digit.ObservedShare, 2),
                Pct(digit.ExpectedShare, 2), Num(digit.ZScore, 2));
        }

        if (result.Suspicious.Count > 0)
        {
            var suspicious = rep.Table("Подозрительные группы",
                ["Цифра", "Отклонение доли", "Z"], [true, true, true]);

            foreach (BenfordDigit digit in result.Suspicious.Take(12))
            {
                suspicious.Row(digit.Digit.ToString(),
                    Pct(digit.ObservedShare - digit.ExpectedShare, 2), Num(digit.ZScore, 2));
            }
        }

        string log =
            $"Проверено {Int(result.SampleSize)} значений, отброшено {Int(result.Excluded)}.\n" +
            $"Хи-квадрат {Num(result.ChiSquare, 1)}, уровень значимости {Num(result.PValue, 5)}.\n" +
            $"Среднее абсолютное отклонение {Num(result.MeanAbsoluteDeviation, 5)} — {result.Conformity}.\n" +
            $"Значимо отклоняются {result.Suspicious.Count} групп из {result.Digits.Count}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Предсказание банкротства

    private static string DoBankruptcyMl(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int choice = (int)p.GetValueOrDefault("model", 3);
        int n = (int)p.GetValueOrDefault("n", 400);
        double rate = p.GetValueOrDefault("rate", 0.25);
        double signal = p.GetValueOrDefault("signal", 6);
        int folds = (int)p.GetValueOrDefault("folds", 5);
        int seed = (int)p.GetValueOrDefault("seed", 21);

        List<BankruptcyObservation> sample = BankruptcySample(n, rate, signal, seed);

        var predictor = new BankruptcyPredictor();
        BankruptcyModelResult result;
        IReadOnlyList<BankruptcyModelResult> comparison = [];

        if (choice >= 3)
        {
            comparison = BankruptcyPredictor.CompareAll(sample, folds, seed);
            result = predictor.Train(sample, comparison[0].Model, folds, seed);
        }
        else
        {
            result = predictor.Train(sample, (BankruptcyModelKind)choice, folds, seed);
        }

        cv.AddPlot(result.CrossValidated.RocFalsePositive, result.CrossValidated.RocTruePositive,
            $"Скользящий контроль: AUC = {Num(result.CrossValidated.Auc, 3)}", C(0), 3);
        cv.AddPlot(result.InSample.RocFalsePositive, result.InSample.RocTruePositive,
            $"Обучающая выборка: AUC = {Num(result.InSample.Auc, 3)}", C(3), 2);
        Segment(cv, 0, 0, 1, 1, C(5), "Случайная модель", 1);
        cv.ChartName = "Разрыв между кривыми — это переобучение, а не качество модели";
        cv.LabelX = "Доля ложных тревог";
        cv.LabelY = "Доля пойманных банкротств";

        if (comparison.Count > 0)
        {
            var models = rep.Table("Сравнение моделей",
                ["Модель", "Джини на контроле", "Джини на обучении", "Разрыв", "KS", "Брайер"],
                [false, true, true, true, true, true]);

            foreach (BankruptcyModelResult candidate in comparison)
            {
                models.Row(ModelName(candidate.Model), Num(candidate.CrossValidated.Gini, 3),
                    Num(candidate.InSample.Gini, 3), Num(candidate.OverfitGap, 3),
                    Num(candidate.CrossValidated.Ks, 3), Num(candidate.CrossValidated.Brier, 4));
            }
        }

        var importance = rep.Table("Важность признаков",
            ["Признак", "Падение AUC", "Среднее у выживших", "Среднее у банкротов"],
            [false, true, true, true]);

        foreach (FeatureImportance feature in result.Importances)
        {
            importance.Row(feature.Feature, Num(feature.Importance, 4),
                Num(feature.MeanHealthy, 3), Num(feature.MeanBankrupt, 3));
        }

        (_, FinancialStatement current) = Statements(p.ContainsKey("revenue") ? p : DefaultStatementParams());
        BankruptcyPrediction prediction = predictor.Predict(current);

        var profile = rep.Table("Прогноз для эталонной компании",
            ["Признак", "Значение", "У выживших", "У банкротов"], [false, true, true, true]);

        foreach ((string feature, double value, double healthy, double bankrupt) in prediction.Features)
            profile.Row(feature, Num(value, 3), Num(healthy, 3), Num(bankrupt, 3));

        string log =
            $"Выборка: {Int(result.Observations)} компаний, банкротств {Int(result.Bankruptcies)} " +
            $"({Pct((double)result.Bankruptcies / result.Observations, 1)}).\n" +
            $"Модель: {ModelName(result.Model)}.\n" +
            $"Джини на контроле {Num(result.CrossValidated.Gini, 3)} против " +
            $"{Num(result.InSample.Gini, 3)} на обучении — разрыв {Num(result.OverfitGap, 3)}.\n" +
            $"Прогноз для эталонной компании: {Pct(prediction.Probability, 2)} " +
            $"при балле Альтмана {Num(prediction.AltmanZ, 2)}.";

        return Explain(rep, result, log) + "\n\n" + prediction.Interpret().ToLlmText();
    }

    private static string ModelName(BankruptcyModelKind kind) => kind switch
    {
        BankruptcyModelKind.Logistic => "логистическая регрессия",
        BankruptcyModelKind.Bayesian => "байесовский классификатор",
        _ => "машина опорных векторов",
    };

    /// <summary>
    /// Обучающая выборка: качество бизнеса задаёт и отчётность, и исход.
    /// </summary>
    /// <remarks>
    /// Связь через одну скрытую переменную — это честная модель ситуации:
    /// отчётность не вызывает банкротство, а отражает то же состояние дел.
    /// </remarks>
    private static List<BankruptcyObservation> BankruptcySample(
        int n, double rate, double signal, int seed)
    {
        Random rng = RandomEngine.Create(seed);
        var sample = new List<BankruptcyObservation>(n);

        // Порог качества подбирается так, чтобы доля банкротств совпала с заданной
        double threshold = 0.5 + (Math.Log(rate / (1 - rate)) / signal);

        for (int i = 0; i < n; i++)
        {
            double quality = Math.Clamp(RandomEngine.NextGaussian(rng, 0.5, 0.22), 0.02, 0.98);
            double revenue = Math.Exp(RandomEngine.NextGaussian(rng, 20, 1.0));
            double probability = 1.0 / (1.0 + Math.Exp(signal * (quality - threshold)));

            sample.Add(new BankruptcyObservation(
                CompanyStatement($"Компания {i + 1}", "2024", revenue, quality),
                rng.NextDouble() < probability));
        }

        return sample;
    }

    #endregion

    #region Синтетическая отчётность

    /// <summary>Параметры отчётности по умолчанию для алгоритмов без своих ползунков.</summary>
    private static Dictionary<string, double> DefaultStatementParams() => new()
    {
        ["revenue"] = 1_000_000_000,
        ["gross_margin"] = 0.4,
        ["opex"] = 0.22,
        ["leverage"] = 0.35,
        ["dso"] = 65,
        ["dio"] = 90,
        ["dpo"] = 90,
        ["accruals"] = 0.02,
        ["growth"] = 0.15,
    };

    /// <summary>
    /// Строит согласованную пару отчётностей по управленческим параметрам.
    /// </summary>
    /// <remarks>
    /// Баланс собирается из сроков оборота, а не задаётся напрямую: так каждый
    /// ползунок демо соответствует решению, которое компания действительно
    /// принимает, а отчётность остаётся внутренне непротиворечивой.
    /// </remarks>
    private static (FinancialStatement Previous, FinancialStatement Current) Statements(
        IReadOnlyDictionary<string, double> p)
    {
        double revenue = p.GetValueOrDefault("revenue", 1_000_000_000);
        double growth = p.GetValueOrDefault("growth", 0.15);

        FinancialStatement current = BuildStatement(
            "Компания", "текущий период", revenue,
            p.GetValueOrDefault("gross_margin", 0.4),
            p.GetValueOrDefault("opex", 0.22),
            p.GetValueOrDefault("leverage", 0.35),
            p.GetValueOrDefault("dso", 65),
            p.GetValueOrDefault("dio", 90),
            p.GetValueOrDefault("dpo", 90),
            p.GetValueOrDefault("accruals", 0.02));

        // Предыдущий период: меньше масштаб, чуть выше маржа и заметно
        // меньше разрыв между прибылью и деньгами
        FinancialStatement previous = BuildStatement(
            "Компания", "предыдущий период", revenue / (1 + growth),
            Math.Min(0.95, p.GetValueOrDefault("gross_margin", 0.4) + 0.02),
            p.GetValueOrDefault("opex", 0.22),
            p.GetValueOrDefault("leverage", 0.35),
            p.GetValueOrDefault("dso", 65) * 0.85,
            p.GetValueOrDefault("dio", 90) * 0.9,
            p.GetValueOrDefault("dpo", 90),
            p.GetValueOrDefault("accruals", 0.02) * 0.3);

        return (previous, current);
    }

    /// <summary>Отчётность компании заданного качества для обучающих выборок.</summary>
    private static FinancialStatement CompanyStatement(
        string company, string period, double revenue, double quality)
    {
        double q = Math.Clamp(quality, 0, 1);

        return BuildStatement(company, period, revenue,
            grossMargin: 0.25 + (0.3 * q),
            opexShare: 0.28 - (0.1 * q),
            leverage: 0.75 - (0.55 * q),
            dso: 100 - (55 * q),
            dio: 130 - (75 * q),
            dpo: 60 + (30 * q),
            accrualGap: 0.09 * (1 - q));
    }

    /// <summary>Собирает баланс, отчёт о прибылях и денежный поток из сроков оборота.</summary>
    private static FinancialStatement BuildStatement(
        string company, string period, double revenue, double grossMargin, double opexShare,
        double leverage, double dso, double dio, double dpo, double accrualGap)
    {
        double cogs = revenue * (1 - Math.Clamp(grossMargin, 0.01, 0.95));
        double opex = revenue * Math.Max(0, opexShare);
        double depreciation = revenue * 0.05;
        double operatingIncome = revenue - cogs - opex - depreciation;

        double receivables = dso * revenue / 365;
        double inventory = dio * cogs / 365;
        double payables = dpo * cogs / 365;
        double cash = revenue * 0.08;
        double investments = revenue * 0.02;

        double currentAssets = receivables + inventory + cash + investments;
        double ppe = revenue * 0.5;
        double intangibles = revenue * 0.05;
        double assets = currentAssets + ppe + intangibles;

        double debt = assets * Math.Clamp(leverage, 0, 0.95);
        double shortTermDebt = debt * 0.3;
        double longTermDebt = debt * 0.7;
        double currentLiabilities = payables + shortTermDebt;
        double liabilities = currentLiabilities + longTermDebt;
        double equity = assets - liabilities;

        double interest = debt * 0.13;
        double pretax = operatingIncome - interest;
        double tax = Math.Max(0, pretax * 0.2);
        double netIncome = pretax - tax;

        return new FinancialStatement
        {
            Company = company,
            Period = period,
            TotalAssets = assets,
            CurrentAssets = currentAssets,
            Cash = cash,
            ShortTermInvestments = investments,
            AccountsReceivable = receivables,
            Inventory = inventory,
            PropertyPlantEquipment = ppe,
            IntangibleAssets = intangibles,
            TotalLiabilities = liabilities,
            CurrentLiabilities = currentLiabilities,
            AccountsPayable = payables,
            ShortTermDebt = shortTermDebt,
            LongTermDebt = longTermDebt,
            RetainedEarnings = equity * 0.6,
            Revenue = revenue,
            CostOfGoodsSold = cogs,
            OperatingExpenses = opex,
            Depreciation = depreciation,
            InterestExpense = interest,
            IncomeTax = tax,
            NetIncome = netIncome,
            OperatingCashFlow = netIncome + depreciation - (accrualGap * revenue),
            CapitalExpenditures = revenue * 0.06,
            DividendsPaid = Math.Max(0, netIncome * 0.2),
            MarketCapitalization = Math.Max(equity, 0) * 2.5,
        };
    }

    #endregion
}

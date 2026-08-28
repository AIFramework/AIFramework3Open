using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы причинного вывода и анализа временных рядов.</summary>
public static partial class EconomicsDemoRunner
{
    #region Разность разностей

    private static string DoCausalDid(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int units = (int)p.GetValueOrDefault("units", 60);
        int periods = (int)p.GetValueOrDefault("periods", 8);
        double effect = p.GetValueOrDefault("effect", 3);
        bool staggered = p.GetValueOrDefault("staggered", 1) > 0.5;
        double pretrend = p.GetValueOrDefault("pretrend", 0);
        double noise = p.GetValueOrDefault("noise", 0.5);
        int boot = (int)p.GetValueOrDefault("boot", 60);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 15));

        var observations = new List<DidObservation>(units * periods);
        int early = Math.Max(2, periods / 2);
        int late = Math.Max(early + 1, (periods * 3) / 4);

        for (int u = 0; u < units; u++)
        {
            int first = staggered
                ? u % 3 == 0 ? 0 : u % 3 == 1 ? early : late
                : u % 2 == 0 ? early : 0;

            double level = RandomEngine.NextGaussian(rng, 10, 2);

            for (int t = 1; t <= periods; t++)
            {
                double outcome = level + (0.5 * t) + RandomEngine.NextGaussian(rng, 0, noise);

                // Нарушение параллельных трендов заложено отдельным параметром
                if (first > 0) outcome += pretrend * t;
                if (first > 0 && t >= first) outcome += effect;

                observations.Add(new DidObservation(u, t, outcome, first));
            }
        }

        DidResult result = DifferenceInDifferences.Estimate(observations, boot, 7);

        var relatives = Vec(result.EventStudy.Select(e => (double)e.RelativePeriod));

        cv.AddPlot(relatives, Vec(result.EventStudy.Select(e => e.Estimate)), "Эффект по периодам", C(0), 3);
        cv.AddPlot(relatives, Vec(result.EventStudy.Select(e => e.ConfidenceLow)), "Нижняя граница", C(5), 1);
        cv.AddPlot(relatives, Vec(result.EventStudy.Select(e => e.ConfidenceHigh)), "Верхняя граница", C(5), 1);
        Segment(cv, relatives.Min(), 0, relatives.Max(), 0, C(3), "Ноль", 1);
        cv.ChartName = $"Эффект {Num(result.RobustAtt, 3)} при истинном {Num(effect, 2)}";
        cv.LabelX = "Периодов от внедрения";
        cv.LabelY = "Оценка эффекта";

        var table = rep.Table("Динамика эффекта",
            ["Период", "Оценка", "Ст. ошибка", "Интервал", "Наблюдений"],
            [true, true, true, false, true]);

        foreach (EventStudyPoint point in result.EventStudy)
        {
            table.Row($"{point.RelativePeriod:+0;-0;0}", Num(point.Estimate, 3),
                Num(point.StandardError, 3),
                $"[{Num(point.ConfidenceLow, 2)}; {Num(point.ConfidenceHigh, 2)}]",
                $"{point.Observations}");
        }

        var estimators = rep.Table("Сравнение оценок",
            ["Оценка", "Значение", "Ст. ошибка"], [false, true, true]);
        estimators.Row("Устойчивая (по когортам)", Num(result.RobustAtt, 4), Num(result.RobustStandardError, 4));
        estimators.Row("Двусторонние фиксированные эффекты",
            Num(result.TwoWayFixedEffects, 4), Num(result.TwoWayStandardError, 4));
        estimators.Row("Истинный эффект", Num(effect, 4), "—");

        string log =
            $"Объектов {result.Treated} под воздействием, {result.NeverTreated} контрольных, " +
            $"{result.Cohorts} когорт.\n" +
            $"Устойчивая оценка {Num(result.RobustAtt, 4)} при истинном {Num(effect, 3)}.\n" +
            $"Двусторонние фиксированные эффекты дают {Num(result.TwoWayFixedEffects, 4)}.\n" +
            $"Параллельность трендов: p = {Num(result.PreTrendPValue, 4)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Разрывный дизайн

    private static string DoCausalRdd(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 2000);
        double jump = p.GetValueOrDefault("jump", 2);
        double slope = p.GetValueOrDefault("slope", 0.5);
        double curvature = p.GetValueOrDefault("curvature", 0);
        double noise = p.GetValueOrDefault("noise", 0.3);
        double manipulation = p.GetValueOrDefault("manipulation", 0);
        double bandwidth = p.GetValueOrDefault("bandwidth", 0);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 16));

        var observations = new List<RddObservation>(n);

        for (int i = 0; i < n; i++)
        {
            double running = (rng.NextDouble() * 4) - 2;

            // Манипуляция: часть объектов чуть ниже порога подтягивается выше
            if (manipulation > 0 && running is > -0.3 and < 0 && rng.NextDouble() < manipulation)
                running = Math.Abs(running) * 0.5;

            double outcome = 1 + (slope * running) + (curvature * running * running)
                + (running >= 0 ? jump : 0) + RandomEngine.NextGaussian(rng, 0, noise);

            observations.Add(new RddObservation(running, outcome));
        }

        RddResult result = RegressionDiscontinuity.Estimate(observations, 0, bandwidth);

        // Средние по узким интервалам переменной назначения
        const int bins = 40;
        var centres = new Vector(bins);
        var means = new Vector(bins);

        for (int b = 0; b < bins; b++)
        {
            double low = -2 + (b * 4.0 / bins);
            double high = low + (4.0 / bins);

            var inside = observations.Where(o => o.Running >= low && o.Running < high).ToList();

            centres[b] = (low + high) / 2;
            means[b] = inside.Count > 0 ? inside.Average(o => o.Outcome) : double.NaN;
        }

        cv.AddPlot(centres, Finite(means), "Средние по интервалам", C(1), 2);
        Segment(cv, 0, means.Where(double.IsFinite).DefaultIfEmpty(0).Min(),
            0, means.Where(double.IsFinite).DefaultIfEmpty(0).Max(), C(3), "Порог", 2);
        cv.ChartName = $"Скачок {Num(result.Effect, 3)} при истинном {Num(jump, 2)}";
        cv.LabelX = "Переменная назначения";
        cv.LabelY = "Отклик";

        var table = rep.Table("Оценка", ["Показатель", "Значение"], [false, true]);
        table.Row("Скачок", Num(result.Effect, 4));
        table.Row("Стандартная ошибка", Num(result.StandardError, 4));
        table.Row("Интервал", $"[{Num(result.ConfidenceLow, 3)}; {Num(result.ConfidenceHigh, 3)}]");
        table.Row("Предел слева", Num(result.LeftLimit, 3));
        table.Row("Предел справа", Num(result.RightLimit, 3));
        table.Row("Полоса пропускания", Num(result.Bandwidth, 3));
        table.Row("Наблюдений в полосе", $"{result.LeftObservations} слева, {result.RightObservations} справа");

        var sensitivity = rep.Table("Устойчивость к полосе",
            ["Полоса", "Оценка", "Ст. ошибка"], [true, true, true]);

        foreach ((double width, double estimate, double error) in result.Sensitivity)
            sensitivity.Row(Num(width, 3), Num(estimate, 3), Num(error, 3));

        var placebo = rep.Table("Ложные пороги",
            ["Порог", "Оценка", "Ст. ошибка", "Значим"], [true, true, true, false]);

        foreach ((double cutoff, double estimate, double error) in result.Placebo)
        {
            placebo.Row(Num(cutoff, 2), Num(estimate, 3), Num(error, 3),
                error > 0 && Math.Abs(estimate / error) > 1.96 ? "да" : "нет");
        }

        string log =
            $"Скачок на пороге {Num(result.Effect, 4)} при истинном {Num(jump, 3)}, " +
            $"p = {Num(result.PValue, 5)}.\n" +
            $"Полоса пропускания {Num(result.Bandwidth, 3)}, наблюдений " +
            $"{result.LeftObservations} и {result.RightObservations}.\n" +
            $"Проверка плотности: статистика {Num(result.DensityStatistic, 3)}, " +
            $"p = {Num(result.DensityPValue, 4)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Сопоставление по склонности

    private static string DoCausalMatching(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 2000);
        double effect = p.GetValueOrDefault("effect", 1);
        double selection = p.GetValueOrDefault("selection", 1.0);
        double confounding = p.GetValueOrDefault("confounding", 1.5);
        double caliper = p.GetValueOrDefault("caliper", 0.2);
        int neighbours = (int)p.GetValueOrDefault("neighbours", 3);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 17));

        var covariates = new Matrix(n, 2);
        var treatment = new Vector(n);
        var outcome = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            double b = RandomEngine.NextGaussian(rng);

            covariates[i, 0] = a;
            covariates[i, 1] = b;

            double probability = 1.0 / (1.0 + Math.Exp(-selection * ((0.9 * a) + (0.5 * b))));
            bool treated = rng.NextDouble() < probability;

            treatment[i] = treated ? 1 : 0;
            outcome[i] = (confounding * a) + (0.7 * b) + (treated ? effect : 0)
                + RandomEngine.NextGaussian(rng, 0, 0.5);
        }

        MatchingResult result = PropensityScoreMatching.Estimate(
            covariates, treatment, outcome, ["опыт", "размер"], caliper, neighbours);

        var index = Axis(result.Balance.Count, 1);
        cv.AddPlot(index, Vec(result.Balance.Select(b => b.StandardizedBefore)), "До сопоставления", C(3), 3);
        cv.AddPlot(index, Vec(result.Balance.Select(b => b.StandardizedAfter)), "После сопоставления", C(0), 3);
        Segment(cv, 1, 0.1, result.Balance.Count, 0.1, C(5), "Порог баланса", 1);
        Segment(cv, 1, -0.1, result.Balance.Count, -0.1, C(5), "", 1);
        cv.ChartName = $"Эффект {Num(result.AverageTreatmentEffectOnTreated, 3)} против наивных " +
                       $"{Num(result.NaiveDifference, 3)}";
        cv.LabelX = "Ковариата";
        cv.LabelY = "Стандартизованная разность";

        var balance = rep.Table("Баланс ковариат",
            ["Ковариата", "Воздействие", "Контроль до", "Контроль после", "Разность до", "Разность после"],
            [false, true, true, true, true, true]);

        foreach (BalanceCheck check in result.Balance)
        {
            balance.Row(check.Variable, Num(check.BeforeTreated, 3), Num(check.BeforeControl, 3),
                Num(check.AfterControl, 3), Num(check.StandardizedBefore, 3),
                Num(check.StandardizedAfter, 3));
        }

        var estimates = rep.Table("Оценки эффекта", ["Оценка", "Значение"], [false, true]);
        estimates.Row("Истинный эффект", Num(effect, 4));
        estimates.Row("Наивная разность средних", Num(result.NaiveDifference, 4));
        estimates.Row("После сопоставления", Num(result.AverageTreatmentEffectOnTreated, 4));
        estimates.Row("Стандартная ошибка", Num(result.StandardError, 4));
        estimates.Row("Общая поддержка", Pct(result.CommonSupport, 1));

        string log =
            $"Истинный эффект {Num(effect, 3)}.\n" +
            $"Наивная разность {Num(result.NaiveDifference, 4)} смещена отбором.\n" +
            $"После сопоставления {Num(result.AverageTreatmentEffectOnTreated, 4)} " +
            $"(p = {Num(result.PValue, 4)}).\n" +
            $"Сопоставлено {result.Matched} из {result.Treated} объектов.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Синтетический контроль

    private static string DoSyntheticControl(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int periods = (int)p.GetValueOrDefault("periods", 30);
        int treatment = Math.Clamp((int)p.GetValueOrDefault("treatment", 20), 3, periods - 2);
        int donorCount = (int)p.GetValueOrDefault("donors", 8);
        double effect = p.GetValueOrDefault("effect", 5);
        double noise = p.GetValueOrDefault("noise", 0.3);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 18));

        var donors = new Matrix(periods, donorCount);
        var affected = new Vector(periods);

        var common = new double[periods];
        for (int t = 0; t < periods; t++)
            common[t] = 10 + (0.3 * t) + RandomEngine.NextGaussian(rng, 0, 1);

        for (int j = 0; j < donorCount; j++)
        {
            double loading = 0.7 + (0.6 * rng.NextDouble());

            for (int t = 0; t < periods; t++)
                donors[t, j] = (common[t] * loading) + RandomEngine.NextGaussian(rng, 0, noise);
        }

        for (int t = 0; t < periods; t++)
        {
            // Объект воспроизводится комбинацией первых двух доноров
            affected[t] = (0.5 * donors[t, 0]) + (0.5 * donors[t, 1])
                + RandomEngine.NextGaussian(rng, 0, noise / 2);

            if (t >= treatment) affected[t] += effect;
        }

        SyntheticControlResult result = SyntheticControl.Build(
            affected, donors, null, treatment, "регион");

        var axis = Axis(periods, 1);
        cv.AddPlot(axis, result.Actual, "Фактический ряд", C(0), 3);
        cv.AddPlot(axis, result.Synthetic, "Синтетический контроль", C(3), 3);
        cv.AddPlot(axis, result.Gap, "Разность", C(1), 2);
        Segment(cv, treatment, result.Gap.Min(), treatment, result.Actual.Max(), C(5), "Вмешательство", 2);
        cv.ChartName = $"Средний эффект {Num(result.AverageEffect, 3)} при истинном {Num(effect, 2)}";
        cv.LabelX = "Период";
        cv.LabelY = "Значение показателя";

        var weights = rep.Table("Веса доноров", ["Донор", "Вес"], [false, true]);
        foreach (DonorWeight weight in result.Weights.Where(w => w.Weight > 1e-4))
            weights.Row(weight.Donor, Pct(weight.Weight, 1));

        var placebo = rep.Table("Плацебо-тест",
            ["Донор", "Отношение ошибок"], [false, true]);

        foreach ((string donor, double ratio) in result.Placebo.OrderByDescending(x => x.Ratio))
            placebo.Row(donor, Num(ratio, 2));

        string log =
            $"Синтетический двойник построен из {result.ActiveDonors} доноров.\n" +
            $"Ошибка подгонки до вмешательства {Num(result.PreTreatmentRmspe, 4)}, после — " +
            $"{Num(result.PostTreatmentRmspe, 4)}.\n" +
            $"Отношение ошибок {Num(result.RmspeRatio, 2)}, p = {Num(result.PValue, 3)}.\n" +
            $"Средний эффект {Num(result.AverageEffect, 4)} при истинном {Num(effect, 3)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Причинный лес

    private static string DoCausalForest(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 2000);
        double effect = p.GetValueOrDefault("effect", 2);
        double share = p.GetValueOrDefault("share", 0.5);
        double noise = p.GetValueOrDefault("noise", 0.5);
        int trees = (int)p.GetValueOrDefault("trees", 80);
        int minLeaf = (int)p.GetValueOrDefault("min_leaf", 25);
        int depth = (int)p.GetValueOrDefault("depth", 3);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 19));

        var features = new Matrix(n, 3);
        var treatment = new Vector(n);
        var outcome = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double driver = rng.NextDouble();

            features[i, 0] = driver;
            features[i, 1] = rng.NextDouble();
            features[i, 2] = RandomEngine.NextGaussian(rng);

            bool treated = rng.NextDouble() < 0.5;
            treatment[i] = treated ? 1 : 0;

            // Эффект есть только у объектов с большим значением первого признака
            double individual = driver > 1 - share ? effect : 0;

            outcome[i] = 1 + (0.5 * driver) + (treated ? individual : 0)
                + RandomEngine.NextGaussian(rng, 0, noise);
        }

        CausalForestResult result = CausalForest.Fit(
            features, treatment, outcome, ["драйвер", "шум", "фон"], trees, minLeaf, depth, 5);

        var groups = Axis(result.Groups.Count, 1);
        cv.AddPlot(groups, Vec(result.Groups.Select(g => g.PredictedEffect)), "Предсказанный эффект", C(0), 3);
        cv.AddPlot(groups, Vec(result.Groups.Select(g => g.ActualEffect)), "Фактический эффект", C(3), 3);
        cv.ChartName = $"Средний эффект {Num(result.AverageEffect, 3)}, калибровка " +
                       $"{Num(result.CalibrationSlope, 2)}";
        cv.LabelX = "Группа по предсказанному эффекту";
        cv.LabelY = "Эффект воздействия";

        var table = rep.Table("Группы по эффекту",
            ["Группа", "Предсказано", "Фактически", "Объектов"], [true, true, true, true]);

        foreach (EffectGroup group in result.Groups)
        {
            table.Row($"{group.Group}", Num(group.PredictedEffect, 3), Num(group.ActualEffect, 3),
                $"{group.Size}");
        }

        var importance = rep.Table("Важность признаков",
            ["Признак", "Вклад в разбиения"], [false, true]);

        foreach ((string variable, double weight) in result.Importance)
            importance.Row(variable, Pct(weight, 1));

        string log =
            $"Средний эффект {Num(result.AverageEffect, 4)} при истинном " +
            $"{Num(effect * share, 3)} в среднем по выборке.\n" +
            $"Разброс индивидуальных эффектов {Num(result.EffectSpread, 4)}.\n" +
            $"Лучшая группа даёт {Num(result.Groups[0].ActualEffect, 3)}, худшая " +
            $"{Num(result.Groups[^1].ActualEffect, 3)}.\n" +
            $"Наиболее важный признак: {result.Importance[0].Variable}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Стационарность

    private static string DoStationarity(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 300);
        double persistence = p.GetValueOrDefault("persistence", 0.5);
        double trend = p.GetValueOrDefault("trend", 0);
        double noise = p.GetValueOrDefault("noise", 1.0);
        var terms = (DeterministicTerms)(int)p.GetValueOrDefault("terms", 1);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 20));

        var series = new Vector(n);
        double level = 0;

        for (int t = 0; t < n; t++)
        {
            level = (persistence * level) + RandomEngine.NextGaussian(rng, 0, noise);
            series[t] = level + (trend * t) + 100;
        }

        StationarityReport result = StationarityTests.Analyze(series, terms, -1, "ряд");

        var differenced = new Vector(n - 1);
        for (int t = 1; t < n; t++) differenced[t - 1] = series[t] - series[t - 1];

        cv.AddPlot(Axis(n, 1), series, "Исходный ряд", C(0), 2);
        cv.AddPlot(Axis(n - 1, 2), differenced, "Первая разность", C(3), 1);
        cv.ChartName = $"{result.Verdict}, порядок интегрирования {result.IntegrationOrder}";
        cv.LabelX = "Наблюдение";
        cv.LabelY = "Значение";

        var table = rep.Table("Тесты",
            ["Тест", "Гипотеза", "Статистика", "1%", "5%", "10%", "Вывод"],
            [false, false, true, true, true, true, false]);

        foreach (UnitRootTest test in new[] { result.AugmentedDickeyFuller, result.Kpss })
        {
            table.Row(test.Name, test.NullHypothesis, Num(test.Statistic, 3),
                Num(test.CriticalOnePercent, 3), Num(test.CriticalFivePercent, 3),
                Num(test.CriticalTenPercent, 3),
                test.Rejected ? "отвергается" : "не отвергается");
        }

        var levels = rep.Table("Тесты после дифференцирования",
            ["Ряд", "ADF", "Отвергается", "KPSS", "Отвергается"], [false, true, false, true, false]);

        UnitRootTest adfDiff = StationarityTests.DickeyFuller(differenced, terms);
        UnitRootTest kpssDiff = StationarityTests.Kpss(differenced, terms);

        levels.Row("Уровни", Num(result.AugmentedDickeyFuller.Statistic, 3),
            result.AugmentedDickeyFuller.Rejected ? "да" : "нет",
            Num(result.Kpss.Statistic, 3), result.Kpss.Rejected ? "да" : "нет");
        levels.Row("Первые разности", Num(adfDiff.Statistic, 3), adfDiff.Rejected ? "да" : "нет",
            Num(kpssDiff.Statistic, 3), kpssDiff.Rejected ? "да" : "нет");

        string log =
            $"Инерция ряда {Num(persistence, 2)}, тренд {Num(trend, 3)} за период.\n" +
            $"ADF: {Num(result.AugmentedDickeyFuller.Statistic, 3)} при критическом " +
            $"{Num(result.AugmentedDickeyFuller.CriticalFivePercent, 3)}.\n" +
            $"KPSS: {Num(result.Kpss.Statistic, 3)} при критическом " +
            $"{Num(result.Kpss.CriticalFivePercent, 3)}.\n" +
            $"Согласованный вывод: {result.Verdict}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Векторная авторегрессия

    private static string DoVarModel(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 500);
        int order = (int)p.GetValueOrDefault("order", 1);
        double own = p.GetValueOrDefault("own", 0.6);
        double cross = p.GetValueOrDefault("cross", 0.7);
        double feedback = p.GetValueOrDefault("feedback", 0);
        int horizon = (int)p.GetValueOrDefault("horizon", 20);
        double noise = p.GetValueOrDefault("noise", 0.5);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 21));

        var data = new Matrix(n, 2);
        double first = 0, second = 0;

        for (int t = 0; t < n; t++)
        {
            double nextFirst = (own * first) + (feedback * second) + RandomEngine.NextGaussian(rng, 0, noise);
            double nextSecond = (own * 0.5 * second) + (cross * first) + RandomEngine.NextGaussian(rng, 0, noise);

            first = nextFirst;
            second = nextSecond;

            data[t, 0] = first;
            data[t, 1] = second;
        }

        VarResult result = VectorAutoregression.Fit(data, order, ["первая", "вторая"]);
        double[][][] responses = VectorAutoregression.ImpulseResponse(result, horizon);
        Matrix decomposition = VectorAutoregression.VarianceDecomposition(result, horizon);

        var axis = Axis(horizon, 0);
        cv.AddPlot(axis, Vec(responses[0][1]), "Отклик второй на шок первой", C(0), 3);
        cv.AddPlot(axis, Vec(responses[1][0]), "Отклик первой на шок второй", C(3), 3);
        cv.AddPlot(axis, Vec(responses[0][0]), "Отклик первой на свой шок", C(1), 1);
        Segment(cv, 0, 0, horizon - 1, 0, C(5), "Ноль", 1);
        cv.ChartName = $"VAR({order}): максимальный корень {Num(result.SpectralRadius, 3)}";
        cv.LabelX = "Периодов после шока";
        cv.LabelY = "Отклик";

        var granger = rep.Table("Причинность по Гренджеру",
            ["Направление", "F", "p", "Вывод"], [false, true, true, false]);

        foreach (GrangerTest test in result.Granger)
        {
            granger.Row($"{test.From} → {test.To}", Num(test.FStatistic, 2), Num(test.PValue, 5),
                test.Causes ? "лаги улучшают прогноз" : "улучшения нет");
        }

        var fevd = rep.Table("Разложение дисперсии прогноза",
            ["Переменная", .. result.Variables.Select(v => $"шок {v}")],
            [false, .. result.Variables.Select(_ => true)]);

        for (int i = 0; i < result.Variables.Count; i++)
        {
            var cells = new string[result.Variables.Count + 1];
            cells[0] = result.Variables[i];

            for (int j = 0; j < result.Variables.Count; j++)
                cells[j + 1] = Pct(decomposition[i, j], 1);

            fevd.Row(cells);
        }

        var coefficients = rep.Table("Коэффициенты уравнений",
            ["Уравнение", "Константа", .. Enumerable.Range(0, result.Variables.Count * order)
                .Select(j => $"лаг {(j / result.Variables.Count) + 1}: {result.Variables[j % result.Variables.Count]}")],
            [false, true, .. Enumerable.Range(0, result.Variables.Count * order).Select(_ => true)]);

        for (int i = 0; i < result.Variables.Count; i++)
        {
            var cells = new string[result.Coefficients.Width + 1];
            cells[0] = result.Variables[i];

            for (int j = 0; j < result.Coefficients.Width; j++)
                cells[j + 1] = Num(result.Coefficients[i, j], 3);

            coefficients.Row(cells);
        }

        string log =
            $"Система из {result.Variables.Count} переменных, порядок {order}, " +
            $"{result.Observations} наблюдений.\n" +
            $"Максимальный корень {Num(result.SpectralRadius, 4)} — система " +
            $"{(result.IsStable ? "устойчива" : "неустойчива")}.\n" +
            $"Причинных связей: {result.Granger.Count(g => g.Causes)} из {result.Granger.Count}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Коинтеграция

    private static string DoCointegration(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 400);
        double beta = p.GetValueOrDefault("beta", 2);
        double adjustment = p.GetValueOrDefault("adjustment", 0.3);
        int lags = (int)p.GetValueOrDefault("lags", 1);
        double noise = p.GetValueOrDefault("noise", 0.4);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 22));

        var data = new Matrix(n, 2);
        double common = 0, second = 0;

        for (int t = 0; t < n; t++)
        {
            common += RandomEngine.NextGaussian(rng, 0, 1);

            // Вторая переменная возвращается к равновесию с заданной скоростью
            double equilibrium = beta * common;
            second += (adjustment * (equilibrium - second)) + RandomEngine.NextGaussian(rng, 0, noise);

            data[t, 0] = common;
            data[t, 1] = second;
        }

        JohansenResult johansen = Cointegration.Johansen(data, lags, ["первый", "второй"]);
        VecmResult vecm = Cointegration.ErrorCorrection(data, Math.Max(1, johansen.Rank), lags,
            ["первый", "второй"]);

        var axis = Axis(n, 1);
        cv.AddPlot(axis, Vec(Enumerable.Range(0, n).Select(t => data[t, 0])), "Первый ряд", C(0), 2);
        cv.AddPlot(axis, Vec(Enumerable.Range(0, n).Select(t => data[t, 1])), "Второй ряд", C(1), 2);
        cv.AddPlot(Axis(vecm.EquilibriumError.Count, lags + 1), vecm.EquilibriumError,
            "Отклонение от равновесия", C(3), 2);
        cv.ChartName = $"Ранг коинтеграции {johansen.Rank}, период полувозврата " +
                       $"{(double.IsFinite(vecm.HalfLife) ? Num(vecm.HalfLife, 1) : "—")}";
        cv.LabelX = "Наблюдение";
        cv.LabelY = "Значение";

        var trace = rep.Table("Тест Йохансена",
            ["Гипотеза", "Собственное число", "След", "Критическое", "Максимальное", "Критическое"],
            [false, true, true, true, true, true]);

        foreach (JohansenRow row in johansen.Rows)
        {
            trace.Row($"r ≤ {row.Rank}", Num(row.Eigenvalue, 4), Num(row.TraceStatistic, 2),
                Num(row.TraceCritical, 2), Num(row.MaxEigenStatistic, 2), Num(row.MaxEigenCritical, 2));
        }

        var vectors = rep.Table("Коинтеграционный вектор и приспособление",
            ["Переменная", "Бета", "Альфа", "p"], [false, true, true, true]);

        for (int i = 0; i < johansen.Variables.Count; i++)
        {
            Coefficient? alpha = i < vecm.AdjustmentCoefficients.Count ? vecm.AdjustmentCoefficients[i] : null;

            vectors.Row(johansen.Variables[i], Num(johansen.CointegratingVectors[i, 0], 4),
                alpha is not null ? Num(alpha.Estimate, 4) : "—",
                alpha is not null ? Num(alpha.PValue, 4) : "—");
        }

        string log =
            $"Истинное соотношение: второй ряд равен {Num(beta, 2)} первого.\n" +
            $"Ранг коинтеграции по следу: {johansen.Rank}.\n" +
            $"Оценённый коинтеграционный вектор: {Num(johansen.CointegratingVectors[1, 0], 4)} " +
            $"при нормировке на первую переменную.\n" +
            $"Период полувозврата к равновесию: " +
            $"{(double.IsFinite(vecm.HalfLife) ? Num(vecm.HalfLife, 2) : "не определён")}.";

        return Explain(rep, johansen, log) + "\n\n" + vecm.Interpret().ToLlmText();
    }

    #endregion

    #region Условная волатильность

    private static string DoGarch(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var model = (GarchModel)(int)p.GetValueOrDefault("model", 0);
        int n = (int)p.GetValueOrDefault("n", 1500);
        double alpha = p.GetValueOrDefault("alpha", 0.1);
        double beta = Math.Min(p.GetValueOrDefault("beta", 0.85), 0.98 - alpha);
        double leverage = p.GetValueOrDefault("leverage", 0);
        double longRun = p.GetValueOrDefault("vol", 0.02);
        int horizon = (int)p.GetValueOrDefault("horizon", 20);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 23));

        double persistence = alpha + (leverage / 2) + beta;
        double omega = longRun * longRun * Math.Max(1 - persistence, 0.01);

        var returns = new Vector(n);
        double variance = longRun * longRun;

        for (int t = 0; t < n; t++)
        {
            double shock = RandomEngine.NextGaussian(rng) * Math.Sqrt(variance);
            returns[t] = shock;

            double asymmetric = shock < 0 ? leverage : 0;
            variance = omega + ((alpha + asymmetric) * shock * shock) + (beta * variance);
        }

        GarchResult result = Garch.Fit(returns, model, horizon);

        var axis = Axis(n, 1);
        cv.AddPlot(axis, Vec(returns.Select(Math.Abs)), "Модуль доходности", C(1), 1);
        cv.AddPlot(axis, result.ConditionalVolatility, "Условная волатильность", C(0), 3);
        Segment(cv, 1, result.LongRunVolatility, n, result.LongRunVolatility, C(3),
            "Долгосрочная волатильность", 2);
        cv.ChartName = $"Инерция {Num(result.Persistence, 3)}, период полураспада " +
                       $"{(double.IsFinite(result.HalfLife) ? Num(result.HalfLife, 1) : "∞")}";
        cv.LabelX = "День";
        cv.LabelY = "Волатильность";

        var table = rep.Table("Параметры модели",
            ["Параметр", "Оценка", "Истинное значение"], [false, true, true]);
        table.Row("Постоянная", Num(result.Omega, 8), Num(omega, 8));
        table.Row("Реакция на шок", Num(result.Alpha, 4), Num(alpha, 4));
        table.Row("Память", Num(result.Beta, 4), Num(beta, 4));
        table.Row("Асимметрия", Num(result.Gamma, 4), Num(leverage, 4));
        table.Row("Инерция", Num(result.Persistence, 4), Num(persistence, 4));
        table.Row("Долгосрочная волатильность", Num(result.LongRunVolatility, 5), Num(longRun, 5));

        var forecast = rep.Table("Прогноз волатильности",
            ["День", "Волатильность"], [true, true]);

        for (int h = 0; h < result.Forecast.Count; h++)
            forecast.Row($"{h + 1}", Pct(result.Forecast[h], 3));

        var comparison = rep.Table("Сравнение спецификаций",
            ["Модель", "Логарифм правдоподобия", "AIC", "Инерция"], [false, true, true, true]);

        foreach (GarchModel candidate in Enum.GetValues<GarchModel>())
        {
            GarchResult item = Garch.Fit(returns, candidate, 1);
            comparison.Row(GarchLabel(candidate), Num(item.LogLikelihood, 1), Num(item.Aic, 1),
                Num(item.Persistence, 4));
        }

        string log =
            $"Спецификация: {GarchLabel(model)}.\n" +
            $"Инерция {Num(result.Persistence, 4)} при истинной {Num(persistence, 4)}.\n" +
            $"Долгосрочная волатильность {Pct(result.LongRunVolatility, 3)}, текущая " +
            $"{Pct(result.ConditionalVolatility[^1], 3)}.\n" +
            $"ARCH-LM на остатках: p = {Num(result.ArchPValue, 4)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название спецификации условной дисперсии.</summary>
    private static string GarchLabel(GarchModel model) => model switch
    {
        GarchModel.Garch => "GARCH(1,1)",
        GarchModel.GjrGarch => "GJR-GARCH",
        _ => "EGARCH",
    };

    #endregion

    #region Фильтр Калмана

    private static string DoStateSpace(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var model = (StateSpaceModel)(int)p.GetValueOrDefault("model", 0);
        int n = (int)p.GetValueOrDefault("n", 200);
        double levelSd = p.GetValueOrDefault("level_sd", 0.3);
        double slopeSd = p.GetValueOrDefault("slope_sd", 0.02);
        double noiseSd = p.GetValueOrDefault("noise_sd", 2.0);
        int horizon = (int)p.GetValueOrDefault("horizon", 12);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 24));

        var series = new Vector(n);
        var trueLevel = new Vector(n);
        double level = 100, slope = 0;

        for (int t = 0; t < n; t++)
        {
            if (model == StateSpaceModel.LocalLinearTrend)
            {
                slope += RandomEngine.NextGaussian(rng, 0, slopeSd);
                level += slope;
            }

            level += RandomEngine.NextGaussian(rng, 0, levelSd);

            trueLevel[t] = level;
            series[t] = level + RandomEngine.NextGaussian(rng, 0, noiseSd);
        }

        StateSpaceResult result = StateSpace.Fit(series, model, horizon);

        var axis = Axis(n, 1);
        cv.AddPlot(axis, series, "Наблюдения", C(1), 1);
        cv.AddPlot(axis, trueLevel, "Истинный уровень", C(5), 2);
        cv.AddPlot(axis, result.Level, "Сглаженный уровень", C(0), 3);
        cv.AddPlot(Axis(horizon, n + 1), result.Forecast, "Прогноз", C(3), 3);
        cv.ChartName = $"Сигнал-шум {Num(result.SignalToNoise, 4)}";
        cv.LabelX = "Наблюдение";
        cv.LabelY = "Значение";

        var table = rep.Table("Оценённые дисперсии",
            ["Компонента", "Оценка", "Истинное значение"], [false, true, true]);
        table.Row("Шум наблюдения", Num(result.ObservationVariance, 5), Num(noiseSd * noiseSd, 5));
        table.Row("Шум уровня", Num(result.LevelVariance, 5), Num(levelSd * levelSd, 5));

        if (model == StateSpaceModel.LocalLinearTrend)
            table.Row("Шум наклона", Num(result.SlopeVariance, 7), Num(slopeSd * slopeSd, 7));

        var forecast = rep.Table("Прогноз",
            ["Шаг", "Прогноз", "Нижняя граница", "Верхняя граница"], [true, true, true, true]);

        for (int h = 0; h < result.Forecast.Count; h++)
        {
            forecast.Row($"{h + 1}", Num(result.Forecast[h], 2),
                Num(result.ForecastLower[h], 2), Num(result.ForecastUpper[h], 2));
        }

        // Насколько сглаживание уменьшило колебания ряда
        double seriesVariation = 0, levelVariation = 0;
        for (int t = 1; t < n; t++)
        {
            seriesVariation += Math.Abs(series[t] - series[t - 1]);
            levelVariation += Math.Abs(result.Level[t] - result.Level[t - 1]);
        }

        string log =
            $"Модель: {(model == StateSpaceModel.LocalLevel ? "локальный уровень" : "локальный тренд")}.\n" +
            $"Отношение сигнал-шум {Num(result.SignalToNoise, 5)}.\n" +
            $"Средний шаг ряда {Num(seriesVariation / (n - 1), 3)}, сглаженного уровня " +
            $"{Num(levelVariation / (n - 1), 3)}.\n" +
            $"Люнг — Бокс на ошибках прогноза: p = {Num(result.LjungBoxPValue, 4)}.";

        return Explain(rep, result, log);
    }

    #endregion
}

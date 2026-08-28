using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категории «Эконометрика».</summary>
public static partial class EconomicsDemoRunner
{
    #region Регрессия с устойчивыми ошибками

    private static string DoRegressionRobust(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 400);
        double hetero = p.GetValueOrDefault("hetero", 1.0);
        double autocorrelation = p.GetValueOrDefault("autocorr", 0);
        int clusterCount = Math.Max(2, (int)p.GetValueOrDefault("clusters", 20));
        double clusterShock = p.GetValueOrDefault("cluster_shock", 0);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 1));

        var x = new Matrix(n, 2);
        var y = new Vector(n);
        var clusters = new List<int>(n);

        var shocks = new double[clusterCount];
        for (int g = 0; g < clusterCount; g++) shocks[g] = RandomEngine.NextGaussian(rng, 0, clusterShock);

        double previous = 0;

        for (int i = 0; i < n; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            double b = RandomEngine.NextGaussian(rng);
            int group = i % clusterCount;

            x[i, 0] = a;
            x[i, 1] = b;
            clusters.Add(group);

            // Ошибка: гетероскедастичная, автокоррелированная и с групповым шоком
            double innovation = RandomEngine.NextGaussian(rng, 0, 0.5 * (1 + (hetero * Math.Abs(a))));
            previous = (autocorrelation * previous) + innovation;

            y[i] = 1 + (2 * a) - (0.5 * b) + previous + shocks[group];
        }

        var variance = (RobustVariance)VarianceIndex((int)p.GetValueOrDefault("variance", 3));

        var options = new RegressionOptions
        {
            Variance = variance,
            Clusters = variance == RobustVariance.Clustered ? clusters : null,
        };

        RegressionResult result = LinearRegression.Fit(x, y, ["a", "b"], options);
        RegressionResult classical = LinearRegression.Fit(x, y, ["a", "b"]);

        var index = Axis(result.Coefficients.Count, 1);
        cv.AddPlot(index, Vec(result.Coefficients.Select(c => c.StandardError)),
            "Выбранные ошибки", C(0), 3);
        cv.AddPlot(index, Vec(classical.Coefficients.Select(c => c.StandardError)),
            "Классические ошибки", C(5), 2);
        cv.ChartName = $"R² = {Num(result.RSquared, 3)}, ошибки: {VarianceLabel(variance)}";
        cv.LabelX = "Коэффициент";
        cv.LabelY = "Стандартная ошибка";

        var table = rep.Table("Коэффициенты",
            ["Регрессор", "Оценка", "Ст. ошибка", "t", "p", "Классическая ошибка"],
            [false, true, true, true, true, true]);

        for (int j = 0; j < result.Coefficients.Count; j++)
        {
            Coefficient coefficient = result.Coefficients[j];
            table.Row(coefficient.Name, Num(coefficient.Estimate, 4), Num(coefficient.StandardError, 4),
                Num(coefficient.TStatistic, 2), Num(coefficient.PValue, 4),
                Num(classical.Coefficients[j].StandardError, 4));
        }

        var comparison = rep.Table("Все способы оценки ошибок",
            ["Способ", "Ошибка при a", "t при a", "p при a"], [false, true, true, true]);

        foreach (RobustVariance candidate in Enum.GetValues<RobustVariance>())
        {
            RegressionResult variant = LinearRegression.Fit(x, y, ["a", "b"], new RegressionOptions
            {
                Variance = candidate,
                Clusters = candidate == RobustVariance.Clustered ? clusters : null,
            });

            Coefficient slope = variant.Coefficients[1];
            comparison.Row(VarianceLabel(candidate), Num(slope.StandardError, 4),
                Num(slope.TStatistic, 2), Num(slope.PValue, 4));
        }

        string log =
            $"Оценки коэффициентов одинаковы при любом способе: {Num(result.Coefficients[1].Estimate, 4)} " +
            $"при a.\n" +
            $"Стандартная ошибка меняется с {Num(classical.Coefficients[1].StandardError, 4)} " +
            $"(классическая) до {Num(result.Coefficients[1].StandardError, 4)} " +
            $"({VarianceLabel(variance)}).\n" +
            $"R² = {Num(result.RSquared, 4)}, F = {Num(result.FStatistic, 2)} при p = " +
            $"{Num(result.FPValue, 5)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Переводит номер выбора в значение перечисления способа оценки ошибок.</summary>
    private static int VarianceIndex(int choice) => choice switch
    {
        0 => (int)RobustVariance.Classical,
        1 => (int)RobustVariance.Hc0,
        2 => (int)RobustVariance.Hc1,
        3 => (int)RobustVariance.Hc3,
        4 => (int)RobustVariance.NeweyWest,
        5 => (int)RobustVariance.Clustered,
        _ => (int)RobustVariance.Hc2,
    };

    /// <summary>Читаемое название способа оценки ошибок.</summary>
    private static string VarianceLabel(RobustVariance variance) => variance switch
    {
        RobustVariance.Classical => "классические",
        RobustVariance.Hc0 => "HC0",
        RobustVariance.Hc1 => "HC1",
        RobustVariance.Hc2 => "HC2",
        RobustVariance.Hc3 => "HC3",
        RobustVariance.NeweyWest => "Ньюи — Уэст",
        _ => "кластерные",
    };

    #endregion

    #region Диагностика регрессии

    private static string DoRegressionDiagnostics(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 400);
        double hetero = p.GetValueOrDefault("hetero", 0.8);
        double collinear = p.GetValueOrDefault("collinear", 0.5);
        double nonlinear = p.GetValueOrDefault("nonlinear", 0);
        double breakSize = p.GetValueOrDefault("break_size", 0);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 4));

        var x = new Matrix(n, 3);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            double b = RandomEngine.NextGaussian(rng);

            // Третий регрессор коррелирован с первым с заданной силой
            double c = (collinear * a) + (Math.Sqrt(Math.Max(1 - (collinear * collinear), 0))
                * RandomEngine.NextGaussian(rng));

            x[i, 0] = a;
            x[i, 1] = b;
            x[i, 2] = c;

            double slope = breakSize > 0 && i >= n / 2 ? 2 - breakSize : 2;
            double error = RandomEngine.NextGaussian(rng, 0, 0.5 * Math.Exp(hetero * a / 2));

            y[i] = 1 + (slope * a) - (0.5 * b) + (0.3 * c) + (nonlinear * a * a) + error;
        }

        DiagnosticReport result = Diagnostics.Run(x, y, ["a", "b", "c"], n / 2);
        RegressionResult fit = LinearRegression.Fit(x, y, ["a", "b", "c"]);

        cv.AddPlot(fit.Fitted, fit.Residuals, "Остатки против расчётных значений", C(1), 0);
        Segment(cv, fit.Fitted.Min(), 0, fit.Fitted.Max(), 0, C(5), "Ноль", 1);
        cv.ChartName = $"Провалено тестов: {result.Failed.Count} из {result.Tests.Count}";
        cv.LabelX = "Расчётное значение";
        cv.LabelY = "Остаток";

        var tests = rep.Table("Тесты",
            ["Тест", "Гипотеза", "Статистика", "p", "Вывод"], [false, false, true, true, false]);

        foreach (DiagnosticTest test in result.Tests)
        {
            tests.Row(test.Name, test.NullHypothesis, Num(test.Statistic, 3), Num(test.PValue, 4),
                test.Rejected ? test.Consequence : "гипотеза не отвергается");
        }

        var collinearity = rep.Table("Коллинеарность",
            ["Регрессор", "VIF", "R² на остальные", "Расширение интервала"], [false, true, true, true]);

        foreach (VarianceInflation vif in result.Collinearity)
        {
            collinearity.Row(vif.Variable, Num(vif.Vif, 2), Num(vif.RSquared, 3),
                $"{Num(vif.IntervalInflation, 2)}x");
        }

        string log =
            $"Дарбин — Уотсон {Num(result.DurbinWatson, 3)}.\n" +
            $"Провалено тестов: {result.Failed.Count} из {result.Tests.Count}.\n" +
            $"Максимальный фактор раздувания дисперсии " +
            $"{Num(result.Collinearity.Count > 0 ? result.Collinearity.Max(c => c.Vif) : 1, 2)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Инструментальные переменные

    private static string DoIv2Sls(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 2000);
        double strength = p.GetValueOrDefault("strength", 0.8);
        double endogeneity = p.GetValueOrDefault("endogeneity", 1.5);
        int instrumentCount = (int)p.GetValueOrDefault("instruments", 2);
        double invalid = p.GetValueOrDefault("invalid", 0);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 7));

        var endogenous = new Matrix(n, 1);
        var instruments = new Matrix(n, instrumentCount);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double confounder = RandomEngine.NextGaussian(rng);
            double price = confounder;

            for (int j = 0; j < instrumentCount; j++)
            {
                double instrument = RandomEngine.NextGaussian(rng);
                instruments[i, j] = instrument;
                price += strength * instrument / Math.Sqrt(instrumentCount);
            }

            price += RandomEngine.NextGaussian(rng, 0, 0.3);
            endogenous[i, 0] = price;

            // Нарушение экзогенности: прямое влияние первого инструмента на отклик
            y[i] = 1 - (2 * price) + (endogeneity * confounder)
                + (invalid * instruments[i, 0]) + RandomEngine.NextGaussian(rng, 0, 0.3);
        }

        bool gmm = p.GetValueOrDefault("estimator", 0) > 0.5;

        IvResult result = gmm
            ? InstrumentalVariables.GeneralizedMethodOfMoments(endogenous, null, instruments, y, ["цена"])
            : InstrumentalVariables.TwoStage(endogenous, null, instruments, y, ["цена"]);

        // Зависимость смещения от силы инструмента
        var strengths = new Vector(9);
        var estimates = new Vector(9);

        for (int s = 0; s < 9; s++)
        {
            double level = 0.1 + (s * 0.15);
            strengths[s] = level;

            var testEndogenous = new Matrix(n / 4, 1);
            var testInstruments = new Matrix(n / 4, 1);
            var testY = new Vector(n / 4);
            Random local = RandomEngine.Create(100 + s);

            for (int i = 0; i < n / 4; i++)
            {
                double confounder = RandomEngine.NextGaussian(local);
                double instrument = RandomEngine.NextGaussian(local);
                double price = (level * instrument) + confounder + RandomEngine.NextGaussian(local, 0, 0.3);

                testEndogenous[i, 0] = price;
                testInstruments[i, 0] = instrument;
                testY[i] = 1 - (2 * price) + (endogeneity * confounder)
                    + RandomEngine.NextGaussian(local, 0, 0.3);
            }

            estimates[s] = InstrumentalVariables
                .TwoStage(testEndogenous, null, testInstruments, testY, ["цена"])
                .Coefficients.First(c => c.Name == "цена").Estimate;
        }

        cv.AddPlot(strengths, estimates, "Оценка при разной силе инструмента", C(0), 3);
        Segment(cv, strengths.Min(), -2, strengths.Max(), -2, C(5), "Истинное значение", 2);
        cv.ChartName = $"Инструментальная оценка " +
                       $"{Num(result.Coefficients.First(c => c.Name == "цена").Estimate, 3)} " +
                       $"против МНК " +
                       $"{Num(result.OrdinaryLeastSquares.First(c => c.Name == "цена").Estimate, 3)}";
        cv.LabelX = "Сила инструмента";
        cv.LabelY = "Оценка коэффициента";

        var table = rep.Table("Оценки",
            ["Регрессор", "Инструментальная", "МНК", "Ст. ошибка", "p"], [false, true, true, true, true]);

        foreach (Coefficient coefficient in result.Coefficients)
        {
            Coefficient? ols = result.OrdinaryLeastSquares.FirstOrDefault(c => c.Name == coefficient.Name);
            table.Row(coefficient.Name, Num(coefficient.Estimate, 4),
                ols is not null ? Num(ols.Estimate, 4) : "—",
                Num(coefficient.StandardError, 4), Num(coefficient.PValue, 4));
        }

        var stages = rep.Table("Диагностика",
            ["Показатель", "Значение", "Смысл"], [false, true, false]);

        foreach (FirstStage stage in result.FirstStages)
        {
            stages.Row($"F первой ступени: {stage.Variable}", Num(stage.FStatistic, 1),
                stage.IsWeak ? "инструменты слабые" : "инструменты сильные");
        }

        stages.Row("Хаусман", Num(result.HausmanStatistic, 3),
            result.HausmanPValue < 0.05 ? "эндогенность подтверждается" : "эндогенность не подтверждается");
        stages.Row("Сверхидентификация", Num(result.OveridentificationStatistic, 3),
            result.OveridentifyingRestrictions > 0
                ? $"p = {Num(result.OveridentificationPValue, 4)}"
                : "тест недоступен");

        string log =
            $"Метод: {(gmm ? "обобщённый метод моментов" : "двухшаговый МНК")}.\n" +
            $"Истинный коэффициент -2, инструментальная оценка " +
            $"{Num(result.Coefficients.First(c => c.Name == "цена").Estimate, 4)}, " +
            $"МНК {Num(result.OrdinaryLeastSquares.First(c => c.Name == "цена").Estimate, 4)}.\n" +
            $"Минимальная F первой ступени {Num(result.FirstStages.Min(s => s.FStatistic), 1)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Панельные данные

    private static string DoPanelData(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        PanelDataset dataset = SimulatePanel(p);
        var estimator = (PanelEstimator)(int)p.GetValueOrDefault("estimator", 1);

        PanelResult result = PanelData.Fit(dataset, estimator);

        var estimators = new List<(PanelEstimator Kind, PanelResult Result)>();

        foreach (PanelEstimator candidate in Enum.GetValues<PanelEstimator>())
        {
            try { estimators.Add((candidate, PanelData.Fit(dataset, candidate))); }
            catch (ArgumentException) { /* оценщику не хватило данных */ }
        }

        var index = Axis(estimators.Count, 1);
        cv.AddPlot(index, Vec(estimators.Select(e =>
            e.Result.Coefficients.FirstOrDefault(c => c.Name == "x")?.Estimate ?? 0)),
            "Оценка коэффициента", C(0), 3);
        Segment(cv, 1, 1, estimators.Count, 1, C(5), "Истинное значение", 2);
        cv.ChartName = $"{EstimatorLabel(estimator)}: коэффициент " +
                       $"{Num(result.Coefficients.FirstOrDefault(c => c.Name == "x")?.Estimate ?? 0, 3)}";
        cv.LabelX = "Оценщик";
        cv.LabelY = "Оценка коэффициента";

        var table = rep.Table("Сравнение оценщиков",
            ["Оценщик", "Коэффициент", "Ст. ошибка", "p", "R²"], [false, true, true, true, true]);

        foreach ((PanelEstimator kind, PanelResult item) in estimators)
        {
            Coefficient? slope = item.Coefficients.FirstOrDefault(c => c.Name == "x");
            table.Row(EstimatorLabel(kind),
                slope is not null ? Num(slope.Estimate, 4) : "—",
                slope is not null ? Num(slope.StandardError, 4) : "—",
                slope is not null ? Num(slope.PValue, 4) : "—",
                Num(item.RSquared, 3));
        }

        PanelResult within = PanelData.Fit(dataset, PanelEstimator.FixedEffects);
        PanelResult random = PanelData.Fit(dataset, PanelEstimator.RandomEffects);
        HausmanResult hausman = PanelData.Hausman(within, random);

        var haus = rep.Table("Тест Хаусмана",
            ["Переменная", "Фиксированные", "Случайные", "Разность"], [false, true, true, true]);

        foreach ((string variable, double fixedEstimate, double randomEstimate, double difference)
            in hausman.Differences)
        {
            haus.Row(variable, Num(fixedEstimate, 4), Num(randomEstimate, 4), Num(difference, 4));
        }

        string log =
            $"Панель: {dataset.UnitCount} объектов x {dataset.PeriodCount} периодов.\n" +
            $"Оценщик {EstimatorLabel(estimator)}: коэффициент " +
            $"{Num(result.Coefficients.FirstOrDefault(c => c.Name == "x")?.Estimate ?? 0, 4)} " +
            $"при истинном 1.\n" +
            $"Доля дисперсии эффектов {Pct(result.Rho, 1)}.\n" +
            $"Хаусман: статистика {Num(hausman.Statistic, 3)}, p = {Num(hausman.PValue, 4)}.";

        return Explain(rep, result, log) + "\n\n" + hausman.Interpret().ToLlmText();
    }

    /// <summary>Читаемое название панельного оценщика.</summary>
    private static string EstimatorLabel(PanelEstimator estimator) => estimator switch
    {
        PanelEstimator.Pooled => "объединённый МНК",
        PanelEstimator.FixedEffects => "фиксированные эффекты",
        PanelEstimator.TwoWayFixedEffects => "двусторонние эффекты",
        PanelEstimator.RandomEffects => "случайные эффекты",
        PanelEstimator.FirstDifference => "первые разности",
        _ => "межгрупповая",
    };

    /// <summary>Панель с коррелированными индивидуальными эффектами.</summary>
    private static PanelDataset SimulatePanel(IReadOnlyDictionary<string, double> p)
    {
        int units = (int)p.GetValueOrDefault("units", 60);
        int periods = (int)p.GetValueOrDefault("periods", 8);
        double correlation = p.GetValueOrDefault("correlation", 0.8);
        double effectSd = p.GetValueOrDefault("effect_sd", 2.0);
        double noise = p.GetValueOrDefault("noise", 0.5);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 8));

        int n = units * periods;
        var x = new Matrix(n, 1);
        var y = new Vector(n);
        var unitIds = new List<int>(n);
        var periodIds = new List<int>(n);

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, effectSd);

            for (int t = 0; t < periods; t++)
            {
                int i = (u * periods) + t;

                // Связь регрессора с эффектом и есть источник смещения объединённого МНК
                double value = (correlation * effect) + RandomEngine.NextGaussian(rng);

                x[i, 0] = value;
                y[i] = value + effect + RandomEngine.NextGaussian(rng, 0, noise);

                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        return new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };
    }

    #endregion

    #region Динамические панели

    private static string DoDynamicPanel(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int units = (int)p.GetValueOrDefault("units", 100);
        int periods = (int)p.GetValueOrDefault("periods", 8);
        double persistence = p.GetValueOrDefault("persistence", 0.5);
        double effectSd = p.GetValueOrDefault("effect_sd", 0.5);
        double noise = p.GetValueOrDefault("noise", 0.3);
        int maxLags = (int)p.GetValueOrDefault("max_lags", 3);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 10));

        var xs = new List<double>();
        var ys = new List<double>();
        var unitIds = new List<int>();
        var periodIds = new List<int>();

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, effectSd);
            double level = effect / Math.Max(1 - persistence, 0.05);

            for (int t = 0; t < periods; t++)
            {
                double regressor = RandomEngine.NextGaussian(rng);
                level = (persistence * level) + (0.4 * regressor) + effect
                    + RandomEngine.NextGaussian(rng, 0, noise);

                xs.Add(regressor);
                ys.Add(level);
                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        var x = new Matrix(xs.Count, 1);
        var y = new Vector(ys.Count);

        for (int i = 0; i < xs.Count; i++) { x[i, 0] = xs[i]; y[i] = ys[i]; }

        var dataset = new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };

        DynamicPanelResult result = DynamicPanel.ArellanoBond(dataset, maxLags);

        var labels = new Vector(1.0, 2.0, 3.0, 4.0);
        var values = new Vector(
            result.WithinPersistence, result.Persistence, result.PooledPersistence, persistence);

        cv.AddPlot(labels, values, "Оценки инерции", C(0), 3);
        Segment(cv, 1, persistence, 4, persistence, C(3), "Истинное значение", 2);
        cv.ChartName = $"Ареллано — Бонд: {Num(result.Persistence, 3)} при истинном " +
                       $"{Num(persistence, 2)}";
        cv.LabelX = "1 — внутригрупповая, 2 — динамическая, 3 — объединённая, 4 — истина";
        cv.LabelY = "Коэффициент при лаге отклика";

        var table = rep.Table("Оценки инерции",
            ["Оценщик", "Коэффициент", "Смещение"], [false, true, true]);
        table.Row("Фиксированные эффекты", Num(result.WithinPersistence, 4),
            Num(result.WithinPersistence - persistence, 4));
        table.Row("Ареллано — Бонд", Num(result.Persistence, 4),
            Num(result.Persistence - persistence, 4));
        table.Row("Объединённый МНК", Num(result.PooledPersistence, 4),
            Num(result.PooledPersistence - persistence, 4));

        var coefficients = rep.Table("Коэффициенты модели",
            ["Регрессор", "Оценка", "Ст. ошибка", "p"], [false, true, true, true]);

        foreach (Coefficient coefficient in result.Coefficients)
        {
            coefficients.Row(coefficient.Name, Num(coefficient.Estimate, 4),
                Num(coefficient.StandardError, 4), Num(coefficient.PValue, 4));
        }

        string log =
            $"Истинная инерция {Num(persistence, 3)}.\n" +
            $"Границы: {Num(result.WithinPersistence, 4)} (фиксированные эффекты) и " +
            $"{Num(result.PooledPersistence, 4)} (объединённый МНК).\n" +
            $"Ареллано — Бонд: {Num(result.Persistence, 4)}, инструментов {result.Instruments}.\n" +
            $"Сарган p = {Num(result.SarganPValue, 4)}, AR(2) p = {Num(result.Ar2PValue, 4)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Ограниченные зависимые переменные

    private static string DoLimitedDependent(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var model = (LimitedDependentModel)(int)p.GetValueOrDefault("model", 0);
        int n = (int)p.GetValueOrDefault("n", 2000);
        double beta = p.GetValueOrDefault("beta", 1.2);
        double intercept = p.GetValueOrDefault("intercept", 0.3);
        double dispersion = p.GetValueOrDefault("dispersion", 0.5);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 11));

        var x = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;

            double index = intercept + (beta * value);

            y[i] = model switch
            {
                LimitedDependentModel.Logit =>
                    rng.NextDouble() < 1.0 / (1.0 + Math.Exp(-index)) ? 1 : 0,
                LimitedDependentModel.Probit =>
                    index + RandomEngine.NextGaussian(rng) > 0 ? 1 : 0,
                LimitedDependentModel.Tobit =>
                    Math.Max(index + RandomEngine.NextGaussian(rng), 0),
                LimitedDependentModel.Poisson =>
                    RandomEngine.NextPoisson(rng, Math.Exp(Math.Clamp(index, -5, 5))),
                _ => NegativeBinomialDraw(rng, Math.Exp(Math.Clamp(index, -5, 5)), dispersion),
            };
        }

        LimitedDependentResult result = LimitedDependent.Fit(x, y, model, ["x"]);

        // Расчётная кривая отклика по регрессору
        var grid = new Vector(41);
        var fitted = new Vector(41);

        for (int i = 0; i <= 40; i++)
        {
            double value = -3 + (i * 0.15);
            double index = result.Coefficients[0].Estimate + (result.Coefficients[1].Estimate * value);

            grid[i] = value;
            fitted[i] = model switch
            {
                LimitedDependentModel.Logit => 1.0 / (1.0 + Math.Exp(-index)),
                LimitedDependentModel.Probit => NormalCdf(index),
                LimitedDependentModel.Tobit => Math.Max(index, 0),
                _ => Math.Exp(Math.Clamp(index, -5, 5)),
            };
        }

        cv.AddPlot(Vec(Enumerable.Range(0, n).Select(i => x[i, 0])), y, "Наблюдения", C(1), 0);
        cv.AddPlot(grid, fitted, "Модель", C(0), 3);
        cv.ChartName = $"{ModelLabel(model)}: коэффициент " +
                       $"{Num(result.Coefficients[1].Estimate, 3)} при истинном {Num(beta, 2)}";
        cv.LabelX = "Регрессор";
        cv.LabelY = "Отклик";

        var table = rep.Table("Коэффициенты",
            ["Регрессор", "Оценка", "Ст. ошибка", "t", "p"], [false, true, true, true, true]);

        foreach (Coefficient coefficient in result.Coefficients)
        {
            table.Row(coefficient.Name, Num(coefficient.Estimate, 4), Num(coefficient.StandardError, 4),
                Num(coefficient.TStatistic, 2), Num(coefficient.PValue, 4));
        }

        var effects = rep.Table("Предельные эффекты",
            ["Регрессор", "Средний предельный эффект"], [false, true]);

        foreach ((string variable, double effect) in result.MarginalEffects)
            effects.Row(variable, Num(effect, 5));

        string log =
            $"Модель: {ModelLabel(model)}. Истинный коэффициент {Num(beta, 3)}, оценка " +
            $"{Num(result.Coefficients[1].Estimate, 4)}.\n" +
            $"Псевдо-R² {Num(result.McFaddenRSquared, 4)}, логарифм правдоподобия " +
            $"{Num(result.LogLikelihood, 1)}.\n" +
            (model is LimitedDependentModel.Logit or LimitedDependentModel.Probit
                ? $"Точность классификации {Pct(result.Accuracy, 1)}."
                : model == LimitedDependentModel.Tobit
                    ? $"Цензурировано {Pct(result.CensoredShare, 1)} наблюдений."
                    : $"Параметр сверхдисперсии {Num(result.ScaleParameter, 4)}.");

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название модели ограниченного отклика.</summary>
    private static string ModelLabel(LimitedDependentModel model) => model switch
    {
        LimitedDependentModel.Logit => "логит",
        LimitedDependentModel.Probit => "пробит",
        LimitedDependentModel.Tobit => "тобит",
        LimitedDependentModel.Poisson => "Пуассон",
        _ => "отрицательная биномиальная",
    };

    /// <summary>Выборка из отрицательного биномиального распределения через смесь Пуассона и гаммы.</summary>
    private static double NegativeBinomialDraw(Random rng, double mean, double dispersion)
    {
        if (dispersion <= 1e-9) return RandomEngine.NextPoisson(rng, mean);

        double shape = 1 / dispersion;
        double gamma = RandomEngine.NextGamma(rng, shape, mean / shape);

        return RandomEngine.NextPoisson(rng, Math.Max(gamma, 1e-9));
    }

    /// <summary>Функция стандартного нормального распределения.</summary>
    private static double NormalCdf(double x) => StatInference.NormalCdf(x);

    #endregion

    #region Квантильная регрессия

    private static string DoQuantileRegression(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 500);
        double slope = p.GetValueOrDefault("slope", 1.5);
        double spread = p.GetValueOrDefault("heteroskedasticity", 1.0);
        double outliers = p.GetValueOrDefault("outliers", 0.05);
        int bootstrap = (int)p.GetValueOrDefault("bootstrap", 80);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 14));

        var x = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = rng.NextDouble() * 4;
            x[i, 0] = value;

            // Разброс растёт с регрессором: именно это и разводит квантили
            y[i] = 1 + (slope * value)
                + RandomEngine.NextGaussian(rng, 0, 0.3 * (1 + (spread * value)));

            if (rng.NextDouble() < outliers) y[i] += 30;
        }

        QuantileProcessResult result = QuantileRegression.FitProcess(
            x, y, [0.1, 0.25, 0.5, 0.75, 0.9], ["x"], bootstrap, 42);

        Vector path = result.Path("x");
        var levels = Vec(result.Quantiles.Select(q => q.Quantile));
        double ols = result.LeastSquares.First(c => c.Name == "x").Estimate;

        cv.AddPlot(levels, path, "Коэффициент по квантилям", C(0), 3);
        Segment(cv, levels.Min(), ols, levels.Max(), ols, C(3), $"МНК: {Num(ols, 3)}", 2);
        Segment(cv, levels.Min(), slope, levels.Max(), slope, C(5), $"Истинное: {Num(slope, 2)}", 1);
        cv.ChartName = $"Наклон меняется от {Num(path.Min(), 2)} до {Num(path.Max(), 2)} по квантилям";
        cv.LabelX = "Квантиль";
        cv.LabelY = "Коэффициент при регрессоре";

        var table = rep.Table("Квантильные регрессии",
            ["Квантиль", "Свободный член", "Коэффициент", "Ст. ошибка", "p", "Псевдо-R²"],
            [true, true, true, true, true, true]);

        foreach (QuantileRegressionResult item in result.Quantiles)
        {
            table.Row(Num(item.Quantile, 2), Num(item.Coefficients[0].Estimate, 3),
                Num(item.Coefficients[1].Estimate, 3), Num(item.Coefficients[1].StandardError, 3),
                Num(item.Coefficients[1].PValue, 4), Num(item.PseudoRSquared, 3));
        }

        var comparison = rep.Table("Сравнение с МНК",
            ["Метод", "Свободный член", "Коэффициент"], [false, true, true]);
        comparison.Row("МНК", Num(result.LeastSquares[0].Estimate, 3), Num(ols, 3));

        QuantileRegressionResult median = result.Quantiles.First(q => Math.Abs(q.Quantile - 0.5) < 1e-9);
        comparison.Row("Медианная регрессия", Num(median.Coefficients[0].Estimate, 3),
            Num(median.Coefficients[1].Estimate, 3));

        string log =
            $"Коэффициент меняется от {Num(path[0], 3)} в нижнем квантиле до " +
            $"{Num(path[^1], 3)} в верхнем.\n" +
            $"МНК даёт {Num(ols, 3)}, медианная регрессия {Num(median.Coefficients[1].Estimate, 3)}.\n" +
            $"Доля выбросов в данных {Pct(outliers, 0)} — они смещают МНК, но не медиану.";

        return Explain(rep, result, log);
    }

    #endregion
}

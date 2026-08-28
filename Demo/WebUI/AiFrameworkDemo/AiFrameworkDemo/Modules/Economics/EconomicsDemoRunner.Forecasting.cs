using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Forecasting;
using AI.Statistics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    /// <summary>Ряд спроса: тренд, сезонность, шум и опциональные выбросы.</summary>
    private static Vector DemandSeries(
        int n, int period, double amplitude, double slope, double noise, double outlierRate, int seed)
    {
        Random rng = RandomEngine.Create(seed);
        var series = new Vector(n);
        double level = 1000;

        for (int t = 0; t < n; t++)
        {
            double value = level + (slope * t)
                         + (amplitude * Math.Sin(2 * Math.PI * t / Math.Max(period, 1)))
                         + RandomEngine.NextGaussian(rng, 0, noise);

            // Промоакции дают редкие всплески, которые ломают наивные модели
            if (outlierRate > 0 && rng.NextDouble() < outlierRate) value *= 1.6;

            series[t] = Math.Max(value, 1);
        }

        return series;
    }

    /// <summary>Рисует историю, прогноз и коридор одним набором серий.</summary>
    private static void PlotForecast(ChartView cv, Vector history, ForecastResult forecast)
    {
        cv.AddPlot(Axis(history.Count), history, "История", C(1), 2);

        Vector axis = Axis(forecast.Horizon, history.Count);
        cv.AddPlot(axis, forecast.PointForecast, "Прогноз", C(0), 3);
        cv.AddPlot(axis, forecast.Upper, "Верхняя граница", C(4), 1);
        cv.AddPlot(axis, forecast.Lower, "Нижняя граница", C(4), 1);
        Segment(cv, history.Count - 1, history.Min(), history.Count - 1, history.Max(), C(6),
            "Конец истории", 1);
    }

    private static string DoArima(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 156);
        int period = I(p, "period", 52);
        int horizon = I(p, "horizon", 26);
        double amplitude = N(p, "amplitude", 150);
        double slope = N(p, "slope", 2.0);
        double noise = N(p, "noise", 40);
        bool auto = I(p, "auto", 1) == 1;

        Vector series = DemandSeries(n, period, amplitude, slope, noise, 0, I(p, "seed", 7));

        ForecastResult result = auto
            ? Arima.AutoFit(series, horizon, period <= 1 ? 1 : period)
            : new Arima().Fit(series, new ArimaOrder
            {
                P = I(p, "ar", 1),
                D = I(p, "diff", 1),
                Q = I(p, "ma", 1),
                Season = period <= 1 ? 1 : period,
            }, horizon);

        PlotForecast(cv, series, result);
        cv.ChartName = $"{result.Model}: прогноз на {horizon} периодов";
        cv.LabelX = "Период";
        cv.LabelY = "Спрос";

        var table = rep.Table("Прогноз по периодам",
            ["Период", "Прогноз", "Нижняя граница", "Верхняя граница"], [true, true, true, true]);

        int step = Math.Max(1, horizon / 10);
        for (int h = 0; h < horizon; h += step)
            table.Row((n + h + 1).ToString(), Num(result.PointForecast[h], 0),
                Num(result.Lower[h], 0), Num(result.Upper[h], 0));

        return Explain(rep, result, $"Наблюдений: {n}, сезонный период: {period}");
    }

    private static string DoEts(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 120);
        int period = I(p, "period", 12);
        int horizon = I(p, "horizon", 24);
        double amplitude = N(p, "amplitude", 150);
        double slope = N(p, "slope", 4.0);
        double noise = N(p, "noise", 30);
        int seasonality = I(p, "seasonality", 1);
        bool damped = I(p, "damped", 1) == 1;

        Vector series = DemandSeries(n, period, amplitude, slope, noise, 0, I(p, "seed", 3));

        ForecastResult result = seasonality == 3
            ? ExponentialSmoothing.AutoFit(series, horizon, period)
            : new ExponentialSmoothing().Fit(series, horizon, period,
                (SeasonalityType)Math.Clamp(seasonality, 0, 2), damped, withTrend: true);

        PlotForecast(cv, series, result);

        // Недемпфированный тренд для сравнения: видно, куда он уходит
        if (damped)
        {
            ForecastResult plain = new ExponentialSmoothing().Fit(
                series, horizon, period, (SeasonalityType)Math.Clamp(seasonality == 3 ? 1 : seasonality, 0, 2),
                damped: false, withTrend: true);
            cv.AddPlot(Axis(horizon, n), plain.PointForecast, "Без затухания тренда", C(3), 2);
        }

        cv.ChartName = $"{result.Model}: прогноз на {horizon} периодов";
        cv.LabelX = "Период";
        cv.LabelY = "Спрос";

        var table = rep.Table("Параметры сглаживания",
            ["Параметр", "Значение", "Смысл"], [false, true, false]);

        foreach ((string name, double value) in result.Parameters)
        {
            table.Row(name, Num(value, 4), name switch
            {
                "alpha" => "вес свежего наблюдения при обновлении уровня",
                "beta" => "вес свежего наблюдения при обновлении тренда",
                "gamma" => "вес свежего наблюдения при обновлении сезонности",
                "phi" => "затухание тренда: ниже единицы — прогноз конечен",
                _ => "",
            });
        }

        return Explain(rep, result, $"Наблюдений: {n}, сезонный период: {period}");
    }

    private static string DoTheta(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 96);
        int period = I(p, "period", 12);
        int horizon = I(p, "horizon", 18);
        double amplitude = N(p, "amplitude", 120);
        double slope = N(p, "slope", 3.0);
        double noise = N(p, "noise", 40);

        Vector series = DemandSeries(n, period, amplitude, slope, noise, 0, I(p, "seed", 23));

        ForecastResult theta = ThetaMethod.Fit(series, horizon, period);
        ForecastResult ets = ExponentialSmoothing.AutoFit(series, horizon, period);
        Vector naive = ForecastMetrics.Naive(series, horizon, period);

        PlotForecast(cv, series, theta);
        cv.AddPlot(Axis(horizon, n), ets.PointForecast, "Экспоненциальное сглаживание", C(2), 2);
        cv.AddPlot(Axis(horizon, n), naive, "Сезонный наивный прогноз", C(3), 2);

        cv.ChartName = $"{theta.Model} против сглаживания и наивного прогноза";
        cv.LabelX = "Период";
        cv.LabelY = "Спрос";

        rep.Table("Сравнение на обучающей выборке",
            ["Модель", "MASE", "Сигма"], [false, true, true],
            note: "Честное сравнение даёт бэктест со скользящим началом, здесь — только посадка.")
           .Row(theta.Model, Num(theta.InSampleMase, 3), Num(theta.Sigma, 1))
           .Row(ets.Model, Num(ets.InSampleMase, 3), Num(ets.Sigma, 1));

        return Explain(rep, theta, $"Наблюдений: {n}, сезонный период: {period}");
    }

    private static string DoStl(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 156);
        int period = I(p, "period", 52);
        double amplitude = N(p, "amplitude", 200);
        double slope = N(p, "slope", 2.0);
        double noise = N(p, "noise", 40);
        double outliers = N(p, "outliers", 0.03);
        int robust = I(p, "robust", 1);

        Vector series = DemandSeries(n, period, amplitude, slope, noise, outliers, I(p, "seed", 13));
        StlResult result = StlDecomposition.Decompose(series, period, robustIterations: robust);

        Vector axis = Axis(n);
        cv.AddPlot(axis, series, "Исходный ряд", C(1), 2);
        cv.AddPlot(axis, result.Trend, "Тренд", C(0), 3);
        cv.AddPlot(axis, result.SeasonallyAdjusted, "Без сезонности", C(2), 2);
        cv.AddPlot(axis, result.Seasonal, "Сезонная составляющая", C(4), 2);
        cv.AddPlot(axis, result.Remainder, "Остаток", C(3), 1);

        cv.ChartName = $"STL: сила тренда {Num(result.TrendStrength)}, сила сезонности {Num(result.SeasonalStrength)}";
        cv.LabelX = "Период";
        cv.LabelY = "Значение";

        if (result.Outliers.Count > 0)
        {
            var table = rep.Table("Обнаруженные выбросы",
                ["Период", "Значение", "Остаток"], [true, true, true],
                note: "Проверьте, не промоакции ли это: их стоит вынести в отдельный регрессор.");

            foreach (int index in result.Outliers.Take(12))
                table.Row(index.ToString(), Num(series[index], 0), Num(result.Remainder[index], 0));
        }

        return Explain(rep, result, $"Наблюдений: {n}, сезонный период: {period}");
    }

    private static string DoIntermittent(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 120);
        double probability = N(p, "probability", 0.25);
        double size = N(p, "size", 12);
        double alpha = N(p, "alpha", 0.1);
        double leadTime = N(p, "lead_time", 4);
        double service = N(p, "service", 0.95);
        int method = I(p, "method", 1);
        Random rng = RandomEngine.Create(I(p, "seed", 3));

        var series = new Vector(n);
        for (int t = 0; t < n; t++)
            series[t] = rng.NextDouble() < probability ? Math.Round(size * (0.5 + rng.NextDouble())) : 0;

        IReadOnlyList<IntermittentForecast> all =
            IntermittentDemand.CompareAll(series, 12, alpha, leadTime, service);

        IntermittentForecast chosen =
            all.FirstOrDefault(r => (int)r.Method == method) ?? all[0];

        cv.AddBar(Axis(n), series, "Фактический спрос", C(1));

        foreach ((IntermittentForecast forecast, int index) in all.Select((f, i) => (f, i)))
        {
            var level = new Vector(n);
            for (int t = 0; t < n; t++) level[t] = forecast.DemandPerPeriod;
            cv.AddPlot(Axis(n), level, $"{MethodName(forecast.Method)}: {Num(forecast.DemandPerPeriod, 2)}",
                C(index), 2);
        }

        var reorder = new Vector(n);
        for (int t = 0; t < n; t++) reorder[t] = chosen.ReorderPoint;
        cv.AddPlot(Axis(n), reorder, "Точка перезаказа", C(3), 3);

        cv.ChartName = $"Прерывистый спрос: {Pct(chosen.ZeroShare)} периодов без продаж";
        cv.LabelX = "Период";
        cv.LabelY = "Спрос";

        var table = rep.Table("Сравнение методов",
            ["Метод", "Спрос за период", "MASE", "Точка перезаказа"], [false, true, true, true]);

        foreach (IntermittentForecast forecast in all)
            table.Row(MethodName(forecast.Method), Num(forecast.DemandPerPeriod, 3),
                Num(forecast.InSampleMase, 3), Num(forecast.ReorderPoint, 1));

        var log = new StringBuilder();
        log.AppendLine($"Тип ряда: {chosen.DemandPattern}");
        log.AppendLine($"Средний интервал между заказами: {Num(chosen.AverageInterval, 2)} периодов");
        log.AppendLine($"Средний размер заказа: {Num(chosen.AverageDemandSize, 1)}");

        return Explain(rep, chosen, log.ToString());
    }

    private static string MethodName(IntermittentMethod method) => method switch
    {
        IntermittentMethod.Croston => "Кростон",
        IntermittentMethod.SyntetosBoylan => "Синтетос — Бойлан",
        _ => "TSB",
    };

    private static string DoHierarchical(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int groups = I(p, "groups", 3);
        int perGroup = I(p, "per_group", 3);
        double disagreement = N(p, "disagreement", 0.12);
        int method = I(p, "method", 3);
        Random rng = RandomEngine.Create(I(p, "seed", 5));

        int[] sizes = [.. Enumerable.Repeat(perGroup, groups)];
        (Matrix summing, IReadOnlyList<HierarchyNode> nodes) =
            HierarchicalReconciliation.BuildTwoLevel(sizes);

        // Базовые прогнозы строятся независимо на каждом уровне, поэтому
        // расходятся между собой — ровно как в реальной компании
        int bottom = sizes.Sum();
        var truth = new double[bottom];
        for (int b = 0; b < bottom; b++) truth[b] = 100 + (rng.NextDouble() * 100);

        const int horizon = 3;
        var baseForecasts = new Matrix(nodes.Count, horizon);

        for (int h = 0; h < horizon; h++)
        {
            int row = 0;
            double total = truth.Sum() * (1 + (disagreement * RandomEngine.NextGaussian(rng)));
            baseForecasts[row++, h] = total;

            int offset = 0;
            for (int g = 0; g < groups; g++)
            {
                double groupTotal = 0;
                for (int b = 0; b < sizes[g]; b++) groupTotal += truth[offset + b];
                baseForecasts[row++, h] = groupTotal * (1 + (disagreement * RandomEngine.NextGaussian(rng)));
                offset += sizes[g];
            }

            for (int b = 0; b < bottom; b++)
                baseForecasts[row++, h] = truth[b] * (1 + (disagreement * RandomEngine.NextGaussian(rng)));
        }

        var variances = new Vector(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
            variances[i] = nodes[i].IsBottom ? 1.0 : nodes[i].Level == 0 ? 4.0 : 2.0;

        ReconciliationResult result = HierarchicalReconciliation.Reconcile(
            nodes, summing, baseForecasts, (ReconciliationMethod)Math.Clamp(method, 0, 3), variances);

        Vector axis = Axis(nodes.Count, 1);
        cv.AddBar(axis, Vec(Enumerable.Range(0, nodes.Count).Select(i => baseForecasts[i, 0])),
            "Независимый прогноз", C(1));
        cv.AddBar(axis, Vec(Enumerable.Range(0, nodes.Count).Select(i => result.ReconciledForecasts[i, 0])),
            "Согласованный прогноз", C(0));

        cv.ChartName = "Узлы иерархии: " + string.Join(" · ", nodes.Select((nd, i) => $"{i + 1}. {nd.Name}"));
        cv.LabelX = "Узел иерархии";
        cv.LabelY = "Прогноз на первый период";

        var table = rep.Table("Согласование по узлам",
            ["Узел", "Уровень", "Было", "Стало", "Сдвиг"], [false, true, true, true, true]);

        for (int i = 0; i < nodes.Count; i++)
        {
            double before = baseForecasts[i, 0];
            double after = result.ReconciledForecasts[i, 0];
            table.Row(nodes[i].Name, nodes[i].Level.ToString(), Num(before, 1), Num(after, 1),
                Pct(Math.Abs(before) > 1e-9 ? (after - before) / before : 0));
        }

        return Explain(rep, result,
            $"Узлов: {nodes.Count}, из них листьев: {nodes.Count(nd => nd.IsBottom)}");
    }

    private static string DoBacktest(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 150);
        int period = I(p, "period", 12);
        int horizon = I(p, "horizon", 6);
        int folds = I(p, "folds", 6);
        double amplitude = N(p, "amplitude", 150);
        double slope = N(p, "slope", 3.0);
        double noise = N(p, "noise", 40);

        Vector series = DemandSeries(n, period, amplitude, slope, noise, 0, I(p, "seed", 21));

        BacktestResult result = ForecastBacktest.Run(
            series,
            [
                ("Сглаживание", (train, h) => ExponentialSmoothing.AutoFit(train, h, period)),
                ("Theta", (train, h) => ThetaMethod.Fit(train, h, period)),
                ("ARIMA", (train, h) => Arima.AutoFit(train, h, period, maxOrder: 1)),
            ],
            horizon, folds, seasonalPeriod: period);

        Vector axis = Axis(horizon, 1);
        for (int i = 0; i < result.Models.Count; i++)
            cv.AddPlot(axis, Finite(result.Models[i].MaeByHorizon), result.Models[i].Model, C(i), 3);

        cv.AddPlot(axis, Finite(result.Naive.MaeByHorizon), "Наивный прогноз", C(3), 2);

        cv.ChartName = "Ошибка растёт с горизонтом: сравнение моделей на скользящем начале";
        cv.LabelX = "Шаг горизонта";
        cv.LabelY = "Средняя абсолютная ошибка";

        var table = rep.Table("Результаты бэктеста",
            ["Модель", "MASE", "sMAPE", "RMSE", "Покрытие интервалов", "Срезов"],
            [false, true, true, true, true, true]);

        foreach (BacktestSummary summary in result.Models)
            table.Row(summary.Model, Num(summary.Mase, 3), Pct(summary.SMape),
                Num(summary.Rmse, 1),
                double.IsNaN(summary.Coverage) ? "—" : Pct(summary.Coverage),
                summary.Folds.ToString());

        table.Row(result.Naive.Model, Num(result.Naive.Mase, 3), Pct(result.Naive.SMape),
            Num(result.Naive.Rmse, 1), "—", result.Naive.Folds.ToString());

        return Explain(rep, result, $"Наблюдений: {n}, горизонт: {horizon}, срезов: {folds}");
    }

    private static string DoConformal(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = I(p, "n", 180);
        int period = I(p, "period", 12);
        int horizon = I(p, "horizon", 8);
        double level = N(p, "level", 0.9);
        int calibration = I(p, "calibration", 30);
        double noise = N(p, "noise", 45);
        int model = I(p, "model", 0);

        Vector series = DemandSeries(n, period, 150, 3.0, noise, 0.02, I(p, "seed", 41));

        Func<Vector, int, ForecastResult> forecaster = model == 1
            ? (train, h) => ThetaMethod.Fit(train, h, period)
            : (train, h) => ExponentialSmoothing.AutoFit(train, h, period);

        ConformalForecast result = ConformalPrediction.Calibrate(
            series, forecaster, horizon, level, calibration, [0.05, 0.5, 0.95]);

        ForecastResult model0 = forecaster(series, horizon);

        cv.AddPlot(Axis(n), series, "История", C(1), 2);

        Vector axis = Axis(horizon, n);
        cv.AddPlot(axis, result.PointForecast, "Прогноз", C(0), 3);
        cv.AddPlot(axis, result.Upper, "Конформная верхняя граница", C(2), 2);
        cv.AddPlot(axis, result.Lower, "Конформная нижняя граница", C(2), 2);
        cv.AddPlot(axis, model0.Upper, "Верхняя граница модели", C(3), 1);
        cv.AddPlot(axis, model0.Lower, "Нижняя граница модели", C(3), 1);

        cv.ChartName = $"Конформные интервалы: покрытие {Pct(result.CalibrationCoverage)} " +
                       $"против {Pct(result.ModelCoverage)} у модели";
        cv.LabelX = "Период";
        cv.LabelY = "Спрос";

        var table = rep.Table("Квантильный прогноз",
            ["Шаг", "5 %", "Медиана", "95 %", "Ширина интервала"],
            [true, true, true, true, true],
            note: "Для планирования запаса берут нижний квантиль, а не точечный прогноз.");

        for (int h = 0; h < horizon; h++)
            table.Row((h + 1).ToString(),
                Num(result.Quantiles[0.05][h], 0), Num(result.Quantiles[0.5][h], 0),
                Num(result.Quantiles[0.95][h], 0), Num(result.Width[h], 0));

        return Explain(rep, result,
            $"Калибровочных срезов: {result.CalibrationSize}, заявленный уровень {Pct(level, 0)}");
    }
}

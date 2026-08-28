using AI.DataStructs.Algebraic;
using AI.Economics.Forecasting;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

public class ForecastingTests
{
    /// <summary>Ряд с трендом, сезонностью и шумом.</summary>
    private static Vector SeasonalSeries(int n, int period, double amplitude, double slope, int seed)
    {
        var rng = RandomEngine.Create(seed);
        var series = new Vector(n);

        for (int t = 0; t < n; t++)
        {
            series[t] = 100 + (slope * t)
                      + (amplitude * Math.Sin(2 * Math.PI * t / period))
                      + RandomEngine.NextGaussian(rng, 0, 2);
        }

        return series;
    }

    [Fact]
    public void Arima_FitsAutoRegressiveProcess()
    {
        var rng = RandomEngine.Create(19);
        const int n = 400;
        var series = new Vector(n);
        double previous = 0;

        for (int t = 0; t < n; t++)
        {
            previous = (0.7 * previous) + RandomEngine.NextGaussian(rng, 0, 1);
            series[t] = 50 + previous;
        }

        var order = new ArimaOrder { P = 1, D = 0, Q = 0 };
        ForecastResult result = new Arima().Fit(series, order, horizon: 10);

        Assert.Equal(10, result.Horizon);
        Assert.InRange(result.Parameters["ar1"], 0.5, 0.9);
        Assert.True(result.Sigma > 0);

        // Интервалы расширяются с горизонтом
        Assert.True(result.Upper[9] - result.Lower[9] > result.Upper[0] - result.Lower[0]);
    }

    [Fact]
    public void Arima_ForecastStaysInReasonableRange()
    {
        Vector series = SeasonalSeries(200, 12, 10, 0.2, 7);
        ForecastResult result = Arima.AutoFit(series, horizon: 12, season: 12);

        double last = series[series.Count - 1];
        for (int h = 0; h < result.Horizon; h++)
            Assert.InRange(result.PointForecast[h], last - 80, last + 80);

        Assert.NotEmpty(result.Interpret().Metrics);
    }

    [Fact]
    public void ExponentialSmoothing_RecoversSeasonalPattern()
    {
        Vector series = SeasonalSeries(120, 12, 20, 0.5, 3);

        ForecastResult result = new ExponentialSmoothing()
            .Fit(series, horizon: 24, season: 12, SeasonalityType.Additive, damped: true, withTrend: true);

        Assert.Equal(12, result.SeasonalPeriod);
        Assert.True(result.InSampleMase < 1.0,
            $"MASE {result.InSampleMase:F3} — модель обязана превзойти наивный прогноз.");

        // Сезонная волна должна воспроизводиться в прогнозе
        double range = result.PointForecast.Max() - result.PointForecast.Min();
        Assert.True(range > 20, $"Размах прогноза {range:F1} слишком мал для амплитуды 20.");
    }

    [Fact]
    public void ExponentialSmoothing_DampedTrendDoesNotExplode()
    {
        Vector series = SeasonalSeries(60, 1, 0, 2.0, 11);

        ForecastResult damped = new ExponentialSmoothing()
            .Fit(series, horizon: 60, season: 1, SeasonalityType.None, damped: true, withTrend: true);
        ForecastResult plain = new ExponentialSmoothing()
            .Fit(series, horizon: 60, season: 1, SeasonalityType.None, damped: false, withTrend: true);

        Assert.True(damped.PointForecast[59] <= plain.PointForecast[59] + 1e-6,
            "Затухающий тренд не может уйти выше недемпфированного.");
        Assert.InRange(damped.Parameters["phi"], 0.79, 1.0);
    }

    [Fact]
    public void ExponentialSmoothing_AutoFitPicksSeasonalModel()
    {
        Vector series = SeasonalSeries(120, 12, 25, 0.3, 5);
        ForecastResult result = ExponentialSmoothing.AutoFit(series, horizon: 12, season: 12);

        Assert.Equal(12, result.SeasonalPeriod);
        Assert.Contains("Уинтерс", result.Model);
    }

    [Fact]
    public void Theta_BeatsNaiveOnTrendingSeries()
    {
        Vector series = SeasonalSeries(80, 1, 0, 1.5, 23);
        ForecastResult result = ThetaMethod.Fit(series, horizon: 12);

        Assert.True(result.InSampleMase < 1.2);
        Assert.True(result.PointForecast[11] > result.PointForecast[0],
            "На растущем ряде прогноз обязан расти.");
        Assert.Equal("Theta", result.Model);
    }

    [Fact]
    public void Theta_DetectsAndRemovesSeasonality()
    {
        Vector series = SeasonalSeries(120, 12, 30, 0.4, 31);
        ForecastResult result = ThetaMethod.Fit(series, horizon: 24, season: 12);

        Assert.Equal(12, result.SeasonalPeriod);
        Assert.Contains("сезонной", result.Model);

        double range = result.PointForecast.Max() - result.PointForecast.Min();
        Assert.True(range > 25, $"Размах {range:F1} не воспроизводит сезонность.");
    }

    [Fact]
    public void Stl_SeparatesTrendAndSeasonality()
    {
        Vector series = SeasonalSeries(156, 52, 30, 0.3, 13);
        StlResult result = StlDecomposition.Decompose(series, period: 52);

        Assert.Equal(series.Count, result.Trend.Count);
        Assert.True(result.SeasonalStrength > 0.4,
            $"Сила сезонности {result.SeasonalStrength:F2} слишком мала для явно сезонного ряда.");
        Assert.True(result.TrendStrength > 0.4);

        // Составляющие складываются в исходный ряд
        for (int t = 0; t < series.Count; t++)
            Assert.Equal(series[t], result.Trend[t] + result.Seasonal[t] + result.Remainder[t], 6);
    }

    [Fact]
    public void Stl_TrendFollowsSlope()
    {
        Vector series = SeasonalSeries(156, 52, 20, 0.5, 17);
        StlResult result = StlDecomposition.Decompose(series, period: 52);

        double change = result.Trend[result.Trend.Count - 1] - result.Trend[0];
        Assert.InRange(change, 0.5 * 155 * 0.6, 0.5 * 155 * 1.4);
        Assert.NotEmpty(result.Interpret().Findings);
    }

    /// <summary>Прерывистый ряд: продажи раз в несколько периодов.</summary>
    private static Vector IntermittentSeries(int n, double probability, double size, int seed)
    {
        var rng = RandomEngine.Create(seed);
        var series = new Vector(n);

        for (int t = 0; t < n; t++)
            series[t] = rng.NextDouble() < probability ? Math.Round(size * (0.5 + rng.NextDouble())) : 0;

        return series;
    }

    [Fact]
    public void Intermittent_ClassifiesDemandPattern()
    {
        Vector series = IntermittentSeries(120, 0.25, 10, 3);
        IntermittentForecast result = IntermittentDemand.Fit(series, IntermittentMethod.SyntetosBoylan);

        Assert.True(result.ZeroShare > 0.6);
        Assert.Contains(result.DemandPattern, new[] { "прерывистый", "комковатый" });
        Assert.True(result.AverageInterval > 2);
        Assert.True(result.DemandPerPeriod > 0);
    }

    [Fact]
    public void Intermittent_SyntetosBoylanIsBelowCroston()
    {
        Vector series = IntermittentSeries(120, 0.25, 10, 3);

        IntermittentForecast croston = IntermittentDemand.Fit(series, IntermittentMethod.Croston);
        IntermittentForecast sba = IntermittentDemand.Fit(series, IntermittentMethod.SyntetosBoylan);

        Assert.True(sba.DemandPerPeriod < croston.DemandPerPeriod,
            "Поправка Синтетоса — Бойлана обязана снижать смещённую вверх оценку Кростона.");
    }

    [Fact]
    public void Intermittent_ReorderPointCoversLeadTimeDemand()
    {
        Vector series = IntermittentSeries(120, 0.3, 12, 9);
        IntermittentForecast result = IntermittentDemand.Fit(
            series, IntermittentMethod.TeunterSyntetosBabai, leadTime: 4, serviceLevel: 0.95);

        Assert.True(result.ReorderPoint > result.DemandPerPeriod * 4,
            "Точка перезаказа обязана превышать средний спрос за срок поставки на страховой запас.");
        Assert.True(result.SafetyStock > 0);
        Assert.NotEmpty(result.Interpret().Warnings);
    }

    [Fact]
    public void Intermittent_CompareAllRanksByError()
    {
        Vector series = IntermittentSeries(120, 0.2, 8, 15);
        IReadOnlyList<IntermittentForecast> all = IntermittentDemand.CompareAll(series);

        Assert.Equal(3, all.Count);
        for (int i = 1; i < all.Count; i++)
            Assert.True(all[i].InSampleMase >= all[i - 1].InSampleMase);
    }

    [Fact]
    public void Hierarchy_ReconciliationMakesForecastsCoherent()
    {
        (Matrix summing, IReadOnlyList<HierarchyNode> nodes) =
            HierarchicalReconciliation.BuildTwoLevel([2, 3]);

        // Несогласованные прогнозы: сумма листьев не сходится с агрегатами
        var baseForecasts = new Matrix(nodes.Count, 2);
        double[] values = [1000, 400, 700, 180, 210, 220, 230, 240];
        for (int i = 0; i < nodes.Count; i++)
            for (int h = 0; h < 2; h++) baseForecasts[i, h] = values[i] * (1 + (0.05 * h));

        ReconciliationResult result = HierarchicalReconciliation.Reconcile(
            nodes, summing, baseForecasts, ReconciliationMethod.MinTraceDiagonal);

        Assert.True(result.MaxIncoherenceBefore > 1,
            "Исходные прогнозы обязаны быть несогласованными.");
        Assert.True(result.MaxIncoherenceAfter < 1e-6,
            $"После согласования расхождение {result.MaxIncoherenceAfter:E2} должно исчезнуть.");
    }

    [Fact]
    public void Hierarchy_BottomUpKeepsLeafForecasts()
    {
        (Matrix summing, IReadOnlyList<HierarchyNode> nodes) =
            HierarchicalReconciliation.BuildTwoLevel([2, 2]);

        var baseForecasts = new Matrix(nodes.Count, 1);
        double[] values = [900, 300, 500, 140, 150, 240, 250];
        for (int i = 0; i < nodes.Count; i++) baseForecasts[i, 0] = values[i];

        ReconciliationResult result = HierarchicalReconciliation.Reconcile(
            nodes, summing, baseForecasts, ReconciliationMethod.BottomUp);

        // Листья не меняются, агрегаты пересчитываются
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].IsBottom)
                Assert.Equal(baseForecasts[i, 0], result.ReconciledForecasts[i, 0], 6);

        Assert.Equal(140 + 150 + 240 + 250, result.ReconciledForecasts[0, 0], 6);
        Assert.NotEmpty(result.Interpret().Findings);
    }

    [Fact]
    public void Backtest_ComparesModelsAgainstNaive()
    {
        Vector series = SeasonalSeries(150, 12, 20, 0.4, 21);

        BacktestResult result = ForecastBacktest.Run(
            series,
            [
                ("ETS", (train, h) => ExponentialSmoothing.AutoFit(train, h, 12)),
                ("Theta", (train, h) => ThetaMethod.Fit(train, h, 12)),
            ],
            horizon: 6, folds: 5, seasonalPeriod: 12);

        Assert.Equal(2, result.Models.Count);
        Assert.True(result.Models[0].Mase <= result.Models[1].Mase);
        Assert.True(result.Naive.Folds > 0);
        Assert.True(result.Models[0].MaeByHorizon.Count == 6);
        Assert.NotEmpty(result.Interpret().Recommendations);
    }

    [Fact]
    public void Backtest_MetricsAreConsistent()
    {
        var actual = new Vector(100.0, 110, 120);
        var forecast = new Vector(105.0, 105, 130);
        var train = new Vector(90.0, 95, 100, 105, 110);

        Assert.Equal(20.0 / 3.0, ForecastMetrics.Mae(actual, forecast), 6);
        Assert.Equal(Math.Sqrt((25 + 25 + 100) / 3.0), ForecastMetrics.Rmse(actual, forecast), 6);
        Assert.True(ForecastMetrics.SMape(actual, forecast) > 0);

        // Наивный прогноз на обучении ошибается ровно на 5 за шаг,
        // модель — на 20/3, отсюда MASE = 4/3
        Assert.Equal(4.0 / 3.0, ForecastMetrics.Mase(actual, forecast, train), 6);
    }

    [Fact]
    public void PinballLoss_PenalisesAsymmetrically()
    {
        var actual = new Vector(100.0);
        var below = new Vector(90.0);
        var above = new Vector(110.0);

        // Для верхнего квантиля занижение штрафуется сильнее
        Assert.True(ForecastMetrics.PinballLoss(actual, below, 0.95)
                  > ForecastMetrics.PinballLoss(actual, above, 0.95));

        // Для нижнего — наоборот
        Assert.True(ForecastMetrics.PinballLoss(actual, above, 0.05)
                  > ForecastMetrics.PinballLoss(actual, below, 0.05));
    }

    [Fact]
    public void Conformal_AchievesRequestedCoverage()
    {
        Vector series = SeasonalSeries(200, 12, 15, 0.3, 41);

        ConformalForecast result = ConformalPrediction.Calibrate(
            series,
            (train, h) => ExponentialSmoothing.AutoFit(train, h, 12),
            horizon: 6,
            confidenceLevel: 0.9,
            calibrationFolds: 30,
            quantiles: [0.05, 0.5, 0.95]);

        Assert.InRange(result.CalibrationCoverage, 0.85, 1.0);
        Assert.Equal(3, result.Quantiles.Count);
        Assert.Equal(6, result.PointForecast.Count);

        // Интервал вложен и упорядочен
        for (int h = 0; h < 6; h++)
        {
            Assert.True(result.Lower[h] <= result.PointForecast[h]);
            Assert.True(result.PointForecast[h] <= result.Upper[h]);
            Assert.True(result.Quantiles[0.05][h] <= result.Quantiles[0.95][h]);
        }
    }

    [Fact]
    public void Conformal_WidthGrowsWithHorizon()
    {
        Vector series = SeasonalSeries(180, 12, 10, 0.5, 55);

        ConformalForecast result = ConformalPrediction.Calibrate(
            series, (train, h) => ThetaMethod.Fit(train, h, 12), horizon: 8, calibrationFolds: 25);

        Assert.True(result.Width[7] > result.Width[0],
            $"Ширина на дальнем горизонте {result.Width[7]:F2} должна превышать ближний {result.Width[0]:F2}.");
        Assert.True(result.HorizonAware);
        Assert.NotEmpty(result.Interpret().Recommendations);
    }

    [Fact]
    public void Calendar_BuildsHolidayAndFourierFeatures()
    {
        CalendarMatrix calendar = CalendarFeatures.Build(
            new DateTime(2024, 1, 1), periods: 52, step: TimeSpan.FromDays(7),
            fourierTerms: 2, fourierPeriod: 52,
            holidays: CalendarFeatures.RussianHolidays(2024, 2024), holidayWindow: 1);

        Assert.Equal(52, calendar.Features.Height);
        Assert.Contains("sin_1", calendar.Names);
        Assert.Contains("holiday", calendar.Names);
        Assert.Contains("before_holiday_1", calendar.Names);
        Assert.Equal(calendar.Names.Count, calendar.Features.Width);

        // Первая неделя года попадает на новогодние каникулы
        int holidayColumn = calendar.Names.ToList().IndexOf("holiday");
        Assert.Equal(1.0, calendar.Features[0, holidayColumn], 6);
    }
}

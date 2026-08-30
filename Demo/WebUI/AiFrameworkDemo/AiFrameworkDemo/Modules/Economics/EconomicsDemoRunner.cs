using System.Globalization;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AiFrameworkDemo.Core;
using SkiaSharp;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>
/// Демонстраторы AI.Economics. Реализация разнесена по частичным файлам
/// по категориям модуля.
/// </summary>
public static partial class EconomicsDemoRunner
{
    private static readonly SKColor[] Pal =
    [
        new(0x34, 0xD3, 0x99), new(0x60, 0xA5, 0xFA), new(0xFB, 0xBF, 0x24),
        new(0xF8, 0x71, 0x71), new(0xA7, 0x8B, 0xFA), new(0x38, 0xBD, 0xF8),
        new(0xFB, 0x92, 0x3C), new(0xF4, 0x72, 0xB6), new(0x4A, 0xDE, 0x80),
        new(0xE8, 0x79, 0xF9), new(0x22, 0xD3, 0xEE), new(0xFF, 0xE0, 0x60),
    ];

    private static SKColor C(int i) => Pal[i % Pal.Length];

    /// <summary>Разделитель разрядов — неразрывный пробел, десятичный — запятая.</summary>
    private static readonly NumberFormatInfo Ru = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ",",
        PercentGroupSeparator = " ",
        PercentDecimalSeparator = ",",
    };

    public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        ChartView cv = MakeView(s);
        var rep = new ReportBuilder();
        string text;

        try
        {
            text = key switch
            {
                "unit_economics" => DoUnitEconomics(p, cv, rep),
                "channel_mix" => DoChannelMix(p, cv, rep),
                "retention_fit" => DoRetentionFit(p, cv, rep),
                "cohort_matrix" => DoCohortMatrix(p, cv, rep),
                "bg_nbd" => DoBgNbd(p, cv, rep),
                "gamma_gamma" => DoGammaGamma(p, cv, rep),
                "clv_portfolio" => DoClvPortfolio(p, cv, rep),
                "kaplan_meier" => DoKaplanMeier(p, cv, rep),
                "cox_ph" => DoCoxPh(p, cv, rep),
                "competing_risks" => DoCompetingRisks(p, cv, rep),
                "mrr_bridge" => DoMrrBridge(p, cv, rep),
                "saas_health" => DoSaasHealth(p, cv, rep),
                "runway_mc" => DoRunway(p, cv, rep),
                "funding_round" => DoFundingRound(p, cv, rep),
                "exit_waterfall" => DoExitWaterfall(p, cv, rep),
                "startup_valuation" => DoStartupValuation(p, cv, rep),
                "real_options" => DoRealOptions(p, cv, rep),
                "market_sizing" => DoMarketSizing(p, cv, rep),
                "bass_diffusion" => DoBassDiffusion(p, cv, rep),
                "elasticity" => DoElasticity(p, cv, rep),
                "price_optimization" => DoPriceOptimization(p, cv, rep),
                "wtp_survey" => DoWillingnessToPay(p, cv, rep),
                "conjoint" => DoConjoint(p, cv, rep),
                "mmm" => DoMarketingMix(p, cv, rep),
                "budget_allocation" => DoBudgetAllocation(p, cv, rep),
                "uplift" => DoUplift(p, cv, rep),
                "experiment_design" => DoExperimentDesign(p, cv, rep),
                "cuped" => DoCuped(p, cv, rep),
                "sequential_testing" => DoSequentialTesting(p, cv, rep),
                "bandits" => DoBandits(p, cv, rep),
                "arima" => DoArima(p, cv, rep),
                "ets" => DoEts(p, cv, rep),
                "theta" => DoTheta(p, cv, rep),
                "stl" => DoStl(p, cv, rep),
                "intermittent" => DoIntermittent(p, cv, rep),
                "hierarchical" => DoHierarchical(p, cv, rep),
                "backtest" => DoBacktest(p, cv, rep),
                "conformal" => DoConformal(p, cv, rep),
                "scorecard" => DoScorecard(p, cv, rep),
                "score_monitoring" => DoScoreMonitoring(p, cv, rep),
                "ifrs9" => DoIfrs9(p, cv, rep),
                "migration_matrix" => DoMigrationMatrix(p, cv, rep),
                "roll_rate" => DoRollRate(p, cv, rep),
                "vintage" => DoVintage(p, cv, rep),
                "merton" => DoMerton(p, cv, rep),
                "counterparty" => DoCounterparty(p, cv, rep),
                "financial_ratios" => DoFinancialRatios(p, cv, rep),
                "dupont" => DoDuPont(p, cv, rep),
                "distress_scores" => DoDistressScores(p, cv, rep),
                "beneish" => DoBeneish(p, cv, rep),
                "working_capital" => DoWorkingCapital(p, cv, rep),
                "earnings_quality" => DoEarningsQuality(p, cv, rep),
                "benford" => DoBenford(p, cv, rep),
                "bankruptcy_ml" => DoBankruptcyMl(p, cv, rep),
                "wacc" => DoWacc(p, cv, rep),
                "dcf" => DoDcf(p, cv, rep),
                "dcf_monte_carlo" => DoDcfMonteCarlo(p, cv, rep),
                "comparables" => DoComparables(p, cv, rep),
                "lbo" => DoLbo(p, cv, rep),
                "eva" => DoEva(p, cv, rep),
                "real_options_lsm" => DoRealOptionsLsm(p, cv, rep),
                "investment_criteria" => DoInvestmentCriteria(p, cv, rep),
                "depreciation" => DoDepreciation(p, cv, rep),
                "loan_schedule" => DoLoanSchedule(p, cv, rep),
                "lease_vs_buy" => DoLeaseVsBuy(p, cv, rep),
                "break_even" => DoBreakEven(p, cv, rep),
                "capital_structure" => DoCapitalStructure(p, cv, rep),
                "value_at_risk" => DoValueAtRisk(p, cv, rep),
                "extreme_value" => DoExtremeValue(p, cv, rep),
                "copulas" => DoCopulas(p, cv, rep),
                "var_backtest" => DoVarBacktest(p, cv, rep),
                "liquidity" => DoLiquidity(p, cv, rep),
                "portfolio_metrics" => DoPortfolioMetrics(p, cv, rep),
                "mean_variance" => DoMeanVariance(p, cv, rep),
                "risk_parity" => DoRiskParity(p, cv, rep),
                "black_litterman" => DoBlackLitterman(p, cv, rep),
                "cvar_portfolio" => DoCvarPortfolio(p, cv, rep),
                "factor_model" => DoFactorModel(p, cv, rep),
                "attribution" => DoAttribution(p, cv, rep),
                "rebalancing" => DoRebalancing(p, cv, rep),
                "regression_robust" => DoRegressionRobust(p, cv, rep),
                "regression_diagnostics" => DoRegressionDiagnostics(p, cv, rep),
                "iv_2sls" => DoIv2Sls(p, cv, rep),
                "panel_data" => DoPanelData(p, cv, rep),
                "dynamic_panel" => DoDynamicPanel(p, cv, rep),
                "limited_dependent" => DoLimitedDependent(p, cv, rep),
                "quantile_regression" => DoQuantileRegression(p, cv, rep),
                "causal_did" => DoCausalDid(p, cv, rep),
                "causal_rdd" => DoCausalRdd(p, cv, rep),
                "causal_matching" => DoCausalMatching(p, cv, rep),
                "synthetic_control" => DoSyntheticControl(p, cv, rep),
                "causal_forest" => DoCausalForest(p, cv, rep),
                "stationarity" => DoStationarity(p, cv, rep),
                "var_model" => DoVarModel(p, cv, rep),
                "cointegration" => DoCointegration(p, cv, rep),
                "garch" => DoGarch(p, cv, rep),
                "state_space" => DoStateSpace(p, cv, rep),
                _ => $"Неизвестный ключ алгоритма: {key}",
            };
        }
        catch (Exception ex)
        {
            // Частично заполненный отчёт выдал бы расчёт за успешный
            rep = new ReportBuilder();
            text = $"Ошибка: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}";
        }

        return Png(cv, s, textOutput: text, report: rep.Build());
    }

    #region Интерпретация результата

    /// <summary>
    /// Переносит разбор результата в отчёт демо и возвращает его текст.
    /// </summary>
    /// <remarks>
    /// Каждый расчёт <c>AI.Economics</c> умеет объяснить себя словами через
    /// <see cref="IInterpretable"/>. Демо не пересказывает эти выводы своими
    /// словами, а показывает ровно то, что получит потребитель библиотеки —
    /// в том числе языковая модель, которой отдают результат расчёта.
    /// </remarks>
    private static string Explain(ReportBuilder rep, IInterpretable result, string? extraLog = null)
    {
        Interpretation interpretation = result.Interpret();

        foreach (InterpretedMetric metric in interpretation.Metrics.Take(6))
        {
            rep.Metric(metric.Name,
                metric.Value + (string.IsNullOrEmpty(metric.Unit) ? "" : " " + metric.Unit),
                null, metric.Meaning, Tone(metric.Quality));
        }

        if (!string.IsNullOrEmpty(interpretation.Summary))
            rep.Note(interpretation.Summary);

        if (interpretation.Metrics.Count > 6)
        {
            var table = rep.Table("Остальные метрики", ["Показатель", "Значение", "Смысл"],
                [false, true, false]);

            foreach (InterpretedMetric metric in interpretation.Metrics.Skip(6))
                table.Row(metric.Name,
                    metric.Value + (string.IsNullOrEmpty(metric.Unit) ? "" : " " + metric.Unit),
                    metric.Meaning ?? "");
        }

        AddList(rep, "Выводы", interpretation.Findings);
        AddList(rep, "Предупреждения", interpretation.Warnings);
        AddList(rep, "Рекомендации", interpretation.Recommendations);

        string text = interpretation.ToLlmText();
        return string.IsNullOrEmpty(extraLog) ? text : extraLog + "\n\n" + text;
    }

    /// <summary>
    /// Добавляет к уже собранному отчёту выводы, предупреждения и рекомендации
    /// и возвращает текст разбора вместе с исходным логом.
    /// </summary>
    /// <remarks>
    /// Отличие от <see cref="Explain"/>: метрики не добавляются, потому что
    /// демо уже собрало собственные, подобранные под график.
    /// </remarks>
    private static string Narrate(ReportBuilder rep, Interpretation interpretation, string log)
    {
        AddList(rep, "Выводы", interpretation.Findings);
        AddList(rep, "Предупреждения", interpretation.Warnings);
        AddList(rep, "Рекомендации", interpretation.Recommendations);

        return log + "\n" + interpretation.ToLlmText();
    }

    /// <summary>То же для результата, умеющего объяснить себя сам.</summary>
    private static string Narrate(ReportBuilder rep, IInterpretable result, string log) =>
        Narrate(rep, result.Interpret(), log);

    private static void AddList(ReportBuilder rep, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;

        var table = rep.Table(title, [title], [false]);
        foreach (string item in items) table.Row(item);
    }

    private static MetricTone Tone(MetricQuality quality) => quality switch
    {
        MetricQuality.Good => MetricTone.Good,
        MetricQuality.Warning => MetricTone.Warn,
        MetricQuality.Critical => MetricTone.Bad,
        _ => MetricTone.Neutral,
    };

    #endregion

    #region Форматирование

    /// <summary>Компактная запись денежной суммы: тысячи, миллионы, миллиарды.</summary>
    private static string Money(double v)
    {
        double abs = Math.Abs(v);
        return abs switch
        {
            >= 1e9 => (v / 1e9).ToString("N2", Ru) + " млрд",
            >= 1e6 => (v / 1e6).ToString("N2", Ru) + " млн",
            >= 1e4 => (v / 1e3).ToString("N1", Ru) + " тыс.",
            _ => v.ToString("N0", Ru),
        };
    }

    /// <summary>Доля в процентах.</summary>
    private static string Pct(double v, int digits = 1) =>
        double.IsNaN(v) ? "—" : (v * 100).ToString("N" + digits, Ru) + " %";

    /// <summary>Число с фиксированной точностью.</summary>
    private static string Num(double v, int digits = 2) =>
        double.IsNaN(v) ? "—"
        : double.IsPositiveInfinity(v) ? "∞"
        : v.ToString("N" + digits, Ru);

    /// <summary>Целое число с разделителями разрядов.</summary>
    private static string Int(double v) => Math.Round(v).ToString("N0", Ru);

    #endregion

    #region Векторы

    /// <summary>Вектор из последовательности.</summary>
    private static Vector Vec(IEnumerable<double> values)
    {
        double[] a = [.. values];
        var v = new Vector(a.Length);
        for (int i = 0; i < a.Length; i++) v[i] = a[i];
        return v;
    }

    /// <summary>Ось «номер периода» от нуля.</summary>
    private static Vector Axis(int n, double from = 0, double step = 1)
    {
        var v = new Vector(n);
        for (int i = 0; i < n; i++) v[i] = from + (i * step);
        return v;
    }

    /// <summary>
    /// Заменяет неопределённые значения соседними.
    /// </summary>
    /// <remarks>
    /// Доверительная последовательность и метрики бэктеста законно
    /// не определены на первых точках. В отчёте это честно печатается словами,
    /// но в JSON интерактивного графика такие значения записать нельзя —
    /// сериализатор на них падает.
    /// </remarks>
    private static Vector Finite(Vector v)
    {
        var result = new Vector(v.Count);
        double carry = double.NaN;

        for (int i = 0; i < v.Count; i++)
        {
            if (double.IsFinite(v[i])) carry = v[i];
            result[i] = carry;
        }

        // Начало ряда заполняется первым определённым значением
        double first = 0;
        for (int i = 0; i < result.Count; i++)
            if (double.IsFinite(result[i])) { first = result[i]; break; }

        for (int i = 0; i < result.Count; i++)
            if (!double.IsFinite(result[i])) result[i] = first;

        return result;
    }

    /// <summary>Отрезок ломаной — им рисуются вертикальные отсечки и пороги.</summary>
    private static void Segment(ChartView cv, double x1, double y1, double x2, double y2,
        SKColor color, string name = "", int width = 2)
    {
        cv.AddPlot(new Vector(x1, x2), new Vector(y1, y2), name, color, width);
    }

    #endregion

    #region Генерация синтетических данных

    /// <summary>
    /// Параметры sBG по «отточному» описанию: доля ушедших в первый месяц и
    /// однородность клиентов. Так ползунки демо остаются интерпретируемыми,
    /// а генерация — честной моделью, а не подрисованной кривой.
    /// </summary>
    private static (double Alpha, double Beta) SbgFromChurn(double firstMonthChurn, double spread)
    {
        double alpha = Math.Max(spread, 0.05);
        double keep = Math.Clamp(1.0 - firstMonthChurn, 0.05, 0.98);
        double beta = alpha * keep / (1.0 - keep);
        return (alpha, beta);
    }

    /// <summary>Кривая доживания sBG длиной <paramref name="periods"/> + 1.</summary>
    private static double[] SbgSurvival(double alpha, double beta, int periods)
    {
        var s = new double[periods + 1];
        s[0] = 1.0;
        for (int t = 1; t <= periods; t++) s[t] = s[t - 1] * (beta + t - 1) / (alpha + beta + t - 1);
        return s;
    }

    /// <summary>
    /// Наблюдаемое число доживших: каждому клиенту сопоставляется собственный
    /// порог, поэтому выборка монотонна и воспроизводит реальный когортный отчёт.
    /// </summary>
    private static double[] SampleSurvivors(double[] survival, int cohortSize, Random rng)
    {
        var thresholds = new double[cohortSize];
        for (int i = 0; i < cohortSize; i++) thresholds[i] = rng.NextDouble();
        Array.Sort(thresholds);

        var counts = new double[survival.Length];
        for (int t = 0; t < survival.Length; t++)
        {
            int lo = 0, hi = cohortSize;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (thresholds[mid] < survival[t]) lo = mid + 1;
                else hi = mid;
            }
            counts[t] = lo;
        }

        return counts;
    }

    /// <summary>Экспоненциальное время до события с заданной интенсивностью.</summary>
    private static double ExponentialTime(Random rng, double rate) =>
        rate <= 0 ? double.PositiveInfinity : -Math.Log(1.0 - rng.NextDouble()) / rate;

    #endregion
}

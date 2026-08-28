using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Portfolio;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категории «Портфель и инвестиции».</summary>
public static partial class EconomicsDemoRunner
{
    /// <summary>Названия активов синтетического рынка.</summary>
    private static readonly string[] MarketAssets = ["Облигации", "Акции", "Сырьё"];

    #region Метрики портфеля

    private static string DoPortfolioMetrics(
        IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (Matrix assets, Vector benchmark) = SimulateMarket(p);

        double bonds = Math.Clamp(p.GetValueOrDefault("w_bonds", 0.4), 0, 1);
        double equity = Math.Clamp(p.GetValueOrDefault("w_equity", 0.4), 0, 1 - bonds);
        var weights = new Vector(bonds, equity, Math.Max(1 - bonds - equity, 0));

        Vector portfolio = PortfolioMetrics.PortfolioReturns(weights, assets);

        PerformanceMetrics result = PortfolioMetrics.Compute(
            portfolio, benchmark, p.GetValueOrDefault("rf", 0.004), 12, 0, "портфель");

        var axis = Axis(portfolio.Count, 1);
        var equityCurve = new Vector(portfolio.Count);
        double value = 1;

        for (int t = 0; t < portfolio.Count; t++)
        {
            value *= 1 + portfolio[t];
            equityCurve[t] = value;
        }

        cv.AddPlot(axis, equityCurve, "Накопленная стоимость", C(0), 3);
        cv.AddPlot(axis, result.Drawdowns, "Просадка", C(3), 2);
        cv.ChartName = $"Шарп {Num(result.Sharpe, 2)}, максимальная просадка {Pct(result.MaxDrawdown, 1)}";
        cv.LabelX = "Месяц";
        cv.LabelY = "Стоимость и просадка";

        var table = rep.Table("Метрики", ["Метрика", "Значение", "Смысл"], [false, true, false]);
        table.Row("Доходность", Pct(result.AnnualReturn, 2), "в годовом выражении");
        table.Row("Волатильность", Pct(result.Volatility, 2), "годовое стандартное отклонение");
        table.Row("Шарп", Num(result.Sharpe, 3), "на единицу общего риска");
        table.Row("Сортино", Num(result.Sortino, 3), "риск считается только по падениям");
        table.Row("Кальмар", Num(result.Calmar, 3), "доходность к максимальной просадке");
        table.Row("Омега", Num(result.Omega, 3), "выигрыши к потерям");
        table.Row("Максимальная просадка", Pct(result.MaxDrawdown, 2),
            $"{result.MaxDrawdownLength} периодов падения");
        table.Row("Доля прибыльных периодов", Pct(result.HitRate, 1), "как часто портфель растёт");

        var active = rep.Table("Против эталона", ["Метрика", "Значение"], [false, true]);
        active.Row("Альфа", Pct(result.Alpha, 2));
        active.Row("Бета", Num(result.Beta, 3));
        active.Row("Ошибка следования", Pct(result.TrackingError, 2));
        active.Row("Информационный коэффициент", Num(result.InformationRatio, 3));

        string log =
            $"Веса: {Pct(weights[0], 0)} / {Pct(weights[1], 0)} / {Pct(weights[2], 0)}.\n" +
            $"Доходность {Pct(result.AnnualReturn, 2)} при волатильности {Pct(result.Volatility, 2)}.\n" +
            $"Шарп {Num(result.Sharpe, 3)}, Сортино {Num(result.Sortino, 3)}, " +
            $"Кальмар {Num(result.Calmar, 3)}.\n" +
            $"Максимальная просадка {Pct(result.MaxDrawdown, 2)}, восстановление " +
            $"{result.RecoveryPeriods} периодов.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Марковиц

    private static string DoMeanVariance(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (Matrix assets, _) = SimulateMarket(p);

        Matrix covariance = MeanVariance.Covariance(assets, p.GetValueOrDefault("shrinkage", 0.1));
        var expected = new Vector(assets.Width);

        for (int j = 0; j < assets.Width; j++)
        {
            double mean = 0;
            for (int t = 0; t < assets.Height; t++) mean += assets[t, j];
            expected[j] = mean / assets.Height * 12;
        }

        // Ковариации приводятся к годовым, как и доходности
        var annual = new Matrix(assets.Width, assets.Width);
        for (int i = 0; i < assets.Width; i++)
            for (int j = 0; j < assets.Width; j++) annual[i, j] = covariance[i, j] * 12;

        OptimizationResult result = MeanVariance.Optimize(
            expected, annual, MarketAssets, p.GetValueOrDefault("rf", 0.05),
            new PortfolioConstraints { MaximumWeight = p.GetValueOrDefault("max_weight", 0.6) });

        cv.AddPlot(Vec(result.Frontier.Select(f => f.Risk)), Vec(result.Frontier.Select(f => f.Return)),
            "Эффективная граница", C(0), 3);
        cv.AddPlot(new Vector(result.Risk), new Vector(result.ExpectedReturn), "Максимум Шарпа", C(3), 0);

        if (result.MinimumVariance is not null)
        {
            cv.AddPlot(new Vector(result.MinimumVariance.Risk), new Vector(result.MinimumVariance.Return),
                "Минимум риска", C(1), 0);
        }

        cv.ChartName = $"Шарп {Num(result.Sharpe, 3)} при риске {Pct(result.Risk, 1)}";
        cv.LabelX = "Риск, годовое стандартное отклонение";
        cv.LabelY = "Ожидаемая доходность";

        var weights = rep.Table("Оптимальный портфель",
            ["Актив", "Вес", "Вклад в риск", "Ожидаемая доходность"], [false, true, true, true]);

        for (int i = 0; i < result.RiskBudget.Count; i++)
        {
            (string asset, double weight, double contribution) = result.RiskBudget[i];
            weights.Row(asset, Pct(weight, 1), Pct(contribution, 1), Pct(expected[i], 1));
        }

        var frontier = rep.Table("Эффективная граница",
            ["Доходность", "Риск", "Шарп"], [true, true, true]);

        foreach (FrontierPoint point in result.Frontier)
            frontier.Row(Pct(point.Return, 2), Pct(point.Risk, 2), Num(point.Sharpe, 3));

        string log =
            $"Ожидаемая доходность {Pct(result.ExpectedReturn, 2)} при риске {Pct(result.Risk, 2)}.\n" +
            $"Коэффициент Шарпа {Num(result.Sharpe, 3)}.\n" +
            $"Эффективное число активов {Num(result.EffectiveAssets, 2)} из {MarketAssets.Length}.\n" +
            $"Портфель минимального риска: доходность " +
            $"{Pct(result.MinimumVariance?.Return ?? 0, 2)} при риске " +
            $"{Pct(result.MinimumVariance?.Risk ?? 0, 2)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Паритет риска

    private static string DoRiskParity(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (Matrix assets, _) = SimulateMarket(p);
        Matrix covariance = MeanVariance.Covariance(assets, p.GetValueOrDefault("shrinkage", 0.05));

        var method = (RiskParityMethod)(int)p.GetValueOrDefault("method", 1);
        RiskParityResult result = RiskParity.Build(covariance, MarketAssets, method);

        var index = Axis(result.RiskBudget.Count, 1);
        cv.AddPlot(index, Vec(result.RiskBudget.Select(r => r.Weight)), "Вес актива", C(0), 3);
        cv.AddPlot(index, Vec(result.RiskBudget.Select(r => r.RiskContribution)), "Вклад в риск", C(3), 3);
        Segment(cv, 1, 1.0 / result.RiskBudget.Count, result.RiskBudget.Count,
            1.0 / result.RiskBudget.Count, C(5), "Равный вклад", 2);
        cv.ChartName = $"Отклонение от паритета {Pct(result.MaximumDeviation, 2)}";
        cv.LabelX = "Актив";
        cv.LabelY = "Доля";

        var table = rep.Table("Распределение риска",
            ["Актив", "Вес", "Вклад в риск", "Волатильность"], [false, true, true, true]);

        for (int i = 0; i < result.RiskBudget.Count; i++)
        {
            (string asset, double weight, double contribution) = result.RiskBudget[i];
            table.Row(asset, Pct(weight, 1), Pct(contribution, 1),
                Pct(Math.Sqrt(Math.Max(covariance[i, i], 0)) * Math.Sqrt(12), 1));
        }

        var comparison = rep.Table("Сравнение методов",
            ["Метод", "Риск портфеля", "Отклонение от паритета", "Диверсификация"],
            [false, true, true, true]);

        foreach (RiskParityMethod candidate in Enum.GetValues<RiskParityMethod>())
        {
            RiskParityResult item = RiskParity.Build(covariance, MarketAssets, candidate);
            comparison.Row(ParityLabel(candidate), Pct(item.Risk * Math.Sqrt(12), 2),
                Pct(item.MaximumDeviation, 2), Num(item.DiversificationRatio, 2));
        }

        string log =
            $"Метод: {ParityLabel(method)}.\n" +
            $"Риск портфеля {Pct(result.Risk * Math.Sqrt(12), 2)} в годовом выражении.\n" +
            $"Максимальное отклонение вклада в риск от равного {Pct(result.MaximumDeviation, 3)}.\n" +
            $"Коэффициент диверсификации {Num(result.DiversificationRatio, 2)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название метода паритета риска.</summary>
    private static string ParityLabel(RiskParityMethod method) => method switch
    {
        RiskParityMethod.InverseVolatility => "обратная волатильность",
        RiskParityMethod.EqualRiskContribution => "равный вклад в риск",
        _ => "иерархический",
    };

    #endregion

    #region Блэк — Литтерман

    private static string DoBlackLitterman(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (Matrix assets, _) = SimulateMarket(p);

        Matrix monthly = MeanVariance.Covariance(assets);
        var covariance = new Matrix(assets.Width, assets.Width);

        for (int i = 0; i < assets.Width; i++)
            for (int j = 0; j < assets.Width; j++) covariance[i, j] = monthly[i, j] * 12;

        var marketWeights = new Vector(0.5, 0.3, 0.2);

        var views = new List<InvestorView>
        {
            BlackLitterman.Relative(3, 1, 0,
                p.GetValueOrDefault("view_excess", 0.03),
                p.GetValueOrDefault("confidence", 0.5),
                "акции обгонят облигации"),
        };

        BlackLittermanResult result = BlackLitterman.Blend(
            marketWeights, covariance, views, MarketAssets,
            p.GetValueOrDefault("risk_aversion", 2.5), p.GetValueOrDefault("tau", 0.05));

        var index = Axis(MarketAssets.Length, 1);
        cv.AddPlot(index, result.MarketWeights, "Рыночные веса", C(5), 2);
        cv.AddPlot(index, result.OptimalWeights, "Веса со взглядами", C(0), 3);
        cv.ChartName = $"Активная доля портфеля {Pct(result.ActiveShare, 1)}";
        cv.LabelX = "Актив";
        cv.LabelY = "Вес в портфеле";

        var table = rep.Table("Доходности и веса",
            ["Актив", "Равновесная доходность", "Апостериорная", "Рыночный вес", "Итоговый вес"],
            [false, true, true, true, true]);

        for (int i = 0; i < MarketAssets.Length; i++)
        {
            table.Row(MarketAssets[i], Pct(result.ImpliedReturns[i], 2),
                Pct(result.PosteriorReturns[i], 2), Pct(result.MarketWeights[i], 1),
                Pct(result.OptimalWeights[i], 1));
        }

        var sensitivity = rep.Table("Активная доля по уверенности",
            ["Уверенность", "Активная доля"], [true, true]);

        foreach (double confidence in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
        {
            BlackLittermanResult variant = BlackLitterman.Blend(
                marketWeights, covariance,
                [BlackLitterman.Relative(3, 1, 0, p.GetValueOrDefault("view_excess", 0.03), confidence)],
                MarketAssets, p.GetValueOrDefault("risk_aversion", 2.5), p.GetValueOrDefault("tau", 0.05));

            sensitivity.Row(Pct(confidence, 0), Pct(variant.ActiveShare, 1));
        }

        string log =
            $"Взгляд: акции обгонят облигации на {Pct(p.GetValueOrDefault("view_excess", 0.03), 1)} " +
            $"с уверенностью {Pct(p.GetValueOrDefault("confidence", 0.5), 0)}.\n" +
            $"Активная доля портфеля {Pct(result.ActiveShare, 2)}.\n" +
            $"Равновесная доходность акций {Pct(result.ImpliedReturns[1], 2)}, апостериорная " +
            $"{Pct(result.PosteriorReturns[1], 2)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Оптимизация по хвосту

    private static string DoCvarPortfolio(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (Matrix assets, _) = SimulateMarket(p);

        double crashProbability = p.GetValueOrDefault("crash_prob", 0.03);
        double crashSize = p.GetValueOrDefault("crash_size", 0.15);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 9) + 100);

        // Третьему активу добавляются редкие глубокие падения
        var scenarios = new Matrix(assets.Height, assets.Width);

        for (int t = 0; t < assets.Height; t++)
        {
            for (int j = 0; j < assets.Width; j++) scenarios[t, j] = assets[t, j];

            if (rng.NextDouble() < crashProbability)
                scenarios[t, assets.Width - 1] -= crashSize;
        }

        CvarOptimizationResult result = CvarOptimization.Optimize(
            scenarios, MarketAssets, p.GetValueOrDefault("confidence", 0.95),
            double.NegativeInfinity, p.GetValueOrDefault("max_weight", 0.7));

        var index = Axis(MarketAssets.Length, 1);
        cv.AddPlot(index, result.MeanVarianceWeights, "Минимум дисперсии", C(5), 2);
        cv.AddPlot(index, result.Weights, "Минимум хвостовых потерь", C(0), 3);
        cv.ChartName = $"Хвостовые потери {Pct(result.ConditionalValueAtRisk, 2)} против " +
                       $"{Pct(result.MeanVarianceCvar, 2)}";
        cv.LabelX = "Актив";
        cv.LabelY = "Вес в портфеле";

        var table = rep.Table("Сравнение портфелей",
            ["Актив", "Минимум хвоста", "Минимум дисперсии", "Разница"], [false, true, true, true]);

        for (int i = 0; i < MarketAssets.Length; i++)
        {
            table.Row(MarketAssets[i], Pct(result.Weights[i], 1),
                Pct(result.MeanVarianceWeights[i], 1),
                Pct(result.Weights[i] - result.MeanVarianceWeights[i], 1));
        }

        var risk = rep.Table("Показатели риска", ["Показатель", "Значение"], [false, true]);
        risk.Row("Ожидаемые потери в хвосте", Pct(result.ConditionalValueAtRisk, 3));
        risk.Row("Стоимость под риском", Pct(result.ValueAtRisk, 3));
        risk.Row("Ожидаемая доходность", Pct(result.ExpectedReturn, 3));
        risk.Row("Волатильность", Pct(result.Volatility, 3));
        risk.Row("Сценариев в хвосте", $"{result.TailScenarios} из {result.Scenarios}");

        string log =
            $"Ожидаемые потери в хвосте {Pct(result.ConditionalValueAtRisk, 3)} против " +
            $"{Pct(result.MeanVarianceCvar, 3)} у портфеля минимальной дисперсии.\n" +
            $"Выигрыш {Pct(result.TailImprovement, 1)}.\n" +
            $"Порог потерь {Pct(result.ValueAtRisk, 3)} на уровне {Pct(result.Confidence, 0)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Факторные модели

    private static string DoFactorModel(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 180);
        double marketBeta = p.GetValueOrDefault("market_beta", 1.1);
        double sizeBeta = p.GetValueOrDefault("size_beta", 0.4);
        double alpha = p.GetValueOrDefault("alpha", 0.002);
        double noise = p.GetValueOrDefault("noise", 0.008);
        double marketVolatility = p.GetValueOrDefault("market_vol", 0.04);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 11));

        var factors = new Matrix(n, 2);
        var excess = new Vector(n);

        for (int t = 0; t < n; t++)
        {
            double market = RandomEngine.NextGaussian(rng, 0.006, marketVolatility);
            double size = RandomEngine.NextGaussian(rng, 0.002, marketVolatility / 2);

            factors[t, 0] = market;
            factors[t, 1] = size;
            excess[t] = alpha + (marketBeta * market) + (sizeBeta * size)
                + RandomEngine.NextGaussian(rng, 0, noise);
        }

        FactorModelResult result = FactorModels.Fit(excess, factors, ["рынок", "размер"], 12, "фонд");

        var axis = Axis(n, 1);
        cv.AddPlot(axis, excess, "Доходность фонда", C(0), 2);
        cv.AddPlot(axis, Vec(Enumerable.Range(0, n).Select(t =>
            result.Loadings[0].Loading * factors[t, 0])), "Объяснено рынком", C(3), 2);
        cv.ChartName = $"Альфа {Pct(result.Alpha, 2)} (t = {Num(result.AlphaTStatistic, 2)}), " +
                       $"R² = {Num(result.RSquared, 2)}";
        cv.LabelX = "Месяц";
        cv.LabelY = "Избыточная доходность";

        var table = rep.Table("Нагрузки на факторы",
            ["Фактор", "Нагрузка", "Ст. ошибка", "t", "Вклад в доходность"],
            [false, true, true, true, true]);

        foreach (FactorLoading loading in result.Loadings)
        {
            table.Row(loading.Factor, Num(loading.Loading, 3), Num(loading.StandardError, 4),
                Num(loading.TStatistic, 2), Pct(loading.Contribution, 2));
        }

        // Статистические факторы из главных компонент на тех же данных
        var wide = new Matrix(n, 4);
        for (int t = 0; t < n; t++)
        {
            wide[t, 0] = excess[t];
            wide[t, 1] = factors[t, 0];
            wide[t, 2] = factors[t, 1];
            wide[t, 3] = (0.5 * factors[t, 0]) + RandomEngine.NextGaussian(rng, 0, noise);
        }

        (Matrix _, Vector explained, Matrix loadings) = FactorModels.PrincipalComponents(wide, 3);

        var pca = rep.Table("Главные компоненты",
            ["Компонента", "Доля дисперсии", "Нагрузка фонда"], [true, true, true]);

        for (int i = 0; i < explained.Count; i++)
            pca.Row($"{i + 1}", Pct(explained[i], 1), Num(loadings[0, i], 3));

        string log =
            $"Альфа {Pct(result.Alpha, 3)} годовых при t = {Num(result.AlphaTStatistic, 2)}.\n" +
            $"Факторы объясняют {Pct(result.RSquared, 1)} дисперсии.\n" +
            $"Доходность {Pct(result.TotalReturn, 2)}, из них факторами объяснено " +
            $"{Pct(result.ExplainedReturn, 2)}.\n" +
            $"Первая главная компонента объясняет {Pct(explained[0], 1)} дисперсии активов.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Атрибуция

    private static string DoAttribution(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double portfolioEquity = Math.Clamp(p.GetValueOrDefault("w_equity", 0.5), 0, 1);
        double portfolioBonds = Math.Clamp(p.GetValueOrDefault("w_bonds", 0.3), 0, 1 - portfolioEquity);
        double benchmarkEquity = Math.Clamp(p.GetValueOrDefault("b_equity", 0.4), 0, 1);
        double benchmarkBonds = Math.Clamp(p.GetValueOrDefault("b_bonds", 0.4), 0, 1 - benchmarkEquity);

        var portfolioWeights = new Vector(
            portfolioEquity, portfolioBonds, Math.Max(1 - portfolioEquity - portfolioBonds, 0));
        var benchmarkWeights = new Vector(
            benchmarkEquity, benchmarkBonds, Math.Max(1 - benchmarkEquity - benchmarkBonds, 0));

        var portfolioReturns = new Vector(
            p.GetValueOrDefault("r_equity", 0.1), p.GetValueOrDefault("r_bonds", 0.05), 0.02);
        var benchmarkReturns = new Vector(
            p.GetValueOrDefault("rb_equity", 0.08), p.GetValueOrDefault("rb_bonds", 0.06), 0.03);

        AttributionResult result = FactorModels.BrinsonAttribution(
            ["Акции", "Облигации", "Деньги"],
            portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns, "портфель");

        var index = Axis(result.Segments.Count, 1);
        cv.AddPlot(index, Vec(result.Segments.Select(s => s.Allocation)), "Распределение", C(0), 3);
        cv.AddPlot(index, Vec(result.Segments.Select(s => s.Selection)), "Выбор инструментов", C(1), 3);
        cv.AddPlot(index, Vec(result.Segments.Select(s => s.Interaction)), "Совместный эффект", C(3), 2);
        Segment(cv, 1, 0, result.Segments.Count, 0, C(5), "Ноль", 1);
        cv.ChartName = $"Активная доходность {Pct(result.ActiveReturn, 2)}";
        cv.LabelX = "Сегмент";
        cv.LabelY = "Вклад в активную доходность";

        var table = rep.Table("Разложение по сегментам",
            ["Сегмент", "Вес портфеля", "Вес эталона", "Доходность портфеля", "Доходность эталона",
                "Распределение", "Выбор", "Совместный", "Итого"],
            [false, true, true, true, true, true, true, true, true]);

        for (int i = 0; i < result.Segments.Count; i++)
        {
            BrinsonSegment segment = result.Segments[i];
            table.Row(segment.Segment, Pct(portfolioWeights[i], 0), Pct(benchmarkWeights[i], 0),
                Pct(portfolioReturns[i], 1), Pct(benchmarkReturns[i], 1),
                Pct(segment.Allocation, 2), Pct(segment.Selection, 2),
                Pct(segment.Interaction, 2), Pct(segment.Total, 2));
        }

        string log =
            $"Доходность портфеля {Pct(result.PortfolioReturn, 2)} против эталона " +
            $"{Pct(result.BenchmarkReturn, 2)}.\n" +
            $"Активная доходность {Pct(result.ActiveReturn, 2)}.\n" +
            $"Распределение {Pct(result.TotalAllocation, 2)}, выбор {Pct(result.TotalSelection, 2)}, " +
            $"совместный эффект {Pct(result.TotalInteraction, 2)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Перебалансировка

    private static string DoRebalancing(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        (Matrix assets, _) = SimulateMarket(p);

        var target = new Vector(0.4, 0.4, 0.2);
        var rule = (RebalancingRule)(int)p.GetValueOrDefault("rule", 2);

        double cost = p.GetValueOrDefault("cost", 0.001);
        double tax = p.GetValueOrDefault("tax", 0.13);
        double threshold = p.GetValueOrDefault("threshold", 0.05);
        int interval = (int)p.GetValueOrDefault("interval", 12);

        RebalancingResult result = Rebalancing.Simulate(
            assets, target, rule, MarketAssets, cost, tax, interval, threshold);

        IReadOnlyList<RebalancingResult> all = Rebalancing.CompareRules(assets, target, MarketAssets, cost, tax);

        var index = Axis(MarketAssets.Length, 1);
        cv.AddPlot(index, target, "Целевые веса", C(5), 2);
        cv.AddPlot(index, result.FinalWeights, "Итоговые веса", C(0), 3);
        cv.ChartName = $"Доходность {Pct(result.AnnualReturn, 2)}, потери на издержках " +
                       $"{Pct(result.CostDrag, 3)}";
        cv.LabelX = "Актив";
        cv.LabelY = "Вес в портфеле";

        var comparison = rep.Table("Сравнение правил",
            ["Правило", "Доходность", "Сделок", "Оборот", "Издержки", "Налог", "Потери доходности", "Дрейф"],
            [false, true, true, true, true, true, true, true]);

        foreach (RebalancingResult item in all)
        {
            comparison.Row(RuleLabel(item.Rule), Pct(item.AnnualReturn, 2), $"{item.RebalanceCount}",
                Pct(item.TotalTurnover, 0), Money(item.TotalCost), Money(item.TotalTax),
                Pct(item.CostDrag, 3), Pct(item.MaximumDrift, 1));
        }

        var trades = rep.Table("Сделки перебалансировки",
            ["Период", "Оборот", "Издержки", "Реализованная прибыль", "Налог"],
            [true, true, true, true, true]);

        foreach (RebalanceTrade trade in result.Trades.Take(20))
        {
            trades.Row($"{trade.Period}", Pct(trade.Turnover, 1), Money(trade.Cost),
                Money(trade.RealizedGain), Money(trade.Tax));
        }

        string log =
            $"Правило: {RuleLabel(rule)}. Совершено {result.RebalanceCount} перебалансировок.\n" +
            $"Суммарный оборот {Pct(result.TotalTurnover, 0)}, издержки {Money(result.TotalCost)}, " +
            $"налог {Money(result.TotalTax)}.\n" +
            $"Доходность после издержек {Pct(result.AnnualReturn, 2)}, потери " +
            $"{Pct(result.CostDrag, 3)} годовых.\n" +
            $"Лучшее правило на этих данных: {RuleLabel(all[0].Rule)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название правила перебалансировки.</summary>
    private static string RuleLabel(RebalancingRule rule) => rule switch
    {
        RebalancingRule.BuyAndHold => "без перебалансировки",
        RebalancingRule.Calendar => "по календарю",
        RebalancingRule.Threshold => "по порогу",
        _ => "частичная",
    };

    #endregion

    #region Синтетический рынок

    /// <summary>
    /// Синтетический рынок из трёх активов с общим фактором.
    /// </summary>
    /// <remarks>
    /// Все портфельные демонстраторы используют один и тот же генератор, поэтому
    /// их результаты сопоставимы: разные критерии оптимизации дают разные веса
    /// на одних и тех же данных.
    /// </remarks>
    private static (Matrix Assets, Vector Benchmark) SimulateMarket(IReadOnlyDictionary<string, double> p)
    {
        int months = (int)p.GetValueOrDefault("months", 180);
        double marketVolatility = p.GetValueOrDefault("market_vol", 0.04);
        double drift = p.GetValueOrDefault("drift", 0.006);
        double idiosyncratic = p.GetValueOrDefault("idio_vol", 0.015);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 9));

        double[] betas =
        [
            p.GetValueOrDefault("beta_bonds", 0.3),
            p.GetValueOrDefault("beta_equity", 1.1),
            p.GetValueOrDefault("beta_commodity", 0.6),
        ];

        var assets = new Matrix(months, 3);
        var benchmark = new Vector(months);

        for (int t = 0; t < months; t++)
        {
            double market = RandomEngine.NextGaussian(rng, drift, marketVolatility);
            benchmark[t] = market;

            for (int j = 0; j < 3; j++)
                assets[t, j] = (betas[j] * market) + RandomEngine.NextGaussian(rng, 0, idiosyncratic);
        }

        return (assets, benchmark);
    }

    #endregion
}

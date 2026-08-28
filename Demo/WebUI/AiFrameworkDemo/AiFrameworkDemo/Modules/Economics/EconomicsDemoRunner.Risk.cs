using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Risk;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категории «Риск-менеджмент».</summary>
public static partial class EconomicsDemoRunner
{
    #region Стоимость под риском

    private static string DoValueAtRisk(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var method = (VarMethod)(int)p.GetValueOrDefault("method", 0);
        int n = (int)p.GetValueOrDefault("n", 1500);
        double confidence = p.GetValueOrDefault("confidence", 0.99);
        int horizon = (int)p.GetValueOrDefault("horizon", 1);
        double portfolio = p.GetValueOrDefault("portfolio", 1_000_000_000);

        Vector returns = SimulateReturns(
            n, p.GetValueOrDefault("vol", 0.012), p.GetValueOrDefault("jump_prob", 0.05),
            p.GetValueOrDefault("jump_size", 4), (int)p.GetValueOrDefault("seed", 3));

        VarResultSet result = ValueAtRisk.Compute(returns, portfolio, confidence, horizon, method, "портфель");

        // Эмпирическое распределение убытков в порядке возрастания
        double[] sorted = [.. returns.OrderBy(r => r)];
        var probabilities = new Vector(sorted.Length);
        var values = new Vector(sorted.Length);

        for (int i = 0; i < sorted.Length; i++)
        {
            probabilities[i] = (i + 1.0) / sorted.Length;
            values[i] = sorted[i];
        }

        cv.AddPlot(values, probabilities, "Накопленная вероятность доходности", C(0), 3);
        Segment(cv, -result.ValueAtRisk, 0, -result.ValueAtRisk, 1, C(3),
            $"Порог: {Pct(result.ValueAtRisk, 2)}", 2);
        Segment(cv, -result.ExpectedShortfall, 0, -result.ExpectedShortfall, 1, C(5),
            $"Ожидаемые потери: {Pct(result.ExpectedShortfall, 2)}", 2);
        cv.ChartName = $"Потери не превысят {Pct(result.ValueAtRisk, 2)} с вероятностью {Pct(confidence, 0)}";
        cv.LabelX = "Доходность за период";
        cv.LabelY = "Накопленная вероятность";

        var methods = rep.Table("Сравнение методов",
            ["Метод", "Стоимость под риском", "Ожидаемые потери", "В деньгах"],
            [false, true, true, true]);

        foreach ((VarMethod candidate, double var, double shortfall) in result.Comparison)
        {
            methods.Row(VarMethodLabel(candidate), Pct(var, 3), Pct(shortfall, 3),
                Money(var * portfolio));
        }

        var moments = rep.Table("Характеристики распределения",
            ["Показатель", "Значение"], [false, true]);
        moments.Row("Волатильность", Pct(result.Volatility, 3));
        moments.Row("Асимметрия", Num(result.Skewness, 3));
        moments.Row("Эксцесс", Num(result.Kurtosis, 2));
        moments.Row("Отношение хвоста к порогу", Num(result.TailRatio, 2));

        string log =
            $"Метод: {VarMethodLabel(method)}, горизонт {horizon} дн., уровень {Pct(confidence, 1)}.\n" +
            $"Стоимость под риском {Pct(result.ValueAtRisk, 3)} ({Money(result.ValueAtRiskAmount)}).\n" +
            $"Ожидаемые потери в хвосте {Pct(result.ExpectedShortfall, 3)} " +
            $"({Money(result.ExpectedShortfallAmount)}).\n" +
            $"Эксцесс {Num(result.Kurtosis, 2)} при трёх у нормального закона.";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название метода расчёта риска.</summary>
    private static string VarMethodLabel(VarMethod method) => method switch
    {
        VarMethod.Historical => "исторический",
        VarMethod.Parametric => "параметрический",
        VarMethod.CornishFisher => "Корниш — Фишер",
        _ => "Монте-Карло",
    };

    /// <summary>Ряд доходностей со скачками: смесь двух нормальных распределений.</summary>
    private static Vector SimulateReturns(int n, double volatility, double jumpProbability, double jumpSize, int seed)
    {
        Random rng = RandomEngine.Create(seed);
        var returns = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            bool jump = rng.NextDouble() < jumpProbability;
            double scale = jump ? volatility * jumpSize : volatility;
            double drift = jump ? -volatility : volatility * 0.03;

            returns[i] = RandomEngine.NextGaussian(rng, drift, scale);
        }

        return returns;
    }

    #endregion

    #region Теория экстремальных значений

    private static string DoExtremeValue(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 3000);
        double tail = p.GetValueOrDefault("tail", 0.25);
        double scale = p.GetValueOrDefault("scale", 0.01);
        double threshold = p.GetValueOrDefault("threshold", 0.95);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 13));

        var returns = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            // Убытки с заданным степенным хвостом, прибыли близки к нормальным
            double uniform = Math.Max(rng.NextDouble(), 1e-9);
            returns[i] = rng.NextDouble() < 0.5
                ? RandomEngine.NextGaussian(rng, 0, scale)
                : -scale * (Math.Pow(uniform, -tail) - 1);
        }

        ExtremeValueResult result = ExtremeValue.Fit(returns, threshold, null, "убытки портфеля");
        IReadOnlyList<(double Threshold, double MeanExcess)> plot = ExtremeValue.MeanExcessPlot(returns);

        cv.AddPlot(Vec(plot.Select(x => x.Threshold)), Vec(plot.Select(x => x.MeanExcess)),
            "Среднее превышение над порогом", C(0), 3);
        Segment(cv, result.Threshold, 0, result.Threshold, plot.Max(x => x.MeanExcess), C(3),
            $"Выбранный порог: {Pct(result.Threshold, 2)}", 2);
        cv.ChartName = $"Параметр формы {Num(result.Shape, 3)} — хвост " +
                       (result.Shape > 0.15 ? "тяжёлый" : "умеренный");
        cv.LabelX = "Порог убытка";
        cv.LabelY = "Среднее превышение";

        var quantiles = rep.Table("Квантили убытков",
            ["Уровень", "По теории экстремумов", "Ожидаемые потери", "Эмпирический"],
            [true, true, true, true]);

        for (int i = 0; i < result.TailQuantiles.Count; i++)
        {
            (double confidence, double var, double shortfall) = result.TailQuantiles[i];
            double empirical = i < result.EmpiricalQuantiles.Count ? result.EmpiricalQuantiles[i].Empirical : 0;

            quantiles.Row(Pct(confidence, 2), Pct(var, 3), Pct(shortfall, 3), Pct(empirical, 3));
        }

        var excess = rep.Table("График среднего превышения",
            ["Порог", "Среднее превышение"], [true, true]);
        foreach ((double level, double mean) in plot) excess.Row(Pct(level, 3), Pct(mean, 4));

        string log =
            $"Порог {Pct(result.Threshold, 3)} превышен {result.Exceedances} раз из {result.Observations}.\n" +
            $"Параметр формы {Num(result.Shape, 4)}, масштаба {Num(result.Scale, 5)}.\n" +
            $"Среднее убытка {(result.HasFiniteMean ? "конечно" : "бесконечно")}, дисперсия " +
            $"{(result.HasFiniteVariance ? "конечна" : "бесконечна")}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Копулы

    private static string DoCopulas(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var family = (CopulaFamily)(int)p.GetValueOrDefault("family", 2);
        var fitFamily = (CopulaFamily)(int)p.GetValueOrDefault("fit_family", 2);
        int n = (int)p.GetValueOrDefault("n", 1500);
        double dependence = p.GetValueOrDefault("dependence", 3);
        double df = p.GetValueOrDefault("df", 5);
        int seed = (int)p.GetValueOrDefault("seed", 4);

        // Параметр приводится к допустимой области выбранного семейства
        double parameter = family switch
        {
            CopulaFamily.Gaussian or CopulaFamily.StudentT => Math.Tanh(dependence / 4),
            CopulaFamily.Gumbel => 1 + dependence,
            _ => dependence,
        };

        Matrix sample = Copulas.Simulate(family, parameter, n, df, seed);

        var first = new Vector(n);
        var second = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            first[i] = sample[i, 0];
            second[i] = sample[i, 1];
        }

        CopulaResult result = Copulas.Fit(first, second, fitFamily, ("актив A", "актив B"));

        cv.AddPlot(first, second, "Пары рангов", C(1), 0);
        Segment(cv, 0, 0.1, 1, 0.1, C(5), "Нижний дециль", 1);
        Segment(cv, 0.1, 0, 0.1, 1, C(5), "", 1);
        cv.ChartName = $"Зависимость в нижнем хвосте {Num(result.LowerTailDependence, 3)}, " +
                       $"Кендалл {Num(result.KendallTau, 3)}";
        cv.LabelX = "Ранг первого актива";
        cv.LabelY = "Ранг второго актива";

        var families = rep.Table("Сравнение семейств",
            ["Семейство", "Логарифм правдоподобия", "AIC"], [false, true, true]);

        foreach ((CopulaFamily candidate, double logLikelihood, double aic) in result.Comparison)
            families.Row(CopulaLabel(candidate), Num(logLikelihood, 1), Num(aic, 1));

        var tails = rep.Table("Характеристики зависимости", ["Показатель", "Значение"], [false, true]);
        tails.Row("Параметр", Num(result.Parameter, 4));
        tails.Row("Корреляция Кендалла", Num(result.KendallTau, 3));
        tails.Row("Корреляция Пирсона", Num(result.PearsonCorrelation, 3));
        tails.Row("Нижний хвост", Num(result.LowerTailDependence, 3));
        tails.Row("Верхний хвост", Num(result.UpperTailDependence, 3));
        tails.Row("Наблюдаемая связь хвостов", Pct(result.EmpiricalLowerTail, 1));

        string log =
            $"Данные порождены копулой {CopulaLabel(family)}, подгоняется {CopulaLabel(fitFamily)}.\n" +
            $"Ранговая корреляция {Num(result.KendallTau, 3)}, линейная " +
            $"{Num(result.PearsonCorrelation, 3)}.\n" +
            $"Зависимость нижних хвостов {Num(result.LowerTailDependence, 3)}, верхних " +
            $"{Num(result.UpperTailDependence, 3)}.\n" +
            $"Лучшее по AIC семейство: " +
            $"{CopulaLabel(result.Comparison.OrderBy(c => c.Aic).First().Family)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Читаемое название семейства копул.</summary>
    private static string CopulaLabel(CopulaFamily family) => family switch
    {
        CopulaFamily.Gaussian => "гауссова",
        CopulaFamily.StudentT => "Стьюдента",
        CopulaFamily.Clayton => "Клейтона",
        _ => "Гумбеля",
    };

    #endregion

    #region Обратное тестирование и стресс

    private static string DoVarBacktest(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int n = (int)p.GetValueOrDefault("n", 1000);
        double volatility = p.GetValueOrDefault("vol", 0.02);
        double bias = p.GetValueOrDefault("bias", 1.0);
        double clustering = p.GetValueOrDefault("clustering", 0.0);
        double confidence = p.GetValueOrDefault("confidence", 0.99);
        double reverse = p.GetValueOrDefault("reverse", 0.2);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 5));

        var returns = new Vector(n);
        var forecasts = new Vector(n);
        double variance = volatility * volatility;

        for (int i = 0; i < n; i++)
        {
            // Модель прогнозирует постоянный риск, а истинная дисперсия инерционна
            double shock = RandomEngine.NextGaussian(rng) * Math.Sqrt(variance);
            returns[i] = shock;
            forecasts[i] = 2.326 * volatility / bias;

            variance = ((1 - clustering) * volatility * volatility)
                + (clustering * 0.5 * ((shock * shock) + variance));
        }

        BacktestVarResult result = VarBacktesting.Backtest(returns, forecasts, confidence, "модель риска");

        var periods = Axis(n, 1);
        cv.AddPlot(periods, returns, "Доходность", C(1), 1);
        cv.AddPlot(periods, Vec(forecasts.Select(v => -v)), "Порог риска", C(3), 2);
        cv.ChartName = $"Пробоев {result.Exceptions} при ожидаемых " +
                       $"{Num(result.ExpectedExceptions, 1)} — зона {result.TrafficLight}";
        cv.LabelX = "Наблюдение";
        cv.LabelY = "Доходность";

        var tests = rep.Table("Тесты",
            ["Тест", "Статистика", "p-значение", "Вывод"], [false, true, true, false]);
        tests.Row("Купец", Num(result.KupiecStatistic, 3), Num(result.KupiecPValue, 4),
            result.KupiecPValue >= 0.05 ? "число пробоев верно" : "число пробоев неверно");
        tests.Row("Кристофферсен", Num(result.IndependenceStatistic, 3), Num(result.IndependencePValue, 4),
            result.IndependencePValue >= 0.05 ? "пробои независимы" : "пробои группируются");
        tests.Row("Условное покрытие", Num(result.ConditionalCoverageStatistic, 3),
            Num(result.ConditionalCoveragePValue, 4),
            result.ConditionalCoveragePValue >= 0.05 ? "модель принимается" : "модель отвергается");

        StressTestResult stress = VarBacktesting.StressTest(
            new Vector(1_000_000_000.0, 500_000_000.0), new Vector(0.02, 0.03),
            [
                ("Кризис 2008", new Vector(-0.45, -0.25)),
                ("Ставочный шок", new Vector(-0.1, -0.35)),
                ("Мягкий спад", new Vector(-0.08, -0.05)),
            ],
            ["акции", "облигации"], result.Exceptions > 0 ? forecasts[0] : 0.03, reverse);

        var scenarios = rep.Table("Стресс-сценарии",
            ["Сценарий", "Шоки", "Потери"], [false, false, true]);

        foreach (StressScenario scenario in stress.Scenarios)
        {
            scenarios.Row(scenario.Name,
                string.Join(", ", scenario.Shocks.Select(s => Pct(s, 0))), Money(scenario.Loss));
        }

        string log =
            $"На {result.Observations} наблюдениях порог пробит {result.Exceptions} раз " +
            $"при ожидаемых {Num(result.ExpectedExceptions, 1)}.\n" +
            $"Купец p = {Num(result.KupiecPValue, 4)}, независимость p = " +
            $"{Num(result.IndependencePValue, 4)}.\n" +
            $"Максимальная серия пробоев {result.LongestExceptionRun}, тяжесть " +
            $"{Num(result.AverageExceptionSeverity, 2)}x.\n" +
            $"Обратный стресс-тест: потери {Pct(reverse, 0)} достигаются на расстоянии " +
            $"{Num(stress.ReverseStressDistance, 2)} стандартных отклонений.";

        return Explain(rep, result, log) + "\n\n" + stress.Interpret().ToLlmText();
    }

    #endregion

    #region Ликвидность

    private static string DoLiquidity(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int periods = (int)p.GetValueOrDefault("periods", 12);
        double inflow = p.GetValueOrDefault("inflow", 100_000_000);
        double outflow = p.GetValueOrDefault("outflow", 105_000_000);
        double seasonality = p.GetValueOrDefault("seasonality", 0.25);

        var inflows = new Vector(periods);
        var outflows = new Vector(periods);

        for (int t = 0; t < periods; t++)
        {
            double season = Math.Sin(2 * Math.PI * t / Math.Max(periods, 2));

            inflows[t] = inflow * (1 + (seasonality * season));
            outflows[t] = outflow * (1 - (seasonality * season * 0.5));
        }

        LiquidityResult result = LiquidityRisk.Analyze(
            p.GetValueOrDefault("opening", 50_000_000), inflows, outflows,
            p.GetValueOrDefault("volatility", 0.15),
            p.GetValueOrDefault("cost", 50_000),
            p.GetValueOrDefault("rate", 0.01),
            0, "компания", 3000, (int)p.GetValueOrDefault("seed", 7));

        var axis = Axis(periods, 1);
        cv.AddPlot(axis, Vec(result.Positions.Select(x => x.Closing)), "Остаток на конец периода", C(0), 3);
        cv.AddPlot(axis, inflows, "Поступления", C(1), 1);
        cv.AddPlot(axis, outflows, "Выплаты", C(3), 1);
        Segment(cv, 1, 0, periods, 0, C(5), "Ноль", 1);
        cv.ChartName = $"Минимальный остаток {Money(result.MinimumBalance)} в периоде {result.MinimumPeriod}";
        cv.LabelX = "Период";
        cv.LabelY = "Рубли";

        var calendar = rep.Table("Платёжный календарь",
            ["Период", "Остаток на начало", "Поступления", "Выплаты", "Остаток на конец", "Нехватка"],
            [true, true, true, true, true, true]);

        foreach (CashPosition position in result.Positions)
        {
            calendar.Row($"{position.Period}", Money(position.Opening), Money(position.Inflow),
                Money(position.Outflow), Money(position.Closing),
                position.Shortfall > 0 ? Money(position.Shortfall) : "—");
        }

        var balances = rep.Table("Оптимальные остатки", ["Модель", "Значение"], [false, true]);
        balances.Row("Баумоль: размер конвертации", Money(result.BaumolCash));
        balances.Row("Миллер — Орр: нижняя граница", Money(result.MillerOrrLower));
        balances.Row("Миллер — Орр: точка возврата", Money(result.MillerOrrReturn));
        balances.Row("Миллер — Орр: верхняя граница", Money(result.MillerOrrUpper));
        balances.Row("Миллер — Орр: средний остаток", Money(result.MillerOrrAverage));

        string log =
            $"Минимальный остаток {Money(result.MinimumBalance)} в периоде {result.MinimumPeriod}.\n" +
            $"Кассовых разрывов: {result.ShortfallPeriods}, максимальная нехватка " +
            $"{Money(result.MaximumShortfall)}.\n" +
            $"Вероятность разрыва с учётом неопределённости {Pct(result.ShortfallProbability, 1)}.\n" +
            $"Рекомендуемая кредитная линия {Money(result.RequiredCreditLine)}.";

        return Explain(rep, result, log);
    }

    #endregion
}

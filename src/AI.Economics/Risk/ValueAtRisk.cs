using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Economics.Insights;
using AI.Economics.Numerics;
using AI.Statistics;

namespace AI.Economics.Risk;

/// <summary>Способ расчёта стоимости под риском.</summary>
public enum VarMethod
{
    /// <summary>Исторический: квантиль эмпирического распределения доходностей.</summary>
    Historical,

    /// <summary>Параметрический: нормальное распределение с оценёнными моментами.</summary>
    Parametric,

    /// <summary>Параметрический с поправкой Корниша — Фишера на асимметрию и хвосты.</summary>
    CornishFisher,

    /// <summary>Монте-Карло с условной волатильностью.</summary>
    MonteCarlo,
}

/// <summary>Результат расчёта стоимости под риском.</summary>
public sealed record VarResultSet : IInterpretable
{
    /// <summary>Название портфеля.</summary>
    public string Portfolio { get; init; } = string.Empty;

    /// <summary>Использованный метод.</summary>
    public VarMethod Method { get; init; }

    /// <summary>Уровень доверия.</summary>
    public double Confidence { get; init; } = 0.99;

    /// <summary>Горизонт в днях.</summary>
    public int Horizon { get; init; } = 1;

    /// <summary>Стоимость под риском в долях портфеля.</summary>
    public double ValueAtRisk { get; init; }

    /// <summary>Ожидаемые потери в хвосте в долях портфеля.</summary>
    public double ExpectedShortfall { get; init; }

    /// <summary>Стоимость под риском в деньгах.</summary>
    public double ValueAtRiskAmount { get; init; }

    /// <summary>Ожидаемые потери в хвосте в деньгах.</summary>
    public double ExpectedShortfallAmount { get; init; }

    /// <summary>Оценки всеми методами для сравнения.</summary>
    public IReadOnlyList<(VarMethod Method, double Var, double Shortfall)> Comparison { get; init; } = [];

    /// <summary>Асимметрия распределения доходностей.</summary>
    public double Skewness { get; init; }

    /// <summary>Эксцесс распределения доходностей.</summary>
    public double Kurtosis { get; init; }

    /// <summary>Стандартное отклонение доходностей за период.</summary>
    public double Volatility { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Отношение ожидаемых потерь к стоимости под риском.</summary>
    public double TailRatio => ValueAtRisk > 0 ? ExpectedShortfall / ValueAtRisk : 0;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool fatTails = Kurtosis > 3.5;
        bool negativeSkew = Skewness < -0.3;

        double parametric = Comparison
            .Where(c => c.Method == VarMethod.Parametric)
            .Select(c => c.Var)
            .FirstOrDefault();

        double historical = Comparison
            .Where(c => c.Method == VarMethod.Historical)
            .Select(c => c.Var)
            .FirstOrDefault();

        double gap = parametric > 0 ? (historical - parametric) / parametric : 0;

        var builder = new InterpretationBuilder($"Стоимость под риском: {Portfolio}")
            .Summary($"На горизонте {Horizon} дн. с вероятностью {Fmt.Pct(Confidence, 0)} потери " +
                     $"не превысят {Fmt.Pct(ValueAtRisk, 2)} портфеля " +
                     $"({Fmt.Money(ValueAtRiskAmount)}). В худших {Fmt.Pct(1 - Confidence, 0)} " +
                     $"случаев средние потери составят {Fmt.Pct(ExpectedShortfall, 2)} " +
                     $"({Fmt.Money(ExpectedShortfallAmount)}). Метод: {MethodName(Method)}.")
            .Metric("Стоимость под риском", ValueAtRisk, null,
                $"{Fmt.Money(ValueAtRiskAmount)} на горизонте {Horizon} дн.",
                MetricQuality.Neutral, 4)
            .Metric("Ожидаемые потери в хвосте", ExpectedShortfall, null,
                $"{Fmt.Money(ExpectedShortfallAmount)}; средняя потеря при пробое порога",
                MetricQuality.Neutral, 4)
            .Metric("Отношение хвоста к порогу", TailRatio, "×",
                TailRatio > 1.3 ? "хвост тяжёлый: пробой порога обходится дорого" : "хвост умеренный",
                TailRatio > 1.4 ? MetricQuality.Warning : MetricQuality.Neutral, 2)
            .Metric("Волатильность", Volatility, null, "стандартное отклонение доходности за период",
                MetricQuality.Neutral, 4)
            .Metric("Асимметрия", Skewness, null,
                negativeSkew ? "распределение вытянуто в сторону убытков" : "распределение почти симметрично",
                negativeSkew ? MetricQuality.Warning : MetricQuality.Neutral, 3)
            .Metric("Эксцесс", Kurtosis, null,
                fatTails ? "хвосты тяжелее нормальных" : "хвосты близки к нормальным",
                fatTails ? MetricQuality.Warning : MetricQuality.Good, 2);

        foreach ((VarMethod method, double var, double shortfall) in Comparison)
        {
            builder.Metric(MethodName(method), var, null,
                $"ожидаемые потери в хвосте {Fmt.Pct(shortfall, 2)}",
                method == Method ? MetricQuality.Good : MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Стоимость под риском отвечает на вопрос «сколько мы не потеряем " +
                     "в 99 случаях из 100», но ничего не говорит о сотом случае. " +
                     "Ожидаемые потери в хвосте закрывают именно этот пробел и потому " +
                     "стали основной мерой в банковском регулировании.")
            .FindingIf(Math.Abs(gap) > 0.15,
                $"Исторический и параметрический методы расходятся на {Fmt.Pct(gap, 0)}. " +
                "Это прямое следствие ненормальности доходностей: нормальное " +
                "распределение недооценивает редкие крупные потери.")
            .FindingIf(fatTails,
                $"Эксцесс {Fmt.Num(Kurtosis, 2)} против трёх у нормального закона. " +
                "Параметрическая оценка на таких данных систематически занижает риск; " +
                "используйте исторический метод или поправку Корниша — Фишера.")
            .FindingIf(negativeSkew,
                $"Асимметрия {Fmt.Num(Skewness, 2)}: крупные движения чаще происходят " +
                "вниз, чем вверх. Симметричные модели риска такую особенность не улавливают.")
            .WarningIf(Observations < 250,
                $"Всего {Observations} наблюдений. Для оценки квантиля уровня " +
                $"{Fmt.Pct(Confidence, 0)} это означает опору на единицы наблюдений " +
                "в хвосте — оценка крайне неустойчива.")
            .WarningIf(Horizon > 1 && Method != VarMethod.MonteCarlo,
                $"Горизонт {Horizon} дн. получен масштабированием однодневной оценки " +
                "корнем из времени. Это верно только при независимости доходностей; " +
                "при кластеризации волатильности такой пересчёт занижает риск.")
            .Warning("Любая оценка риска по историческим данным предполагает, что будущее " +
                     "похоже на прошлое. Кризис, которого нет в выборке, из неё " +
                     "не выводится — для этого нужны стресс-тесты.")
            .Recommendation("Отчитывайтесь по ожидаемым потерям в хвосте, а не только " +
                            "по порогу: две позиции с одинаковой стоимостью под риском " +
                            "могут отличаться по хвостовым потерям в разы.")
            .Recommendation("Проверяйте модель обратным тестированием: доля пробоев порога " +
                            "должна соответствовать заявленному уровню доверия.")
            .Build();
    }

    /// <summary>Читаемое название метода.</summary>
    private static string MethodName(VarMethod method) => method switch
    {
        VarMethod.Historical => "исторический",
        VarMethod.Parametric => "параметрический",
        VarMethod.CornishFisher => "Корниш — Фишер",
        _ => "Монте-Карло",
    };
}

/// <summary>
/// Стоимость под риском и ожидаемые потери в хвосте.
/// </summary>
/// <remarks>
/// <para>
/// Стоимость под риском — квантиль распределения убытков: величина, которую
/// потери не превысят с заданной вероятностью. Ожидаемые потери в хвосте —
/// средний убыток при условии, что порог всё-таки пробит:
/// </para>
/// <code>
/// VaR_a  = -quantile(returns, 1 - a)
/// ES_a   = -mean(returns | returns &lt;= quantile(returns, 1 - a))
/// </code>
/// <para>
/// Три способа расчёта различаются предпосылками о распределении. Исторический
/// не делает никаких, но ограничен наблюдённым прошлым. Параметрический
/// предполагает нормальность и потому занижает риск при тяжёлых хвостах.
/// Поправка Корниша — Фишера корректирует нормальный квантиль на асимметрию и
/// эксцесс:
/// </para>
/// <code>
/// z_cf = z + (z^2 - 1) * S / 6 + (z^3 - 3z) * (K - 3) / 24 - (2z^3 - 5z) * S^2 / 36
/// </code>
/// <para>
/// Метод Монте-Карло моделирует доходности с условной волатильностью, что
/// позволяет учесть кластеризацию волатильности — главную причину, по которой
/// однодневная оценка не масштабируется корнем из времени.
/// </para>
/// </remarks>
public static class ValueAtRisk
{
    /// <summary>Рассчитывает стоимость под риском всеми методами.</summary>
    /// <param name="returns">Ряд доходностей портфеля.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <param name="horizon">Горизонт в днях.</param>
    /// <param name="method">Основной метод для итоговой оценки.</param>
    /// <param name="portfolio">Название портфеля.</param>
    /// <returns>Оценки риска всеми методами и характеристики распределения.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно или уровень доверия вне диапазона.</exception>
    public static VarResultSet Compute(
        Vector returns, double portfolioValue = 1, double confidence = 0.99,
        int horizon = 1, VarMethod method = VarMethod.Historical, string portfolio = "портфель")
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 30)
            throw new ArgumentException("Нужно минимум тридцать наблюдений.", nameof(returns));
        if (confidence is <= 0.5 or >= 1)
            throw new ArgumentException("Уровень доверия должен лежать между 0,5 и 1.", nameof(confidence));

        int n = returns.Count;
        double mean = returns.Average();
        double variance = returns.Sum(r => (r - mean) * (r - mean)) / (n - 1);
        double sigma = Math.Sqrt(Math.Max(variance, 0));

        double m3 = returns.Sum(r => Math.Pow(r - mean, 3)) / n;
        double m4 = returns.Sum(r => Math.Pow(r - mean, 4)) / n;

        double skewness = sigma > 0 ? m3 / Math.Pow(sigma, 3) : 0;
        double kurtosis = sigma > 0 ? m4 / Math.Pow(sigma, 4) : 3;

        double scale = Math.Sqrt(horizon);

        var comparison = new List<(VarMethod, double, double)>
        {
            (VarMethod.Historical, HistoricalVar(returns, confidence) * scale,
                HistoricalShortfall(returns, confidence) * scale),
            (VarMethod.Parametric, ParametricVar(mean, sigma, confidence) * scale,
                ParametricShortfall(mean, sigma, confidence) * scale),
            (VarMethod.CornishFisher, CornishFisherVar(mean, sigma, skewness, kurtosis, confidence) * scale,
                CornishFisherVar(mean, sigma, skewness, kurtosis, Math.Min(0.999, confidence + ((1 - confidence) / 2))) * scale),
            (VarMethod.MonteCarlo, 0.0, 0.0),
        };

        (double simulatedVar, double simulatedShortfall) = MonteCarlo(returns, confidence, horizon);
        comparison[3] = (VarMethod.MonteCarlo, simulatedVar, simulatedShortfall);

        (VarMethod _, double var, double shortfall) = comparison.First(c => c.Item1 == method);

        return new VarResultSet
        {
            Portfolio = portfolio,
            Method = method,
            Confidence = confidence,
            Horizon = horizon,
            ValueAtRisk = var,
            ExpectedShortfall = shortfall,
            ValueAtRiskAmount = var * portfolioValue,
            ExpectedShortfallAmount = shortfall * portfolioValue,
            Comparison = comparison,
            Skewness = skewness,
            Kurtosis = kurtosis,
            Volatility = sigma,
            Observations = n,
        };
    }

    /// <summary>Исторический квантиль убытков.</summary>
    /// <param name="returns">Ряд доходностей.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Стоимость под риском в долях.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    public static double HistoricalVar(Vector returns, double confidence)
    {
        ArgumentNullException.ThrowIfNull(returns);

        double[] sorted = [.. returns.OrderBy(r => r)];
        return -EconMath.Quantile(sorted, 1 - confidence);
    }

    /// <summary>Средний убыток за порогом по историческим данным.</summary>
    /// <param name="returns">Ряд доходностей.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Ожидаемые потери в хвосте в долях.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    public static double HistoricalShortfall(Vector returns, double confidence)
    {
        ArgumentNullException.ThrowIfNull(returns);

        double threshold = -HistoricalVar(returns, confidence);
        var tail = returns.Where(r => r <= threshold).ToList();

        return tail.Count > 0 ? -tail.Average() : -threshold;
    }

    /// <summary>Параметрическая оценка в предположении нормальности.</summary>
    /// <param name="mean">Среднее доходности.</param>
    /// <param name="sigma">Стандартное отклонение.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Стоимость под риском в долях.</returns>
    public static double ParametricVar(double mean, double sigma, double confidence) =>
        -(mean + (EconMath.NormalInv(1 - confidence) * sigma));

    /// <summary>Ожидаемые потери в хвосте при нормальном распределении.</summary>
    /// <param name="mean">Среднее доходности.</param>
    /// <param name="sigma">Стандартное отклонение.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Ожидаемые потери в долях.</returns>
    public static double ParametricShortfall(double mean, double sigma, double confidence)
    {
        double z = EconMath.NormalInv(1 - confidence);
        return -(mean - (sigma * EconMath.NormalPdf(z) / (1 - confidence)));
    }

    /// <summary>Оценка с поправкой Корниша — Фишера на асимметрию и эксцесс.</summary>
    /// <param name="mean">Среднее доходности.</param>
    /// <param name="sigma">Стандартное отклонение.</param>
    /// <param name="skewness">Асимметрия.</param>
    /// <param name="kurtosis">Эксцесс.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Стоимость под риском в долях.</returns>
    public static double CornishFisherVar(
        double mean, double sigma, double skewness, double kurtosis, double confidence)
    {
        double z = EconMath.NormalInv(1 - confidence);
        double excess = kurtosis - 3;

        double adjusted = z
            + ((((z * z) - 1) * skewness) / 6)
            + (((z * z * z) - (3 * z)) * excess / 24)
            - ((((2 * z * z * z) - (5 * z)) * skewness * skewness) / 36);

        return -(mean + (adjusted * sigma));
    }

    /// <summary>Оценка методом Монте-Карло с условной волатильностью.</summary>
    private static (double Var, double Shortfall) MonteCarlo(
        Vector returns, double confidence, int horizon, int simulations = 20_000, int seed = 42)
    {
        double sigma;
        double mean = returns.Average();

        try
        {
            GarchResult garch = Garch.Fit(returns, GarchModel.Garch, horizon);
            sigma = garch.Forecast.Count > 0 ? garch.Forecast.Average() : 0;
        }
        catch (ArgumentException)
        {
            sigma = 0;
        }

        if (sigma <= 0)
        {
            double variance = returns.Sum(r => (r - mean) * (r - mean)) / Math.Max(1, returns.Count - 1);
            sigma = Math.Sqrt(Math.Max(variance, 0));
        }

        // Стандартизованные исторические остатки сохраняют форму хвостов
        double[] standardized = [.. returns.Select(r => sigma > 0 ? (r - mean) / sigma : 0)];

        Random rng = RandomEngine.Create(seed);
        var draws = new double[simulations];

        for (int s = 0; s < simulations; s++)
        {
            double total = 0;
            for (int h = 0; h < horizon; h++)
                total += mean + (sigma * standardized[rng.Next(standardized.Length)]);

            draws[s] = total;
        }

        Array.Sort(draws);
        double var = -EconMath.Quantile(draws, 1 - confidence);
        double threshold = -var;

        var tail = draws.Where(d => d <= threshold).ToList();

        return (var, tail.Count > 0 ? -tail.Average() : var);
    }
}

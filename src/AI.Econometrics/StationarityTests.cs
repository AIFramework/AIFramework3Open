using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Econometrics;

/// <summary>Детерминированная часть в тестах на стационарность.</summary>
public enum DeterministicTerms
{
    /// <summary>Без свободного члена и тренда.</summary>
    None,

    /// <summary>Со свободным членом.</summary>
    Constant,

    /// <summary>Со свободным членом и линейным трендом.</summary>
    ConstantAndTrend,
}

/// <summary>Результат теста на единичный корень или стационарность.</summary>
/// <param name="Name">Название теста.</param>
/// <param name="NullHypothesis">Проверяемая нулевая гипотеза.</param>
/// <param name="Statistic">Значение статистики.</param>
/// <param name="CriticalOnePercent">Критическое значение на уровне 1%.</param>
/// <param name="CriticalFivePercent">Критическое значение на уровне 5%.</param>
/// <param name="CriticalTenPercent">Критическое значение на уровне 10%.</param>
/// <param name="Rejected">Отвергнута ли нулевая гипотеза на уровне 5%.</param>
/// <param name="Lags">Число использованных лагов.</param>
public sealed record UnitRootTest(
    string Name, string NullHypothesis, double Statistic,
    double CriticalOnePercent, double CriticalFivePercent, double CriticalTenPercent,
    bool Rejected, int Lags);

/// <summary>Свод проверки ряда на стационарность.</summary>
public sealed record StationarityReport : IInterpretable
{
    /// <summary>Название ряда.</summary>
    public string Series { get; init; } = "ряд";

    /// <summary>Расширенный тест Дики — Фуллера.</summary>
    public UnitRootTest AugmentedDickeyFuller { get; init; } =
        new("ADF", "ряд содержит единичный корень", 0, 0, 0, 0, false, 0);

    /// <summary>Тест Квятковского — Филлипса — Шмидта — Шина.</summary>
    public UnitRootTest Kpss { get; init; } =
        new("KPSS", "ряд стационарен", 0, 0, 0, 0, false, 0);

    /// <summary>Порядок интегрирования, определённый последовательным дифференцированием.</summary>
    public int IntegrationOrder { get; init; }

    /// <summary>Детерминированная часть, использованная в тестах.</summary>
    public DeterministicTerms Terms { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Согласованный вывод о стационарности.</summary>
    public string Verdict =>
        AugmentedDickeyFuller.Rejected && !Kpss.Rejected ? "ряд стационарен"
        : !AugmentedDickeyFuller.Rejected && Kpss.Rejected ? "ряд нестационарен"
        : AugmentedDickeyFuller.Rejected && Kpss.Rejected ? "тесты противоречат: возможен структурный сдвиг"
        : "данных не хватает для вывода";

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool agree = AugmentedDickeyFuller.Rejected != Kpss.Rejected;

        return new InterpretationBuilder($"Стационарность ряда: {Series}")
            .Summary($"Расширенный тест Дики — Фуллера даёт статистику " +
                     $"{Fmt.Num(AugmentedDickeyFuller.Statistic, 3)} при критическом значении " +
                     $"{Fmt.Num(AugmentedDickeyFuller.CriticalFivePercent, 3)}; KPSS — " +
                     $"{Fmt.Num(Kpss.Statistic, 3)} при {Fmt.Num(Kpss.CriticalFivePercent, 3)}. " +
                     $"Согласованный вывод: {Verdict}. Порядок интегрирования {IntegrationOrder}.")
            .Metric("ADF", AugmentedDickeyFuller.Statistic, null,
                $"критическое {Fmt.Num(AugmentedDickeyFuller.CriticalFivePercent, 3)}, " +
                $"лагов {AugmentedDickeyFuller.Lags}; " +
                (AugmentedDickeyFuller.Rejected ? "единичный корень отвергнут" : "единичный корень не отвергнут"),
                AugmentedDickeyFuller.Rejected ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("KPSS", Kpss.Statistic, null,
                $"критическое {Fmt.Num(Kpss.CriticalFivePercent, 3)}, лагов {Kpss.Lags}; " +
                (Kpss.Rejected ? "стационарность отвергнута" : "стационарность не отвергнута"),
                Kpss.Rejected ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Порядок интегрирования", IntegrationOrder, null,
                "сколько раз нужно продифференцировать ряд",
                IntegrationOrder <= 1 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Metric("Наблюдений", Observations, null,
                $"детерминированная часть: {TermsName()}", MetricQuality.Neutral, 0)
            .Finding("Два теста проверяют противоположные гипотезы, и смотреть их нужно " +
                     "вместе. Дики — Фуллер исходит из нестационарности, KPSS — из " +
                     "стационарности; согласованный вывод получается, когда один тест " +
                     "отвергает свою гипотезу, а другой нет.")
            .FindingIf(agree,
                $"Тесты согласованы: {Verdict}.")
            .FindingIf(IntegrationOrder > 0,
                $"Ряд требует {IntegrationOrder}-кратного дифференцирования. Регрессии " +
                "в уровнях на таких данных дают ложную значимость: коэффициенты выглядят " +
                "существенными просто потому, что обе переменные растут.")
            .WarningIf(AugmentedDickeyFuller.Rejected && Kpss.Rejected,
                "Оба теста отвергли свои гипотезы. Обычная причина — структурный сдвиг " +
                "или изменение дисперсии: ряд не описывается ни одной из двух простых моделей.")
            .WarningIf(!AugmentedDickeyFuller.Rejected && !Kpss.Rejected,
                "Ни один тест не отверг свою гипотезу: наблюдений не хватает, чтобы " +
                "различить стационарный ряд с сильной инерцией и случайное блуждание.")
            .WarningIf(Observations < 50,
                $"Всего {Observations} наблюдений. Тесты на единичный корень известны " +
                "низкой мощностью на коротких рядах: они почти всегда не отвергают " +
                "нестационарность.")
            .Warning("Оба теста чувствительны к выбору детерминированной части. Включение " +
                     "тренда там, где его нет, снижает мощность; отсутствие тренда там, " +
                     "где он есть, ведёт к ложному выводу о единичном корне.")
            .Recommendation("Смотрите на график ряда до тестов: структурный сдвиг видно " +
                            "глазами, а тесты на него реагируют выводом о нестационарности.")
            .Recommendation("Для регрессий между нестационарными рядами проверяйте " +
                            "коинтеграцию, а не переходите механически к разностям: " +
                            "разности убирают долгосрочную связь вместе с трендом.")
            .Build();
    }

    /// <summary>Читаемое название детерминированной части.</summary>
    private string TermsName() => Terms switch
    {
        DeterministicTerms.None => "без константы",
        DeterministicTerms.Constant => "константа",
        _ => "константа и тренд",
    };
}

/// <summary>
/// Тесты на стационарность: расширенный Дики — Фуллера и KPSS.
/// </summary>
/// <remarks>
/// <para>
/// Регрессия между нестационарными рядами даёт ложную значимость: две
/// независимые случайные прогулки в среднем показывают высокий R² и значимый
/// коэффициент. Поэтому проверка порядка интегрирования предшествует любой
/// работе с рядами.
/// </para>
/// <para>
/// Расширенный тест Дики — Фуллера оценивает регрессию
/// </para>
/// <code>
/// d y_t = alpha + beta * t + gamma * y_{t-1} + sum_i delta_i * d y_{t-i} + e_t
/// </code>
/// <para>
/// и проверяет <c>gamma = 0</c>. Статистика не имеет стьюдентова распределения,
/// поэтому сравнивается с табличными критическими значениями Дики — Фуллера,
/// зависящими от состава детерминированной части.
/// </para>
/// <para>
/// KPSS проверяет обратную гипотезу — стационарность вокруг константы или
/// тренда — через сумму накопленных остатков, нормированную на долгосрочную
/// дисперсию. Совместное использование двух тестов и даёт содержательный вывод:
/// каждый из них по отдельности не отвергает свою гипотезу слишком часто.
/// </para>
/// </remarks>
public static class StationarityTests
{
    private static readonly double[] AdfNone = [-2.58, -1.95, -1.62];
    private static readonly double[] AdfConstant = [-3.43, -2.86, -2.57];
    private static readonly double[] AdfTrend = [-3.96, -3.41, -3.13];
    private static readonly double[] KpssConstant = [0.739, 0.463, 0.347];
    private static readonly double[] KpssTrend = [0.216, 0.146, 0.119];

    /// <summary>Проверяет ряд на стационарность двумя тестами.</summary>
    /// <param name="series">Временной ряд.</param>
    /// <param name="terms">Детерминированная часть.</param>
    /// <param name="lags">Число лагов; при отрицательном подбирается по правилу Шверта.</param>
    /// <param name="name">Название ряда для отчёта.</param>
    /// <returns>Результаты обоих тестов и порядок интегрирования.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Ряд слишком короткий.</exception>
    public static StationarityReport Analyze(
        Vector series, DeterministicTerms terms = DeterministicTerms.Constant,
        int lags = -1, string name = "ряд")
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count < 12) throw new ArgumentException("Нужно минимум двенадцать наблюдений.", nameof(series));

        UnitRootTest adf = DickeyFuller(series, terms, lags);
        UnitRootTest kpss = Kpss(series, terms, lags);

        int order = 0;
        Vector current = series;

        while (order < 2 && !DickeyFuller(current, terms, lags).Rejected && current.Count > 12)
        {
            var differenced = new Vector(current.Count - 1);
            for (int t = 1; t < current.Count; t++) differenced[t - 1] = current[t] - current[t - 1];

            current = differenced;
            order++;
        }

        return new StationarityReport
        {
            Series = name,
            AugmentedDickeyFuller = adf,
            Kpss = kpss,
            IntegrationOrder = order,
            Terms = terms,
            Observations = series.Count,
        };
    }

    /// <summary>Расширенный тест Дики — Фуллера.</summary>
    /// <param name="series">Временной ряд.</param>
    /// <param name="terms">Детерминированная часть.</param>
    /// <param name="lags">Число лагов; при отрицательном подбирается по правилу Шверта.</param>
    /// <returns>Статистика и критические значения.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    public static UnitRootTest DickeyFuller(
        Vector series, DeterministicTerms terms = DeterministicTerms.Constant, int lags = -1)
    {
        ArgumentNullException.ThrowIfNull(series);

        int n = series.Count;
        int p = lags >= 0 ? lags : Math.Max(0, (int)Math.Floor(12 * Math.Pow(n / 100.0, 0.25)) / 3);
        p = Math.Min(p, Math.Max(0, (n / 4) - 2));

        int rows = n - p - 1;
        if (rows < 8) return new UnitRootTest("ADF", "ряд содержит единичный корень", 0, 0, 0, 0, false, p);

        int deterministic = terms switch
        {
            DeterministicTerms.None => 0,
            DeterministicTerms.Constant => 1,
            _ => 2,
        };

        int k = 1 + p + deterministic;
        var design = new double[rows, k];
        var response = new double[rows];
        var names = new List<string> { "уровень" };

        for (int j = 0; j < p; j++) names.Add($"лаг разности {j + 1}");
        if (deterministic >= 1) names.Add("const");
        if (deterministic == 2) names.Add("тренд");

        for (int i = 0; i < rows; i++)
        {
            int t = i + p + 1;

            design[i, 0] = series[t - 1];
            for (int j = 0; j < p; j++) design[i, 1 + j] = series[t - 1 - j] - series[t - 2 - j];

            if (deterministic >= 1) design[i, 1 + p] = 1;
            if (deterministic == 2) design[i, 2 + p] = t;

            response[i] = series[t] - series[t - 1];
        }

        RegressionResult fit = LinearRegression.FitDesign(
            design, response, names, new RegressionOptions { AddIntercept = false }, "ADF");

        double statistic = fit.Coefficients[0].TStatistic;
        double[] critical = terms switch
        {
            DeterministicTerms.None => AdfNone,
            DeterministicTerms.Constant => AdfConstant,
            _ => AdfTrend,
        };

        return new UnitRootTest(
            "ADF", "ряд содержит единичный корень", statistic,
            critical[0], critical[1], critical[2], statistic < critical[1], p);
    }

    /// <summary>Тест KPSS на стационарность.</summary>
    /// <param name="series">Временной ряд.</param>
    /// <param name="terms">Детерминированная часть: константа или константа с трендом.</param>
    /// <param name="lags">Число лагов для долгосрочной дисперсии; при отрицательном берётся правило Ньюи — Уэста.</param>
    /// <returns>Статистика и критические значения.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    public static UnitRootTest Kpss(
        Vector series, DeterministicTerms terms = DeterministicTerms.Constant, int lags = -1)
    {
        ArgumentNullException.ThrowIfNull(series);

        int n = series.Count;
        bool trend = terms == DeterministicTerms.ConstantAndTrend;
        int p = lags >= 0 ? lags : (int)Math.Floor(4 * Math.Pow(n / 100.0, 0.25));
        p = Math.Clamp(p, 0, Math.Max(0, n / 4));

        var residuals = new double[n];

        if (trend)
        {
            var design = new double[n, 2];
            var response = new double[n];

            for (int t = 0; t < n; t++)
            {
                design[t, 0] = 1;
                design[t, 1] = t + 1;
                response[t] = series[t];
            }

            RegressionResult fit = LinearRegression.FitDesign(
                design, response, ["const", "тренд"],
                new RegressionOptions { AddIntercept = false }, "KPSS");

            for (int t = 0; t < n; t++) residuals[t] = fit.Residuals[t];
        }
        else
        {
            double mean = series.Average();
            for (int t = 0; t < n; t++) residuals[t] = series[t] - mean;
        }

        double partial = 0, numerator = 0;
        for (int t = 0; t < n; t++)
        {
            partial += residuals[t];
            numerator += partial * partial;
        }

        double variance = residuals.Sum(e => e * e) / n;
        for (int l = 1; l <= p; l++)
        {
            double covariance = 0;
            for (int t = l; t < n; t++) covariance += residuals[t] * residuals[t - l];

            variance += 2 * (1 - ((double)l / (p + 1))) * covariance / n;
        }

        double statistic = variance > 0 ? numerator / (n * (double)n * variance) : 0;
        double[] critical = trend ? KpssTrend : KpssConstant;

        return new UnitRootTest(
            "KPSS", "ряд стационарен", statistic,
            critical[0], critical[1], critical[2], statistic > critical[1], p);
    }
}

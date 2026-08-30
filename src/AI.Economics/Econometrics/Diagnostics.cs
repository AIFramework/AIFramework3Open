using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Econometrics;

/// <summary>Результат отдельного диагностического теста.</summary>
/// <param name="Name">Название теста.</param>
/// <param name="NullHypothesis">Проверяемая нулевая гипотеза.</param>
/// <param name="Statistic">Значение статистики.</param>
/// <param name="PValue">Уровень значимости; для тестов без явного распределения равен единице.</param>
/// <param name="Rejected">Отвергнута ли нулевая гипотеза на уровне 5%.</param>
/// <param name="Consequence">Что означает отвержение для практики.</param>
public sealed record DiagnosticTest(
    string Name, string NullHypothesis, double Statistic, double PValue, bool Rejected, string Consequence);

/// <summary>Фактор раздувания дисперсии для одного регрессора.</summary>
/// <param name="Variable">Название регрессора.</param>
/// <param name="Vif">Фактор раздувания дисперсии.</param>
/// <param name="RSquared">Коэффициент детерминации регрессии на остальные регрессоры.</param>
public sealed record VarianceInflation(string Variable, double Vif, double RSquared)
{
    /// <summary>Во сколько раз шире доверительный интервал из-за коллинеарности.</summary>
    public double IntervalInflation => Math.Sqrt(Math.Max(Vif, 1));

    /// <summary>Серьёзна ли коллинеарность по общепринятому порогу.</summary>
    public bool IsSevere => Vif >= 10;
}

/// <summary>Свод диагностики регрессионной модели.</summary>
public sealed record DiagnosticReport : IInterpretable
{
    /// <summary>Название модели.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Выполненные тесты.</summary>
    public IReadOnlyList<DiagnosticTest> Tests { get; init; } = [];

    /// <summary>Факторы раздувания дисперсии по регрессорам.</summary>
    public IReadOnlyList<VarianceInflation> Collinearity { get; init; } = [];

    /// <summary>Статистика Дарбина — Уотсона.</summary>
    public double DurbinWatson { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Тесты, отвергшие нулевую гипотезу.</summary>
    public IReadOnlyList<DiagnosticTest> Failed => [.. Tests.Where(t => t.Rejected)];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        VarianceInflation? worst = Collinearity.OrderByDescending(c => c.Vif).FirstOrDefault();
        bool autocorrelated = DurbinWatson < 1.5 || DurbinWatson > 2.5;
        bool heteroskedastic = Tests.Any(t => t.Rejected && t.Name.Contains("Бройша", StringComparison.Ordinal));

        var builder = new InterpretationBuilder($"Диагностика модели: {Model}")
            .Summary($"Выполнено {Tests.Count} тестов по {Observations} наблюдениям, " +
                     $"нулевая гипотеза отвергнута в {Failed.Count}. " +
                     $"Статистика Дарбина — Уотсона {Fmt.Num(DurbinWatson, 3)} " +
                     $"({(autocorrelated ? "автокорреляция остатков" : "автокорреляции нет")}). " +
                     $"Максимальный фактор раздувания дисперсии {Fmt.Num(worst?.Vif ?? 1, 2)}.")
            .Metric("Тестов провалено", Failed.Count, null, $"из {Tests.Count}",
                Failed.Count == 0 ? MetricQuality.Good
                    : Failed.Count <= 2 ? MetricQuality.Warning : MetricQuality.Critical, 0)
            .Metric("Дарбин — Уотсон", DurbinWatson, null,
                DurbinWatson < 1.5 ? "положительная автокорреляция"
                    : DurbinWatson > 2.5 ? "отрицательная автокорреляция" : "автокорреляции нет",
                autocorrelated ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Максимальный VIF", worst?.Vif ?? 1, null,
                worst is not null ? $"регрессор «{worst.Variable}»" : "коллинеарность не оценивалась",
                (worst?.Vif ?? 1) >= 10 ? MetricQuality.Critical
                    : (worst?.Vif ?? 1) >= 5 ? MetricQuality.Warning : MetricQuality.Good, 2);

        foreach (DiagnosticTest test in Tests)
        {
            builder.Metric(test.Name, test.Statistic, null,
                $"p = {Fmt.Num(test.PValue, 4)}; {(test.Rejected ? test.Consequence : "гипотеза не отвергается")}",
                test.Rejected ? MetricQuality.Warning : MetricQuality.Good, 3);
        }

        foreach (VarianceInflation vif in Collinearity)
        {
            builder.Metric($"VIF: {vif.Variable}", vif.Vif, null,
                $"интервал шире в {Fmt.Num(vif.IntervalInflation, 2)} раза, R² на остальные {Fmt.Num(vif.RSquared, 3)}",
                vif.IsSevere ? MetricQuality.Warning : MetricQuality.Good, 2);
        }

        return builder
            .FindingIf(heteroskedastic,
                "Гетероскедастичность обнаружена. Оценки коэффициентов остаются несмещёнными, " +
                "но классические стандартные ошибки неверны — пересчитайте их устойчивыми " +
                "по Уайту.")
            .FindingIf(autocorrelated,
                $"Дарбин — Уотсон {Fmt.Num(DurbinWatson, 3)} указывает на автокорреляцию остатков. " +
                "Для рядов это обычно означает пропущенную динамику: лаг отклика, тренд " +
                "или сезонность. Ошибки Ньюи — Уэста лечат последствие, но не причину.")
            .FindingIf(worst is not null && worst.IsSevere,
                $"Регрессор «{worst?.Variable}» коллинеарен остальным: VIF {Fmt.Num(worst?.Vif ?? 0, 1)}, " +
                $"доверительный интервал шире в {Fmt.Num(worst?.IntervalInflation ?? 1, 1)} раза. " +
                "Коллинеарность не смещает оценки — она делает их неразличимыми между собой.")
            .FindingIf(Failed.Count == 0,
                "Ни один тест не отверг свою нулевую гипотезу. Это не доказывает корректность " +
                "модели: тесты проверяют конкретные нарушения, а не спецификацию в целом.")
            .WarningIf(Tests.Any(t => t.Rejected && t.Name.Contains("RESET", StringComparison.Ordinal)),
                "Тест RESET отверг линейность. Скорее всего пропущены нелинейные члены или " +
                "взаимодействия; устойчивые ошибки эту проблему не решают.")
            .WarningIf(Tests.Any(t => t.Rejected && t.Name.Contains("Чоу", StringComparison.Ordinal)),
                "Тест Чоу зафиксировал структурный сдвиг: коэффициенты различаются между " +
                "подвыборками. Единая модель на всём периоде описывает смесь двух режимов.")
            .Warning("Диагностика проверяет предпосылки, а не причинность. Модель может " +
                     "пройти все тесты и при этом давать бессмысленные с экономической " +
                     "точки зрения коэффициенты из-за пропущенной переменной.")
            .Recommendation("Начинайте с гетероскедастичности и автокорреляции: они меняют " +
                            "выводы о значимости, а лечатся заменой формулы стандартных ошибок.")
            .Recommendation("Отвержение RESET или Чоу означает ошибку спецификации — здесь " +
                            "нужно менять модель, а не способ оценки ошибок.")
            .Build();
    }
}

/// <summary>
/// Диагностика регрессионной модели: гетероскедастичность, автокорреляция,
/// нормальность, нелинейность, структурный сдвиг и коллинеарность.
/// </summary>
/// <remarks>
/// <para>
/// Нарушения предпосылок делятся на два класса, и лечатся они по-разному.
/// Гетероскедастичность и автокорреляция не смещают оценки коэффициентов —
/// они делают неверными стандартные ошибки, и достаточно сменить формулу
/// ковариационной матрицы. Нелинейность и структурный сдвиг — это ошибки
/// спецификации: здесь нужно менять саму модель.
/// </para>
/// <para>
/// Реализованы тесты Бройша — Пагана и Уайта на гетероскедастичность,
/// Дарбина — Уотсона на автокорреляцию первого порядка, Жарка — Бера на
/// нормальность остатков, RESET Рамсея на пропущенную нелинейность, Чоу на
/// структурный сдвиг и факторы раздувания дисперсии на коллинеарность.
/// </para>
/// </remarks>
public static class Diagnostics
{
    /// <summary>Выполняет полную диагностику модели.</summary>
    /// <param name="x">Матрица регрессоров без свободного члена.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <param name="names">Названия регрессоров.</param>
    /// <param name="breakPoint">Точка структурного сдвига для теста Чоу; при <c>null</c> берётся середина выборки.</param>
    /// <returns>Свод диагностических тестов.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static DiagnosticReport Run(
        Matrix x, Vector y, IReadOnlyList<string>? names = null, int? breakPoint = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        RegressionResult fit = LinearRegression.Fit(x, y, names);

        var tests = new List<DiagnosticTest>
        {
            BreuschPagan(x, fit.Residuals),
            White(x, fit.Residuals),
            JarqueBera(fit.Residuals),
            Reset(x, y),
        };

        int split = breakPoint ?? (x.Height / 2);
        if (split > x.Width + 2 && x.Height - split > x.Width + 2)
            tests.Add(Chow(x, y, split));

        return new DiagnosticReport
        {
            Model = fit.Model,
            Tests = tests,
            Collinearity = VarianceInflationFactors(x, names),
            DurbinWatson = DurbinWatsonStatistic(fit.Residuals),
            Observations = x.Height,
        };
    }

    /// <summary>Тест Бройша — Пагана на гетероскедастичность.</summary>
    /// <param name="x">Матрица регрессоров.</param>
    /// <param name="residuals">Остатки модели.</param>
    /// <returns>Статистика, p-значение и вывод.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static DiagnosticTest BreuschPagan(Matrix x, Vector residuals)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(residuals);

        int n = residuals.Count;
        var squared = new Vector(n);
        double meanSquare = 0;

        for (int i = 0; i < n; i++) meanSquare += residuals[i] * residuals[i];
        meanSquare /= n;

        for (int i = 0; i < n; i++) squared[i] = residuals[i] * residuals[i] / Math.Max(meanSquare, 1e-300);

        RegressionResult auxiliary = LinearRegression.Fit(x, squared);
        double statistic = 0.5 * auxiliary.RSquared * n;
        double p = Distributions.ChiSquarePValue(statistic, Math.Max(1, x.Width));

        return new DiagnosticTest(
            "Бройша — Пагана", "дисперсия ошибки постоянна", statistic, p, p < 0.05,
            "дисперсия ошибки зависит от регрессоров: нужны устойчивые стандартные ошибки");
    }

    /// <summary>Тест Уайта на гетероскедастичность общего вида.</summary>
    /// <param name="x">Матрица регрессоров.</param>
    /// <param name="residuals">Остатки модели.</param>
    /// <returns>Статистика, p-значение и вывод.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static DiagnosticTest White(Matrix x, Vector residuals)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(residuals);

        int n = x.Height, k = x.Width;
        int extended = k + (k * (k + 1) / 2);
        var design = new Matrix(n, extended);

        for (int i = 0; i < n; i++)
        {
            int column = 0;
            for (int j = 0; j < k; j++) design[i, column++] = x[i, j];

            // Квадраты и попарные произведения регрессоров: гетероскедастичность
            // общего вида не обязана быть линейной по исходным переменным
            for (int a = 0; a < k; a++)
                for (int b = a; b < k; b++) design[i, column++] = x[i, a] * x[i, b];
        }

        var squared = new Vector(n);
        for (int i = 0; i < n; i++) squared[i] = residuals[i] * residuals[i];

        if (n <= extended + 2)
        {
            return new DiagnosticTest(
                "Уайта", "дисперсия ошибки постоянна", double.NaN, 1, false,
                "наблюдений не хватает для теста общего вида");
        }

        RegressionResult auxiliary = LinearRegression.Fit(design, squared);
        double statistic = n * auxiliary.RSquared;
        double p = Distributions.ChiSquarePValue(statistic, extended);

        return new DiagnosticTest(
            "Уайта", "дисперсия ошибки постоянна", statistic, p, p < 0.05,
            "дисперсия зависит от регрессоров нелинейно: используйте ошибки HC3");
    }

    /// <summary>Статистика Дарбина — Уотсона.</summary>
    /// <param name="residuals">Остатки модели.</param>
    /// <returns>Значение статистики; около двух означает отсутствие автокорреляции.</returns>
    /// <exception cref="ArgumentNullException">Остатки не заданы.</exception>
    public static double DurbinWatsonStatistic(Vector residuals)
    {
        ArgumentNullException.ThrowIfNull(residuals);

        double numerator = 0, denominator = 0;

        for (int i = 0; i < residuals.Count; i++)
        {
            denominator += residuals[i] * residuals[i];
            if (i > 0)
            {
                double difference = residuals[i] - residuals[i - 1];
                numerator += difference * difference;
            }
        }

        return denominator > 0 ? numerator / denominator : 2;
    }

    /// <summary>Тест Жарка — Бера на нормальность остатков.</summary>
    /// <param name="residuals">Остатки модели.</param>
    /// <returns>Статистика, p-значение и вывод.</returns>
    /// <exception cref="ArgumentNullException">Остатки не заданы.</exception>
    public static DiagnosticTest JarqueBera(Vector residuals)
    {
        ArgumentNullException.ThrowIfNull(residuals);

        int n = residuals.Count;
        double mean = residuals.Average();
        double m2 = 0, m3 = 0, m4 = 0;

        for (int i = 0; i < n; i++)
        {
            double d = residuals[i] - mean;
            double d2 = d * d;
            m2 += d2;
            m3 += d2 * d;
            m4 += d2 * d2;
        }

        m2 /= n; m3 /= n; m4 /= n;

        double skewness = m2 > 0 ? m3 / Math.Pow(m2, 1.5) : 0;
        double kurtosis = m2 > 0 ? m4 / (m2 * m2) : 3;
        double statistic = n / 6.0 * ((skewness * skewness) + ((kurtosis - 3) * (kurtosis - 3) / 4.0));
        double p = Distributions.ChiSquarePValue(statistic, 2);

        return new DiagnosticTest(
            "Жарка — Бера", "остатки распределены нормально", statistic, p, p < 0.05,
            "остатки ненормальны: доверительные интервалы на малых выборках неточны");
    }

    /// <summary>Тест RESET Рамсея на пропущенную нелинейность.</summary>
    /// <param name="x">Матрица регрессоров.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <returns>Статистика, p-значение и вывод.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static DiagnosticTest Reset(Matrix x, Vector y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        RegressionResult baseline = LinearRegression.Fit(x, y);
        int n = x.Height, k = x.Width;

        if (n <= k + 4)
        {
            return new DiagnosticTest(
                "RESET Рамсея", "модель линейна по регрессорам", double.NaN, 1, false,
                "наблюдений не хватает для теста");
        }

        var extended = new Matrix(n, k + 2);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < k; j++) extended[i, j] = x[i, j];

            double fitted = baseline.Fitted[i];
            extended[i, k] = fitted * fitted;
            extended[i, k + 1] = fitted * fitted * fitted;
        }

        RegressionResult augmented = LinearRegression.Fit(extended, y);

        double rssRestricted = Rss(baseline.Residuals);
        double rssUnrestricted = Rss(augmented.Residuals);
        int df = n - k - 3;

        double statistic = df > 0 && rssUnrestricted > 0
            ? (rssRestricted - rssUnrestricted) / 2 / (rssUnrestricted / df)
            : double.NaN;

        double p = double.IsNaN(statistic) ? 1 : Distributions.FPValue(statistic, 2, df);

        return new DiagnosticTest(
            "RESET Рамсея", "модель линейна по регрессорам",
            double.IsNaN(statistic) ? 0 : statistic, p, p < 0.05,
            "спецификация неверна: пропущены нелинейные члены или взаимодействия");
    }

    /// <summary>Тест Чоу на структурный сдвиг.</summary>
    /// <param name="x">Матрица регрессоров.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <param name="breakPoint">Номер наблюдения, с которого начинается второй режим.</param>
    /// <returns>Статистика, p-значение и вывод.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Точка сдвига вне допустимого диапазона.</exception>
    public static DiagnosticTest Chow(Matrix x, Vector y, int breakPoint)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        int n = x.Height, k = x.Width + 1;
        ArgumentOutOfRangeException.ThrowIfLessThan(breakPoint, k + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(breakPoint, n - k - 1);

        RegressionResult pooled = LinearRegression.Fit(x, y);
        RegressionResult first = LinearRegression.Fit(Slice(x, 0, breakPoint), Slice(y, 0, breakPoint));
        RegressionResult second = LinearRegression.Fit(Slice(x, breakPoint, n), Slice(y, breakPoint, n));

        double rssPooled = Rss(pooled.Residuals);
        double rssSplit = Rss(first.Residuals) + Rss(second.Residuals);
        int df = n - (2 * k);

        double statistic = df > 0 && rssSplit > 0
            ? (rssPooled - rssSplit) / k / (rssSplit / df)
            : double.NaN;

        double p = double.IsNaN(statistic) ? 1 : Distributions.FPValue(statistic, k, df);

        return new DiagnosticTest(
            "Чоу", "коэффициенты одинаковы в обеих подвыборках",
            double.IsNaN(statistic) ? 0 : statistic, p, p < 0.05,
            "структурный сдвиг: единая модель описывает смесь двух режимов");
    }

    /// <summary>Факторы раздувания дисперсии по регрессорам.</summary>
    /// <param name="x">Матрица регрессоров.</param>
    /// <param name="names">Названия регрессоров.</param>
    /// <returns>Фактор для каждого регрессора.</returns>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    public static IReadOnlyList<VarianceInflation> VarianceInflationFactors(
        Matrix x, IReadOnlyList<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(x);

        int n = x.Height, k = x.Width;
        if (k < 2) return [];

        var factors = new List<VarianceInflation>(k);

        for (int target = 0; target < k; target++)
        {
            var others = new Matrix(n, k - 1);
            var response = new Vector(n);

            for (int i = 0; i < n; i++)
            {
                int column = 0;
                for (int j = 0; j < k; j++)
                {
                    if (j == target) continue;
                    others[i, column++] = x[i, j];
                }

                response[i] = x[i, target];
            }

            RegressionResult auxiliary = LinearRegression.Fit(others, response);
            double rSquared = Math.Clamp(auxiliary.RSquared, 0, 0.999999);

            factors.Add(new VarianceInflation(
                names is not null && target < names.Count ? names[target] : $"x{target + 1}",
                1 / (1 - rSquared), rSquared));
        }

        return factors;
    }

    /// <summary>Сумма квадратов остатков.</summary>
    private static double Rss(Vector residuals)
    {
        double sum = 0;
        for (int i = 0; i < residuals.Count; i++) sum += residuals[i] * residuals[i];
        return sum;
    }

    /// <summary>Подматрица по строкам.</summary>
    private static Matrix Slice(Matrix source, int from, int to)
    {
        var slice = new Matrix(to - from, source.Width);
        for (int i = from; i < to; i++)
            for (int j = 0; j < source.Width; j++) slice[i - from, j] = source[i, j];

        return slice;
    }

    /// <summary>Подвектор.</summary>
    private static Vector Slice(Vector source, int from, int to)
    {
        var slice = new Vector(to - from);
        for (int i = from; i < to; i++) slice[i - from] = source[i];
        return slice;
    }
}

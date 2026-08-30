using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;
using AI.Statistics;

namespace AI.Economics.Risk;

/// <summary>Семейство копул.</summary>
public enum CopulaFamily
{
    /// <summary>Гауссова: симметричная зависимость без хвостовой связи.</summary>
    Gaussian,

    /// <summary>Стьюдента: симметричная зависимость с общей хвостовой связью.</summary>
    StudentT,

    /// <summary>Клейтона: усиленная зависимость в нижнем хвосте.</summary>
    Clayton,

    /// <summary>Гумбеля: усиленная зависимость в верхнем хвосте.</summary>
    Gumbel,
}

/// <summary>Результат подгонки копулы к паре рядов.</summary>
public sealed record CopulaResult : IInterpretable
{
    /// <summary>Названия рядов.</summary>
    public (string First, string Second) Series { get; init; } = ("первый", "второй");

    /// <summary>Семейство копулы.</summary>
    public CopulaFamily Family { get; init; }

    /// <summary>Параметр зависимости.</summary>
    public double Parameter { get; init; }

    /// <summary>Число степеней свободы для копулы Стьюдента.</summary>
    public double DegreesOfFreedom { get; init; }

    /// <summary>Ранговая корреляция Кендалла.</summary>
    public double KendallTau { get; init; }

    /// <summary>Линейная корреляция Пирсона.</summary>
    public double PearsonCorrelation { get; init; }

    /// <summary>Коэффициент зависимости в нижнем хвосте.</summary>
    public double LowerTailDependence { get; init; }

    /// <summary>Коэффициент зависимости в верхнем хвосте.</summary>
    public double UpperTailDependence { get; init; }

    /// <summary>Логарифм правдоподобия.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Информационный критерий Акаике.</summary>
    public double Aic { get; init; }

    /// <summary>Правдоподобие всех семейств для сравнения.</summary>
    public IReadOnlyList<(CopulaFamily Family, double LogLikelihood, double Aic)> Comparison { get; init; } = [];

    /// <summary>Наблюдаемая доля совместных экстремумов в нижнем хвосте.</summary>
    public double EmpiricalLowerTail { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool asymmetric = Math.Abs(LowerTailDependence - UpperTailDependence) > 0.05;
        bool tailRisk = LowerTailDependence > 0.1;

        (CopulaFamily Family, double LogLikelihood, double Aic) best =
            Comparison.OrderBy(c => c.Aic).FirstOrDefault();

        var builder = new InterpretationBuilder($"Копула: {Series.First} и {Series.Second}")
            .Summary($"Семейство {FamilyName(Family)}, параметр {Fmt.Num(Parameter, 3)}. " +
                     $"Ранговая корреляция {Fmt.Num(KendallTau, 3)}, линейная " +
                     $"{Fmt.Num(PearsonCorrelation, 3)}. Зависимость в нижнем хвосте " +
                     $"{Fmt.Num(LowerTailDependence, 3)}, в верхнем {Fmt.Num(UpperTailDependence, 3)}. " +
                     $"По информационному критерию лучше всего подходит {FamilyName(best.Family)}.")
            .Metric("Параметр зависимости", Parameter, null, "в шкале выбранного семейства",
                MetricQuality.Neutral, 4)
            .Metric("Корреляция Кендалла", KendallTau, null,
                "ранговая мера, инвариантная к монотонным преобразованиям",
                MetricQuality.Neutral, 3)
            .Metric("Зависимость в нижнем хвосте", LowerTailDependence, null,
                tailRisk ? "падения происходят совместно" : "совместных обвалов модель не предполагает",
                tailRisk ? MetricQuality.Warning : MetricQuality.Neutral, 3)
            .Metric("Зависимость в верхнем хвосте", UpperTailDependence, null,
                "вероятность совместного роста в экстремуме", MetricQuality.Neutral, 3)
            .Metric("Наблюдаемая связь хвостов", EmpiricalLowerTail, null,
                "доля совместных падений в худшем дециле", MetricQuality.Neutral, 3)
            .Metric("AIC", Aic, null, $"логарифм правдоподобия {Fmt.Num(LogLikelihood, 1)}",
                MetricQuality.Neutral, 1);

        foreach ((CopulaFamily family, double logLikelihood, double aic) in Comparison)
        {
            builder.Metric(FamilyName(family), aic, null,
                $"логарифм правдоподобия {Fmt.Num(logLikelihood, 1)}",
                family == best.Family ? MetricQuality.Good : MetricQuality.Unknown, 1);
        }

        return builder
            .Finding("Копула отделяет структуру зависимости от распределений отдельных " +
                     "переменных. Это принципиально для риска: два портфеля с одинаковой " +
                     "корреляцией могут вести себя совершенно по-разному в кризис.")
            .FindingIf(tailRisk,
                $"Зависимость в нижнем хвосте {Fmt.Num(LowerTailDependence, 3)}: активы " +
                "падают вместе. Гауссова копула такую связь исключает по построению, " +
                "и модели на её основе систематически занижают риск портфеля в кризис.")
            .FindingIf(asymmetric,
                "Зависимость асимметрична: связь в падениях и в росте различается. " +
                "Симметричные семейства — гауссово и Стьюдента — такую асимметрию " +
                "воспроизвести не могут.")
            .FindingIf(best.Family != Family,
                $"По информационному критерию лучше подходит семейство {FamilyName(best.Family)}. " +
                "Стоит пересчитать риск на нём и сравнить результат.")
            .FindingIf(Math.Abs(PearsonCorrelation - KendallTau) > 0.15,
                "Линейная и ранговая корреляции заметно расходятся: зависимость нелинейна, " +
                "и коэффициент Пирсона описывает её плохо.")
            .WarningIf(Observations < 250,
                $"Всего {Observations} наблюдений. Хвостовая зависимость оценивается " +
                "по считанным совместным экстремумам и на такой выборке крайне неустойчива.")
            .WarningIf(Family == CopulaFamily.Gaussian && EmpiricalLowerTail > 0.15,
                $"Наблюдаемая доля совместных падений {Fmt.Pct(EmpiricalLowerTail, 0)} " +
                "заметно выше нуля, а гауссова копула хвостовой зависимости не допускает. " +
                "Для оценки риска портфеля это опасное сочетание.")
            .Warning("Параметры оценены по псевдонаблюдениям — эмпирическим рангам. " +
                     "Стандартные ошибки в этом случае занижены, и различие между " +
                     "близкими семействами по правдоподобию не следует считать значимым.")
            .Recommendation("Проверяйте хвостовую зависимость до выбора семейства: она " +
                            "определяет поведение портфеля в кризис сильнее, чем корреляция.")
            .Recommendation("Для портфельного риска используйте копулу вместе с раздельно " +
                            "подогнанными хвостами активов: это и есть практический смысл " +
                            "разделения структуры зависимости и маргиналов.")
            .Build();
    }

    /// <summary>Читаемое название семейства.</summary>
    private static string FamilyName(CopulaFamily family) => family switch
    {
        CopulaFamily.Gaussian => "гауссова",
        CopulaFamily.StudentT => "Стьюдента",
        CopulaFamily.Clayton => "Клейтона",
        _ => "Гумбеля",
    };
}

/// <summary>
/// Копулы: моделирование структуры зависимости отдельно от распределений.
/// </summary>
/// <remarks>
/// <para>
/// По теореме Склара совместное распределение раскладывается на маргинальные
/// распределения и копулу — функцию, связывающую их равномерные преобразования:
/// </para>
/// <code>
/// F(x, y) = C( F_1(x), F_2(y) )
/// </code>
/// <para>
/// Для риска решающее значение имеет хвостовая зависимость — вероятность
/// совместного экстремума. Гауссова копула её не допускает вовсе: при любой
/// корреляции меньше единицы вероятность совместного обвала стремится к нулю.
/// Именно это свойство сделало её печально известной в оценке структурных
/// продуктов.
/// </para>
/// <para>
/// Семейства различаются формой этой зависимости:
/// </para>
/// <code>
/// Стьюдента: симметричная связь обоих хвостов
/// Клейтона:  lambda_L = 2^(-1/theta),  верхний хвост независим
/// Гумбеля:   lambda_U = 2 - 2^(1/theta), нижний хвост независим
/// </code>
/// <para>
/// Параметры оцениваются по псевдонаблюдениям — эмпирическим рангам, что
/// избавляет от необходимости задавать маргинальные распределения.
/// </para>
/// </remarks>
public static class Copulas
{
    /// <summary>Подгоняет копулу к паре рядов и сравнивает семейства.</summary>
    /// <param name="first">Первый ряд.</param>
    /// <param name="second">Второй ряд.</param>
    /// <param name="family">Семейство для основного результата.</param>
    /// <param name="names">Названия рядов.</param>
    /// <returns>Параметры зависимости и хвостовые коэффициенты.</returns>
    /// <exception cref="ArgumentNullException">Ряды не заданы.</exception>
    /// <exception cref="ArgumentException">Длины не совпадают или наблюдений мало.</exception>
    public static CopulaResult Fit(
        Vector first, Vector second, CopulaFamily family = CopulaFamily.StudentT,
        (string First, string Second)? names = null)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count != second.Count)
            throw new ArgumentException("Ряды должны быть одной длины.", nameof(second));
        if (first.Count < 50)
            throw new ArgumentException("Нужно минимум пятьдесят наблюдений.", nameof(first));

        int n = first.Count;
        double[] u = PseudoObservations(first);
        double[] v = PseudoObservations(second);

        double tau = KendallTau(first, second);
        double pearson = Pearson(first, second);

        var comparison = new List<(CopulaFamily, double, double)>();

        foreach (CopulaFamily candidate in Enum.GetValues<CopulaFamily>())
        {
            (double parameter, double df, double logLikelihood) = FitFamily(u, v, candidate, tau);
            int parameters = candidate == CopulaFamily.StudentT ? 2 : 1;

            comparison.Add((candidate, logLikelihood, (-2 * logLikelihood) + (2 * parameters)));
        }

        (double theta, double degrees, double likelihood) = FitFamily(u, v, family, tau);
        (double lower, double upper) = TailDependence(family, theta, degrees);

        // Наблюдаемая доля совместных попаданий в худший дециль
        double empirical = 0;
        int lowCount = 0;

        for (int i = 0; i < n; i++)
        {
            if (u[i] > 0.1) continue;

            lowCount++;
            if (v[i] <= 0.1) empirical++;
        }

        return new CopulaResult
        {
            Series = names ?? ("первый", "второй"),
            Family = family,
            Parameter = theta,
            DegreesOfFreedom = degrees,
            KendallTau = tau,
            PearsonCorrelation = pearson,
            LowerTailDependence = lower,
            UpperTailDependence = upper,
            LogLikelihood = likelihood,
            Aic = (-2 * likelihood) + (2 * (family == CopulaFamily.StudentT ? 2 : 1)),
            Comparison = comparison,
            EmpiricalLowerTail = lowCount > 0 ? empirical / lowCount : 0,
            Observations = n,
        };
    }

    /// <summary>Моделирует пары равномерных величин с заданной структурой зависимости.</summary>
    /// <param name="family">Семейство копулы.</param>
    /// <param name="parameter">Параметр зависимости.</param>
    /// <param name="count">Число пар.</param>
    /// <param name="degreesOfFreedom">Число степеней свободы для копулы Стьюдента.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Матрица пар в единичном квадрате.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Число пар неположительно.</exception>
    public static Matrix Simulate(
        CopulaFamily family, double parameter, int count, double degreesOfFreedom = 5, int seed = 42)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        Random rng = RandomEngine.Create(seed);
        var sample = new Matrix(count, 2);

        for (int i = 0; i < count; i++)
        {
            (double u, double v) = family switch
            {
                CopulaFamily.Gaussian => SimulateGaussian(rng, parameter),
                CopulaFamily.StudentT => SimulateStudent(rng, parameter, degreesOfFreedom),
                CopulaFamily.Clayton => SimulateClayton(rng, parameter),
                _ => SimulateGumbel(rng, parameter),
            };

            sample[i, 0] = Math.Clamp(u, 1e-9, 1 - 1e-9);
            sample[i, 1] = Math.Clamp(v, 1e-9, 1 - 1e-9);
        }

        return sample;
    }

    /// <summary>Ранговая корреляция Кендалла.</summary>
    /// <param name="first">Первый ряд.</param>
    /// <param name="second">Второй ряд.</param>
    /// <returns>Коэффициент от минус единицы до единицы.</returns>
    /// <exception cref="ArgumentNullException">Ряды не заданы.</exception>
    public static double KendallTau(Vector first, Vector second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        int n = Math.Min(first.Count, second.Count);
        long concordant = 0, discordant = 0;

        for (int i = 0; i < n - 1; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double a = (first[i] - first[j]) * (second[i] - second[j]);
                if (a > 0) concordant++;
                else if (a < 0) discordant++;
            }
        }

        long total = concordant + discordant;
        return total > 0 ? (double)(concordant - discordant) / total : 0;
    }

    /// <summary>Преобразует ряд в псевдонаблюдения по эмпирическим рангам.</summary>
    private static double[] PseudoObservations(Vector series)
    {
        int n = series.Count;
        var order = Enumerable.Range(0, n).OrderBy(i => series[i]).ToArray();
        var ranks = new double[n];

        for (int r = 0; r < n; r++) ranks[order[r]] = (r + 1.0) / (n + 1.0);

        return ranks;
    }

    /// <summary>Линейная корреляция.</summary>
    private static double Pearson(Vector first, Vector second)
    {
        int n = Math.Min(first.Count, second.Count);
        double meanFirst = first.Average(), meanSecond = second.Average();
        double covariance = 0, varianceFirst = 0, varianceSecond = 0;

        for (int i = 0; i < n; i++)
        {
            double a = first[i] - meanFirst, b = second[i] - meanSecond;
            covariance += a * b;
            varianceFirst += a * a;
            varianceSecond += b * b;
        }

        double denominator = Math.Sqrt(varianceFirst * varianceSecond);
        return denominator > 0 ? covariance / denominator : 0;
    }

    /// <summary>Оценивает параметр семейства по методу максимального правдоподобия.</summary>
    private static (double Parameter, double DegreesOfFreedom, double LogLikelihood) FitFamily(
        double[] u, double[] v, CopulaFamily family, double tau)
    {
        // Инверсия связи параметра с корреляцией Кендалла даёт начальное приближение
        double start = family switch
        {
            CopulaFamily.Gaussian or CopulaFamily.StudentT => Math.Sin(Math.PI * tau / 2),
            CopulaFamily.Clayton => Math.Max(2 * tau / (1 - Math.Max(tau, 0)), 0.05),
            _ => Math.Max(1 / (1 - Math.Max(tau, 0)), 1.01),
        };

        double Negative(double[] p)
        {
            double theta = Transform(p[0], family);
            double df = family == CopulaFamily.StudentT ? 2 + Math.Exp(Math.Clamp(p[1], -5, 4)) : 5;
            double total = 0;

            for (int i = 0; i < u.Length; i++)
            {
                double density = Density(u[i], v[i], theta, df, family);
                if (density <= 0 || !double.IsFinite(density)) return double.MaxValue;

                total += Math.Log(density);
            }

            return double.IsFinite(total) ? -total : double.MaxValue;
        }

        double[] initial = family == CopulaFamily.StudentT
            ? [Inverse(start, family), Math.Log(3.0)]
            : [Inverse(start, family)];

        double[] estimate = NelderMead.Minimize(Negative, initial, 2000);

        double parameter = Transform(estimate[0], family);
        double degrees = family == CopulaFamily.StudentT
            ? 2 + Math.Exp(Math.Clamp(estimate[1], -5, 4))
            : double.NaN;

        return (parameter, degrees, -Negative(estimate));
    }

    /// <summary>Переводит параметр из пространства оптимизации в допустимую область.</summary>
    private static double Transform(double raw, CopulaFamily family) => family switch
    {
        CopulaFamily.Gaussian or CopulaFamily.StudentT => Math.Tanh(raw),
        CopulaFamily.Clayton => Math.Exp(Math.Clamp(raw, -8, 4)),
        _ => 1 + Math.Exp(Math.Clamp(raw, -8, 4)),
    };

    /// <summary>Обратное преобразование параметра.</summary>
    private static double Inverse(double parameter, CopulaFamily family) => family switch
    {
        CopulaFamily.Gaussian or CopulaFamily.StudentT => Atanh(Math.Clamp(parameter, -0.99, 0.99)),
        CopulaFamily.Clayton => Math.Log(Math.Max(parameter, 0.05)),
        _ => Math.Log(Math.Max(parameter - 1, 0.05)),
    };

    /// <summary>Ареатангенс.</summary>
    private static double Atanh(double x) => 0.5 * Math.Log((1 + x) / (1 - x));

    /// <summary>Плотность копулы в точке.</summary>
    private static double Density(double u, double v, double theta, double df, CopulaFamily family)
    {
        switch (family)
        {
            case CopulaFamily.Gaussian:
            {
                double x = EconMath.NormalInv(u), y = EconMath.NormalInv(v);
                double r2 = theta * theta;
                if (r2 >= 1) return 0;

                return Math.Exp(-((r2 * ((x * x) + (y * y))) - (2 * theta * x * y)) / (2 * (1 - r2)))
                    / Math.Sqrt(1 - r2);
            }

            case CopulaFamily.StudentT:
            {
                double x = StudentQuantile(u, df), y = StudentQuantile(v, df);
                double r2 = theta * theta;
                if (r2 >= 1) return 0;

                double quadratic = ((x * x) - (2 * theta * x * y) + (y * y)) / (1 - r2);
                double numerator = Math.Exp(EconMath.LogGamma((df + 2) / 2) + EconMath.LogGamma(df / 2))
                    * Math.Pow(1 + (quadratic / df), -(df + 2) / 2);
                double denominator = Math.Sqrt(1 - r2) * Math.Exp(2 * EconMath.LogGamma((df + 1) / 2))
                    * Math.Pow(1 + (x * x / df), -(df + 1) / 2)
                    * Math.Pow(1 + (y * y / df), -(df + 1) / 2);

                return denominator > 0 ? numerator / denominator : 0;
            }

            case CopulaFamily.Clayton:
            {
                if (theta <= 0) return 1;

                double term = Math.Pow(u, -theta) + Math.Pow(v, -theta) - 1;
                if (term <= 0) return 0;

                return (1 + theta) * Math.Pow(u * v, -theta - 1) * Math.Pow(term, -(1 / theta) - 2);
            }

            default:
            {
                if (theta <= 1) return 1;

                double a = Math.Pow(-Math.Log(u), theta);
                double b = Math.Pow(-Math.Log(v), theta);
                double s = Math.Pow(a + b, 1.0 / theta);

                if (!double.IsFinite(s) || s <= 0) return 0;

                return Math.Exp(-s) / (u * v)
                    * Math.Pow(a + b, (2 / theta) - 2)
                    * Math.Pow(Math.Log(u) * Math.Log(v), theta - 1)
                    * (1 + ((theta - 1) / s));
            }
        }
    }

    /// <summary>Хвостовые коэффициенты зависимости семейства.</summary>
    private static (double Lower, double Upper) TailDependence(
        CopulaFamily family, double theta, double df) => family switch
    {
        CopulaFamily.Gaussian => (0, 0),
        CopulaFamily.StudentT => TailStudent(theta, df),
        CopulaFamily.Clayton => (theta > 0 ? Math.Pow(2, -1 / theta) : 0, 0),
        _ => (0, theta > 1 ? 2 - Math.Pow(2, 1 / theta) : 0),
    };

    /// <summary>Хвостовая зависимость копулы Стьюдента.</summary>
    private static (double Lower, double Upper) TailStudent(double theta, double df)
    {
        if (!double.IsFinite(df) || df <= 0) return (0, 0);

        double argument = -Math.Sqrt((df + 1) * (1 - theta) / (1 + theta));
        double lambda = 2 * StudentCdf(argument, df + 1);

        return (lambda, lambda);
    }

    /// <summary>Функция распределения Стьюдента.</summary>
    private static double StudentCdf(double x, double df) =>
        StatInference.TCdf(x, Math.Max(1, (int)Math.Round(df)));

    /// <summary>Квантиль распределения Стьюдента.</summary>
    private static double StudentQuantile(double p, double df) =>
        StatInference.TQuantile(Math.Clamp(p, 1e-9, 1 - 1e-9), Math.Max(1, (int)Math.Round(df)));

    /// <summary>Пара из гауссовой копулы.</summary>
    private static (double U, double V) SimulateGaussian(Random rng, double rho)
    {
        double z1 = RandomEngine.NextGaussian(rng);
        double z2 = (rho * z1) + (Math.Sqrt(Math.Max(1 - (rho * rho), 0)) * RandomEngine.NextGaussian(rng));

        return (EconMath.NormalCdf(z1), EconMath.NormalCdf(z2));
    }

    /// <summary>Пара из копулы Стьюдента.</summary>
    private static (double U, double V) SimulateStudent(Random rng, double rho, double df)
    {
        (double n1, double n2) = (RandomEngine.NextGaussian(rng), RandomEngine.NextGaussian(rng));
        double z2 = (rho * n1) + (Math.Sqrt(Math.Max(1 - (rho * rho), 0)) * n2);

        double chi = 2 * RandomEngine.NextGamma(rng, df / 2, 1.0);
        double scale = Math.Sqrt(df / Math.Max(chi, 1e-12));

        int degrees = Math.Max(1, (int)Math.Round(df));
        return (StatInference.TCdf(n1 * scale, degrees), StatInference.TCdf(z2 * scale, degrees));
    }

    /// <summary>Пара из копулы Клейтона.</summary>
    private static (double U, double V) SimulateClayton(Random rng, double theta)
    {
        if (theta <= 0) return (rng.NextDouble(), rng.NextDouble());

        double u = rng.NextDouble();
        double w = rng.NextDouble();

        double v = Math.Pow(
            (Math.Pow(w, -theta / (1 + theta)) - 1) * Math.Pow(u, -theta) + 1, -1 / theta);

        return (u, v);
    }

    /// <summary>Пара из копулы Гумбеля методом отбора.</summary>
    private static (double U, double V) SimulateGumbel(Random rng, double theta)
    {
        if (theta <= 1) return (rng.NextDouble(), rng.NextDouble());

        // Обратное преобразование условной копулы решается численно
        double u = rng.NextDouble();
        double target = rng.NextDouble();

        double low = 1e-9, high = 1 - 1e-9;

        for (int i = 0; i < 60; i++)
        {
            double mid = (low + high) / 2;
            double conditional = ConditionalGumbel(u, mid, theta);

            if (conditional < target) low = mid;
            else high = mid;
        }

        return (u, (low + high) / 2);
    }

    /// <summary>Условная функция копулы Гумбеля.</summary>
    private static double ConditionalGumbel(double u, double v, double theta)
    {
        double a = Math.Pow(-Math.Log(u), theta);
        double b = Math.Pow(-Math.Log(v), theta);
        double s = Math.Pow(a + b, 1.0 / theta);

        if (!double.IsFinite(s)) return 0;

        return Math.Exp(-s) * Math.Pow(a + b, (1 / theta) - 1)
            * Math.Pow(-Math.Log(u), theta - 1) / u;
    }
}

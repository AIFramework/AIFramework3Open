using System;

namespace AI.Statistics;

/// <summary>
/// Доверительные интервалы и тесты гипотез для среднего.
/// Реализованы z-тест (известная σ), t-тест (неизвестная σ),
/// а также приближения квантилей нормального и t-распределения.
/// </summary>
public static partial class StatInference
{
    #region Доверительные интервалы

    /// <summary>
    /// CI для среднего при известной σ (z-интервал).
    /// Возвращает (lower, upper).
    /// </summary>
    public static (double Lower, double Upper) ConfidenceIntervalZ(
        double mean, double sigma, int n, double confidence = 0.95)
    {
        double alpha = 1.0 - confidence;
        double z = NormalQuantile(1.0 - alpha / 2.0);
        double margin = z * sigma / Math.Sqrt(n);
        return (mean - margin, mean + margin);
    }

    /// <summary>
    /// CI для среднего при неизвестной σ (t-интервал).
    /// Возвращает (lower, upper).
    /// </summary>
    public static (double Lower, double Upper) ConfidenceIntervalT(
        double mean, double std, int n, double confidence = 0.95)
    {
        double alpha = 1.0 - confidence;
        int df = n - 1;
        double t = TQuantile(1.0 - alpha / 2.0, df);
        double margin = t * std / Math.Sqrt(n);
        return (mean - margin, mean + margin);
    }

    /// <summary>
    /// CI для дисперсии (χ²-интервал): (n−1)s² / χ²_{1−α/2} ≤ σ² ≤ (n−1)s² / χ²_{α/2}.
    /// </summary>
    public static (double Lower, double Upper) ConfidenceIntervalVariance(
        double variance, int n, double confidence = 0.95)
    {
        double alpha = 1.0 - confidence;
        int df = n - 1;
        double chi2Upper = ChiSquaredQuantile(1.0 - alpha / 2.0, df);
        double chi2Lower = ChiSquaredQuantile(alpha / 2.0, df);
        double ss = df * variance;
        return (ss / chi2Upper, ss / chi2Lower);
    }

    /// <summary>
    /// CI для СКО — корень из границ CI для дисперсии.
    /// </summary>
    public static (double Lower, double Upper) ConfidenceIntervalStd(
        double std, int n, double confidence = 0.95)
    {
        var (vLo, vHi) = ConfidenceIntervalVariance(std * std, n, confidence);
        return (Math.Sqrt(vLo), Math.Sqrt(vHi));
    }

    #endregion

    #region Тесты гипотез

    /// <summary>
    /// Результат теста гипотезы.
    /// </summary>
    public readonly struct TestResult
    {
        public double Statistic { get; init; }
        public double PValue { get; init; }
        public bool Reject { get; init; }
        public double CriticalLower { get; init; }
        public double CriticalUpper { get; init; }
    }

    /// <summary>
    /// Z-тест для среднего H₀: μ = μ₀ vs H₁: μ ≠ μ₀ (двусторонний).
    /// </summary>
    public static TestResult ZTest(double mean, double sigma, int n, double mu0, double alpha = 0.05)
    {
        double z = (mean - mu0) / (sigma / Math.Sqrt(n));
        double pValue = 2.0 * (1.0 - NormalCdf(Math.Abs(z)));
        double zCrit = NormalQuantile(1.0 - alpha / 2.0);
        return new TestResult
        {
            Statistic = z,
            PValue = pValue,
            Reject = Math.Abs(z) > zCrit,
            CriticalLower = -zCrit,
            CriticalUpper = zCrit
        };
    }

    /// <summary>
    /// t-тест для среднего H₀: μ = μ₀ vs H₁: μ ≠ μ₀ (двусторонний).
    /// </summary>
    public static TestResult TTest(double mean, double std, int n, double mu0, double alpha = 0.05)
    {
        int df = n - 1;
        double t = (mean - mu0) / (std / Math.Sqrt(n));
        double pValue = 2.0 * (1.0 - TCdf(Math.Abs(t), df));
        double tCrit = TQuantile(1.0 - alpha / 2.0, df);
        return new TestResult
        {
            Statistic = t,
            PValue = pValue,
            Reject = Math.Abs(t) > tCrit,
            CriticalLower = -tCrit,
            CriticalUpper = tCrit
        };
    }

    /// <summary>
    /// χ²-тест для дисперсии H₀: σ² = σ₀² vs H₁: σ² ≠ σ₀² (двусторонний).
    /// </summary>
    public static TestResult ChiSquaredVarianceTest(
        double sampleVariance, int n, double sigma0Sq, double alpha = 0.05)
    {
        int df = n - 1;
        double chi2 = df * sampleVariance / sigma0Sq;
        double pLower = ChiSquaredCdf(chi2, df);
        double pUpper = 1.0 - pLower;
        double pValue = 2.0 * Math.Min(pLower, pUpper);
        double critLo = ChiSquaredQuantile(alpha / 2.0, df);
        double critHi = ChiSquaredQuantile(1.0 - alpha / 2.0, df);
        return new TestResult
        {
            Statistic = chi2,
            PValue = Math.Min(pValue, 1.0),
            Reject = chi2 < critLo || chi2 > critHi,
            CriticalLower = critLo,
            CriticalUpper = critHi
        };
    }

    /// <summary>
    /// Двухвыборочный t-тест (Уэлча) H₀: μ₁ = μ₂.
    /// </summary>
    public static TestResult TTestTwoSample(
        double mean1, double std1, int n1,
        double mean2, double std2, int n2,
        double alpha = 0.05)
    {
        double se = Math.Sqrt(std1 * std1 / n1 + std2 * std2 / n2);
        double t = (mean1 - mean2) / se;
        // Степени свободы Уэлча-Саттертуэйта
        double v1 = std1 * std1 / n1, v2 = std2 * std2 / n2;
        double df = (v1 + v2) * (v1 + v2) / (v1 * v1 / (n1 - 1) + v2 * v2 / (n2 - 1));
        int dfi = Math.Max(1, (int)Math.Round(df));
        double pValue = 2.0 * (1.0 - TCdf(Math.Abs(t), dfi));
        double tCrit = TQuantile(1.0 - alpha / 2.0, dfi);
        return new TestResult
        {
            Statistic = t,
            PValue = pValue,
            Reject = Math.Abs(t) > tCrit,
            CriticalLower = -tCrit,
            CriticalUpper = tCrit
        };
    }

    #region Тесты на нормальность

    /// <summary>
    /// Тест Жарка-Бера: JB = n/6 · (S² + K²/4), где S — асимметрия, K — эксцесс.
    /// Под H₀ (нормальность) JB ~ χ²(2).
    /// </summary>
    public static TestResult JarqueBeraTest(double skewness, double excessKurtosis, int n, double alpha = 0.05)
    {
        double jb = n / 6.0 * (skewness * skewness + excessKurtosis * excessKurtosis / 4.0);
        double pValue = 1.0 - ChiSquaredCdf(jb, 2);
        double critVal = ChiSquaredQuantile(1.0 - alpha, 2);
        return new TestResult
        {
            Statistic = jb,
            PValue = pValue,
            Reject = jb > critVal,
            CriticalLower = 0,
            CriticalUpper = critVal
        };
    }

    /// <summary>
    /// Тест Андерсона-Дарлинга на нормальность.
    /// Вычисляет A² с поправкой на малую выборку.
    /// Критические значения: 0.576 (15%), 0.656 (10%), 0.787 (5%), 1.038 (1%).
    /// </summary>
    public static TestResult AndersonDarlingTest(double[] sortedData, int n, double mean, double std, double alpha = 0.05)
    {
        double s = 0;
        for (int i = 0; i < n; i++)
        {
            double zi = (sortedData[i] - mean) / std;
            double phiI = NormalCdf(zi);
            phiI = Math.Clamp(phiI, 1e-10, 1.0 - 1e-10);
            double phiN = NormalCdf((sortedData[n - 1 - i] - mean) / std);
            phiN = Math.Clamp(phiN, 1e-10, 1.0 - 1e-10);
            s += (2.0 * (i + 1) - 1.0) * (Math.Log(phiI) + Math.Log(1.0 - phiN));
        }
        double a2 = -n - s / n;
        // Stephens correction for estimated parameters
        double a2Star = a2 * (1.0 + 0.75 / n + 2.25 / (n * n));

        // Approximate p-value (Marsaglia & Marsaglia 2004 approximation)
        double pValue = AndersonDarlingPValue(a2Star);
        // Critical value lookup for given alpha
        double critVal = alpha switch
        {
            <= 0.01 => 1.038,
            <= 0.025 => 0.918,
            <= 0.05 => 0.787,
            <= 0.10 => 0.656,
            _ => 0.576
        };

        return new TestResult
        {
            Statistic = a2Star,
            PValue = pValue,
            Reject = a2Star > critVal,
            CriticalLower = 0,
            CriticalUpper = critVal
        };
    }

    private static double AndersonDarlingPValue(double a2)
    {
        if (a2 <= 0.2) return 1.0 - Math.Exp(-13.436 + 101.14 * a2 - 223.73 * a2 * a2);
        if (a2 <= 0.34) return 1.0 - Math.Exp(-8.318 + 42.796 * a2 - 59.938 * a2 * a2);
        if (a2 <= 0.6) return Math.Exp(0.9177 - 4.279 * a2 - 1.38 * a2 * a2);
        if (a2 <= 10) return Math.Exp(1.2937 - 5.709 * a2 + 0.0186 * a2 * a2);
        return 0;
    }

    #endregion

    #endregion

    #region Приближения квантилей и CDF

    /// <summary>Квантиль стандартного нормального (приближение Beasley-Springer-Moro).</summary>
    public static double NormalQuantile(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        if (Math.Abs(p - 0.5) < 1e-15) return 0;

        // Алгоритм Acklam: относительная погрешность порядка 1e-9 против 4.5e-4
        // у рациональной аппроксимации Абрамовица - Стиган 26.2.23, стоявшей здесь раньше.
        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
                       1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
                       6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
                      -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
                      3.754408661907416e+00];

        const double PLow = 0.02425;
        double q, r;

        if (p < PLow)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            return ((((((c[0] * q) + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   (((((d[0] * q) + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p <= 1 - PLow)
        {
            q = p - 0.5;
            r = q * q;
            return ((((((a[0] * r) + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
                   ((((((b[0] * r) + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }

        q = Math.Sqrt(-2 * Math.Log(1 - p));
        return -((((((c[0] * q) + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                (((((d[0] * q) + d[1]) * q + d[2]) * q + d[3]) * q + 1);
    }

    /// <summary>CDF стандартного нормального.</summary>
    public static double NormalCdf(double x)
    {
        // Approximation via error function
        return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
    }

    /// <summary>
    /// Квантиль t-распределения. Для df = 1 и df = 2 — точные замкнутые формулы;
    /// для df ≥ 3 вычисляется точно через обращение CDF (<see cref="TCdf"/>) бисекцией,
    /// разложение Корниша-Фишера используется только как начальное приближение.
    /// </summary>
    public static double TQuantile(double p, int df)
    {
        if (df <= 0) df = 1;
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        if (Math.Abs(p - 0.5) < 1e-15) return 0;
        if (df == 1) return Math.Tan(Math.PI * (p - 0.5));
        if (df == 2) return (2.0 * p - 1.0) / Math.Sqrt(2.0 * p * (1.0 - p));

        // Симметрия распределения: достаточно правого хвоста
        if (p < 0.5) return -TQuantile(1.0 - p, df);

        // Начальное приближение — двухчленное разложение Корниша-Фишера
        double z = NormalQuantile(p);
        double g1 = (z * z * z + z) / (4.0 * df);
        double g2 = (5.0 * z * z * z * z * z + 16.0 * z * z * z + 3.0 * z) / (96.0 * df * df);
        double guess = z + g1 + g2;

        // Скобка [0, hi]: расширяем hi, пока CDF(hi) не накроет p
        double lo = 0.0;
        double hi = Math.Max(guess * 2.0, 2.0);
        for (int i = 0; i < 200 && TCdf(hi, df) < p; i++) hi *= 2.0;

        // Уточнение бисекцией по точной CDF
        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (TCdf(mid, df) < p) lo = mid; else hi = mid;
            if (hi - lo <= 1e-12 * Math.Max(1.0, hi)) break;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>CDF t-распределения (приближение через бета-неполную).</summary>
    public static double TCdf(double t, int df)
    {
        double x = df / (df + t * t);
        double beta = RegularizedIncompleteBeta(df / 2.0, 0.5, x);
        return t >= 0 ? 1.0 - 0.5 * beta : 0.5 * beta;
    }

    /// <summary>
    /// Квантиль χ²-распределения. Вычисляется точно через обращение CDF
    /// (<see cref="ChiSquaredCdf"/>) бисекцией; преобразование Wilson-Hilferty
    /// используется только как начальное приближение для выбора скобки.
    /// </summary>
    public static double ChiSquaredQuantile(double p, int df)
    {
        if (df <= 0) df = 1;
        if (p <= 0) return 0;
        if (p >= 1) return double.PositiveInfinity;

        // Начальное приближение — преобразование Wilson-Hilferty:
        // χ²_p ≈ df * (1 - 2/(9df) + z*sqrt(2/(9df)))^3
        double z = NormalQuantile(p);
        double a = 2.0 / (9.0 * df);
        double cube = 1.0 - a + z * Math.Sqrt(a);
        double guess = Math.Max(0, df * cube * cube * cube);

        // Скобка [0, hi]: расширяем hi, пока CDF(hi) не накроет p
        double lo = 0.0;
        double hi = Math.Max(guess * 4.0, df * 4.0 + 40.0);
        for (int i = 0; i < 200 && ChiSquaredCdf(hi, df) < p; i++) hi *= 2.0;

        // Уточнение бисекцией по точной CDF
        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (ChiSquaredCdf(mid, df) < p) lo = mid; else hi = mid;
            if (hi - lo <= 1e-12 * Math.Max(1.0, hi)) break;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>CDF χ²-распределения (через регуляризованную неполную гамма-функцию).</summary>
    public static double ChiSquaredCdf(double x, int df)
    {
        if (x <= 0) return 0;
        return RegularizedGammaP(df / 2.0, x / 2.0);
    }

    /// <summary>PDF стандартного нормального.</summary>
    public static double NormalPdf(double x)
        => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

    /// <summary>PDF t-распределения.</summary>
    public static double TPdf(double t, int df)
    {
        double halfDfPlus1 = (df + 1.0) / 2.0;
        return Math.Exp(LogGamma(halfDfPlus1) - LogGamma(df / 2.0)
            - 0.5 * Math.Log(df * Math.PI)
            - halfDfPlus1 * Math.Log(1.0 + t * t / df));
    }

    #endregion

    #region Математические утилиты

    /// <summary>Функция ошибок (Abramowitz & Stegun, точность ~1.5e-7).</summary>
    public static double Erf(double x)
    {
        double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
        double poly = t * (0.254829592 + t * (-0.284496736 + t * (1.421413741 + t * (-1.453152027 + t * 1.061405429))));
        double result = 1.0 - poly * Math.Exp(-x * x);
        return x >= 0 ? result : -result;
    }

    /// <summary>
    /// Логарифм гамма-функции, аппроксимация Ланцоша (g = 7, n = 9).
    /// </summary>
    /// <remarks>
    /// Для аргументов меньше 1/2 применяется формула отражения, поэтому функция
    /// определена на всей положительной полуоси, а не только при x больше 1/2.
    /// </remarks>
    /// <param name="x">Аргумент, x &gt; 0.</param>
    public static double LogGamma(double x)
    {
        if (x <= 0 || double.IsNaN(x)) return double.NaN;

        if (x < 0.5)
        {
            // Формула отражения: Г(x)Г(1-x) = pi / sin(pi x)
            return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LogGamma(1.0 - x);
        }

        double[] lanczos = [0.99999999999980993, 676.5203681218851, -1259.1392167224028,
                            771.32342877765313, -176.61502916214059, 12.507343278686905,
                            -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7];

        double z = x - 1.0;
        double a = lanczos[0];
        for (int i = 1; i < lanczos.Length; i++) a += lanczos[i] / (z + i);

        double t = z + 7.5;
        return (0.5 * Math.Log(2 * Math.PI)) + ((z + 0.5) * Math.Log(t)) - t + Math.Log(a);
    }

    #endregion

    #region Внутренние

    // Regularized lower incomplete gamma P(a, x) = γ(a,x)/Γ(a)
    private static double RegularizedGammaP(double a, double x)
    {
        if (x <= 0) return 0;
        if (x < a + 1.0)
        {
            // Series expansion
            double sum = 1.0 / a, term = 1.0 / a;
            for (int n = 1; n < 200; n++)
            {
                term *= x / (a + n);
                sum += term;
                if (Math.Abs(term) < 1e-12 * Math.Abs(sum)) break;
            }
            return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
        }
        else
        {
            // Continued fraction (upper gamma), P = 1 - Q
            return 1.0 - RegularizedGammaQ(a, x);
        }
    }

    private static double RegularizedGammaQ(double a, double x)
    {
        // Lentz continued fraction for Q(a,x)
        double b = x + 1.0 - a;
        double c = 1e30, d = 1.0 / b;
        double h = d;
        for (int i = 1; i < 200; i++)
        {
            double an = -i * (i - a);
            b += 2.0;
            d = an * d + b; if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = b + an / c; if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1.0 / d;
            double del = d * c;
            h *= del;
            if (Math.Abs(del - 1.0) < 1e-12) break;
        }
        return h * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
    }

    // Regularized incomplete beta I_x(a,b) via continued fraction
    private static double RegularizedIncompleteBeta(double a, double b, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        double logBeta = LogGamma(a) + LogGamma(b) - LogGamma(a + b);
        double front = Math.Exp(Math.Log(x) * a + Math.Log(1.0 - x) * b - logBeta);

        // Use continued fraction (Lentz's method)
        if (x < (a + 1.0) / (a + b + 2.0))
            return front * BetaCF(a, b, x) / a;
        else
            return 1.0 - front * BetaCF(b, a, 1.0 - x) / b;
    }

    private static double BetaCF(double a, double b, double x)
    {
        const int maxIter = 200;
        const double eps = 1e-10;
        double qab = a + b, qap = a + 1, qam = a - 1;
        double c = 1, d = 1.0 - qab * x / qap;
        if (Math.Abs(d) < eps) d = eps;
        d = 1.0 / d;
        double h = d;
        for (int m = 1; m <= maxIter; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + aa * d; if (Math.Abs(d) < eps) d = eps;
            c = 1.0 + aa / c; if (Math.Abs(c) < eps) c = eps;
            d = 1.0 / d; h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + aa * d; if (Math.Abs(d) < eps) d = eps;
            c = 1.0 + aa / c; if (Math.Abs(c) < eps) c = eps;
            d = 1.0 / d;
            double del = d * c; h *= del;
            if (Math.Abs(del - 1.0) < eps) break;
        }
        return h;
    }

    #endregion
}

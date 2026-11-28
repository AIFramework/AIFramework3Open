using System;
using AI.HighLevelFunctions;

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
        double pValue = 2.0 * NormalSf(Math.Abs(z));
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
        double pValue = 2.0 * TSf(Math.Abs(t), df);
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
        double pValue = 2.0 * TSf(Math.Abs(t), dfi);
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
        double pValue = ChiSquaredSf(jb, 2);
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

    /// <summary>
    /// Квантиль стандартного нормального: алгоритм Acklam с одним шагом уточнения Галлея по точной <see cref="Erfc"/>.
    /// </summary>
    /// <remarks>
    /// Рациональное приближение Acklam само по себе даёт относительную погрешность около 1,15e-9 — против 4.5e-4
    /// у аппроксимации Абрамовица - Стиган 26.2.23, стоявшей здесь раньше. Шаг Галлея, который рекомендует сам Acklam,
    /// доводит квантиль почти до двойной точности: невязка Φ(x) − p считается через erfc и не теряет знаков в хвостах.
    /// Верхняя половина (p &gt; ½) сводится к нижней по симметрии Φ⁻¹(p) = −Φ⁻¹(1 − p).
    /// </remarks>
    public static double NormalQuantile(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        if (Math.Abs(p - 0.5) < 1e-15) return 0;

        // При p > ½ невязка Φ(x) − p — разность двух чисел около 1, и в верхнем хвосте шаг Галлея тонет в округлении:
        // при p = 1 − 1e-14 квантиль ошибался на 8e-9 (относительно 1e-9 — уровень Acklam без уточнения).
        // Разность 1 − p при p ≥ ½ вычисляется точно (лемма Штербенца), а в нижней половине и p, и Φ(x)
        // хранятся с полной относительной точностью — там уточнение даёт двойную точность
        if (p > 0.5) return -NormalQuantile(1 - p);

        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
                       1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
                       6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
                      -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
                      3.754408661907416e+00];

        const double PLow = 0.02425;
        double q, r, x;

        if (p < PLow)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            x = ((((((c[0] * q) + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                (((((d[0] * q) + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        else
        {
            q = p - 0.5;
            r = q * q;
            x = ((((((a[0] * r) + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
                ((((((b[0] * r) + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }

        // Шаг Галлея для уравнения Φ(x) = p. Где e^(x²/2) переполняется (p порядка 1e-320), остаётся приближение Acklam
        double u = (NormalCdf(x) - p) * Math.Sqrt(2 * Math.PI) * Math.Exp(x * x / 2);
        return double.IsFinite(u) ? x - (u / (1 + (x * u / 2))) : x;
    }

    /// <summary>CDF стандартного нормального: Φ(x) = ½·erfc(−x/√2).</summary>
    /// <remarks>
    /// Через дополнительную функцию ошибок, а не ½·(1 + erf): в левом хвосте разность 1 − erf состоит из одних
    /// разрядов округления, а erfc считается напрямую, поэтому Φ(−10) = 7,6e-24 верна и в относительном смысле.
    /// </remarks>
    public static double NormalCdf(double x) => 0.5 * SpecialFunctions.Erfc(-x / Math.Sqrt(2.0));

    /// <summary>Правый хвост стандартного нормального: P(Z &gt; x) = ½·erfc(x/√2).</summary>
    /// <remarks>
    /// Для p-значений: разность <c>1 − Φ(x)</c> обращается в ноль уже при <c>x ≈ 8.3</c>, а хвост,
    /// посчитанный напрямую, верен до <c>x ≈ 37</c>.
    /// </remarks>
    public static double NormalSf(double x) => 0.5 * SpecialFunctions.Erfc(x / Math.Sqrt(2.0));

    /// <summary>Дополнительная функция ошибок.</summary>
    [Obsolete("Используйте AI.HighLevelFunctions.SpecialFunctions.Erfc — единственную реализацию.")]
    public static double Erfc(double x) => SpecialFunctions.Erfc(x);

    /// <summary>
    /// Квантиль t-распределения с вещественным числом степеней свободы.
    /// </summary>
    /// <remarks>
    /// Для df = 1 и df = 2 — замкнутые формулы, иначе — обращение правого хвоста
    /// (<see cref="TSf"/>) делением пополам с относительным критерием. Хвост обращается напрямую,
    /// без перехода к <c>1 − p</c>, поэтому квантиль верен и при <c>p = 1e-300</c>.
    /// </remarks>
    /// <param name="p">Вероятность.</param>
    /// <param name="df">Число степеней свободы, <c>df &gt; 0</c>; иначе результат <c>NaN</c>.</param>
    public static double TQuantile(double p, double df)
    {
        if (double.IsNaN(p) || !(df > 0)) return double.NaN;
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        if (p == 0.5) return 0;

        // Распределение симметрично: ищется модуль квантиля по вероятности хвоста
        double tail = p < 0.5 ? p : 1 - p;
        double magnitude;

        if (df == 1) magnitude = 1 / Math.Tan(Math.PI * tail);
        else if (df == 2) magnitude = (1 - (2 * tail)) / Math.Sqrt(2 * tail * (1 - tail));
        else magnitude = Bisect(t => TSf(t, df) > tail, start: 1);

        return p < 0.5 ? -magnitude : magnitude;
    }

    /// <summary>CDF t-распределения с вещественным числом степеней свободы.</summary>
    /// <param name="t">Значение.</param>
    /// <param name="df">Число степеней свободы, <c>df &gt; 0</c>; иначе результат <c>NaN</c>.</param>
    public static double TCdf(double t, double df) => TSf(-t, df);

    /// <summary>Правый хвост t-распределения P(T &gt; t) — через неполную бета-функцию, без вычитания из единицы.</summary>
    /// <param name="t">Значение.</param>
    /// <param name="df">Число степеней свободы, <c>df &gt; 0</c>; иначе результат <c>NaN</c>.</param>
    public static double TSf(double t, double df)
    {
        if (double.IsNaN(t) || !(df > 0)) return double.NaN;
        if (double.IsInfinity(t)) return t > 0 ? 0 : 1;

        double magnitude = Math.Abs(t);

        // x = df/(df + t²) и его дополнение t²/(df + t²) известны оба без вычитания; при огромном t
        // квадрат переполняется, и отношения берутся делением по очереди
        double x, complement;

        if (magnitude > 1e150)
        {
            x = df / magnitude / magnitude;
            complement = 1;
        }
        else
        {
            double square = magnitude * magnitude;

            x = df / (df + square);
            complement = square / (df + square);
        }

        double tail = 0.5 * SpecialFunctions.RegularizedBeta(df / 2, 0.5, x, complement);

        return t >= 0 ? tail : 1 - tail;
    }

    /// <summary>
    /// Квантиль χ²-распределения с вещественным числом степеней свободы.
    /// </summary>
    /// <remarks>
    /// Обращение функции распределения делением пополам с относительным критерием: при
    /// <c>p &lt; ½</c> обращается левый хвост, иначе правый. Абсолютный критерий остановки давал
    /// при df = 1, p = 1e-10 ответ, завышенный в двадцать миллионов раз.
    /// </remarks>
    /// <param name="p">Вероятность.</param>
    /// <param name="df">Число степеней свободы, <c>df &gt; 0</c>; иначе результат <c>NaN</c>.</param>
    public static double ChiSquaredQuantile(double p, double df)
    {
        if (double.IsNaN(p) || !(df > 0)) return double.NaN;
        if (p <= 0) return 0;
        if (p >= 1) return double.PositiveInfinity;

        // Каждый хвост обращается сам: малое p сравнивается с P(x), малое 1 − p — с Q(x)
        double upper = 1 - p;

        return p < 0.5
            ? Bisect(x => ChiSquaredCdf(x, df) < p, start: df)
            : Bisect(x => ChiSquaredSf(x, df) > upper, start: df);
    }

    /// <summary>CDF χ²-распределения (через регуляризованную неполную гамма-функцию).</summary>
    /// <param name="x">Значение.</param>
    /// <param name="df">Число степеней свободы, <c>df &gt; 0</c>; иначе результат <c>NaN</c>.</param>
    public static double ChiSquaredCdf(double x, double df)
    {
        if (x <= 0) return df > 0 ? 0 : double.NaN;

        return SpecialFunctions.RegularizedGammaP(df / 2.0, x / 2.0);
    }

    /// <summary>Правый хвост χ²-распределения P(X &gt; x) — цепной дробью, без вычитания из единицы.</summary>
    /// <param name="x">Значение.</param>
    /// <param name="df">Число степеней свободы, <c>df &gt; 0</c>; иначе результат <c>NaN</c>.</param>
    public static double ChiSquaredSf(double x, double df)
    {
        if (x <= 0) return df > 0 ? 1 : double.NaN;

        return SpecialFunctions.RegularizedGammaQ(df / 2.0, x / 2.0);
    }

    /// <summary>CDF распределения Фишера.</summary>
    /// <param name="f">Значение.</param>
    /// <param name="df1">Степени свободы числителя.</param>
    /// <param name="df2">Степени свободы знаменателя.</param>
    public static double FCdf(double f, double df1, double df2)
    {
        if (double.IsNaN(f) || !(df1 > 0) || !(df2 > 0)) return double.NaN;
        if (f <= 0) return 0;

        double scaled = df1 * f;

        return SpecialFunctions.RegularizedBeta(df1 / 2, df2 / 2, scaled / (scaled + df2), df2 / (scaled + df2));
    }

    /// <summary>Правый хвост распределения Фишера P(F &gt; f) — без вычитания из единицы.</summary>
    /// <param name="f">Значение.</param>
    /// <param name="df1">Степени свободы числителя.</param>
    /// <param name="df2">Степени свободы знаменателя.</param>
    public static double FSf(double f, double df1, double df2)
    {
        if (double.IsNaN(f) || !(df1 > 0) || !(df2 > 0)) return double.NaN;
        if (f <= 0) return 1;

        double scaled = df1 * f;

        return SpecialFunctions.RegularizedBeta(df2 / 2, df1 / 2, df2 / (df2 + scaled), scaled / (df2 + scaled));
    }

    /// <summary>PDF стандартного нормального.</summary>
    public static double NormalPdf(double x)
        => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

    /// <summary>PDF t-распределения.</summary>
    public static double TPdf(double t, int df)
    {
        double halfDfPlus1 = (df + 1.0) / 2.0;
        return Math.Exp(SpecialFunctions.LogGamma(halfDfPlus1) - SpecialFunctions.LogGamma(df / 2.0)
            - 0.5 * Math.Log(df * Math.PI)
            - halfDfPlus1 * Math.Log(1.0 + t * t / df));
    }

    /// <summary>
    /// Логарифм вероятности Пуассона: ln P(K = k) = k·ln λ − λ − ln k!
    /// </summary>
    /// <param name="k">Число событий</param>
    /// <param name="lambda">Среднее число событий λ ≥ 0</param>
    public static double PoissonLogPmf(int k, double lambda)
    {
        if (!(lambda >= 0) || double.IsInfinity(lambda))
            throw new ArgumentOutOfRangeException(nameof(lambda), "Среднее — конечное неотрицательное число");

        if (k < 0)
            return double.NegativeInfinity;

        if (lambda == 0)
            return k == 0 ? 0.0 : double.NegativeInfinity;

        return (k * Math.Log(lambda)) - lambda - SpecialFunctions.LogFactorial(k);
    }

    #endregion

    #region Математические утилиты

    /// <summary>Функция ошибок.</summary>
    [Obsolete("Используйте AI.HighLevelFunctions.SpecialFunctions.Erf — единственную реализацию.")]
    public static double Erf(double x) => SpecialFunctions.Erf(x);

    /// <summary>Логарифм гамма-функции.</summary>
    /// <param name="x">Аргумент, x &gt; 0.</param>
    [Obsolete("Используйте AI.HighLevelFunctions.SpecialFunctions.LogGamma — единственную реализацию.")]
    public static double LogGamma(double x) => SpecialFunctions.LogGamma(x);

    #endregion

    #region Внутренние

    /// <summary>
    /// Точка на x &gt; 0, где признак «левее корня» сменяется на «правее».
    /// </summary>
    /// <remarks>
    /// Скобка расширяется удвоением и сужается делением пополам до относительной ширины в
    /// единицу младшего разряда. Критерий относительный, поэтому корень порядка 1e-20 находится
    /// так же точно, как корень порядка 1.
    /// </remarks>
    private static double Bisect(Func<double, bool> isLeftOfRoot, double start)
    {
        double lo = 0, hi = start;

        while (isLeftOfRoot(hi))
        {
            lo = hi;
            hi *= 2;

            if (double.IsInfinity(hi)) return double.PositiveInfinity;
        }

        // 2200 шагов хватает, чтобы пройти все двоичные порядки double от start до нуля
        for (int i = 0; i < 2200; i++)
        {
            double mid = lo + ((hi - lo) / 2);

            if (mid <= lo || mid >= hi) break;

            if (isLeftOfRoot(mid)) lo = mid;
            else hi = mid;

            if (hi - lo <= 4.5e-16 * hi) break;
        }

        return lo + ((hi - lo) / 2);
    }

    #endregion
}

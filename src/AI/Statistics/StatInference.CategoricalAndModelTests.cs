using System;

namespace AI.Statistics;

/// <summary>
/// Категориальные критерии (χ² Пирсона, Фишер, Макнемар, Кохрэн — Мантель — Гензель),
/// портманто Льюнга — Бокса и отношение правдоподобия для вложенных моделей.
/// </summary>
public static partial class StatInference
{
    #region Таблицы сопряжённости: Пирсон, Йейтс, Фишер, Макнемар, CMH

    /// <summary>Критерий независимости (χ² Пирсона) в таблице сопряжённости; ожидаемые частоты по модели независимости.</summary>
    public static TestResult PearsonChiSquareContingency(ReadOnlySpan<int> observed, int rows, int cols, double alpha = 0.05)
    {
        if (rows < 2 || cols < 2)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (observed.Length != rows * cols)
            throw new ArgumentException("Длина должна быть rows * cols.");

        var rowSum = new double[rows];
        var colSum = new double[cols];
        double total = 0;
        int idx = 0;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++, idx++)
            {
                int v = observed[idx];
                if (v < 0) throw new ArgumentOutOfRangeException(nameof(observed));
                rowSum[i] += v;
                colSum[j] += v;
                total += v;
            }
        }
        if (total <= 0) throw new ArgumentException("Суммарный объём должен быть положительным.");

        double chi2 = 0;
        idx = 0;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++, idx++)
            {
                double expected = rowSum[i] * colSum[j] / total;
                if (expected <= 0) continue;
                double d = observed[idx] - expected;
                chi2 += d * d / expected;
            }
        }
        int df = (rows - 1) * (cols - 1);
        return ChiSquareUpperTailTest(chi2, df, alpha);
    }

    /// <summary>Критерий согласия: наблюдаемые и ожидаемые частоты; df = (категорий − 1 − число оценённых параметров распределения).</summary>
    public static TestResult PearsonChiSquareGoodnessOfFit(
        ReadOnlySpan<double> observed, ReadOnlySpan<double> expected, int estimatedParameters, double alpha = 0.05)
    {
        if (observed.Length != expected.Length || observed.Length == 0)
            throw new ArgumentException();
        if (estimatedParameters < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedParameters));

        double chi2 = 0;
        for (int i = 0; i < observed.Length; i++)
        {
            double e = expected[i];
            if (e <= 0) throw new ArgumentException("Ожидаемые частоты должны быть положительными.");
            double d = observed[i] - e;
            chi2 += d * d / e;
        }
        int df = observed.Length - 1 - estimatedParameters;
        if (df <= 0)
            throw new InvalidOperationException("Число степеней свободы должно быть положительным.");
        return ChiSquareUpperTailTest(chi2, df, alpha);
    }

    /// <summary>χ² для 2×2 с поправкой Йейтса (непрерывность).</summary>
    public static TestResult YatesChiSquare2x2(int a, int b, int c, int d, double alpha = 0.05)
    {
        Validate2x2(a, b, c, d);
        int n = a + b + c + d;
        double num = Math.Abs(a * (double)d - b * (double)c) - n / 2.0;
        if (num < 0) num = 0;
        double denom = (a + b) * (double)(c + d) * (a + c) * (b + d);
        double chi2 = n * num * num / denom;
        return ChiSquareUpperTailTest(chi2, 1, alpha);
    }

    /// <summary>χ² для 2×2 без поправки Йейтса.</summary>
    public static TestResult PearsonChiSquare2x2(int a, int b, int c, int d, double alpha = 0.05)
    {
        Validate2x2(a, b, c, d);
        int n = a + b + c + d;
        double cross = a * (double)d - b * (double)c;
        double denom = (a + b) * (double)(c + d) * (a + c) * (b + d);
        double chi2 = n * cross * cross / denom;
        return ChiSquareUpperTailTest(chi2, 1, alpha);
    }

    /// <summary>Точный двусторонний критерий Фишера; статистика — отношение шансов ad/(bc).</summary>
    public static TestResult FisherExactTestTwoSided(int a, int b, int c, int d, double alpha = 0.05)
    {
        Validate2x2(a, b, c, d);
        int n = a + b + c + d;
        int row1 = a + b;
        int col1 = a + c;
        double observedProb = HypergeometricProbability(a, col1, row1, n);
        double pCutoff = observedProb + 1e-15;
        double pTwo = 0;
        int minA = Math.Max(0, row1 + col1 - n);
        int maxA = Math.Min(row1, col1);
        for (int aa = minA; aa <= maxA; aa++)
        {
            double p = HypergeometricProbability(aa, col1, row1, n);
            if (p <= pCutoff) pTwo += p;
        }
        pTwo = Math.Clamp(pTwo, 0.0, 1.0);
        double orStat = (b == 0 || c == 0) ? double.PositiveInfinity : (a * (double)d) / (b * (double)c);
        return new TestResult
        {
            Statistic = orStat,
            PValue = pTwo,
            Reject = pTwo < alpha,
            CriticalLower = double.NaN,
            CriticalUpper = double.NaN
        };
    }

    /// <summary>Точный односторонний критерий Фишера (направление «больше a»).</summary>
    public static TestResult FisherExactTestGreater(int a, int b, int c, int d, double alpha = 0.05)
    {
        Validate2x2(a, b, c, d);
        int n = a + b + c + d;
        int row1 = a + b;
        int col1 = a + c;
        double pOne = 0;
        int maxA = Math.Min(row1, col1);
        for (int aa = a; aa <= maxA; aa++)
            pOne += HypergeometricProbability(aa, col1, row1, n);
        pOne = Math.Clamp(pOne, 0.0, 1.0);
        double orStat = (b == 0 || c == 0) ? double.PositiveInfinity : (a * (double)d) / (b * (double)c);
        return new TestResult
        {
            Statistic = orStat,
            PValue = pOne,
            Reject = pOne < alpha,
            CriticalLower = double.NaN,
            CriticalUpper = double.NaN
        };
    }

    /// <summary>Макнемар с поправкой на непрерывность (пары b, c — дискордантные ячейки).</summary>
    public static TestResult McNemarWithContinuity(int b, int c, double alpha = 0.05)
    {
        if (b < 0 || c < 0) throw new ArgumentOutOfRangeException();
        int disc = b + c;
        if (disc == 0)
            return new TestResult { Statistic = 0, PValue = 1.0, Reject = false, CriticalLower = 0, CriticalUpper = ChiSquaredQuantile(1.0 - alpha, 1) };

        double num = Math.Abs(b - c) - 1.0;
        if (num < 0) num = 0;
        double chi2 = num * num / disc;
        return ChiSquareUpperTailTest(chi2, 1, alpha);
    }

    /// <summary>Макнемар без поправки.</summary>
    public static TestResult McNemarNoContinuity(int b, int c, double alpha = 0.05)
    {
        if (b < 0 || c < 0) throw new ArgumentOutOfRangeException();
        int disc = b + c;
        if (disc == 0)
            return new TestResult { Statistic = 0, PValue = 1.0, Reject = false, CriticalLower = 0, CriticalUpper = ChiSquaredQuantile(1.0 - alpha, 1) };

        double diff = b - c;
        double chi2 = diff * diff / disc;
        return ChiSquareUpperTailTest(chi2, 1, alpha);
    }

    /// <summary>Стратум 2×2 для критерия Кохрена — Мантеля — Гензеля.</summary>
    public readonly record struct CmhStratum2x2(int A, int B, int C, int D);

    /// <summary>Обобщённый χ² CMH (df=1) с поправкой 0,5 (или без неё).</summary>
    public static TestResult CochranMantelHaenszelTest(ReadOnlySpan<CmhStratum2x2> strata, double alpha = 0.05, bool continuityCorrection = true)
    {
        if (strata.Length == 0)
            throw new ArgumentException("Нужна хотя бы одна страта.");

        double sumObsMinusExpected = 0;
        double sumVar = 0;
        foreach (var s in strata)
        {
            int n = s.A + s.B + s.C + s.D;
            if (n <= 1) continue;
            int n1 = s.A + s.B;
            int n0 = s.C + s.D;
            int m1 = s.A + s.C;
            int m0 = s.B + s.D;
            double ea = n1 * (double)m1 / n;
            sumObsMinusExpected += s.A - ea;
            sumVar += n1 * (double)n0 * m1 * m0 / (n * n * (n - 1.0));
        }
        if (sumVar <= 0)
            throw new InvalidOperationException("Суммарная дисперсия нулевая.");

        double adj = continuityCorrection ? 0.5 : 0.0;
        double num = Math.Abs(sumObsMinusExpected) - adj;
        if (num < 0) num = 0;
        double chi2 = num * num / sumVar;
        return ChiSquareUpperTailTest(chi2, 1, alpha);
    }

    /// <summary>Обобщённое отношение шансов Мантеля — Гензеля.</summary>
    public static double MantelHaenszelOddsRatio(ReadOnlySpan<CmhStratum2x2> strata)
    {
        double sumNum = 0, sumDen = 0;
        foreach (var s in strata)
        {
            int n = s.A + s.B + s.C + s.D;
            if (n == 0) continue;
            sumNum += s.A * (double)s.D / n;
            sumDen += s.B * (double)s.C / n;
        }
        if (sumDen == 0) return double.PositiveInfinity;
        return sumNum / sumDen;
    }

    #endregion

    #region Временные ряды и вложенные модели

    /// <summary>
    /// Критерий Льюнга — Бокса: Q = n(n+2) Σ r_k²/(n−k).
    /// <paramref name="adjustedChiSquareDf"/> — степени свободы для χ² (например h − p − q для остатков ARMA).
    /// </summary>
    public static TestResult LjungBoxTest(int sampleSize, ReadOnlySpan<double> autocorrelations, int adjustedChiSquareDf, double alpha = 0.05)
    {
        if (sampleSize <= 0) throw new ArgumentOutOfRangeException(nameof(sampleSize));
        if (autocorrelations.Length == 0) throw new ArgumentOutOfRangeException(nameof(autocorrelations));
        if (adjustedChiSquareDf <= 0) throw new ArgumentOutOfRangeException(nameof(adjustedChiSquareDf));

        double q = 0;
        for (int k = 0; k < autocorrelations.Length; k++)
        {
            int lag = k + 1;
            double denom = sampleSize - lag;
            if (denom <= 0) throw new ArgumentException("Лаг превышает объём выборки.");
            double rk = autocorrelations[k];
            q += rk * rk / denom;
        }
        q *= sampleSize * (sampleSize + 2.0);
        return ChiSquareUpperTailTest(q, adjustedChiSquareDf, alpha);
    }

    /// <summary>
    /// Тест отношения правдоподобия: 2(ℓ_общая − ℓ_вложенная); df — разница числа параметров.
    /// </summary>
    public static TestResult LikelihoodRatioNestedModels(
        double logLikelihoodNested, double logLikelihoodGeneral, int parameterDifference, double alpha = 0.05)
    {
        if (parameterDifference <= 0)
            throw new ArgumentOutOfRangeException(nameof(parameterDifference));
        double lr = 2.0 * (logLikelihoodGeneral - logLikelihoodNested);
        if (lr < 0) lr = 0;
        return ChiSquareUpperTailTest(lr, parameterDifference, alpha);
    }

    #endregion

    #region Внутренние хелперы

    static TestResult ChiSquareUpperTailTest(double chi2Statistic, int df, double alpha)
    {
        double p = 1.0 - ChiSquaredCdf(chi2Statistic, df);
        double crit = ChiSquaredQuantile(1.0 - alpha, df);
        return new TestResult
        {
            Statistic = chi2Statistic,
            PValue = Math.Clamp(p, 0.0, 1.0),
            Reject = chi2Statistic > crit,
            CriticalLower = 0,
            CriticalUpper = crit
        };
    }

    static void Validate2x2(int a, int b, int c, int d)
    {
        if (a < 0 || b < 0 || c < 0 || d < 0)
            throw new ArgumentOutOfRangeException();
        if (a + b + c + d == 0)
            throw new ArgumentException();
    }

    static double HypergeometricProbability(int k, int K, int n, int N)
    {
        if (N <= 0 || K < 0 || K > N || n < 0 || n > N)
            throw new ArgumentOutOfRangeException();
        if (k < Math.Max(0, n + K - N) || k > Math.Min(K, n))
            return 0.0;
        return Math.Exp(LogChoose(K, k) + LogChoose(N - K, n - k) - LogChoose(N, n));
    }

    static double LogChoose(int n, int k)
    {
        if (k < 0 || k > n) return double.NegativeInfinity;
        if (k == 0 || k == n) return 0.0;
        return LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1);
    }

    #endregion
}

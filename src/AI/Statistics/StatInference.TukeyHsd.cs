using System;
using System.Collections.Generic;

namespace AI.Statistics;

/// <summary>Процедура Тьюки (HSD) для попарных сравнений средних после однофакторного ANOVA.</summary>
public static partial class StatInference
{
    /// <summary>Результат попарного сравнения групп с индексами i, j.</summary>
    public readonly record struct TukeyPairwiseResult(int GroupI, int GroupJ, double MeanI, double MeanJ, double Difference, double CriticalDifference);

    /// <summary>Сводка ANOVA и критические разности Тьюки.</summary>
    public readonly record struct TukeyHsdSummary(
        double GrandMean,
        double MeanSquareWithin,
        int DegreesOfFreedomWithin,
        double StudentizedRangeQ,
        double CriticalDifferenceBalanced,
        IReadOnlyList<double> GroupMeans,
        IReadOnlyList<TukeyPairwiseResult> PairwiseComparisons);

    /// <summary>Сбалансированный дизайн (одинаковый n в группах). Критическое q для α ≈ 0,05 по таблице с интерполяцией по df ошибки.</summary>
    public static TukeyHsdSummary TukeyHsdBalanced(IReadOnlyList<IReadOnlyList<double>> groups)
    {
        if (groups == null || groups.Count < 2)
            throw new ArgumentException("Нужно не менее двух групп.");

        int k = groups.Count;
        int nPerGroup = groups[0].Count;
        if (nPerGroup == 0)
            throw new ArgumentException("Группы не должны быть пустыми.");

        for (int i = 0; i < k; i++)
        {
            if (groups[i].Count != nPerGroup)
                throw new ArgumentException("Для сбалансированного дизайна объёмы групп должны совпадать.");
        }

        int nTotal = k * nPerGroup;
        var means = new double[k];
        double grandSum = 0;
        for (int i = 0; i < k; i++)
        {
            double s = 0;
            for (int j = 0; j < nPerGroup; j++)
                s += groups[i][j];
            means[i] = s / nPerGroup;
            grandSum += s;
        }
        double grandMean = grandSum / nTotal;

        double ssWithin = 0;
        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j < nPerGroup; j++)
            {
                double d = groups[i][j] - means[i];
                ssWithin += d * d;
            }
        }

        int dfWithin = nTotal - k;
        double msWithin = ssWithin / dfWithin;
        double q = StudentizedRangeCriticalValueAlpha05(k, dfWithin);
        double criticalDiff = q * Math.Sqrt(msWithin / nPerGroup);

        var pairs = new List<TukeyPairwiseResult>();
        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
                pairs.Add(new TukeyPairwiseResult(i, j, means[i], means[j], means[i] - means[j], criticalDiff));
        }

        return new TukeyHsdSummary(grandMean, msWithin, dfWithin, q, criticalDiff, means, pairs);
    }

    /// <summary>Несбалансированный дизайн (поправка Тьюки — Крамера).</summary>
    public static TukeyHsdSummary TukeyHsdUnbalanced(IReadOnlyList<IReadOnlyList<double>> groups)
    {
        if (groups == null || groups.Count < 2)
            throw new ArgumentException("Нужно не менее двух групп.");

        int k = groups.Count;
        var means = new double[k];
        var sizes = new int[k];
        int nTotal = 0;
        double grandSum = 0;

        for (int i = 0; i < k; i++)
        {
            if (groups[i].Count == 0)
                throw new ArgumentException("Каждая группа должна содержать наблюдения.");
            sizes[i] = groups[i].Count;
            double s = 0;
            for (int j = 0; j < sizes[i]; j++)
                s += groups[i][j];
            means[i] = s / sizes[i];
            grandSum += s;
            nTotal += sizes[i];
        }

        double grandMean = grandSum / nTotal;
        double ssWithin = 0;
        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j < sizes[i]; j++)
            {
                double d = groups[i][j] - means[i];
                ssWithin += d * d;
            }
        }

        int dfWithin = nTotal - k;
        double msWithin = ssWithin / dfWithin;
        double q = StudentizedRangeCriticalValueAlpha05(k, dfWithin);

        var pairs = new List<TukeyPairwiseResult>();
        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
            {
                double cd = q * Math.Sqrt(msWithin / 2.0 * (1.0 / sizes[i] + 1.0 / sizes[j]));
                pairs.Add(new TukeyPairwiseResult(i, j, means[i], means[j], means[i] - means[j], cd));
            }
        }

        return new TukeyHsdSummary(grandMean, msWithin, dfWithin, q, double.NaN, means, pairs);
    }

    /// <summary>Критическое значение стьюдентизированного размаха q при α = 0,05.</summary>
    public static double StudentizedRangeCriticalValueAlpha05(int numberOfGroups, int degreesOfFreedomError)
    {
        if (numberOfGroups < 2)
            throw new ArgumentOutOfRangeException(nameof(numberOfGroups));
        if (degreesOfFreedomError < 1)
            throw new ArgumentOutOfRangeException(nameof(degreesOfFreedomError));

        int k = numberOfGroups;
        double df = degreesOfFreedomError;

        if (k <= 10)
            return InterpolateStudentizedQAlongDf(df, k - 2);

        double q9 = InterpolateStudentizedQAlongDf(df, 7);
        double q10 = InterpolateStudentizedQAlongDf(df, 8);
        return q10 + (k - 10) * (q10 - q9);
    }

    static readonly double[] TukeyDfAxis = [5, 10, 20, 30, 60, 120, 10_000];

    static readonly double[,] TukeyQAlpha05 =
    {
        { 3.64, 4.60, 5.22, 5.67, 6.03, 6.33, 6.58, 6.80, 6.99 },
        { 3.15, 3.88, 4.33, 4.65, 4.91, 5.13, 5.32, 5.49, 5.65 },
        { 2.95, 3.58, 3.96, 4.23, 4.45, 4.65, 4.82, 4.97, 5.11 },
        { 2.89, 3.49, 3.85, 4.10, 4.30, 4.48, 4.63, 4.77, 4.89 },
        { 2.83, 3.40, 3.74, 3.98, 4.16, 4.31, 4.44, 4.56, 4.67 },
        { 2.80, 3.36, 3.68, 3.92, 4.10, 4.24, 4.37, 4.48, 4.59 },
        { 2.77, 3.31, 3.63, 3.86, 4.03, 4.17, 4.29, 4.39, 4.47 }
    };

    static double InterpolateStudentizedQAlongDf(double df, int columnIndex)
    {
        int col = Math.Clamp(columnIndex, 0, TukeyQAlpha05.GetLength(1) - 1);

        if (df <= TukeyDfAxis[0])
            return TukeyQAlpha05[0, col];
        if (df >= TukeyDfAxis[TukeyDfAxis.Length - 1])
            return TukeyQAlpha05[TukeyQAlpha05.GetLength(0) - 1, col];

        int lo = 0;
        while (lo < TukeyDfAxis.Length - 1 && TukeyDfAxis[lo + 1] < df)
            lo++;

        int hi = lo + 1;
        double t = (df - TukeyDfAxis[lo]) / (TukeyDfAxis[hi] - TukeyDfAxis[lo]);
        return TukeyQAlpha05[lo, col] * (1.0 - t) + TukeyQAlpha05[hi, col] * t;
    }
}

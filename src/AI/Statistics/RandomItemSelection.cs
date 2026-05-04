using AI.DataStructs.Algebraic;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AI.Statistics;

/// <summary>
/// Выбор элемента по дискретному распределению вероятностей.
/// 
/// Реализация — обратное преобразование CDF (накопленная сумма +
/// бинарный поиск), O(log N) на одно извлечение против O(N) у
/// старого «угадывающего» подхода и с корректной нормировкой
/// произвольного неотрицательного распределения.
/// </summary>
[Serializable]
public class RandomItemSelection
{
    #region Публичный API — элементы

    /// <summary>
    /// Случайный элемент массива согласно распределению вероятностей.
    /// Распределение автоматически нормируется (суммируется в 1);
    /// нули и отрицательные элементы тоже допустимы — отрицательные
    /// обнуляются.
    /// </summary>
    public static T GetElement<T>(Vector distributionFunction, T[] arrayStates, Random random)
    {
        if (arrayStates == null || arrayStates.Length == 0)
            throw new ArgumentException("Пустой массив состояний", nameof(arrayStates));

        int idx = GetIndex(distributionFunction, random);
        return arrayStates[idx];
    }

    #endregion

    #region Публичный API — индексы

    /// <summary>
    /// Случайный индекс по распределению вероятностей (инверсия CDF).
    /// При <paramref name="t"/> &lt; 1 распределение «заостряется»
    /// (low-temperature), при &gt; 1 — сглаживается. Это softmax-style
    /// перешкалирование: p_i^(1/t) / Σ p_j^(1/t).
    /// </summary>
    public static int GetIndex(Vector distributionFunction, Random random, double t = 1.0)
    {
        if (distributionFunction == null || distributionFunction.Count == 0)
            throw new ArgumentException("Пустое распределение", nameof(distributionFunction));

        int n = distributionFunction.Count;
        Span<double> cdf = n <= 512 ? stackalloc double[n] : new double[n];

        BuildCdf(distributionFunction, t, cdf);

        double total = cdf[n - 1];
        if (total <= 0.0) // всё нули или отрицательны — equal uniform
            return random.Next(n);

        double u = random.NextDouble() * total;
        return UpperBound(cdf, u, n);
    }

    #endregion

    #region Внутренности

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildCdf(Vector src, double t, Span<double> cdf)
    {
        double acc = 0.0;
        bool reshape = t != 1.0 && t > 0.0;

        for (int i = 0; i < cdf.Length; i++)
        {
            double p = src[i];
            if (p < 0.0) p = 0.0;
            if (reshape) p = Math.Pow(p, 1.0 / t);
            acc += p;
            cdf[i] = acc;
        }
    }

    /// <summary>
    /// upper_bound — первый индекс i, где cdf[i] &gt; u.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int UpperBound(ReadOnlySpan<double> cdf, double u, int length)
    {
        int lo = 0, hi = length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (cdf[mid] <= u) lo = mid + 1;
            else hi = mid;
        }
        return lo < length ? lo : length - 1;
    }

    #endregion

    #region Вспомогательные перегрузки

    /// <summary>Случайный элемент на поток-локальном RNG.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static T GetElement<T>(Vector distributionFunction, T[] arrayStates)
        => GetElement(distributionFunction, arrayStates, RandomEngine.Shared);

    /// <summary>Случайный индекс на поток-локальном RNG.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static int GetIndex(Vector distributionFunction, double t = 1.0)
        => GetIndex(distributionFunction, RandomEngine.Shared, t);

    #endregion
}

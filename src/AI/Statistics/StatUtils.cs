using System;
using System.Runtime.CompilerServices;

namespace AI.Statistics;

/// <summary>
/// Численные примитивы, которые используются сквозь весь статистический
/// модуль: устойчивый log-sum-exp, одно-проходный Welford для
/// среднего/дисперсии, NaN-safe редьюсеры и бинарный поиск по
/// отсортированному массиву.
/// </summary>
/// <remarks>
/// Все методы чисто функциональные и потокобезопасные — не хранят
/// никакого общего состояния.
/// </remarks>
public static class StatUtils
{
    #region log-sum-exp

    /// <summary>
    /// log(exp(a) + exp(b)) с защитой от переполнения.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LogSumExp(double a, double b)
    {
        if (double.IsNegativeInfinity(a)) return b;
        if (double.IsNegativeInfinity(b)) return a;
        if (a >= b) return a + Math.Log(1.0 + Math.Exp(b - a));
        return b + Math.Log(1.0 + Math.Exp(a - b));
    }

    /// <summary>
    /// log(sum(exp(logs))). Численно устойчивая версия: вычитает
    /// максимум перед экспонентой, чтобы не переполниться.
    /// </summary>
    public static double LogSumExp(ReadOnlySpan<double> logs)
    {
        if (logs.Length == 0) return double.NegativeInfinity;
        if (logs.Length == 1) return logs[0];

        double max = double.NegativeInfinity;
        for (int i = 0; i < logs.Length; i++)
            if (logs[i] > max) max = logs[i];

        if (double.IsNegativeInfinity(max)) return double.NegativeInfinity;

        double acc = 0.0;
        for (int i = 0; i < logs.Length; i++)
            acc += Math.Exp(logs[i] - max);
        return max + Math.Log(acc);
    }

    /// <summary>
    /// softmax в лог-пространстве. Результат — вектор нормированных
    /// экспонент (суммируется в 1), численно устойчив.
    /// </summary>
    public static void LogSoftmax(ReadOnlySpan<double> logs, Span<double> target)
    {
        if (target.Length != logs.Length)
            throw new ArgumentException("Длины span'ов должны совпадать", nameof(target));

        double lse = LogSumExp(logs);
        for (int i = 0; i < logs.Length; i++)
            target[i] = Math.Exp(logs[i] - lse);
    }

    #endregion

    #region Устойчивый логарифм и деление

    /// <summary>Логарифм с «подъёмом» нуля до eps (избегает -Infinity).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SafeLog(double x, double eps = 1e-300)
        => Math.Log(x < eps ? eps : x);

    /// <summary>Заменяет ноль на <see cref="AISettings.GlobalEps"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SafeStd(double std)
        => std == 0 ? AISettings.GlobalEps : std;

    #endregion

    #region Welford (один проход, NaN-aware)

    /// <summary>
    /// Одно-проходное устойчивое вычисление среднего и дисперсии.
    /// Пропускает NaN, возвращает число валидных элементов.
    /// </summary>
    /// <param name="data">Исходные данные</param>
    /// <param name="skipNaN">Игнорировать ли NaN</param>
    /// <param name="unbiased">true -> деление на (n-1), иначе на n</param>
    public static (double mean, double variance, int count) Welford(
        ReadOnlySpan<double> data, bool skipNaN = true, bool unbiased = true)
    {
        double mean = 0.0, m2 = 0.0;
        int n = 0;

        for (int i = 0; i < data.Length; i++)
        {
            double x = data[i];
            if (skipNaN && double.IsNaN(x)) continue;

            n++;
            double delta = x - mean;
            mean += delta / n;
            m2 += delta * (x - mean);
        }

        if (n == 0) return (0.0, 0.0, 0);
        if (n == 1) return (mean, 0.0, 1);

        double denom = unbiased ? (n - 1) : n;
        return (mean, m2 / denom, n);
    }

    #endregion

    #region Бинарный поиск

    /// <summary>
    /// Первый индекс в отсортированном по возрастанию массиве, для
    /// которого sorted[i] &gt;= value. Если все меньше — length.
    /// </summary>
    public static int LowerBound(ReadOnlySpan<double> sortedAsc, double value)
    {
        int lo = 0, hi = sortedAsc.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (sortedAsc[mid] < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    #endregion
}

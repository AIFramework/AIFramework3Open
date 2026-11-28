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

    #region Среднее и дисперсия

    /// <summary>
    /// Среднее, дисперсия и число значений выборки.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Два прохода: сначала среднее, затем сумма квадратов отклонений с поправкой на остаток
    /// суммы отклонений: <c>Σd² − (Σd)²/n</c>. Поправка убирает ошибку округления самого среднего,
    /// поэтому результат точен до последних знаков и на данных с большим сдвигом — там, где
    /// формула <c>E[x²] − E[x]²</c> теряет всё, а однопроходная схема Уэлфорда — семь знаков из шестнадцати.
    /// </para>
    /// <para>
    /// Выборка без значений даёт <c>(0, 0, 0)</c>, из одного значения — нулевую дисперсию.
    /// </para>
    /// </remarks>
    /// <param name="data">Выборка.</param>
    /// <param name="skipNaN">Не включать <c>NaN</c> в расчёт.</param>
    /// <param name="unbiased">Выборочная дисперсия (делитель n − 1) вместо генеральной (n).</param>
    public static (double mean, double variance, int count) MeanAndVariance(
        ReadOnlySpan<double> data, bool skipNaN = true, bool unbiased = true)
    {
        double sum = 0.0;
        int n = 0;

        for (int i = 0; i < data.Length; i++)
        {
            double x = data[i];
            if (skipNaN && double.IsNaN(x)) continue;

            n++;
            sum += x;
        }

        if (n == 0) return (0.0, 0.0, 0);

        double mean = sum / n;

        if (n == 1) return (mean, 0.0, 1);

        double squares = 0.0, rest = 0.0;

        for (int i = 0; i < data.Length; i++)
        {
            double x = data[i];
            if (skipNaN && double.IsNaN(x)) continue;

            double d = x - mean;
            squares += d * d;
            rest += d;
        }

        double m2 = Math.Max(0.0, squares - (rest * rest / n));

        return (mean, m2 / (unbiased ? n - 1 : n), n);
    }

    /// <summary>Среднее, дисперсия и число значений.</summary>
    [Obsolete("Используйте StatUtils.MeanAndVariance: два прохода точнее на данных со сдвигом.")]
    public static (double mean, double variance, int count) Welford(
        ReadOnlySpan<double> data, bool skipNaN = true, bool unbiased = true)
        => MeanAndVariance(data, skipNaN, unbiased);

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

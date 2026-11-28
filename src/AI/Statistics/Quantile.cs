using AI.DataStructs.Algebraic;
using AI.Extensions;
using System;
using System.Runtime.CompilerServices;

namespace AI.Statistics;

/// <summary>
/// Выборочные квантили. Конструктор делает копию входных данных —
/// исходный вектор пользователя не модифицируется.
/// </summary>
[Serializable]
public class Quantile
{
    private readonly int _max;
    private readonly double[] _sorted;

    /// <summary>
    /// Отсортированные по возрастанию значения (копия входа).
    /// </summary>
    public Vector SortVec { get; }

    /// <summary>
    /// Собирает объект для расчёта квантилей. Входные данные
    /// клонируются, а не сортируются in-place (исправлен побочный
    /// эффект старой реализации).
    /// </summary>
    public Quantile(IAlgebraicStructure<double> structureDouble)
    {
        double[] copy = new double[structureDouble.Shape.Count];
        Array.Copy(structureDouble.Data, copy, copy.Length);
        Array.Sort(copy);
        _sorted = copy;
        SortVec = new Vector((double[])copy.Clone());
        _max = SortVec.Count - 1;
    }

    /// <summary>
    /// Квантиль уровня <paramref name="q"/> ∈ [0; 1] по выборке, переданной в конструктор.
    /// </summary>
    /// <param name="q">Уровень.</param>
    /// <param name="method">Правило; по умолчанию тип 7 — линейная интерполяция.</param>
    /// <exception cref="InvalidOperationException">Выборка пуста.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Уровень вне [0; 1].</exception>
    public double GetQuantile(double q, QuantileMethod method = QuantileMethod.Linear)
    {
        if (_max < 0)
            throw new InvalidOperationException("Выборка пуста");

        return OfSorted(_sorted, q, method);
    }

    /// <summary>
    /// Выборочный квантиль уровня <paramref name="q"/>; вход не изменяется.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Единственная реализация квантиля во фреймворке. По умолчанию тип 7 (линейная интерполяция
    /// между порядковыми статистиками) — то же, что дают R, NumPy и pandas; остальные восемь типов
    /// Хайндмана — Фана задаются параметром. Сверено с <c>numpy.quantile</c>.
    /// </para>
    /// <para>
    /// Пустая выборка и выборка с <c>NaN</c> дают <c>NaN</c>: квантиль там не определён, и это
    /// видно в результате.
    /// </para>
    /// </remarks>
    /// <param name="values">Выборка в любом порядке.</param>
    /// <param name="q">Уровень из отрезка [0; 1].</param>
    /// <param name="method">Правило; по умолчанию тип 7.</param>
    /// <exception cref="ArgumentOutOfRangeException">Уровень вне [0; 1].</exception>
    public static double Of(ReadOnlySpan<double> values, double q, QuantileMethod method = QuantileMethod.Linear)
    {
        double[] sorted = values.ToArray();

        Array.Sort(sorted);

        return OfSorted(sorted, q, method);
    }

    /// <summary>
    /// Выборочный квантиль по уже отсортированной по возрастанию выборке — без копии и сортировки.
    /// </summary>
    /// <param name="sorted">Выборка по возрастанию.</param>
    /// <param name="q">Уровень из отрезка [0; 1].</param>
    /// <param name="method">Правило; по умолчанию тип 7.</param>
    /// <exception cref="ArgumentOutOfRangeException">Уровень вне [0; 1].</exception>
    public static double OfSorted(ReadOnlySpan<double> sorted, double q, QuantileMethod method = QuantileMethod.Linear)
    {
        if (!(q >= 0.0 && q <= 1.0))
            throw new ArgumentOutOfRangeException(nameof(q), q, "Квантиль должен быть в [0; 1]");

        int n = sorted.Length;

        // NaN при сортировке встаёт первым, так что проверка первого элемента ловит любой NaN выборки
        if (n == 0 || double.IsNaN(sorted[0]) || double.IsNaN(sorted[n - 1])) return double.NaN;
        if (n == 1) return sorted[0];

        if (method == QuantileMethod.Linear)
        {
            double position = q * (n - 1);
            int low = (int)Math.Floor(position);

            return Between(sorted, low + 1, position - low);
        }

        // Положение в выборке с номерами от единицы: h = n·q + m, j — целая часть, g — дробная
        double h = method switch
        {
            QuantileMethod.InvertedCdf or QuantileMethod.AveragedInvertedCdf or QuantileMethod.InterpolatedInvertedCdf => n * q,
            QuantileMethod.ClosestObservation => (n * q) - 0.5,
            QuantileMethod.Hazen => (n * q) + 0.5,
            QuantileMethod.Weibull => (n * q) + q,
            QuantileMethod.MedianUnbiased => (n * q) + ((q + 1) / 3),
            QuantileMethod.NormalUnbiased => (n * q) + (q / 4) + 0.375,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Неизвестное правило квантиля"),
        };

        double j = Math.Floor(h);
        double g = h - j;

        // Произведение n·q, равное целому, из-за округления может оказаться на волос в стороне
        if (g < 4 * double.Epsilon + (4.5e-16 * Math.Max(1.0, Math.Abs(h)))) g = 0;
        else if (1 - g < 4.5e-16 * Math.Max(1.0, Math.Abs(h))) { g = 0; j += 1; }

        int index = (int)j;

        return method switch
        {
            QuantileMethod.InvertedCdf => g > 0 ? At(sorted, index + 1) : At(sorted, index),
            QuantileMethod.AveragedInvertedCdf => g > 0 ? At(sorted, index + 1) : Mid(At(sorted, index), At(sorted, index + 1)),
            QuantileMethod.ClosestObservation => g == 0 && index % 2 == 0 ? At(sorted, index) : At(sorted, index + 1),
            _ => Between(sorted, index, g),
        };
    }

    /// <summary>Медиана выборки: квантиль уровня ½ по типу 7, на чётном числе элементов — полусумма двух средних.</summary>
    /// <param name="values">Выборка в любом порядке.</param>
    public static double Median(ReadOnlySpan<double> values) => Of(values, 0.5);

    /// <summary>Быстрый квантиль.</summary>
    [Obsolete("Используйте Quantile.Of: одна реализация с явным правилом (по умолчанию тип 7).")]
    public static double FastQuantile(IAlgebraicStructure<double> structure, double q)
        => Of(structure.Data.AsSpan(0, structure.Shape.Count), Math.Clamp(q, 0.0, 1.0));

    /// <summary>Элемент с номером от единицы; номера за краями приводятся к краю.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double At(ReadOnlySpan<double> sorted, int oneBased)
        => sorted[Math.Clamp(oneBased, 1, sorted.Length) - 1];

    /// <summary>Точка между элементами с номерами j и j + 1 на доле g пути.</summary>
    private static double Between(ReadOnlySpan<double> sorted, int oneBased, double fraction)
    {
        double low = At(sorted, oneBased), high = At(sorted, oneBased + 1);

        // Равные соседи возвращаются как есть: среди значений бывают бесконечности, а inf − inf дало бы NaN
        return fraction == 0 || low.Equals(high) ? low : low + (fraction * (high - low));
    }

    private static double Mid(double low, double high) => low.Equals(high) ? low : (low + high) / 2;

}

/// <summary>
/// Выбор порядковой статистики за среднее O(N) через алгоритм Хоара
/// (quickselect с рандомизацией).
/// </summary>
[Serializable]
public class QuickSelection<T> where T : IComparable<T>
{
    /// <summary>
    /// Возвращает элемент, стоящий на позиции <paramref name="orderStatistic"/>
    /// в отсортированной выборке. Порядок в массиве после вызова — частично нарушен.
    /// </summary>
    public static T Selection(T[] data, int orderStatistic)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Пустой массив", nameof(data));

        if (orderStatistic < 0) orderStatistic = 0;
        if (orderStatistic > data.Length - 1) orderStatistic = data.Length - 1;

        data.Shuffle();

        int s = 0, e = data.Length - 1;
        while (e > s)
        {
            int j = Partitions(data, s, e);
            if (j < orderStatistic) s = j + 1;
            else if (j > orderStatistic) e = j - 1;
            else return data[orderStatistic];
        }
        return data[orderStatistic];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Partitions(T[] data, int s, int e)
    {
        int s1 = s, e1 = e + 1;
        while (true)
        {
            while (Less(data[++s1], data[s]))
                if (s1 == e) break;

            while (Less(data[s], data[--e1]))
                if (e1 == s) break;

            if (s1 >= e1) break;
            ExCh(data, s1, e1);
        }
        ExCh(data, s, e1);
        return e1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Less(T a, T b) => a.CompareTo(b) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExCh(T[] data, int i, int j)
    {
        T t = data[i]; data[i] = data[j]; data[j] = t;
    }
}

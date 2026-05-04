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
        SortVec = new Vector(copy);
        _max = SortVec.Count - 1;
    }

    /// <summary>
    /// Квантиль уровня <paramref name="q"/> ∈ [0; 1] по отсортированному
    /// вектору. Использует ближайший ранг (nearest-rank).
    /// </summary>
    public double GetQuantile(double q)
    {
        if (_max < 0)
            throw new InvalidOperationException("Выборка пуста");

        if (q < 0.0 || q > 1.0)
            throw new ArgumentOutOfRangeException(nameof(q), "Квантиль должен быть в [0; 1]");

        int index = ClampIndex((int)Math.Round(q * _max));
        return SortVec[index];
    }

    /// <summary>
    /// Быстрый квантиль через выбор порядковой статистики за O(N)
    /// среднее (без полной сортировки). Не модифицирует вход.
    /// </summary>
    public static double FastQuantile(IAlgebraicStructure<double> structure, double q)
    {
        int n = structure.Shape.Count;
        if (n == 0)
            throw new InvalidOperationException("Выборка пуста");

        if (q < 0) q = 0;
        else if (q > 1) q = 1;

        double[] data = new double[n];
        Array.Copy(structure.Data, data, n);

        // Правильная формула: ранг в 0..n-1 пропорционально q.
        int ordinal = (int)Math.Round(q * (n - 1));
        return QuickSelection<double>.Selection(data, ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ClampIndex(int i)
    {
        if (i < 0) return 0;
        if (i > _max) return _max;
        return i;
    }
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

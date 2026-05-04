using System.Collections.Generic;

namespace AI.Geometry.Numerics;

/// <summary>
/// Суммирование по алгоритму Кэхэна для снижения ошибки округления.
/// </summary>
public static class KahanSum
{
    /// <summary>
    /// Суммирует последовательность чисел с компенсацией ошибки округления.
    /// </summary>
    public static double Sum(IEnumerable<double> values)
    {
        var acc = new KahanAccumulator();
        foreach (double v in values)
            acc.Add(v);
        return acc.Total;
    }
}

/// <summary>
/// Аккумулятор для пошагового суммирования по алгоритму Кэхэна.
/// </summary>
public struct KahanAccumulator
{
    private double _sum;
    private double _compensation;

    /// <summary>
    /// Текущая сумма.
    /// </summary>
    public double Total => _sum;

    /// <summary>
    /// Добавляет значение к сумме с компенсацией ошибки.
    /// </summary>
    public void Add(double value)
    {
        double y = value - _compensation;
        double t = _sum + y;
        _compensation = (t - _sum) - y;
        _sum = t;
    }
}

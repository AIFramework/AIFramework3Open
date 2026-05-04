using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI.Statistics;

/// <summary>
/// Описательная статистика + обёртки над потокобезопасным
/// генератором случайных чисел (<see cref="RandomEngine"/>).
/// 
/// Публичный контракт сохранён по именам и сигнатурам; под капотом
/// вся случайность идёт через <see cref="RandomEngine"/>, все
/// моменты считаются одно-проходно (Welford), генераторы Uniform/Normal
/// дедуплицированы через универсальный <c>Fill</c>-хелпер.
/// </summary>
[Serializable]
public partial class Statistic
{
    #region Поля и свойства

    private readonly Vector _vector;
    private readonly int _n;

    /// <summary>Оценка средне-квадратичного отклонения (СКО).</summary>
    public double STD { get; private set; }
    /// <summary>Минимальное значение выборки (NaN игнорируются).</summary>
    public double MinValue { get; private set; }
    /// <summary>Максимальное значение выборки (NaN игнорируются).</summary>
    public double MaxValue { get; private set; }
    /// <summary>Оценка несмещённой дисперсии.</summary>
    public double Variance { get; private set; }
    /// <summary>Оценка математического ожидания.</summary>
    public double Expected { get; private set; }

    #endregion

    #region Ctor

    /// <summary>
    /// Собирает статистики по вектору за один проход (Welford).
    /// </summary>
    public Statistic(IAlgebraicStructure<double> data)
    {
        _vector = data.Data;
        _n = _vector.Count;

        // Списочный бэкинг Vector -> Span без копирования.
        ReadOnlySpan<double> span = CollectionsMarshal.AsSpan(_vector);

        var (mean, variance, _) = StatUtils.Welford(span, skipNaN: true, unbiased: true);
        Expected = mean;
        Variance = variance;
        STD = Math.Sqrt(variance);

        (MinValue, MaxValue) = MinMax(span);
    }

    #endregion

    #region Генераторы: единый движок

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector FillVector(int n, Random rng, bool gaussian)
    {
        Vector v = new Vector(n);
        if (gaussian)
            for (int i = 0; i < n; i++) v[i] = RandomEngine.NextGaussian(rng);
        else
            for (int i = 0; i < n; i++) v[i] = rng.NextDouble();
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix FillMatrix(int h, int w, Random rng, bool gaussian)
    {
        Matrix m = new Matrix(h, w);
        double[] data = m.Data;
        if (gaussian) RandomEngine.FillGaussian(data, rng);
        else RandomEngine.FillUniform(data, rng);
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Tensor FillTensor(int h, int w, int d, Random rng, bool gaussian)
    {
        Tensor t = new Tensor(h, w, d);
        for (int k = 0; k < d; k++)
            for (int i = 0; i < h; i++)
                for (int j = 0; j < w; j++)
                    t[i, j, k] = gaussian ? RandomEngine.NextGaussian(rng) : rng.NextDouble();
        return t;
    }

    #endregion

    #region Min / Max

    private static (double min, double max) MinMax(ReadOnlySpan<double> data)
    {
        double min = double.MaxValue, max = double.MinValue;
        bool any = false;
        for (int i = 0; i < data.Length; i++)
        {
            double x = data[i];
            if (double.IsNaN(x)) continue;
            any = true;
            if (x < min) min = x;
            if (x > max) max = x;
        }
        return any ? (min, max) : (0.0, 0.0);
    }

    /// <summary>Максимум по алгебраической структуре (NaN игнорируются).</summary>
    public static double MaximalValue(IAlgebraicStructure<double> array)
    {
        return MinMax(array.Data).max;
    }

    /// <summary>Минимум по алгебраической структуре (NaN игнорируются).</summary>
    public static double MinimalValue(IAlgebraicStructure<double> array)
    {
        return MinMax(array.Data).min;
    }

    #endregion
}

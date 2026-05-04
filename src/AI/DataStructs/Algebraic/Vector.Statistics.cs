using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using Vd = System.Numerics.Vector<double>;

namespace AI.DataStructs.Algebraic;

public partial class Vector
{
    #region Статистика

    /// <summary>
    /// Нормализация вектора от 0 до 1
    /// </summary>
    public Vector Minimax()
    {
        double max = Max();
        double min = Min();
        double range = max - min;

        if (Math.Abs(range) < double.Epsilon)
            return new Vector(Count) + 0.5;

        double d = 1.0 / range;
        return Transform(x => (x - min) * d);
    }

    /// <summary>
    /// Минимальное значение (NaN игнорируются)
    /// </summary>
    public double Min()
    {
        double val = double.MaxValue;
        for (int i = 0; i < Count; i++)
            if (this[i] < val && !double.IsNaN(this[i]))
                val = this[i];
        return val;
    }

    /// <summary>
    /// Максимальное значение (NaN игнорируются)
    /// </summary>
    public double Max()
    {
        double val = double.MinValue;
        for (int i = 0; i < Count; i++)
            if (this[i] > val && !double.IsNaN(this[i]))
                val = this[i];
        return val;
    }

    /// <summary>
    /// Максимальное по модулю значение (NaN игнорируются)
    /// </summary>
    public double MaxAbs()
    {
        double val = double.MinValue;
        for (int i = 0; i < Count; i++)
        {
            double abs = Math.Abs(this[i]);
            if (!double.IsNaN(abs) && abs > val)
                val = abs;
        }
        return val;
    }

    /// <summary>
    /// Минимальное по модулю значение (NaN игнорируются)
    /// </summary>
    public double MinAbs()
    {
        double val = double.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            double abs = Math.Abs(this[i]);
            if (!double.IsNaN(abs) && abs < val)
                val = abs;
        }
        return val;
    }

    /// <summary>
    /// Среднее арифметическое
    /// </summary>
    public double Mean() => Statistic.ExpectedValue(this);

    /// <summary>
    /// Сумма компонент вектора (SIMD)
    /// </summary>
    public double Sum()
    {
        double[] a = ToArray();
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var acc = Vd.Zero;
        for (int i = 0; i < end; i += w) acc += new Vd(a, i);
        double sum = 0;
        for (int i = 0; i < w; i++) sum += acc[i];
        for (int i = end; i < n; i++) sum += a[i];
        return sum;
    }

    /// <summary>
    /// Содержит ли вектор NaN
    /// </summary>
    public bool ContainsNan()
    {
        for (int i = 0; i < Count; i++)
            if (double.IsNaN(this[i])) return true;
        return false;
    }

    /// <summary>
    /// Дисперсия
    /// </summary>
    public double Dispersion() => Statistic.CalcVariance(this);

    /// <summary>
    /// Среднеквадратичное отклонение
    /// </summary>
    public double Std() => Statistic.CalcStd(this);

    /// <summary>
    /// L2 норма вектора
    /// </summary>
    public double NormL2() => AnalyticGeometryFunctions.NormVect(this);

    /// <summary>
    /// Z-нормализация (среднее = 0, СКО = 1)
    /// </summary>
    public Vector ZNormalise()
    {
        double mean = Mean();
        double std = Std();

        if (std < double.Epsilon)
            return new Vector(Count);

        return (Clone() - mean) / std;
    }

    /// <summary>
    /// Нормализация с заданными средним и СКО
    /// </summary>
    public Vector Normalise(Vector mean, Vector std)
        => (Clone() - mean) / (std + AISettings.GlobalEps);

    #endregion

    #region Поиск индексов

    /// <summary>
    /// Индекс элемента с максимальным значением
    /// </summary>
    public int MaxElementIndex()
    {
        int indMax = 0;
        for (int i = 1; i < Count; i++)
            if (this[i] > this[indMax]) indMax = i;
        return indMax;
    }

    /// <summary>
    /// Индекс элемента с максимальным по модулю значением
    /// </summary>
    public int AbsoluteMaxElementIndex()
    {
        int indMax = 0;
        Vector vector = Transform(x => Math.Abs(x));
        for (int i = 1; i < Count; i++)
            if (vector[i] > vector[indMax]) indMax = i;
        return indMax;
    }

    /// <summary>
    /// Индекс элемента с минимальным значением
    /// </summary>
    public int MinElementIndex()
    {
        int indMin = 0;
        for (int i = 1; i < Count; i++)
            if (this[i] < this[indMin]) indMin = i;
        return indMin;
    }

    /// <summary>
    /// Индекс элемента с минимальным по модулю значением
    /// </summary>
    public int AbsoluteMinElementIndex()
    {
        int indMin = 0;
        Vector vector = Transform(x => Math.Abs(x));
        for (int i = 1; i < Count; i++)
            if (vector[i] < vector[indMin]) indMin = i;
        return indMin;
    }

    /// <summary>
    /// Индекс элемента с максимальным значением в регионе [a; b]
    /// </summary>
    public int MaxElementIndexInRegion(int a, int b)
    {
        int end = (b < Count) ? b + 1 : Count;
        int indMax = a;
        for (int i = a; i < end; i++)
            if (this[i] > this[indMax]) indMax = i;
        return indMax;
    }

    /// <summary>
    /// Индекс элемента с максимальным по модулю значением в регионе [a; b]
    /// </summary>
    public int AbsoluteMaxElementIndexInRegion(int a, int b)
    {
        int end = (b < Count) ? b + 1 : Count;
        int indMax = a;
        Vector vector = Transform(x => Math.Abs(x));
        for (int i = a + 1; i < end; i++)
            if (vector[i] > vector[indMax]) indMax = i;
        return indMax;
    }

    /// <summary>
    /// Индекс элемента с минимальным значением в регионе [a; b]
    /// </summary>
    public int MinElementIndexInRegion(int a, int b)
    {
        int end = (b < Count) ? b + 1 : Count;
        int indMin = a;
        for (int i = a; i < end; i++)
            if (this[i] < this[indMin]) indMin = i;
        return indMin;
    }

    /// <summary>
    /// Индекс элемента с минимальным по модулю значением в регионе [a; b]
    /// </summary>
    public int AbsoluteMinElementIndexInRegion(int a, int b)
    {
        int end = (b < Count) ? b + 1 : Count;
        int indMin = a;
        Vector vector = Transform(x => Math.Abs(x));
        for (int i = a + 1; i < end; i++)
            if (vector[i] < vector[indMin]) indMin = i;
        return indMin;
    }

    /// <summary>
    /// Индекс элемента, ближайшего к <paramref name="value"/> (минимум |this[i] − value|)
    /// </summary>
    public int IndexValueNeighborhoodMin(double value)
    {
        if (Count == 0) return 0;

        int best = 0;
        double bestAbs = Math.Abs(this[0] - value);
        for (int i = 1; i < Count; i++)
        {
            double a = Math.Abs(this[i] - value);
            if (a < bestAbs) { bestAbs = a; best = i; }
        }
        return best;
    }

    /// <summary>
    /// L1-норма (сумма модулей элементов).
    /// </summary>
    public double NormL1()
    {
        double sum = 0;
        for (int i = 0; i < Count; i++) sum += Math.Abs(this[i]);
        return sum;
    }

    #endregion
}

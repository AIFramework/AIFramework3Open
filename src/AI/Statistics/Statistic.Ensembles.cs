using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI.Statistics;

public partial class Statistic
{
    #region Ансамбли

    /// <summary>Среднее по ансамблю векторов.</summary>
    public static Vector MeanVector(IEnumerable<Vector> vectors)
    {
        Vector[] data = vectors.ToArray();
        if (data.Length == 0) return new Vector();

        Vector output = Functions.Summ(data);
        output /= data.Length;
        return output;
    }

    /// <summary>Среднее геометрическое (с учётом знаков).</summary>
    public static double MeanGeom(Vector vect)
    {
        int negatives = 0;
        for (int i = 0; i < vect.Count; i++)
            if (vect[i] < 0) negatives++;

        Vector res = FunctionsForEachElements.Ln(FunctionsForEachElements.Abs(vect));
        double sum = Functions.Summ(res) / vect.Count;

        return (negatives % 2 == 1) ? -Math.Exp(sum) : Math.Exp(sum);
    }

    /// <summary>Среднее гармоническое.</summary>
    public static double MeanGarmonic(Vector vect)
    {
        Vector res = 1 / vect;
        return vect.Count / Functions.Summ(res);
    }

    /// <summary>Среднеквадратичное значение.</summary>
    public static double RMS(Vector vect)
    {
        double s = 0.0;
        for (int i = 0; i < vect.Count; i++) s += vect[i] * vect[i];
        return Math.Sqrt(s / vect.Count);
    }

    /// <summary>Несмещённая дисперсия по ансамблю (покомпонентно).</summary>
    public static Vector EnsembleDispersion(IEnumerable<Vector> vectors)
    {
        Vector[] ensemble = vectors.ToArray();
        return EnsembleDispersion(ensemble, MeanVector(ensemble));
    }

    /// <summary>Несмещённая дисперсия по ансамблю с заданным средним.</summary>
    public static Vector EnsembleDispersion(Vector[] ensemble, Vector mean)
    {
        if (ensemble.Length == 0) return new Vector();

        Vector res = new Vector(ensemble[0].Count);
        for (int i = 0; i < ensemble.Length; i++)
            res += (ensemble[i] - mean).Transform(x => x * x);

        return ensemble.Length < 2 ? res : res / (ensemble.Length - 1);
    }

    /// <summary>СКО по ансамблю.</summary>
    public static Vector EnsembleStd(IEnumerable<Vector> ensemble)
        => EnsembleDispersion(ensemble).Transform(Math.Sqrt);

    /// <summary>СКО по ансамблю с заданным средним.</summary>
    public static Vector EnsembleStd(Vector[] ensemble, Vector mean)
        => EnsembleDispersion(ensemble, mean).Transform(Math.Sqrt);

    /// <summary>Максимум по ансамблю покомпонентно.</summary>
    public static Vector MaxEns(Vector[] ensemble)
    {
        if (ensemble.Length == 0) return new Vector();

        Vector res = new Vector(ensemble[0].Count);
        for (int i = 0; i < res.Count; i++)
        {
            double m = ensemble[0][i];
            for (int j = 1; j < ensemble.Length; j++)
                if (ensemble[j][i] > m) m = ensemble[j][i];
            res[i] = m;
        }
        return res;
    }

    /// <summary>Вектор с максимальной L2-энергией в ансамбле.</summary>
    public static Vector MaxEnergeVector(Vector[] ens)
    {
        if (ens.Length == 0) throw new ArgumentException("Пустой ансамбль", nameof(ens));

        Vector res = new Vector(ens.Length);
        for (int i = 0; i < res.Count; i++)
            res[i] = AnalyticGeometryFunctions.NormVect(ens[i]);

        double max = MaximalValue(res);
        int ind = res.FindIndex(el => el == max);
        return ens[ind].Clone();
    }

    #endregion

    #region Частотные характеристики

    /// <summary>
    /// Средняя частота (не нормированная, в долях шага
    /// дискретизации).
    /// </summary>
    public static double SimpleMeanFreq(Vector signal)
    {
        Vector centered = signal.Clone();
        centered -= ExpectedValue(centered);

        double e2 = 0, de2 = 0;
        double prev = centered[0];
        for (int i = 0; i < centered.Count; i++)
        {
            double x = centered[i];
            e2 += x * x;
            if (i > 0)
            {
                double d = x - prev;
                de2 += d * d;
            }
            prev = x;
        }
        return e2 == 0 ? 0 : Math.Sqrt(de2 / e2);
    }

    /// <summary>Средняя частота сигнала в Гц.</summary>
    public static double MeanFreq(Vector signal, double fd)
    {
        double k = fd / (2 * Math.PI);
        double w = SimpleMeanFreq(signal);
        return Math.Round(k * w, 3);
    }

    /// <summary>Девиация средней частоты.</summary>
    public static double DivFreq(Vector signal)
    {
        Vector dif = Functions.Diff(signal);
        double denom = SimpleMeanFreq(signal);
        return denom == 0 ? 0 : SimpleMeanFreq(dif) / denom;
    }

    /// <summary>Средний шаг между соседними отсчётами.</summary>
    public static double MeanStep(Vector vector, double eps = double.Epsilon)
    {
        if (vector.Count < 2) return eps;

        Vector dif = Functions.Diff(vector);
        double sum = 0.0;
        for (int i = 1; i < dif.Count; i++) sum += dif[i];

        double step = sum / (dif.Count - 1);
        return double.IsNaN(step) ? eps : step;
    }

    /// <summary>Средний шаг по диапазону: (max−min)/N + eps.</summary>
    public static double MeanStep2(Vector vector, double eps = double.Epsilon)
    {
        if (vector.Count == 0) return eps;
        double max = vector.Max();
        double min = vector.Min();
        return ((max - min) / vector.Count) + eps;
    }

    #endregion
}

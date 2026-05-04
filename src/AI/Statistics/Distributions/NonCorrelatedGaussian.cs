using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.Statistics.Distributions;

/// <summary>
/// Некоррелированный (диагональная ковариация) гауссов процесс.
/// 
/// Одна реализация работает и в 1D, и в ND-случае через
/// словари параметров «mean» и «std». Лог-плотность вычисляется
/// напрямую, без промежуточного умножения плотностей — это
/// численно устойчиво при больших размерностях.
/// </summary>
[Serializable]
public class NonCorrelatedGaussian : IDistribution
{
    // 0.5 * ln(2π). Через static readonly (в const нельзя вызывать Math.Log).
    private static readonly double s_halfLog2Pi = 0.5 * Math.Log(2.0 * Math.PI);

    #region Ключи параметров

    /// <summary>Имя параметра «среднее».</summary>
    public const string KeyMean = "mean";

    /// <summary>Имя параметра «СКО».</summary>
    public const string KeyStd = "std";

    #endregion

    #region Лог-плотность

    /// <inheritdoc/>
    public double CulcLogProb(double x, Dictionary<string, double> param_dist)
        => LogPdf(x, param_dist[KeyMean], StatUtils.SafeStd(param_dist[KeyStd]));

    /// <inheritdoc/>
    public double CulcLogProb(Vector x, Dictionary<string, Vector> param_dist)
    {
        Vector mean = param_dist[KeyMean];
        Vector std = param_dist[KeyStd];

        if (x.Count != mean.Count || mean.Count != std.Count)
            throw new ArgumentException("Размеры x, mean, std должны совпадать");

        double sum = 0.0;
        for (int i = 0; i < x.Count; i++)
            sum += LogPdf(x[i], mean[i], StatUtils.SafeStd(std[i]));
        return sum;
    }

    #endregion

    #region Плотность

    /// <inheritdoc/>
    public double CulcProb(Vector x, Dictionary<string, Vector> param_dist)
        => Math.Exp(CulcLogProb(x, param_dist));

    /// <inheritdoc/>
    public double CulcProb(double x, Dictionary<string, double> param_dist)
        => Math.Exp(CulcLogProb(x, param_dist));

    #endregion

    #region Сэмплирование

    /// <summary>Сэмпл одной 1D-реализации.</summary>
    public static double Sample(double mean, double std, Random rng)
        => RandomEngine.NextGaussian(rng, mean, StatUtils.SafeStd(std));

    /// <summary>Сэмпл одной ND-реализации с диагональной ковариацией.</summary>
    public static Vector Sample(Vector mean, Vector std, Random rng)
    {
        if (mean.Count != std.Count)
            throw new ArgumentException("mean и std должны быть одной длины");

        Vector v = new Vector(mean.Count);
        for (int i = 0; i < mean.Count; i++)
            v[i] = Sample(mean[i], std[i], rng);
        return v;
    }

    /// <summary>Сэмпл по параметрам из словаря.</summary>
    public Vector Sample(Dictionary<string, Vector> param_dist, Random rng)
        => Sample(param_dist[KeyMean], param_dist[KeyStd], rng);

    /// <summary>Сэмпл по параметрам из словаря (1D).</summary>
    public double Sample(Dictionary<string, double> param_dist, Random rng)
        => Sample(param_dist[KeyMean], param_dist[KeyStd], rng);

    #endregion

    #region Оценка параметров методом максимального правдоподобия

    /// <summary>
    /// ML-оценка параметров 1D-гауссианы по выборке. Возвращает
    /// словарь, пригодный для передачи в CulcProb/CulcLogProb.
    /// </summary>
    public static Dictionary<string, double> FitMaximumLikelihood(ReadOnlySpan<double> data)
    {
        var (mean, variance, _) = StatUtils.Welford(data, skipNaN: true, unbiased: false);
        return new Dictionary<string, double>
        {
            [KeyMean] = mean,
            [KeyStd] = Math.Sqrt(variance),
        };
    }

    /// <summary>
    /// ML-оценка параметров ND-диагональной гауссианы по выборке
    /// векторов.
    /// </summary>
    public static Dictionary<string, Vector> FitMaximumLikelihood(IReadOnlyList<Vector> samples)
    {
        if (samples == null || samples.Count == 0)
            throw new ArgumentException("Пустая выборка", nameof(samples));

        int dim = samples[0].Count;
        Vector mean = new Vector(dim);
        Vector std = new Vector(dim);

        // поэлементно считаем mean / variance
        double[] buf = new double[samples.Count];
        for (int d = 0; d < dim; d++)
        {
            for (int k = 0; k < samples.Count; k++) buf[k] = samples[k][d];
            var (m, v, _) = StatUtils.Welford(buf, skipNaN: true, unbiased: false);
            mean[d] = m;
            std[d] = Math.Sqrt(v);
        }

        return new Dictionary<string, Vector>
        {
            [KeyMean] = mean,
            [KeyStd] = std,
        };
    }

    #endregion

    #region Внутренности

    // log-плотность нормального: −0.5((x−μ)/σ)² − ln σ − 0.5 ln(2π)
    private static double LogPdf(double x, double mean, double std)
    {
        double z = (x - mean) / std;
        return (-0.5 * z * z) - Math.Log(std) - s_halfLog2Pi;
    }

    #endregion
}

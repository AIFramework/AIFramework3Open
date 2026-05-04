using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI.Statistics.MixtureModeling;

/// <summary>
/// EM-алгоритм подгонки гауссовой смеси (диагональная ковариация),
/// единообразно работающий для 1D и ND-данных.
/// 
/// Особенности реализации:
///   * инициализация средних через <b>k-means++</b> (устойчиво к
///     локальным минимумам);
///   * все вычисления — в лог-пространстве через log-sum-exp
///     (нет underflow при высокой размерности);
///   * E-шаг параллелится через <c>Parallel.For</c> с
///     поток-локальными аккумуляторами (потокобезопасно, линейное
///     ускорение по ядрам);
///   * критерий останова — относительное изменение
///     log-likelihood ниже заданного tol.
/// </summary>
public static class EM
{
    #region Параметры по умолчанию

    /// <summary>Максимальное число итераций EM по умолчанию.</summary>
    public const int DefaultMaxIter = 200;

    /// <summary>Относительный порог сходимости log-likelihood.</summary>
    public const double DefaultTol = 1e-6;

    #endregion

    #region Публичный API — 1D

    /// <summary>
    /// Подгонка 1D-гауссовой смеси по выборке скаляров.
    /// </summary>
    /// <param name="data">Одномерные наблюдения</param>
    /// <param name="numComponents">Число компонент K &gt; 0</param>
    /// <param name="maxIter">Максимум итераций</param>
    /// <param name="tol">Относительный критерий сходимости</param>
    /// <param name="seed">Seed для воспроизводимости</param>
    /// <param name="token">Токен отмены</param>
    public static GaussianMixture Fit(
        IReadOnlyList<double> data,
        int numComponents,
        int maxIter = DefaultMaxIter,
        double tol = DefaultTol,
        int? seed = null,
        CancellationToken token = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Count == 0) throw new ArgumentException("Пустая выборка", nameof(data));

        // переводим в ND-формат с Dim = 1 -> одна реализация EM
        Vector[] vectors = new Vector[data.Count];
        for (int i = 0; i < data.Count; i++) vectors[i] = new Vector(data[i]);

        return Fit(vectors, numComponents, maxIter, tol, seed, token);
    }

    #endregion

    #region Публичный API — ND

    /// <summary>
    /// Подгонка ND-гауссовой смеси (диагональная ковариация).
    /// </summary>
    public static GaussianMixture Fit(
        IReadOnlyList<Vector> data,
        int numComponents,
        int maxIter = DefaultMaxIter,
        double tol = DefaultTol,
        int? seed = null,
        CancellationToken token = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Count == 0) throw new ArgumentException("Пустая выборка", nameof(data));
        if (numComponents <= 0) throw new ArgumentException("K > 0", nameof(numComponents));

        int n = data.Count;
        int dim = data[0].Count;
        int k = numComponents;

        for (int i = 1; i < n; i++)
            if (data[i].Count != dim)
                throw new ArgumentException($"Размерность выборки {i} не совпадает с dim={dim}");

        Random rng = RandomEngine.Create(seed);

        // ---------- инициализация: k-means++ по средним + глобальная дисперсия ----------
        Vector[] means = KMeansPlusPlusInit(data, k, rng);
        Vector globalStd = ComputeGlobalStd(data);

        // клонируем глобальную СКО для каждой компоненты
        Vector[] stds = new Vector[k];
        for (int i = 0; i < k; i++) stds[i] = globalStd.Clone();

        Vector weights = new Vector(k);
        for (int i = 0; i < k; i++) weights[i] = 1.0 / k;

        var model = new GaussianMixture(weights, means, stds);

        // ---------- цикл EM ----------
        double prevLogL = double.NegativeInfinity;
        double logL = double.NegativeInfinity;

        for (int iter = 0; iter < maxIter; iter++)
        {
            token.ThrowIfCancellationRequested();

            var agg = EStep(model, data);
            logL = agg.LogLikelihood;

            if (iter > 0)
            {
                double denom = Math.Max(Math.Abs(prevLogL), 1.0);
                double rel = Math.Abs(logL - prevLogL) / denom;
                if (rel < tol) break;
            }
            prevLogL = logL;

            MStep(model, agg);
        }

        model.LogLikelihood = logL;
        return model;
    }

    #endregion

    #region E-шаг (параллельный, лог-пространство)

    private readonly struct Aggregate
    {
        public readonly double[] NkSum;        // Σ γ(z_ik)
        public readonly Vector[] MeanSum;      // Σ γ(z_ik) · x_i
        public readonly Vector[] SqSum;        // Σ γ(z_ik) · (x_i)^2 (для формулы через моменты)
        public readonly double LogLikelihood;

        public Aggregate(double[] nk, Vector[] m, Vector[] sq, double logL)
        {
            NkSum = nk; MeanSum = m; SqSum = sq; LogLikelihood = logL;
        }
    }

    private static Aggregate EStep(GaussianMixture model, IReadOnlyList<Vector> data)
    {
        int k = model.K, dim = model.Dim, n = data.Count;

        // Поток-локальные аккумуляторы: каждый поток накапливает свои,
        // потом результаты редьюсятся в глобальные под блокировкой.
        double[] globalNk = new double[k];
        Vector[] globalMean = new Vector[k];
        Vector[] globalSq = new Vector[k];
        for (int i = 0; i < k; i++)
        {
            globalMean[i] = new Vector(dim);
            globalSq[i] = new Vector(dim);
        }
        double globalLogL = 0.0;
        object gate = new object();

        Parallel.For(0, n,
            localInit: () =>
            {
                var nk = new double[k];
                var m = new Vector[k];
                var sq = new Vector[k];
                for (int i = 0; i < k; i++)
                {
                    m[i] = new Vector(dim);
                    sq[i] = new Vector(dim);
                }
                return (nk, m, sq, logL: 0.0);
            },
            body: (i, state, local) =>
            {
                Vector x = data[i];
                // logs[k] = log w_k + log N(x | μ_k, Σ_k)
                double[] logs = new double[k];
                for (int c = 0; c < k; c++)
                    logs[c] = StatUtils.SafeLog(model.Weights[c]) + model.ComponentLogProb(c, x);

                double lse = StatUtils.LogSumExp(logs);
                local.logL += lse;

                // γ_ic = exp(logs[c] - lse)
                for (int c = 0; c < k; c++)
                {
                    double gamma = Math.Exp(logs[c] - lse);
                    local.nk[c] += gamma;
                    Vector m = local.m[c];
                    Vector sq = local.sq[c];
                    for (int d = 0; d < dim; d++)
                    {
                        double xd = x[d];
                        m[d] += gamma * xd;
                        sq[d] += gamma * xd * xd;
                    }
                }
                return local;
            },
            localFinally: local =>
            {
                lock (gate)
                {
                    for (int c = 0; c < k; c++)
                    {
                        globalNk[c] += local.nk[c];
                        for (int d = 0; d < dim; d++)
                        {
                            globalMean[c][d] += local.m[c][d];
                            globalSq[c][d] += local.sq[c][d];
                        }
                    }
                    globalLogL += local.logL;
                }
            });

        return new Aggregate(globalNk, globalMean, globalSq, globalLogL);
    }

    #endregion

    #region M-шаг

    private static void MStep(GaussianMixture model, Aggregate agg)
    {
        int k = model.K, dim = model.Dim;
        double n = 0;
        for (int c = 0; c < k; c++) n += agg.NkSum[c];
        if (n <= 0) return;

        for (int c = 0; c < k; c++)
        {
            double nk = agg.NkSum[c];
            if (nk <= 0)
            {
                // пустая компонента — оставляем веса малыми, параметры без изменений
                model.Weights[c] = AISettings.GlobalEps;
                continue;
            }

            model.Weights[c] = nk / n;

            Vector newMean = new Vector(dim);
            for (int d = 0; d < dim; d++) newMean[d] = agg.MeanSum[c][d] / nk;
            model.Means[c] = newMean;

            Vector newStd = new Vector(dim);
            for (int d = 0; d < dim; d++)
            {
                // E[x²] − (E[x])²
                double variance = (agg.SqSum[c][d] / nk) - (newMean[d] * newMean[d]);
                if (variance < 0) variance = 0;
                newStd[d] = Math.Sqrt(variance);
                if (newStd[d] < model.MinStd) newStd[d] = model.MinStd;
            }
            model.Stds[c] = newStd;
        }

        // веса могут чуть «поехать» из-за пустых компонент — перенормируем
        double totalW = 0;
        for (int c = 0; c < k; c++) totalW += model.Weights[c];
        if (totalW > 0)
            for (int c = 0; c < k; c++) model.Weights[c] /= totalW;
    }

    #endregion

    #region Инициализация: k-means++

    // Стандартная k-means++ схема:
    // 1) выбрать случайный центр из data;
    // 2) выбрать следующий центр с вероятностью, пропорциональной
    //    квадрату расстояния до ближайшего уже выбранного;
    // 3) повторить до k центров.
    private static Vector[] KMeansPlusPlusInit(IReadOnlyList<Vector> data, int k, Random rng)
    {
        int n = data.Count;
        Vector[] centers = new Vector[k];
        double[] d2 = new double[n];

        int firstIdx = rng.Next(n);
        centers[0] = data[firstIdx].Clone();
        for (int i = 0; i < n; i++) d2[i] = SqDist(data[i], centers[0]);

        for (int c = 1; c < k; c++)
        {
            double total = 0;
            for (int i = 0; i < n; i++) total += d2[i];
            if (total <= 0)
            {
                // все точки уже совпадают с выбранными центрами — добираем случайными
                centers[c] = data[rng.Next(n)].Clone();
                continue;
            }

            double u = rng.NextDouble() * total;
            double acc = 0;
            int picked = n - 1;
            for (int i = 0; i < n; i++)
            {
                acc += d2[i];
                if (acc >= u) { picked = i; break; }
            }

            centers[c] = data[picked].Clone();
            for (int i = 0; i < n; i++)
            {
                double cand = SqDist(data[i], centers[c]);
                if (cand < d2[i]) d2[i] = cand;
            }
        }

        return centers;
    }

    private static double SqDist(Vector a, Vector b)
    {
        double s = 0;
        for (int i = 0; i < a.Count; i++)
        {
            double d = a[i] - b[i];
            s += d * d;
        }
        return s;
    }

    private static Vector ComputeGlobalStd(IReadOnlyList<Vector> data)
    {
        int dim = data[0].Count, n = data.Count;
        // поэлементно через Welford
        Vector mean = new Vector(dim);
        Vector m2 = new Vector(dim);
        for (int i = 0; i < n; i++)
        {
            Vector x = data[i];
            double invN = 1.0 / (i + 1);
            for (int d = 0; d < dim; d++)
            {
                double delta = x[d] - mean[d];
                mean[d] += delta * invN;
                m2[d] += delta * (x[d] - mean[d]);
            }
        }
        Vector std = new Vector(dim);
        double denom = n > 1 ? (n - 1) : 1;
        for (int d = 0; d < dim; d++)
            std[d] = Math.Max(Math.Sqrt(m2[d] / denom), 1e-3);
        return std;
    }

    #endregion
}

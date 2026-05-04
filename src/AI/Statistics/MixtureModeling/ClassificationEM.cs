using AI.DataStructs.Algebraic;
using AI.Statistics.Distributions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI.Statistics.MixtureModeling;

/// <summary>
/// Classification EM (индикаторный / Hard-EM) для гетерогенных смесей.
/// 
/// Единая реализация для 1D и ND данных. E-шаг — жёсткое назначение
/// (argmax), M-шаг — MLE по кластерам. O(n·K) на итерацию, E-шаг
/// параллелен через <see cref="Parallel.For"/>.
/// 
/// Компоненты должны реализовывать <see cref="IRefittable"/>.
/// </summary>
public static class ClassificationEM
{
    /// <summary>Результат Classification EM (общий для 1D и ND).</summary>
    public sealed class FitResult
    {
        /// <summary>Подогнанные компоненты.</summary>
        public IDistributionWithoutParams[] Components { get; init; } = Array.Empty<IDistributionWithoutParams>();

        /// <summary>Нормированные веса.</summary>
        public double[] Weights { get; init; } = Array.Empty<double>();

        /// <summary>Жёсткие назначения z_i ∈ {0..K-1}.</summary>
        public int[] Assignments { get; init; } = Array.Empty<int>();

        /// <summary>Log-likelihood финальная.</summary>
        public double LogLikelihood { get; init; }

        /// <summary>Число итераций.</summary>
        public int Iterations { get; init; }

        /// <summary>Построить <see cref="MixtureModel"/> из результата.</summary>
        public MixtureModel ToMixtureModel()
            => new(Components, new Vector(Weights));
    }

    #region Публичный API

    /// <summary>
    /// Подгонка 1D-смеси. Компоненты должны реализовывать <see cref="IRefittable"/>.
    /// </summary>
    public static FitResult Fit(
        double[] data,
        IDistributionWithoutParams[] initialComponents,
        int maxIter = 100,
        double tol = 1e-5,
        CancellationToken token = default)
    {
        ValidateComponents(initialComponents);
        int n = data?.Length ?? throw new ArgumentException("data == null");
        if (n == 0) throw new ArgumentException("Пустая выборка", nameof(data));

        return RunCore(
            n, initialComponents.Length, maxIter, tol, token,
            logProb: (comp, i) => comp.CulcLogProb(data[i]),
            refit: (comp, indices, count) =>
            {
                var buf = new double[count];
                for (int j = 0; j < count; j++) buf[j] = data[indices[j]];
                return ((IRefittable)comp).Refit1D(buf, count);
            },
            initialComponents);
    }

    /// <summary>
    /// Подгонка ND-смеси. Компоненты должны реализовывать <see cref="IRefittable"/>.
    /// </summary>
    public static FitResult FitND(
        Vector[] data,
        IDistributionWithoutParams[] initialComponents,
        int maxIter = 100,
        double tol = 1e-5,
        CancellationToken token = default)
    {
        ValidateComponents(initialComponents);
        int n = data?.Length ?? throw new ArgumentException("data == null");
        if (n == 0) throw new ArgumentException("Пустая выборка", nameof(data));

        return RunCore(
            n, initialComponents.Length, maxIter, tol, token,
            logProb: (comp, i) => comp.CulcLogProb(data[i]),
            refit: (comp, indices, count) =>
            {
                var buf = new Vector[count];
                for (int j = 0; j < count; j++) buf[j] = data[indices[j]];
                return ((IRefittable)comp).RefitND(buf, count);
            },
            initialComponents);
    }

    #endregion

    #region Ядро алгоритма (общее для 1D и ND)

    private static FitResult RunCore(
        int n, int k, int maxIter, double tol,
        CancellationToken token,
        Func<IDistributionWithoutParams, int, double> logProb,
        Func<IDistributionWithoutParams, int[], int, IDistributionWithoutParams> refit,
        IDistributionWithoutParams[] initialComponents)
    {
        var components = new IDistributionWithoutParams[k];
        Array.Copy(initialComponents, components, k);

        var weights = new double[k];
        for (int c = 0; c < k; c++) weights[c] = 1.0 / k;

        var assignments = new int[n];
        double prevLogL = double.NegativeInfinity;
        int iter = 0;

        for (; iter < maxIter; iter++)
        {
            token.ThrowIfCancellationRequested();

            // ── E-шаг (параллельный): z_i = argmax_k [log w_k + log p_k(x_i)] ──
            double logL = EStepParallel(n, k, components, weights, assignments, logProb);

            // Проверка сходимости
            if (iter > 0)
            {
                double denom = Math.Max(Math.Abs(prevLogL), 1.0);
                if (Math.Abs(logL - prevLogL) / denom < tol) { iter++; break; }
            }
            prevLogL = logL;

            // ── M-шаг: пере-оценка весов + refit компонент ──
            MStep(n, k, components, weights, assignments, refit);
        }

        // Финальный log-likelihood
        double finalLogL = ComputeLogLikelihood(n, k, components, weights, logProb);

        return new FitResult
        {
            Components = components,
            Weights = weights,
            Assignments = assignments,
            LogLikelihood = finalLogL,
            Iterations = iter
        };
    }

    /// <summary>Параллельный E-шаг. Возвращает log-likelihood.</summary>
    private static double EStepParallel(
        int n, int k,
        IDistributionWithoutParams[] components,
        double[] weights,
        int[] assignments,
        Func<IDistributionWithoutParams, int, double> logProb)
    {
        // Предвычисление log-весов
        double[] logW = new double[k];
        for (int c = 0; c < k; c++)
            logW[c] = Math.Log(Math.Max(weights[c], 1e-300));

        double globalLogL = 0;
        object gate = new();

        Parallel.For(0, n,
            localInit: () => 0.0,
            body: (i, _, localLogL) =>
            {
                double bestScore = double.NegativeInfinity;
                int bestK = 0;
                // Для log-likelihood нужен log-sum-exp всех компонент
                double maxScore = double.NegativeInfinity;

                Span<double> scores = k <= 16 ? stackalloc double[k] : new double[k];
                for (int c = 0; c < k; c++)
                {
                    double s = logW[c] + logProb(components[c], i);
                    scores[c] = s;
                    if (s > bestScore) { bestScore = s; bestK = c; }
                    if (s > maxScore) maxScore = s;
                }
                assignments[i] = bestK;

                // log-sum-exp для likelihood
                double sumExp = 0;
                for (int c = 0; c < k; c++)
                    sumExp += Math.Exp(scores[c] - maxScore);
                return localLogL + maxScore + Math.Log(sumExp);
            },
            localFinally: localLogL =>
            {
                lock (gate) { globalLogL += localLogL; }
            });

        return globalLogL;
    }

    /// <summary>M-шаг: обновление весов и refit компонент.</summary>
    private static void MStep(
        int n, int k,
        IDistributionWithoutParams[] components,
        double[] weights,
        int[] assignments,
        Func<IDistributionWithoutParams, int[], int, IDistributionWithoutParams> refit)
    {
        // Подсчёт размеров кластеров + сбор индексов
        var counts = new int[k];
        for (int i = 0; i < n; i++) counts[assignments[i]]++;

        // Обновление весов
        double wSum = 0;
        for (int c = 0; c < k; c++)
        {
            weights[c] = Math.Max((double)counts[c] / n, 1e-10);
            wSum += weights[c];
        }
        for (int c = 0; c < k; c++) weights[c] /= wSum;

        // Refit каждой компоненты (параллельно по компонентам)
        Parallel.For(0, k, c =>
        {
            if (counts[c] < 2) return;

            var indices = new int[counts[c]];
            int idx = 0;
            for (int i = 0; i < n; i++)
                if (assignments[i] == c)
                    indices[idx++] = i;

            components[c] = refit(components[c], indices, counts[c]);
        });
    }

    private static double ComputeLogLikelihood(
        int n, int k,
        IDistributionWithoutParams[] components,
        double[] weights,
        Func<IDistributionWithoutParams, int, double> logProb)
    {
        double[] logW = new double[k];
        for (int c = 0; c < k; c++)
            logW[c] = Math.Log(Math.Max(weights[c], 1e-300));

        double totalLogL = 0;
        object gate = new();

        Parallel.For(0, n,
            localInit: () => 0.0,
            body: (i, _, local) =>
            {
                double max = double.NegativeInfinity;
                Span<double> scores = k <= 16 ? stackalloc double[k] : new double[k];
                for (int c = 0; c < k; c++)
                {
                    scores[c] = logW[c] + logProb(components[c], i);
                    if (scores[c] > max) max = scores[c];
                }
                double sumExp = 0;
                for (int c = 0; c < k; c++) sumExp += Math.Exp(scores[c] - max);
                return local + max + Math.Log(sumExp);
            },
            localFinally: local => { lock (gate) { totalLogL += local; } });

        return totalLogL;
    }

    #endregion

    #region Валидация

    private static void ValidateComponents(IDistributionWithoutParams[] comps)
    {
        if (comps == null || comps.Length == 0)
            throw new ArgumentException("Нет компонент");
        for (int c = 0; c < comps.Length; c++)
            if (comps[c] is not IRefittable)
                throw new ArgumentException(
                    $"Компонента {c} ({comps[c].GetType().Name}) не реализует IRefittable");
    }

    #endregion
}

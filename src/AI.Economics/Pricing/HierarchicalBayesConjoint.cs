using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Statistics;

namespace AI.Economics.Pricing;

/// <summary>
/// Иерархическая байесовская оценка conjoint-модели: индивидуальные
/// полезности вместо одной усреднённой.
/// </summary>
/// <remarks>
/// <para>
/// Агрегатный логит приписывает всем респондентам одинаковые предпочтения.
/// На неоднородной аудитории это не просто теряет информацию — оно смещает
/// симуляцию долей: половина, которой важна цена, и половина, которой важен
/// бренд, в среднем выглядят как аудитория, которой не важно ничего.
/// </para>
/// <para>
/// Модель: индивидуальные полезности <c>beta_i ~ N(mu, diag(sigma^2))</c>,
/// выбор внутри респондента описывается логитом. Оценка — выборка Гиббса:
/// шаг Метрополиса для каждого <c>beta_i</c>, сопряжённые обновления для
/// <c>mu</c> и дисперсий. Ковариационная матрица берётся диагональной:
/// полная требует обратного Уишарта и на типичных для отрасли выборках
/// в двести-триста респондентов оценивается неустойчиво.
/// </para>
/// </remarks>
public sealed class HierarchicalBayesConjoint
{
    private double[][] _individualBetas = [];
    private double[] _populationMean = [];
    private double[] _populationSd = [];

    /// <summary>Средние индивидуальные полезности по респондентам.</summary>
    public IReadOnlyList<double[]> IndividualUtilities => _individualBetas;

    /// <summary>Среднее полезностей по популяции.</summary>
    public Vector PopulationMean => new(_populationMean);

    /// <summary>Стандартное отклонение полезностей по популяции.</summary>
    public Vector PopulationStdDev => new(_populationSd);

    /// <summary>План исследования, использованный при обучении.</summary>
    public ConjointDesign? Design { get; private set; }

    /// <summary>Доля принятых шагов Метрополиса — диагностика сходимости.</summary>
    public double AcceptanceRate { get; private set; }

    /// <summary>Обучает модель выборкой Гиббса с шагом Метрополиса.</summary>
    /// <param name="tasks">Задания на выбор с указанием респондента.</param>
    /// <param name="design">План исследования.</param>
    /// <param name="draws">Число сохраняемых выборок после прогрева.</param>
    /// <param name="burnIn">Число выборок прогрева.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Полезности популяции с оценкой разброса.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Заданий нет.</exception>
    public ConjointResult Fit(
        IReadOnlyList<ChoiceTask> tasks, ConjointDesign design,
        int draws = 2000, int burnIn = 1000, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(design);
        if (tasks.Count == 0) throw new ArgumentException("Нужно хотя бы одно задание.", nameof(tasks));

        Design = design;
        int k = design.ParameterCount;
        Random rng = RandomEngine.Create(seed);

        int[] respondents = [.. tasks.Select(t => t.Respondent).Distinct().OrderBy(r => r)];
        var index = respondents.Select((r, i) => (r, i)).ToDictionary(p => p.r, p => p.i);
        int m = respondents.Length;

        var byRespondent = new List<ChoiceTask>[m];
        for (int i = 0; i < m; i++) byRespondent[i] = [];
        foreach (ChoiceTask task in tasks) byRespondent[index[task.Respondent]].Add(task);

        var encoded = new double[m][][][];
        for (int i = 0; i < m; i++) encoded[i] = MultinomialLogit.Encode(byRespondent[i], design);

        // Старт из агрегатной оценки: она даёт разумный центр и резко
        // сокращает прогрев по сравнению со стартом из нуля
        var aggregate = new MultinomialLogit();
        aggregate.Fit(tasks, design);
        double[] mu = [.. aggregate.Coefficients];

        var sigma2 = new double[k];
        for (int a = 0; a < k; a++) sigma2[a] = 1.0;

        var beta = new double[m][];
        for (int i = 0; i < m; i++) beta[i] = (double[])mu.Clone();

        var sumBeta = new double[m][];
        for (int i = 0; i < m; i++) sumBeta[i] = new double[k];
        var sumMu = new double[k];
        var sumSigma = new double[k];

        double step = 0.35;
        long accepted = 0, proposed = 0;
        int total = burnIn + draws;

        for (int iteration = 0; iteration < total; iteration++)
        {
            // ── Шаг 1. Индивидуальные полезности, Метрополис со случайным блужданием ──
            for (int i = 0; i < m; i++)
            {
                var candidate = new double[k];
                for (int a = 0; a < k; a++)
                    candidate[a] = beta[i][a] + (RandomEngine.NextGaussian(rng) * step * Math.Sqrt(sigma2[a]));

                double current = MultinomialLogit.LogLikelihood(encoded[i], byRespondent[i], beta[i])
                               + LogPrior(beta[i], mu, sigma2);
                double proposedValue = MultinomialLogit.LogLikelihood(encoded[i], byRespondent[i], candidate)
                                     + LogPrior(candidate, mu, sigma2);

                proposed++;
                if (Math.Log(rng.NextDouble()) < proposedValue - current)
                {
                    beta[i] = candidate;
                    accepted++;
                }
            }

            // ── Шаг 2. Среднее популяции при плоском априорном распределении ──
            for (int a = 0; a < k; a++)
            {
                double mean = 0;
                for (int i = 0; i < m; i++) mean += beta[i][a];
                mean /= m;
                mu[a] = mean + (RandomEngine.NextGaussian(rng) * Math.Sqrt(sigma2[a] / m));
            }

            // ── Шаг 3. Дисперсии из обратного гамма-распределения ──
            for (int a = 0; a < k; a++)
            {
                double ss = 0;
                for (int i = 0; i < m; i++) ss += (beta[i][a] - mu[a]) * (beta[i][a] - mu[a]);

                double shape = (m + 2) / 2.0;
                double scale = 2.0 / (ss + 2.0);
                sigma2[a] = 1.0 / Math.Max(RandomEngine.NextGamma(rng, shape, scale), 1e-8);
            }

            if (iteration < burnIn) continue;

            for (int a = 0; a < k; a++)
            {
                sumMu[a] += mu[a];
                sumSigma[a] += Math.Sqrt(sigma2[a]);
                for (int i = 0; i < m; i++) sumBeta[i][a] += beta[i][a];
            }
        }

        _populationMean = new double[k];
        _populationSd = new double[k];
        for (int a = 0; a < k; a++)
        {
            _populationMean[a] = sumMu[a] / draws;
            _populationSd[a] = sumSigma[a] / draws;
        }

        _individualBetas = new double[m][];
        for (int i = 0; i < m; i++)
        {
            _individualBetas[i] = new double[k];
            for (int a = 0; a < k; a++) _individualBetas[i][a] = sumBeta[i][a] / draws;
        }

        AcceptanceRate = proposed > 0 ? (double)accepted / proposed : 0;

        // Качество посадки оценивается на индивидуальных полезностях: именно
        // они, а не среднее популяции, предсказывают выбор конкретного человека
        double[][][] allEncoded = MultinomialLogit.Encode(tasks, design);
        var standardErrors = new double[k];
        for (int a = 0; a < k; a++) standardErrors[a] = _populationSd[a] / Math.Sqrt(m);

        ConjointResult result = MultinomialLogit.BuildResult(
            tasks, design, allEncoded, _populationMean, standardErrors,
            isHierarchical: true, _populationSd);

        return result with { HitRate = IndividualHitRate(tasks, byRespondent, encoded) };
    }

    /// <summary>
    /// Симулятор долей по индивидуальным полезностям: доли усредняются по
    /// респондентам, а не считаются от среднего — на неоднородной аудитории
    /// это разные числа.
    /// </summary>
    /// <param name="profiles">Конфигурации товаров.</param>
    /// <returns>Доли выбора, суммирующиеся в единицу.</returns>
    /// <exception cref="InvalidOperationException">Модель не обучена.</exception>
    public Vector SimulateShares(IReadOnlyList<ConjointProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (Design is null) throw new InvalidOperationException("Сначала обучите модель.");

        double[][] rows = [.. profiles.Select(Design.Encode)];
        var shares = new double[profiles.Count];

        foreach (double[] individual in _individualBetas)
        {
            double[] p = MultinomialLogit.Probabilities(rows, individual);
            for (int j = 0; j < shares.Length; j++) shares[j] += p[j];
        }

        for (int j = 0; j < shares.Length; j++) shares[j] /= Math.Max(_individualBetas.Length, 1);
        return new Vector(shares);
    }

    private static double LogPrior(double[] beta, double[] mu, double[] sigma2)
    {
        double sum = 0;
        for (int a = 0; a < beta.Length; a++)
        {
            double d = beta[a] - mu[a];
            sum += -0.5 * ((d * d / sigma2[a]) + Math.Log(sigma2[a]));
        }
        return sum;
    }

    private double IndividualHitRate(
        IReadOnlyList<ChoiceTask> tasks, List<ChoiceTask>[] byRespondent, double[][][][] encoded)
    {
        int hits = 0;
        for (int i = 0; i < byRespondent.Length; i++)
        {
            for (int t = 0; t < byRespondent[i].Count; t++)
            {
                double[] p = MultinomialLogit.Probabilities(encoded[i][t], _individualBetas[i]);
                int best = 0;
                for (int j = 1; j < p.Length; j++) if (p[j] > p[best]) best = j;
                if (best == byRespondent[i][t].ChosenIndex) hits++;
            }
        }

        return tasks.Count > 0 ? (double)hits / tasks.Count : 0;
    }
}

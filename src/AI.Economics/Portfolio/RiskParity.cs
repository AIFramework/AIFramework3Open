using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Portfolio;

/// <summary>Способ построения портфеля равного риска.</summary>
public enum RiskParityMethod
{
    /// <summary>Обратная волатильность: простейшее приближение без учёта корреляций.</summary>
    InverseVolatility,

    /// <summary>Равный вклад в риск с учётом ковариаций.</summary>
    EqualRiskContribution,

    /// <summary>Иерархический паритет риска по кластерам корреляций.</summary>
    HierarchicalRiskParity,
}

/// <summary>Узел иерархической кластеризации активов.</summary>
/// <param name="Assets">Активы кластера.</param>
/// <param name="Variance">Дисперсия кластера при внутреннем распределении весов.</param>
/// <param name="Weight">Вес кластера в портфеле.</param>
public sealed record RiskCluster(IReadOnlyList<string> Assets, double Variance, double Weight);

/// <summary>Результат построения портфеля равного риска.</summary>
public sealed record RiskParityResult : IInterpretable
{
    /// <summary>Использованный метод.</summary>
    public RiskParityMethod Method { get; init; }

    /// <summary>Названия активов.</summary>
    public IReadOnlyList<string> Assets { get; init; } = [];

    /// <summary>Веса активов.</summary>
    public Vector Weights { get; init; } = new(0);

    /// <summary>Вклады активов в риск.</summary>
    public IReadOnlyList<(string Asset, double Weight, double RiskContribution)> RiskBudget { get; init; } = [];

    /// <summary>Риск портфеля.</summary>
    public double Risk { get; init; }

    /// <summary>Максимальное отклонение вклада в риск от равного.</summary>
    public double MaximumDeviation { get; init; }

    /// <summary>Кластеры при иерархическом построении.</summary>
    public IReadOnlyList<RiskCluster> Clusters { get; init; } = [];

    /// <summary>Эффективное число активов.</summary>
    public double EffectiveAssets =>
        Weights.Count > 0 && Weights.Sum(w => w * w) > 0 ? 1 / Weights.Sum(w => w * w) : 0;

    /// <summary>Коэффициент диверсификации: взвешенная волатильность к риску портфеля.</summary>
    public double DiversificationRatio { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (string Asset, double Weight, double RiskContribution) heaviest =
            RiskBudget.OrderByDescending(r => r.Weight).FirstOrDefault();

        bool balanced = MaximumDeviation < 0.02;

        var builder = new InterpretationBuilder($"Паритет риска: {MethodName()}")
            .Summary($"Портфель из {Assets.Count} активов с риском {Fmt.Pct(Risk, 2)}. " +
                     $"Максимальное отклонение вклада в риск от равного " +
                     $"{Fmt.Pct(MaximumDeviation, 2)}. Коэффициент диверсификации " +
                     $"{Fmt.Num(DiversificationRatio, 2)}, эффективное число активов " +
                     $"{Fmt.Num(EffectiveAssets, 1)}.")
            .Metric("Риск портфеля", Risk, null, "стандартное отклонение доходности",
                MetricQuality.Neutral, 4)
            .Metric("Отклонение от паритета", MaximumDeviation, null,
                balanced ? "вклады в риск выровнены" : "вклады в риск различаются",
                balanced ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Коэффициент диверсификации", DiversificationRatio, null,
                "во сколько раз риск портфеля ниже взвешенной волатильности активов",
                DiversificationRatio > 1.3 ? MetricQuality.Good : MetricQuality.Neutral, 2)
            .Metric("Эффективное число активов", EffectiveAssets, null,
                $"из {Assets.Count}", MetricQuality.Neutral, 2);

        foreach ((string asset, double weight, double contribution) in RiskBudget)
        {
            builder.Metric(asset, weight, null,
                $"вклад в риск {Fmt.Pct(contribution, 2)}", MetricQuality.Unknown, 4);
        }

        foreach (RiskCluster cluster in Clusters)
        {
            builder.Metric($"Кластер: {string.Join(", ", cluster.Assets.Take(3))}" +
                           (cluster.Assets.Count > 3 ? $" и ещё {cluster.Assets.Count - 3}" : ""),
                cluster.Weight, null,
                $"дисперсия кластера {Fmt.Num(cluster.Variance, 6)}", MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Паритет риска отказывается от оценки ожидаемых доходностей и " +
                     "распределяет риск, а не капитал. Это делает его устойчивее " +
                     "оптимизации по средней и дисперсии: ковариации оцениваются " +
                     "надёжнее доходностей на порядок.")
            .FindingIf(heaviest.Asset is not null,
                $"Наибольший вес получил «{heaviest.Asset}» ({Fmt.Pct(heaviest.Weight, 1)}) — " +
                "как правило, это самый низковолатильный актив набора. Именно поэтому " +
                "портфели паритета риска обычно смещены в облигации.")
            .FindingIf(Method == RiskParityMethod.HierarchicalRiskParity,
                "Иерархический метод не требует обращения ковариационной матрицы " +
                "и потому устойчив при числе активов, сопоставимом с длиной истории. " +
                "Обычная оптимизация в этой ситуации даёт бессмысленные веса.")
            .FindingIf(Method == RiskParityMethod.InverseVolatility,
                "Обратная волатильность игнорирует корреляции. Если в наборе есть " +
                "группа сильно связанных активов, их совокупный вклад в риск " +
                "окажется выше расчётного.")
            .WarningIf(!balanced && Method != RiskParityMethod.InverseVolatility,
                $"Вклады в риск разошлись на {Fmt.Pct(MaximumDeviation, 2)}. Увеличьте " +
                "число итераций или проверьте обусловленность ковариационной матрицы.")
            .WarningIf(DiversificationRatio < 1.1,
                $"Коэффициент диверсификации {Fmt.Num(DiversificationRatio, 2)} близок " +
                "к единице: активы сильно связаны, и распределение риска почти " +
                "не снижает общий риск портфеля.")
            .Warning("Паритет риска даёт низкую ожидаемую доходность в номинальном " +
                     "выражении, поскольку концентрируется в низковолатильных активах. " +
                     "На практике его применяют с плечом, а это возвращает в портфель " +
                     "риск финансирования.")
            .Recommendation("Сравните вклады в риск с весами: их расхождение показывает, " +
                            "насколько обманчиво выглядит распределение капитала.")
            .Recommendation("При числе активов, сравнимом с длиной истории, используйте " +
                            "иерархический метод: он не требует обращения матрицы " +
                            "и потому не разваливается на плохих данных.")
            .Build();
    }

    /// <summary>Читаемое название метода.</summary>
    private string MethodName() => Method switch
    {
        RiskParityMethod.InverseVolatility => "обратная волатильность",
        RiskParityMethod.EqualRiskContribution => "равный вклад в риск",
        _ => "иерархический паритет риска",
    };
}

/// <summary>
/// Портфели равного вклада в риск, включая иерархический паритет.
/// </summary>
/// <remarks>
/// <para>
/// Идея паритета риска в том, чтобы распределять не капитал, а риск. Вклад
/// актива в риск портфеля равен
/// </para>
/// <code>
/// RC_i = w_i * (Sigma w)_i / sqrt(w' Sigma w)
/// </code>
/// <para>
/// и условие равенства вкладов решается итеративно. В отличие от оптимизации по
/// средней и дисперсии, метод не требует ожидаемых доходностей — самой ненадёжно
/// оцениваемой величины в портфельной теории.
/// </para>
/// <para>
/// Иерархический паритет риска решает другую проблему: при числе активов,
/// сопоставимом с длиной истории, ковариационная матрица плохо обусловлена, и
/// её обращение даёт бессмысленные веса. Метод обходит обращение вовсе: активы
/// кластеризуются по корреляциям, кластеры рекурсивно делят капитал обратно
/// пропорционально своей дисперсии.
/// </para>
/// </remarks>
public static class RiskParity
{
    /// <summary>Строит портфель равного вклада в риск.</summary>
    /// <param name="covariance">Ковариационная матрица доходностей.</param>
    /// <param name="assets">Названия активов.</param>
    /// <param name="method">Способ построения.</param>
    /// <returns>Веса, вклады в риск и характеристики диверсификации.</returns>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    /// <exception cref="ArgumentException">Матрица не квадратная или активов меньше двух.</exception>
    public static RiskParityResult Build(
        Matrix covariance, IReadOnlyList<string>? assets = null,
        RiskParityMethod method = RiskParityMethod.EqualRiskContribution)
    {
        ArgumentNullException.ThrowIfNull(covariance);

        int n = covariance.Height;
        if (covariance.Width != n) throw new ArgumentException("Матрица должна быть квадратной.", nameof(covariance));
        if (n < 2) throw new ArgumentException("Нужно минимум два актива.", nameof(covariance));

        var names = new List<string>(n);
        for (int i = 0; i < n; i++)
            names.Add(assets is not null && i < assets.Count ? assets[i] : $"актив {i + 1}");

        var sigma = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) sigma[i, j] = covariance[i, j];

        var clusters = new List<RiskCluster>();

        double[] weights = method switch
        {
            RiskParityMethod.InverseVolatility => InverseVolatility(sigma),
            RiskParityMethod.HierarchicalRiskParity => Hierarchical(sigma, names, clusters),
            _ => EqualRiskContribution(sigma),
        };

        double variance = LinearAlgebra.QuadraticForm(weights, sigma);
        double risk = Math.Sqrt(Math.Max(variance, 0));
        double[] marginal = LinearAlgebra.Multiply(sigma, weights);

        var budget = new List<(string, double, double)>(n);
        double target = 1.0 / n, maxDeviation = 0;

        for (int i = 0; i < n; i++)
        {
            double contribution = variance > 1e-18 ? weights[i] * marginal[i] / variance : 0;
            budget.Add((names[i], weights[i], contribution));
            maxDeviation = Math.Max(maxDeviation, Math.Abs(contribution - target));
        }

        double weightedVolatility = 0;
        for (int i = 0; i < n; i++) weightedVolatility += weights[i] * Math.Sqrt(Math.Max(sigma[i, i], 0));

        var vector = new Vector(n);
        for (int i = 0; i < n; i++) vector[i] = weights[i];

        return new RiskParityResult
        {
            Method = method,
            Assets = names,
            Weights = vector,
            RiskBudget = budget,
            Risk = risk,
            MaximumDeviation = maxDeviation,
            Clusters = clusters,
            DiversificationRatio = risk > 1e-12 ? weightedVolatility / risk : 1,
        };
    }

    /// <summary>Веса обратно пропорционально волатильности.</summary>
    private static double[] InverseVolatility(double[,] sigma)
    {
        int n = sigma.GetLength(0);
        var weights = new double[n];
        double total = 0;

        for (int i = 0; i < n; i++)
        {
            double volatility = Math.Sqrt(Math.Max(sigma[i, i], 1e-18));
            weights[i] = 1 / volatility;
            total += weights[i];
        }

        for (int i = 0; i < n; i++) weights[i] /= total;
        return weights;
    }

    /// <summary>Итеративное выравнивание вкладов в риск.</summary>
    private static double[] EqualRiskContribution(double[,] sigma)
    {
        int n = sigma.GetLength(0);
        double[] weights = InverseVolatility(sigma);
        double target = 1.0 / n;

        // Мультипликативное обновление: вес растёт там, где вклад в риск ниже целевого
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            double variance = LinearAlgebra.QuadraticForm(weights, sigma);
            if (variance <= 1e-18) break;

            double[] marginal = LinearAlgebra.Multiply(sigma, weights);
            double shift = 0, total = 0;

            var updated = new double[n];

            for (int i = 0; i < n; i++)
            {
                double contribution = weights[i] * marginal[i] / variance;
                double ratio = contribution > 1e-18 ? target / contribution : 2;

                updated[i] = weights[i] * Math.Pow(ratio, 0.25);
                total += updated[i];
            }

            for (int i = 0; i < n; i++)
            {
                updated[i] /= total;
                shift += Math.Abs(updated[i] - weights[i]);
            }

            weights = updated;
            if (shift < 1e-14) break;
        }

        return weights;
    }

    /// <summary>Иерархический паритет риска через кластеризацию корреляций.</summary>
    private static double[] Hierarchical(double[,] sigma, IReadOnlyList<string> names, List<RiskCluster> clusters)
    {
        int n = sigma.GetLength(0);
        double[,] correlation = LinearAlgebra.ToCorrelation(sigma);

        // Расстояние между активами: корень из половины единицы минус корреляция
        var distance = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                distance[i, j] = Math.Sqrt(Math.Max(0.5 * (1 - correlation[i, j]), 0));

        List<int> order = SeriateBySimilarity(distance, n);

        var weights = new double[n];
        for (int i = 0; i < n; i++) weights[i] = 1;

        Bisect(order, sigma, weights, names, clusters);

        double total = weights.Sum();
        if (total > 0) for (int i = 0; i < n; i++) weights[i] /= total;

        return weights;
    }

    /// <summary>Упорядочивает активы так, чтобы похожие оказались рядом.</summary>
    private static List<int> SeriateBySimilarity(double[,] distance, int n)
    {
        var order = new List<int> { 0 };
        var remaining = Enumerable.Range(1, n - 1).ToList();

        while (remaining.Count > 0)
        {
            int last = order[^1];
            int nearest = remaining.OrderBy(i => distance[last, i]).First();

            order.Add(nearest);
            remaining.Remove(nearest);
        }

        return order;
    }

    /// <summary>Рекурсивное деление капитала между половинами кластера.</summary>
    private static void Bisect(
        List<int> indices, double[,] sigma, double[] weights,
        IReadOnlyList<string> names, List<RiskCluster> clusters)
    {
        if (indices.Count <= 1) return;

        int middle = indices.Count / 2;
        var left = indices.Take(middle).ToList();
        var right = indices.Skip(middle).ToList();

        double leftVariance = ClusterVariance(left, sigma);
        double rightVariance = ClusterVariance(right, sigma);

        // Капитал делится обратно пропорционально дисперсии половин
        double total = leftVariance + rightVariance;
        double leftShare = total > 1e-18 ? 1 - (leftVariance / total) : 0.5;

        foreach (int i in left) weights[i] *= leftShare;
        foreach (int i in right) weights[i] *= 1 - leftShare;

        clusters.Add(new RiskCluster([.. left.Select(i => names[i])], leftVariance, leftShare));
        clusters.Add(new RiskCluster([.. right.Select(i => names[i])], rightVariance, 1 - leftShare));

        Bisect(left, sigma, weights, names, clusters);
        Bisect(right, sigma, weights, names, clusters);
    }

    /// <summary>Дисперсия кластера при весах, обратных дисперсиям активов.</summary>
    private static double ClusterVariance(List<int> indices, double[,] sigma)
    {
        var weights = new double[indices.Count];
        double total = 0;

        for (int i = 0; i < indices.Count; i++)
        {
            weights[i] = 1 / Math.Max(sigma[indices[i], indices[i]], 1e-18);
            total += weights[i];
        }

        for (int i = 0; i < indices.Count; i++) weights[i] /= total;

        double variance = 0;
        for (int i = 0; i < indices.Count; i++)
            for (int j = 0; j < indices.Count; j++)
                variance += weights[i] * sigma[indices[i], indices[j]] * weights[j];

        return Math.Max(variance, 0);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Portfolio;

/// <summary>Результат оптимизации портфеля по ожидаемым потерям в хвосте.</summary>
public sealed record CvarOptimizationResult : IInterpretable
{
    /// <summary>Названия активов.</summary>
    public IReadOnlyList<string> Assets { get; init; } = [];

    /// <summary>Оптимальные веса.</summary>
    public Vector Weights { get; init; } = new(0);

    /// <summary>Ожидаемые потери в хвосте оптимального портфеля.</summary>
    public double ConditionalValueAtRisk { get; init; }

    /// <summary>Стоимость под риском оптимального портфеля.</summary>
    public double ValueAtRisk { get; init; }

    /// <summary>Ожидаемая доходность портфеля.</summary>
    public double ExpectedReturn { get; init; }

    /// <summary>Стандартное отклонение портфеля.</summary>
    public double Volatility { get; init; }

    /// <summary>Уровень доверия.</summary>
    public double Confidence { get; init; } = 0.95;

    /// <summary>Ожидаемые потери портфеля минимальной дисперсии для сравнения.</summary>
    public double MeanVarianceCvar { get; init; }

    /// <summary>Веса портфеля минимальной дисперсии.</summary>
    public Vector MeanVarianceWeights { get; init; } = new(0);

    /// <summary>Число сценариев.</summary>
    public int Scenarios { get; init; }

    /// <summary>Число сценариев в хвосте.</summary>
    public int TailScenarios { get; init; }

    /// <summary>Выигрыш по хвостовым потерям относительно портфеля минимальной дисперсии.</summary>
    public double TailImprovement =>
        MeanVarianceCvar > 0 ? (MeanVarianceCvar - ConditionalValueAtRisk) / MeanVarianceCvar : 0;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var shifts = new List<(string Asset, double Shift)>();
        for (int i = 0; i < Assets.Count && i < Weights.Count && i < MeanVarianceWeights.Count; i++)
            shifts.Add((Assets[i], Weights[i] - MeanVarianceWeights[i]));

        (string Asset, double Shift) largest = shifts.OrderByDescending(s => Math.Abs(s.Shift)).FirstOrDefault();
        bool worthIt = TailImprovement > 0.02;

        var builder = new InterpretationBuilder("Оптимизация по ожидаемым потерям в хвосте")
            .Summary($"На уровне {Fmt.Pct(Confidence, 0)} ожидаемые потери в хвосте " +
                     $"{Fmt.Pct(ConditionalValueAtRisk, 2)} против {Fmt.Pct(MeanVarianceCvar, 2)} " +
                     $"у портфеля минимальной дисперсии — выигрыш {Fmt.Pct(TailImprovement, 1)}. " +
                     $"Доходность {Fmt.Pct(ExpectedReturn, 2)}, волатильность " +
                     $"{Fmt.Pct(Volatility, 2)}. В хвосте {TailScenarios} сценариев " +
                     $"из {Scenarios}.")
            .Metric("Ожидаемые потери в хвосте", ConditionalValueAtRisk, null,
                $"порог {Fmt.Pct(ValueAtRisk, 2)}", MetricQuality.Neutral, 4)
            .Metric("Выигрыш по хвосту", TailImprovement, null,
                worthIt ? "оптимизация по хвосту оправдана" : "разница с дисперсией невелика",
                worthIt ? MetricQuality.Good : MetricQuality.Neutral, 3)
            .Metric("Доходность", ExpectedReturn, null, "ожидаемая доходность портфеля",
                MetricQuality.Neutral, 4)
            .Metric("Волатильность", Volatility, null, "стандартное отклонение",
                MetricQuality.Neutral, 4)
            .Metric("Сценариев в хвосте", TailScenarios, null,
                $"из {Scenarios}; на них и опирается оценка",
                TailScenarios >= 30 ? MetricQuality.Good : MetricQuality.Warning, 0);

        for (int i = 0; i < Assets.Count && i < Weights.Count; i++)
        {
            double reference = i < MeanVarianceWeights.Count ? MeanVarianceWeights[i] : 0;

            builder.Metric(Assets[i], Weights[i], null,
                $"против {Fmt.Pct(reference, 1)} при минимизации дисперсии",
                MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Дисперсия наказывает за отклонения в обе стороны, ожидаемые потери " +
                     "в хвосте — только за убытки. Для распределений с асимметрией " +
                     "и тяжёлыми хвостами это принципиально разные задачи.")
            .FindingIf(largest.Asset is not null && Math.Abs(largest.Shift) > 0.02,
                $"Сильнее всего изменился вес «{largest.Asset}»: {Fmt.Pct(largest.Shift, 2)} " +
                "относительно портфеля минимальной дисперсии. Обычно это актив " +
                "с редкими крупными убытками, которые дисперсия недооценивает.")
            .FindingIf(!worthIt,
                "Выигрыш по хвостовым потерям невелик: на этих данных распределение " +
                "близко к симметричному, и минимизация дисперсии даёт почти тот же портфель.")
            .Finding("Задача сводится к линейному программированию по представлению " +
                     "Рокафеллара и Урясева: минимизируется порог плюс средний избыток " +
                     "над ним. Здесь она решается субградиентным спуском.")
            .WarningIf(TailScenarios < 30,
                $"В хвост попало всего {TailScenarios} сценариев. Оценка ожидаемых потерь " +
                "по такой выборке неустойчива: понизьте уровень доверия или увеличьте " +
                "число сценариев.")
            .WarningIf(Scenarios < 250,
                $"Всего {Scenarios} сценариев. Оптимизация по хвосту требует существенно " +
                "больше данных, чем по дисперсии, — она использует только часть выборки.")
            .Warning("Оптимизация по историческим сценариям подгоняет портфель под " +
                     "конкретные реализовавшиеся убытки. Вне выборки выигрыш обычно " +
                     "меньше расчётного, а иногда исчезает вовсе.")
            .Recommendation("Сравнивайте результат с портфелем минимальной дисперсии: " +
                            "если разница мала, дополнительная сложность не нужна.")
            .Recommendation("Дополняйте исторические сценарии стрессовыми: именно ради " +
                            "редких событий и применяется этот критерий.")
            .Build();
    }
}

/// <summary>
/// Оптимизация портфеля по ожидаемым потерям в хвосте.
/// </summary>
/// <remarks>
/// <para>
/// Дисперсия штрафует отклонения в обе стороны и потому плохо подходит для
/// асимметричных распределений. Ожидаемые потери в хвосте учитывают только
/// убытки и обладают свойством субаддитивности, которого нет у стоимости под
/// риском.
/// </para>
/// <para>
/// Рокафеллар и Урясев показали, что задача сводится к выпуклой минимизации
/// по весам и вспомогательной переменной порога:
/// </para>
/// <code>
/// min_{w, v}  v + 1 / ((1 - a) * T) * sum_t max( -w' r_t - v, 0 )
/// </code>
/// <para>
/// Оптимальное значение вспомогательной переменной совпадает со стоимостью под
/// риском, а значение целевой функции — с ожидаемыми потерями в хвосте.
/// Функция выпукла и кусочно-линейна, поэтому решается субградиентным спуском
/// с проекцией на симплекс весов.
/// </para>
/// </remarks>
public static class CvarOptimization
{
    /// <summary>Минимизирует ожидаемые потери в хвосте при ограничении на доходность.</summary>
    /// <param name="scenarios">Сценарии доходностей: строка — сценарий, столбец — актив.</param>
    /// <param name="assets">Названия активов.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <param name="minimumReturn">Минимальная требуемая доходность портфеля.</param>
    /// <param name="maximumWeight">Максимальный вес одного актива.</param>
    /// <returns>Оптимальные веса и сравнение с портфелем минимальной дисперсии.</returns>
    /// <exception cref="ArgumentNullException">Сценарии не заданы.</exception>
    /// <exception cref="ArgumentException">Сценариев или активов недостаточно.</exception>
    public static CvarOptimizationResult Optimize(
        Matrix scenarios, IReadOnlyList<string>? assets = null,
        double confidence = 0.95, double minimumReturn = double.NegativeInfinity,
        double maximumWeight = 1)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        int t = scenarios.Height, n = scenarios.Width;
        if (t < 50) throw new ArgumentException("Нужно минимум пятьдесят сценариев.", nameof(scenarios));
        if (n < 2) throw new ArgumentException("Нужно минимум два актива.", nameof(scenarios));

        var names = new List<string>(n);
        for (int i = 0; i < n; i++)
            names.Add(assets is not null && i < assets.Count ? assets[i] : $"актив {i + 1}");

        var returns = new double[t, n];
        for (int s = 0; s < t; s++)
            for (int i = 0; i < n; i++) returns[s, i] = scenarios[s, i];

        var means = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int s = 0; s < t; s++) sum += returns[s, i];
            means[i] = sum / t;
        }

        double[] weights = Solve(returns, means, confidence, minimumReturn, maximumWeight);
        (double var, double cvar) = Evaluate(returns, weights, confidence, out int tailCount);

        double[,] covariance = LinearAlgebra.Covariance(returns);
        double[] minimumVariance = MinimumVariance(covariance, maximumWeight);
        (double _, double referenceCvar) = Evaluate(returns, minimumVariance, confidence, out _);

        double expectedReturn = LinearAlgebra.Dot(weights, means);
        double volatility = Math.Sqrt(Math.Max(LinearAlgebra.QuadraticForm(weights, covariance), 0));

        return new CvarOptimizationResult
        {
            Assets = names,
            Weights = ToVector(weights),
            ConditionalValueAtRisk = cvar,
            ValueAtRisk = var,
            ExpectedReturn = expectedReturn,
            Volatility = volatility,
            Confidence = confidence,
            MeanVarianceCvar = referenceCvar,
            MeanVarianceWeights = ToVector(minimumVariance),
            Scenarios = t,
            TailScenarios = tailCount,
        };
    }

    /// <summary>Ожидаемые потери в хвосте для заданных весов.</summary>
    /// <param name="scenarios">Сценарии доходностей.</param>
    /// <param name="weights">Веса активов.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Стоимость под риском и ожидаемые потери в хвосте.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static (double ValueAtRisk, double ConditionalValueAtRisk) Evaluate(
        Matrix scenarios, Vector weights, double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(weights);

        var returns = new double[scenarios.Height, scenarios.Width];
        for (int s = 0; s < scenarios.Height; s++)
            for (int i = 0; i < scenarios.Width; i++) returns[s, i] = scenarios[s, i];

        return Evaluate(returns, [.. weights], confidence, out _);
    }

    /// <summary>Субградиентный спуск по представлению Рокафеллара — Урясева.</summary>
    private static double[] Solve(
        double[,] returns, double[] means, double confidence, double minimumReturn, double maximumWeight)
    {
        int t = returns.GetLength(0), n = returns.GetLength(1);
        var weights = new double[n];
        for (int i = 0; i < n; i++) weights[i] = 1.0 / n;

        double threshold = 0;
        double alpha = 1 - confidence;
        double step = 0.05;

        for (int iteration = 0; iteration < 6000; iteration++)
        {
            var gradient = new double[n];
            double thresholdGradient = 1;
            int exceed = 0;

            for (int s = 0; s < t; s++)
            {
                double loss = 0;
                for (int i = 0; i < n; i++) loss -= weights[i] * returns[s, i];

                if (loss - threshold <= 0) continue;

                exceed++;
                for (int i = 0; i < n; i++) gradient[i] -= returns[s, i] / (alpha * t);
            }

            thresholdGradient -= exceed / (alpha * t);

            // Мягкое ограничение на минимальную доходность
            if (double.IsFinite(minimumReturn))
            {
                double portfolioReturn = LinearAlgebra.Dot(weights, means);
                if (portfolioReturn < minimumReturn)
                    for (int i = 0; i < n; i++) gradient[i] -= 10 * means[i];
            }

            double rate = step / (1 + (iteration / 500.0));
            threshold -= rate * thresholdGradient;

            for (int i = 0; i < n; i++) weights[i] -= rate * gradient[i];

            ProjectToSimplex(weights, maximumWeight);
        }

        return weights;
    }

    /// <summary>Оценивает риск портфеля по сценариям.</summary>
    private static (double ValueAtRisk, double ConditionalValueAtRisk) Evaluate(
        double[,] returns, double[] weights, double confidence, out int tailCount)
    {
        int t = returns.GetLength(0), n = returns.GetLength(1);
        var losses = new double[t];

        for (int s = 0; s < t; s++)
        {
            double loss = 0;
            for (int i = 0; i < n; i++) loss -= weights[i] * returns[s, i];
            losses[s] = loss;
        }

        Array.Sort(losses);

        double var = EconMath.Quantile(losses, confidence);
        var tail = losses.Where(l => l >= var).ToList();

        tailCount = tail.Count;
        return (var, tail.Count > 0 ? tail.Average() : var);
    }

    /// <summary>Портфель минимальной дисперсии с ограничением на вес.</summary>
    private static double[] MinimumVariance(double[,] covariance, double maximumWeight)
    {
        int n = covariance.GetLength(0);
        var weights = new double[n];
        for (int i = 0; i < n; i++) weights[i] = 1.0 / n;

        double scale = 0;
        for (int i = 0; i < n; i++) scale += Math.Abs(covariance[i, i]);
        double step = scale > 0 ? 1.0 / (4 * scale) : 1e-3;

        for (int iteration = 0; iteration < 3000; iteration++)
        {
            double[] gradient = LinearAlgebra.Multiply(covariance, weights);
            for (int i = 0; i < n; i++) weights[i] -= step * 2 * gradient[i];

            ProjectToSimplex(weights, maximumWeight);
        }

        return weights;
    }

    /// <summary>Проекция весов на симплекс с верхней границей.</summary>
    private static void ProjectToSimplex(double[] weights, double maximumWeight)
    {
        int n = weights.Length;

        for (int pass = 0; pass < 60; pass++)
        {
            for (int i = 0; i < n; i++) weights[i] = Math.Clamp(weights[i], 0, maximumWeight);

            double sum = weights.Sum();
            double excess = sum - 1;

            if (Math.Abs(excess) < 1e-12) return;

            var adjustable = Enumerable.Range(0, n)
                .Where(i => excess > 0 ? weights[i] > 1e-12 : weights[i] < maximumWeight - 1e-12)
                .ToList();

            if (adjustable.Count == 0) return;

            double share = excess / adjustable.Count;
            foreach (int i in adjustable) weights[i] -= share;
        }
    }

    /// <summary>Преобразует массив в вектор фреймворка.</summary>
    private static Vector ToVector(double[] values)
    {
        var vector = new Vector(values.Length);
        for (int i = 0; i < values.Length; i++) vector[i] = values[i];
        return vector;
    }
}

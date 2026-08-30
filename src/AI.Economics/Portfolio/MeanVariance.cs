using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Portfolio;

/// <summary>Ограничения портфельной оптимизации.</summary>
public sealed record PortfolioConstraints
{
    /// <summary>Минимальный вес актива.</summary>
    public double MinimumWeight { get; init; }

    /// <summary>Максимальный вес актива.</summary>
    public double MaximumWeight { get; init; } = 1;

    /// <summary>Максимальное число активов в портфеле; при нуле ограничения нет.</summary>
    public int MaximumAssets { get; init; }

    /// <summary>Размер лота в долях портфеля; при нуле веса непрерывны.</summary>
    public double LotSize { get; init; }

    /// <summary>Издержки сделки в долях от оборота.</summary>
    public double TransactionCost { get; init; }

    /// <summary>Текущие веса для расчёта издержек перебалансировки.</summary>
    public IReadOnlyList<double>? CurrentWeights { get; init; }
}

/// <summary>Точка эффективной границы.</summary>
/// <param name="Return">Ожидаемая доходность.</param>
/// <param name="Risk">Стандартное отклонение.</param>
/// <param name="Sharpe">Коэффициент Шарпа.</param>
/// <param name="Weights">Веса активов.</param>
public sealed record FrontierPoint(double Return, double Risk, double Sharpe, Vector Weights);

/// <summary>Результат портфельной оптимизации.</summary>
public sealed record OptimizationResult : IInterpretable
{
    /// <summary>Названия активов.</summary>
    public IReadOnlyList<string> Assets { get; init; } = [];

    /// <summary>Оптимальные веса.</summary>
    public Vector Weights { get; init; } = new(0);

    /// <summary>Ожидаемая доходность портфеля.</summary>
    public double ExpectedReturn { get; init; }

    /// <summary>Риск портфеля.</summary>
    public double Risk { get; init; }

    /// <summary>Коэффициент Шарпа портфеля.</summary>
    public double Sharpe { get; init; }

    /// <summary>Эффективная граница.</summary>
    public IReadOnlyList<FrontierPoint> Frontier { get; init; } = [];

    /// <summary>Портфель минимального риска.</summary>
    public FrontierPoint? MinimumVariance { get; init; }

    /// <summary>Портфель максимального коэффициента Шарпа.</summary>
    public FrontierPoint? MaximumSharpe { get; init; }

    /// <summary>Вклад активов в риск портфеля.</summary>
    public IReadOnlyList<(string Asset, double Weight, double RiskContribution)> RiskBudget { get; init; } = [];

    /// <summary>Издержки перехода к оптимальным весам.</summary>
    public double TransactionCost { get; init; }

    /// <summary>Эффективное число активов по индексу Херфиндаля.</summary>
    public double EffectiveAssets =>
        Weights.Count > 0 && Weights.Sum(w => w * w) > 0 ? 1 / Weights.Sum(w => w * w) : 0;

    /// <summary>Безрисковая ставка.</summary>
    public double RiskFreeRate { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (string Asset, double Weight, double RiskContribution) dominant =
            RiskBudget.OrderByDescending(r => r.RiskContribution).FirstOrDefault();

        bool concentrated = EffectiveAssets < Assets.Count / 3.0;
        double diversificationRatio = Assets.Count > 0 ? EffectiveAssets / Assets.Count : 0;

        var builder = new InterpretationBuilder("Портфельная оптимизация")
            .Summary($"Портфель из {Assets.Count} активов: ожидаемая доходность " +
                     $"{Fmt.Pct(ExpectedReturn, 2)} при риске {Fmt.Pct(Risk, 2)}, " +
                     $"коэффициент Шарпа {Fmt.Num(Sharpe, 3)}. Эффективное число активов " +
                     $"{Fmt.Num(EffectiveAssets, 1)} из {Assets.Count}. " +
                     (TransactionCost > 0
                         ? $"Издержки перехода {Fmt.Pct(TransactionCost, 3)} портфеля."
                         : ""))
            .Metric("Ожидаемая доходность", ExpectedReturn, null, "в годовом выражении",
                MetricQuality.Neutral, 4)
            .Metric("Риск", Risk, null, "стандартное отклонение доходности",
                MetricQuality.Neutral, 4)
            .Metric("Шарп", Sharpe, null, "избыточная доходность на единицу риска",
                Sharpe > 1 ? MetricQuality.Good : MetricQuality.Neutral, 3)
            .Metric("Эффективное число активов", EffectiveAssets, null,
                concentrated ? "портфель сконцентрирован" : "диверсификация приемлема",
                concentrated ? MetricQuality.Warning : MetricQuality.Good, 2)
            .Metric("Уровень диверсификации", diversificationRatio, null,
                "эффективное число активов к общему", MetricQuality.Neutral, 3);

        if (MinimumVariance is not null)
        {
            builder.Metric("Портфель минимального риска", MinimumVariance.Risk, null,
                $"доходность {Fmt.Pct(MinimumVariance.Return, 2)}", MetricQuality.Neutral, 4);
        }

        if (TransactionCost > 0)
        {
            builder.Metric("Издержки перехода", TransactionCost, null,
                "стоимость перебалансировки к целевым весам",
                TransactionCost > 0.01 ? MetricQuality.Warning : MetricQuality.Neutral, 4);
        }

        foreach ((string asset, double weight, double contribution) in RiskBudget)
        {
            builder.Metric(asset, weight, null,
                $"вклад в риск {Fmt.Pct(contribution, 1)}",
                Math.Abs(contribution - weight) > 0.15 ? MetricQuality.Warning : MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Эффективная граница показывает, что каждая дополнительная единица " +
                     "доходности покупается всё большим приростом риска. Выбор точки " +
                     "на границе — вопрос предпочтений, а не оптимизации.")
            .FindingIf(dominant.Asset is not null,
                $"Наибольший вклад в риск даёт «{dominant.Asset}»: вес " +
                $"{Fmt.Pct(dominant.Weight, 1)} при вкладе в риск " +
                $"{Fmt.Pct(dominant.RiskContribution, 1)}. Вес и вклад в риск — " +
                "разные вещи, и управлять нужно вторым.")
            .FindingIf(concentrated,
                $"Эффективное число активов {Fmt.Num(EffectiveAssets, 1)} против " +
                $"{Assets.Count} в наборе. Оптимизация по средней и дисперсии " +
                "склонна концентрировать портфель: она принимает оценки доходностей " +
                "за точные и ставит всё на лучшую из них.")
            .WarningIf(concentrated,
                "Концентрация — известная патология метода. Ошибки оценки ожидаемых " +
                "доходностей увеличиваются оптимизацией, а не сглаживаются ею: " +
                "актив с завышенной оценкой получает завышенный вес.")
            .WarningIf(TransactionCost > 0.01,
                $"Издержки перехода {Fmt.Pct(TransactionCost, 3)} сопоставимы с ожидаемым " +
                "выигрышем от оптимизации. Проверьте, окупает ли перебалансировка себя.")
            .Warning("Ожидаемые доходности оцениваются по истории и потому крайне " +
                     "неточны: стандартная ошибка средней доходности за десять лет " +
                     "сопоставима с самой доходностью. Ковариации оцениваются заметно " +
                     "надёжнее, поэтому портфель минимального риска устойчивее " +
                     "портфеля максимального Шарпа.")
            .Recommendation("Ограничивайте максимальный вес актива: это простейший " +
                            "и самый действенный способ борьбы с концентрацией, " +
                            "вызванной ошибками оценки.")
            .Recommendation("Сравнивайте результат с равновзвешенным портфелем. " +
                            "На практике он часто оказывается не хуже вне выборки, " +
                            "и это честный ориентир для оценки пользы оптимизации.")
            .Build();
    }
}

/// <summary>
/// Портфельная оптимизация по средней и дисперсии.
/// </summary>
/// <remarks>
/// <para>
/// Задача Марковица минимизирует дисперсию при заданной доходности:
/// </para>
/// <code>
/// min w' Sigma w  при  w' mu = target,  sum w = 1,  L &lt;= w &lt;= U
/// </code>
/// <para>
/// Без ограничений на веса задача имеет аналитическое решение через обратную
/// ковариационную матрицу. С ограничениями требуется численный метод: здесь
/// используется проекционный градиентный спуск на допустимое множество.
/// </para>
/// <para>
/// Главная практическая проблема метода — не вычислительная, а статистическая.
/// Ожидаемые доходности оцениваются по истории с огромной ошибкой, и
/// оптимизация эту ошибку усиливает: актив со случайно завышенной оценкой
/// получает завышенный вес. Отсюда две стандартные меры — ограничение
/// максимального веса и опора на портфель минимального риска, для которого
/// ожидаемые доходности вообще не нужны.
/// </para>
/// <para>
/// Вклад актива в риск портфеля отличается от его веса и считается отдельно:
/// </para>
/// <code>
/// RC_i = w_i * (Sigma w)_i / (w' Sigma w)
/// </code>
/// </remarks>
public static class MeanVariance
{
    /// <summary>Строит эффективную границу и выбирает оптимальный портфель.</summary>
    /// <param name="expectedReturns">Ожидаемые доходности активов.</param>
    /// <param name="covariance">Ковариационная матрица доходностей.</param>
    /// <param name="assets">Названия активов.</param>
    /// <param name="riskFreeRate">Безрисковая ставка.</param>
    /// <param name="constraints">Ограничения; при <c>null</c> берутся только неотрицательность и полнота.</param>
    /// <param name="frontierPoints">Число точек границы.</param>
    /// <returns>Оптимальные веса, граница и разложение риска.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static OptimizationResult Optimize(
        Vector expectedReturns, Matrix covariance, IReadOnlyList<string>? assets = null,
        double riskFreeRate = 0, PortfolioConstraints? constraints = null, int frontierPoints = 25)
    {
        ArgumentNullException.ThrowIfNull(expectedReturns);
        ArgumentNullException.ThrowIfNull(covariance);

        int n = expectedReturns.Count;
        if (covariance.Height != n || covariance.Width != n)
            throw new ArgumentException("Ковариационная матрица должна быть квадратной по числу активов.",
                nameof(covariance));
        if (n < 2) throw new ArgumentException("Нужно минимум два актива.", nameof(expectedReturns));

        constraints ??= new PortfolioConstraints();

        var names = new List<string>(n);
        for (int i = 0; i < n; i++)
            names.Add(assets is not null && i < assets.Count ? assets[i] : $"актив {i + 1}");

        double[,] sigma = ToArray(covariance);
        double[] mu = [.. expectedReturns];

        double minReturn = mu.Min(), maxReturn = mu.Max();
        var frontier = new List<FrontierPoint>(frontierPoints);

        for (int i = 0; i < frontierPoints; i++)
        {
            double target = minReturn + (i * (maxReturn - minReturn) / Math.Max(1, frontierPoints - 1));
            double[] weights = Solve(mu, sigma, target, constraints, targetReturn: true);

            double portfolioReturn = LinearAlgebra.Dot(weights, mu);
            double risk = Math.Sqrt(Math.Max(LinearAlgebra.QuadraticForm(weights, sigma), 0));

            frontier.Add(new FrontierPoint(
                portfolioReturn, risk,
                risk > 0 ? (portfolioReturn - riskFreeRate) / risk : 0,
                LinearRegressionVector(weights)));
        }

        double[] minimumVariance = Solve(mu, sigma, 0, constraints, targetReturn: false);
        double minimumRisk = Math.Sqrt(Math.Max(LinearAlgebra.QuadraticForm(minimumVariance, sigma), 0));
        double minimumReturn = LinearAlgebra.Dot(minimumVariance, mu);

        FrontierPoint minimum = new(
            minimumReturn, minimumRisk,
            minimumRisk > 0 ? (minimumReturn - riskFreeRate) / minimumRisk : 0,
            LinearRegressionVector(minimumVariance));

        FrontierPoint best = frontier.OrderByDescending(p => p.Sharpe).First();
        double[] chosen = [.. best.Weights];

        double cost = 0;
        if (constraints.TransactionCost > 0 && constraints.CurrentWeights is not null)
        {
            for (int i = 0; i < n && i < constraints.CurrentWeights.Count; i++)
                cost += Math.Abs(chosen[i] - constraints.CurrentWeights[i]) * constraints.TransactionCost;
        }

        return new OptimizationResult
        {
            Assets = names,
            Weights = best.Weights,
            ExpectedReturn = best.Return,
            Risk = best.Risk,
            Sharpe = best.Sharpe,
            Frontier = frontier,
            MinimumVariance = minimum,
            MaximumSharpe = best,
            RiskBudget = RiskContributions(chosen, sigma, names),
            TransactionCost = cost,
            RiskFreeRate = riskFreeRate,
        };
    }

    /// <summary>Вклады активов в риск портфеля.</summary>
    /// <param name="weights">Веса активов.</param>
    /// <param name="covariance">Ковариационная матрица.</param>
    /// <param name="assets">Названия активов.</param>
    /// <returns>Вес и доля в риске по каждому активу.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static IReadOnlyList<(string Asset, double Weight, double RiskContribution)> RiskBudget(
        Vector weights, Matrix covariance, IReadOnlyList<string>? assets = null)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(covariance);

        var names = new List<string>(weights.Count);
        for (int i = 0; i < weights.Count; i++)
            names.Add(assets is not null && i < assets.Count ? assets[i] : $"актив {i + 1}");

        return RiskContributions([.. weights], ToArray(covariance), names);
    }

    /// <summary>Ковариационная матрица по историческим доходностям.</summary>
    /// <param name="returns">Доходности: строка — период, столбец — актив.</param>
    /// <param name="shrinkage">Доля сжатия к диагональной матрице.</param>
    /// <returns>Ковариационная матрица.</returns>
    /// <exception cref="ArgumentNullException">Доходности не заданы.</exception>
    public static Matrix Covariance(Matrix returns, double shrinkage = 0)
    {
        ArgumentNullException.ThrowIfNull(returns);

        double[,] sample = LinearAlgebra.Covariance(ToArray(returns));
        int k = sample.GetLength(0);

        if (shrinkage > 0)
        {
            // Сжатие к диагонали снижает ошибку оценки и делает матрицу устойчивее
            double averageVariance = 0;
            for (int i = 0; i < k; i++) averageVariance += sample[i, i];
            averageVariance /= k;

            double lambda = Math.Clamp(shrinkage, 0, 1);

            for (int i = 0; i < k; i++)
                for (int j = 0; j < k; j++)
                {
                    double target = i == j ? averageVariance : 0;
                    sample[i, j] = ((1 - lambda) * sample[i, j]) + (lambda * target);
                }
        }

        var matrix = new Matrix(k, k);
        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++) matrix[i, j] = sample[i, j];

        return matrix;
    }

    /// <summary>Решает задачу оптимизации проекционным градиентным спуском.</summary>
    private static double[] Solve(
        double[] mu, double[,] sigma, double target,
        PortfolioConstraints constraints, bool targetReturn)
    {
        int n = mu.Length;
        var weights = new double[n];
        for (int i = 0; i < n; i++) weights[i] = 1.0 / n;

        double scale = 0;
        for (int i = 0; i < n; i++) scale += Math.Abs(sigma[i, i]);
        double step = scale > 0 ? 1.0 / (4 * scale) : 1e-3;

        // Штраф за отклонение от целевой доходности растёт по ходу спуска
        for (int iteration = 0; iteration < 4000; iteration++)
        {
            double penalty = targetReturn ? 50.0 * (1 + (iteration / 500.0)) : 0;
            var gradient = new double[n];

            double[] sigmaW = LinearAlgebra.Multiply(sigma, weights);
            double currentReturn = LinearAlgebra.Dot(weights, mu);

            for (int i = 0; i < n; i++)
            {
                gradient[i] = 2 * sigmaW[i];
                if (targetReturn) gradient[i] += 2 * penalty * (currentReturn - target) * mu[i];
            }

            double shift = 0;
            for (int i = 0; i < n; i++)
            {
                double updated = weights[i] - (step * gradient[i]);
                shift += Math.Abs(updated - weights[i]);
                weights[i] = updated;
            }

            Project(weights, constraints);
            if (shift < 1e-14) break;
        }

        if (constraints.LotSize > 0) RoundToLots(weights, constraints.LotSize);
        if (constraints.MaximumAssets > 0) LimitAssets(weights, constraints);

        return weights;
    }

    /// <summary>Проекция весов на допустимое множество.</summary>
    private static void Project(double[] weights, PortfolioConstraints constraints)
    {
        int n = weights.Length;

        for (int pass = 0; pass < 50; pass++)
        {
            for (int i = 0; i < n; i++)
                weights[i] = Math.Clamp(weights[i], constraints.MinimumWeight, constraints.MaximumWeight);

            double sum = weights.Sum();
            double excess = sum - 1;

            if (Math.Abs(excess) < 1e-12) return;

            // Излишек распределяется только между весами, которые могут двигаться
            var adjustable = Enumerable.Range(0, n)
                .Where(i => excess > 0
                    ? weights[i] > constraints.MinimumWeight + 1e-12
                    : weights[i] < constraints.MaximumWeight - 1e-12)
                .ToList();

            if (adjustable.Count == 0) return;

            double share = excess / adjustable.Count;
            foreach (int i in adjustable) weights[i] -= share;
        }
    }

    /// <summary>Округление весов до размера лота с сохранением полноты.</summary>
    private static void RoundToLots(double[] weights, double lot)
    {
        int n = weights.Length;
        double allocated = 0;

        for (int i = 0; i < n; i++)
        {
            weights[i] = Math.Round(weights[i] / lot) * lot;
            allocated += weights[i];
        }

        int largest = 0;
        for (int i = 1; i < n; i++) if (weights[i] > weights[largest]) largest = i;

        weights[largest] += 1 - allocated;
    }

    /// <summary>Оставляет заданное число активов с наибольшими весами.</summary>
    private static void LimitAssets(double[] weights, PortfolioConstraints constraints)
    {
        int n = weights.Length;
        if (constraints.MaximumAssets >= n) return;

        var keep = Enumerable.Range(0, n)
            .OrderByDescending(i => weights[i])
            .Take(constraints.MaximumAssets)
            .ToHashSet();

        double removed = 0;
        for (int i = 0; i < n; i++)
        {
            if (keep.Contains(i)) continue;

            removed += weights[i];
            weights[i] = 0;
        }

        double remaining = weights.Sum();
        if (remaining <= 0) return;

        for (int i = 0; i < n; i++)
            if (keep.Contains(i)) weights[i] += removed * weights[i] / remaining;
    }

    /// <summary>Вклады активов в общий риск.</summary>
    private static IReadOnlyList<(string, double, double)> RiskContributions(
        double[] weights, double[,] sigma, IReadOnlyList<string> names)
    {
        double variance = LinearAlgebra.QuadraticForm(weights, sigma);
        double[] marginal = LinearAlgebra.Multiply(sigma, weights);

        var contributions = new List<(string, double, double)>(weights.Length);

        for (int i = 0; i < weights.Length; i++)
        {
            double contribution = variance > 1e-18 ? weights[i] * marginal[i] / variance : 0;
            contributions.Add((names[i], weights[i], contribution));
        }

        return contributions;
    }

    /// <summary>Преобразует матрицу фреймворка в массив.</summary>
    private static double[,] ToArray(Matrix matrix)
    {
        var array = new double[matrix.Height, matrix.Width];
        for (int i = 0; i < matrix.Height; i++)
            for (int j = 0; j < matrix.Width; j++) array[i, j] = matrix[i, j];

        return array;
    }

    /// <summary>Преобразует массив в вектор фреймворка.</summary>
    private static Vector LinearRegressionVector(double[] values)
    {
        var vector = new Vector(values.Length);
        for (int i = 0; i < values.Length; i++) vector[i] = values[i];
        return vector;
    }
}

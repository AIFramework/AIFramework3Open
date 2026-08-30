using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Portfolio;

/// <summary>Взгляд инвестора на доходность активов.</summary>
/// <param name="Description">Словесная формулировка взгляда.</param>
/// <param name="Weights">Коэффициенты активов: для относительного взгляда сумма равна нулю.</param>
/// <param name="ExpectedReturn">Ожидаемая доходность по взгляду.</param>
/// <param name="Confidence">Уверенность во взгляде от нуля до единицы.</param>
public sealed record InvestorView(
    string Description, Vector Weights, double ExpectedReturn, double Confidence);

/// <summary>Результат смешивания равновесных доходностей со взглядами инвестора.</summary>
public sealed record BlackLittermanResult : IInterpretable
{
    /// <summary>Названия активов.</summary>
    public IReadOnlyList<string> Assets { get; init; } = [];

    /// <summary>Равновесные доходности, выведенные из рыночных весов.</summary>
    public Vector ImpliedReturns { get; init; } = new(0);

    /// <summary>Апостериорные доходности после учёта взглядов.</summary>
    public Vector PosteriorReturns { get; init; } = new(0);

    /// <summary>Рыночные веса.</summary>
    public Vector MarketWeights { get; init; } = new(0);

    /// <summary>Оптимальные веса при апостериорных доходностях.</summary>
    public Vector OptimalWeights { get; init; } = new(0);

    /// <summary>Отклонения оптимальных весов от рыночных.</summary>
    public Vector ActiveWeights { get; init; } = new(0);

    /// <summary>Учтённые взгляды.</summary>
    public IReadOnlyList<InvestorView> Views { get; init; } = [];

    /// <summary>Коэффициент неприятия риска.</summary>
    public double RiskAversion { get; init; }

    /// <summary>Вес априорного распределения.</summary>
    public double Tau { get; init; }

    /// <summary>Суммарное отклонение от рыночного портфеля.</summary>
    public double ActiveShare => ActiveWeights.Sum(Math.Abs) / 2;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var shifts = new List<(string Asset, double Shift)>();
        for (int i = 0; i < Assets.Count && i < ActiveWeights.Count; i++)
            shifts.Add((Assets[i], ActiveWeights[i]));

        (string Asset, double Shift) largest = shifts.OrderByDescending(s => Math.Abs(s.Shift)).FirstOrDefault();
        bool aggressive = ActiveShare > 0.3;

        var builder = new InterpretationBuilder("Модель Блэка — Литтермана")
            .Summary($"Учтено {Views.Count} взглядов на {Assets.Count} активов. Активная доля " +
                     $"портфеля {Fmt.Pct(ActiveShare, 1)} — настолько итоговые веса отличаются " +
                     $"от рыночных. Наибольший сдвиг у «{largest.Asset}»: " +
                     $"{Fmt.Pct(largest.Shift, 2)}. Коэффициент неприятия риска " +
                     $"{Fmt.Num(RiskAversion, 2)}, вес априорного распределения {Fmt.Num(Tau, 3)}.")
            .Metric("Активная доля", ActiveShare, null,
                aggressive ? "портфель заметно отличается от рыночного" : "умеренное отклонение от рынка",
                aggressive ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Взглядов", Views.Count, null, $"активов {Assets.Count}",
                MetricQuality.Neutral, 0)
            .Metric("Неприятие риска", RiskAversion, null,
                "чем выше, тем ближе портфель к рыночному", MetricQuality.Neutral, 2);

        for (int i = 0; i < Assets.Count && i < PosteriorReturns.Count; i++)
        {
            double shift = PosteriorReturns[i] - ImpliedReturns[i];

            builder.Metric(Assets[i], PosteriorReturns[i], null,
                $"равновесная {Fmt.Pct(ImpliedReturns[i], 2)}, сдвиг {Fmt.Pct(shift, 2)}; " +
                $"вес {Fmt.Pct(OptimalWeights[i], 1)} против рыночного {Fmt.Pct(MarketWeights[i], 1)}",
                Math.Abs(shift) > 0.01 ? MetricQuality.Warning : MetricQuality.Unknown, 4);
        }

        foreach (InvestorView view in Views)
        {
            builder.Metric($"Взгляд: {view.Description}", view.ExpectedReturn, null,
                $"уверенность {Fmt.Pct(view.Confidence, 0)}", MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Модель решает главную проблему оптимизации по средней и дисперсии: " +
                     "вместо оценки доходностей по истории она берёт за отправную точку " +
                     "равновесие, выведенное из рыночных весов. Взгляды инвестора лишь " +
                     "смещают эту точку, а не заменяют её.")
            .FindingIf(Views.Count == 0,
                "Взглядов нет, и апостериорные доходности совпадают с равновесными. " +
                "Оптимальный портфель в этом случае равен рыночному — это и есть " +
                "проверка корректности реализации.")
            .FindingIf(largest.Asset is not null && Math.Abs(largest.Shift) > 0.05,
                $"Сдвиг веса «{largest.Asset}» на {Fmt.Pct(largest.Shift, 2)} — прямое " +
                "следствие сформулированных взглядов. Модель делает эту связь явной: " +
                "каждое отклонение от рынка можно объяснить конкретным утверждением.")
            .FindingIf(!aggressive,
                "Отклонение от рыночного портфеля умеренное: взгляды учтены, но не " +
                "доминируют. Это обычно признак разумно заданной уверенности.")
            .WarningIf(aggressive,
                $"Активная доля {Fmt.Pct(ActiveShare, 1)} велика. Проверьте заданную " +
                "уверенность во взглядах: высокая уверенность фактически отменяет " +
                "равновесие и возвращает все проблемы прямой оптимизации.")
            .WarningIf(Views.Any(v => v.Confidence > 0.9),
                "Есть взгляды с уверенностью выше 0,9. Такая уверенность означает " +
                "почти точное знание будущей доходности и в портфеле проявится " +
                "экстремальными весами.")
            .Warning("Равновесные доходности выводятся из рыночных весов и ковариаций. " +
                     "Если рыночные веса заданы неверно — например, взяты равными, — " +
                     "модель теряет своё главное преимущество перед прямой оптимизацией.")
            .Recommendation("Формулируйте относительные взгляды («A обгонит B на 2%») " +
                            "чаще абсолютных: они устойчивее и не требуют прогноза " +
                            "общего уровня доходности рынка.")
            .Recommendation("Задавайте уверенность осознанно: она напрямую определяет, " +
                            "насколько итоговый портфель отойдёт от рыночного.")
            .Build();
    }
}

/// <summary>
/// Модель Блэка — Литтермана: смешивание рыночного равновесия со взглядами
/// инвестора.
/// </summary>
/// <remarks>
/// <para>
/// Прямая оптимизация по историческим доходностям даёт неустойчивые и
/// экстремальные веса. Блэк и Литтерман предложили идти от обратного: принять
/// рыночный портфель за оптимальный и вывести из него доходности, которые
/// сделали бы его таковым:
/// </para>
/// <code>
/// Pi = lambda * Sigma * w_market
/// </code>
/// <para>
/// Эти равновесные доходности служат априорным распределением. Взгляды
/// инвестора задаются линейными ограничениями и смешиваются с априорным по
/// формуле байесовского обновления:
/// </para>
/// <code>
/// mu = [ (tau Sigma)^-1 + P' Omega^-1 P ]^-1 [ (tau Sigma)^-1 Pi + P' Omega^-1 Q ]
/// </code>
/// <para>
/// Ключевое свойство: при отсутствии взглядов апостериорные доходности равны
/// равновесным, и оптимальный портфель совпадает с рыночным. Каждое отклонение
/// от рынка становится объяснимым — оно следует из конкретного утверждения
/// инвестора, а не из шума в исторических данных.
/// </para>
/// </remarks>
public static class BlackLitterman
{
    /// <summary>Смешивает рыночное равновесие со взглядами инвестора.</summary>
    /// <param name="marketWeights">Рыночные веса активов.</param>
    /// <param name="covariance">Ковариационная матрица доходностей.</param>
    /// <param name="views">Взгляды инвестора.</param>
    /// <param name="assets">Названия активов.</param>
    /// <param name="riskAversion">Коэффициент неприятия риска.</param>
    /// <param name="tau">Вес априорного распределения.</param>
    /// <returns>Равновесные и апостериорные доходности с оптимальными весами.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static BlackLittermanResult Blend(
        Vector marketWeights, Matrix covariance, IReadOnlyList<InvestorView>? views = null,
        IReadOnlyList<string>? assets = null, double riskAversion = 2.5, double tau = 0.05)
    {
        ArgumentNullException.ThrowIfNull(marketWeights);
        ArgumentNullException.ThrowIfNull(covariance);

        int n = marketWeights.Count;
        if (covariance.Height != n || covariance.Width != n)
            throw new ArgumentException("Ковариационная матрица должна соответствовать числу активов.",
                nameof(covariance));

        var names = new List<string>(n);
        for (int i = 0; i < n; i++)
            names.Add(assets is not null && i < assets.Count ? assets[i] : $"актив {i + 1}");

        var sigma = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) sigma[i, j] = covariance[i, j];

        double[] weights = [.. marketWeights];

        // Равновесные доходности: обратная оптимизация из рыночных весов
        double[] implied = LinearAlgebra.Multiply(sigma, weights);
        for (int i = 0; i < n; i++) implied[i] *= riskAversion;

        double[] posterior;
        IReadOnlyList<InvestorView> list = views ?? [];

        if (list.Count == 0)
        {
            posterior = (double[])implied.Clone();
        }
        else
        {
            int k = list.Count;
            var p = new double[k, n];
            var q = new double[k];
            var omega = new double[k, k];

            for (int v = 0; v < k; v++)
            {
                for (int i = 0; i < n && i < list[v].Weights.Count; i++) p[v, i] = list[v].Weights[i];
                q[v] = list[v].ExpectedReturn;

                // Неопределённость взгляда обратна заявленной уверенности
                var row = new double[n];
                for (int i = 0; i < n; i++) row[i] = p[v, i];

                double viewVariance = tau * LinearAlgebra.QuadraticForm(row, sigma);
                double confidence = Math.Clamp(list[v].Confidence, 0.01, 0.99);

                omega[v, v] = Math.Max(viewVariance * (1 - confidence) / confidence, 1e-12);
            }

            var priorPrecision = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) priorPrecision[i, j] = sigma[i, j] * tau;

            double[,] priorInverse = EconMath.Inverse(priorPrecision)
                ?? throw new ArgumentException("Ковариационная матрица вырождена.", nameof(covariance));

            double[,] omegaInverse = EconMath.Inverse(omega)
                ?? throw new ArgumentException("Матрица неопределённости взглядов вырождена.", nameof(views));

            double[,] pt = LinearAlgebra.Transpose(p);
            double[,] middle = LinearAlgebra.Multiply(LinearAlgebra.Multiply(pt, omegaInverse), p);

            var precision = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) precision[i, j] = priorInverse[i, j] + middle[i, j];

            double[,] posteriorCovariance = EconMath.Inverse(precision)
                ?? throw new ArgumentException("Апостериорная матрица вырождена.", nameof(views));

            double[] priorTerm = LinearAlgebra.Multiply(priorInverse, implied);
            double[] viewTerm = LinearAlgebra.Multiply(
                LinearAlgebra.Multiply(pt, omegaInverse), q);

            var combined = new double[n];
            for (int i = 0; i < n; i++) combined[i] = priorTerm[i] + viewTerm[i];

            posterior = LinearAlgebra.Multiply(posteriorCovariance, combined);
        }

        // Оптимальные веса при апостериорных доходностях
        double[,] sigmaInverse = EconMath.Inverse(sigma)
            ?? throw new ArgumentException("Ковариационная матрица вырождена.", nameof(covariance));

        double[] optimal = LinearAlgebra.Multiply(sigmaInverse, posterior);
        for (int i = 0; i < n; i++) optimal[i] /= riskAversion;

        double sum = optimal.Sum();
        if (Math.Abs(sum) > 1e-12) for (int i = 0; i < n; i++) optimal[i] /= sum;

        var active = new Vector(n);
        for (int i = 0; i < n; i++) active[i] = optimal[i] - weights[i];

        return new BlackLittermanResult
        {
            Assets = names,
            ImpliedReturns = ToVector(implied),
            PosteriorReturns = ToVector(posterior),
            MarketWeights = marketWeights,
            OptimalWeights = ToVector(optimal),
            ActiveWeights = active,
            Views = list,
            RiskAversion = riskAversion,
            Tau = tau,
        };
    }

    /// <summary>Строит относительный взгляд: один актив обгонит другой.</summary>
    /// <param name="assetCount">Общее число активов.</param>
    /// <param name="outperformer">Индекс актива, который обгонит.</param>
    /// <param name="underperformer">Индекс актива, который отстанет.</param>
    /// <param name="excessReturn">Ожидаемое превышение доходности.</param>
    /// <param name="confidence">Уверенность во взгляде.</param>
    /// <param name="description">Словесная формулировка.</param>
    /// <returns>Взгляд для подстановки в модель.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Индексы вне диапазона.</exception>
    public static InvestorView Relative(
        int assetCount, int outperformer, int underperformer,
        double excessReturn, double confidence, string description = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outperformer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(outperformer, assetCount);
        ArgumentOutOfRangeException.ThrowIfNegative(underperformer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(underperformer, assetCount);

        var weights = new Vector(assetCount);
        weights[outperformer] = 1;
        weights[underperformer] = -1;

        return new InvestorView(
            string.IsNullOrEmpty(description)
                ? $"актив {outperformer + 1} обгонит актив {underperformer + 1}"
                : description,
            weights, excessReturn, confidence);
    }

    /// <summary>Строит абсолютный взгляд на доходность актива.</summary>
    /// <param name="assetCount">Общее число активов.</param>
    /// <param name="asset">Индекс актива.</param>
    /// <param name="expectedReturn">Ожидаемая доходность.</param>
    /// <param name="confidence">Уверенность во взгляде.</param>
    /// <param name="description">Словесная формулировка.</param>
    /// <returns>Взгляд для подстановки в модель.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Индекс вне диапазона.</exception>
    public static InvestorView Absolute(
        int assetCount, int asset, double expectedReturn, double confidence, string description = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(asset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(asset, assetCount);

        var weights = new Vector(assetCount);
        weights[asset] = 1;

        return new InvestorView(
            string.IsNullOrEmpty(description) ? $"доходность актива {asset + 1}" : description,
            weights, expectedReturn, confidence);
    }

    /// <summary>Преобразует массив в вектор фреймворка.</summary>
    private static Vector ToVector(double[] values)
    {
        var vector = new Vector(values.Length);
        for (int i = 0; i < values.Length; i++) vector[i] = values[i];
        return vector;
    }
}

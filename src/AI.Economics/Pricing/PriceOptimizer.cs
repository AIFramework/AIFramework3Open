using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Pricing;

/// <summary>Товар линейки с текущей ценой, спросом и себестоимостью.</summary>
public sealed record ProductPricing
{
    /// <summary>Название товара.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Текущая цена.</summary>
    public double CurrentPrice { get; init; }

    /// <summary>Текущий объём продаж за период.</summary>
    public double CurrentQuantity { get; init; }

    /// <summary>Переменные издержки на единицу.</summary>
    public double UnitCost { get; init; }

    /// <summary>Текущая валовая маржа как доля цены.</summary>
    public double CurrentMargin => CurrentPrice > 0 ? (CurrentPrice - UnitCost) / CurrentPrice : 0;
}

/// <summary>Ограничения задачи оптимизации цены.</summary>
public sealed record PriceConstraints
{
    /// <summary>Максимальное относительное изменение цены в обе стороны.</summary>
    public double MaxPriceChange { get; init; } = 0.2;

    /// <summary>Минимально допустимая валовая маржа каждого товара.</summary>
    public double MinMarginRate { get; init; }

    /// <summary>Минимальный суммарный объём продаж — прокси доли рынка.</summary>
    public double MinTotalVolume { get; init; }

    /// <summary>Минимальная суммарная выручка.</summary>
    public double MinTotalRevenue { get; init; }
}

/// <summary>Рекомендация по цене одного товара.</summary>
public sealed record ProductPriceRecommendation
{
    /// <summary>Название товара.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Текущая цена.</summary>
    public double CurrentPrice { get; init; }

    /// <summary>Рекомендованная цена.</summary>
    public double OptimalPrice { get; init; }

    /// <summary>Относительное изменение цены.</summary>
    public double PriceChange => CurrentPrice > 0 ? (OptimalPrice - CurrentPrice) / CurrentPrice : 0;

    /// <summary>Текущий объём.</summary>
    public double CurrentQuantity { get; init; }

    /// <summary>Ожидаемый объём при новой цене.</summary>
    public double OptimalQuantity { get; init; }

    /// <summary>Текущая прибыль товара.</summary>
    public double CurrentProfit { get; init; }

    /// <summary>Ожидаемая прибыль при новой цене.</summary>
    public double OptimalProfit { get; init; }

    /// <summary>Валовая маржа при новой цене.</summary>
    public double NewMargin { get; init; }

    /// <summary>Упёрлась ли цена в границу допустимого изменения.</summary>
    public bool AtBound { get; init; }
}

/// <summary>Результат оптимизации цен по линейке.</summary>
public sealed record PriceOptimizationResult : IInterpretable
{
    /// <summary>Рекомендации по товарам, по убыванию прироста прибыли.</summary>
    public IReadOnlyList<ProductPriceRecommendation> Products { get; init; } = [];

    /// <summary>Текущая суммарная прибыль.</summary>
    public double CurrentProfit { get; init; }

    /// <summary>Прибыль при рекомендованных ценах.</summary>
    public double OptimalProfit { get; init; }

    /// <summary>Прирост прибыли в деньгах.</summary>
    public double ProfitGain => OptimalProfit - CurrentProfit;

    /// <summary>Относительный прирост прибыли.</summary>
    public double ProfitGainRate => CurrentProfit > 0 ? ProfitGain / CurrentProfit : 0;

    /// <summary>Текущая выручка.</summary>
    public double CurrentRevenue { get; init; }

    /// <summary>Выручка при рекомендованных ценах.</summary>
    public double OptimalRevenue { get; init; }

    /// <summary>Текущий суммарный объём.</summary>
    public double CurrentVolume { get; init; }

    /// <summary>Объём при рекомендованных ценах.</summary>
    public double OptimalVolume { get; init; }

    /// <summary>
    /// Во что обошёлся бы отказ от учёта каннибализации: разница между
    /// фактической прибылью «независимого» решения и оптимума.
    /// </summary>
    public double CannibalizationCost { get; init; }

    /// <summary>Прибыль, если оптимизировать каждый товар в отрыве от линейки.</summary>
    public double IndependentOptimumProfit { get; init; }

    /// <summary>Сработало ли хотя бы одно ограничение.</summary>
    public bool ConstraintsBinding => Products.Any(p => p.AtBound);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var up = Products.Where(p => p.PriceChange > 0.005).ToList();
        var down = Products.Where(p => p.PriceChange < -0.005).ToList();
        ProductPriceRecommendation? best = Products.OrderByDescending(p => p.OptimalProfit - p.CurrentProfit).FirstOrDefault();

        var builder = new InterpretationBuilder("Оптимизация цен по линейке")
            .Summary($"Пересмотр цен даёт {Fmt.Money(ProfitGain)} дополнительной прибыли " +
                     $"({Fmt.Pct(ProfitGainRate)} к текущей). Поднять цену стоит у {up.Count} " +
                     $"позиций, снизить — у {down.Count}, объём при этом меняется на " +
                     $"{Fmt.Pct(CurrentVolume > 0 ? (OptimalVolume - CurrentVolume) / CurrentVolume : 0)}.")
            .Metric("Прибыль сейчас", Fmt.Money(CurrentProfit), null, "при текущих ценах")
            .Metric("Прибыль после", Fmt.Money(OptimalProfit), null, "при рекомендованных ценах",
                MetricQuality.Good)
            .Metric("Прирост", Fmt.Money(ProfitGain), null, Fmt.Pct(ProfitGainRate) + " к текущей прибыли",
                ProfitGain > 0 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Изменение выручки",
                Fmt.Pct(CurrentRevenue > 0 ? (OptimalRevenue - CurrentRevenue) / CurrentRevenue : 0), null,
                "выручка и прибыль могут двигаться в разные стороны")
            .Metric("Изменение объёма",
                Fmt.Pct(CurrentVolume > 0 ? (OptimalVolume - CurrentVolume) / CurrentVolume : 0), null,
                "падение объёма — плата за маржу",
                OptimalVolume < CurrentVolume * 0.9 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Цена игнорирования каннибализации", Fmt.Money(CannibalizationCost), null,
                "сколько потеряли бы, оптимизируя каждый товар отдельно",
                CannibalizationCost > 0.02 * Math.Abs(CurrentProfit) ? MetricQuality.Warning : MetricQuality.Neutral);

        builder
            .FindingIf(best is not null,
                $"Наибольший вклад даёт «{best?.Name}»: цена {Fmt.Pct(best?.PriceChange ?? 0)}, " +
                $"прибыль {Fmt.Money((best?.OptimalProfit ?? 0) - (best?.CurrentProfit ?? 0))}.")
            .FindingIf(up.Count > 0 && down.Count > 0,
                "Решение не сводится к общему повышению или снижению: часть линейки дешевеет, " +
                "часть дорожает — это и есть эффект перекрёстных эластичностей.")
            .FindingIf(CannibalizationCost > 0.02 * Math.Abs(CurrentProfit),
                $"Оптимизация каждого товара в отрыве от линейки дала бы на " +
                $"{Fmt.Money(CannibalizationCost)} меньше прибыли: соседние позиции забирают спрос друг у друга.")
            .WarningIf(ConstraintsBinding,
                $"У {Products.Count(p => p.AtBound)} позиций цена упёрлась в границу допустимого " +
                "изменения. Настоящий оптимум лежит за ней — расширьте коридор или примите, " +
                "что решение неполное.")
            .WarningIf(OptimalVolume < CurrentVolume * 0.85,
                $"Объём падает на {Fmt.Pct(1 - (OptimalVolume / Math.Max(CurrentVolume, 1e-9)))}. " +
                "Если доля рынка важна стратегически, задайте ограничение MinTotalVolume.")
            .Warning("Модель постоянной эластичности верна только вблизи текущих цен. " +
                     "Изменения свыше 20–30 % экстраполируют её за пределы данных.")
            .Recommendation("Проверяйте рекомендацию экспериментом на части ассортимента " +
                            "или регионов, прежде чем менять цены целиком.")
            .RecommendationIf(ProfitGain > 0,
                "Начните с позиций из верхней части таблицы: они дают основную часть прироста " +
                "при наименьшем числе изменений.");

        return builder.Build();
    }
}

/// <summary>
/// Оптимизация цен товарной линейки при ограничениях на маржу, объём и
/// величину изменения цены.
/// </summary>
/// <remarks>
/// <para>
/// Спрос описывается моделью постоянной эластичности с перекрёстными эффектами:
/// </para>
/// <code>
/// q_i(p) = q_i0 * prod_j (p_j / p_j0) ^ E[i, j]
/// </code>
/// <para>
/// Максимизируется суммарная прибыль по линейке. Задача невыпуклая и с
/// ограничениями, поэтому решается симплекс-методом в пространстве
/// логарифмов ценовых множителей: границы изменения цены реализуются
/// отсечением, ограничения на маржу и объём — штрафом.
/// </para>
/// </remarks>
public static class PriceOptimizer
{
    /// <summary>Оптимизирует цены линейки.</summary>
    /// <param name="products">Товары с текущими ценами и себестоимостью.</param>
    /// <param name="elasticities">Матрица собственных и перекрёстных эластичностей.</param>
    /// <param name="constraints">Ограничения; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Рекомендованные цены и экономический эффект.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности не согласованы.</exception>
    public static PriceOptimizationResult Optimize(
        IReadOnlyList<ProductPricing> products,
        Matrix elasticities,
        PriceConstraints? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(elasticities);

        int n = products.Count;
        if (n == 0) throw new ArgumentException("Нужен хотя бы один товар.", nameof(products));
        if (elasticities.Height != n || elasticities.Width != n)
            throw new ArgumentException("Матрица эластичностей должна быть размера «товары x товары».",
                nameof(elasticities));

        constraints ??= new PriceConstraints();

        double[] optimal = Solve(products, elasticities, constraints, useCross: true);
        double[] independent = Solve(products, elasticities, constraints, useCross: false);

        double currentProfit = Profit(products, elasticities, Ones(n));
        double optimalProfit = Profit(products, elasticities, optimal);
        double independentProfit = Profit(products, elasticities, independent);

        double[] quantities = Demand(products, elasticities, optimal);
        double[] currentQuantities = [.. products.Select(p => p.CurrentQuantity)];

        var rows = new List<ProductPriceRecommendation>(n);
        (double[] lowerBound, double[] upperBound) = Bounds(products, constraints);

        for (int i = 0; i < n; i++)
        {
            double price = products[i].CurrentPrice * optimal[i];
            double logMultiplier = Math.Log(optimal[i]);

            rows.Add(new ProductPriceRecommendation
            {
                Name = products[i].Name,
                CurrentPrice = products[i].CurrentPrice,
                OptimalPrice = price,
                CurrentQuantity = products[i].CurrentQuantity,
                OptimalQuantity = quantities[i],
                CurrentProfit = (products[i].CurrentPrice - products[i].UnitCost) * products[i].CurrentQuantity,
                OptimalProfit = (price - products[i].UnitCost) * quantities[i],
                NewMargin = price > 0 ? (price - products[i].UnitCost) / price : 0,
                AtBound = Math.Abs(logMultiplier - lowerBound[i]) < 1e-6
                          || Math.Abs(logMultiplier - upperBound[i]) < 1e-6,
            });
        }

        return new PriceOptimizationResult
        {
            Products = [.. rows.OrderByDescending(r => r.OptimalProfit - r.CurrentProfit)],
            CurrentProfit = currentProfit,
            OptimalProfit = optimalProfit,
            CurrentRevenue = products.Sum(p => p.CurrentPrice * p.CurrentQuantity),
            OptimalRevenue = rows.Sum(r => r.OptimalPrice * r.OptimalQuantity),
            CurrentVolume = currentQuantities.Sum(),
            OptimalVolume = quantities.Sum(),
            IndependentOptimumProfit = independentProfit,
            CannibalizationCost = Math.Max(optimalProfit - independentProfit, 0),
        };
    }

    /// <summary>
    /// Оптимальная цена одного товара по правилу Лернера — замкнутое решение
    /// для проверки численного результата.
    /// </summary>
    /// <param name="unitCost">Переменные издержки на единицу.</param>
    /// <param name="elasticity">Собственная эластичность, должна быть меньше −1.</param>
    /// <returns>Цена, максимизирующая прибыль; <c>NaN</c> при неэластичном спросе.</returns>
    public static double LernerPrice(double unitCost, double elasticity) =>
        elasticity < -1.0 ? unitCost * elasticity / (1.0 + elasticity) : double.NaN;

    private static double[] Ones(int n)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = 1.0;
        return v;
    }

    /// <summary>Ищет вектор ценовых множителей симплекс-методом в логарифмах.</summary>
    private static double[] Solve(
        IReadOnlyList<ProductPricing> products, Matrix elasticities,
        PriceConstraints constraints, bool useCross)
    {
        int n = products.Count;
        (double[] lower, double[] upper) = Bounds(products, constraints);

        Matrix effective = useCross ? elasticities : CrossElasticity.Diagonal(Diagonal(elasticities));

        double Objective(double[] logMultipliers)
        {
            double[] multipliers = Clamp(logMultipliers, lower, upper);
            double profit = Profit(products, effective, multipliers);
            return -(profit - Penalty(products, effective, multipliers, constraints));
        }

        double[] start = new double[n];
        for (int i = 0; i < n; i++) start[i] = EconMath.Clamp(0, lower[i], upper[i]);

        double[] solution = NelderMead.Minimize(Objective, start, 0.15, 6000);
        return Clamp(solution, lower, upper);
    }

    /// <summary>
    /// Границы ценового множителя по каждому товару.
    /// </summary>
    /// <remarks>
    /// Требование минимальной маржи переводится не в штраф, а прямо в нижнюю
    /// границу цены: <c>p &gt;= c / (1 - m)</c>. Штраф у границы всегда
    /// продавливается — выигрыш в прибыли от малого нарушения растёт линейно,
    /// а квадратичный штраф вблизи нуля убывает быстрее.
    /// </remarks>
    private static (double[] Lower, double[] Upper) Bounds(
        IReadOnlyList<ProductPricing> products, PriceConstraints constraints)
    {
        int n = products.Count;
        var lower = new double[n];
        var upper = new double[n];

        for (int i = 0; i < n; i++)
        {
            double maxDown = Math.Log(Math.Max(1.0 - constraints.MaxPriceChange, 1e-3));
            double maxUp = Math.Log(1.0 + constraints.MaxPriceChange);

            if (constraints.MinMarginRate > 0 && constraints.MinMarginRate < 1 && products[i].CurrentPrice > 0)
            {
                double minPrice = products[i].UnitCost / (1.0 - constraints.MinMarginRate);
                double minMultiplier = Math.Log(Math.Max(minPrice / products[i].CurrentPrice, 1e-6));
                if (minMultiplier > maxDown) maxDown = minMultiplier;
            }

            // Ограничения могут оказаться несовместимыми: тогда приоритет
            // у маржи, а коридор изменения цены расширяется до неё
            if (maxDown > maxUp) maxUp = maxDown;

            lower[i] = maxDown;
            upper[i] = maxUp;
        }

        return (lower, upper);
    }

    private static Vector Diagonal(Matrix m)
    {
        var v = new Vector(m.Height);
        for (int i = 0; i < m.Height; i++) v[i] = m[i, i];
        return v;
    }

    private static double[] Clamp(double[] logMultipliers, double[] lower, double[] upper)
    {
        var result = new double[logMultipliers.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = Math.Exp(EconMath.Clamp(logMultipliers[i], lower[i], upper[i]));
        return result;
    }

    private static double[] Demand(IReadOnlyList<ProductPricing> products, Matrix elasticities, double[] multipliers)
    {
        int n = products.Count;
        var quantities = new double[n];

        for (int i = 0; i < n; i++)
        {
            double factor = 0;
            for (int j = 0; j < n; j++) factor += elasticities[i, j] * Math.Log(multipliers[j]);
            quantities[i] = products[i].CurrentQuantity * Math.Exp(factor);
        }

        return quantities;
    }

    private static double Profit(IReadOnlyList<ProductPricing> products, Matrix elasticities, double[] multipliers)
    {
        double[] quantities = Demand(products, elasticities, multipliers);
        double profit = 0;

        for (int i = 0; i < products.Count; i++)
            profit += ((products[i].CurrentPrice * multipliers[i]) - products[i].UnitCost) * quantities[i];

        return profit;
    }

    /// <summary>
    /// Штраф за нарушение совокупных ограничений на объём и выручку.
    /// </summary>
    /// <remarks>
    /// Линейное слагаемое обязательно: чисто квадратичный штраф вблизи границы
    /// убывает быстрее, чем растёт выигрыш в прибыли, и оптимум систематически
    /// оказывается чуть за пределами допустимой области. Ограничение на маржу
    /// сюда не входит — оно задано границами цены и выполняется точно.
    /// </remarks>
    private static double Penalty(
        IReadOnlyList<ProductPricing> products, Matrix elasticities,
        double[] multipliers, PriceConstraints constraints)
    {
        if (constraints.MinTotalVolume <= 0 && constraints.MinTotalRevenue <= 0) return 0;

        double[] quantities = Demand(products, elasticities, multipliers);
        double scale = Math.Max(products.Sum(p => p.CurrentPrice * p.CurrentQuantity), 1.0);
        double penalty = 0;

        if (constraints.MinTotalVolume > 0)
        {
            double gap = (constraints.MinTotalVolume - quantities.Sum()) / constraints.MinTotalVolume;
            if (gap > 0) penalty += scale * ((10 * gap) + (1000 * gap * gap));
        }

        if (constraints.MinTotalRevenue > 0)
        {
            double revenue = 0;
            for (int i = 0; i < products.Count; i++)
                revenue += products[i].CurrentPrice * multipliers[i] * quantities[i];

            double gap = (constraints.MinTotalRevenue - revenue) / constraints.MinTotalRevenue;
            if (gap > 0) penalty += scale * ((10 * gap) + (1000 * gap * gap));
        }

        return penalty;
    }
}

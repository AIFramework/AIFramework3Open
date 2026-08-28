using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Processors.Inorganic;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Production;

/// <summary>
/// Потребность в одном реагенте на партию
/// </summary>
/// <param name="Formula">Формула вещества</param>
/// <param name="Coefficient">Стехиометрический коэффициент</param>
/// <param name="MolarMass">Молярная масса, г/моль</param>
/// <param name="Kilomoles">Количество вещества, кмоль</param>
/// <param name="StoichiometricMass">Масса по стехиометрии, кг</param>
/// <param name="ExcessFraction">Избыток сверх стехиометрии (0.05 = 5%)</param>
public readonly record struct ReagentDemand(
    string Formula,
    int Coefficient,
    double MolarMass,
    double Kilomoles,
    double StoichiometricMass,
    double ExcessFraction)
{
    /// <summary>Масса с учётом избытка, кг</summary>
    public double MassWithExcess => StoichiometricMass * (1 + ExcessFraction);
}

/// <summary>
/// Материальный баланс партии: сколько сырья нужно на заданный выпуск продукта
/// </summary>
public sealed class ReactionDemand
{
    /// <summary>Сбалансированная реакция</summary>
    public BalancedReaction Reaction { get; init; }

    /// <summary>Формула целевого продукта</summary>
    public string Product { get; init; }

    /// <summary>Плановый выпуск продукта, кг</summary>
    public double ProductMass { get; init; }

    /// <summary>Выход по реакции (0..1)</summary>
    public double YieldFraction { get; init; }

    /// <summary>Теоретический выпуск при 100% выходе, кг</summary>
    public double TheoreticalProductMass { get; init; }

    /// <summary>Потребность в реагентах</summary>
    public IReadOnlyList<ReagentDemand> Reagents { get; init; }

    /// <summary>Побочные продукты реакции</summary>
    public IReadOnlyList<ReagentDemand> ByProducts { get; init; }

    /// <summary>Суммарная загрузка сырья с учётом избытков, кг</summary>
    public double TotalInputMass => Reagents.Sum(r => r.MassWithExcess);

    /// <summary>
    /// Отходы: всё, что загружено, но не вышло в целевом продукте, кг
    /// </summary>
    public double WasteMass => TotalInputMass - ProductMass;

    /// <summary>Расходный коэффициент: кг сырья на кг продукта</summary>
    public double MassIntensity => ProductMass > 0 ? TotalInputMass / ProductMass : double.NaN;

    /// <summary>
    /// E-фактор: кг отходов на кг продукта (показатель «зелёности» процесса)
    /// </summary>
    public double EFactor => ProductMass > 0 ? WasteMass / ProductMass : double.NaN;

    /// <summary>Атомная экономия реакции, %</summary>
    public double AtomEconomyPercent { get; init; }

    /// <summary>
    /// Превращает материальный баланс в рецептуру: остаётся проставить цены
    /// </summary>
    /// <param name="prices">Цены сырья по формуле, ден.ед./кг</param>
    /// <param name="purities">Чистота сырья по формуле (0..1); по умолчанию 1</param>
    public Recipe ToRecipe(IReadOnlyDictionary<string, double> prices = null,
        IReadOnlyDictionary<string, double> purities = null)
    {
        var recipe = new Recipe(Product, ProductMass) { YieldFraction = YieldFraction };

        foreach (var reagent in Reagents)
        {
            double price = prices != null && prices.TryGetValue(reagent.Formula, out double p) ? p : 0;
            double purity = purities != null && purities.TryGetValue(reagent.Formula, out double q) ? q : 1.0;

            recipe.Add(new RecipeComponent
            {
                Name = reagent.Formula,
                Formula = reagent.Formula,
                Quantity = reagent.MassWithExcess,
                PricePerKg = price,
                Purity = purity,
                Role = ComponentRole.RawMaterial
            });
        }

        return recipe;
    }

    /// <summary>Отчёт по материальному балансу</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"Материальный баланс: {Product}, партия {ProductMass.ToString("G6", culture)} кг");
        text.AppendLine($"  Реакция: {Reaction.Equation}");
        text.AppendLine($"  Выход: {(YieldFraction * 100).ToString("F1", culture)}%, "
            + $"теоретический выпуск: {TheoreticalProductMass.ToString("G6", culture)} кг");
        text.AppendLine();
        text.AppendLine("  вещество        коэф.  M, г/моль   кмоль      по стех., кг  с избытком, кг");

        foreach (var reagent in Reagents)
        {
            text.AppendLine(string.Format(culture,
                "  {0,-15} {1,4}   {2,-11:F3} {3,-10:G4} {4,-13:F2} {5:F2}",
                reagent.Formula, reagent.Coefficient, reagent.MolarMass,
                reagent.Kilomoles, reagent.StoichiometricMass, reagent.MassWithExcess));
        }

        if (ByProducts.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("  Побочные продукты:");

            foreach (var product in ByProducts)
            {
                text.AppendLine(string.Format(culture,
                    "    {0,-15} {1:F2} кг", product.Formula, product.StoichiometricMass));
            }
        }

        text.AppendLine();
        text.AppendLine($"  Загрузка сырья: {TotalInputMass.ToString("F2", culture)} кг");
        text.AppendLine($"  Расходный коэффициент: {MassIntensity.ToString("F2", culture)} кг/кг");
        text.AppendLine($"  Отходы: {WasteMass.ToString("F2", culture)} кг (E-фактор {EFactor.ToString("F2", culture)})");
        text.AppendLine($"  Атомная экономия: {AtomEconomyPercent.ToString("F1", culture)}%");

        return text.ToString();
    }
}

/// <summary>
/// Материальный баланс по уравнению реакции: от стехиометрии к загрузке сырья
/// </summary>
/// <remarks>
/// Расчёт ведётся в килограммах и киломолях: численно молярная масса в г/моль
/// совпадает с кг/кмоль, поэтому переводить единицы не требуется.
/// </remarks>
public static class MaterialBalance
{
    /// <summary>
    /// Считает потребность в сырье для получения заданного количества продукта
    /// </summary>
    /// <param name="equation">Уравнение реакции, например "N2 + H2 = NH3"</param>
    /// <param name="product">Формула целевого продукта</param>
    /// <param name="productMass">Плановый выпуск, кг</param>
    /// <param name="database">База атомных масс</param>
    /// <param name="yieldFraction">Выход по реакции (0..1)</param>
    /// <param name="excess">Избыток реагентов сверх стехиометрии по формуле (0.05 = 5%)</param>
    public static ReactionDemand FromReaction(
        string equation,
        string product,
        double productMass,
        ChemDatabase database,
        double yieldFraction = 1.0,
        IReadOnlyDictionary<string, double> excess = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (productMass <= 0)
            throw new ArgumentException("Product mass must be positive", nameof(productMass));

        if (yieldFraction is <= 0 or > 1)
            throw new ArgumentException("Yield must be within (0; 1]", nameof(yieldFraction));

        var balancer = new EquationBalancer(database, VerbosityLevel.Silent);

        if (!balancer.TryBalance(equation, out var reaction, out string error))
            throw new ArgumentException(error, nameof(equation));

        if (!reaction.TryFind(product, out var targetSpecies, out int targetCoefficient, out bool isProduct) || !isProduct)
            throw new ArgumentException($"'{product}' is not among the products of the reaction", nameof(product));

        double targetMolarMass = targetSpecies.CalculateMolarMass(database);

        if (targetMolarMass <= 0)
            throw new ArgumentException($"Molar mass of '{product}' could not be calculated", nameof(product));

        double theoreticalMass = productMass / yieldFraction;
        double productKilomoles = theoreticalMass / targetMolarMass;
        double reactionExtent = productKilomoles / targetCoefficient; // кмоль реакции

        var reagents = new List<ReagentDemand>();

        for (int i = 0; i < reaction.Reactants.Count; i++)
        {
            var species = reaction.Reactants[i];
            int coefficient = reaction.ReactantCoefficient(i);
            double molarMass = species.CalculateMolarMass(database);
            double kilomoles = reactionExtent * coefficient;

            double excessFraction = 0;
            excess?.TryGetValue(species.CoreFormula, out excessFraction);

            reagents.Add(new ReagentDemand(
                species.CoreFormula, coefficient, molarMass, kilomoles, kilomoles * molarMass, excessFraction));
        }

        var byProducts = new List<ReagentDemand>();

        for (int i = 0; i < reaction.Products.Count; i++)
        {
            var species = reaction.Products[i];

            if (ReferenceEquals(species, targetSpecies))
                continue;

            int coefficient = reaction.ProductCoefficient(i);
            double molarMass = species.CalculateMolarMass(database);
            double kilomoles = reactionExtent * coefficient;

            byProducts.Add(new ReagentDemand(
                species.CoreFormula, coefficient, molarMass, kilomoles, kilomoles * molarMass, 0));
        }

        return new ReactionDemand
        {
            Reaction = reaction,
            Product = targetSpecies.CoreFormula,
            ProductMass = productMass,
            YieldFraction = yieldFraction,
            TheoreticalProductMass = theoreticalMass,
            Reagents = reagents,
            ByProducts = byProducts,
            AtomEconomyPercent = reaction.AtomEconomyPercent(product, database)
        };
    }
}

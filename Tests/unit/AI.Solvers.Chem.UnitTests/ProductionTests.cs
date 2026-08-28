using AI.Solvers.Chem.Production;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Материальный баланс партии и калькуляция себестоимости.</summary>
public class ProductionTests
{
    /// <summary>
    /// 100 кг оксида кальция при выходе 90% требуют 198.3 кг известняка:
    /// 100/0.9 кг CaO - это 1.981 кмоль, столько же кмоль CaCO3 по 100.09 кг/кмоль.
    /// </summary>
    [Fact]
    public void Calcination_RequiresKnownAmountOfLimestone()
    {
        ReactionDemand demand = MaterialBalance.FromReaction(
            "CaCO3 = CaO + CO2", "CaO", 100, ChemTestContext.Database, 0.9);

        Assert.Equal(198.3, demand.Reagents[0].MassWithExcess, 0.5);
        Assert.Equal(111.11, demand.TheoreticalProductMass, 0.02);
        Assert.Equal(1.983, demand.MassIntensity, 0.01);
        Assert.Equal(56.03, demand.AtomEconomyPercent, 0.1);
    }

    [Fact]
    public void ByProducts_AreListedSeparately()
    {
        ReactionDemand demand = MaterialBalance.FromReaction(
            "CaCO3 = CaO + CO2", "CaO", 100, ChemTestContext.Database, 0.9);

        ReagentDemand byProduct = Assert.Single(demand.ByProducts);
        Assert.Equal("CO2", byProduct.Formula);
    }

    [Fact]
    public void Excess_IsAppliedPerReagent()
    {
        ReactionDemand demand = MaterialBalance.FromReaction(
            "N2 + H2 = NH3", "NH3", 1000, ChemTestContext.Database, 1.0,
            new Dictionary<string, double> { ["H2"] = 0.05 });

        ReagentDemand nitrogen = demand.Reagents.First(r => r.Formula == "N2");
        ReagentDemand hydrogen = demand.Reagents.First(r => r.Formula == "H2");

        Assert.Equal(822.4, nitrogen.MassWithExcess, 1.0);
        Assert.Equal(nitrogen.StoichiometricMass, nitrogen.MassWithExcess, 1e-9);
        Assert.Equal(hydrogen.StoichiometricMass * 1.05, hydrogen.MassWithExcess, 1e-9);
    }

    [Theory]
    [InlineData("CaCO3 = CaO + CO2", "MgO")]
    [InlineData("CaCO3 = CaO + CO2", "CaCO3")]
    public void UnknownProduct_IsRejected(string equation, string product)
        => Assert.Throws<ArgumentException>(
            () => MaterialBalance.FromReaction(equation, product, 100, ChemTestContext.Database));

    [Fact]
    public void UnbalanceableReaction_IsRejected()
        => Assert.Throws<ArgumentException>(
            () => MaterialBalance.FromReaction("Fe + O2 = FeO + Fe2O3 + Fe3O4", "FeO", 100, ChemTestContext.Database));

    private static Recipe SampleRecipe()
    {
        var recipe = new Recipe("продукт", 100)
            .Add("сырьё A", 120, 35)
            .Add("сырьё B", 80, 12)
            .Add("растворитель", 200, 5);

        recipe.Components[2].RecoveryFraction = 0.8;
        recipe.LaborHours = 8;
        recipe.LaborRatePerHour = 500;
        recipe.EnergyCost = 1200;
        recipe.OverheadPercent = 20;

        return recipe;
    }

    private const double ExpectedMaterial = (120 * 35) + (80 * 12) + (200 * 5 * 0.2);
    private const double ExpectedDirect = ExpectedMaterial + 4000 + 1200;

    [Fact]
    public void BatchCost_SumsMaterialsProcessingAndOverhead()
    {
        BatchCost cost = SampleRecipe().Cost();

        Assert.Equal(ExpectedMaterial, cost.MaterialCost, 1e-9);
        Assert.Equal(4000, cost.LaborCost, 1e-9);
        Assert.Equal(ExpectedDirect * 0.2, cost.OverheadCost, 1e-9);
        Assert.Equal(ExpectedDirect * 1.2 / 100, cost.CostPerKg, 1e-9);
    }

    /// <summary>Регенерация растворителя уменьшает затраты пропорционально возврату.</summary>
    [Fact]
    public void SolventRecovery_ReducesMaterialCost()
    {
        var withoutRecovery = new Recipe("продукт", 100).Add("растворитель", 200, 5);
        var withRecovery = new Recipe("продукт", 100).Add("растворитель", 200, 5);
        withRecovery.Components[0].RecoveryFraction = 0.8;

        Assert.Equal(1000, withoutRecovery.Cost().MaterialCost, 1e-9);
        Assert.Equal(200, withRecovery.Cost().MaterialCost, 1e-9);
    }

    /// <summary>Чистота сырья увеличивает закупаемое количество.</summary>
    [Fact]
    public void Purity_IncreasesPurchasedQuantity()
    {
        var recipe = new Recipe("продукт", 100).Add("сырьё", 100, 10, purity: 0.98);

        Assert.Equal(100 / 0.98, recipe.Components[0].GrossQuantity, 1e-9);
        Assert.Equal(100 / 0.98 * 10, recipe.Cost().MaterialCost, 1e-9);
    }

    [Fact]
    public void PricingHelpers_AreConsistent()
    {
        BatchCost cost = SampleRecipe().Cost();

        Assert.Equal(cost.CostPerKg, cost.BreakEvenPrice, 1e-12);
        Assert.Equal(cost.CostPerKg / 0.7, cost.PriceForMargin(30), 1e-9);
        Assert.Equal(100.0 * ((200 * 100) - cost.TotalCost) / (200 * 100), cost.MarginPercent(200), 1e-9);
    }

    /// <summary>
    /// Рост цены компонента поднимает себестоимость на его долю с учётом накладных.
    /// </summary>
    [Fact]
    public void Sensitivity_RanksCostDrivers()
    {
        Recipe recipe = SampleRecipe();
        BatchCost cost = recipe.Cost();
        IReadOnlyList<CostDriver> drivers = recipe.Sensitivity(0.10);

        Assert.Equal("сырьё A", drivers[0].Name);
        Assert.Equal(10.0 * (120 * 35 * 1.2) / cost.TotalCost, drivers[0].CostPerKgChangePercent, 0.01);
    }

    [Fact]
    public void ScaleTo_KeepsUnitCost()
    {
        Recipe recipe = SampleRecipe();
        Recipe scaled = recipe.ScaleTo(500);

        Assert.Equal(600, scaled.Components[0].Quantity, 1e-9);
        Assert.Equal(recipe.Cost().CostPerKg, scaled.Cost().CostPerKg, 1e-9);
    }

    /// <summary>Материальный баланс превращается в калькуляцию подстановкой цен.</summary>
    [Fact]
    public void MaterialBalance_FeedsCostCalculation()
    {
        ReactionDemand demand = MaterialBalance.FromReaction(
            "CaCO3 = CaO + CO2", "CaO", 100, ChemTestContext.Database, 0.9);

        Recipe recipe = demand.ToRecipe(new Dictionary<string, double> { ["CaCO3"] = 4.5 });

        Assert.Equal(198.31 * 4.5, recipe.Cost().MaterialCost, 3.0);
    }

    [Fact]
    public void Reports_AreProduced()
    {
        ReactionDemand demand = MaterialBalance.FromReaction(
            "CaCO3 = CaO + CO2", "CaO", 100, ChemTestContext.Database, 0.9);

        Assert.Contains("Материальный баланс", demand.Report(), StringComparison.Ordinal);
        Assert.Contains("Калькуляция", SampleRecipe().Cost().Report(200), StringComparison.Ordinal);
    }
}

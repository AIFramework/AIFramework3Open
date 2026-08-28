using AI.Solvers.Chem.Models;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Разбор химических формул: скобки, гидраты, заряд, состояние.</summary>
public class FormulaTests
{
    [Theory]
    [InlineData("H2O", "H2 O1")]
    [InlineData("Ca(OH)2", "Ca1 H2 O2")]
    [InlineData("Al2(SO4)3", "Al2 O12 S3")]
    [InlineData("K4[Fe(CN)6]", "C6 Fe1 K4 N6")]
    [InlineData("CuSO4·5H2O", "Cu1 H10 O9 S1")]
    [InlineData("CH3COOH", "C2 H4 O2")]
    [InlineData("Ca3(PO4)2", "Ca3 O8 P2")]
    public void Composition_IsParsed(string formula, string expected)
    {
        var parsed = new MolecularFormula(formula);
        string composition = string.Join(" ", parsed.Elements.OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => $"{e.Key}{e.Value}"));

        Assert.Equal(expected, composition);
    }

    [Theory]
    [InlineData("SO4^2-", -2)]
    [InlineData("Cu2+", 2)]
    [InlineData("Cl-", -1)]
    [InlineData("NH4+", 1)]
    [InlineData("Ca++", 2)]
    [InlineData("H2O", 0)]
    public void Charge_IsParsed(string formula, int expected)
        => Assert.Equal(expected, new MolecularFormula(formula).Charge);

    /// <summary>
    /// Без знака «^» цифра перед знаком - это индекс, а не заряд: MnO4⁻, а не MnO с зарядом -4.
    /// </summary>
    [Fact]
    public void PolyatomicAnion_KeepsSubscript()
    {
        var parsed = new MolecularFormula("MnO4-");

        Assert.Equal(-1, parsed.Charge);
        Assert.Equal(4, parsed.GetCount("O"));
        Assert.Equal(1, parsed.GetCount("Mn"));
    }

    [Theory]
    [InlineData("H2O(l)", "l")]
    [InlineData("CO2(g)", "g")]
    [InlineData("NaCl(s)", "s")]
    [InlineData("Ca(OH)2", null)]
    public void State_IsParsed(string formula, string? expected)
        => Assert.Equal(expected, new MolecularFormula(formula).State);

    [Theory]
    [InlineData("2H2O", 2)]
    [InlineData("10CO2", 10)]
    [InlineData("H2O", 1)]
    public void Coefficient_IsParsed(string formula, int expected)
        => Assert.Equal(expected, new MolecularFormula(formula).Coefficient);

    [Theory]
    [InlineData("H2SO4", 98.072)]
    [InlineData("Ca(OH)2", 74.092)]
    [InlineData("CuSO4·5H2O", 249.677)]
    [InlineData("K4[Fe(CN)6]", 368.34)]
    public void MolarMass_MatchesReference(string formula, double expected)
        => Assert.Equal(expected, new MolecularFormula(formula).CalculateMolarMass(ChemTestContext.Database), 0.02);

    [Fact]
    public void UnknownElement_IsReported()
    {
        var parsed = new MolecularFormula("XyZ2");

        Assert.False(parsed.TryCalculateMolarMass(ChemTestContext.Database, out _, out string error));
        Assert.Contains("Unknown element", error, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => parsed.CalculateMolarMass(ChemTestContext.Database));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ca(OH2")]
    [InlineData("2")]
    public void MalformedFormula_IsRejected(string formula)
        => Assert.False(MolecularFormula.TryParse(formula, out _, out _));

    /// <summary>Знак «+» разделяет вещества, но не разрывает заряд иона.</summary>
    [Fact]
    public void SplitSide_KeepsIonCharges()
    {
        string[] parts = MolecularFormula.SplitSide("MnO4- + 5Fe2+ + 8H+");

        Assert.Equal(new[] { "MnO4-", "5Fe2+", "8H+" }, parts);
    }

    [Fact]
    public void SplitSide_WorksWithoutSpaces()
        => Assert.Equal(new[] { "Fe", "O2" }, MolecularFormula.SplitSide("Fe+O2"));

    [Theory]
    [InlineData("Fe + O2 = Fe2O3", "Fe + O2", "Fe2O3")]
    [InlineData("N2 + H2 -> NH3", "N2 + H2", "NH3")]
    [InlineData("A ⇌ B", "A", "B")]
    public void Equation_IsSplit(string equation, string reactants, string products)
    {
        Assert.True(MolecularFormula.TrySplitEquation(equation, out string left, out string right));
        Assert.Equal(reactants, left);
        Assert.Equal(products, right);
    }

    [Fact]
    public void EquationWithoutArrow_IsRejected()
        => Assert.False(MolecularFormula.TrySplitEquation("Fe + O2", out _, out _));

    [Theory]
    [InlineData("SO4^2-", "SO42-")]
    [InlineData("H2O(l)", "H2O(l)")]
    [InlineData("2H2O", "2H2O")]
    [InlineData("Ca(OH)2", "CaO2H2")]
    public void ToString_RestoresCoefficientChargeAndState(string formula, string expected)
        => Assert.Equal(expected, new MolecularFormula(formula).ToString());
}

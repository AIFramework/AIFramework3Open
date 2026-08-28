using AI.Solvers.Chem.Core;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Команды движка: неорганика, физхимия, аналитика, медицина, органика.</summary>
public class EngineTests
{
    [Theory]
    [InlineData("balance Fe + O2 = Fe2O3", "4 Fe + 3 O2 = 2 Fe2O3")]
    [InlineData("balance C3H8 + O2 = CO2 + H2O", "C3H8 + 5 O2 = 3 CO2 + 4 H2O")]
    [InlineData("balance N2 + H2 -> NH3", "N2 + 3 H2 = 2 NH3")]
    [InlineData("balance Ca(OH)2 + H3PO4 = Ca3(PO4)2 + H2O", "3 Ca(OH)2 + 2 H3PO4 = Ca3(PO4)2 + 6 H2O")]
    [InlineData("balance Ag+ + Cl- = AgCl", "Ag+ + Cl- = AgCl")]
    public void Balance_ProducesKnownEquation(string command, string expected)
        => Assert.Equal(expected, ChemTestContext.Ok(command).Result.Trim());

    /// <summary>Классический трудный случай: скобки и нетривиальные коэффициенты.</summary>
    [Fact]
    public void Balance_CopperWithNitricAcid()
        => ChemTestContext.Ok("balance Cu + HNO3 = Cu(NO3)2 + NO + H2O")
            .ShouldContain("3 Cu", "8 HNO3", "2 NO", "4 H2O");

    /// <summary>Ионное уравнение балансируется с учётом заряда.</summary>
    [Fact]
    public void Balance_IonicEquationUsesChargeBalance()
        => ChemTestContext.Ok("balance MnO4- + Fe2+ + H+ = Mn2+ + Fe3+ + H2O")
            .ShouldContain("5 Fe2+", "8 H+");

    [Theory]
    [InlineData("balance Fe + O2 = FeO + Fe2O3 + Fe3O4", "underdetermined")]
    [InlineData("balance H2O = H2 + Xx2", "Unknown element")]
    public void Balance_RefusesInsteadOfGuessing(string command, string reason)
        => Assert.Contains(reason, ChemTestContext.Fail(command).ErrorMessage, StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("molar mass of H2SO4", "98.0")]
    [InlineData("molar mass of CuSO4·5H2O", "249.6")]
    [InlineData("molar mass of K4[Fe(CN)6]", "368.3")]
    [InlineData("molarity of 10g NaOH in 500ml", "0.500")]
    [InlineData("dilute 2M HCl to 0.5M, volume 100ml", "25.00")]
    public void Stoichiometry_AndSolutions(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    [Theory]
    [InlineData("pH of 0.01M HCl", "pH = 2.00")]
    [InlineData("pH of 0.1M CH3COOH Ka=1.8e-5", "pH = 2.87")]
    [InlineData("buffer pKa=4.76 acid=0.1M base=0.15M", "pH = 4.94")]
    [InlineData("titration acid=0.1M base=0.1M V_acid=25ml V_base=10ml", "pH = 1.37")]
    public void AcidBase(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    /// <summary>Без объёма титранта команда строит кривую, как обещает справка.</summary>
    [Fact]
    public void Titration_WithoutTitrantVolume_BuildsCurve()
        => ChemTestContext.Ok("titration curve acid=0.1M base=0.1M V_acid=25ml")
            .ShouldContain("Equivalence point", "25.00");

    /// <summary>Заданная pKa переключает расчёт на слабую кислоту.</summary>
    [Fact]
    public void Titration_WithPka_UsesWeakAcidModel()
        => ChemTestContext.Ok("titration curve acid=0.1M base=0.1M V_acid=25ml pKa=4.76")
            .ShouldContain("Initial Point", "Buffer Region");

    [Theory]
    [InlineData("solubility of AgCl", "1.330E-005")]
    [InlineData("common ion compound=AgCl ion=Cl concentration=0.1M", "1.770E-009")]
    [InlineData("predict precipitation compound=AgCl [Ag]=0.01M [Cl]=0.001M", "YES")]
    [InlineData("fractional precipitation compound1=AgCl compound2=AgBr anion=0.001", "AgBr")]
    public void Solubility(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    /// <summary>Ионное произведение для соли 1:2 считается как [K]·[A]².</summary>
    [Fact]
    public void Precipitation_UsesStoichiometry()
        => ChemTestContext.Ok("predict precipitation compound=CaF2 [Ca]=0.01M [F]=0.001M")
            .ShouldContain("YES", "1.000E-008");

    [Theory]
    [InlineData("complex metal=Cu ligand=NH3 [metal]=0.1M [ligand]=1.0M", "complexed")]
    [InlineData("stepwise complex metal=Cu ligand=NH3 [ligand]=0.1M", "Dominant species")]
    [InlineData("complex metal=Ca ligand=EDTA pH=10 [metal]=0.01M [EDTA]=0.01M", "EDTA")]
    [InlineData("chelate effect metal=Ni ligand1=NH3 ligand2=en", "Chelate")]
    public void Complexes(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    /// <summary>Потенциал зависит от концентрации иона, а не остаётся стандартным.</summary>
    [Fact]
    public void Nernst_DependsOnConcentration()
    {
        double standard = (double)ChemTestContext.Ok("Nernst ion=Cu2+ [Cu2+]=1.0").Data["E"];
        double dilute = (double)ChemTestContext.Ok("Nernst ion=Cu2+ [Cu2+]=0.01").Data["E"];

        Assert.True(standard > dilute, $"E(1 M) = {standard:F3} должен быть больше E(0.01 M) = {dilute:F3}");
        Assert.Equal(0.281, dilute, 0.002);
    }

    /// <summary>Стандартный потенциал берётся из справочника, если не задан.</summary>
    [Fact]
    public void Nernst_TakesStandardPotentialFromDatabase()
        => ChemTestContext.Ok("Nernst ion=Cu2+ [Cu2+]=0.01").ShouldContain("Cu2+/Cu");

    [Theory]
    [InlineData("Nernst n=2 E0=0.34 [Cu2+]=0.01", "E = 0.28")]
    [InlineData("Faraday I=2A time=3600s substance=Cu", "2.37")]
    [InlineData("Faraday I=1.5 time=1800 substance=Ag n=1", "3.0")]
    public void Electrochemistry(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    [Theory]
    [InlineData("rate law k=0.05 A=0.1 B=0.2 orderA=1 orderB=2", "2.000E-004")]
    [InlineData("rate law k=0.05 concentrations=0.1,0.2 orders=1,2", "2.000E-004")]
    [InlineData("half-life k=0.05 order=1", "1.386E+001")]
    [InlineData("half-life k=0.05 order=2 A0=2.0", "1.000E+001")]
    [InlineData("Arrhenius k1=0.01 T1=300 k2=0.05 T2=320", "Ea")]
    [InlineData("determine order rate1=0.1 rate2=0.4 conc1=0.1 conc2=0.2", "n = 2.00")]
    [InlineData("integrated rate law order=1 A0=1.0 k=0.05 time=10", "6.065E-001")]
    public void Kinetics(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    [Fact]
    public void IdealGas_SolvesForMissingVariable()
        => ChemTestContext.Ok("ideal gas P=2 V=10 T=300 find n").ShouldContain("0.812");

    /// <summary>
    /// Энтальпия берётся с учётом агрегатного состояния: для воды стандартное
    /// состояние жидкое, поэтому сгорание метана даёт -890, а не ноль.
    /// </summary>
    [Fact]
    public void Thermochemistry_UsesStandardStates()
        => ChemTestContext.Ok("calculate delta H for CH4 + 2O2 = CO2 + 2H2O").ShouldContain("-890");

    /// <summary>Отсутствие справочных данных - отказ, а не молчаливый ноль.</summary>
    [Fact]
    public void Thermochemistry_RefusesWithoutData()
        => Assert.Contains("not available",
            ChemTestContext.Fail("calculate delta H for XeF6 = Xe + 3F2").ErrorMessage, StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("Beer's law eps=150 c=0.005M l=1cm", "0.750")]
    [InlineData("Beer's law A=0.45 eps=1500 l=1", "3.000E-004")]
    [InlineData("Beer law A=0.8 c=0.002M l=1cm", "4.000E+002")]
    [InlineData("Beer's law T=45%", "0.347")]
    [InlineData("mixture analysis A1=0.5 A2=0.7 eps1_1=100 eps1_2=50 eps2_1=40 eps2_2=120 l=1", "c1")]
    [InlineData("calibration concentrations=1,2,3,4,5 absorbance=0.1,0.2,0.3,0.4,0.5", "0.1")]
    public void Spectroscopy(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    [Theory]
    [InlineData("blood gas pH=7.25 pCO2=55mmHg HCO3=24mEq/L", "Respiratory Acidosis")]
    [InlineData("bicarbonate pH=7.40 pCO2=40mmHg", "23.9")]
    [InlineData("base excess HCO3=18mEq/L pH=7.30", "Metabolic Acidosis")]
    [InlineData("Michaelis-Menten Vmax=100 Km=0.5M S=1.0M", "66.6")]
    [InlineData("Michaelis-Menten Vmax=100 Km=0.5 S=0.5", "50.0")]
    [InlineData("Lineweaver-Burk substrate=0.001,0.002,0.005,0.01 velocity=10,18,33,45", "Km")]
    [InlineData("enzyme inhibition type=noncompetitive Vmax=100 Km=0.5 S=1.0 I=0.1 Ki=0.05", "Non-Competitive")]
    [InlineData("specific activity activity=500units protein=2.5mg", "200")]
    [InlineData("pharmacokinetics type=iv_bolus dose=500mg Vd=50L t_half=6h time=12h", "C(t)")]
    [InlineData("pharmacokinetics calculate_half_life C1=10 C2=5 t1=0h t2=6h", "6.0")]
    [InlineData("dose target_concentration=10mg/L Vd=50L bioavailability=0.9 t_half=6h", "Loading")]
    public void Medical(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    [Theory]
    [InlineData("oxidation states of KMnO4", "Mn: +7")]
    [InlineData("oxidation states of H2SO4", "S: +6")]
    [InlineData("oxidation states of SO4^2-", "S: +6")]
    [InlineData("oxidation states of NaH", "H: -1")]
    public void OxidationStates(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    /// <summary>В Fe3O4 железо имеет дробную степень окисления +8/3.</summary>
    [Fact]
    public void OxidationStates_AllowFractionalValues()
        => ChemTestContext.Ok("oxidation states of Fe3O4").ShouldContain("+2.67");

    [Theory]
    [InlineData("properties of element Fe", "Iron")]
    [InlineData("lookup water", "H2O")]
    [InlineData("isomers of C4H10", "Isobutane")]
    [InlineData("functional groups CC(=O)O", "Carboxyl")]
    [InlineData("SMILES to structure CC(C)CCO", "C5H12O")]
    [InlineData("IUPAC name CCO", "ethanol")]
    [InlineData("structure to SMILES ethanol", "CCO")]
    [InlineData("analyze CC(=O)Oc1ccccc1C(=O)O", "C9H8O4")]
    [InlineData("retrosynthesis aspirin from benzene", "sulfonation")]
    public void Organic(string command, string expected)
        => ChemTestContext.Ok(command).ShouldContain(expected);

    /// <summary>
    /// Регистр SMILES значим: цель ретросинтеза нельзя приводить к нижнему регистру,
    /// иначе ароматические и алифатические атомы перепутаются.
    /// </summary>
    [Fact]
    public void Retrosynthesis_KeepsSmilesCase()
        => ChemTestContext.Ok("retrosynthesis CC(=O)Oc1ccccc1C(=O)O from benzene").ShouldContain("РЕТРОСИНТЕЗ");

    /// <summary>Отсутствующий параметр даёт понятное сообщение, а не KeyNotFoundException.</summary>
    [Fact]
    public void MissingParameter_IsExplained()
    {
        ChemResult result = ChemTestContext.Fail("Michaelis-Menten Vmax=100 Km=0.5");

        Assert.Contains("Missing required parameter", result.ErrorMessage, StringComparison.Ordinal);
    }
}

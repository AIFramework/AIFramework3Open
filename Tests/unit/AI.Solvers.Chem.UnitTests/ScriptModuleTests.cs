using AI.Script.Chem;
using AI.Script.Hosting;
using AI.Script.Std;
using System.Globalization;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Модуль <c>chem</c> языка AIScript и инструменты агента.</summary>
public class ScriptModuleTests
{
    private static ScriptHost Host() => StandardLibrary.CreateHost().UseChem();

    private static RunResult Run(string source)
        => Host().RunAsync(source).GetAwaiter().GetResult();

    private static RunResult RunOk(string source)
    {
        RunResult result = Run(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Message)));

        return result;
    }

    private static double Number(string source)
        => Convert.ToDouble(RunOk(source).Emitted["r"], CultureInfo.InvariantCulture);

    private static bool Flag(string source)
        => Assert.IsType<bool>(RunOk(source).Emitted["r"]);

    [Theory]
    [InlineData("emit r = chem.mass(\"H2SO4\")", 98.072)]
    [InlineData("emit r = chem.mass(\"CuSO4·5H2O\")", 249.677)]
    [InlineData("emit r = chem.formula(\"Ca(OH)2\").mass", 74.092)]
    [InlineData("emit r = chem.formula(\"SO4^2-\").charge", -2)]
    public void Formulas_AreComputed(string source, double expected)
        => Assert.Equal(expected, Number(source), 0.01);

    [Fact]
    public void Balance_IsAvailableInScript()
        => Assert.Equal("4 Fe + 3 O2 = 2 Fe2O3", RunOk("emit r = chem.balance(\"Fe + O2 = Fe2O3\")").Emitted["r"]);

    [Fact]
    public void Check_AcceptsBalancedEquation()
        => Assert.True(Flag("emit r = chem.check(\"2H2 + O2 = 2H2O\").balanced"));

    [Fact]
    public void Check_RejectsUnbalancedEquation()
        => Assert.True(Flag("emit r = chem.check(\"H2 + O2 = H2O\").balanced == false"));

    [Fact]
    public void Solve_RunsEngineCommand()
        => Assert.True(Flag("emit r = chem.solve(\"pH of 0.01M HCl\").ok"));

    [Fact]
    public void Calibrate_ReturnsDetectionLimit()
    {
        double lod = Number(
            "let c = <0, 1, 2, 5, 10>\n"
            + "let s = <0.012, 0.105, 0.203, 0.501, 0.998>\n"
            + "emit r = chem.calibrate(c, s).lod");

        Assert.True(lod > 0 && lod < 1, $"LOD = {lod:G4}");
    }

    [Fact]
    public void Concentration_IsBackCalculated()
    {
        double value = Number(
            "let c = <0, 1, 2, 5, 10>\n"
            + "let s = <0.012, 0.105, 0.203, 0.501, 0.998>\n"
            + "emit r = chem.concentration(c, s, response: 0.501).value");

        Assert.Equal(5.0, value, 0.05);
    }

    [Fact]
    public void Outlier_IsDetected()
        => Assert.True(Flag("emit r = chem.outlier(<10.1, 10.2, 10.0, 10.15, 10.05, 12.4>).is_outlier"));

    [Fact]
    public void Precision_SeparatesVariances()
        => Assert.Equal(2.0, Number("emit r = chem.precision([<10, 10, 10>, <12, 12, 12>, <14, 14, 14>]).sl"), 1e-9);

    [Fact]
    public void ControlChart_ReportsOutOfControl()
        => Assert.True(Flag(
            "emit r = chem.control_chart(<10.0, 10.1, 9.9, 10.05, 9.95, 10.02, 9.98, 10.03, 12.5>).in_control == false"));

    [Fact]
    public void Smooth_PreservesStraightLine()
        => Assert.Equal(0.0, Number(
            "let x = vec.arange(0, 20, by: 1)\n"
            + "emit r = stat.max(vec.abs(chem.smooth(x, window: 9) - x))"), 1e-9);

    [Fact]
    public void Batch_ReturnsRawMaterialDemand()
        => Assert.Equal(198.3, Number(
            "let t = chem.batch(\"CaCO3 = CaO + CO2\", product: \"CaO\", mass: 100, yield: 0.9)\n"
            + "emit r = vec.sum(table.column(t, \"mass_kg\"))"), 0.5);

    [Fact]
    public void Cost_ComputesUnitCost()
        => Assert.Equal(((120 * 35) + (80 * 12)) / 100.0, Number(
            "let c = chem.cost([\"A\", \"B\"], <120, 80>, <35, 12>, batch: 100)\n"
            + "emit r = c.per_kg"), 1e-9);

    [Fact]
    public void Peaks_AreReturnedAsTable()
    {
        const string source =
            "let t = <0, 1, 2, 3, 4, 5, 6, 7, 8>\n"
            + "let s = <0, 1, 3, 7, 10, 7, 3, 1, 0>\n"
            + "let p = chem.peaks(t, s, window: 0)\n"
            + "emit r = len(table.column(p, \"rt\"))";

        Assert.Equal(1.0, Number(source), 1e-9);
    }

    [Fact]
    public void Peaks_ReportRetentionTime()
        => Assert.Equal(4.0, Number(
            "let t = <0, 1, 2, 3, 4, 5, 6, 7, 8>\n"
            + "let s = <0, 1, 3, 7, 10, 7, 3, 1, 0>\n"
            + "emit r = vec.sum(table.column(chem.peaks(t, s, window: 0), \"rt\"))"), 0.01);

    [Fact]
    public void FitOrder_IsAvailableInScript()
        => Assert.Equal(1.0, Number(
            "let t = <0, 5, 10, 20, 40>\n"
            + "let c = <1.0, 0.7788, 0.6065, 0.3679, 0.1353>\n"
            + "emit r = chem.fit_order(t, c).order"), 1e-9);

    [Fact]
    public void Arrhenius_IsAvailableInScript()
        => Assert.Equal(80.0, Number(
            "let temp = <300, 310, 320, 330>\n"
            + "let k = <1.0966e-2, 3.0783e-2, 8.1206e-2, 0.20302>\n"
            + "emit r = chem.arrhenius(temp, k).ea"), 0.2);

    [Fact]
    public void Runaway_IsAvailableInScript()
        => Assert.True(Number(
            "emit r = chem.runaway(heat: 800000, cp: 1800, ea: 120, a: 1e13, t0: 350).dt_adiabatic") > 400);

    [Fact]
    public void Classify_ReturnsSignalWord()
        => Assert.Equal("Опасно", RunOk(
            "emit r = chem.classify([\"щёлочь\"], <6>, [\"Skin Corr. 1B\"]).signal").Emitted["r"]);

    [Fact]
    public void Classify_ReportsNonHazardousMixture()
        => Assert.True(Flag("emit r = chem.classify([\"вода\"], <100>, [\"\"]).hazardous == false"));

    [Fact]
    public void Classify_RejectsUnknownNotation()
    {
        RunResult result = Run("emit r = chem.classify([\"нечто\"], <10>, [\"Очень Опасно 1\"]).signal");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("неизвестная классификация", StringComparison.Ordinal));
    }

    [Fact]
    public void SafetyDataSheet_IsAvailableInScript()
    {
        object sheet = RunOk(
            "emit r = chem.sds(\"Растворитель\", [\"метанол\"], <60>, [\"Acute Tox. 3 (oral); STOT SE 1\"])")
            .Emitted["r"];

        string text = Assert.IsType<string>(sheet);
        Assert.Contains("РАЗДЕЛ 16.", text, StringComparison.Ordinal);
        Assert.Contains("H301", text, StringComparison.Ordinal);
    }

    /// <summary>Прикладная ошибка возвращается диагностикой, а не роняет хост.</summary>
    [Fact]
    public void UnknownElement_IsReportedAsDiagnostic()
    {
        RunResult result = Run("emit r = chem.mass(\"XyZ2\")");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unknown element", StringComparison.Ordinal));
    }

    [Fact]
    public void Module_IsRegisteredUnderChemNamespace()
        => Assert.Equal("chem", ChemLibrary.Module.Name);
}

/// <summary>Инструменты, которые агент вызывает напрямую.</summary>
public class ChemAgentToolTests
{
    private readonly ChemAgentTools _tools = new();

    [Fact]
    public void MolarMass_IsExact()
        => Assert.Contains("368.3", _tools.MolarMass("K4[Fe(CN)6]"), StringComparison.Ordinal);

    [Fact]
    public void MolarMass_ExplainsBadFormula()
        => Assert.Contains("не разобрана", _tools.MolarMass("Ca(OH2"), StringComparison.Ordinal);

    [Fact]
    public void Balance_ReturnsCoefficients()
    {
        string answer = _tools.Balance("Cu + HNO3 = Cu(NO3)2 + NO + H2O");

        Assert.Contains("3 Cu", answer, StringComparison.Ordinal);
        Assert.Contains("8 HNO3", answer, StringComparison.Ordinal);
    }

    /// <summary>На неразрешимом наборе веществ инструмент отказывает, а не выдумывает.</summary>
    [Fact]
    public void Balance_RefusesWhenUnderdetermined()
        => Assert.Contains("не удалось", _tools.Balance("Fe + O2 = FeO + Fe2O3 + Fe3O4"), StringComparison.Ordinal);

    [Fact]
    public void CheckEquation_CatchesUnbalanced()
    {
        string verdict = _tools.CheckEquation("H2 + O2 = H2O");

        Assert.Contains("НЕ сбалансировано", verdict, StringComparison.Ordinal);
        Assert.Contains("2 H2", verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckEquation_AcceptsBalanced()
    {
        string verdict = _tools.CheckEquation("2H2 + O2 = 2H2O");

        Assert.Contains("сбалансировано", verdict, StringComparison.Ordinal);
        Assert.DoesNotContain("НЕ сбалансировано", verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckEquation_ChecksChargeToo()
        => Assert.Contains("заряд", _tools.CheckEquation("Ag+ + Cl- = AgCl + H+"), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Calculate_RunsEngineCommand()
        => Assert.Contains("pH = 2.00", _tools.Calculate("pH of 0.01M HCl"), StringComparison.Ordinal);

    [Fact]
    public void Calculate_ExplainsFailure()
        => Assert.Contains("не выполнен", _tools.Calculate("сделай красиво"), StringComparison.Ordinal);

    /// <summary>Классификацию смеси считают правила, а агент получает готовое обоснование.</summary>
    [Fact]
    public void ClassifyMixture_AppliesRules()
    {
        string answer = _tools.ClassifyMixture(
            "метанол | 60 | Flam. Liq. 2; Acute Tox. 3 (oral); STOT SE 1\n"
            + "толуол | 30 | Skin Irrit. 2; Repr. 2\n"
            + "вода | 10 |");

        Assert.Contains("Опасно", answer, StringComparison.Ordinal);
        Assert.Contains("H301", answer, StringComparison.Ordinal);
        Assert.Contains("против порога", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassifyMixture_ReportsUnknownNotation()
        => Assert.Contains("Не распознаны классификации",
            _tools.ClassifyMixture("нечто | 50 | Совсем Опасно 1"), StringComparison.Ordinal);

    [Fact]
    public void ClassifyMixture_ExplainsBadInput()
        => Assert.Contains("Состав не разобран", _tools.ClassifyMixture("просто текст"), StringComparison.Ordinal);
}

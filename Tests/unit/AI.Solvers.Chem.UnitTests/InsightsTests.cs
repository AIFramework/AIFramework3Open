using AI.Insights;
using AI.Solvers.Chem.Crystallography;
using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Polymers;
using AI.Solvers.Chem.Quantum;
using AI.Units;

namespace AI.Solvers.Chem.UnitTests;

/// <summary>
/// Объяснимость результатов: каждый разбор обязан назвать метрики, следствия
/// и — главное — границы применимости метода, за которыми числам верить нельзя.
/// </summary>
public class InsightsTests
{
    #region Хюккель

    [Fact]
    public void Huckel_Interpretation_ReportsFrontierOrbitalsAndGap()
    {
        Interpretation interpretation = Huckel.Solve(PiSystem.Ring(6)).Interpret();

        Assert.False(string.IsNullOrWhiteSpace(interpretation.Summary));
        Assert.Contains(interpretation.Metrics, m => m.Name == "E(ВЗМО)");
        Assert.Contains(interpretation.Metrics, m => m.Name == "E(НСМО)");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Щель");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Энергия делокализации");
    }

    [Fact]
    public void Huckel_Interpretation_StatesMethodLimits()
    {
        Interpretation interpretation = Huckel.Solve(PiSystem.Ring(6)).Interpret();

        Assert.Contains(interpretation.Warnings, w => w.Contains("π-подсистему", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("эмпирические параметры", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("перекрывания", StringComparison.Ordinal));
    }

    [Fact]
    public void Huckel_Benzene_IsRecognizedAsAromatic()
    {
        Interpretation interpretation = Huckel.Solve(PiSystem.Ring(6)).Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("4n+2", StringComparison.Ordinal));
        Assert.Contains(interpretation.Findings, f => f.Contains("стабилизацию", StringComparison.Ordinal));
    }

    [Fact]
    public void Huckel_Ethylene_ReportsNoDelocalization()
    {
        Interpretation interpretation = Huckel.Solve(PiSystem.Chain(2)).Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("изолированных", StringComparison.Ordinal));
        Assert.DoesNotContain(interpretation.Findings, f => f.Contains("4n+2", StringComparison.Ordinal));
    }

    [Fact]
    public void Huckel_OddElectronSystem_IsFlaggedAsRadical()
    {
        // Циклопентадиенил: пять центров, пять π-электронов — верхняя орбиталь занята наполовину
        HuckelSolution solution = Huckel.Solve(PiSystem.Ring(5));

        Assert.Equal(1, solution.Electrons % 2);
        Assert.Contains(solution.Interpret().Findings,
            f => f.Contains("система — радикал", StringComparison.Ordinal));
    }

    #endregion

    #region Индицирование

    [Fact]
    public void Indexing_Interpretation_WarnsAboutCubicAssumption()
    {
        IndexingResult result = PowderAnalysis.IndexCubic(
            [38.47, 44.72, 65.09, 78.23, 82.44, 99.08], PowderPattern.CopperKAlpha);

        Interpretation interpretation = result.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Параметр a");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Критерий качества");
        Assert.Contains(interpretation.Warnings, w => w.Contains("кубической сингонии", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("Интенсивности", StringComparison.Ordinal));
    }

    [Fact]
    public void Indexing_FewLines_IsCalledOutAsUnreliable()
    {
        IndexingResult result = PowderAnalysis.IndexCubic([38.47, 44.72, 65.09], PowderPattern.CopperKAlpha);

        if (result is null)
            return;

        Interpretation interpretation = result.Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("Линий всего", StringComparison.Ordinal));
    }

    #endregion

    #region Бюджет неопределённости

    [Fact]
    public void Budget_Interpretation_NamesDominantSource()
    {
        var budget = new UncertaintyBudget("масса навески", 1.0, Si.Gram)
            .Add("весы", 0.0100)
            .Add("калибровка", 0.0001);

        Interpretation interpretation = budget.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Главный источник" && m.Value == "весы");
        Assert.Contains(interpretation.Findings, f => f.Contains("весы", StringComparison.Ordinal));
    }

    [Fact]
    public void Budget_Interpretation_WarnsAboutCorrelationAssumption()
    {
        var budget = new UncertaintyBudget("масса навески", 1.0, Si.Gram).Add("весы", 0.0002);

        Assert.Contains(budget.Interpret().Warnings,
            w => w.Contains("некоррелированными", StringComparison.Ordinal));
    }

    [Fact]
    public void Budget_EmptyBudget_IsHonestAboutIt()
    {
        Interpretation interpretation = new UncertaintyBudget("отклик", 1.0, Si.Gram).Interpret();

        Assert.Contains(interpretation.Warnings,
            w => w.Contains("ни одной составляющей", StringComparison.Ordinal));
    }

    [Fact]
    public void Budget_TypeBOnly_IsReportedAsUnverified()
    {
        var budget = new UncertaintyBudget("масса", 1.0, Si.Gram).Add("весы", 0.0002);

        Assert.Contains(budget.Interpret().Findings,
            f => f.Contains("типа B", StringComparison.Ordinal));
    }

    [Fact]
    public void Budget_SmallDegreesOfFreedom_ExplainsCoverageFactor()
    {
        var budget = new UncertaintyBudget("концентрация", 10.0, Si.Gram)
            .Add(UncertaintyComponent.FromSeries("серия", [10.1, 10.4, 9.8]));

        Interpretation interpretation = budget.Interpret();

        Assert.True(budget.EffectiveDegreesOfFreedom < 10);
        Assert.Contains(interpretation.Findings, f => f.Contains("ν_eff", StringComparison.Ordinal));
    }

    #endregion

    #region Полимеры и QSAR

    [Fact]
    public void Polymer_NarrowDistribution_IsRecognized()
    {
        var narrow = new MolarMassDistribution([100_000.0, 105_000.0], [0.5, 0.5]);
        Interpretation interpretation = narrow.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Đ = Mw/Mn");
        Assert.Contains(interpretation.Findings, f => f.Contains("узкое", StringComparison.Ordinal));
    }

    [Fact]
    public void Polymer_BroadDistribution_IsRecognized()
    {
        var broad = new MolarMassDistribution(
            [1_000.0, 20_000.0, 500_000.0], [0.4, 0.3, 0.3]);

        Interpretation interpretation = broad.Interpret();

        Assert.True(broad.Dispersity > 3.0);
        Assert.Contains(interpretation.Findings, f => f.Contains("широкое", StringComparison.Ordinal));
    }

    [Fact]
    public void Polymer_Interpretation_WarnsAboutRelativeCalibration()
    {
        var distribution = new MolarMassDistribution([50_000.0, 100_000.0], [0.5, 0.5]);

        Assert.Contains(distribution.Interpret().Warnings,
            w => w.Contains("калибровку", StringComparison.Ordinal));
    }

    #endregion

    #region Текст для языковой модели

    [Fact]
    public void Interpretation_RendersAllSectionsForLanguageModel()
    {
        string text = Huckel.Solve(PiSystem.Ring(6)).Interpret().ToLlmText();

        Assert.Contains("Метрики:", text, StringComparison.Ordinal);
        Assert.Contains("Выводы:", text, StringComparison.Ordinal);
        Assert.Contains("Предупреждения:", text, StringComparison.Ordinal);
        Assert.Contains("Рекомендации:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpretation_IsExposedThroughTheCommonInterface()
    {
        IInterpretable[] results =
        [
            Huckel.Solve(PiSystem.Ring(6)),
            new UncertaintyBudget("масса", 1.0, Si.Gram).Add("весы", 0.0002),
            new MolarMassDistribution([50_000.0, 100_000.0], [0.5, 0.5]),
        ];

        foreach (IInterpretable result in results)
        {
            Interpretation interpretation = result.Interpret();

            Assert.False(string.IsNullOrWhiteSpace(interpretation.Title));
            Assert.False(string.IsNullOrWhiteSpace(interpretation.Summary));
            Assert.NotEmpty(interpretation.Metrics);
            Assert.NotEmpty(interpretation.Warnings);
        }
    }

    #endregion
}

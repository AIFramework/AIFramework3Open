using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class StackTests
{
    private const string VectorType = "AI.DataStructs.Algebraic.Vector";
    private const string ComplexType = "AI.DataStructs.WithComplexElements.ComplexVector";
    private const string PeaksType = "System.Collections.Generic.IReadOnlyList<AI.Signals.Peak>";

    private static readonly CodeUnit[] Units =
    [
        Type("T:AI.DataStructs.Algebraic.Vector", "public class Vector", "Вектор", "System.Collections.Generic.List<double>|System.Collections.Generic.IEnumerable<double>"),
        Type("T:AI.Signals.Kind", "public enum Kind", "Вид сигнала", ""),
        Method("M:AI.DSP.FFT.CalcFFT(AI.DataStructs.Algebraic.Vector)", "public static ComplexVector FFT.CalcFFT(Vector inp)",
            "Спектр сигнала: быстрое преобразование Фурье", ComplexType, VectorType),
        Method("M:AI.DataStructs.WithComplexElements.ComplexVector.Magnitude", "public Vector ComplexVector.Magnitude()",
            "Амплитуды комплексного спектра", VectorType, ""),
        Method("M:AI.Signals.PeakDetector.Detect(AI.DataStructs.Algebraic.Vector,System.Double,AI.Signals.Kind)",
            "public static IReadOnlyList<Peak> PeakDetector.Detect(Vector signal, double threshold, Kind kind)",
            "Поиск пиков в сигнале выше порога", PeaksType, $"{VectorType}|double|AI.Signals.Kind"),
        Method("M:AI.Text.Words.Split(System.String)", "public static string[] Words.Split(string text)",
            "Разбивает текст на слова", "string[]", "string"),
        Method("M:AI.DataStructs.Algebraic.Vector.op_Implicit(AI.DataStructs.Algebraic.Vector)~System.Double[]",
            "public static implicit operator double[](Vector vector)", "Вектор как массив", "double[]", VectorType, kind: "operator"),
    ];

    [Fact]
    public void DataInputsSkipKnobsAndIncludeReceiver()
    {
        var graph = new TypeGraph(Units);

        Assert.Equal([VectorType], graph.DataInputs(Units[4]));
        Assert.Equal([ComplexType], graph.DataInputs(Units[3]));
        Assert.Null(graph.DataOutput(Units[5] with { Returns = "double" }));
        Assert.False(graph.IsData("AI.Script.Binding.IScriptContext"));
    }

    [Fact]
    public void LinkUsesSupertypesAndConversions()
    {
        var graph = new TypeGraph(Units);

        Assert.Equal(0, graph.Link(VectorType, "System.Collections.Generic.IEnumerable<double>").Distance);
        (int distance, CodeUnit? via) = graph.Link(VectorType, "double[]");
        Assert.Equal(1, distance);
        Assert.Contains("op_Implicit", via!.Id);
        Assert.Equal(-1, graph.Link("string[]", VectorType).Distance);
        Assert.Equal(0, graph.Link("AI.Script.Runtime.ScriptValue", VectorType).Distance);
    }

    [Fact]
    public void PathsGoThroughMethodsBetweenTypes()
    {
        var graph = new TypeGraph(Units);

        IReadOnlyList<CodeUnit> path = Assert.Single(graph.Paths(VectorType, PeaksType, k: 1));

        Assert.Equal("M:AI.Signals.PeakDetector.Detect(AI.DataStructs.Algebraic.Vector,System.Double,AI.Signals.Kind)", Assert.Single(path).Id);
        Assert.Equal(PeaksType, graph.Resolve("Peak"));
    }

    [Theory]
    [InlineData("сгенерировать сигнал, затем посчитать спектр и потом найти пики", new[] { "сгенерировать сигнал", "посчитать спектр и", "найти пики" })]
    [InlineData("1. загрузить таблицу\n2. сгруппировать по городу", new[] { "загрузить таблицу", "сгруппировать по городу" })]
    [InlineData("спектр → пики; отчёт", new[] { "спектр", "пики", "отчёт" })]
    [InlineData("медиана чисел", new[] { "медиана чисел" })]
    public void TaskIsSplitIntoSteps(string task, string[] expected) =>
        Assert.Equal(expected, TaskSteps.Split(task));

    [Fact]
    public async Task StackChainsCompatibleStepsAndDeclaresGaps()
    {
        var builder = new StackBuilder(new ApiSearch(Units), new TypeGraph(Units), new Dictionary<((string, string), (string, string)), int>());

        StackPlan plan = (await builder.BuildAsync(["спектр сигнала", "поиск пиков", "перевести на китайский"]))[0];

        Assert.Equal("M:AI.DSP.FFT.CalcFFT(AI.DataStructs.Algebraic.Vector)", plan.Steps[0].Hit!.Unit.Id);
        Assert.Contains("PeakDetector.Detect", plan.Steps[1].Hit!.Unit.Id);
        Assert.StartsWith("тот же вход", plan.Steps[1].Link);
        Assert.Null(plan.Steps[2].Hit);
        Assert.Single(plan.Gaps);
    }

    [Fact]
    public async Task FlowsFromRealCodePreferTheUsualNextStep()
    {
        CodeUnit fft = Units[2];
        CodeUnit magnitude = Units[3];
        CodeUnit rival = Method("M:AI.DSP.Phase.Of(AI.DataStructs.WithComplexElements.ComplexVector)",
            "public static Vector Phase.Of(ComplexVector spectrum)", "Амплитуды и фазы комплексного спектра", VectorType, ComplexType);
        CodeUnit[] units = [.. Units, rival];
        var flows = new Dictionary<((string, string), (string, string)), int> { [((fft.Project, fft.Id), (magnitude.Project, magnitude.Id))] = 50 };

        var builder = new StackBuilder(new ApiSearch(units), new TypeGraph(units), flows);
        StackPlan plan = (await builder.BuildAsync(["спектр сигнала", "амплитуды комплексного спектра"]))[0];

        Assert.Equal(magnitude.Id, plan.Steps[1].Hit!.Unit.Id);
    }

    [Fact]
    public void CSharpSkeletonCompilesWhenTypesChain()
    {
        CodeUnit create = Method("M:System.Text.StringBuilder.#ctor(System.String)", "public StringBuilder(string value)",
            "", "System.Text.StringBuilder", "string", kind: "ctor");
        CodeUnit append = Method("M:System.Text.StringBuilder.Append(System.String)", "public StringBuilder StringBuilder.Append(string value)",
            "", "System.Text.StringBuilder", "string");

        CheckOutcome check = StackCheck.CompileCSharp(Plan(create, append), new TypeGraph([create, append]), Path.GetTempPath());

        Assert.True(check.Passed, check.Details);
        Assert.Contains("s1.Append(", check.Details);
    }

    [Fact]
    public void CSharpSkeletonFailsWhenTypesDoNotChain()
    {
        CodeUnit create = Method("M:System.Text.StringBuilder.#ctor(System.String)", "public StringBuilder(string value)",
            "", "System.Text.StringBuilder", "string", kind: "ctor");
        // Индекс утверждает, что Math.Abs принимает StringBuilder: компилятор это опровергнет.
        CodeUnit abs = Method("M:System.Math.Abs(System.Double)", "public static double Math.Abs(double value)",
            "", "double", "System.Text.StringBuilder");

        CheckOutcome check = StackCheck.CompileCSharp(Plan(create, abs), new TypeGraph([create, abs]), Path.GetTempPath());

        Assert.False(check.Passed);
    }

    [Fact]
    public void ScriptStackPassesChecker()
    {
        CodeUnit sine = Method("M:X.Sine", "public static Vector Sine()", "", VectorType, "") with { Script = "signal.sine" };
        CodeUnit fft = Method("M:X.Fft", "public static ScriptRecord Fft()", "", "AI.Script.Runtime.ScriptRecord", "") with { Script = "dsp.fft" };

        CheckOutcome check = StackCheck.CheckScript(Plan(sine, fft), StackReport.CreateScriptHost());

        Assert.True(check.Passed, check.Details);
        Assert.Contains("dsp.fft(x1", check.Details);
    }

    private static StackPlan Plan(params CodeUnit[] units) =>
        new([.. units.Select(unit => new StackStep("шаг", new SearchHit(unit, 1, 1, null), "", 1))], 1);

    private static CodeUnit Type(string id, string signature, string summary, string supertypes) =>
        new("AI", id, "type", "public", "src/x.cs", 1, signature, $"<summary>{summary}</summary>", "", null, id, Supertypes: supertypes);

    private static CodeUnit Method(string id, string signature, string summary, string returns, string inputs, string kind = "method") =>
        new("AI", id, kind, "public", "src/x.cs", 1, signature, summary.Length > 0 ? $"<summary>{summary}</summary>" : "", "", null, id,
            Returns: returns, Inputs: inputs);
}

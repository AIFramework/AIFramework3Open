using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class LoopTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-loop-").FullName;

    [Fact]
    public void RepeatedAuditWithoutChangesIsEmpty()
    {
        var copy = new AuditEntry("AI|M:A.F", "AI|M:B.F", "копия", "поведенческий дубль");
        var subject = new AuditEntry("AI|M:C.G", "AI|M:D.G", "один предмет, разный код", "разные функции");
        Dictionary<string, AuditEntry> before = Snapshot(copy, subject);

        Assert.True(Audit.Compare(before, Snapshot(subject, copy with { A = copy.B, B = copy.A })).IsEmpty);

        var added = new AuditEntry("AI|M:E.H", "AI|M:F.H", "копия", "");
        AuditDiff diff = Audit.Compare(before, Snapshot(copy with { Outcome = "расхождение" }, added));

        Assert.Equal([added], diff.Added);
        Assert.Equal("расхождение", Assert.Single(diff.Changed).Now.Outcome);
        Assert.Equal([subject], diff.Gone);
    }

    [Fact]
    public void CalibrationBeatsRuleOnHeldOutPairs()
    {
        // Одинаково ведут себя пары с похожим кодом и тем же именем; «копия» по правилу — лишь часть из них.
        var random = new Random(3);
        CalibrationExample[] examples = [.. Enumerable.Range(0, 400).Select(i =>
        {
            double code = random.NextDouble(), name = random.Next(2);
            bool same = code * 0.7 + name * 0.6 + random.NextDouble() * 0.2 > 0.8;
            string verdict = code >= 0.8 ? "копия" : "один предмет, разный код";
            return new CalibrationExample($"pair-{i}", new DupFeatures(code, random.NextDouble(), name, 0, 0, 1, i % 2 == 0, false), verdict, same);
        })];

        Calibration calibration = Calibration.Train(examples);

        Assert.True(calibration.ModelAccuracy > calibration.RuleAccuracy);
        Assert.True(calibration.ModelAccuracy > calibration.MajorityAccuracy);
        Assert.True(calibration.ModelAccuracy >= 0.85);

        string path = Path.Combine(_folder, "calibration.json");
        calibration.Save(path);
        Assert.Equal(calibration.Weights, Calibration.Load(path)!.Weights);
    }

    [Fact]
    public void CalibrationNeedsBothClasses()
    {
        CalibrationExample[] same = [.. Enumerable.Range(0, 30).Select(i => new CalibrationExample($"p{i}", new DupFeatures(1, 0, 1, 0, 0, 1, true, false), "копия", true))];

        Assert.Throws<InvalidOperationException>(() => Calibration.Train(same));
    }

    [Fact]
    public async Task SelectedUnitRisesInSearch()
    {
        CodeUnit first = Unit("M:Lib.A.Total(System.Double[])"), second = Unit("M:Lib.B.Total(System.Double[])");
        var search = new ApiSearch([first, second]);

        Assert.Equal(first.Id, (await search.SearchAsync("сумма элементов ряда")).First().Unit.Id);

        var journal = new SelectionJournal(Path.Combine(_folder, "selections.json"));
        search.UseSelections(journal.Counts);
        journal.Record(second);

        Assert.Equal(second.Id, (await search.SearchAsync("сумма элементов ряда")).First().Unit.Id);
        Assert.Equal(1, new SelectionJournal(Path.Combine(_folder, "selections.json")).Counts[second.Id]);
    }

    [Theory]
    [InlineData("""{"tool_input":{"file_path":"C:\\nowhere\\readme.md"}}""")]
    [InlineData("""{"tool_input":{}}""")]
    [InlineData("не JSON")]
    public void HookStaysSilentWhenThereIsNothingToSay(string payload)
    {
        var output = new StringWriter();

        Assert.Equal(0, EditHook.Run(new StringReader(payload), output));
        Assert.Equal("", output.ToString());
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Dictionary<string, AuditEntry> Snapshot(params AuditEntry[] entries) => entries.ToDictionary(entry => entry.Key);

    private static CodeUnit Unit(string id) =>
        new("Lib", id, "method", "public", "src/x.cs", 1, $"public static double {id[2..id.IndexOf('(')]}(double[] values)",
            "<summary>Сумма элементов ряда.</summary>", "{ return 0; }", null, id);
}

using Microsoft.Data.Sqlite;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class UnitStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-store-").FullName;

    private string DbPath => Path.Combine(_folder, "sub", "index.db");

    [Fact]
    public void ReindexWithoutChangesChangesNothing()
    {
        using var store = new UnitStore(DbPath);
        CodeUnit[] units = [Unit("M:A.F", "a.cs", 10, "h1"), Unit("M:A.G", "a.cs", 20, "h2")];

        Assert.Equal(2, store.ReplaceFile(File("a.cs"), units, []));
        Assert.Equal(0, store.ReplaceFile(File("a.cs"), units, []));
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void CountsOnlyChangedAndRemovedUnits()
    {
        using var store = new UnitStore(DbPath);
        store.ReplaceFile(File("a.cs"), [Unit("M:A.F", "a.cs", 10, "h1"), Unit("M:A.G", "a.cs", 20, "h2")], []);

        int changed = store.ReplaceFile(File("a.cs"), [Unit("M:A.F", "a.cs", 10, "h1-new")], []);

        Assert.Equal(2, changed);
        Assert.Equal("h1-new", Assert.Single(store.Units()).Hash);
    }

    [Fact]
    public void ShiftedLineIsUpdatedButNotCountedAsChange()
    {
        using var store = new UnitStore(DbPath);
        store.ReplaceFile(File("a.cs"), [Unit("M:A.F", "a.cs", 10, "h1")], []);

        Assert.Equal(0, store.ReplaceFile(File("a.cs"), [Unit("M:A.F", "a.cs", 12, "h1")], []));
        Assert.Equal(12, Assert.Single(store.Units()).Line);
    }

    [Fact]
    public void MethodMovedToAnotherFileSurvivesReindexOfOldFile()
    {
        using var store = new UnitStore(DbPath);
        store.ReplaceFile(File("a.cs"), [Unit("M:A.F", "a.cs", 10, "h1")], []);

        store.ReplaceFile(File("b.cs"), [Unit("M:A.F", "b.cs", 5, "h1")], []);
        store.ReplaceFile(File("a.cs"), [], []);

        Assert.Equal("b.cs", Assert.Single(store.Units()).File);
    }

    [Fact]
    public void SameIdInDifferentAssembliesAreDifferentUnits()
    {
        using var store = new UnitStore(DbPath);

        store.ReplaceFile(File("a/Program.cs"), [Unit("M:Program.Main", "a/Program.cs", 1, "h1", "DemoA")], []);
        store.ReplaceFile(File("b/Program.cs"), [Unit("M:Program.Main", "b/Program.cs", 1, "h2", "DemoB")], []);

        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void FileHashDecidesWhetherToParse()
    {
        using var store = new UnitStore(DbPath);
        store.ReplaceFile(File("a.cs", "text-1"), [], []);

        Assert.True(store.IsCurrent("a.cs", "text-1"));
        Assert.False(store.IsCurrent("a.cs", "text-2"));
        Assert.False(store.IsCurrent("b.cs", "text-1"));
    }

    [Fact]
    public void CallersCarryAreaAndTutorialName()
    {
        using var store = new UnitStore(DbPath);
        CodeUnit target = Unit("M:Lib.Fft", "src/Fft.cs", 3, "h1");
        CodeUnit test = Unit("M:Tests.FftTest", "Tests/FftTest.cs", 7, "h2", "Tests");
        CodeUnit snippet = Unit("M:Snip.Run", "Tools/SnippetCheck/S_Dsp_fft.g.cs", 1, "h3", "SnippetCheck");

        store.ReplaceFile(File("src/Fft.cs"), [target], []);
        store.ReplaceFile(File("Tests/FftTest.cs", area: "test"), [test], [Call(test, target, 2)]);
        store.ReplaceFile(new SourceFile("Tools/SnippetCheck/S_Dsp_fft.g.cs", "x", "tutorial", "Docs/Tutorials/DSP/fft.md"), [snippet], [Call(snippet, target, 1)]);

        IReadOnlyList<Caller> callers = store.Callers(target);

        Assert.Equal(["test", "tutorial"], callers.Select(c => c.Area).Order());
        Assert.Equal("Docs/Tutorials/DSP/fft.md", callers.Single(c => c.Area == "tutorial").ShownAs);
        Assert.Equal(2, callers.Single(c => c.Area == "test").Count);
    }

    [Fact]
    public void CalleesOutsideIndexAreKeptWithoutUnit()
    {
        using var store = new UnitStore(DbPath);
        CodeUnit caller = Unit("M:Lib.Norm", "src/Norm.cs", 3, "h1");

        store.ReplaceFile(File("src/Norm.cs"), [caller], [new CodeCall("AI", caller.Id, "System.Runtime", "M:System.Math.Sqrt(System.Double)", 1)]);

        Callee callee = Assert.Single(store.Callees(caller));
        Assert.Null(callee.Unit);
        Assert.Equal("System.Runtime", callee.Project);
    }

    [Fact]
    public void RemovingFileDropsItsUnitsAndCalls()
    {
        using var store = new UnitStore(DbPath);
        CodeUnit a = Unit("M:A.F", "a.cs", 1, "h1");
        CodeUnit b = Unit("M:B.G", "b.cs", 1, "h2");

        store.ReplaceFile(File("a.cs"), [a], []);
        store.ReplaceFile(File("b.cs"), [b], [Call(b, a, 1)]);

        Assert.Equal(1, store.RemoveFilesExcept(new HashSet<string> { "a.cs" }));
        Assert.Equal(1, store.Count);
        Assert.Empty(store.Callers(a));
    }

    [Fact]
    public void FindPrefersScriptNameAndLibrary()
    {
        using var store = new UnitStore(DbPath);
        store.ReplaceFile(File("Tests/T.cs", area: "test"), [Unit("M:Tests.FftCheck", "Tests/T.cs", 1, "h1", "Tests")], []);
        store.ReplaceFile(File("src/Dsp.cs"), [Unit("M:AI.Script.Std.DspModule.Fft", "src/Dsp.cs", 1, "h2", script: "dsp.fft")], []);

        Assert.Equal("dsp.fft", store.Find("dsp.fft")[0].Script);
        Assert.Equal("M:AI.Script.Std.DspModule.Fft", store.Find("Fft")[0].Id);
    }

    [Fact]
    public void DataSurvivesReopen()
    {
        using (var store = new UnitStore(DbPath))
            store.ReplaceFile(File("a.cs"), [Unit("M:A.F", "a.cs", 10, "h1")], []);

        using var reopened = new UnitStore(DbPath);

        Assert.Equal(Unit("M:A.F", "a.cs", 10, "h1"), Assert.Single(reopened.Units()));
    }

    public void Dispose()
    {
        // Пул соединений держит файл открытым и после Dispose: без очистки папку не удалить.
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private static SourceFile File(string path, string hash = "x", string area = "library") => new(path, hash, area, null);

    private static CodeCall Call(CodeUnit from, CodeUnit to, int count) => new(from.Project, from.Id, to.Project, to.Id, count);

    private static CodeUnit Unit(string id, string file, int line, string hash, string project = "AI", string? script = null) =>
        new(project, id, "method", "public", file, line, "void F()", "<summary>Описание.</summary>", "{ }", script, hash);
}

using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Сохранение файлов: единый <c>io.save</c> и файл как артефакт прогона.
/// </summary>
/// <remarks>
/// Скрипт кладёт файл в рабочую папку, а хост обязан узнать о нём, не разглядывая папку сам:
/// иначе готовый отчёт остаётся внутри песочницы и до пользователя не доходит. Поэтому файл
/// объявляется артефактом с медиатипом.
/// </remarks>
public sealed class SaveTests : IDisposable
{
    private readonly string _root;

    public SaveTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aiscript-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки не должна ронять тест.
        }
    }

    private RunOptions Options() => new() { Sandbox = new WorkspaceSandbox(_root) };

    private RunResult Run(string source) => Script.RunWith(Script.FullHost(), source, Options());

    private static ScriptArtifact File(RunResult result, string title)
    {
        foreach (ScriptArtifact artifact in result.Artifacts)
        {
            if (artifact.Kind == "file" && artifact.Title == title) return artifact;
        }

        Assert.Fail($"артефакта-файла '{title}' нет: {string.Join(", ", result.Artifacts.Select(a => $"{a.Kind} {a.Title}"))}");
        throw new InvalidOperationException();
    }

    /// <summary>Чем писать, решает расширение: сам <c>io</c> про форматы не знает.</summary>
    [Fact]
    public void Save_PicksWriterByExtension()
    {
        RunResult result = Run("""
            let t = table.of({ a: <1, 2>, b: ["x", "y"] })

            emit csv = io.save(t, "итог.csv")
            emit json = io.save({ a: 1 }, "итог.json")
            """);

        Assert.True(result.Success, Script.Report(result));
        Assert.True(System.IO.File.Exists(Path.Combine(_root, "итог.csv")));
        Assert.True(System.IO.File.Exists(Path.Combine(_root, "итог.json")));
    }

    [Fact]
    public void Save_RegistersFileArtifactWithMediaType()
    {
        RunResult result = Run("emit r = io.save(table.of({ a: <1> }), \"отчёт.csv\")");

        Assert.True(result.Success, Script.Report(result));

        ScriptArtifact artifact = File(result, "отчёт.csv");

        Assert.Equal("text/csv", artifact.MediaType);
        Assert.IsType<ScriptFileInfo>(artifact.Value);
    }

    [Fact]
    public void Save_UnknownExtension_ListsWhatIsWritten()
    {
        RunResult result = Run("emit r = io.save(table.of({ a: <1> }), \"итог.dbf\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.BadFileFormat, result.Error!.Code);
        Assert.Contains("csv", result.Error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_WithoutExtension_SaysSo()
    {
        RunResult result = Run("emit r = io.save(table.of({ a: <1> }), \"итог\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.BadFileFormat, result.Error!.Code);
    }

    /// <summary>Сохранённое читается обратно тем же языком — круг замыкается.</summary>
    [Fact]
    public void Save_ThenLoad_KeepsData()
    {
        RunResult result = Run("""
            let t = table.of({ клиент: ["а", "б"], сумма: <10, 20> })

            io.save(t, "итог.csv")

            emit r = vec.sum(io.load("итог.csv")["сумма"])
            """);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(30.0, result.Emitted["r"]);
    }

    /// <summary>Запись в файл поверх песочницы только для чтения обязана отказать.</summary>
    [Fact]
    public void Save_ReadOnlySandbox_IsDenied()
    {
        var options = new RunOptions { Sandbox = new WorkspaceSandbox(_root, readOnly: true) };

        RunResult result = Script.RunWith(
            Script.FullHost(), "emit r = io.save(table.of({ a: <1> }), \"итог.csv\")", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.SandboxDenied, result.Error!.Code);
    }
}

using AI.Script.Hosting;
using AI.Script.Semantics;
using System.Text.RegularExpressions;

namespace AI.Script.UnitTests;

/// <summary>
/// Функции <c>io</c> и <c>cv</c> над хранилищем в памяти.
/// </summary>
/// <remarks>
/// Модули обязаны работать над любым хранилищем, а не только над папкой на диске: хост отдаёт
/// файлы пользователя откуда угодно. <see cref="IoTests"/> держит обещание на диске, этот
/// набор — в памяти, и последний тест следит, чтобы в обход хранилища никто не ходил.
/// </remarks>
public sealed partial class StorageTests
{
    [Fact]
    public void ReadCsv_FromMemory()
    {
        var store = new MemorySandbox().Put("sales.csv", "client,amount\na,10\nb,20.5\n");

        RunResult result = Script.RunOk("emit sum = vec.sum(io.read_csv(\"sales.csv\")[\"amount\"])", Over(store));

        Assert.Equal(30.5, result.Emitted["sum"]);
    }

    [Fact]
    public void WriteThenRead_StaysInMemory()
    {
        var store = new MemorySandbox();

        const string source = """
            let t = table.of({ a: <1, 2>, note: ["x", "y"] })
            let path = t |> io.write_csv("out/t.csv")
            emit rows = len(io.read_csv(path))
            emit text = io.write_text("привет", "notes.md") |> io.read_text()
            """;

        RunResult result = Script.RunOk(source, Over(store));

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal("привет", result.Emitted["text"]);
        Assert.NotNull(store.Get("out/t.csv"));
    }

    [Fact]
    public void Files_ListsKindSizeAndMediaType()
    {
        var store = new MemorySandbox()
            .Put("a.csv", "x\n1\n")
            .Put("b.png", [1, 2, 3])
            .Put("docs/c.txt", "z");

        const string source = """
            let files = io.files()
            let images = files |> table.filter(row => row.kind == "image")
            emit count = len(files)
            emit png = images[0].media_type
            emit nested = len(io.files(dir: "docs"))
            emit size = io.info("b.png").size
            """;

        RunResult result = Script.RunOk(source, Over(store));

        Assert.Equal(2.0, result.Emitted["count"]);
        Assert.Equal("image/png", result.Emitted["png"]);
        Assert.Equal(1.0, result.Emitted["nested"]);
        Assert.Equal(3.0, result.Emitted["size"]);
    }

    [Fact]
    public void Load_OpensByKind()
    {
        var store = new MemorySandbox()
            .Put("t.csv", "x\n1\n2\n")
            .Put("cfg.json", "{ \"temp\": 0.2 }")
            .Put("notes.md", "# заголовок");

        const string source = """
            emit rows = len(io.load("t.csv"))
            emit temp = io.load("cfg.json").temp
            emit title = io.load("notes.md")
            """;

        RunResult result = Script.RunOk(source, Over(store));

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(0.2, result.Emitted["temp"]);
        Assert.Equal("# заголовок", result.Emitted["title"]);
    }

    [Fact]
    public void Load_UnknownFormat_NamesWhatOpens()
    {
        var store = new MemorySandbox().Put("a.xyz", "?");

        Diagnostic error = Script.FailsWith("emit r = io.load(\"a.xyz\")", Over(store));

        Assert.Equal(DiagnosticCodes.BadFileFormat, error.Code);
        Assert.Contains("csv", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Картинку открывает модуль <c>cv</c>, хотя <c>io</c> о нём ничего не знает.
    /// </summary>
    [Fact]
    public void Load_Image_UsesReaderOfVisionModule()
    {
        var store = new MemorySandbox();

        const string source = """
            let path = cv.save(mat.zeros(4, cols: 6), "a.png")
            let img = io.load(path)
            emit rows = mat.rows(cv.channel(img, "red"))
            """;

        RunResult result = Script.RunWith(Script.FullHost(), source, Over(store));

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(4.0, result.Emitted["rows"]);
        Assert.NotNull(store.Get("a.png"));
    }

    [Fact]
    public void ReadOnlyMemory_DeniesScriptWrite()
    {
        var store = new MemorySandbox(readOnly: true);

        Assert.Equal(DiagnosticCodes.SandboxDenied, Script.FailsWith("emit r = io.write_text(\"x\", \"a.txt\")", Over(store)).Code);
        Assert.Null(store.Get("a.txt"));
    }

    [Fact]
    public void Escape_IsDenied_OverMemory()
    {
        var store = new MemorySandbox().Put("a.txt", "x");

        Assert.Equal(DiagnosticCodes.SandboxDenied, Script.FailsWith("emit r = io.read_text(\"../a.txt\")", Over(store)).Code);
    }

    [Fact]
    public void Workdir_NarrowsAnyStore()
    {
        var store = new MemorySandbox().Put("nested/in.txt", "есть");

        const string source = """
            options { workdir: "nested" }
            emit seen = io.read_text("in.txt")
            emit path = io.write_text("x", "out.txt")
            """;

        RunResult result = Script.RunOk(source, Over(store));

        Assert.Equal("есть", result.Emitted["seen"]);
        Assert.NotNull(store.Get("nested/out.txt"));
    }

    [Fact]
    public void MissingFile_PointsToListing()
    {
        Diagnostic error = Script.FailsWith("emit r = io.read_text(\"nope.txt\")", Over(new MemorySandbox()));

        Assert.Equal(DiagnosticCodes.FileNotFound, error.Code);
        Assert.Contains("io.files", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Модули не ходят к файловой системе в обход хранилища.
    /// </summary>
    /// <remarks>
    /// Один прямой <c>File.ReadAllText</c> в модуле — и функция работает только на диске, а
    /// песочница в ней держится на честном слове. Проверяется текст модулей, потому что такой
    /// вызов не виден ни типам, ни тестам на диске: там он просто работает.
    /// </remarks>
    [Fact]
    public void Modules_DoNotTouchFileSystemDirectly()
    {
        var offenders = new List<string>();

        foreach (string folder in Directory.EnumerateDirectories(Path.Combine(RepositoryRoot(), "src"), "AI.Script*"))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*Module.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);

                foreach (Match match in DirectAccess().Matches(text)) offenders.Add($"{Path.GetFileName(file)}: {match.Value}");
            }
        }

        Assert.True(offenders.Count == 0, "обращения к диску в обход хранилища:\n  " + string.Join("\n  ", offenders));
    }

    private static RunOptions Over(IScriptSandbox store) => new() { Sandbox = store };

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AI.Script")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("корень репозитория не найден");
    }

    [GeneratedRegex(@"\b(File|Directory)\s*\.\s*[A-Z]\w*|\bnew\s+(FileInfo|FileStream|DirectoryInfo)\b")]
    private static partial Regex DirectAccess();
}

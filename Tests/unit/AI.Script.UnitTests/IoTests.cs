using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Файловый ввод-вывод и песочница.
/// </summary>
/// <remarks>
/// Исполнение чужого кода — штатный сценарий языка, поэтому проверяется не только «читает
/// файл», но и «не читает то, что не должен».
/// </remarks>
public sealed class IoTests : IDisposable
{
    private readonly string _root;

    public IoTests()
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

    private RunOptions Options(bool readOnly = false) => new()
    {
        Sandbox = new WorkspaceSandbox(_root, readOnly),
    };

    private void Write(string name, string content) =>
        File.WriteAllText(Path.Combine(_root, name), content);

    [Fact]
    public void Io_IsDeniedByDefault()
    {
        RunResult result = Script.Run("emit r = io.read_text(\"a.txt\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.SandboxDenied, result.Error!.Code);
        Assert.Contains("Sandbox", result.Error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Io_ReadCsv_InfersColumnTypes()
    {
        Write("sales.csv", "client,amount\na,10\nb,20.5\n");

        const string source = """
            let t = io.read_csv("sales.csv")
            emit rows = len(t)
            emit sum = vec.sum(t["amount"])
            emit clientType = type(t["client"])
            emit amountType = type(t["amount"])
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(30.5, result.Emitted["sum"]);
        Assert.Equal("list", result.Emitted["clientType"]);
        Assert.Equal("vec", result.Emitted["amountType"]);
    }

    [Fact]
    public void Io_ReadCsv_DetectsSemicolonSeparator()
    {
        Write("ru.csv", "a;b\n1;2\n");

        RunResult result = Script.RunOk("emit r = len(table.columns(io.read_csv(\"ru.csv\")))", Options());

        Assert.Equal(2.0, result.Emitted["r"]);
    }

    [Fact]
    public void Io_ReadCsv_HandlesQuotedFields()
    {
        Write("q.csv", "name,note\n\"Иванов, И.\",\"строка с \"\"кавычками\"\"\"\n");

        const string source = """
            let t = io.read_csv("q.csv")
            emit name = t[0].name
            emit note = t[0].note
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal("Иванов, И.", result.Emitted["name"]);
        Assert.Equal("строка с \"кавычками\"", result.Emitted["note"]);
    }

    [Fact]
    public void Io_ReadCsv_EmptyCellBecomesMissing()
    {
        Write("gaps.csv", "x\n1\n\n3\n");

        RunResult result = Script.RunOk("emit r = len(io.read_csv(\"gaps.csv\") |> table.drop_na())", Options());

        Assert.Equal(2.0, result.Emitted["r"]);
    }

    [Fact]
    public void Io_ReadCsv_TextAmongNumbersKeepsColumnTextual()
    {
        // Одна строка «н/д» посреди чисел означает, что колонка не числовая: молча превращать
        // её в числа с пропусками нельзя, это меняет данные.
        Write("mixed.csv", "x\n1\nн/д\n3\n");

        RunResult result = Script.RunOk("emit r = type(io.read_csv(\"mixed.csv\")[\"x\"])", Options());

        Assert.Equal("list", result.Emitted["r"]);
    }

    [Fact]
    public void Io_ReadCsv_RowLengthMismatch_IsReported()
    {
        Write("bad.csv", "a,b\n1,2\n3\n");

        Diagnostic error = Script.FailsWith("emit r = io.read_csv(\"bad.csv\")", Options());

        Assert.Equal(DiagnosticCodes.BadFileFormat, error.Code);
        Assert.Contains("строке 3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Io_WriteCsv_RoundTrips()
    {
        const string source = """
            let t = table.of({ a: <1, 2>, note: ["x,y", "z"] })
            let path = t |> io.write_csv("out.csv")
            let back = io.read_csv(path)
            emit rows = len(back)
            emit note = back[0].note
            emit sum = vec.sum(back["a"])
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal("x,y", result.Emitted["note"]);
        Assert.Equal(3.0, result.Emitted["sum"]);
    }

    [Fact]
    public void Io_Json_RoundTrips()
    {
        const string source = """
            let cfg = { model: "x", temp: 0.2, tags: ["a", "b"], on: true, missing: none }
            let path = cfg |> io.write_json("cfg.json")
            let back = io.read_json(path)
            emit temp = back.temp
            emit tag = back.tags[1]
            emit on = back.on
            emit missing = type(back.missing)
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal(0.2, result.Emitted["temp"]);
        Assert.Equal("b", result.Emitted["tag"]);
        Assert.Equal(true, result.Emitted["on"]);
        Assert.Equal("none", result.Emitted["missing"]);
    }

    [Fact]
    public void Io_Json_MalformedFile_IsReported()
    {
        Write("broken.json", "{ \"a\": ");

        Assert.Equal(DiagnosticCodes.BadFileFormat, Script.FailsWith("emit r = io.read_json(\"broken.json\")", Options()).Code);
    }

    [Fact]
    public void Io_TextAndLines()
    {
        Write("notes.txt", "первая\nвторая\n");

        const string source = """
            emit text = str.trim(io.read_text("notes.txt"))
            emit lines = len(io.read_lines("notes.txt"))
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal("первая\nвторая", result.Emitted["text"]);
        Assert.Equal(3.0, result.Emitted["lines"]);
    }

    [Fact]
    public void Io_LsAndExists()
    {
        Write("a.csv", "x\n1\n");
        Write("b.txt", "z");

        const string source = """
            emit csv = len(io.ls(".", mask: "*.csv"))
            emit all = len(io.ls("."))
            emit has = io.exists("a.csv")
            emit hasNot = io.exists("nope.csv")
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal(1.0, result.Emitted["csv"]);
        Assert.Equal(2.0, result.Emitted["all"]);
        Assert.Equal(true, result.Emitted["has"]);
        Assert.Equal(false, result.Emitted["hasNot"]);
    }

    [Fact]
    public void Io_MissingFile_ListsWorkdir()
    {
        Diagnostic error = Script.FailsWith("emit r = io.read_text(\"nope.txt\")", Options());

        Assert.Equal(DiagnosticCodes.FileNotFound, error.Code);
        Assert.Contains("io.ls", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("sub/../../secret.txt")]
    [InlineData("./a/../../b.txt")]
    public void Io_EscapingWorkdir_IsDenied(string path)
    {
        Diagnostic error = Script.FailsWith($"emit r = io.read_text(\"{path}\")", Options());

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
    }

    [Fact]
    public void Io_AbsolutePath_IsDenied()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "x.txt").Replace("\\", "/", StringComparison.Ordinal);

        Assert.Equal(DiagnosticCodes.SandboxDenied, Script.FailsWith($"emit r = io.read_text(\"{absolute}\")", Options()).Code);
    }

    [Fact]
    public void Io_ReadOnlySandbox_DeniesWrite()
    {
        Write("a.txt", "x");

        RunResult read = Script.Run("emit r = io.read_text(\"a.txt\")", Options(readOnly: true));
        RunResult write = Script.Run("emit r = io.write_text(\"y\", \"b.txt\")", Options(readOnly: true));

        Assert.True(read.Success, Script.Report(read));
        Assert.False(write.Success);
        Assert.Equal(DiagnosticCodes.SandboxDenied, write.Error!.Code);
    }

    [Fact]
    public void Io_Workdir_OptionNarrowsSandbox()
    {
        const string source = """
            options { workdir: "nested" }
            let path = io.write_text("x", "a.txt")
            emit files = len(io.ls("."))
            """;

        RunResult result = Script.RunOk(source, Options());

        Assert.Equal(1.0, result.Emitted["files"]);
        Assert.True(File.Exists(Path.Combine(_root, "nested", "a.txt")));
    }

    [Fact]
    public void Io_Workdir_CannotEscape()
    {
        RunResult result = Script.Run("options { workdir: \"../..\" }\nemit r = 1", Options());

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.SandboxDenied, result.Error!.Code);
    }

    [Fact]
    public void Io_WriteCreatesNestedDirectories()
    {
        RunResult result = Script.RunOk("emit r = io.write_text(\"x\", \"a/b/c.txt\")", Options());

        Assert.Equal("a/b/c.txt", result.Emitted["r"]);
        Assert.True(File.Exists(Path.Combine(_root, "a", "b", "c.txt")));
    }
}

using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Стадии конвейера: кэш, повторы, собственный таймаут, граф прогона и прогресс.
/// </summary>
/// <remarks>
/// Кэш проверяется по наблюдаемому следствию — тело стадии не выполнилось повторно, — а не по
/// внутреннему счётчику попаданий. Счётчик подтвердил бы, что кэш «сработал», даже если бы
/// стадия при этом всё равно считалась заново.
/// </remarks>
public sealed class StageTests
{
    private const string Counting = """
        @cache
        stage double(x: num) -> num {
            print("считаю ${x}")
            x * 2
        }
        """;

    [Fact]
    public void Stage_WithoutCache_RunsEveryTime()
    {
        RunResult result = Script.RunOk("""
            stage double(x: num) -> num {
                print("считаю ${x}")
                x * 2
            }

            emit a = double(2)
            emit b = double(2)
            """);

        Assert.Equal(4.0, result.Emitted["a"]);
        Assert.Equal(2, Counted(result));
    }

    /// <summary>Кэш в памяти прогона: второй вызов с теми же аргументами тело не выполняет.</summary>
    [Fact]
    public void Stage_Cache_SkipsSecondCallWithSameArguments()
    {
        RunResult result = Run($"{Counting}\nemit a = double(2)\nemit b = double(2)\nemit c = double(3)",
            new MemoryStageCache());

        Assert.Equal(4.0, result.Emitted["a"]);
        Assert.Equal(4.0, result.Emitted["b"]);
        Assert.Equal(6.0, result.Emitted["c"]);

        // Два разных аргумента — два счёта; повтор второго аргумента счёта не потребовал.
        Assert.Equal(2, Counted(result));
        Assert.Equal(3, result.Stats.Stages);
        Assert.Equal(1, result.Stats.CachedStages);
    }

    /// <summary>Признак готовности этапа: повторный прогон неизменённого скрипта не считает заново.</summary>
    [Fact]
    public void Stage_Cache_SurvivesBetweenRuns()
    {
        var cache = new MemoryStageCache();
        string source = $"{Counting}\nemit a = double(21)";

        RunResult first = Run(source, cache);
        RunResult second = Run(source, cache);

        Assert.Equal(42.0, first.Emitted["a"]);
        Assert.Equal(42.0, second.Emitted["a"]);

        Assert.Equal(1, Counted(first));
        Assert.Equal(0, Counted(second));
        Assert.Equal(1, second.Stats.CachedStages);
    }

    /// <summary>Правка тела обесценивает прежний результат: иначе кэш врал бы про новый код.</summary>
    [Fact]
    public void Stage_Cache_IsInvalidatedByEditedBody()
    {
        var cache = new MemoryStageCache();

        RunResult first = Run("@cache\nstage f(x: num) -> num { x * 2 }\nemit r = f(10)", cache);
        RunResult second = Run("@cache\nstage f(x: num) -> num { x * 3 }\nemit r = f(10)", cache);

        Assert.Equal(20.0, first.Emitted["r"]);
        Assert.Equal(30.0, second.Emitted["r"]);
    }

    [Fact]
    public void Stage_Cache_IsDisabledByOptions()
    {
        RunResult result = Run($"options {{ cache: \"off\" }}\n{Counting}\nemit a = double(2)\nemit b = double(2)",
            new MemoryStageCache());

        Assert.Equal(2, Counted(result));
        Assert.Equal(0, result.Stats.CachedStages);
    }

    /// <summary>
    /// Дескриптор в аргументах делает стадию некэшируемой — с указанием причины в графе, а не
    /// молча: молчание выглядело бы как работающий кэш, который почему-то не срабатывает.
    /// </summary>
    [Fact]
    public void Stage_HandleArgument_MakesStageNotCacheable()
    {
        RunResult result = Run("""
            @cache
            stage size(m: handle) -> num { 1 }

            let model = ml.kmeans(mat.eye(4), k: 2)

            emit a = size(model)
            emit b = size(model)
            """, new MemoryStageCache());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(0, result.Stats.CachedStages);

        StageNode node = result.Graph.Nodes[0];

        Assert.Null(node.Key);
        Assert.NotNull(node.NotCacheable);
        Assert.Contains("дескриптор", node.NotCacheable!, StringComparison.Ordinal);
    }

    // --- повторы ---

    /// <summary>
    /// Повтор проверяется на стадии, которая перестаёт падать сама.
    /// </summary>
    /// <remarks>
    /// Стадия не видит внешние имена, поэтому счётчик попыток живёт в файле рабочей папки —
    /// единственном состоянии, до которого она вправе дотянуться. Это же и делает проверку
    /// честной: считает попытки не тест, а сама стадия.
    /// </remarks>
    [Fact]
    public void Stage_Retry_RepeatsUntilSuccess()
    {
        string root = NewDirectory();

        try
        {
            RunResult result = Script.RunOk("""
                @retry(3)
                stage flaky(needed: num) -> num {
                    let seen = if io.exists("attempts.txt") { core.parse_num(io.read_text("attempts.txt")) } else { 0 }
                    let now = seen + 1

                    core.to_str(now) |> io.write_text("attempts.txt")

                    assert now >= needed, "попытка ${now} из ${needed}"

                    now
                }

                emit attempts = flaky(3)
                """, new RunOptions { Sandbox = new WorkspaceSandbox(root) });

            Assert.Equal(3.0, result.Emitted["attempts"]);
            Assert.Equal(3, result.Graph.Nodes[0].Attempts);
            Assert.Equal(StageOutcome.Computed, result.Graph.Nodes[0].Outcome);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void Stage_Retry_GivesUpAfterLastAttempt()
    {
        RunResult result = Script.Run("""
            @retry(2)
            stage always(x: num) -> num {
                assert false, "всегда падает"
                x
            }

            emit r = always(1)
            """);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.AssertionFailed, result.Error!.Code);
        Assert.Equal(2, result.Graph.Nodes[0].Attempts);
        Assert.Equal(StageOutcome.Failed, result.Graph.Nodes[0].Outcome);
    }

    /// <summary>Прерывание по лимиту не повторяется: превышенный потолок от повтора не исчезнет.</summary>
    [Fact]
    public void Stage_Retry_DoesNotRepeatLimitAbort()
    {
        RunResult result = Script.Run("""
            @retry(5)
            stage endless(n: num) -> num {
                let total = 0

                while true { set total = total + 1 }

                total
            }

            emit r = endless(1)
            """, new RunOptions { Limits = new ScriptLimits { Steps = 5000 } });

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.StepLimit, result.Error!.Code);
        Assert.Equal(1, result.Graph.Nodes[0].Attempts);
    }

    // --- обещания атрибутов ---

    /// <summary>
    /// Кэшируемая стадия, читающая файл, — ловушка: содержимого файла в ключе нет, и после
    /// правки файла вернётся прежний результат. Это предупреждение, а не ошибка: чтение файла
    /// стадией законно, если кэш ей не нужен.
    /// </summary>
    [Fact]
    public void Stage_CacheWithFileRead_IsWarned()
    {
        Diagnostic warning = Script.CheckDiagnostic("""
            @cache
            stage загрузить(имя: str) -> str { io.read_text(имя) }

            emit r = загрузить("a.txt")
            """, DiagnosticCodes.SandboxDenied);

        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("в ключ кэша не входит", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>А вот '@pure' — прямое обещание автора, и его нарушение уже ошибка.</summary>
    [Fact]
    public void Stage_PureWithFileRead_IsRejected()
    {
        Diagnostic error = Script.CheckFailsWith("""
            @pure
            stage загрузить(имя: str) -> str { io.read_text(имя) }

            emit r = загрузить("a.txt")
            """);

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
        Assert.Contains("@pure", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_FileReadWithoutCache_IsSilent()
    {
        CheckResult result = Script.Check("""
            stage загрузить(имя: str) -> str { io.read_text(имя) }

            emit r = загрузить("a.txt")
            """);

        Assert.True(result.Success, result.Render());
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Stage_Deprecated_WarnsAtCallSite()
    {
        Diagnostic warning = Script.CheckDiagnostic("""
            @deprecated("вместо неё используйте признаки2")
            stage признаки(x: num) -> num { x }

            emit r = признаки(1)
            """, DiagnosticCodes.NotImplementedYet);

        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("признаки2", warning.Hint, StringComparison.Ordinal);
    }

    // --- граф прогона ---

    [Fact]
    public void Graph_RecordsCallerOfNestedStage()
    {
        RunResult result = Script.RunOk("""
            stage inner(x: num) -> num { x + 1 }
            stage outer(x: num) -> num { inner(x) * 2 }

            emit r = outer(1)
            """);

        Assert.Equal(4.0, result.Emitted["r"]);
        Assert.Equal(2, result.Graph.Nodes.Count);

        StageNode top = result.Graph.Nodes[0];
        StageNode nested = result.Graph.Nodes[1];

        Assert.Equal("outer", top.Name);
        Assert.Null(top.Caller);

        Assert.Equal("inner", nested.Name);
        Assert.Equal(top.Id, nested.Caller);
    }

    [Fact]
    public void Graph_RendersMermaidWithEdges()
    {
        RunResult result = Script.RunOk("""
            stage inner(x: num) -> num { x + 1 }
            stage outer(x: num) -> num { inner(x) }

            emit r = outer(1)
            """);

        string mermaid = result.Graph.ToMermaid();

        Assert.StartsWith("graph TD", mermaid, StringComparison.Ordinal);
        Assert.Contains("n0 --> n1", mermaid, StringComparison.Ordinal);
        Assert.Contains("outer", mermaid, StringComparison.Ordinal);
    }

    [Fact]
    public void Graph_IsEmptyWithoutStages()
    {
        RunResult result = Script.RunOk("fn f(x: num) -> num { x }\nemit r = f(1)");

        Assert.True(result.Graph.IsEmpty);
        Assert.Equal(0, result.Stats.Stages);
    }

    [Fact]
    public void Graph_KeepsFailedStage()
    {
        RunResult result = Script.Run("stage f(x: num) -> num { assert false, \"нет\"\n x }\nemit r = f(1)");

        Assert.False(result.Success);
        Assert.Single(result.Graph.Nodes);
        Assert.Equal(StageOutcome.Failed, result.Graph.Nodes[0].Outcome);
        Assert.NotNull(result.Graph.Nodes[0].Error);
    }

    // --- прогресс ---

    [Fact]
    public void Progress_ReportsStartAndFinish()
    {
        var events = new List<string>();

        var options = new RunOptions
        {
            Progress = new DelegateProgressSink((stage, finished) =>
                events.Add($"{stage.Name}:{(finished ? "конец" : "начало")}")),
        };

        RunResult result = Script.Run("stage f(x: num) -> num { x }\nemit r = f(1)", options);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(["f:начало", "f:конец"], events);
    }

    // --- таймаут стадии ---

    [Fact]
    public void Stage_Timeout_StopsLongStage()
    {
        RunResult result = Script.Run("""
            @timeout(0.05s)
            stage slow(n: num) -> num {
                let total = 0

                for i in 0..n { set total = total + i }

                total
            }

            emit r = slow(50000000)
            """);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.Timeout, result.Error!.Code);
        Assert.Contains("slow", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_Timeout_DoesNotFireWhenStageIsFast()
    {
        RunResult result = Script.RunOk("@timeout(30s)\nstage fast(x: num) -> num { x }\nemit r = fast(1)");

        Assert.Equal(1.0, result.Emitted["r"]);
    }

    // --- кэш на диске ---

    [Fact]
    public void FileCache_SurvivesNewCacheObject()
    {
        string directory = NewDirectory();

        try
        {
            string source = $"{Counting}\nemit a = double(4)";

            RunResult first = Run(source, new FileStageCache(directory));
            RunResult second = Run(source, new FileStageCache(directory));

            Assert.Equal(8.0, first.Emitted["a"]);
            Assert.Equal(8.0, second.Emitted["a"]);

            Assert.Equal(1, Counted(first));
            Assert.Equal(0, Counted(second));
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void FileCache_RoundTripsTables()
    {
        string directory = NewDirectory();

        try
        {
            const string source = """
                @cache
                stage build(n: num) -> table {
                    table.of({ i: vec.arange(0, n), name: ["a", "b", "c"] })
                }

                let t = build(3)

                emit rows = len(t)
                emit name = t[1].name
                emit i = t[2].i
                """;

            RunResult first = Run(source, new FileStageCache(directory));
            RunResult second = Run(source, new FileStageCache(directory));

            Assert.Equal(3.0, second.Emitted["rows"]);
            Assert.Equal("b", second.Emitted["name"]);
            Assert.Equal(2.0, second.Emitted["i"]);
            Assert.Equal(first.Emitted["name"], second.Emitted["name"]);

            Assert.Equal(1, second.Stats.CachedStages);
        }
        finally
        {
            Delete(directory);
        }
    }

    private static string NewDirectory() =>
        Path.Combine(Path.GetTempPath(), "aisc-tests", Guid.NewGuid().ToString("N"));

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static RunResult Run(string source, IStageCache cache)
    {
        RunResult result = Script.Run(source, new RunOptions { Cache = cache });

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    private static int Counted(RunResult result)
    {
        int count = 0;

        foreach (string line in result.Transcript)
        {
            if (line.StartsWith("считаю", StringComparison.Ordinal)) count++;
        }

        return count;
    }
}

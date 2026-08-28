using AI.Script.Charts;
using AI.Script.Llm;
using AI.Script.Docs;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;
using AI.Script.Syntax;
using System.Globalization;
using System.Text;

namespace AiFramework.Tools.Aisc;

/// <summary>
/// Утилита командной строки AIScript: проверка, запуск и документация.
/// </summary>
/// <remarks>
/// Коды возврата отличают три исхода: 0 — успех, 1 — скрипт не прошёл проверку либо сорвался,
/// 2 — неверно вызвана сама утилита. Без этого различия любой вызывающий скрипт сборки
/// вынужден разбирать текст, чтобы понять, чья ошибка.
/// </remarks>
internal static class Program
{
    private const int Ok = 0;
    private const int ScriptFailed = 1;
    private const int BadUsage = 2;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var line = CommandLine.Parse(args);

        if (line.Command.Length == 0 || line.Has("help") || line.Command is "help" && line.Arguments.Count == 0)
        {
            PrintUsage();
            return line.Command.Length == 0 ? BadUsage : Ok;
        }

        try
        {
            return line.Command switch
            {
                "check" => Check(line),
                "run" => await Run(line).ConfigureAwait(false),
                "docs" => Docs(line),
                "help" => Help(line),
                _ => Unknown(line.Command),
            };
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"aisc: ошибка ввода-вывода — {exception.Message}");
            return BadUsage;
        }
    }

    // --- команды ---

    private static int Check(CommandLine line)
    {
        if (!TryReadScript(line, out string path, out string source)) return BadUsage;

        CheckResult result = CreateHost(line).Check(source, Path.GetFileName(path));

        if (line.Has("json"))
        {
            Console.WriteLine(DiagnosticsJson(result.Diagnostics, result.Success));
            return result.Success ? Ok : ScriptFailed;
        }

        Console.WriteLine(result.Render());

        return result.Success ? Ok : ScriptFailed;
    }

    private static async Task<int> Run(CommandLine line)
    {
        if (!TryReadScript(line, out string path, out string source)) return BadUsage;

        ScriptHost host = CreateHost(line);
        RunOptions options = RunOptionsFrom(line, path);

        RunResult result = await host.RunAsync(source, options).ConfigureAwait(false);

        if (line.Has("json"))
        {
            Console.WriteLine(RunJson(result));
            return result.Success ? Ok : ScriptFailed;
        }

        foreach (string transcript in result.Transcript) Console.WriteLine(transcript);

        if (result.Emitted.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Результаты:");

            foreach (var pair in result.Emitted)
                Console.WriteLine($"  {pair.Key} = {ScriptFormatter.Format(Marshalled(pair.Value))}");
        }

        foreach (Diagnostic diagnostic in result.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Info && !line.Has("verbose")) continue;

            Console.Error.WriteLine();
            Console.Error.WriteLine(diagnostic.Render());
        }

        if (line.Has("graph") && !result.Graph.IsEmpty)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(line.Value("graph") == "mermaid"
                ? result.Graph.ToMermaid()
                : result.Graph.Render());
        }

        if (line.Has("stats"))
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(result.Stats.ToString());
        }

        return result.Success ? Ok : ScriptFailed;
    }

    private static int Docs(CommandLine line)
    {
        ScriptHost host = CreateHost(line);

        var options = new ManifestOptions
        {
            Format = line.Has("json") ? ManifestFormat.Json
                : line.Has("compact") ? ManifestFormat.Compact
                : ManifestFormat.Markdown,
            IndexOnly = line.Has("index"),
            IncludeExamples = !line.Has("no-examples"),
            MaxFunctions = line.Number("max") ?? 0,
            Namespaces = line.Arguments,
        };

        string text = host.DescribeCapabilities(options);
        string? output = line.Value("out");

        if (output == null)
        {
            Console.WriteLine(text);
            return Ok;
        }

        File.WriteAllText(output, text, new UTF8Encoding(false));
        Console.Error.WriteLine($"aisc: манифест записан в {output}");

        return Ok;
    }

    private static int Help(CommandLine line)
    {
        ScriptHost host = CreateHost(line);
        string query = string.Join(' ', line.Arguments);

        Console.WriteLine(host.Describe(query));

        IReadOnlyList<ManifestMatch> matches = host.Search(query, limit: 8);

        if (matches.Count == 0) return Ok;

        Console.WriteLine();
        Console.WriteLine("Похожие функции:");

        foreach (ManifestMatch match in matches)
            Console.WriteLine($"  {match.Function.Signature}");

        return Ok;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"aisc: неизвестная команда '{command}'");
        PrintUsage();

        return BadUsage;
    }

    // --- обвязка ---

    /// <summary>
    /// Хост утилиты: стандартная библиотека, графики и LLM-контур без служб.
    /// </summary>
    /// <remarks>
    /// <c>llm</c> и <c>search</c> подключаются без клиента и без эмбеддера: так словесный
    /// поиск работает офлайн, а скрипт, обращающийся к модели, получает внятный отказ «модель
    /// не подключена хостом» вместо загадочного «неизвестная функция». Ключи утилита не
    /// хранит и не спрашивает — это забота того хоста, который будет её встраивать.
    /// </remarks>
    private static ScriptHost CreateHost(CommandLine line)
    {
        _ = line;
        return StandardLibrary.CreateHost().UseCharts().UseLlm();
    }

    /// <summary>
    /// Настройки прогона из флагов.
    /// </summary>
    /// <remarks>
    /// Рабочая папка по умолчанию — папка самого скрипта: относительные пути в нём тогда
    /// означают то же, что и в проводнике, а выйти за неё скрипт всё равно не сможет.
    /// </remarks>
    private static RunOptions RunOptionsFrom(CommandLine line, string path)
    {
        string root = line.Value("workdir") ?? Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";

        // '--untrusted' — это профиль целиком, а не один флаг: скрипт, написанный моделью,
        // требует восьми согласованных настроек, и собирать их поштучно значит однажды забыть
        // ту, ради которой всё затевалось.
        RunOptions options = line.Has("untrusted")
            ? RunProfiles.Untrusted(line.Has("no-files") ? null : root)
            : new RunOptions
            {
                Sandbox = line.Has("no-files")
                    ? DeniedSandbox.Instance
                    : new WorkspaceSandbox(root, line.Has("read-only")),
            };

        options.FileName = Path.GetFileName(path);

        if (line.Number("seed") is int seed) options.Seed = seed;
        if (line.Number("steps") is int steps) options.Limits.Steps = steps;
        if (line.Duration("timeout") is TimeSpan timeout) options.Limits.Timeout = timeout;
        if (line.Number("parallel") is int parallel) options.Parallelism = Math.Max(1, parallel);

        options.Cache = CacheFrom(line, root);

        if (line.Has("progress")) options.Progress = new DelegateProgressSink(ReportStage);

        return options;
    }

    /// <summary>
    /// Кэш стадий по флагам командной строки.
    /// </summary>
    /// <remarks>
    /// По умолчанию — папка <c>.aisc-cache</c> рядом со скриптом: без этого <c>@cache</c> не
    /// пережил бы завершение процесса, а весь смысл кэша для утилиты именно в повторном
    /// запуске. Убирается это одним флагом.
    /// </remarks>
    private static IStageCache? CacheFrom(CommandLine line, string root)
    {
        if (line.Has("no-cache")) return DisabledStageCache.Instance;

        string directory = line.Value("cache") ?? Path.Combine(root, ".aisc-cache");

        return new FileStageCache(directory);
    }

    private static void ReportStage(StageNode stage, bool finished)
    {
        if (!finished)
        {
            Console.Error.WriteLine($"  → {stage.Name} …");
            return;
        }

        Console.Error.WriteLine($"  ✓ {stage}");
    }

    private static bool TryReadScript(CommandLine line, out string path, out string source)
    {
        path = line.Arguments.Count > 0 ? line.Arguments[0] : string.Empty;
        source = string.Empty;

        if (path.Length == 0)
        {
            Console.Error.WriteLine("aisc: не указан файл скрипта");
            return false;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"aisc: файл не найден — {path}");
            return false;
        }

        source = File.ReadAllText(path);
        return true;
    }

    private static ScriptValue Marshalled(object? value) => AI.Script.Binding.Marshaller.FromClr(value);

    private static string DiagnosticsJson(IReadOnlyList<Diagnostic> diagnostics, bool success)
    {
        var builder = new StringBuilder();

        _ = builder.Append("{\"success\":").Append(success ? "true" : "false").Append(",\"diagnostics\":[");

        for (int i = 0; i < diagnostics.Count; i++)
        {
            if (i > 0) _ = builder.Append(',');

            AppendDiagnostic(builder, diagnostics[i]);
        }

        return builder.Append("]}").ToString();
    }

    private static string RunJson(RunResult result)
    {
        var builder = new StringBuilder();

        _ = builder.Append("{\"success\":").Append(result.Success ? "true" : "false");

        _ = builder.Append(",\"transcript\":[");
        for (int i = 0; i < result.Transcript.Count; i++)
        {
            if (i > 0) _ = builder.Append(',');
            AppendString(builder, result.Transcript[i]);
        }

        _ = builder.Append("],\"emitted\":");
        _ = builder.Append(Json.Write(Marshalled(AsRecord(result.Emitted)), pretty: false));

        _ = builder.Append(",\"diagnostics\":[");
        for (int i = 0; i < result.Diagnostics.Count; i++)
        {
            if (i > 0) _ = builder.Append(',');
            AppendDiagnostic(builder, result.Diagnostics[i]);
        }

        _ = builder.Append("],\"stages\":[");
        for (int i = 0; i < result.Graph.Nodes.Count; i++)
        {
            if (i > 0) _ = builder.Append(',');
            AppendStage(builder, result.Graph.Nodes[i]);
        }

        _ = builder.Append("],\"stats\":{\"steps\":").Append(result.Stats.Steps.ToString(CultureInfo.InvariantCulture))
            .Append(",\"calls\":").Append(result.Stats.Calls.ToString(CultureInfo.InvariantCulture))
            .Append(",\"stages\":").Append(result.Stats.Stages.ToString(CultureInfo.InvariantCulture))
            .Append(",\"cached\":").Append(result.Stats.CachedStages.ToString(CultureInfo.InvariantCulture))
            .Append(",\"ms\":").Append(((long)result.Stats.Elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture))
            .Append("}}");

        return builder.ToString();
    }

    private static void AppendStage(StringBuilder builder, StageNode stage)
    {
        _ = builder.Append("{\"id\":").Append(stage.Id.ToString(CultureInfo.InvariantCulture))
            .Append(",\"name\":");

        AppendString(builder, stage.Name);

        _ = builder.Append(",\"caller\":")
            .Append(stage.Caller?.ToString(CultureInfo.InvariantCulture) ?? "null")
            .Append(",\"outcome\":");

        AppendString(builder, stage.Outcome switch
        {
            StageOutcome.Cached => "cached",
            StageOutcome.Failed => "failed",
            _ => "computed",
        });

        _ = builder.Append(",\"attempts\":").Append(stage.Attempts.ToString(CultureInfo.InvariantCulture))
            .Append(",\"ms\":").Append(((long)stage.Elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture))
            .Append(",\"result\":");

        AppendString(builder, stage.Result);
        _ = builder.Append('}');
    }

    private static Dictionary<string, object?> AsRecord(IReadOnlyDictionary<string, object?> values)
    {
        var record = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);

        foreach (var pair in values) record[pair.Key] = pair.Value;

        return record;
    }

    private static void AppendDiagnostic(StringBuilder builder, Diagnostic diagnostic)
    {
        LinePosition position = diagnostic.Position;

        _ = builder.Append("{\"code\":");
        AppendString(builder, diagnostic.Code);
        _ = builder.Append(",\"severity\":");
        AppendString(builder, diagnostic.Severity.ToString().ToLowerInvariant());
        _ = builder.Append(",\"line\":").Append(position.Line.ToString(CultureInfo.InvariantCulture));
        _ = builder.Append(",\"column\":").Append(position.Column.ToString(CultureInfo.InvariantCulture));
        _ = builder.Append(",\"message\":");
        AppendString(builder, diagnostic.Message);
        _ = builder.Append(",\"hint\":");
        AppendString(builder, diagnostic.Hint ?? string.Empty);
        _ = builder.Append('}');
    }

    private static void AppendString(StringBuilder builder, string text)
    {
        _ = builder.Append('"');

        foreach (char c in text)
        {
            switch (c)
            {
                case '"': _ = builder.Append("\\\""); break;
                case '\\': _ = builder.Append("\\\\"); break;
                case '\n': _ = builder.Append("\\n"); break;
                case '\r': _ = builder.Append("\\r"); break;
                case '\t': _ = builder.Append("\\t"); break;
                default:
                    if (c < ' ') _ = builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else _ = builder.Append(c);
                    break;
            }
        }

        _ = builder.Append('"');
    }

    private static void PrintUsage() => Console.WriteLine("""
        aisc — утилита языка AIScript

        Команды:
          aisc check <файл.ais> [--json]
              Проверяет скрипт, не выполняя его. Код возврата 1, если есть ошибки.

          aisc run <файл.ais> [--seed N] [--steps N] [--timeout 30s] [--parallel N]
                             [--workdir DIR] [--read-only] [--no-files] [--untrusted]
                             [--cache DIR | --no-cache] [--graph[=mermaid]] [--progress]
                             [--json] [--stats] [--verbose]
              Проверяет и выполняет скрипт. Рабочая папка по умолчанию — папка скрипта.
              '--untrusted' — профиль для скрипта, написанного моделью: сеть выключена,
              файлы только на чтение, время и шаги ограничены, часть опций закреплена.
              Результаты стадий с '@cache' сохраняются в '.aisc-cache' рядом со скриптом,
              поэтому повторный запуск их не считает; '--no-cache' это отключает.
              '--graph' печатает стадии прогона, '--graph=mermaid' — тот же граф записью
              Mermaid, готовой для вставки в отчёт.

          aisc docs [пространство ...] [--index] [--compact] [--json]
                                       [--no-examples] [--max N] [--out ФАЙЛ]
              Печатает манифест возможностей. '--index' даёт только список пространств —
              это то, что кладётся в системный промпт модели.

          aisc help <запрос>
              Справка по пространству либо функции, плюс похожие функции.

        Коды возврата: 0 — успех, 1 — ошибка в скрипте, 2 — ошибка вызова утилиты.
        """);
}

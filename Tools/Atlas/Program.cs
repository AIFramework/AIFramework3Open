using AI.LLM.Agents.MCP;
using AI.Script.Charts;
using AI.Script.Chem;
using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Nn;
using AI.Script.Std;
using AI.Script.Vision;
using AiFramework.Tools.Atlas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using System.Text;

// Атлас: справка по AIFramework для модели. Без аргументов — MCP-сервер на stdio.
Console.OutputEncoding = Encoding.UTF8;

string here = Directory.GetCurrentDirectory();
string? Arg(int index) => args.Length > index && !args[index].StartsWith("--", StringComparison.Ordinal) ? args[index] : null;
string? Option(string name) => Array.IndexOf(args, name) is int at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;

try
{
    return args.FirstOrDefault() switch
    {
        null or "serve" => await ServeAsync(),
        "worker" when Arg(1) is string root => BehaviorWorker.Run(root),
        "hook" => EditHook.Run(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8), Console.Out),
        "guard" => Guard(Arg(1) ?? here),
        "status" => Status(),
        "index" => await IndexCommands.IndexAsync(Arg(1) ?? here, Option("--solution")),
        "calibrate" => await IndexCommands.CalibrateAsync(Arg(1) ?? here),
        "audit" => await IndexCommands.AuditAsync(Arg(1) ?? here, args.Contains("--build")),
        "project" => await IndexCommands.ProjectAsync(Arg(1) ?? here, int.TryParse(Option("--limit"), out int shown) ? shown : 20, args.Contains("--behavior")),
        "show" when Arg(1) is string query => IndexCommands.Show(query, Arg(2) ?? here),
        "package" when Arg(1) is string dll => IndexCommands.Package(dll, Arg(2) ?? here),
        "search" when Arg(1) is string query => await SearchCommands.SearchAsync(query, here, args.Contains("--llm")),
        "embed" => await SearchCommands.EmbedAsync(Arg(1) ?? here),
        "glossary" when Arg(1) is string words => SearchCommands.Glossary(words, here),
        "path" when Arg(1) is string from && Arg(2) is string to => StackCommands.Path(from, to, here),
        "stack" when Arg(1) is string task => await StackCommands.StackAsync(task, Option("--steps"), args.Contains("--script"), args.Contains("--candidates"), here),
        "stack-eval" => await StackCommands.EvalAsync(here, args.Contains("--script"),
            double.TryParse(Option("--coverage"), System.Globalization.CultureInfo.InvariantCulture, out double coverage) ? coverage : null,
            args.Contains("--no-glossary") ? false : null),
        "dups" => Dups(context => DupReport.LibraryAsync(context, int.TryParse(Option("--limit"), out int limit) ? limit : 30, args.Contains("--behavior"),
            new Progress<int>(done => { if (done % 100 == 0) Console.Error.WriteLine($"исполнено пар: {done}"); })).GetAwaiter().GetResult()),
        "dup-explain" when Arg(1) is string a && Arg(2) is string b => Dups(context => DupReport.ExplainAsync(context, a, b).GetAwaiter().GetResult()),
        "dup-check" when Arg(1) is string file => Dups(context => DupReport.CheckAsync(context, file == "-" ? Console.In.ReadToEnd() : File.ReadAllText(file), Option("--intent")).GetAwaiter().GetResult()),
        "dup-decide" when Arg(1) is string a && Arg(2) is string b && Arg(3) is string verdict => Dups(context => Decide(context, a, b, verdict, Arg(4) ?? "")),
        "dup-eval" => Dups(context => DupReport.GoldAsync(context).GetAwaiter().GetResult()),
        "eval" => await SearchCommands.EvalAsync(Arg(1) ?? here, Option("--misses"), int.TryParse(Option("--sample"), out int n) ? n : null),
        _ => Usage(),
    };
}
catch (InvalidOperationException error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

int Dups(Func<AtlasContext, string> report)
{
    using AtlasContext context = AtlasContext.Open(here);
    Console.WriteLine(report(context));
    return 0;
}

static string Decide(AtlasContext context, string a, string b, string verdict, string reason)
{
    CodeUnit? first = context.Find(a);
    CodeUnit? second = context.Find(b);

    if (first is null || second is null) return $"Не найдено: {(first is null ? a : b)}";

    DupDecision decision = context.Ledger.Decide(first, second, verdict, reason);
    return $"Записано: {decision.A} ↔ {decision.B} — «{decision.Verdict}». Решение действует, пока тела не изменятся.";
}

static async Task<int> ServeAsync()
{
    ScriptHost host = StackReport.CreateScriptHost();

    // Пустой построитель: stdout занят протоколом, и консольный журнал по умолчанию сломал бы
    // его первой же строкой; appsettings.json из папки, где запущен сервер, тоже ни к чему.
    HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    // Claude Code передаёт корень проекта в CLAUDE_PROJECT_DIR; без неё — текущая папка.
    using var tools = new AtlasTools(Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") is { Length: > 0 } project ? project : Directory.GetCurrentDirectory());

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = "atlas", Version = "0.2.0" };
            options.ServerInstructions = AtlasTools.Instructions;
        })
        .WithStdioServerTransport()
        .AddAIFrameworkTools(new ScriptTool(host), tools);

    await builder.Build().RunAsync();
    return 0;
}

static int Guard(string root)
{
    IReadOnlyList<SecretGuard.Finding> findings = SecretGuard.Scan(GitRepository.WorkingTreeFiles(root), AtlasSettings.Load().ApiKey);

    foreach (SecretGuard.Finding finding in findings)
        Console.WriteLine($"{finding.File}:{finding.Line}: {finding.Kind}");

    Console.WriteLine(findings.Count == 0 ? "Секретов в рабочем дереве нет." : $"Найдено: {findings.Count}.");
    return findings.Count == 0 ? 0 : 1;
}

static int Status()
{
    Console.WriteLine(AtlasSettings.Load().Describe());
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("""
        atlas [serve]                       MCP-сервер на stdio
        atlas index [папка] [--solution f]  индекс исходников репозитория; повторный прогон разбирает только изменённое
        atlas show <имя> [папка]            карточка единицы: описание, кто вызывает, что вызывает
        atlas package <сборка.dll> [папка]  режим «только DLL» против индекса исходников той же сборки
        atlas search <запрос> [--llm]       поиск по открытому API библиотеки; --llm — с разбором моделью
        atlas embed [папка]                 посчитать векторы открытого API (нужен ключ OpenRouter)
        atlas eval [папка] [--sample N] [--misses файл]
                                            замер поиска на эталоне из туториалов и демо
        atlas path <тип> <тип>              до трёх кратчайших цепочек методов от типа к типу
        atlas stack <задача> [--steps "a; b"] [--script]
                                            стек под задачу: шаги, методы по порядку, проверка компиляцией или скриптом
        atlas stack-eval [--script]         сборка стеков на задачах бенчмарка AIScript
        atlas project [папка] [--limit N] [--behavior]
                                            проект на AIFramework: связь, снимок библиотеки, индекс проекта и отчёт
                                            «проект повторяет библиотеку / дубли в проекте / кандидаты в библиотеку»
        atlas dups [--limit N] [--behavior] дубли в библиотеке: копии, похожий код, один предмет — разный код;
                                            с --behavior пары исполняются, расхождения идут первыми
        atlas dup-check <файл|-> [--intent "замысел"]
                                            есть ли в библиотеке то, что написано в черновике
        atlas dup-explain <a> <b>           признаки пары, вердикт, решение журнала, начала тел
        atlas dup-decide <a> <b> <вердикт> [причина]
                                            записать решение в журнал (%LOCALAPPDATA%\Atlas)
        atlas dup-eval                      замер на эталоне дублей
        atlas audit [папка] [--build]       цикл гигиены: индекс, дубли, исполнение, сверка с журналом и прошлым аудитом;
                                            отчёт только о переменах, файл — в кэше рядом с индексом
        atlas calibrate [папка]             обучить вероятность «то же поведение» по журналу и исполнению
        atlas hook                          хук Claude Code после правки .cs: событие на stdin, замечание на stdout
        atlas guard [папка]                 ищет ключи OpenRouter в рабочем дереве git; код выхода 1 — найдено
        atlas status                        ключ, модели, пути настроек и кэша
        """);
    return 2;
}

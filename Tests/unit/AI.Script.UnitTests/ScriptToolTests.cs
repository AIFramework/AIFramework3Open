using AI.LLM.Agents.Tools;
using AI.Script.Charts;
using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Std;

namespace AI.Script.UnitTests;

/// <summary>
/// Скрипт как инструмент агента и цикл «написал → проверил → исполнил».
/// </summary>
public sealed class ScriptToolTests
{
    private static ScriptHost Host() => StandardLibrary.CreateHost().UseCharts();

    private static ScriptTool Tool() => new(Host(), static () => RunProfiles.Trusted());

    // --- инструмент ---

    [Fact]
    public async Task Tool_Run_ReturnsEmittedValues()
    {
        string report = await Tool().RunAsync("emit сумма = 2 + 2\nprint(\"считаю\")");

        Assert.Contains("Скрипт выполнен.", report, StringComparison.Ordinal);
        Assert.Contains("\"сумма\": 4", report, StringComparison.Ordinal);
        Assert.Contains("считаю", report, StringComparison.Ordinal);
    }

    /// <summary>Скрипт с ошибкой не исполняется: агент получает диагностики, а не последствия.</summary>
    [Fact]
    public async Task Tool_Run_DoesNotExecuteBadScript()
    {
        string report = await Tool().RunAsync("emit r = stat.срееднее(<1, 2>)");

        Assert.Contains("не прошёл проверку", report, StringComparison.Ordinal);
        Assert.Contains("AIS1101", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tool_Run_ReportsFailure()
    {
        var tool = Tool();

        string report = await tool.RunAsync("assert 1 == 2, \"не сходится\"");

        Assert.Contains("Скрипт сорвался.", report, StringComparison.Ordinal);
        Assert.Contains("не сходится", report, StringComparison.Ordinal);
        Assert.False(tool.Last!.Success);
    }

    /// <summary>
    /// Данные остаются в процессе: агент видит, что показан график, но не его содержимое.
    /// </summary>
    [Fact]
    public async Task Tool_Run_MentionsArtifactsWithoutTheirContents()
    {
        var tool = Tool();

        string report = await tool.RunAsync("""
            let t = signal.time(0.01, fs: 1000)

            show plot.line(signal.sine(t, freq: 100), title: "Сигнал")

            emit точек = len(t)
            """);

        Assert.Contains("Показано пользователю: plot «Сигнал»", report, StringComparison.Ordinal);
        Assert.DoesNotContain("traces", report, StringComparison.Ordinal);
        Assert.Single(tool.Last!.Artifacts);
    }

    /// <summary>Длинный вывод усекается с начала: причина отказа обычно в последних строках.</summary>
    [Fact]
    public async Task Tool_Run_TruncatesLongTranscriptFromTheStart()
    {
        string report = await Tool().RunAsync("""
            for i in 0..200 { print("строка ${i}") }

            emit r = 1
            """);

        Assert.Contains("пропущено строк:", report, StringComparison.Ordinal);
        Assert.Contains("строка 199", report, StringComparison.Ordinal);
        Assert.DoesNotContain("строка 0\n", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_Check_DoesNotRun()
    {
        Assert.Contains("Проверка пройдена", Tool().Check("emit r = 1"), StringComparison.Ordinal);
        Assert.Contains("AIS1101", Tool().Check("emit r = math.квадрат(2)"), StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_Help_AnswersOnThreeLevels()
    {
        var tool = Tool();

        Assert.Contains("Пространства имён", tool.Help(), StringComparison.Ordinal);
        Assert.Contains("stat.mean", tool.Help("stat"), StringComparison.Ordinal);
        Assert.Contains("Коэффициент корреляции", tool.Help("stat.corr"), StringComparison.Ordinal);
        Assert.Contains("corr", tool.Help("корреляция"), StringComparison.Ordinal);
    }

    /// <summary>Инструменты видны реестру агента: без этого модель их просто не увидит.</summary>
    [Fact]
    public void Tool_IsVisibleToAgentRegistry()
    {
        ToolRegistry registry = ToolRegistry.FromObjects(Tool());

        Assert.Contains("run_script", registry.ToolNames);
        Assert.Contains("check_script", registry.ToolNames);
        Assert.Contains("script_help", registry.ToolNames);
    }

    // --- цикл исправления ---

    [Fact]
    public async Task Writer_ReturnsResultOnFirstTry()
    {
        var llm = new FakeLlm("emit r = 6 * 7");
        var writer = new ScriptWriter(llm, Host(), Trusted());

        ScriptSolution solution = await writer.SolveAsync("Умножь 6 на 7");

        Assert.True(solution.Success);
        Assert.Equal(1, solution.Attempts);
        Assert.Equal(42.0, solution.Result!.Emitted["r"]);
    }

    /// <summary>
    /// Ради этого цикла проверка и существует: опечатка модели стоит одного дешёвого ответа с
    /// диагностикой, а не полного прогона.
    /// </summary>
    [Fact]
    public async Task Writer_RepairsAfterCheckDiagnostics()
    {
        var llm = new FakeLlm("emit r = stat.срееднее(<1, 2, 3>)", "emit r = stat.mean(<1, 2, 3>)");
        var writer = new ScriptWriter(llm, Host(), Trusted());

        ScriptSolution solution = await writer.SolveAsync("Посчитай среднее");

        Assert.True(solution.Success);
        Assert.Equal(2, solution.Attempts);
        Assert.Equal(2.0, solution.Result!.Emitted["r"]);

        // Диагностика ушла модели дословно: код, само неизвестное имя и подсказка, куда смотреть.
        string repair = llm.LastMessages[^1].Content?.ToString() ?? string.Empty;

        Assert.Contains("AIS1101", repair, StringComparison.Ordinal);
        Assert.Contains("срееднее", repair, StringComparison.Ordinal);
        Assert.Contains("help(\"stat\")", repair, StringComparison.Ordinal);
    }

    /// <summary>Сорвавшийся прогон — такая же поправимая ошибка, как и ошибка проверки.</summary>
    [Fact]
    public async Task Writer_RepairsAfterFailedRun()
    {
        var llm = new FakeLlm(
            "emit r = <1, 2, 3>[10]",
            "emit r = <1, 2, 3>[2]");

        var writer = new ScriptWriter(llm, Host(), Trusted());

        ScriptSolution solution = await writer.SolveAsync("Возьми последний элемент");

        Assert.True(solution.Success);
        Assert.Equal(2, solution.Attempts);
        Assert.Equal(3.0, solution.Result!.Emitted["r"]);
    }

    [Fact]
    public async Task Writer_StopsAfterMaxRepairs()
    {
        var llm = new FakeLlm("плохо(", "тоже плохо(", "и это плохо(", "запас");
        var writer = new ScriptWriter(llm, Host(), Trusted());

        ScriptSolution solution = await writer.SolveAsync("Сделай что-нибудь");

        Assert.False(solution.Success);
        Assert.Null(solution.Result);
        Assert.Equal(3, solution.Attempts);
        Assert.Equal(3, llm.Requests);
        Assert.NotEmpty(solution.Diagnostics);
    }

    /// <summary>Ограды кода снимаются: иначе разбор спотыкается на трёх обратных кавычках.</summary>
    [Fact]
    public async Task Writer_StripsCodeFences()
    {
        var llm = new FakeLlm("""
            Вот скрипт:

            ```python
            emit r = 1 + 1
            ```
            """);

        ScriptSolution solution = await new ScriptWriter(llm, Host(), Trusted()).SolveAsync("Сложи");

        Assert.True(solution.Success);
        Assert.Equal("emit r = 1 + 1", solution.Script);
    }

    /// <summary>
    /// В системный промпт уходят и правила языка, и перечень пространств именно этого хоста:
    /// зашитый список рано или поздно разошёлся бы с тем, что подключено.
    /// </summary>
    [Fact]
    public void Prompt_CarriesRulesAndHostNamespaces()
    {
        string prompt = ScriptPrompt.System(Host());

        Assert.Contains("Вызов функции:", prompt, StringComparison.Ordinal);
        Assert.Contains("Пространства имён", prompt, StringComparison.Ordinal);
        Assert.Contains("**plot**", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Этот текст кладётся в системный промпт", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_ExtractScript_HandlesPlainAnswer()
    {
        Assert.Equal("emit r = 1", ScriptPrompt.ExtractScript("  emit r = 1  "));
        Assert.Equal(string.Empty, ScriptPrompt.ExtractScript("   "));
    }

    private static ScriptWriterOptions Trusted() => new()
    {
        RunOptions = static () => RunProfiles.Trusted(),
    };
}

using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Interop;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Agents.Tools;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Правило строкового результата инструмента [AgentTool] (сессия С2): строка не считается удачей молча. Правило
/// выбирается явно; при <see cref="ToolTextResult.Refused"/> инструмент со строкой — ошибка конфигурации, при
/// <see cref="ToolTextResult.Success"/> — прежнее поведение; старые входы помечены [Obsolete] и ведут себя как прежде.
/// </summary>
public class ToolTextResultTests
{
    [Fact]
    public void Refused_StringTool_IsRejectedAtConstruction_WithItsName()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ToolRegistryToolSource.FromObjects(ToolTextResult.Refused, new TypedTools(), new TextTools()));

        Assert.Contains("read_note", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("save_note", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refused_TypedTools_KeepTheirOutcome()
    {
        var source = ToolRegistryToolSource.FromObjects(ToolTextResult.Refused, new TypedTools());
        IReActTool[] tools = [.. source.GetTools(new ReActRunContext("вопрос"))];

        ReActToolOutcome saved = await Run(tools.Single(t => t.Name == "save_note"));
        ReActToolOutcome refused = await Run(tools.Single(t => t.Name == "delete_note"));
        ReActToolOutcome nothing = await Run(tools.Single(t => t.Name == "touch"));

        Assert.True(saved.Ok);
        Assert.False(refused.Ok);
        Assert.True(nothing.Ok);
    }

    [Fact]
    public void Refused_StringToolRegisteredLater_IsRejectedOnTheNextRun()
    {
        var registry = ToolRegistry.FromObjects(new TypedTools());
        var source = new ToolRegistryToolSource(registry, ToolTextResult.Refused);
        registry.Register("late", "Поздний", (string input) => "ок: " + input);

        Assert.Throws<ArgumentException>(() => source.GetTools(new ReActRunContext("вопрос")).ToList());
    }

    [Fact]
    public async Task Success_StringIsOk_AsBefore()
    {
        var source = ToolRegistryToolSource.FromObjects(ToolTextResult.Success, new TextTools());

        ReActToolOutcome outcome = await Run(source.GetTools(new ReActRunContext("вопрос")).Single());

        Assert.True(outcome.Ok);
        Assert.Equal("файла нет", outcome.Observation);
    }

    [Fact]
    public void Unspecified_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolRegistryToolSource(new ToolRegistry(), ToolTextResult.Unspecified));

    /// <summary>Старые входы — адаптеры с прежним поведением и пометкой [Obsolete], а не удаление.</summary>
    [Fact]
    public async Task OldEntryPoints_AreObsoleteAdapters_WithTheOldBehaviour()
    {
#pragma warning disable CS0618
        var source = ToolRegistryToolSource.FromObjects(new TextTools());
#pragma warning restore CS0618

        Assert.True((await Run(source.GetTools(new ReActRunContext("вопрос")).Single())).Ok);

        string[] obsolete =
        [
            Describe(typeof(ToolRegistryToolSource).GetConstructor([typeof(ToolRegistry)])),
            Describe(typeof(ToolRegistryToolSource).GetMethod(nameof(ToolRegistryToolSource.FromObjects), [typeof(object[])])),
            Describe(typeof(ReActAgentBuilder).GetMethod(nameof(ReActAgentBuilder.WithAttributedTools), [typeof(object)])),
            Describe(typeof(ReActAgentBuilder).GetMethod(nameof(ReActAgentBuilder.WithToolRegistry), [typeof(ToolRegistry)])),
#pragma warning disable CS0618
            Describe(typeof(AI.LLM.Services.LLM.ClassifierWithLLM)),
#pragma warning restore CS0618
        ];

        Assert.All(obsolete, member => Assert.EndsWith(": obsolete", member, StringComparison.Ordinal));
    }

    private static string Describe(System.Reflection.MemberInfo? member) =>
        member == null
            ? "не найден"
            : member.Name + (member.IsDefined(typeof(ObsoleteAttribute), inherit: false) ? ": obsolete" : ": нет [Obsolete]");

    private static async Task<ReActToolOutcome> Run(IReActTool tool)
    {
        ReActToolOutcome? outcome = null;
        await foreach (ReActToolEvent e in tool.ExecuteAsync(new ReActToolInvocation(new ReActAction(tool.Name, "{}"), new ReActRunContext("вопрос"))))
        {
            if (e is ReActToolEvent.Result result)
                outcome = result.Value;
        }

        return outcome ?? throw new InvalidOperationException("инструмент не вернул итога");
    }

    private sealed class TypedTools
    {
        [AgentTool("save_note", "Сохраняет заметку")]
        public ToolResult Save() => ToolResult.Success("сохранено");

        [AgentTool("delete_note", "Удаляет заметку")]
        public Task<ToolResult> Delete() => Task.FromResult(ToolResult.Failure("заметки нет"));

        [AgentTool("touch", "Ничего не возвращает")]
        public void Touch()
        {
        }
    }

    private sealed class TextTools
    {
        [AgentTool("read_note", "Читает заметку")]
        public string Read() => "файла нет";
    }
}

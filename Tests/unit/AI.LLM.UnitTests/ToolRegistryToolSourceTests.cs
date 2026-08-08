using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Interop;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Agents.Tools;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Мост к инструментам на атрибутах. Главное здесь — снять расхождение форматов: реестр ждёт
/// аргументы JSON-объектом, а текстовый протокол решений присылает простую строку. Без этого
/// один и тот же инструмент работал бы только с одним способом принятия решений.
/// </summary>
public class ToolRegistryToolSourceTests
{
    [Fact]
    public void ToolRegistryToolSource_GetTools_ExposesOneToolPerAttributedMethod()
    {
        var source = ToolRegistryToolSource.FromObjects(new SampleTools());

        List<IReActTool> tools = source.GetTools(new ReActRunContext("вопрос")).ToList();

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "echo");
        Assert.Contains(tools, t => t.Name == "add_numbers");
    }

    [Fact]
    public void ToolRegistryToolSource_GetTools_SeesToolRegisteredAfterSourceWasBuilt()
    {
        var registry = ToolRegistry.FromObjects(new SampleTools());
        var source = new ToolRegistryToolSource(registry);

        // Имя инструмента может приходить из данных, а не из кода: реестр потокобезопасен и
        // допускает регистрацию во время работы. Набор читается на каждом прогоне, поэтому
        // зарегистрированный позже инструмент модель обязана увидеть — исполнить его она
        // и так может, реестр резолвит по имени в момент вызова.
        Assert.DoesNotContain(source.GetTools(new ReActRunContext("вопрос")), t => t.Name == "runtime_tool");

        registry.Register("runtime_tool", "Появился в рантайме", (string input) => "ок: " + input);

        List<IReActTool> tools = source.GetTools(new ReActRunContext("вопрос")).ToList();
        Assert.Contains(tools, t => t.Name == "runtime_tool");
        Assert.Contains(tools, t => t.Name == "echo");
    }

    [Fact]
    public async Task ReActEngine_RunAsync_OffersToolRegisteredBetweenRuns()
    {
        var registry = ToolRegistry.FromObjects(new SampleTools());

        var policy = new Fakes.FakeReActPolicy { Fallback = ReActDecision.Final("готово") };
        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithToolRegistry(registry)
            .Build();

        await engine.RunAsync("первый прогон");
        registry.Register("runtime_tool", "Появился в рантайме", (string input) => "ок: " + input);
        await engine.RunAsync("второй прогон");

        Assert.DoesNotContain(policy.Calls[0].Tools, t => t.Name == "runtime_tool");
        Assert.Contains(policy.Calls[1].Tools, t => t.Name == "runtime_tool");
    }

    [Fact]
    public void ToolRegistryToolSource_GetTools_CarriesSchemaFromReflection()
    {
        var source = ToolRegistryToolSource.FromObjects(new SampleTools());

        IReActTool add = source.GetTools(new ReActRunContext("вопрос")).Single(t => t.Name == "add_numbers");

        Assert.NotNull(add.ParametersJsonSchema);
        Assert.Contains("\"a\"", add.ParametersJsonSchema);
        Assert.Contains("\"b\"", add.ParametersJsonSchema);
    }

    [Fact]
    public async Task ToolRegistryToolSource_ExecuteAsync_WrapsPlainStringForSingleParameterTool()
    {
        var source = ToolRegistryToolSource.FromObjects(new SampleTools());
        IReActTool echo = source.GetTools(new ReActRunContext("вопрос")).Single(t => t.Name == "echo");

        ReActToolOutcome outcome = await RunAsync(echo, "привет");

        // Аргумент пришёл простой строкой, а реестр получил корректный JSON-объект.
        Assert.True(outcome.Ok);
        Assert.Equal("эхо: привет", outcome.Observation);
    }

    [Fact]
    public async Task ToolRegistryToolSource_ExecuteAsync_PassesJsonArgumentThrough()
    {
        var source = ToolRegistryToolSource.FromObjects(new SampleTools());
        IReActTool add = source.GetTools(new ReActRunContext("вопрос")).Single(t => t.Name == "add_numbers");

        ReActToolOutcome outcome = await RunAsync(add, """{"a":2,"b":3}""");

        Assert.True(outcome.Ok);
        Assert.Equal("5", outcome.Observation);
    }

    [Fact]
    public async Task ToolRegistryToolSource_ExecuteAsync_ReportsToolFailureAsFailedOutcome()
    {
        var source = ToolRegistryToolSource.FromObjects(new SampleTools());
        IReActTool echo = source.GetTools(new ReActRunContext("вопрос")).Single(t => t.Name == "echo");

        ReActToolOutcome outcome = await RunAsync(echo, "взорвись");

        Assert.False(outcome.Ok);
        Assert.Contains("так не пойдёт", outcome.Observation);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_UsesAttributedToolsThroughBuilder()
    {
        var policy = new Fakes.FakeReActPolicy(
            ReActDecision.Act(new ReActAction("echo", "мир")),
            ReActDecision.Final("готово"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithAttributedTools(new SampleTools())
            .Build();

        ReActResult result = await engine.RunAsync("скажи что-нибудь");

        Assert.Equal("эхо: мир", result.Steps[0].Observations[0].Text);
    }

    private static async Task<ReActToolOutcome> RunAsync(IReActTool tool, string arguments)
    {
        var invocation = new ReActToolInvocation(
            new ReActAction(tool.Name, arguments), new ReActRunContext("вопрос"));

        ReActToolOutcome? outcome = null;
        await foreach (ReActToolEvent evt in tool.ExecuteAsync(invocation))
        {
            if (evt is ReActToolEvent.Result result)
                outcome = result.Value;
        }

        Assert.NotNull(outcome);
        return outcome!;
    }

    /// <summary>Инструменты, объявленные атрибутами — старый способ, который должен продолжать работать.</summary>
    private sealed class SampleTools
    {
        [AgentTool("echo", "Повторяет переданный текст")]
        public string Echo([ToolParameter("Текст для повтора")] string text)
        {
            if (text == "взорвись")
                throw new InvalidOperationException("так не пойдёт");

            return "эхо: " + text;
        }

        [AgentTool("add_numbers", "Складывает два числа")]
        public string Add([ToolParameter("Первое слагаемое")] int a, [ToolParameter("Второе слагаемое")] int b) =>
            (a + b).ToString();
    }
}

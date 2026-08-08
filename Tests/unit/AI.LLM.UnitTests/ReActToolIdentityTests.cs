using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Имя инструмента и идентификатор вызова в следе. Резолв намеренно прощает модели разное
/// написание (<c>web_search</c>, <c>web-search</c>, <c>Web Search</c>), поэтому дальше по ходу —
/// ключ повтора, счёт падений подряд, восстановление переписки — имя обязано быть одно.
/// А переиспользованное при повторе наблюдение обязано нести идентификатор ТЕКУЩЕГО вызова:
/// два вызова с одним id в одной переписке провайдер отвергает целиком.
/// </summary>
public class ReActToolIdentityTests
{
    [Fact]
    public async Task ReActEngine_RunAsync_RecognisesRepeatWrittenWithAnotherSeparator()
    {
        var tool = new FakeReActTool("web_search");

        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("web_search", "погода")),
            ReActDecision.Act(new ReActAction("web-search", "погода")),   // то же самое, иначе записанное
            ReActDecision.Final("готово"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithMaxIterations(5)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);

        // Повтор распознан: инструмент исполнен ровно один раз, второй шаг получил прежний результат.
        Assert.Single(tool.Invocations);
        Assert.Equal("результат погода", result.Steps[1].Observations.Single().Text);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_WritesCanonicalToolNameIntoTrace()
    {
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("Web Search", "погода")),
            ReActDecision.Final("готово"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(new FakeReActTool("web_search"))
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // В след уходит имя инструмента, а не написание модели: именно оно поедет обратно
        // провайдеру, который знает инструмент только под каноническим именем.
        Assert.Equal("web_search", result.Steps[0].Observations.Single().Action!.ToolName);
        Assert.Equal("web_search", result.Steps[0].Actions.Single().ToolName);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_GivesRepeatedActionItsOwnCallId()
    {
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("web_search", "погода", "call_1")),
            ReActDecision.Act(new ReActAction("web_search", "погода", "call_2")),
            ReActDecision.Final("готово"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(new FakeReActTool("web_search"))
            .WithRepeatedActionPolicy(maxRepeats: 3, maxConsecutiveFailures: 2)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal("call_1", result.Steps[0].Observations.Single().Action!.Id);

        // Результат прежний, а идентификатор — нового вызова.
        ReActObservation repeated = result.Steps[1].Observations.Single();
        Assert.Equal("call_2", repeated.Action!.Id);
        Assert.Equal("результат погода", repeated.Text);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_NeverRepeatsToolCallIdAcrossSteps()
    {
        // След, каким он получается после повтора действия: два шага, один и тот же вызов.
        var first = new ReActAction("web_search", "погода", "call_1");
        var second = new ReActAction("web_search", "погода", "call_2");

        var trace = new ReActTrace();
        trace.Add(new ReActStep
        {
            Number = 1,
            Actions = [first],
            Observations = [new ReActObservation { Action = first, Ok = true, Text = "ясно" }],
        });
        trace.Add(new ReActStep
        {
            Number = 2,
            Actions = [second],
            Observations = [new ReActObservation { Action = second, Ok = true, Text = "ясно" }],
        });

        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("готово"));
        await new NativeToolCallPolicy(llm).DecideAsync(new ReActPolicyContext
        {
            Query = new ReActQuery("вопрос"),
            Tools = [new FakeReActTool("web_search")],
            Trace = trace,
            SystemPrompt = "инструкция",
            StepNumber = 3,
            MaxSteps = 5,
        });

        List<string> ids = llm.SentMessages[0]
            .Where(m => m.ToolCalls is { Count: > 0 })
            .SelectMany(m => m.ToolCalls)
            .Select(c => c.Id)
            .ToList();

        Assert.Equal(["call_1", "call_2"], ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());

        // На каждый вызов — ответ с тем же идентификатором.
        List<string> answered = llm.SentMessages[0]
            .Where(m => m.Role == LLMMessage.ToolRole)
            .Select(m => m.ToolCallId!)
            .ToList();
        Assert.Equal(ids, answered);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_CountsConsecutiveFailuresAcrossDifferentSpellings()
    {
        var tool = new FakeReActTool(
            "web_search",
            (_, _) => Task.FromResult(ReActToolOutcome.Failure("сеть недоступна")));

        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("web_search", "а")),
            ReActDecision.Act(new ReActAction("web-search", "б")),
            ReActDecision.Act(new ReActAction("Web Search", "в")),
            ReActDecision.Final("готово"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithRepeatedActionPolicy(maxRepeats: 5, maxConsecutiveFailures: 2)
            .WithMaxIterations(6)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // Инструмент падает подряд — ход останавливается, как бы модель его ни называла.
        Assert.Equal(ReActStopReason.NoProgress, result.StopReason);
        Assert.Equal(2, tool.Invocations.Count);
    }
}

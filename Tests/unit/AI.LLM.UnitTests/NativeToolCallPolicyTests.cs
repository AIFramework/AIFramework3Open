using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Решения через нативные вызовы инструментов. Здесь проверяется самое хрупкое место всего
/// движка: переписка восстанавливается из следа заново на каждом шаге, и на каждый вызов
/// инструмента обязан прийти ответ с тем же идентификатором. Потеря хотя бы одного —
/// и поставщик отвергает весь следующий запрос.
/// </summary>
public class NativeToolCallPolicyTests
{
    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_SendsToolDefinitionsInSettings()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("готово"));
        var policy = new NativeToolCallPolicy(llm);

        await policy.DecideAsync(Context([new FakeReActTool("web_search"), new FakeReActTool("read_file")]));

        var tools = llm.SentSettings[0]!.Tools;
        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Function.Name == "web_search");
        Assert.Contains(tools, t => t.Function.Name == "read_file");
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_ReturnsAllParallelToolCalls()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithToolCalls(
            ("call_a", "web_search", """{"input":"погода"}"""),
            ("call_b", "web_search", """{"input":"курс"}""")));

        var policy = new NativeToolCallPolicy(llm);

        ReActDecision decision = await policy.DecideAsync(Context([new FakeReActTool("web_search")]));

        Assert.Equal(2, decision.Actions.Count);
        Assert.Equal("call_a", decision.Actions[0].Id);
        Assert.Equal("call_b", decision.Actions[1].Id);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_UnwrapsPlainArgumentForToolWithoutSchema()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithToolCalls(
            ("call_a", "web_search", """{"input":"погода в Москве"}""")));

        var policy = new NativeToolCallPolicy(llm);

        ReActDecision decision = await policy.DecideAsync(Context([new FakeReActTool("web_search")]));

        // Инструмент без собственной схемы должен получить простую строку — ровно ту же,
        // что пришла бы ему при текстовом протоколе решений.
        Assert.Equal("погода в Москве", decision.Actions[0].Arguments);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_ReturnsFinalWhenNoToolCalls()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("вот ответ"));
        var policy = new NativeToolCallPolicy(llm);

        ReActDecision decision = await policy.DecideAsync(Context([new FakeReActTool("web_search")]));

        Assert.True(decision.IsFinal);
        Assert.Equal("вот ответ", decision.FinalText);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_ReturnsMalformedWhenResponseHasNoChoices()
    {
        var llm = new FakeLLMClient().EnqueueFull(new ChatCompletionsResponse { Choices = [] });
        var policy = new NativeToolCallPolicy(llm);

        ReActDecision decision = await policy.DecideAsync(Context([new FakeReActTool("web_search")]));

        // Пустой ответ — это сбой, а не «модель закончила».
        Assert.True(decision.IsMalformed);
        Assert.False(decision.IsFinal);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_AccumulatesUsage()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("готово"));
        var policy = new NativeToolCallPolicy(llm);

        ReActDecision decision = await policy.DecideAsync(Context([new FakeReActTool("web_search")]));

        Assert.NotNull(decision.Usage);
        Assert.Equal(10, decision.Usage!.TotalTokens);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_RebuildsMessagesWithMatchingToolResultForEveryToolCallId()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("готово"));
        var policy = new NativeToolCallPolicy(llm);

        // След с шагом, где было ДВА параллельных вызова.
        var trace = new ReActTrace();
        var first = new ReActAction("web_search", """{"q":"погода"}""", "call_a");
        var second = new ReActAction("web_search", """{"q":"курс"}""", "call_b");
        trace.Add(new ReActStep
        {
            Number = 1,
            Thought = "поищу оба",
            Actions = [first, second],
            Observations =
            [
                new ReActObservation { Action = first, Ok = true, Text = "ясно" },
                new ReActObservation { Action = second, Ok = true, Text = "90" },
            ],
        });

        await policy.DecideAsync(Context([new FakeReActTool("web_search")], trace, step: 2));

        List<LLMMessage> sent = llm.SentMessages[0];

        // На каждый tool_call должен найтись ответ role=tool с тем же идентификатором.
        List<string> requested = sent
            .Where(m => m.ToolCalls is { Count: > 0 })
            .SelectMany(m => m.ToolCalls)
            .Select(c => c.Id)
            .ToList();

        List<string> answered = sent
            .Where(m => m.Role == LLMMessage.ToolRole)
            .Select(m => m.ToolCallId)
            .ToList();

        Assert.Equal(["call_a", "call_b"], requested);
        Assert.Equal(["call_a", "call_b"], answered);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_PutsHistoryAndQueryBeforeTrace()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("готово"));
        var policy = new NativeToolCallPolicy(llm);

        var query = new ReActQuery(
            "что там с погодой",
            history: [LLMMessage.CreateMessage(Roles.User, "привет")]);

        await policy.DecideAsync(new ReActPolicyContext
        {
            Query = query,
            Tools = [new FakeReActTool("web_search")],
            Trace = new ReActTrace(),
            SystemPrompt = "инструкция",
            StepNumber = 1,
            MaxSteps = 5,
        });

        List<LLMMessage> sent = llm.SentMessages[0];

        Assert.Equal(LLMMessage.SystemRole, sent[0].Role);
        Assert.Equal("привет", sent[1].Content?.ToString());
        Assert.Equal("что там с погодой", sent[2].Content?.ToString());
    }

    private static ReActPolicyContext Context(
        IReadOnlyList<IReActTool> tools, ReActTrace? trace = null, int step = 1) =>
        new()
        {
            Query = new ReActQuery("вопрос"),
            Tools = tools,
            Trace = trace ?? new ReActTrace(),
            SystemPrompt = "инструкция",
            StepNumber = step,
            MaxSteps = 5,
        };
}

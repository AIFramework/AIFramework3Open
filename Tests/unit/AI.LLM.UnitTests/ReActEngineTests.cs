using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Synthesis;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Проверки цикла ReAct. Каждый тест закрывает конкретный дефект, ради предотвращения
/// которого движок и делался.
/// </summary>
public class ReActEngineTests
{
    [Fact]
    public async Task ReActEngine_RunAsync_ReturnsFinalAnswerWhenPolicySignalsFinal()
    {
        var tool = new FakeReActTool("search");
        var policy = new FakeReActPolicy(ReActDecision.Final("готовый ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
        Assert.Equal("готовый ответ", result.Answer);
        Assert.Empty(tool.Invocations);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_DoesNotReexecuteIdenticalAction()
    {
        var tool = new FakeReActTool("search");
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("search", "погода")),
            ReActDecision.Act(new ReActAction("search", "  ПОГОДА ")),
            ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithRepeatedActionPolicy(maxRepeats: 2, maxConsecutiveFailures: 2)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // Повтор того же вызова не исполняется заново — берётся прежнее наблюдение.
        Assert.Single(tool.Invocations);
        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_FeedsUnknownToolNameBackToPolicy()
    {
        var tool = new FakeReActTool("search");
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("web_lookup", "погода")),
            ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // Цикл не оборвался, а подсказал модели доступные имена.
        Assert.Equal(2, policy.Calls.Count);
        Assert.Contains("search", policy.Calls[1].CorrectiveNote);
        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_MatchesToolNameIgnoringCaseAndSeparators()
    {
        var tool = new FakeReActTool("web_search");
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("Web-Search", "погода")),
            ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        await engine.RunAsync("вопрос");

        Assert.Single(tool.Invocations);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_RunsSynthesisOverObservationsOnIterationLimit()
    {
        var tool = new FakeReActTool("search");
        var synthesizer = new FakeReActSynthesizer("ответ по собранным данным");

        // Модель бесконечно просит новые вызовы — цикл упрётся в лимит шагов.
        var policy = new FakeReActPolicy
        {
            Fallback = ReActDecision.Act(new ReActAction("search", Guid.NewGuid().ToString())),
        };

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithMaxIterations(3)
            .WithRepeatedActionPolicy(maxRepeats: 10, maxConsecutiveFailures: 10)
            .WithSynthesizer(synthesizer, ReActSynthesisMode.WhenNoAnswer)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal(ReActStopReason.IterationLimit, result.StopReason);

        // Главное: собранная работа не выброшена — синтез получил все наблюдения,
        // а ответом стал текст, а не служебная строка про лимит.
        Assert.Single(synthesizer.Calls);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("ответ по собранным данным", result.Answer);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_EndsTurnAndCarriesPayloadFromTerminalTool()
    {
        var payload = new object();
        var tool = new FakeReActTool(
            "draw",
            (_, _) => Task.FromResult(ReActToolOutcome.Terminal("картинка готова", payload: payload)));
        var synthesizer = new FakeReActSynthesizer();
        var policy = new FakeReActPolicy(ReActDecision.Act(new ReActAction("draw", "кот")));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithSynthesizer(synthesizer, ReActSynthesisMode.Always)
            .Build();

        ReActResult result = await engine.RunAsync("нарисуй кота");

        Assert.Equal(ReActStopReason.TerminalTool, result.StopReason);
        Assert.Equal("картинка готова", result.Answer);
        Assert.Same(payload, result.Payload);

        // Терминальный результат не пересказывается синтезом.
        Assert.Empty(synthesizer.Calls);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_ConvertsToolExceptionToFailedObservation()
    {
        var tool = new FakeReActTool("search", (_, _) => throw new InvalidOperationException("провайдер недоступен"));
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("search", "погода")),
            ReActDecision.Final("не получилось"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        ReActObservation observation = Assert.Single(result.Steps[0].Observations);
        Assert.False(observation.Ok);
        Assert.Contains("провайдер недоступен", observation.Text);
        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
    }

    [Fact]
    public async Task ReActEngine_StreamAsync_ForwardsToolProgressPayloadUnchanged()
    {
        var payload = new object();
        var tool = new ProgressTool("slow", payload);
        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("slow", "x")),
            ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        var events = new List<ReActEvent>();
        await foreach (ReActEvent evt in engine.StreamAsync("вопрос"))
            events.Add(evt);

        ReActEvent.ToolProgress progress = Assert.Single(events.OfType<ReActEvent.ToolProgress>());

        // Нагрузка потребителя проходит сквозь движок той же ссылкой.
        Assert.Same(payload, progress.Payload);
        Assert.IsType<ReActEvent.Completed>(events[^1]);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_ExecutesForcedFirstActionWithoutCallingPolicy()
    {
        var tool = new FakeReActTool("generate");
        var policy = new FakeReActPolicy(ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        var query = new ReActQuery("сделай файл", forcedFirstAction: new ReActAction("generate", "отчёт"));
        await engine.RunAsync(query);

        Assert.Single(tool.Invocations);
        Assert.Equal("отчёт", tool.Invocations[0]);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_ThrowsOperationCanceledWhenTokenCancelled()
    {
        var tool = new FakeReActTool("search");
        var policy = new FakeReActPolicy { Fallback = ReActDecision.Act(new ReActAction("search", "x")) };

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.RunAsync("вопрос", cts.Token));
    }

    /// <summary>Инструмент, отдающий прогресс с нагрузкой вызывающей стороны.</summary>
    private sealed class ProgressTool(string name, object payload) : IReActTool
    {
        public string Name { get; } = name;

        public string Description => "инструмент с прогрессом";

        public async IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
            ReActToolInvocation invocation,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ReActToolEvent.Progress("половина", payload);
            yield return new ReActToolEvent.Result(ReActToolOutcome.Success("готово"));
        }
    }
}

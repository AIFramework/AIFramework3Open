using System.Runtime.CompilerServices;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Исполнение инструментов: таймаут, ограничение параллелизма, отмена соседних вызовов и
/// освобождение ресурсов при досрочном выходе потребителя. Ничего из этого не было ни в одном
/// из циклов, которые движок заменяет.
/// </summary>
public class ReActEngineExecutionTests
{
    [Fact]
    public async Task ReActEngine_RunAsync_CancelsToolAfterTimeoutAndContinues()
    {
        var tool = new FakeReActTool(
            "slow",
            async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return ReActToolOutcome.Success("не дождётесь");
            });

        var policy = new FakeReActPolicy(
            ReActDecision.Act(new ReActAction("slow", "x")),
            ReActDecision.Final("сдаюсь"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithToolTimeout(TimeSpan.FromMilliseconds(50))
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        ReActObservation observation = Assert.Single(result.Steps[0].Observations);
        Assert.False(observation.Ok);
        Assert.Equal("timeout", observation.ErrorCode);

        // Таймаут одного инструмента не обрывает ход.
        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_LimitsConcurrentToolExecution()
    {
        int current = 0;
        int peak = 0;

        var tool = new FakeReActTool(
            "work",
            async (invocation, ct) =>
            {
                int now = Interlocked.Increment(ref current);
                InterlockedMax(ref peak, now);
                await Task.Delay(40, ct);
                Interlocked.Decrement(ref current);
                return ReActToolOutcome.Success("готово " + invocation.Arguments);
            });

        var policy = new FakeReActPolicy(
            ReActDecision.Act([
                new ReActAction("work", "a"),
                new ReActAction("work", "b"),
                new ReActAction("work", "c"),
                new ReActAction("work", "d"),
            ]),
            ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithMaxParallelTools(2)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal(4, tool.Invocations.Count);
        Assert.True(peak <= 2, $"одновременно работало {peak} инструментов при пределе 2");
        Assert.Equal(4, result.Steps[0].Observations.Count);
    }

    [Fact]
    public async Task ReActEngine_RunAsync_CancelsSiblingToolsWhenOneEndsTurn()
    {
        var tools = new List<IReActTool>
        {
            new FakeReActTool("finish", (_, _) => Task.FromResult(ReActToolOutcome.Terminal("готовый ответ"))),
            new FakeReActTool(
                "endless",
                async (_, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return ReActToolOutcome.Success("никогда");
                }),
        };

        var policy = new FakeReActPolicy(
            ReActDecision.Act([new ReActAction("finish", "x"), new ReActAction("endless", "y")]));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTools(tools)
            .WithMaxParallelTools(2)
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        Assert.Equal(ReActStopReason.TerminalTool, result.StopReason);
        Assert.Equal("готовый ответ", result.Answer);

        // Соседний вызов не брошен работать в пустоту, а прерван.
        ReActObservation endless = result.Steps[0].Observations
            .Single(o => o.Action!.ToolName == "endless");
        Assert.Equal("superseded", endless.ErrorCode);
    }

    [Fact]
    public async Task ReActEngine_StreamAsync_DisposesToolWhenConsumerStopsEarly()
    {
        var tool = new DisposalTrackingTool("slow");
        var policy = new FakeReActPolicy { Fallback = ReActDecision.Act(new ReActAction("slow", Guid.NewGuid().ToString())) };

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .Build();

        await foreach (ReActEvent evt in engine.StreamAsync("вопрос"))
        {
            // Прекращаем чтение, как только инструмент подал признак жизни.
            if (evt is ReActEvent.ToolProgress)
                break;
        }

        Assert.True(tool.Disposed, "инструмент должен быть остановлен при досрочном выходе потребителя");
    }

    [Fact]
    public async Task ReActEngine_StreamAsync_IsColdAndDoesNotCallPolicyBeforeEnumeration()
    {
        var policy = new FakeReActPolicy(ReActDecision.Final("ответ"));

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(new FakeReActTool("search"))
            .Build();

        IAsyncEnumerable<ReActEvent> stream = engine.StreamAsync("вопрос");
        Assert.Empty(policy.Calls);

        await foreach (ReActEvent _ in stream)
        {
            // просто протягиваем
        }

        Assert.Single(policy.Calls);
    }

    [Fact]
    public async Task ReActEngine_StreamAsync_SupportsTwoConcurrentRunsOnOneInstance()
    {
        var tool = new FakeReActTool("search");
        var policy = new FakeReActPolicy
        {
            Fallback = ReActDecision.Act(new ReActAction("search", "общий запрос")),
        };

        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(tool)
            .WithMaxIterations(2)
            .WithRepeatedActionPolicy(maxRepeats: 5, maxConsecutiveFailures: 5)
            .Build();

        ReActResult[] results = await Task.WhenAll(
            engine.RunAsync("первый"),
            engine.RunAsync("второй"));

        // Прогоны не мешают друг другу: у каждого свой след.
        Assert.All(results, r => Assert.NotEmpty(r.Steps));
        Assert.Equal(2, results.Length);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen = Volatile.Read(ref target);
        while (value > seen)
        {
            int previous = Interlocked.CompareExchange(ref target, value, seen);
            if (previous == seen)
                return;

            seen = previous;
        }
    }

    /// <summary>Инструмент, отмечающий, что его перечислитель был закрыт.</summary>
    private sealed class DisposalTrackingTool(string name) : IReActTool
    {
        public string Name { get; } = name;

        public string Description => "долгий инструмент";

        public bool Disposed { get; private set; }

        public async IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
            ReActToolInvocation invocation,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                yield return new ReActToolEvent.Progress("начал", null);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                yield return new ReActToolEvent.Result(ReActToolOutcome.Success("готово"));
            }
            finally
            {
                Disposed = true;
            }
        }
    }
}

using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Гейт эффектов: ключ на каждом вызове, повтор исполненного — сохраненный итог без вызова, правила повтора по
/// «повтор безопасен».
/// </summary>
public sealed class EffectGateTests
{
    private static ToolSpec Spec(string name, ToolAction action, ToolIdempotency? idempotency) =>
        Kit.Spec(name, action) with { Idempotency = idempotency };

    private static Task<List<ToolUpdate>> Call(IToolSet set, string name, string arguments = """{"q":"x"}""", string runId = "run-42") =>
        Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "call-" + Guid.NewGuid().ToString("N"), Name = name, Arguments = Kit.Json(arguments) },
            Kit.Context() with { RunId = runId }));

    [Fact]
    public async Task EveryCall_GetsFrameworkKey()
    {
        var tool = new CountingTool(Spec("send", ToolAction.Irreversible, ToolIdempotency.WithKey), (_, _) => CountingTool.Ok());
        var log = new CapturingLog();
        var gate = new EffectGate(new ListToolSet(tool), log.Logger);

        await Kit.ReadAll(gate.CallAsync(
            new ToolCall { Id = "c1", Name = "send", Arguments = Kit.Json("""{"q":"x"}"""), EffectKey = "host-key" },
            Kit.Context()));

        Assert.Equal(EffectKeys.Create("run-42", "send", Kit.Json("""{"q":"x"}""")), tool.Received.Single().EffectKey);
        Assert.Contains(log.Warnings, w => w.Contains("заменен", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepeatOfDoneKey_ReturnsStoredResult_WithoutCallingTool()
    {
        var tool = new CountingTool(Spec("send", ToolAction.Irreversible, ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok("письмо 1"));
        var gate = new EffectGate(new ListToolSet(tool));

        ToolResult first = Kit.Result(await Call(gate, "send"));
        ToolResult second = Kit.Result(await Call(gate, "send", """{ "q" : "x" }"""));

        Assert.Equal(1, tool.Calls);
        Assert.Equal(first, second);
        EffectRecord record = Assert.Single(gate.Journal);
        Assert.Equal("send", record.Tool);
        Assert.Equal(ToolStatus.Ok, record.Result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ToolIdempotency.Unknown)]
    [InlineData(ToolIdempotency.NotIdempotent)]
    public async Task NotIdempotent_AfterFailure_IsNotRepeated(ToolIdempotency? idempotency)
    {
        var tool = new CountingTool(Spec("pay", ToolAction.Irreversible, idempotency), (_, _) => CountingTool.Failed());
        var gate = new EffectGate(new ListToolSet(tool));

        Assert.Equal(ToolStatus.Failed, Kit.Result(await Call(gate, "pay")).Status);
        ToolResult repeat = Kit.Result(await Call(gate, "pay"));

        Assert.Equal(1, tool.Calls);
        Assert.Equal(ToolStatus.Failed, repeat.Status);
        Assert.Equal(EffectGateCodes.RepeatUnsafe, repeat.ErrorCode);
    }

    [Fact]
    public async Task NotIdempotent_AfterException_IsNotRepeated_AndJournalShowsUnknownOutcome()
    {
        var tool = new CountingTool(Spec("pay", ToolAction.Irreversible, ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok())
        {
            ThrowOn = 1,
        };
        var gate = new EffectGate(new ListToolSet(tool));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Call(gate, "pay"));
        EffectRecord record = Assert.Single(gate.Journal);
        Assert.Equal(ToolStatus.Unknown, record.Result.Status);
        Assert.Equal(EffectGateCodes.Exception, record.Result.ErrorCode);

        ToolResult repeat = Kit.Result(await Call(gate, "pay"));
        Assert.Equal(1, tool.Calls);
        Assert.Equal(EffectGateCodes.RepeatUnsafe, repeat.ErrorCode);
    }

    [Fact]
    public async Task NotIdempotent_StreamWithoutResult_IsUnknownAndNotRepeated()
    {
        var tool = new CountingTool(Spec("post", ToolAction.Reversible, ToolIdempotency.NotIdempotent), (_, _) => null);
        var gate = new EffectGate(new ListToolSet(tool));

        ToolResult first = Kit.Result(await Call(gate, "post"));
        Assert.Equal(ToolStatus.Unknown, first.Status);
        Assert.Equal(EffectGateCodes.NoResult, first.ErrorCode);

        Assert.Equal(EffectGateCodes.RepeatUnsafe, Kit.Result(await Call(gate, "post")).ErrorCode);
        Assert.Equal(1, tool.Calls);
    }

    [Fact]
    public async Task NotIdempotent_Cancelled_IsNotRepeated()
    {
        var tool = new CountingTool(Spec("post", ToolAction.Irreversible, ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(tool));

        // Потребитель перестал читать поток до итога — обрыв.
        await using (IAsyncEnumerator<ToolUpdate> e = gate.CallAsync(
                         new ToolCall { Id = "c1", Name = "post", Arguments = Kit.Json("""{"q":"x"}""") }, Kit.Context()).GetAsyncEnumerator())
        {
        }

        Assert.Empty(gate.Journal); // вызов не начинался: перечислитель не двигали
        await using (IAsyncEnumerator<ToolUpdate> e = new InterruptingSet(gate).CallAsync(
                         new ToolCall { Id = "c2", Name = "post", Arguments = Kit.Json("""{"q":"x"}""") }, Kit.Context()).GetAsyncEnumerator())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await e.MoveNextAsync());
        }

        Assert.Equal(EffectGateCodes.Interrupted, Assert.Single(gate.Journal).Result.ErrorCode);
        Assert.Equal(EffectGateCodes.RepeatUnsafe, Kit.Result(await Call(gate, "post")).ErrorCode);
        Assert.Equal(1, tool.Calls);
    }

    [Fact]
    public async Task WithKey_IsRepeated_WithTheSameKey()
    {
        var tool = new CountingTool(Spec("pay", ToolAction.Irreversible, ToolIdempotency.WithKey),
            (_, _) => CountingTool.Failed(), (_, _) => CountingTool.Ok("оплачено"));
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "pay");
        ToolResult second = Kit.Result(await Call(gate, "pay"));
        ToolResult third = Kit.Result(await Call(gate, "pay"));

        Assert.Equal(2, tool.Calls);
        Assert.Equal(tool.Received[0].EffectKey, tool.Received[1].EffectKey);
        Assert.Equal("оплачено", second.Text);
        Assert.Equal(second, third);
    }

    [Fact]
    public async Task WithKey_DifferentArguments_IsANewEffect()
    {
        var tool = new CountingTool(Spec("pay", ToolAction.Irreversible, ToolIdempotency.WithKey), (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "pay", """{"q":"100"}""");
        await Call(gate, "pay", """{"q":"200"}""");

        Assert.Equal(2, tool.Calls);
        Assert.NotEqual(tool.Received[0].EffectKey, tool.Received[1].EffectKey);
        Assert.Equal(2, gate.Journal.Count);
    }

    [Fact]
    public async Task Idempotent_AfterFailure_IsRepeated()
    {
        var tool = new CountingTool(Spec("delete", ToolAction.Irreversible, ToolIdempotency.Idempotent),
            (_, _) => CountingTool.Failed(), (_, _) => CountingTool.Ok("удалено"));
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "delete");
        Assert.Equal("удалено", Kit.Result(await Call(gate, "delete")).Text);
        Assert.Equal(2, tool.Calls);
    }

    [Fact]
    public async Task ReadAndCompute_PassWithoutJournal()
    {
        var read = new CountingTool(Spec("look", ToolAction.Read, null), (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(read));

        await Call(gate, "look");
        await Call(gate, "look");

        Assert.Equal(2, read.Calls);
        Assert.Empty(gate.Journal);
        Assert.NotNull(read.Received[0].EffectKey);
    }

    [Fact]
    public async Task AwaitsUser_IsNotJournaled_SoTheCallCanBeRepeated()
    {
        var tool = new CountingTool(Spec("send", ToolAction.Irreversible, ToolIdempotency.NotIdempotent),
            (_, _) => new ToolResult
            {
                Status = ToolStatus.AwaitsUser,
                Text = "нужно согласие",
                Origin = new Origin { Kind = OriginKind.Trusted },
                Question = new() { Id = "q1", Kind = Contracts.Engines.QuestionKind.Confirm, Text = "Отправить?", AskedBy = "run-42" },
            },
            (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(tool));

        Assert.Equal(ToolStatus.AwaitsUser, Kit.Result(await Call(gate, "send")).Status);
        Assert.Empty(gate.Journal);
        Assert.Equal(ToolStatus.Ok, Kit.Result(await Call(gate, "send")).Status);
        Assert.Equal(2, tool.Calls);
    }

    [Fact]
    public async Task DifferentRuns_AreDifferentEffects()
    {
        var tool = new CountingTool(Spec("send", ToolAction.Irreversible, ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "send", runId: "run-1");
        await Call(gate, "send", runId: "run-2");

        Assert.Equal(2, tool.Calls);
    }

    [Fact]
    public async Task UnknownTool_GoesToInnerSet()
    {
        var gate = new EffectGate(new ListToolSet());
        Assert.Equal(ToolStatus.Failed, Kit.Result(await Call(gate, "nope")).Status);
        Assert.Empty(gate.Journal);
    }

    /// <summary>Набор, который начинает вызов и обрывает его отменой до итога.</summary>
    private sealed class InterruptingSet(IToolSet inner) : IToolSet
    {
        public IReadOnlyList<ToolSpec> Available => inner.Available;

        public async IAsyncEnumerable<ToolUpdate> CallAsync(ToolCall call, ToolContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var cts = new CancellationTokenSource();
            await using IAsyncEnumerator<ToolUpdate> e = inner.CallAsync(call, context, cts.Token).GetAsyncEnumerator(cts.Token);
            await cts.CancelAsync();
            await e.MoveNextAsync();
            yield return e.Current;
        }
    }
}

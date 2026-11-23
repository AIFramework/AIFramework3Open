using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Гейт эффектов, сессия С2: отказ до действия снимает запись (код из словаря <see cref="ToolCodes"/>), прежний журнал
/// запуска продолжается, о каждом изменении журнала гейт сообщает.
/// </summary>
public sealed class EffectGateJournalTests
{
    private const string Run = "run-42";

    private static ToolSpec Spec(string name, ToolIdempotency? idempotency) =>
        Kit.Spec(name, ToolAction.Irreversible) with { Idempotency = idempotency };

    private static Task<List<ToolUpdate>> Call(IToolSet set, string name) =>
        Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "call-" + Guid.NewGuid().ToString("N"), Name = name, Arguments = Kit.Json("""{"q":"x"}""") },
            Kit.Context() with { RunId = Run }));

    private static string Key(string name) => EffectKeys.Create(Run, name, Kit.Json("""{"q":"x"}"""));

    private static ToolResult Coded(ToolStatus status, string code, string? handle = null) => new()
    {
        Status = status,
        Text = code,
        Origin = new Origin { Kind = OriginKind.Trusted },
        ErrorCode = code,
        Handle = handle,
    };

    public static TheoryData<string> NotPerformedCodes()
    {
        var data = new TheoryData<string>();
        foreach (string code in ToolCodes.All.Where(c => ToolCodes.NotPerformed(c)))
            data.Add(code);
        return data;
    }

    [Theory]
    [MemberData(nameof(NotPerformedCodes))]
    public async Task FailureBeforeAction_RemovesRecord_SoTheCallRunsAgain(string code)
    {
        var tool = new CountingTool(Spec("send", ToolIdempotency.NotIdempotent),
            (_, _) => Coded(ToolStatus.Failed, code),
            (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(tool));

        Assert.Equal(code, Kit.Result(await Call(gate, "send")).ErrorCode);
        Assert.Empty(gate.Journal);

        Assert.Equal(ToolStatus.Ok, Kit.Result(await Call(gate, "send")).Status);
        Assert.Equal(2, tool.Calls);
    }

    [Theory]
    [InlineData(ToolCodes.UnknownResult)]
    [InlineData(ToolCodes.RepeatUnsafe)]
    [InlineData("remote_error")]
    public async Task FailureOnTheMerits_IsKept_AndNotRepeated(string code)
    {
        var tool = new CountingTool(Spec("send", ToolIdempotency.NotIdempotent), (_, _) => Coded(ToolStatus.Failed, code));
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "send");
        ToolResult repeat = Kit.Result(await Call(gate, "send"));

        Assert.Equal(1, tool.Calls);
        Assert.Equal(EffectGateCodes.RepeatUnsafe, repeat.ErrorCode);
    }

    [Fact]
    public async Task TransientAfterUnknown_RestoresUnknown_NotForgetsIt()
    {
        var tool = new CountingTool(Spec("pay", ToolIdempotency.WithKey),
            (_, _) => Coded(ToolStatus.Unknown, ToolCodes.UnknownResult),
            (_, _) => Coded(ToolStatus.Failed, ToolCodes.Transient));
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "pay");
        await Call(gate, "pay");

        // Временный отказ повтора не стирает того, что первый вызов мог состояться.
        Assert.Equal(ToolStatus.Unknown, Assert.Single(gate.Journal).Result.Status);
    }

    [Fact]
    public async Task Pending_IsKept_AndRepeatReturnsTheHandle_WithoutStartingAgain()
    {
        var tool = new CountingTool(Spec("render", ToolIdempotency.NotIdempotent),
            (_, _) => Coded(ToolStatus.Ok, ToolCodes.Pending, handle: "job-7"));
        var gate = new EffectGate(new ListToolSet(tool));

        await Call(gate, "render");
        ToolResult repeat = Kit.Result(await Call(gate, "render"));

        Assert.Equal(1, tool.Calls);
        Assert.Equal(ToolCodes.Pending, repeat.ErrorCode);
        Assert.Equal("job-7", repeat.Handle);
    }

    [Fact]
    public async Task PreviousJournal_DoneKey_ReturnsStoredResult_WithoutCallingTool()
    {
        var tool = new CountingTool(Spec("send", ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok("второй раз"));
        EffectRecord done = new() { Key = Key("send"), Tool = "send", Result = CountingTool.Ok("письмо ушло") };
        var gate = new EffectGate(new ListToolSet(tool), [done]);

        ToolResult result = Kit.Result(await Call(gate, "send"));

        Assert.Equal(0, tool.Calls);
        Assert.Equal("письмо ушло", result.Text);
    }

    [Theory]
    [InlineData(ToolIdempotency.NotIdempotent, 0, ToolStatus.Failed)]
    [InlineData(ToolIdempotency.Idempotent, 1, ToolStatus.Ok)]
    [InlineData(ToolIdempotency.WithKey, 1, ToolStatus.Ok)]
    public async Task PreviousJournal_UnknownOutcome_RepeatsOnlyWhenDeclaredSafe(
        ToolIdempotency idempotency, int calls, ToolStatus status)
    {
        var tool = new CountingTool(Spec("send", idempotency), (_, _) => CountingTool.Ok());
        EffectRecord lost = new() { Key = Key("send"), Tool = "send", Result = Coded(ToolStatus.Unknown, ToolCodes.UnknownResult) };
        var gate = new EffectGate(new ListToolSet(tool), [lost]);

        ToolResult result = Kit.Result(await Call(gate, "send"));

        Assert.Equal(calls, tool.Calls);
        Assert.Equal(status, result.Status);
        if (status == ToolStatus.Failed)
            Assert.Equal(EffectGateCodes.RepeatUnsafe, result.ErrorCode);
    }

    [Fact]
    public void PreviousJournal_WithDuplicateKey_IsRejected()
    {
        EffectRecord record = new() { Key = "k", Tool = "send", Result = CountingTool.Ok() };

        Assert.Throws<ArgumentException>(() => new EffectGate(new ListToolSet(), [record, record]));
    }

    [Fact]
    public async Task JournalChanged_ReportsStartAndOutcome_AndLastSnapshotIsTheJournal()
    {
        var tool = new CountingTool(Spec("send", ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok());
        var gate = new EffectGate(new ListToolSet(tool));
        var snapshots = new List<IReadOnlyList<EffectRecord>>();
        gate.JournalChanged += snapshots.Add;

        await Call(gate, "send");

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(ToolStatus.Unknown, Assert.Single(snapshots[0]).Result.Status);
        Assert.Equal(gate.Journal, snapshots[^1]);
    }

    [Fact]
    public async Task JournalChanged_ReportsRemoval_OfARefusedCall()
    {
        var tool = new CountingTool(Spec("send", ToolIdempotency.NotIdempotent), (_, _) => Coded(ToolStatus.Failed, ToolCodes.Transient));
        var gate = new EffectGate(new ListToolSet(tool));
        IReadOnlyList<EffectRecord>? last = null;
        gate.JournalChanged += j => last = j;

        await Call(gate, "send");

        Assert.NotNull(last);
        Assert.Empty(last);
    }

    [Fact]
    public async Task JournalChanged_HandlerFailure_IsLogged_AndDoesNotBreakTheCall()
    {
        var tool = new CountingTool(Spec("send", ToolIdempotency.NotIdempotent), (_, _) => CountingTool.Ok());
        var log = new CapturingLog();
        var gate = new EffectGate(new ListToolSet(tool), log.Logger);
        gate.JournalChanged += _ => throw new IOException("диск полон");

        ToolResult result = Kit.Result(await Call(gate, "send"));

        Assert.Equal(ToolStatus.Ok, result.Status);
        Assert.Contains(log.All, line => line.Contains("JournalChanged", StringComparison.Ordinal));
    }
}

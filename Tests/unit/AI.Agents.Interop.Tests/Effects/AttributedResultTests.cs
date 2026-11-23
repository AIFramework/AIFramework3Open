using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.LLM.Agents.Tools;
using Xunit;
using LlmToolResult = AI.LLM.Agents.Multimodal.ToolResult;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Итог инструмента [AgentTool] под контрактом, сессия С2: строка не считается удачей молча (правило
/// <see cref="ToolTextResult"/> выбирается явно), итог контракта проходит целиком, исключение — сбой.
/// </summary>
public sealed class AttributedResultTests
{
    private static Task<List<ToolUpdate>> Call(IToolSet set, string name, string arguments = "{}") =>
        Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "c-" + Guid.NewGuid().ToString("N"), Name = name, Arguments = Kit.Json(arguments) },
            Kit.Context()));

    [Fact]
    public async Task ContractResult_PassesWhole_StatusCodeAndHandle()
    {
        IToolSet set = AttributedTools.AsToolSet([new Typed()], ToolTextResult.Refused);

        ToolResult moved = Kit.Result(await Call(set, "move_file"));
        ToolResult render = Kit.Result(await Call(set, "render_video"));

        Assert.Equal(ToolStatus.Failed, moved.Status);
        Assert.Equal(ToolCodes.NotFound, moved.ErrorCode);
        Assert.Equal(ToolStatus.Ok, render.Status);
        Assert.Equal(ToolCodes.Pending, render.ErrorCode);
        Assert.Equal("job-1", render.Handle);
        Assert.Equal(OriginKind.Trusted, render.Origin.Kind);
    }

    [Fact]
    public async Task LlmResult_WithFailure_IsFailed_AndExceptionIsFailed()
    {
        IToolSet set = AttributedTools.AsToolSet([new Typed()], ToolTextResult.Refused);

        Assert.Equal(ToolStatus.Failed, Kit.Result(await Call(set, "write_file")).Status);
        Assert.Equal(ToolStatus.Failed, Kit.Result(await Call(set, "explode")).Status);
    }

    [Fact]
    public void Refused_StringTool_IsAConfigurationError_NamingTheTool()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            AttributedTools.AsToolSet([new Typed(), new Texty()], ToolTextResult.Refused));

        Assert.Contains("read_note", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("move_file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Success_StringIsOk_AsBefore()
    {
        IToolSet set = AttributedTools.AsToolSet([new Texty()], ToolTextResult.Success);

        ToolResult result = Kit.Result(await Call(set, "read_note"));

        Assert.Equal(ToolStatus.Ok, result.Status);
        Assert.Equal("файла нет", result.Text);
    }

    [Fact]
    public void Unspecified_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AttributedTools.AsToolSet([new Typed()], ToolTextResult.Unspecified));

    [Fact]
    public async Task ContractResult_TransientThroughGate_LeavesNoRecord()
    {
        var gate = new EffectGate(AttributedTools.AsToolSet([new Typed()], ToolTextResult.Refused));

        await Call(gate, "post_message");

        Assert.Empty(gate.Journal);
    }

    private sealed class Typed
    {
        [AgentTool("move_file", "Переносит файл")]
        [ToolEffect(ToolAction.Reversible, ToolIdempotency.NotIdempotent)]
        public ToolResult Move() => Result(ToolStatus.Failed, ToolCodes.NotFound, "файла нет");

        [AgentTool("render_video", "Начинает рендер")]
        [ToolEffect(ToolAction.Reversible, ToolIdempotency.NotIdempotent)]
        public Task<ToolResult> Render() =>
            Task.FromResult(Result(ToolStatus.Ok, ToolCodes.Pending, "рендер начат") with { Handle = "job-1" });

        [AgentTool("post_message", "Отправляет сообщение")]
        [ToolEffect(ToolAction.Irreversible, ToolIdempotency.NotIdempotent)]
        public ToolResult Post() => Result(ToolStatus.Failed, ToolCodes.Transient, "429");

        [AgentTool("write_file", "Пишет файл")]
        [ToolEffect(ToolAction.Reversible, ToolIdempotency.Idempotent)]
        public LlmToolResult Write() => LlmToolResult.Failure("нет прав на папку");

        [AgentTool("explode", "Падает")]
        [ToolEffect(ToolAction.Compute, ToolIdempotency.Idempotent)]
        public void Explode() => throw new InvalidOperationException("сбой");

        private static ToolResult Result(ToolStatus status, string code, string text) => new()
        {
            Status = status,
            Text = text,
            Origin = new Origin { Kind = OriginKind.Trusted },
            ErrorCode = code,
        };
    }

    private sealed class Texty
    {
        [AgentTool("read_note", "Читает заметку")]
        [ToolEffect(ToolAction.Read, ToolIdempotency.Idempotent)]
        public string Read() => "файла нет";
    }
}

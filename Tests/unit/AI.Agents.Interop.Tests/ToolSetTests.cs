using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// IReActToolSource ↔ IToolSet: доступные инструменты, поиск по имени с учетом регистра, контекст вызова.
/// </summary>
public sealed class ToolSetTests
{
    [Fact]
    public async Task UnknownName_GivesFailedWithUnknownTool()
    {
        IToolSet set = new FreshSource("search").AsToolSet(log: new CapturingLog().Logger);

        List<ToolUpdate> updates = await Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "1", Name = "missing", Arguments = Kit.Json("{}") }, Kit.Context()));

        ToolResult result = Kit.Result(updates);
        Assert.Equal(ToolStatus.Failed, result.Status);
        Assert.Equal(ReActInteropCodes.UnknownTool, result.ErrorCode);
    }

    [Fact]
    public async Task Name_IsCaseSensitiveLikeToolSpec()
    {
        IToolSet set = new FreshSource("search").AsToolSet(log: new CapturingLog().Logger);

        ToolResult result = Kit.Result(await Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "1", Name = "Search", Arguments = Kit.Json("{}") }, Kit.Context())));

        Assert.Equal(ReActInteropCodes.UnknownTool, result.ErrorCode);
    }

    [Fact]
    public async Task Call_FindsToolAndBuildsRunContext()
    {
        var source = new FreshSource("search", "fetch");
        var baseRun = new ReActRunContext("что такое ReAct");
        IToolSet set = source.AsToolSet(baseRun, log: new CapturingLog().Logger);

        Assert.Equal(["search", "fetch"], set.Available.Select(s => s.Name));

        var context = Kit.Context(tier: 5);
        ToolResult result = Kit.Result(await Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "c7", Name = "fetch", Arguments = Kit.Json("{\"input\":\"адрес\"}") }, context)));

        Assert.Equal(ToolStatus.Ok, result.Status);
        Assert.Equal("fetch:адрес", result.Text);

        ReActToolInvocation invocation = Assert.Single(source.Invocations);
        Assert.Equal("c7", invocation.ActionId);
        Assert.Equal("что такое ReAct", invocation.Run.Query);
        Assert.Equal(5, invocation.Run.StepNumber);
        Assert.Same(context, invocation.Run.TagAs<ToolContext>());
        Assert.Equal(5, source.Contexts[^1].StepNumber);
    }

    [Fact]
    public void BaseRunWithTag_IsRejected()
    {
        var run = new ReActRunContext("запрос", tag: "сессия");

        Assert.Throws<ArgumentException>(() => new FreshSource("search").AsToolSet(run));
    }

    [Fact]
    public async Task ToolSource_OverSet_CallsThroughSetAndBack()
    {
        var log = new CapturingLog();
        IToolSet set = new FreshSource("search").AsToolSet(log: log.Logger);
        IReActToolSource source = set.AsToolSource(log.Logger);

        IReActTool tool = Assert.Single(source.GetTools(new ReActRunContext("q")));
        Assert.Equal("search", tool.Name);
        Assert.Contains("\"input\"", tool.ParametersJsonSchema);

        ReActToolOutcome outcome = Kit.Outcome(await Kit.Execute(tool, "кот", new ReActRunContext("q", stepNumber: 2)));

        Assert.True(outcome.Ok);
        Assert.Equal("search:кот", outcome.Observation);
    }

    [Fact]
    public async Task ToolSource_OverSet_UnknownToolStaysUnknownTool()
    {
        var set = new FixedSet(Kit.Spec("ghost"));
        IReActTool tool = Assert.Single(set.AsToolSource(new CapturingLog().Logger).GetTools(new ReActRunContext("q")));

        ReActToolOutcome outcome = Kit.Outcome(await Kit.Execute(tool, "{\"q\":\"x\"}"));

        Assert.False(outcome.Ok);
        Assert.Equal(ReActInteropCodes.UnknownTool, outcome.ErrorCode);
    }

    /// <summary>Набор, который объявляет инструмент, но на вызов отвечает «не найден».</summary>
    private sealed class FixedSet(ToolSpec spec) : IToolSet
    {
        public IReadOnlyList<ToolSpec> Available { get; } = [spec];

        public async IAsyncEnumerable<ToolUpdate> CallAsync(
            ToolCall call, ToolContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ToolUpdate.Final
            {
                Result = new ToolResult
                {
                    Status = ToolStatus.Failed,
                    Text = "инструмент не найден",
                    Origin = new Origin { Kind = OriginKind.Trusted },
                    ErrorCode = ReActInteropCodes.UnknownTool,
                },
            };
        }
    }
}

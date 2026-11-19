using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Статусы контракта, которых старый движок не знает: Ok = false, свой код и причина в Observation.
/// </summary>
public sealed class StatusTests
{
    [Theory]
    [InlineData(ToolStatus.Denied, ReActInteropCodes.Denied)]
    [InlineData(ToolStatus.NeedsInput, ReActInteropCodes.NeedsInput)]
    [InlineData(ToolStatus.AwaitsUser, ReActInteropCodes.AwaitsUser)]
    [InlineData(ToolStatus.Unknown, ReActInteropCodes.UnknownStatus)]
    public async Task InexpressibleStatus_GivesOwnErrorCodeAndNotOk(ToolStatus status, string expectedCode)
    {
        var tool = new FakeTool(Kit.Spec("gate_tool"), new ToolUpdate.Final
        {
            Result = new ToolResult
            {
                Status = status,
                Text = "причина " + status,
                Origin = new Origin { Kind = OriginKind.Trusted },
                ErrorCode = "own_code",
            },
        });

        ReActToolOutcome outcome = Kit.Outcome(await Kit.Execute(tool.AsReActTool(new CapturingLog().Logger), "{\"q\":\"x\"}"));

        Assert.False(outcome.Ok);
        Assert.Equal(expectedCode, outcome.ErrorCode);
        Assert.Contains("причина " + status, outcome.Observation);
        Assert.Contains("own_code", outcome.Observation);
    }

    [Fact]
    public void InexpressibleStatuses_HaveDistinctCodes()
    {
        string[] codes = [.. Enum.GetValues<ToolStatus>()
            .Where(s => s is not (ToolStatus.Ok or ToolStatus.Failed))
            .Select(s => Translate(new ToolResult { Status = s, Text = "", Origin = new Origin { Kind = OriginKind.Unknown } }).ErrorCode!)];

        Assert.Equal(4, codes.Length);
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    [Theory]
    [InlineData(ToolStatus.NeedsInput, ReActInteropCodes.NeedsInput)]
    [InlineData(ToolStatus.AwaitsUser, ReActInteropCodes.AwaitsUser)]
    public void Question_IsVisibleInErrorCodeAndObservation(ToolStatus status, string expectedCode)
    {
        ReActToolOutcome outcome = Translate(new ToolResult
        {
            Status = status,
            Text = "",
            Origin = new Origin { Kind = OriginKind.Trusted },
            Question = new Question { Id = "q1", Kind = QuestionKind.Unknown, Text = "Какой город?", AskedBy = "weather" },
        });

        Assert.False(outcome.Ok);
        Assert.Equal(expectedCode, outcome.ErrorCode);
        Assert.Contains("Какой город?", outcome.Observation);
    }

    [Fact]
    public void Failed_KeepsOwnErrorCode()
    {
        ReActToolOutcome outcome = Translate(new ToolResult
        {
            Status = ToolStatus.Failed,
            Text = "таймаут",
            Origin = new Origin { Kind = OriginKind.Trusted },
            ErrorCode = "timeout",
        });

        Assert.False(outcome.Ok);
        Assert.Equal("timeout", outcome.ErrorCode);
        Assert.Equal("таймаут", outcome.Observation);
    }

    [Fact]
    public void Raised_IsNotCarriedButWarned()
    {
        var log = new CapturingLog();

        ReActToolOutcome outcome = new ToolResult
        {
            Status = ToolStatus.Ok,
            Text = "нажато",
            Origin = new Origin { Kind = OriginKind.Trusted },
            Raised = ToolAction.Irreversible,
        }.ToOutcome(log.Logger);

        Assert.True(outcome.Ok);
        Assert.Contains(log.Warnings, w => w.Contains("Irreversible"));
    }

    private static ReActToolOutcome Translate(ToolResult result) => result.ToOutcome(new CapturingLog().Logger);
}

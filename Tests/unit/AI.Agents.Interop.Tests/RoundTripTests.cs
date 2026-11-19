using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;
using AgentImage = AI.LLM.Agents.Multimodal.AgentImage;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Круги перевода: старый инструмент → контракт → старый и контракт → старый → контракт.
/// </summary>
public sealed class RoundTripTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ReActTool_ThroughContractAndBack_KeepsEveryOutcomeField(bool ok, bool endsTurn)
    {
        var outcome = new ReActToolOutcome
        {
            Ok = ok,
            Observation = "итог",
            Citations = [new ReActCitation("https://a.example/1", "Первый", "фрагмент 1"), new ReActCitation("https://b.example/2")],
            Images = [new AgentImage(Png, "image/png", "screenshot")],
            EndsTurn = endsTurn,
            TerminalAnswer = endsTurn ? "итог" : null,
            Payload = new { Answer = 42, Items = new[] { "x", "y" } },
            ErrorCode = ok ? null : "tool_failed",
        };

        string? received = null;
        IReActTool legacy = DelegateReActTool.FromStream("legacy_tool", "старый инструмент", (invocation, _) =>
        {
            received = invocation.Arguments;
            return Stream(new ReActToolEvent.Progress("ищу", new { Percent = 50 }), new ReActToolEvent.Result(outcome));
        });

        var log = new CapturingLog();
        IReActTool back = legacy.AsTool(ToolAction.Compute, log.Logger).AsReActTool(log.Logger);
        List<ReActToolEvent> events = await Kit.Execute(back, "простой аргумент");

        // Простая строка прошла через {"input": "…"} и развернулась обратно.
        Assert.Equal("простой аргумент", received);

        var progress = Assert.IsType<ReActToolEvent.Progress>(events[0]);
        Assert.Equal("ищу", progress.Message);
        Assert.Equal("{\"Percent\":50}", Assert.IsType<JsonElement>(progress.Payload).GetRawText());

        ReActToolOutcome result = Kit.Outcome(events);
        Assert.Equal(outcome.Ok, result.Ok);
        Assert.Equal(outcome.Observation, result.Observation);
        Assert.Equal(outcome.EndsTurn, result.EndsTurn);
        Assert.Equal(outcome.TerminalAnswer, result.TerminalAnswer);
        Assert.Equal(outcome.ErrorCode, result.ErrorCode);
        Assert.Equal(JsonSerializer.Serialize(outcome.Payload), Assert.IsType<JsonElement>(result.Payload).GetRawText());

        Assert.Equal(outcome.Citations.Count, result.Citations.Count);
        for (int i = 0; i < outcome.Citations.Count; i++)
        {
            Assert.Equal(outcome.Citations[i].Url, result.Citations[i].Url);
            Assert.Equal(outcome.Citations[i].Title, result.Citations[i].Title);
            Assert.Equal(outcome.Citations[i].Snippet, result.Citations[i].Snippet);
        }

        AgentImage image = Assert.Single(result.Images);
        Assert.Equal(Png, image.Data);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal("screenshot", image.Label);

        // Круг без потерь — без предупреждений.
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public async Task ReActTool_ToContract_FillsEveryResultField()
    {
        var outcome = ReActToolOutcome.Terminal("ответ", payload: new { A = 1 },
            citations: [new ReActCitation("https://a.example", "A", "цитата")]);
        IReActTool legacy = DelegateReActTool.FromOutcome("final_tool", "финал", (_, _) => Task.FromResult(outcome));

        ToolResult result = Kit.Result(await Kit.Call(legacy.AsTool(ToolAction.Compute, new CapturingLog().Logger), "{}"));

        Assert.Equal(ToolStatus.Ok, result.Status);
        Assert.Equal("ответ", result.Text);
        Assert.True(result.EndsRun);
        Assert.Equal(OriginKind.Unknown, result.Origin.Kind);
        Assert.Null(result.Raised);
        Assert.Null(result.Question);
        Assert.Null(result.Files);
        Assert.Equal("{\"A\":1}", result.Data!.Value.GetRawText());
        Citation citation = Assert.Single(result.Evidence!);
        Assert.Equal(CitationKind.Web, citation.Kind);
        Assert.Equal("https://a.example", citation.Url);
        Assert.Equal("A", citation.Title);
        Assert.Equal("цитата", citation.Quote);
        Assert.False(citation.Verified);
    }

    [Fact]
    public async Task Tool_ThroughReActAndBack_KeepsOkResultFields()
    {
        var result = new ToolResult
        {
            Status = ToolStatus.Ok,
            Text = "текст для модели",
            Origin = new Origin { Kind = OriginKind.External },
            ErrorCode = "partial",
            Data = Kit.Json("{\"rows\":[1,2,3]}"),
            Files = [new ToolFile { ContentType = "image/png", Name = "chart", Content = Png, ShowToModel = true }],
            Evidence = [new Citation { Kind = CitationKind.Web, Title = "Источник", Quote = "цитата", Url = "https://s.example" }],
            EndsRun = true,
        };

        var tool = new FakeTool(Kit.Spec("contract_tool"),
            new ToolUpdate.Progress { Message = "шаг", Data = Kit.Json("{\"n\":1}") },
            new ToolUpdate.Final { Result = result });

        var log = new CapturingLog();
        ITool back = tool.AsReActTool(log.Logger).AsTool(ToolAction.Compute, log.Logger);
        ToolContext context = Kit.Context();
        List<ToolUpdate> updates = await Kit.Call(back, "{\"q\":\"найти\"}", context);

        // Вызов дошел до инструмента с теми же аргументами, идентификатором и контекстом.
        Assert.Equal("{\"q\":\"найти\"}", tool.LastCall!.Arguments.GetRawText());
        Assert.Equal("call-1", tool.LastCall.Id);
        Assert.Same(context, tool.LastContext);

        var progress = Assert.IsType<ToolUpdate.Progress>(updates[0]);
        Assert.Equal("шаг", progress.Message);
        Assert.Equal("{\"n\":1}", progress.Data!.Value.GetRawText());

        ToolResult final = Kit.Result(updates);
        Assert.Equal(ToolStatus.Ok, final.Status);
        Assert.Equal(result.Text, final.Text);
        Assert.Equal(result.ErrorCode, final.ErrorCode);
        Assert.Equal(result.Data!.Value.GetRawText(), final.Data!.Value.GetRawText());
        Assert.True(final.EndsRun);

        ToolFile file = Assert.Single(final.Files!);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("chart", file.Name);
        Assert.Equal(Png, file.Content);
        Assert.True(file.ShowToModel);

        Citation citation = Assert.Single(final.Evidence!);
        Assert.Equal("https://s.example", citation.Url);
        Assert.Equal("Источник", citation.Title);
        Assert.Equal("цитата", citation.Quote);
        Assert.Equal(CitationKind.Web, citation.Kind);

        Assert.Empty(log.Warnings);
    }

    [Fact]
    public async Task Payload_NotSerializable_ThrowsClearError()
    {
        var loop = new Loop();
        loop.Self = loop;
        IReActTool legacy = DelegateReActTool.FromOutcome("loop_tool", "петля",
            (_, _) => Task.FromResult(new ReActToolOutcome { Ok = true, Observation = "x", Payload = loop }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Kit.Call(legacy.AsTool(ToolAction.Compute), "{}"));

        Assert.Contains("loop_tool", error.Message);
        Assert.Contains("Payload", error.Message);
        Assert.Contains(nameof(Loop), error.Message);
    }

    [Fact]
    public async Task TerminalAnswer_DifferentFromObservation_GoesToTextWithWarning()
    {
        IReActTool legacy = DelegateReActTool.FromOutcome("final_tool", "финал",
            (_, _) => Task.FromResult(ReActToolOutcome.Terminal("ответ", observation: "след")));
        var log = new CapturingLog();

        ToolResult result = Kit.Result(await Kit.Call(legacy.AsTool(ToolAction.Compute, log.Logger), "{}"));

        Assert.Equal("ответ", result.Text);
        Assert.True(result.EndsRun);
        Assert.Contains(log.Warnings, w => w.Contains("Observation не переносится"));
    }

    private static async IAsyncEnumerable<ReActToolEvent> Stream(
        ReActToolEvent first, ReActToolEvent second, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return first;
        yield return second;
    }

    /// <summary>Значение со ссылкой на себя: System.Text.Json его не сериализует.</summary>
    private sealed class Loop
    {
        public Loop? Self { get; set; }
    }
}

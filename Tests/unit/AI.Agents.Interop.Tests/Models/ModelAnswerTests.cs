using System.Text.Json;
using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Responses;
using Xunit;
using LlmToolCall = AI.LLM.Core.Models.Common.ToolCalling.ToolCall;
using FunctionCall = AI.LLM.Core.Models.Common.ToolCalling.FunctionCall;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>Ответ старой формы в итоге контракта: статус, расход, вызовы инструментов, потери.</summary>
public sealed class ModelAnswerTests
{
    private static ChatCompletionsResponse Response(
        string? content, string finish = "stop", string? native = null, List<LlmToolCall>? calls = null, string? refusal = null) => new()
        {
            Model = "provider/model-1",
            Choices =
            [
                new Choice
                {
                    Message = new LLMMessage { Role = "assistant", Content = content, ToolCalls = calls, Refusal = refusal, Reasoning = "думаю" },
                    FinishReason = finish,
                    NativeFinishReason = native,
                },
            ],
            Usage = new Usage { PromptTokens = 120, CompletionTokens = 30, Cost = Kit.Json("""{"total_cost":0.0042}""") },
        };

    [Fact]
    public void Usage_ComesInAnswer()
    {
        LlmAnswer answer = Response("готово").ToAnswer();

        Assert.Equal(LlmStatus.Ok, answer.Status);
        Assert.Equal("готово", answer.Text);
        Assert.Equal("думаю", answer.Reasoning);
        Assert.Equal("provider/model-1", answer.Model);
        Assert.Equal(new ModelUsage { InputTokens = 120, OutputTokens = 30, Cost = 0.0042m }, answer.Usage);
    }

    [Fact]
    public async Task Usage_ComesThroughPort()
    {
        var client = new FakeClient { Respond = () => Response("x") };

        LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities()).AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")));

        Assert.Equal(150, answer.Usage!.InputTokens + answer.Usage.OutputTokens);
        Assert.Equal(0.0042m, answer.Usage.Cost);
    }

    [Fact]
    public void ZeroUsage_IsNotReported()
    {
        Assert.Null(new Usage().ToUsage());
        Assert.Null(((Usage?)null).ToUsage());
        Assert.Equal(0.5m, new Usage { Cost = 0.5m }.ToUsage()!.Cost);
    }

    /// <summary>
    /// Число CLR в Cost не зависит от культуры: под ru-RU дробь пишется через запятую, и прежний разбор строкой
    /// превращал 0,5 в 5.
    /// </summary>
    [Fact]
    public void NumericCost_IsCultureIndependent()
    {
        System.Globalization.CultureInfo saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");

            Assert.Equal(0.5m, new Usage { PromptTokens = 1, Cost = 0.5m }.ToUsage()!.Cost);
            Assert.Equal(0.25m, new Usage { PromptTokens = 1, Cost = 0.25d }.ToUsage()!.Cost);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Usage_RoundTrip_KeepsCost()
    {
        var usage = new ModelUsage { InputTokens = 10, OutputTokens = 5, Cost = 0.000123m };

        Usage old = usage.ToLlmUsage();

        Assert.Equal(15, old.TotalTokens);
        Assert.Equal(0.000123m, CostExtractor.TryExtract(old.Cost));
        Assert.Equal(usage, old.ToUsage());
    }

    [Theory]
    [InlineData("length", null)]
    [InlineData("stop", "MAX_TOKENS")]
    public void Length_IsTruncated(string finish, string? native) =>
        Assert.Equal(LlmStatus.Truncated, Response("полу", finish, native).ToAnswer().Status);

    [Fact]
    public void NothingSaid_IsEmptyWithReasoningKept()
    {
        LlmAnswer answer = Response("").ToAnswer();

        Assert.Equal(LlmStatus.Empty, answer.Status);
        Assert.Null(answer.Text);
        Assert.Equal("думаю", answer.Reasoning);
    }

    [Fact]
    public void NoChoices_IsEmpty() =>
        Assert.Equal(LlmStatus.Empty, new ChatCompletionsResponse().ToAnswer().Status);

    [Fact]
    public void ContentFilter_IsProviderRefusal_AndTextIsReportedLost()
    {
        var lost = new List<string>();

        LlmAnswer answer = Response("частично", finish: "content_filter").ToAnswer(lost);

        Assert.Equal(LlmStatus.Failed, answer.Status);
        Assert.Equal(LlmFailure.ProviderRefusal, answer.Failure);
        Assert.Null(answer.Text);
        Assert.Contains("LLMMessage.Content", lost);
    }

    [Fact]
    public void Refusal_WithoutAnswer_IsProviderRefusal()
    {
        var lost = new List<string>();

        LlmAnswer answer = Response(null, refusal: "не могу помочь").ToAnswer(lost);

        Assert.Equal(LlmFailure.ProviderRefusal, answer.Failure);
        // Текст отказа едет рядом с кодом, а не теряется.
        Assert.Equal("не могу помочь", answer.FailureText);
        Assert.DoesNotContain("LLMMessage.Refusal", lost);
    }

    [Fact]
    public void ToolCalls_AreParsed_BadJsonStaysAsString()
    {
        var lost = new List<string>();
        List<LlmToolCall> calls =
        [
            new() { Id = "c1", Function = new FunctionCall { Name = "search", Arguments = "{\"q\":\"кот\"}" } },
            new() { Id = "c2", Function = new FunctionCall { Name = "fetch", Arguments = "{не json" } },
            new() { Id = null, Function = new FunctionCall { Name = "now", Arguments = "" } },
        ];

        LlmAnswer answer = Response(null, "tool_calls", calls: calls).ToAnswer(lost);

        Assert.Equal(LlmStatus.Ok, answer.Status);
        Assert.Null(answer.Text);
        Assert.Equal("кот", answer.ToolCalls![0].Arguments.GetProperty("q").GetString());
        Assert.Equal(JsonValueKind.String, answer.ToolCalls[1].Arguments.ValueKind);
        Assert.Equal("{не json", answer.ToolCalls[1].Arguments.GetString());
        Assert.Equal(JsonValueKind.Object, answer.ToolCalls[2].Arguments.ValueKind);
        Assert.Equal("", answer.ToolCalls[2].Id);
        Assert.Contains("ToolCall.Id", lost);
    }

    [Fact]
    public async Task LostFields_AreWarned()
    {
        var log = new CapturingLog();
        var client = new FakeClient
        {
            Respond = () =>
            {
                ChatCompletionsResponse response = Response("с картинкой");
                response.Choices[0].Message.Images = [new ImageInfo { Type = "image_url" }];
                response.Choices.Add(new Choice { Message = new LLMMessage("assistant", "второй") });
                return response;
            },
        };

        await client.AsModelPort(ModelKit.Capabilities(), log: log.Logger).AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")));

        Assert.Contains(log.Warnings, w => w.Contains("LLMMessage.Images"));
        Assert.Contains(log.Warnings, w => w.Contains("ChatCompletionsResponse.Choices"));
    }
}

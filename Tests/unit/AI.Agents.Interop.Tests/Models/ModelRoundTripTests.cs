using System.Text.Json;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Models;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using Xunit;
using ContractToolCall = AI.Agents.Contracts.Tools.ToolCall;
using LlmToolCall = AI.LLM.Core.Models.Common.ToolCalling.ToolCall;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Туда и обратно: вызовы инструментов и их результаты проходят порт и старый клиент; старый клиент над портом
/// над старым клиентом отдает то же, что исходный.
/// </summary>
public sealed class ModelRoundTripTests
{
    private static readonly ContractToolCall Search = new()
    {
        Id = "c1",
        Name = "search",
        Arguments = Kit.Json("""{"q":"ReAct"}"""),
    };

    [Fact]
    public void ToolCallConversation_SurvivesBothWays()
    {
        ModelMessage[] conversation =
        [
            ModelKit.Text(MessageRole.User, "найди"),
            new() { Role = MessageRole.Assistant, ToolCalls = [Search] },
            ModelKit.Text(MessageRole.Tool, "нашел три статьи") with { ToolCallId = "c1" },
        ];

        List<LLMMessage> old = ModelTranslation.ToLlmMessages(conversation);

        Assert.Null(old[1].Content);
        Assert.Equal("search", old[1].ToolCalls![0].Function.Name);
        Assert.Equal("""{"q":"ReAct"}""", old[1].ToolCalls![0].Function.Arguments);
        Assert.Equal("c1", old[2].ToolCallId);

        ModelMessage[] back = [.. old.Select(m => m.ToModelMessage())];

        Assert.Equal(conversation.Select(m => m.Role), back.Select(m => m.Role));
        ContractToolCall call = Assert.Single(back[1].ToolCalls!);
        Assert.Equal((Search.Id, Search.Name), (call.Id, call.Name));
        Assert.Equal(Search.Arguments.GetRawText(), call.Arguments.GetRawText());
        Assert.Equal("c1", back[2].ToolCallId);
        Assert.Equal("нашел три статьи", Assert.IsType<MessagePart.Text>(back[2].Parts![0]).Content);
    }

    [Fact]
    public void UnparsedArguments_GoBackAsOriginalText()
    {
        ContractToolCall call = new() { Id = "c2", Name = "fetch", Arguments = ModelTranslation.ParseArguments("{oops") };

        Assert.Equal("{oops", call.ToLlmToolCall().Function.Arguments);
    }

    [Fact]
    public async Task OldClient_OverPort_OverOldClient_GivesSameAnswer()
    {
        var inner = new FakeClient
        {
            Respond = () => new ChatCompletionsResponse
            {
                Model = "m",
                Choices =
                [
                    new Choice
                    {
                        Message = new LLMMessage
                        {
                            Role = "assistant",
                            Content = "",
                            ToolCalls = [new LlmToolCall { Id = "x1", Function = new FunctionCall { Name = "search", Arguments = """{"q":"кот"}""" } }],
                        },
                        FinishReason = "tool_calls",
                    },
                ],
                Usage = new Usage { PromptTokens = 11, CompletionTokens = 4, Cost = 0.002m },
            },
        };
        var log = new CapturingLog();
        ILLMClient outer = inner.AsModelPort(ModelKit.Capabilities(), log: log.Logger).AsLlmClient(ModelRole.Decide, log.Logger);
        var settings = new GenerateSettings
        {
            Tools = [ToolDefinition.Create("search", "поиск", Kit.QuerySchema)],
            ToolChoice = ToolChoice.Required(),
            MaxTokens = 77,
        };

        ChatCompletionsResponse response = await outer.SendFullAsync(
            [new LLMMessage("system", "правила"), new LLMMessage("user", "найди кота")], settings);

        Assert.Equal(["system", "user"], inner.Messages.Select(m => m.Role));
        Assert.Equal("required", inner.Settings!.ToolChoice.Mode);
        Assert.Equal(77, inner.Settings.MaxTokens);
        Assert.Equal("search", Assert.Single(inner.Settings.Tools!).Function.Name);

        Choice choice = Assert.Single(response.Choices);
        Assert.Equal("tool_calls", choice.FinishReason);
        LlmToolCall call = Assert.Single(choice.Message.ToolCalls!);
        Assert.Equal(("x1", "search"), (call.Id, call.Function.Name));
        Assert.Equal("кот", JsonDocument.Parse(call.Function.Arguments).RootElement.GetProperty("q").GetString());
        Assert.Equal((11, 4), (response.Usage.PromptTokens, response.Usage.CompletionTokens));
        Assert.Equal(0.002m, CostExtractor.TryExtract(response.Usage.Cost));
        Assert.Equal("m", response.Model);
    }

    [Fact]
    public void Ask_FromOldRequest()
    {
        var settings = new GenerateSettings
        {
            Tools = [ToolDefinition.Create("now", "время")],
            ToolChoice = ToolChoice.ForFunction("now"),
            ResponseFormat = ResponseFormat.CreateJsonSchema("plan", Kit.QuerySchema),
            MaxTokens = 10,
        };

        ModelAsk ask = ModelTranslation.ToAsk([new LLMMessage("user", "q")], settings, ModelRole.Judge);

        Assert.Equal(ModelRole.Judge, ask.Role);
        Assert.Equal(ToolMode.Named, ask.ToolMode);
        Assert.Equal("now", ask.ToolName);
        ToolSpec spec = Assert.Single(ask.Tools!);
        Assert.Equal(ToolAction.Unknown, spec.Action);
        Assert.Equal(JsonValueKind.Object, spec.Parameters.ValueKind);
        Assert.Equal(Kit.Json(Kit.QuerySchema).GetRawText(), ask.Schema!.Value.GetRawText());
        Assert.Equal(10, ask.MaxTokens);
    }

    [Theory]
    [InlineData(null, false, ToolMode.None)]
    [InlineData(null, true, ToolMode.Auto)]
    [InlineData("auto", true, ToolMode.Auto)]
    [InlineData("none", true, ToolMode.None)]
    [InlineData("required", true, ToolMode.Required)]
    public void ToolChoice_ToToolMode(string? choice, bool tools, ToolMode mode)
    {
        var settings = new GenerateSettings
        {
            Tools = tools ? [ToolDefinition.Create("t", "d")] : null,
            ToolChoice = choice switch { "auto" => ToolChoice.Auto(), "none" => ToolChoice.None(), "required" => ToolChoice.Required(), _ => null },
        };

        Assert.Equal(mode, ModelTranslation.ToAsk([new LLMMessage("user", "q")], settings, ModelRole.Decide).ToolMode);
    }

    [Fact]
    public async Task OldSettings_WithoutPlace_WarnOncePerClient()
    {
        var log = new CapturingLog();
        ILLMClient client = new FakePort().AsLlmClient(ModelRole.Decide, log.Logger);
        var settings = new GenerateSettings(temperature: 0.3) { TopP = 0.9, ResponseFormat = ResponseFormat.CreateJsonObject() };

        for (int i = 0; i < 3; i++)
            await client.SendAsync([new LLMMessage("user", "q")], settings);

        // Температура с С2 переводится в ModelAsk.Temperature и потерей не считается.
        Assert.Equal(0, log.Warnings.Count(w => w.Contains("GenerateSettings.Temperature")));
        Assert.Equal(1, log.Warnings.Count(w => w.Contains("GenerateSettings.TopP")));
        Assert.Equal(1, log.Warnings.Count(w => w.Contains("GenerateSettings.ResponseFormat")));
        Assert.Equal(1, log.Warnings.Count(w => w.Contains("GenerateSettings.TopK")));
    }

    [Fact]
    public async Task OldClient_SendAsync_ReturnsText()
    {
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "ответ" } };

        string text = await port.AsLlmClient(ModelRole.Write, new CapturingLog().Logger).SendAsync("вопрос");

        Assert.Equal("ответ", text);
        ModelMessage message = Assert.Single(Assert.Single(port.Asks).Messages);
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal(ToolMode.None, port.Asks[0].ToolMode);
    }

    [Fact]
    public async Task OldClient_Tokenize_IsNotSupported() =>
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            new FakePort().AsLlmClient(ModelRole.Write).TokenizeAsync([new LLMMessage("user", "q")]));

    [Fact]
    public void OldClient_WithoutRole_IsRejected() =>
        Assert.Throws<ArgumentException>(() => new FakePort().AsLlmClient(default));

    [Fact]
    public void UnknownOldRole_IsRejected() =>
        Assert.Throws<NotSupportedException>(() => new LLMMessage("developer", "x").ToModelMessage());
}

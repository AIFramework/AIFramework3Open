using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Порт модели, сессия С2: температура и уровень размышления доходят до провайдера и обратно, текст сбоя едет рядом с
/// кодом, ответ по схеме обязан быть JSON, пустой ответ отличим от сбоя.
/// </summary>
public sealed class ModelAskSettingsTests
{
    private static ModelAsk Ask() => ModelKit.Ask(ModelKit.Text(MessageRole.User, "q"));

    [Fact]
    public void Temperature_And_Reasoning_GoToSettings_AsOpenAiFields()
    {
        GenerateSettings settings = (Ask() with { Temperature = 0.15, Reasoning = ReasoningLevel.Low }).ToSettings();

        Assert.Equal(0.15, settings.Temperature);
        Assert.Equal("low", settings.ReasoningEffort);
        Assert.Null(settings.ReasoningSettings);
    }

    [Fact]
    public void Reasoning_GoesToReasoningObject_WhenPortSpeaksItsForm_WithoutTouchingPortSettings()
    {
        var portSettings = new GenerateSettings { ReasoningSettings = new ReasoningSettings { Effort = "high", MaxTokens = 2000 } };

        GenerateSettings settings = (Ask() with { Reasoning = ReasoningLevel.Minimal }).ToSettings(portSettings);

        Assert.Equal("minimal", settings.ReasoningSettings!.Effort);
        Assert.Equal(2000, settings.ReasoningSettings.MaxTokens);
        Assert.Null(settings.ReasoningEffort);
        Assert.Equal("high", portSettings.ReasoningSettings.Effort);
    }

    [Fact]
    public void NullTemperature_KeepsPortSettings()
    {
        GenerateSettings settings = Ask().ToSettings(new GenerateSettings(temperature: 0.7));

        Assert.Equal(0.7, settings.Temperature);
    }

    [Fact]
    public void OldSettings_Temperature_And_Effort_ComeBack_WithoutLoss()
    {
        var lost = new List<string>();
        // Конструктор GenerateSettings заполняет TopP, TopK и прочее значениями по умолчанию: здесь они сняты.
        var settings = new GenerateSettings
        {
            Temperature = 0.3, ReasoningEffort = "HIGH", TopP = null, TopK = null, RepetitionPenalty = null, MinTokens = null,
        };

        ModelAsk ask = ModelTranslation.ToAsk([new("user", "q")], settings, ModelRole.Decide, lost);

        Assert.Equal(0.3, ask.Temperature);
        Assert.Equal(ReasoningLevel.High, ask.Reasoning);
        Assert.Empty(lost);
    }

    [Fact]
    public void OldSettings_UnknownEffort_IsALoss_NotAGuess()
    {
        var lost = new List<string>();

        ModelAsk ask = ModelTranslation.ToAsk(
            [new("user", "q")], new GenerateSettings { ReasoningEffort = "ultra" }, ModelRole.Decide, lost);

        Assert.Null(ask.Reasoning);
        Assert.Contains("GenerateSettings.ReasoningEffort", lost);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Temperature_OutOfRange_IsRejected(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ask() with { Temperature = value });

    [Fact]
    public void Reasoning_Unknown_IsRejected_NullMeansPortChooses() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ask() with { Reasoning = ReasoningLevel.Unknown });

    [Fact]
    public async Task ClientFailure_CarriesHumanText_NextToCode()
    {
        var client = new FakeClient { Respond = () => throw LlmRequestException.NoResponse(new HttpRequestException("сокет закрыт")) };

        LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger).AskAsync(Ask());

        Assert.Equal(LlmStatus.Failed, answer.Status);
        Assert.Equal(LlmFailure.Network, answer.Failure);
        Assert.False(string.IsNullOrEmpty(answer.FailureText));
    }

    [Fact]
    public void FailureText_ReachesOldCallers_InExceptionMessage()
    {
        Exception error = new LlmAnswer
        {
            Status = LlmStatus.Failed,
            Failure = LlmFailure.RateLimit,
            FailureText = "ключ исчерпал лимит",
        }.ToException()!;

        Assert.Contains("ключ исчерпал лимит", error.Message, StringComparison.Ordinal);
        Assert.Equal(LlmFailure.RateLimit, ModelTranslation.FailureOf(error));
    }

    [Fact]
    public async Task SchemaAnswer_NotJson_IsFailedInvalidOutput_WithUsage()
    {
        var client = new FakeClient
        {
            Respond = () => new ChatCompletionsResponse("Конечно! Вот ответ: {не json")
            {
                Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 },
            },
        };

        LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger)
            .AskAsync(Ask() with { Schema = Kit.Json("""{"type":"object"}"""), ToolMode = ToolMode.None });

        Assert.Equal(LlmStatus.Failed, answer.Status);
        Assert.Equal(LlmFailure.InvalidOutput, answer.Failure);
        Assert.Null(answer.Text);
        Assert.Contains("{не json", answer.FailureText, StringComparison.Ordinal);
        Assert.Equal(15, answer.Usage!.InputTokens + answer.Usage.OutputTokens);
    }

    [Fact]
    public async Task SchemaAnswer_Json_IsOk_AndEmptyStaysEmpty()
    {
        ModelAsk ask = Ask() with { Schema = Kit.Json("""{"type":"object"}"""), ToolMode = ToolMode.None };
        var json = new FakeClient { Respond = () => new ChatCompletionsResponse("""{"ok":true}""") };
        var empty = new FakeClient { Respond = () => new ChatCompletionsResponse("") };

        Assert.Equal(LlmStatus.Ok, (await json.AsModelPort(ModelKit.Capabilities()).AskAsync(ask)).Status);
        Assert.Equal(LlmStatus.Empty, (await empty.AsModelPort(ModelKit.Capabilities()).AskAsync(ask)).Status);
    }

    [Fact]
    public void RequireJson_LeavesNonOkAnswersAlone()
    {
        var failed = new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.Access, Text = "x" };
        var truncated = new LlmAnswer { Status = LlmStatus.Truncated, Text = """{"a":""" };

        Assert.Same(failed, LlmAnswers.RequireJson(failed));
        Assert.Same(truncated, LlmAnswers.RequireJson(truncated));
    }
}

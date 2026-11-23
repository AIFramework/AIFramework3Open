using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Классификатор поверх порта, сессия С2: сбой, пустой ответ и ответ без класса различаются и не выглядят как класс;
/// расход и текст сбоя доходят до вызывающего.
/// </summary>
public sealed class ModelClassifierTests
{
    private const string Prompt = "Оцени от 0 до 9: {{input_text}}";

    private static ModelClassifier Classifier(FakePort port) =>
        new(port, ModelRole.Decide, ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"], Prompt);

    [Theory]
    [InlineData("2", "2")]
    [InlineData("2.", "2")]
    [InlineData("Ответ: 7, потому что", "7")]
    public async Task ClassFromFirstAllowedWord(string reply, string expected)
    {
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = reply } };

        Classification result = await Classifier(port).ClassifyAsync("текст");

        Assert.Equal(ClassificationStatus.Ok, result.Status);
        Assert.Equal(expected, result.Class);
    }

    [Fact]
    public async Task PortFailure_IsFailed_WithCodeAndText_NotAClass()
    {
        var port = new FakePort
        {
            Answer = new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.RateLimit, FailureText = "429" },
        };

        Classification result = await Classifier(port).ClassifyAsync("текст");

        Assert.Equal(ClassificationStatus.Failed, result.Status);
        Assert.Null(result.Class);
        Assert.Equal(LlmFailure.RateLimit, result.Answer.Failure);
        Assert.Equal("429", result.Answer.FailureText);
    }

    [Fact]
    public async Task EmptyAnswer_And_AnswerWithoutClass_AreDifferent()
    {
        var empty = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Empty } };
        var talk = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "не знаю" } };

        Assert.Equal(ClassificationStatus.Empty, (await Classifier(empty).ClassifyAsync("т")).Status);
        Assert.Equal(ClassificationStatus.NoClass, (await Classifier(talk).ClassifyAsync("т")).Status);
    }

    [Fact]
    public async Task Ask_HasZeroTemperature_NoTools_AndTheTextInPlace()
    {
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "1" } };

        await Classifier(port).ClassifyAsync("кот {{input_text}}");

        ModelAsk ask = Assert.Single(port.Asks);
        Assert.Equal(0, ask.Temperature);
        Assert.Equal(ToolMode.None, ask.ToolMode);
        var part = Assert.IsType<MessagePart.Text>(Assert.Single(Assert.Single(ask.Messages).Parts!));
        Assert.Equal("Оцени от 0 до 9: кот [input_text]", part.Content);
    }

    [Fact]
    public async Task UsageOfTheCall_IsInTheResult()
    {
        var usage = new ModelUsage { InputTokens = 30, OutputTokens = 1, Cost = 0.001m };
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "yes", Usage = usage } };

        Classification result = await new ModelClassifier(port, ModelRole.Decide, ["Yes", "No"], Prompt).ClassifyAsync("т");

        Assert.Equal("Yes", result.Class);
        Assert.Equal(usage, result.Answer.Usage);
    }

    [Fact]
    public void Construction_RejectsWhatCannotWork()
    {
        var port = new FakePort();

        Assert.Throws<ArgumentException>(() => new ModelClassifier(port, ModelRole.Decide, ["да"], "без места текста"));
        Assert.Throws<ArgumentException>(() => new ModelClassifier(port, ModelRole.Decide, ["два слова"], Prompt));
        Assert.Throws<ArgumentException>(() => new ModelClassifier(port, ModelRole.Decide, [], Prompt));
        Assert.Throws<ArgumentException>(() => new ModelClassifier(port, default, ["да"], Prompt));
    }
}

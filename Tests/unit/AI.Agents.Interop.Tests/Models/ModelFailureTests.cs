using System.Net;
using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Responses;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Сбой, пустой ответ, обрыв и переполнение различимы в итоге порта; итог без ответа возвращается старым
/// вызывающим тем исключением, по которому они его различают.
/// </summary>
public sealed class ModelFailureTests
{
    private static readonly string OverflowBody = """{"error":{"message":"This model's maximum context length is 8192 tokens"}}""";

    public static TheoryData<string, LlmStatus, LlmFailure?> Cases() => new()
    {
        { "сбой сервера", LlmStatus.Failed, LlmFailure.Other },
        { "пустой ответ", LlmStatus.Empty, null },
        { "обрыв потока", LlmStatus.Failed, LlmFailure.Network },
        { "переполнение окна", LlmStatus.Failed, LlmFailure.ContextOverflow },
        { "частота", LlmStatus.Failed, LlmFailure.RateLimit },
        { "ключ", LlmStatus.Failed, LlmFailure.Access },
        { "модель не найдена", LlmStatus.Failed, LlmFailure.ProviderRefusal },
    };

    private static Exception Error(string name) => name switch
    {
        "сбой сервера" => LlmRequestException.FromResponse(HttpStatusCode.BadGateway, "bad gateway", null),
        // Так ChatLLMApi сообщает о пустом ответе после повторов.
        "пустой ответ" => LlmRequestException.NoResponse(new EmptyModelAnswerException()),
        // Так ChatLLMApi сообщает о потоке без finish_reason и о разрыве соединения.
        "обрыв потока" => LlmRequestException.NoResponse(new IOException("connection reset")),
        "переполнение окна" => LlmRequestException.FromResponse(HttpStatusCode.BadRequest, OverflowBody, null),
        "частота" => LlmRequestException.FromResponse(HttpStatusCode.TooManyRequests, "slow down", null),
        "ключ" => LlmRequestException.FromResponse(HttpStatusCode.Unauthorized, "bad key", null),
        "модель не найдена" => LlmRequestException.FromResponse(HttpStatusCode.NotFound, "no model", null),
        _ => throw new ArgumentOutOfRangeException(name),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ClientError_BecomesDistinctAnswer(string name, LlmStatus status, LlmFailure? failure)
    {
        var log = new CapturingLog();
        var client = new FakeClient { Respond = () => throw Error(name) };

        LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities(), log: log.Logger)
            .AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос")));

        Assert.Equal(status, answer.Status);
        Assert.Equal(failure, answer.Failure);
        Assert.Null(answer.Text);
        Assert.Contains(log.Warnings, w => w.Contains(status.ToString()));
    }

    [Fact]
    public async Task FourKinds_AreAllDifferent()
    {
        var seen = new HashSet<(LlmStatus, LlmFailure?)>();

        foreach (string name in new[] { "сбой сервера", "пустой ответ", "обрыв потока", "переполнение окна" })
        {
            var client = new FakeClient { Respond = () => throw Error(name) };
            LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger)
                .AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")));
            Assert.True(seen.Add((answer.Status, answer.Failure)), name);
        }
    }

    [Fact]
    public async Task CallerCancellation_StaysException()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        IModelPort port = new FakeClient().AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            port.AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")), cancel.Token));
    }

    [Fact]
    public async Task OtherCancellation_IsNetworkFailure()
    {
        var client = new FakeClient { Respond = () => throw new TaskCanceledException("HttpClient.Timeout") };

        LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger)
            .AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")));

        Assert.Equal(LlmFailure.Network, answer.Failure);
    }

    [Fact]
    public async Task NullResponse_IsFailedOther()
    {
        var client = new FakeClient { Respond = () => null };

        LlmAnswer answer = await client.AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger)
            .AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")));

        Assert.Equal(LlmStatus.Failed, answer.Status);
        Assert.Equal(LlmFailure.Other, answer.Failure);
    }

    /// <summary>Каждый код сбоя проходит порт → старое исключение → порт; Cancelled и Unknown — с записанной потерей.</summary>
    [Fact]
    public void EveryFailure_SurvivesExceptionRoundTrip()
    {
        var expected = new Dictionary<LlmFailure, LlmFailure>
        {
            [LlmFailure.ContextOverflow] = LlmFailure.ContextOverflow,
            [LlmFailure.RateLimit] = LlmFailure.RateLimit,
            [LlmFailure.ProviderRefusal] = LlmFailure.ProviderRefusal,
            [LlmFailure.Access] = LlmFailure.Access,
            [LlmFailure.Network] = LlmFailure.Network,
            [LlmFailure.Other] = LlmFailure.Other,
            // В старой форме отмена не вызывающим неотличима от сети, а незаданный код — от прочего сбоя.
            [LlmFailure.Cancelled] = LlmFailure.Network,
            [LlmFailure.Unknown] = LlmFailure.Other,
            // Молчание модели в старой форме — ответа нет (тайм-аут); ответ не по схеме — прочий сбой.
            [LlmFailure.Stalled] = LlmFailure.Network,
            [LlmFailure.InvalidOutput] = LlmFailure.Other,
        };

        Assert.Equal(Enum.GetValues<LlmFailure>().Order(), expected.Keys.Order());

        foreach ((LlmFailure failure, LlmFailure back) in expected)
        {
            Exception error = Assert.IsType<LlmRequestException>(
                new LlmAnswer { Status = LlmStatus.Failed, Failure = failure }.ToException());

            Assert.Equal(back, ModelTranslation.FailureOf(error));
            Assert.Equal(LlmStatus.Failed, ModelTranslation.ToAnswer(error).Status);
        }
    }

    [Fact]
    public void Empty_IsEmptyAnswerLikeChatLlmApi()
    {
        var error = Assert.IsType<LlmRequestException>(new LlmAnswer { Status = LlmStatus.Empty }.ToException());

        Assert.True(error.IsEmptyAnswer);
        Assert.False(error.IsContextOverflow);
        Assert.Equal(LlmStatus.Empty, ModelTranslation.ToAnswer(error).Status);
    }

    [Fact]
    public void Overflow_IsContextOverflowForOldCallers()
    {
        var error = Assert.IsType<LlmRequestException>(
            new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.ContextOverflow }.ToException());

        Assert.True(error.IsContextOverflow);
        Assert.False(error.IsRetryable);
    }

    [Theory]
    [InlineData(LlmStatus.Ok)]
    [InlineData(LlmStatus.Truncated)]
    public void AnswerWithText_HasNoException(LlmStatus status) =>
        Assert.Null(new LlmAnswer { Status = status, Text = "t" }.ToException());

    [Fact]
    public void UnknownStatus_IsInvalidOperation() =>
        Assert.IsType<InvalidOperationException>(new LlmAnswer { Status = LlmStatus.Unknown }.ToException());

    [Fact]
    public async Task OldClient_OverFailedPort_ThrowsLlmRequestException()
    {
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.RateLimit } };
        ILLMClient client = port.AsLlmClient(ModelRole.Decide, new CapturingLog().Logger);

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => client.SendFullAsync([new("user", "q")], null!));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
    }

    [Fact]
    public async Task OldClient_OverEmptyPort_ThrowsEmptyAnswer()
    {
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Empty } };
        ILLMClient client = port.AsLlmClient(ModelRole.Decide, new CapturingLog().Logger);

        var error = await Assert.ThrowsAsync<LlmRequestException>(() => client.SendAsync("q"));

        Assert.True(error.IsEmptyAnswer);
    }

    [Fact]
    public async Task TruncatedAnswer_IsLengthFinishForOldCallers()
    {
        var port = new FakePort { Answer = new LlmAnswer { Status = LlmStatus.Truncated, Text = "полу" } };

        ChatCompletionsResponse response = await port.AsLlmClient(ModelRole.Write, new CapturingLog().Logger)
            .SendFullAsync([new("user", "q")], null!);

        Assert.Equal("length", response.Choices[0].FinishReason);
        Assert.Equal("полу", response.Choices[0].Message.Content);
    }
}

using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Responses;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>Поток: фрагменты по порядку, последний кадр один и несет расход; обратно — фрагменты в onChunk.</summary>
public sealed class ModelStreamTests
{
    private static FakeClient StreamingClient() => new()
    {
        Chunks =
        {
            new LLMStreamChunk { Reasoning = "хм" },
            new LLMStreamChunk { Content = "При" },
            new LLMStreamChunk { Content = "вет" },
            new LLMStreamChunk { Usage = new Usage { PromptTokens = 7 } },
        },
        Respond = () => new ChatCompletionsResponse("Привет")
        {
            Usage = new Usage { PromptTokens = 7, CompletionTokens = 2, Cost = 0.01m },
        },
    };

    [Fact]
    public async Task LastFrame_IsSingleFinalWithUsage()
    {
        FakeClient client = StreamingClient();
        IModelPort port = client.AsModelPort(ModelKit.Capabilities(streaming: true), log: new CapturingLog().Logger);

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q"))));

        Assert.True(client.Streamed);
        Assert.Collection(frames,
            f => Assert.Equal("хм", Assert.IsType<LlmChunk.Reasoning>(f).Chunk),
            f => Assert.Equal("При", Assert.IsType<LlmChunk.Text>(f).Chunk),
            f => Assert.Equal("вет", Assert.IsType<LlmChunk.Text>(f).Chunk),
            f =>
            {
                LlmAnswer answer = Assert.IsType<LlmChunk.Final>(f).Answer;
                Assert.Equal("Привет", answer.Text);
                Assert.Equal(new ModelUsage { InputTokens = 7, OutputTokens = 2, Cost = 0.01m }, answer.Usage);
            });
    }

    [Fact]
    public async Task WithoutStreaming_OneFinalFrame()
    {
        FakeClient client = StreamingClient();
        IModelPort port = client.AsModelPort(ModelKit.Capabilities(streaming: false), log: new CapturingLog().Logger);

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q"))));

        Assert.False(client.Streamed);
        Assert.NotNull(Assert.IsType<LlmChunk.Final>(Assert.Single(frames)).Answer.Usage);
    }

    [Fact]
    public async Task FailureMidStream_EndsWithFailedFinal()
    {
        FakeClient client = StreamingClient();
        client.Respond = () => throw LlmRequestException.NoResponse(new IOException("обрыв"));
        IModelPort port = client.AsModelPort(ModelKit.Capabilities(streaming: true), log: new CapturingLog().Logger);

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "q"))));

        LlmAnswer answer = Assert.IsType<LlmChunk.Final>(frames[^1]).Answer;
        Assert.Equal(LlmFailure.Network, answer.Failure);
        Assert.Single(frames.OfType<LlmChunk.Final>());
    }

    [Fact]
    public async Task OldClient_OverPort_ForwardsFragmentsAndReturnsFinal()
    {
        var port = new FakePort
        {
            Frames = { new LlmChunk.Reasoning { Chunk = "r" }, new LlmChunk.Text { Chunk = "a" }, new LlmChunk.Text { Chunk = "b" } },
            Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "ab", Usage = new ModelUsage { InputTokens = 3, OutputTokens = 2 } },
        };
        ILLMClient client = port.AsLlmClient(ModelRole.Write, new CapturingLog().Logger);
        var received = new List<LLMStreamChunk>();

        ChatCompletionsResponse response = await client.SendFullAsync([new LLMMessage("user", "q")], null!, received.Add);

        Assert.Equal(["r", null, null], received.Select(c => c.Reasoning));
        Assert.Equal([null, "a", "b"], received.Select(c => c.Content));
        Assert.Equal("ab", response.Choices[0].Message.Content);
        Assert.Equal(5, response.Usage.TotalTokens);
    }

    [Fact]
    public async Task OldClient_OverPortWithoutFinal_Throws()
    {
        var port = new FakePort { SkipFinal = true };
        ILLMClient client = port.AsLlmClient(ModelRole.Write, new CapturingLog().Logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendFullAsync([new LLMMessage("user", "q")], null!, _ => { }));
    }
}

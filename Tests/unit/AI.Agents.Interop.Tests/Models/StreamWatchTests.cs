using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Сторож потока модели, сессия С2: молчание до первого фрагмента и простой посреди ответа — итог Failed/Stalled,
/// внутренний поток отменяется; живой поток и порт без потока проходят как есть.
/// </summary>
public sealed class StreamWatchTests
{
    private static readonly StreamWatch Watch = new()
    {
        FirstChunk = TimeSpan.FromMilliseconds(150),
        Idle = TimeSpan.FromMilliseconds(150),
    };

    private static ModelAsk Ask() => ModelKit.Ask(ModelKit.Text(MessageRole.User, "q"));

    [Fact]
    public async Task SilenceBeforeFirstChunk_IsStalled_AndInnerStreamIsCancelled()
    {
        var inner = new SlowPort(firstDelay: TimeSpan.FromSeconds(30), chunks: 1);
        IModelPort port = inner.Watched(Watch, log: new CapturingLog().Logger);

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(Ask()));

        LlmAnswer answer = Assert.IsType<LlmChunk.Final>(Assert.Single(frames)).Answer;
        Assert.Equal(LlmStatus.Failed, answer.Status);
        Assert.Equal(LlmFailure.Stalled, answer.Failure);
        Assert.Contains("не начала", answer.FailureText, StringComparison.Ordinal);
        Assert.True(inner.Cancelled);
    }

    [Fact]
    public async Task SilenceMidAnswer_IsStalled_AfterTheChunksAlreadyShown()
    {
        var inner = new SlowPort(firstDelay: TimeSpan.Zero, chunks: 2, stallAfter: 2);
        IModelPort port = inner.Watched(Watch, log: new CapturingLog().Logger);

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(Ask()));

        Assert.Equal(2, frames.Count(f => f is LlmChunk.Text));
        LlmAnswer answer = Assert.IsType<LlmChunk.Final>(frames[^1]).Answer;
        Assert.Equal(LlmFailure.Stalled, answer.Failure);
        Assert.Contains("посреди", answer.FailureText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveStream_PassesWithItsFinal()
    {
        var inner = new SlowPort(firstDelay: TimeSpan.Zero, chunks: 3);

        // Щедрые пределы: живой поток не должен зависеть от скорости машины.
        var generous = new StreamWatch { FirstChunk = TimeSpan.FromSeconds(10), Idle = TimeSpan.FromSeconds(10) };
        List<LlmChunk> frames = await Kit.ReadAll(inner.Watched(generous).StreamAsync(Ask()));

        Assert.Equal(4, frames.Count);
        Assert.Equal(LlmStatus.Ok, Assert.IsType<LlmChunk.Final>(frames[^1]).Answer.Status);
    }

    [Fact]
    public async Task AskAsync_IsWatchedToo()
    {
        var inner = new SlowPort(firstDelay: TimeSpan.FromSeconds(30), chunks: 1);

        LlmAnswer answer = await inner.Watched(Watch, log: new CapturingLog().Logger).AskAsync(Ask());

        Assert.Equal(LlmFailure.Stalled, answer.Failure);
    }

    [Fact]
    public async Task PortWithoutStreaming_IsNotWatched()
    {
        var inner = new SlowPort(firstDelay: TimeSpan.FromMilliseconds(400), chunks: 0, streaming: false);

        LlmAnswer answer = await inner.Watched(Watch).AskAsync(Ask());

        Assert.Equal(LlmStatus.Ok, answer.Status);
    }

    [Fact]
    public async Task CallerCancellation_IsAnException_NotAStall()
    {
        var inner = new SlowPort(firstDelay: TimeSpan.FromSeconds(30), chunks: 1);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        IModelPort port = inner.Watched(new StreamWatch { FirstChunk = TimeSpan.FromSeconds(20) });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Kit.ReadAll(port.StreamAsync(Ask(), cancel.Token)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Watch_RequiresPositiveLimits(int ms)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamWatch { FirstChunk = TimeSpan.FromMilliseconds(ms) });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StreamWatch { FirstChunk = TimeSpan.FromSeconds(1), Idle = TimeSpan.FromMilliseconds(ms) });
    }

    /// <summary>Поток с задержкой первого кадра и, по желанию, зависанием после N кадров.</summary>
    private sealed class SlowPort(TimeSpan firstDelay, int chunks, int stallAfter = -1, bool streaming = true) : IModelPort
    {
        public bool Cancelled { get; private set; }

        public async Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default)
        {
            await Task.Delay(firstDelay, cancellationToken);
            return new LlmAnswer { Status = LlmStatus.Ok, Text = "ответ" };
        }

        public async IAsyncEnumerable<LlmChunk> StreamAsync(ModelAsk ask, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(firstDelay, cancellationToken);
                for (int i = 0; i < chunks; i++)
                {
                    if (i == stallAfter)
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    yield return new LlmChunk.Text { Chunk = "ч" + i };
                }

                if (stallAfter == chunks)
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                yield return new LlmChunk.Final { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "ответ" } };
            }
            finally
            {
                Cancelled = cancellationToken.IsCancellationRequested;
            }
        }

        public ModelCapabilities Describe(ModelRole role) => ModelKit.Capabilities(streaming);
    }
}

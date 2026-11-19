using System.Reflection;
using System.Text.Json;
using AI.Agents.Contracts.Models;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Поток ответа модели: реализация по умолчанию отдает один итоговый кадр, кадры проходят круг через JSON.
/// </summary>
public sealed class ModelStreamTests
{
    private static readonly ModelAsk Ask = new()
    {
        Role = ModelRole.Write,
        Messages = [new ModelMessage { Role = MessageRole.User }],
        ToolMode = ToolMode.None,
    };

    /// <summary>Реализация по умолчанию: ровно один кадр, это Final с тем же LlmAnswer, что дает AskAsync.</summary>
    [Fact]
    public async Task StreamAsync_Default_YieldsSingleFinalWithAskAnswer()
    {
        var port = new AskOnlyPort();
        using var cancellation = new CancellationTokenSource();

        var chunks = new List<LlmChunk>();

        await foreach (LlmChunk chunk in ((IModelPort)port).StreamAsync(Ask, cancellation.Token))
            chunks.Add(chunk);

        LlmChunk single = Assert.Single(chunks);
        LlmChunk.Final final = Assert.IsType<LlmChunk.Final>(single);

        Assert.Same(port.Answer, final.Answer);
        Assert.Equal(1, port.Calls);
        Assert.Same(Ask, port.LastAsk);
        Assert.Equal(cancellation.Token, port.LastToken);
        Assert.False(port.Describe(ModelRole.Write).Streaming);
    }

    /// <summary>Отмена до начала потока доходит до AskAsync исключением, а не пустым потоком.</summary>
    [Fact]
    public async Task StreamAsync_Default_PropagatesCancellation()
    {
        var port = new AskOnlyPort();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (LlmChunk _ in ((IModelPort)port).StreamAsync(Ask, cancellation.Token))
            {
            }
        });
    }

    /// <summary>Метод потока объявлен с реализацией по умолчанию и отдает кадры LlmChunk.</summary>
    [Fact]
    public void StreamAsync_IsDefaultInterfaceMethod()
    {
        MethodInfo stream = typeof(IModelPort).GetMethod(nameof(IModelPort.StreamAsync))!;

        Assert.Equal(typeof(IAsyncEnumerable<LlmChunk>), stream.ReturnType);
        Assert.False(stream.IsAbstract);
        Assert.Equal([typeof(ModelAsk), typeof(CancellationToken)], stream.GetParameters().Select(parameter => parameter.ParameterType));
    }

    /// <summary>Каждый вид кадра проходит круг через базовый тип с дискриминатором вида.</summary>
    [Fact]
    public void Chunks_OfEveryKind_SurviveRoundTripThroughBase()
    {
        LlmChunk[] chunks =
        [
            new LlmChunk.Text { Chunk = "Курс " },
            new LlmChunk.Reasoning { Chunk = "Думаю" },
            new LlmChunk.Final { Answer = new LlmAnswer { Status = LlmStatus.Ok, Text = "Курс 101,5" } },
        ];

        foreach (LlmChunk chunk in chunks)
        {
            string json = JsonSerializer.Serialize(chunk);
            LlmChunk back = JsonSerializer.Deserialize<LlmChunk>(json)!;

            Assert.Contains($"\"$type\":\"{chunk.GetType().Name}\"", json);
            Assert.IsType(chunk.GetType(), back);
            Assert.Empty(ValueComparison.Differences(chunk, back));
        }
    }

    /// <summary>Порт только с AskAsync: поток берется из реализации по умолчанию.</summary>
    private sealed class AskOnlyPort : IModelPort
    {
        public LlmAnswer Answer { get; } = new() { Status = LlmStatus.Ok, Text = "Готово", Model = "m" };

        public int Calls { get; private set; }

        public ModelAsk? LastAsk { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastAsk = ask;
            LastToken = cancellationToken;
            return Task.FromResult(Answer);
        }

        public ModelCapabilities Describe(ModelRole role) => new()
        {
            ToolCalling = false,
            Images = false,
            Audio = false,
            StructuredOutput = false,
            Streaming = false,
        };
    }
}

using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>Порт модели для тестов: запоминает запросы и отдает заданный итог, в потоке — заданные кадры перед ним.</summary>
internal sealed class FakePort : IModelPort
{
    /// <summary>Итог.</summary>
    public LlmAnswer Answer { get; set; } = new() { Status = LlmStatus.Ok, Text = "ok" };

    /// <summary>Кадры потока перед итогом.</summary>
    public List<LlmChunk> Frames { get; } = [];

    /// <summary>Не отдавать итоговый кадр (поломанный порт).</summary>
    public bool SkipFinal { get; set; }

    /// <summary>Запросы по порядку.</summary>
    public List<ModelAsk> Asks { get; } = [];

    public Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default)
    {
        Asks.Add(ask);
        return Task.FromResult(Answer);
    }

    public async IAsyncEnumerable<LlmChunk> StreamAsync(
        ModelAsk ask, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Asks.Add(ask);
        foreach (LlmChunk frame in Frames)
        {
            await Task.Yield();
            yield return frame;
        }

        if (!SkipFinal)
            yield return new LlmChunk.Final { Answer = Answer };
    }

    public ModelCapabilities Describe(ModelRole role) => ModelKit.Capabilities();
}

/// <summary>Общие значения тестов модели.</summary>
internal static class ModelKit
{
    public static ModelCapabilities Capabilities(bool streaming = false) => new()
    {
        ToolCalling = true,
        Images = true,
        Audio = true,
        StructuredOutput = true,
        Streaming = streaming,
    };

    public static readonly Origin Trusted = new() { Kind = OriginKind.Trusted };

    public static ModelMessage Text(MessageRole role, string text, bool cache = false) => new()
    {
        Role = role,
        Parts = [new MessagePart.Text { Content = text, Origin = Trusted }],
        Cache = cache,
    };

    public static ModelAsk Ask(params ModelMessage[] messages) => new()
    {
        Role = ModelRole.Decide,
        Messages = messages,
        ToolMode = ToolMode.Auto,
    };
}

using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Клиент модели для тестов: запоминает запросы, отдает заданный ответ или бросает заданное исключение,
/// в потоковой перегрузке перед ответом шлет заданные фрагменты.
/// </summary>
internal sealed class FakeClient : ILLMClient
{
    /// <summary>Что вернуть.</summary>
    public Func<ChatCompletionsResponse?> Respond { get; set; } = () => new ChatCompletionsResponse("ok");

    /// <summary>Фрагменты потока перед ответом.</summary>
    public List<LLMStreamChunk> Chunks { get; } = [];

    /// <summary>Сообщения последнего запроса.</summary>
    public List<LLMMessage> Messages { get; private set; } = [];

    /// <summary>Настройки последнего запроса.</summary>
    public GenerateSettings? Settings { get; private set; }

    /// <summary>Звали ли потоковую перегрузку.</summary>
    public bool Streamed { get; private set; }

    public Task<string> SendAsync(string text, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<string> SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default)
    {
        Messages = [.. messages];
        Settings = generateSettings;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Respond()!);
    }

    public async Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings, Action<LLMStreamChunk> onChunk,
        CancellationToken cancellationToken = default)
    {
        Streamed = true;
        foreach (LLMStreamChunk chunk in Chunks)
        {
            await Task.Yield();
            onChunk(chunk);
        }

        return await SendFullAsync(messages, generateSettings, cancellationToken);
    }

    public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

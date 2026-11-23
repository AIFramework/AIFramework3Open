using AI.Agents.Contracts.Models;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Старый клиент <see cref="ILLMClient"/> над портом модели: старые пути (<c>Agent</c>, <c>PlanGenerator</c>,
/// <c>ScriptWriter</c>, модуль <c>llm</c>) ходят через порт без изменений в себе.
/// </summary>
/// <remarks>
/// Итог без ответа становится тем исключением, которое ждут старые вызывающие (<c>LlmRequestException</c>, см.
/// <see cref="ModelTranslation.ToException(LlmAnswer)"/>). Температура и уровень размышления передаются; настройки,
/// которых нет в контракте (TopP, бюджет рассуждения и прочее), не передаются: о каждой — одно предупреждение на
/// экземпляр клиента.
/// </remarks>
internal sealed class PortLlmClient : ILLMClient
{
    private readonly IModelPort _port;
    private readonly ModelRole _role;
    private readonly ILogger? _log;
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

    public PortLlmClient(IModelPort port, ModelRole role, ILogger? log)
    {
        _port = port;
        _role = role;
        _log = log;
    }

    private ILogger Logger => _log ?? Log.Logger;

    public Task<string> SendAsync(string text, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
        SendAsync([new LLMMessage(LLMMessage.UserRole, text)], generateSettings, cancellationToken);

    public async Task<string> SendAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default)
    {
        ChatCompletionsResponse response = await CallAsync(messages, generateSettings, null, cancellationToken).ConfigureAwait(false);
        return response.Choices[0].Message.Content?.ToString() ?? "";
    }

    public Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
        CallAsync(messages, generateSettings, null, cancellationToken);

    public Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings, Action<LLMStreamChunk> onChunk,
        CancellationToken cancellationToken = default) =>
        CallAsync(messages, generateSettings, onChunk, cancellationToken);

    /// <summary>Порт модели токены не считает: в контракте такой операции нет.</summary>
    public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Клиент над портом модели не считает токены: в IModelPort такой операции нет.");

    private async Task<ChatCompletionsResponse> CallAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings? settings, Action<LLMStreamChunk>? onChunk,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var lost = new List<string>();
        ModelAsk ask = ModelTranslation.ToAsk(messages, settings, _role, lost);
        WarnOnce(lost);

        LlmAnswer answer = onChunk == null
            ? await _port.AskAsync(ask, cancellationToken).ConfigureAwait(false)
            : await StreamAsync(ask, onChunk, cancellationToken).ConfigureAwait(false);

        if (answer.Status is LlmStatus.Failed or LlmStatus.Empty or LlmStatus.Unknown)
            Logger.Warning("Клиент над портом модели: итог {Status} ({Failure}) сообщается исключением старой формы",
                answer.Status, answer.Failure);

        return answer.ToResponse();
    }

    /// <summary>Поток порта: фрагменты — в <paramref name="onChunk"/>, итог — первый кадр <see cref="LlmChunk.Final"/>.</summary>
    private async Task<LlmAnswer> StreamAsync(ModelAsk ask, Action<LLMStreamChunk> onChunk, CancellationToken cancellationToken)
    {
        await foreach (LlmChunk frame in _port.StreamAsync(ask, cancellationToken).ConfigureAwait(false))
        {
            switch (frame)
            {
                case LlmChunk.Text text:
                    onChunk(new LLMStreamChunk { Content = text.Chunk });
                    break;
                case LlmChunk.Reasoning reasoning:
                    onChunk(new LLMStreamChunk { Reasoning = reasoning.Chunk });
                    break;
                case LlmChunk.Final final:
                    return final.Answer;
            }
        }

        throw new InvalidOperationException("Поток порта модели кончился без итогового кадра LlmChunk.Final.");
    }

    private void WarnOnce(IEnumerable<string> lost)
    {
        foreach (string field in lost)
        {
            bool first;
            lock (_warned)
                first = _warned.Add(field);

            if (first)
                Logger.Warning(
                    "Клиент над портом модели: {Field} в контракте не выражается и в порт не передается " +
                    "(предупреждение одно на клиент)", field);
        }
    }
}

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AI.Agents.Contracts.Models;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Порт модели над старым клиентом <see cref="ILLMClient"/>: одна модель на все роли.
/// </summary>
/// <remarks>
/// Сбой клиента становится итогом <see cref="LlmStatus.Failed"/> или <see cref="LlmStatus.Empty"/>, текст сбоя
/// для человека — в <see cref="LlmAnswer.FailureText"/> и предупреждением в журнал. Ответ по схеме проверяется
/// правилом <see cref="LlmAnswers.RequireJson(LlmAnswer)"/>. Отмена токеном вызывающего остается
/// исключением. Запрос, который старая форма не выражает, отвергается до отправки исключением.
/// </remarks>
internal sealed class ClientModelPort : IModelPort
{
    private readonly ILLMClient _client;
    private readonly ModelCapabilities _capabilities;
    private readonly GenerateSettings? _settings;
    private readonly ILogger? _log;

    public ClientModelPort(ILLMClient client, ModelCapabilities capabilities, GenerateSettings? settings, ILogger? log)
    {
        _client = client;
        _capabilities = capabilities;
        _settings = settings?.Clone();
        _log = log;
    }

    private ILogger Logger => _log ?? Log.Logger;

    public Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default)
    {
        (List<LLMMessage> messages, GenerateSettings settings) = Request(ask);
        return CallAsync(messages, settings, onChunk: null, ask.Schema != null, cancellationToken);
    }

    public IAsyncEnumerable<LlmChunk> StreamAsync(ModelAsk ask, CancellationToken cancellationToken = default)
    {
        (List<LLMMessage> messages, GenerateSettings settings) = Request(ask);

        bool json = ask.Schema != null;
        return _capabilities.Streaming
            ? Stream(messages, settings, json, cancellationToken)
            : SingleFinal(() => CallAsync(messages, settings, onChunk: null, json, cancellationToken));
    }

    public ModelCapabilities Describe(ModelRole role) => _capabilities;

    /// <summary>Перевод запроса сразу, а не в итераторе: неверный запрос отвергается при вызове, а не при чтении потока.</summary>
    private (List<LLMMessage>, GenerateSettings) Request(ModelAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        return (ModelTranslation.ToLlmMessages(ask.Messages), ask.ToSettings(_settings));
    }

    private async IAsyncEnumerable<LlmChunk> Stream(
        List<LLMMessage> messages, GenerateSettings settings, bool json, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var frames = Channel.CreateUnbounded<LlmChunk>(new UnboundedChannelOptions { SingleReader = true });

        void OnChunk(LLMStreamChunk chunk)
        {
            if (!string.IsNullOrEmpty(chunk.Reasoning))
                frames.Writer.TryWrite(new LlmChunk.Reasoning { Chunk = chunk.Reasoning });
            if (!string.IsNullOrEmpty(chunk.Content))
                frames.Writer.TryWrite(new LlmChunk.Text { Chunk = chunk.Content });
        }

        Task<LlmAnswer> call = CompleteAfter(CallAsync(messages, settings, OnChunk, json, cancellationToken), frames.Writer);

        await foreach (LlmChunk frame in frames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return frame;

        yield return new LlmChunk.Final { Answer = await call.ConfigureAwait(false) };
    }

    private static async Task<LlmAnswer> CompleteAfter(Task<LlmAnswer> call, ChannelWriter<LlmChunk> writer)
    {
        try
        {
            return await call.ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    /// <summary>Поток из одного итога, как реализация по умолчанию: порт без настоящего потока (<c>Streaming = false</c>).</summary>
    private static async IAsyncEnumerable<LlmChunk> SingleFinal(Func<Task<LlmAnswer>> call)
    {
        yield return new LlmChunk.Final { Answer = await call().ConfigureAwait(false) };
    }

    private async Task<LlmAnswer> CallAsync(
        List<LLMMessage> messages, GenerateSettings settings, Action<LLMStreamChunk>? onChunk, bool json,
        CancellationToken cancellationToken)
    {
        ChatCompletionsResponse? response;
        try
        {
            response = onChunk == null
                ? await _client.SendFullAsync(messages, settings, cancellationToken).ConfigureAwait(false)
                : await _client.SendFullAsync(messages, settings, onChunk, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LlmAnswer failed = ModelTranslation.ToAnswer(error);
            Logger.Warning(error, "Порт модели: вызов клиента кончился итогом {Status} ({Failure}): {Message}",
                failed.Status, failed.Failure, error.Message);
            return failed;
        }

        if (response == null)
        {
            Logger.Warning("Порт модели: клиент вернул null вместо ответа, итог Failed (Other)");
            return new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.Other };
        }

        var lost = new List<string>();
        LlmAnswer answer = response.ToAnswer(lost);

        if (answer.Status == LlmStatus.Failed)
            Logger.Warning("Порт модели: модель или провайдер отказали ({Failure}); finish_reason {FinishReason}, refusal {Refusal}",
                answer.Failure, response.Choices?.FirstOrDefault()?.FinishReason,
                response.Choices?.FirstOrDefault()?.Message?.Refusal ?? response.Choices?.FirstOrDefault()?.Refusal);

        foreach (string field in lost.Distinct())
            Logger.Warning("Порт модели: поле ответа {Field} в контракт не переводится и не передано", field);

        if (!json)
            return answer;

        LlmAnswer checkedAnswer = LlmAnswers.RequireJson(answer);
        if (checkedAnswer.Status != answer.Status)
            Logger.Warning("Порт модели: ответ по схеме не JSON, итог Failed (InvalidOutput)");
        return checkedAnswer;
    }
}

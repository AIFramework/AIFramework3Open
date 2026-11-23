using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Models;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Сторож потока над любым <see cref="IModelPort"/>: первый фрагмент и простой посреди ответа по
/// <see cref="StreamWatch"/>. Молчание становится итогом <see cref="LlmStatus.Failed"/> с кодом
/// <see cref="LlmFailure.Stalled"/>, а не бесконечным ожиданием.
/// </summary>
/// <remarks>
/// Сторожить можно только настоящий поток: у порта без него (<see cref="ModelCapabilities.Streaming"/> =
/// <see langword="false"/>) единственный кадр приходит в конце длинного ответа, и молчание нельзя отличить от
/// работы. Такой вызов идет без сторожа. <see cref="AskAsync"/> при настоящем потоке читает поток и отдает его итог,
/// поэтому сторож действует и на него.
/// </remarks>
internal sealed class WatchedModelPort : IModelPort
{
    /// <summary>Сколько ждать, пока внутренний поток отпустит чтение после отмены.</summary>
    private static readonly TimeSpan Release = TimeSpan.FromSeconds(5);

    private readonly IModelPort _inner;
    private readonly StreamWatch _watch;
    private readonly TimeProvider _time;
    private readonly ILogger? _log;

    public WatchedModelPort(IModelPort inner, StreamWatch watch, TimeProvider time, ILogger? log)
    {
        _inner = inner;
        _watch = watch;
        _time = time;
        _log = log;
    }

    private ILogger Logger => _log ?? Log.Logger;

    public async Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ask);
        if (!_inner.Describe(ask.Role).Streaming)
            return await _inner.AskAsync(ask, cancellationToken).ConfigureAwait(false);

        LlmAnswer? answer = null;
        await foreach (LlmChunk chunk in Watched(ask, cancellationToken).ConfigureAwait(false))
        {
            if (chunk is LlmChunk.Final final)
                answer = final.Answer;
        }

        return answer ?? new LlmAnswer
        {
            Status = LlmStatus.Failed,
            Failure = LlmFailure.Other,
            FailureText = "поток модели кончился без итогового кадра",
        };
    }

    public IAsyncEnumerable<LlmChunk> StreamAsync(ModelAsk ask, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ask);
        return _inner.Describe(ask.Role).Streaming
            ? Watched(ask, cancellationToken)
            : _inner.StreamAsync(ask, cancellationToken);
    }

    public ModelCapabilities Describe(ModelRole role) => _inner.Describe(role);

    private async IAsyncEnumerable<LlmChunk> Watched(ModelAsk ask, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IAsyncEnumerator<LlmChunk> frames = _inner.StreamAsync(ask, stop.Token).GetAsyncEnumerator(stop.Token);
        Task<bool>? pending = null;
        int received = 0;
        bool final = false;

        try
        {
            while (true)
            {
                TimeSpan? limit = received == 0 ? _watch.FirstChunk : _watch.Idle;
                pending = frames.MoveNextAsync().AsTask();
                bool more;
                try
                {
                    more = limit is { } wait
                        ? await pending.WaitAsync(wait, _time, cancellationToken).ConfigureAwait(false)
                        : await pending.ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    break;
                }

                pending = null;
                if (!more)
                    yield break;

                received++;
                LlmChunk chunk = frames.Current;
                final = chunk is LlmChunk.Final;
                yield return chunk;
                if (final)
                    yield break;
            }

            // Молчание дольше предела: внутренний поток отменяется, вызывающий получает итог сбоя.
            string text = received == 0
                ? $"модель не начала отвечать за {_watch.FirstChunk.TotalSeconds:0.###} с"
                : $"поток модели встал посреди ответа: нет фрагментов дольше {_watch.Idle!.Value.TotalSeconds:0.###} с";
            Logger.Warning("Сторож потока модели: {Reason} (роль {Role}, получено кадров {Count})", text, ask.Role.Name, received);
            yield return new LlmChunk.Final
            {
                Answer = new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.Stalled, FailureText = text },
            };
        }
        finally
        {
            stop.Cancel();
            if (pending != null)
            {
                // Чтение еще идет: дожидаемся, пока отмена его отпустит, иначе освободить поток нельзя.
                try
                {
                    await pending.WaitAsync(Release, _time, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    pending = ex is TimeoutException ? pending : null;
                }
            }

            if (pending is null or { IsCompleted: true })
                await frames.DisposeAsync().ConfigureAwait(false);
            else
                Logger.Warning("Сторож потока модели: внутренний поток не отпустил чтение после отмены; он брошен без освобождения");
        }
    }
}

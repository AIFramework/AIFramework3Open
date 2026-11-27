#nullable enable

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using AI.LLM.Clients.Base;

namespace AI.LLM.Infrastructure.Resilience;

/// <summary>Безопасен ли повтор вызова: решает вызывающий, он знает, что делает вызов.</summary>
public enum RepeatSafety
{
    /// <summary>Не задано; правило повтора такой вызов отвергает.</summary>
    Unspecified = 0,

    /// <summary>Вызов ничего не меняет в мире (ответ модели, поиск, эмбеддинги) или идемпотентен.</summary>
    Safe,

    /// <summary>Вызов необратим (создаёт задачу, списывает, отправляет): не повторяется никогда.</summary>
    Unsafe,
}

/// <summary>Чем был сбой для правила повтора.</summary>
public enum FailureKind
{
    /// <summary>Не задано.</summary>
    Unspecified = 0,

    /// <summary>Временный: сервер не выполнил запрос и может выполнить позже (перегрузка, лимит, сеть до ответа).</summary>
    Transient,

    /// <summary>Результат неизвестен: запрос мог дойти и выполниться (обрыв после отправки у необратимого вызова).</summary>
    Unknown,

    /// <summary>Повтор не поможет: неверный запрос, ключ, переполнение окна, ответ не того вида.</summary>
    Fatal,
}

/// <summary>
/// Правило повторов — одно на все клиенты: повтор только временного сбоя, пауза по <c>Retry-After</c>, если сервер её
/// дал, иначе нарастающая с пределом; необратимый вызов и вызов с неизвестным результатом не повторяются.
/// </summary>
/// <remarks>
/// <para>
/// Временные коды ответа — 408, 425, 429 и 5xx (<see cref="IsTransient(HttpStatusCode)"/>). Сеть без ответа сервера
/// для безопасного вызова временна, для необратимого — «результат неизвестен». <c>Retry-After</c> длиннее
/// <see cref="MaxRetryAfter"/> не ждётся: такой сбой отдаётся вызывающему сразу.
/// </para>
/// <para>
/// Отказ по ключу и смена маршрута — не повтор, а ротация (<see cref="ApiKeyPool"/>, <see cref="CooldownPool{T}"/>
/// в <c>ProviderHttpClient</c>): попытки этого правила на неё не тратятся.
/// </para>
/// </remarks>
public sealed record RetryPolicy
{
    /// <summary>Три попытки, пауза 1 с, 2 с…, не больше 30 с; <c>Retry-After</c> до минуты.</summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>Без повторов: одна попытка.</summary>
    public static RetryPolicy None { get; } = new() { MaxAttempts = 1 };

    /// <summary>Сколько всего попыток, включая первую; не меньше 1.</summary>
    public int MaxAttempts
    {
        get => _maxAttempts;
        init => _maxAttempts = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(MaxAttempts), value, "Нужна хотя бы одна попытка.");
    }

    private readonly int _maxAttempts = 3;

    /// <summary>Пауза перед второй попыткой; дальше удваивается.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Предел нарастающей паузы.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Дольше этого по <c>Retry-After</c> не ждём: проще сказать человеку.</summary>
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Часы пауз; <see langword="null"/>: <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? Time { get; init; }

    /// <summary>Временный ли код ответа: 408, 425, 429, 5xx.</summary>
    /// <param name="status">Код ответа.</param>
    public static bool IsTransient(HttpStatusCode status) => (int)status is 408 or 425 or 429 or >= 500;

    /// <summary>Срок из заголовка <c>Retry-After</c> (секунды или дата); <see langword="null"/>: заголовка нет.</summary>
    /// <param name="header">Заголовок.</param>
    /// <param name="time">Часы для даты; <see langword="null"/>: <see cref="TimeProvider.System"/>.</param>
    public static TimeSpan? RetryAfterOf(RetryConditionHeaderValue? header, TimeProvider? time = null)
    {
        if (header?.Delta is TimeSpan delta)
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        if (header?.Date is DateTimeOffset date)
        {
            TimeSpan left = date - (time ?? TimeProvider.System).GetUtcNow();
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>Ключ <see cref="Exception.Data"/>, в котором исключение несёт срок <c>Retry-After</c>.</summary>
    public const string RetryAfterKey = "RetryAfter";

    /// <summary>Помечает исключение сроком, который назвал сервер: правило подождёт столько перед повтором.</summary>
    /// <param name="error">Исключение попытки.</param>
    /// <param name="retryAfter">Срок; <see langword="null"/>: сервер не назвал.</param>
    /// <returns>То же исключение.</returns>
    public static TException WithRetryAfter<TException>(TException error, TimeSpan? retryAfter) where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(error);
        if (retryAfter is { } wait)
            error.Data[RetryAfterKey] = wait;
        return error;
    }

    /// <summary>Срок повтора, который несёт исключение: <see cref="LlmRequestException.RetryAfter"/> или пометка <see cref="WithRetryAfter"/>.</summary>
    /// <param name="error">Исключение попытки.</param>
    public static TimeSpan? RetryAfterOf(Exception error) =>
        (error as LlmRequestException)?.RetryAfter ?? (error?.Data[RetryAfterKey] as TimeSpan?);

    /// <summary>Чем был сбой для правила повтора.</summary>
    /// <param name="error">Исключение попытки.</param>
    /// <param name="safety">Безопасен ли повтор вызова.</param>
    public static FailureKind Classify(Exception error, RepeatSafety safety)
    {
        ArgumentNullException.ThrowIfNull(error);
        EnsureKnown(safety);

        return error switch
        {
            LlmRequestException { IsContextOverflow: true } => FailureKind.Fatal,
            LlmRequestException { StatusCode: { } status } => IsTransient(status) ? FailureKind.Transient : FailureKind.Fatal,
            LlmRequestException e => e.IsRetryable ? Network(safety) : FailureKind.Fatal,
            HttpRequestException { StatusCode: { } status } => IsTransient(status) ? FailureKind.Transient : FailureKind.Fatal,
            HttpRequestException h when BeforeSend(h) => FailureKind.Transient,
            HttpRequestException or TimeoutException or IOException or OperationCanceledException => Network(safety),
            _ => FailureKind.Fatal,
        };
    }

    /// <summary>Делать ли ещё попытку.</summary>
    /// <param name="kind">Чем был сбой.</param>
    /// <param name="safety">Безопасен ли повтор вызова.</param>
    /// <param name="attemptsMade">Сколько попыток уже сделано.</param>
    /// <param name="retryAfter">Срок, который назвал сервер; <see langword="null"/>: не назвал.</param>
    public bool ShouldRetry(FailureKind kind, RepeatSafety safety, int attemptsMade, TimeSpan? retryAfter = null)
    {
        EnsureKnown(safety);
        return safety == RepeatSafety.Safe
            && kind == FailureKind.Transient
            && attemptsMade < MaxAttempts
            && (retryAfter == null || retryAfter <= MaxRetryAfter);
    }

    /// <summary>Пауза после попытки <paramref name="attemptsMade"/>: срок сервера, иначе нарастающая с пределом.</summary>
    /// <param name="attemptsMade">Сколько попыток уже сделано (от 1).</param>
    /// <param name="retryAfter">Срок, который назвал сервер.</param>
    public TimeSpan DelayAfter(int attemptsMade, TimeSpan? retryAfter = null)
    {
        if (retryAfter is { } wait)
            return wait;

        double factor = Math.Pow(2, Math.Max(0, attemptsMade - 1));
        double ticks = Math.Min(InitialDelay.Ticks * factor, MaxDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>
    /// Выполняет вызов по правилу: временный сбой безопасного вызова повторяется с паузой, остальное уходит вызывающему
    /// как есть (исключение последней попытки).
    /// </summary>
    /// <param name="call">Попытка; получает её номер (от 1) и токен.</param>
    /// <param name="safety">Безопасен ли повтор вызова.</param>
    /// <param name="cancellationToken">Отмена; отмена вызывающим не повторяется.</param>
    /// <param name="onRetry">Перед паузой: номер сделанной попытки, сбой и пауза (для журнала).</param>
    public async Task<T> RunAsync<T>(
        Func<int, CancellationToken, Task<T>> call,
        RepeatSafety safety,
        CancellationToken cancellationToken = default,
        Action<int, Exception, TimeSpan>? onRetry = null)
    {
        ArgumentNullException.ThrowIfNull(call);
        EnsureKnown(safety);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await call(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                TimeSpan? after = RetryAfterOf(error);
                if (!ShouldRetry(Classify(error, safety), safety, attempt, after))
                {
                    ExceptionDispatchInfo.Capture(error).Throw();
                    throw;
                }

                TimeSpan pause = DelayAfter(attempt, after);
                onRetry?.Invoke(attempt, error, pause);
                if (pause > TimeSpan.Zero)
                    await Task.Delay(pause, Time ?? TimeProvider.System, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Сеть без ответа: для безопасного вызова временно, для необратимого результат неизвестен.</summary>
    private static FailureKind Network(RepeatSafety safety) =>
        safety == RepeatSafety.Safe ? FailureKind.Transient : FailureKind.Unknown;

    /// <summary>Сбой до отправки запроса: имя не разрешилось, соединение не установлено, прокси не пустил.</summary>
    private static bool BeforeSend(HttpRequestException error) =>
        error.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.ProxyTunnelError
        || error.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused or SocketError.HostNotFound };

    private static void EnsureKnown(RepeatSafety safety)
    {
        if (safety is not (RepeatSafety.Safe or RepeatSafety.Unsafe))
            throw new ArgumentOutOfRangeException(nameof(safety), safety, "Безопасность повтора не задана или незнакома.");
    }
}

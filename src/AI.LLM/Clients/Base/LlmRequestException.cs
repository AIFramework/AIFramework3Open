using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AI.LLM.Infrastructure.Resilience;

namespace AI.LLM.Clients.Base;

/// <summary>
/// Запрос к модели не удался: сервер ответил ошибкой или связи нет. Сообщение — для человека
/// («сервер отклонил ключ», «не хватает средств»), подробности для журнала — во внутреннем исключении.
/// </summary>
/// <remarks>
/// Отдельный тип нужен, чтобы отличать отказ сервера от неразобранного ответа модели. Раньше
/// цикл агента считал любую ошибку запроса «неверным форматом» и просил модель повторить —
/// неверный ключ превращался в несколько бесполезных запросов и «нет ответа» в конце.
/// </remarks>
public sealed class LlmRequestException : Exception
{
    /// <summary>Код ответа сервера; <c>null</c> — ответа не было (сеть, обрыв потока).</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Повтор может помочь: перегрузка, ограничение частоты, сбой сервера, сеть. Ключ, баланс,
    /// неверный запрос и переполнение окна модели повтором не лечатся.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>Сколько подождать перед повтором, если сервер сказал; <c>null</c> — не сказал.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Запрос не поместился в окно модели.</summary>
    public bool IsContextOverflow { get; }

    /// <summary>
    /// Сервер ответил, но модель ничего не сказала (<see cref="EmptyModelAnswerException"/>): цикл агента лечит это
    /// подсказкой и новым шагом, а не показывает человеку как отказ сервера.
    /// </summary>
    public bool IsEmptyAnswer => GetBaseException() is EmptyModelAnswerException;

    private LlmRequestException(
        string message, HttpStatusCode? statusCode, bool retryable, TimeSpan? retryAfter, bool contextOverflow, Exception inner)
        : base(message, inner)
    {
        StatusCode = statusCode;
        IsRetryable = retryable;
        RetryAfter = retryAfter;
        IsContextOverflow = contextOverflow;
    }

    /// <summary>Ошибка по ответу сервера.</summary>
    /// <param name="statusCode">Код ответа.</param>
    /// <param name="body">Тело ответа; обычно <c>{"error":{"message":…}}</c>.</param>
    /// <param name="retryAfter">Заголовок <c>Retry-After</c>; может быть <c>null</c>.</param>
    /// <param name="details">Подробности для журнала: запрос, попытка.</param>
    public static LlmRequestException FromResponse(
        HttpStatusCode statusCode, string body, RetryConditionHeaderValue retryAfter, string details = null)
    {
        string provider = ProviderMessage(body);
        int code = (int)statusCode;
        bool overflow = IsOverflow(provider) || IsOverflow(body);
        TimeSpan? wait = RetryPolicy.RetryAfterOf(retryAfter);
        bool retryable = !overflow
            && RetryPolicy.IsTransient(statusCode)
            && (wait == null || wait <= RetryPolicy.Default.MaxRetryAfter);

        string what = overflow ? "Запрос не помещается в окно модели"
            : code is 401 or 403 ? "Сервер модели отклонил ключ"
            : code == 402 ? "Не хватает средств на счёте у поставщика модели"
            : code == 404 ? "Модель или адрес сервера не найдены"
            : code == 429 ? "Поставщик модели ограничил частоту запросов"
            : code >= 500 ? "Сервер модели не справился с запросом"
            : "Сервер модели отклонил запрос";

        string message = $"{what} ({code})" + (string.IsNullOrWhiteSpace(provider) ? "." : ": " + provider);
        Exception inner = details == null ? null : new Exception(details);
        var error = new LlmRequestException(message, statusCode, retryable, wait, overflow, inner);
        // Настоящая причина отказа поставщика лежит глубже «Provider returned error»: человеку она ни к чему,
        // а журналу без нее нечего разбирать. В Data, а не во внутреннем исключении: там текст запроса.
        if (UpstreamCause(body) is { } upstream)
            error.Data[UpstreamKey] = upstream;
        return error;
    }

    /// <summary>Ключ <see cref="Exception.Data"/> с отказом поставщика за посредником.</summary>
    public const string UpstreamKey = "Upstream";

    /// <summary>
    /// Отказ поставщика за посредником (OpenRouter): <c>error.metadata.provider_name</c> и
    /// <c>error.metadata.raw</c>; <c>null</c>, если посредник его не передал.
    /// </summary>
    public static string UpstreamCause(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out JsonElement error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty("metadata", out JsonElement metadata)
                || metadata.ValueKind != JsonValueKind.Object
                || !metadata.TryGetProperty("raw", out JsonElement raw))
                return null;

            string name = metadata.TryGetProperty("provider_name", out JsonElement p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : "поставщик";
            string text = (raw.ValueKind == JsonValueKind.String ? raw.GetString() : raw.GetRawText())?.Trim() ?? "";
            if (text.Length == 0)
                return null;
            return $"{name}: " + (text.Length <= 1000 ? text : text[..1000] + "…");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Ответа не было: сеть, тайм-аут, оборванный поток.</summary>
    /// <param name="cause">Исходная ошибка.</param>
    /// <param name="details">
    /// Подробности для журнала. Переполнение окна по ним не определяется: в них есть сам запрос, и
    /// слова о «context window» в вопросе пользователя давали бы ложное срабатывание.
    /// </param>
    public static LlmRequestException NoResponse(Exception cause, Exception details = null)
    {
        bool overflow = IsOverflow(cause?.Message) || IsOverflow(cause?.InnerException?.Message);
        string message = overflow
            ? "Запрос не помещается в окно модели."
            : "Нет связи с сервером модели: " + (cause?.GetBaseException().Message ?? "причина неизвестна") + ".";
        return new LlmRequestException(message, null, !overflow, null, overflow, details ?? cause);
    }

    /// <summary>
    /// Клиент модели бросил то, что не является ответом сервера и не сетью (своя реализация <c>ILLMClient</c>,
    /// ошибка разбора потока): вызов модели не удался. Повтор не поможет; причина — во внутреннем исключении.
    /// </summary>
    /// <param name="cause">Исходная ошибка.</param>
    public static LlmRequestException FromClient(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        if (cause is LlmRequestException already)
            return already;

        string message = "Вызов модели не удался: " + (cause.GetBaseException().Message ?? "причина неизвестна") + ".";
        return new LlmRequestException(message, null, false, null, false, cause);
    }

    /// <summary>Текст ошибки от поставщика: <c>error.message</c>, <c>error</c> строкой или <c>message</c>.</summary>
    private static string ProviderMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out JsonElement error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                        return error.GetString();
                    if (error.ValueKind == JsonValueKind.Object
                        && error.TryGetProperty("message", out JsonElement nested)
                        && nested.ValueKind == JsonValueKind.String)
                        return nested.GetString();
                }

                if (root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Не JSON — показываем начало как есть.
        }

        string text = body.Trim();
        return text.Length <= 300 ? text : text[..300] + "…";
    }

    /// <summary>
    /// Переполнение окна по тексту ошибки. Код у поставщиков разный (400, 413) или его нет вовсе,
    /// поэтому смотрим на формулировки OpenAI-совместимых серверов.
    /// </summary>
    private static bool IsOverflow(string text) =>
        !string.IsNullOrEmpty(text)
        && (text.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase)
            || text.Contains("maximum context length", StringComparison.OrdinalIgnoreCase)
            || text.Contains("context window", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Please reduce the length", StringComparison.OrdinalIgnoreCase));
}

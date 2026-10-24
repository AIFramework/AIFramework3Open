using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

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
    /// <summary>Дольше этого ждать по <c>Retry-After</c> не имеет смысла: проще сказать человеку.</summary>
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

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
        TimeSpan? wait = Wait(retryAfter);
        bool retryable = !overflow
            && code is 408 or 425 or 429 or >= 500
            && (wait == null || wait <= MaxRetryAfter);

        string what = overflow ? "Запрос не помещается в окно модели"
            : code is 401 or 403 ? "Сервер модели отклонил ключ"
            : code == 402 ? "Не хватает средств на счёте у поставщика модели"
            : code == 404 ? "Модель или адрес сервера не найдены"
            : code == 429 ? "Поставщик модели ограничил частоту запросов"
            : code >= 500 ? "Сервер модели не справился с запросом"
            : "Сервер модели отклонил запрос";

        string message = $"{what} ({code})" + (string.IsNullOrWhiteSpace(provider) ? "." : ": " + provider);
        Exception inner = details == null ? null : new Exception(details);
        return new LlmRequestException(message, statusCode, retryable, wait, overflow, inner);
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

    private static TimeSpan? Wait(RetryConditionHeaderValue retryAfter)
    {
        if (retryAfter?.Delta is TimeSpan delta)
            return delta;
        if (retryAfter?.Date is DateTimeOffset date)
        {
            TimeSpan left = date - DateTimeOffset.UtcNow;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        return null;
    }
}

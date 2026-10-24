using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.LLM.Clients.Base;

namespace AI.LLM.Clients.Fim;

/// <summary>
/// Дополнение кода «заполнением середины» (fill-in-the-middle): модель получает текст до курсора и после
/// него и пишет то, что стоит между ними. Запрос — POST на OpenAI-совместимый <c>/v1/completions</c>
/// (Ollama, llama.cpp, LM Studio); ответ — <c>choices[0].text</c>.
/// </summary>
/// <remarks>
/// Отдельный клиент, а не <see cref="ChatLLMApi"/>: у дополнения нет ролей и диалога, а чатовый шаблон
/// портит FIM — модель начинает «отвечать», а не продолжать код. Повторов нет намеренно: подсказка нужна
/// за доли секунды, и опоздавший ответ уже никому не нужен — следующий запрос придёт с новым набором.
/// <para>
/// Серверу без поля <c>suffix</c> (vLLM) нужен <see cref="Template"/>: тогда текст до и после курсора
/// склеиваются служебными метками модели в одну строку <c>prompt</c>.
/// </para>
/// </remarks>
public sealed class FimClient : IDisposable
{
    /// <summary>Адрес Ollama по умолчанию.</summary>
    public const string OllamaEndpoint = "http://localhost:11434/v1/completions";

    /// <summary>Метки FIM у Qwen2.5-Coder: {0} — текст до курсора, {1} — после.</summary>
    public const string QwenTemplate = "<|fim_prefix|>{0}<|fim_suffix|>{1}<|fim_middle|>";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>Адрес <c>…/v1/completions</c>.</summary>
    public string Endpoint { get; }

    /// <summary>Имя модели на сервере.</summary>
    public string Model { get; }

    /// <summary>Ключ сервера; <c>null</c> — серверу ключ не нужен.</summary>
    public string ApiKey { get; set; }

    /// <summary>Предел длины ответа в токенах: строке кода хватает нескольких десятков.</summary>
    public int MaxTokens { get; set; } = 64;

    /// <summary>Сколько ждать ответа: дольше подсказка теряет смысл.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Шаблон для серверов без поля <c>suffix</c> (см. <see cref="QwenTemplate"/>); <c>null</c> — текст после
    /// курсора уходит полем <c>suffix</c>, и метки модели подставляет сервер.
    /// </summary>
    public string Template { get; set; }

    /// <summary>Клиент дополнения.</summary>
    /// <param name="endpoint">Адрес <c>…/v1/completions</c>.</param>
    /// <param name="model">Имя модели, например <c>qwen2.5-coder:1.5b</c>.</param>
    /// <param name="apiKey">Ключ; <c>null</c> — без ключа.</param>
    /// <param name="httpClient">Общий HttpClient; не задан — свой, освобождается вместе с клиентом.</param>
    public FimClient(string endpoint, string model, string apiKey = null, HttpClient httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentNullException(nameof(endpoint), "Адрес сервера не может быть пустым");
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentNullException(nameof(model), "Имя модели не может быть пустым");

        Endpoint = endpoint.Trim();
        Model = model.Trim();
        ApiKey = apiKey;
        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    /// <summary>Текст между <paramref name="prefix"/> и <paramref name="suffix"/>; пустая строка — модели нечего добавить.</summary>
    /// <param name="prefix">Текст до курсора.</param>
    /// <param name="suffix">Текст после курсора.</param>
    /// <param name="stop">Строки, на которых модель останавливается (например, перевод строки).</param>
    /// <param name="cancellationToken">Отмена: пользователь набрал дальше.</param>
    /// <exception cref="LlmRequestException">Сервер ответил ошибкой, не ответил за <see cref="Timeout"/> или связи нет.</exception>
    public async Task<string> CompleteAsync(string prefix, string suffix, IReadOnlyList<string> stop = null, CancellationToken cancellationToken = default)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(Payload(prefix ?? "", suffix ?? "", stop), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey.Trim());

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, linked.Token);
            string body = await response.Content.ReadAsStringAsync(linked.Token);
            if (!response.IsSuccessStatusCode)
                throw LlmRequestException.FromResponse(response.StatusCode, body, response.Headers.RetryAfter, $"FIM {Model} → {Endpoint}");
            return Text(body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            throw LlmRequestException.NoResponse(ex is OperationCanceledException ? new TimeoutException($"нет ответа за {Timeout.TotalSeconds:0.#} с", ex) : ex);
        }
    }

    /// <summary>Освобождает свой HttpClient (общий, переданный снаружи, не трогает).</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private string Payload(string prefix, string suffix, IReadOnlyList<string> stop)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = Model,
            ["prompt"] = Template == null ? prefix : string.Format(Template, prefix, suffix),
            ["max_tokens"] = MaxTokens,
            ["temperature"] = 0,
            ["stream"] = false,
        };
        if (Template == null)
            payload["suffix"] = suffix;
        if (stop is { Count: > 0 })
            payload["stop"] = stop;
        return JsonSerializer.Serialize(payload);
    }

    /// <summary><c>choices[0].text</c>; нет его — пустая строка.</summary>
    private static string Text(string body)
    {
        using JsonDocument json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("choices", out JsonElement choices)
            && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("text", out JsonElement text) && text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : "";
    }
}

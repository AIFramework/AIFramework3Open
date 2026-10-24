using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;

namespace AI.LLM.Clients.Replicate;

/// <summary>
/// Языковая модель на Replicate (https://replicate.com) через API предсказаний: POST
/// <c>/v1/models/{владелец}/{модель}/predictions</c> с <c>Prefer: wait</c>, а если модель не уложилась —
/// опрос <c>urls.get</c>, пока предсказание не завершится.
/// </summary>
/// <remarks>
/// API совместимого с OpenAI адреса у Replicate нет, и сообщений с ролями модели обычно не принимают: на входе
/// строки <c>prompt</c> и <c>system_prompt</c>. Поэтому системные сообщения уходят в <c>system_prompt</c>, а
/// остальные склеиваются в <c>prompt</c> с подписями ролей. Инструментов на входе тоже нет — агенту на этом
/// клиенте нужна политика вызова инструментов текстом (<c>StructuredJsonPolicy</c>).
/// </remarks>
public sealed class ReplicateClient : ILLMClient, IDisposable
{
    /// <summary>Адрес API Replicate.</summary>
    public const string ApiBase = "https://api.replicate.com/v1";

    /// <summary>Сколько сервер держит запрос, дожидаясь ответа модели (не больше 60 с по документации Replicate).</summary>
    private const int WaitSeconds = 60;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _token;

    /// <summary>Модель в виде «владелец/имя», например <c>meta/meta-llama-3-70b-instruct</c>.</summary>
    public string Model { get; }

    /// <summary>Пауза между опросами незавершённого предсказания.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Сколько всего ждать предсказание: дольше — ошибка «нет ответа».</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Клиент Replicate.</summary>
    /// <param name="token">Ключ API Replicate (<c>REPLICATE_API_TOKEN</c>).</param>
    /// <param name="model">Модель «владелец/имя».</param>
    /// <param name="httpClient">Общий HttpClient; не задан — свой, освобождается вместе с клиентом.</param>
    public ReplicateClient(string token, string model, HttpClient httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentNullException(nameof(token), "Ключ Replicate не может быть пустым");
        if (string.IsNullOrWhiteSpace(model) || !model.Contains('/'))
            throw new ArgumentException("Модель Replicate задаётся как «владелец/имя»", nameof(model));

        _token = token.Trim();
        Model = model.Trim();
        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    /// <inheritdoc/>
    public Task<string> SendAsync(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default) =>
        SendAsync([LLMMessage.CreateMessage(Roles.User, text)], generateSettings, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default) =>
        (await SendFullAsync(messages, generateSettings, cancellationToken)).Choices[0].Message.Content?.ToString() ?? "";

    /// <inheritdoc/>
    public async Task<ChatCompletionsResponse> SendFullAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using HttpRequestMessage create = Request(HttpMethod.Post, $"{ApiBase}/models/{Model}/predictions");
            create.Headers.TryAddWithoutValidation("Prefer", "wait=" + WaitSeconds);
            create.Content = new StringContent(Payload(messages, generateSettings), Encoding.UTF8, "application/json");
            JsonElement prediction = await SendJsonAsync(create, linked.Token);
            while (Status(prediction) is "starting" or "processing")
            {
                await Task.Delay(PollInterval, linked.Token);
                string poll = prediction.TryGetProperty("urls", out JsonElement urls) && urls.TryGetProperty("get", out JsonElement get)
                    ? get.GetString()
                    : $"{ApiBase}/predictions/{prediction.GetProperty("id").GetString()}";
                using HttpRequestMessage request = Request(HttpMethod.Get, poll);
                prediction = await SendJsonAsync(request, linked.Token);
            }

            if (Status(prediction) != "succeeded")
            {
                string error = prediction.TryGetProperty("error", out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : Status(prediction);
                throw LlmRequestException.FromResponse(System.Net.HttpStatusCode.InternalServerError, error, null, $"Replicate {Model}");
            }

            return new ChatCompletionsResponse(Output(prediction)) { Model = Model, Provider = "replicate" };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            throw LlmRequestException.NoResponse(ex is OperationCanceledException ? new TimeoutException($"Replicate не ответил за {Timeout.TotalSeconds:0} с", ex) : ex);
        }
    }

    /// <summary>Подсчёта токенов у Replicate нет: оценка по длине текста, четыре знака на токен.</summary>
    public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
        Task.FromResult(messages.Sum(m => (m.Content?.ToString()?.Length ?? 0) / 4 + 1));

    /// <summary>Освобождает свой HttpClient (общий, переданный снаружи, не трогает).</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private async Task<JsonElement> SendJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw LlmRequestException.FromResponse(response.StatusCode, body, response.Headers.RetryAfter, $"Replicate {Model}");
        using JsonDocument json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    /// <summary>
    /// Вход модели: системные сообщения — в <c>system_prompt</c>, диалог — в <c>prompt</c> с подписями ролей и
    /// приглашением ответить в конце.
    /// </summary>
    private static string Payload(IEnumerable<LLMMessage> messages, GenerateSettings settings)
    {
        var system = new StringBuilder();
        var prompt = new StringBuilder();
        List<LLMMessage> list = messages.ToList();
        foreach (LLMMessage message in list)
        {
            string text = message.Content?.ToString() ?? "";
            if (message.Role == LLMMessage.SystemRole)
                system.AppendLine(text);
            else if (list.Count(m => m.Role != LLMMessage.SystemRole) == 1)
                prompt.Append(text);
            else
                prompt.Append(Label(message.Role)).Append(": ").AppendLine(text).AppendLine();
        }

        if (list.Count(m => m.Role != LLMMessage.SystemRole) > 1)
            prompt.Append("Assistant:");
        var input = new Dictionary<string, object> { ["prompt"] = prompt.ToString() };
        if (system.Length > 0)
            input["system_prompt"] = system.ToString().Trim();
        if (settings?.MaxTokens is int max)
            input["max_tokens"] = max;
        if (settings?.Temperature is double temperature)
            input["temperature"] = temperature;
        return JsonSerializer.Serialize(new { input });
    }

    private static string Label(string role) => role switch
    {
        LLMMessage.AssistantRole => "Assistant",
        LLMMessage.ToolRole => "Tool",
        _ => "User",
    };

    private static string Status(JsonElement prediction) =>
        prediction.TryGetProperty("status", out JsonElement status) ? status.GetString() : "succeeded";

    /// <summary>Ответ модели: у языковых моделей <c>output</c> — массив фрагментов, у некоторых — строка.</summary>
    private static string Output(JsonElement prediction)
    {
        if (!prediction.TryGetProperty("output", out JsonElement output))
            return "";
        return output.ValueKind switch
        {
            JsonValueKind.String => output.GetString(),
            JsonValueKind.Array => string.Concat(output.EnumerateArray().Select(p => p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString())),
            JsonValueKind.Null => "",
            _ => output.ToString(),
        };
    }
}

using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Services.Embeddings.Base;
using AI.LLM.Services.Embeddings.OpenRouter.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AI.LLM.Services.Embeddings.OpenRouter;

/// <summary>
/// Эмбеддер поверх OpenRouter (https://openrouter.ai/api/v1/embeddings).
/// Даёт единый доступ к моделям OpenAI, Google, Qwen, Mistral, BAAI, NVIDIA и др.
/// Мультимодальные модели (<see cref="OpenRouterEmbeddingModels.GeminiEmbedding2"/>,
/// <see cref="OpenRouterEmbeddingModels.NemotronEmbedVL"/>) укладывают текст и изображения
/// в одно пространство, поэтому картинку можно искать текстовым запросом.
/// </summary>
public class OpenRouterEmbedder : EmbedderServiceBase, IMultimodalEmbedderService, IDisposable
{
    /// <summary>
    /// Эндпоинт эмбеддингов OpenRouter
    /// </summary>
    public const string DefaultApiUrl = "https://openrouter.ai/api/v1/embeddings";

    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private int _disposed; // 0 = not disposed, 1 = disposed

    /// <summary>
    /// URL эндпоинта (можно переопределить на прокси-шлюз)
    /// </summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;

    /// <summary>
    /// Требуемая размерность вектора. Работает только на моделях с поддержкой Matryoshka
    /// (см. <see cref="OpenRouterEmbeddingModel.SupportsDimensions"/>). null — размерность модели по умолчанию.
    /// </summary>
    public int? Dimensions { get; set; }

    /// <summary>
    /// Маршрутизация по провайдерам OpenRouter (например, только google-vertex)
    /// </summary>
    public ProviderPreference PreferredProvider { get; set; }

    /// <summary>
    /// Размер пакета: сколько элементов уходит в один HTTP-запрос
    /// </summary>
    public int BatchSize { get; set; } = 96;

    /// <summary>
    /// Число повторов при сетевых ошибках, 429 и 5xx
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Таймаут одного запроса
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Приводить вектор к единичной длине. Нужно включать при усечении через <see cref="Dimensions"/>:
    /// обрезанный вектор перестаёт быть нормированным, и косинус считается неверно.
    /// </summary>
    public bool NormalizeVectors { get; set; }

    /// <summary>
    /// Инструкция для запросов на инструктивных моделях (Qwen3 Embedding и подобные).
    /// Если задана, <see cref="EmbedderServiceBase.EncodeQuestionAsync"/> оформит запрос как
    /// "Instruct: {инструкция}\nQuery: {запрос}".
    /// </summary>
    public string QueryInstruction { get; set; }

    /// <summary>
    /// Заголовок HTTP-Referer для статистики OpenRouter (необязательный)
    /// </summary>
    public string HttpReferer { get; set; }

    /// <summary>
    /// Заголовок X-Title — имя приложения в статистике OpenRouter (необязательный)
    /// </summary>
    public string AppTitle { get; set; }

    /// <summary>
    /// Потребление токенов и стоимость последнего успешного запроса (информационно)
    /// </summary>
    public Usage LastUsage { get; private set; }

    /// <summary>
    /// Принимает ли выбранная модель изображения. Для модели вне каталога возвращает true —
    /// решение остаётся за сервером.
    /// </summary>
    public bool SupportsImages => OpenRouterEmbeddingModels.Find(ModelName)?.SupportsImages ?? true;

    /// <summary>
    /// Описание выбранной модели из каталога; null, если модель в каталоге не значится
    /// </summary>
    public OpenRouterEmbeddingModel ModelInfo => OpenRouterEmbeddingModels.Find(ModelName);

    /// <summary>
    /// Эмбеддер поверх OpenRouter
    /// </summary>
    /// <param name="apiKey">Ключ OpenRouter</param>
    /// <param name="modelName">Слаг модели, см. <see cref="OpenRouterEmbeddingModels"/></param>
    /// <param name="dimensions">Требуемая размерность вектора (только для моделей с Matryoshka)</param>
    /// <param name="httpClient">Внешний HttpClient (например, с прокси). Если не задан — создаётся свой и утилизируется вместе с эмбеддером.</param>
    public OpenRouterEmbedder(
        string apiKey,
        string modelName = OpenRouterEmbeddingModels.TextEmbedding3Small,
        int? dimensions = null,
        HttpClient httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey), "Ключ OpenRouter не может быть пустым");
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentNullException(nameof(modelName), "Имя модели не может быть пустым");

        _apiKey = apiKey;
        ModelName = modelName;
        Dimensions = dimensions;

        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(7) };
    }

    /// <inheritdoc/>
    public override string GetDetailedInstruct(string question) =>
        string.IsNullOrWhiteSpace(QueryInstruction)
            ? question
            : $"Instruct: {QueryInstruction}\nQuery: {question}";

    /// <inheritdoc/>
    public override async Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var items = texts?.ToArray() ?? throw new ArgumentNullException(nameof(texts));
        return await EncodeBatchedAsync(items, chunk => (object)chunk, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Vector> EncodeAsync(MessageContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var vectors = await EncodeAsync(new[] { content }, cancellationToken);
        return vectors.FirstOrDefault()
            ?? throw new InvalidOperationException("Embedding result is empty or null");
    }

    /// <inheritdoc/>
    public async Task<Vector[]> EncodeAsync(IEnumerable<MessageContent> contents, CancellationToken cancellationToken = default)
    {
        var items = contents?.ToArray() ?? throw new ArgumentNullException(nameof(contents));

        if (!SupportsImages && items.Any(c => c != null && c.OfType<ImageContent>().Any()))
            throw new NotSupportedException(
                $"Модель '{ModelName}' не принимает изображения. Мультимодальные модели: " +
                string.Join(", ", OpenRouterEmbeddingModels.Multimodal.Select(m => m.Id)));

        return await EncodeBatchedAsync(
            items,
            chunk => (object)chunk.Select(c => new OpenRouterEmbeddingInput(c)).ToArray(),
            cancellationToken);
    }

    /// <summary>
    /// Совместный эмбеддинг изображения и (необязательного) текстового описания
    /// </summary>
    /// <param name="imageUrl">Ссылка на изображение либо data:...;base64,... URI</param>
    /// <param name="text">Сопровождающий текст</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    public Task<Vector> EncodeImageAsync(string imageUrl, string text = null, CancellationToken cancellationToken = default) =>
        EncodeAsync(BuildContent(text, new ImageContent(imageUrl)), cancellationToken);

    /// <summary>
    /// Совместный эмбеддинг изображения из байтов и (необязательного) текстового описания
    /// </summary>
    /// <param name="image">Байты изображения, MIME-тип определяется по сигнатуре</param>
    /// <param name="text">Сопровождающий текст</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    public Task<Vector> EncodeImageAsync(IEnumerable<byte> image, string text = null, CancellationToken cancellationToken = default) =>
        EncodeAsync(BuildContent(text, new ImageContent(image)), cancellationToken);

    private static MessageContent BuildContent(string text, ImageContent image)
    {
        var content = new MessageContent();
        if (!string.IsNullOrEmpty(text))
            content.AddText(text);
        content.Add(image);
        return content;
    }

    /// <summary>
    /// Режет вход на пакеты по <see cref="BatchSize"/> и склеивает результаты в исходном порядке.
    /// Пакеты идут последовательно, чтобы не упираться в лимиты OpenRouter.
    /// </summary>
    private async Task<Vector[]> EncodeBatchedAsync<T>(
        T[] items,
        Func<T[], object> toInput,
        CancellationToken cancellationToken)
    {
        if (items.Length == 0)
            return [];

        if (BatchSize < 1)
            throw new InvalidOperationException($"{nameof(BatchSize)} должен быть положительным");

        var result = new List<Vector>(items.Length);
        foreach (var chunk in items.Chunk(BatchSize))
            result.AddRange(await SendAsync(toInput(chunk), chunk.Length, cancellationToken));

        return [.. result];
    }

    private async Task<Vector[]> SendAsync(object input, int expectedCount, CancellationToken cancellationToken)
    {
        var modelInfo = ModelInfo;
        if (Dimensions.HasValue && modelInfo != null && !modelInfo.SupportsDimensions)
            throw new NotSupportedException($"Модель '{ModelName}' не поддерживает параметр dimensions");

        var request = new OpenRouterEmbeddingsRequest
        {
            Model = ModelName,
            Input = input,
            Dimensions = Dimensions,
            Provider = PreferredProvider
        };

        Exception lastException = null;

        for (int attempt = 0; ; attempt++)
        {
            bool fatal = false;
            using var timeoutCts = new CancellationTokenSource(RequestTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
                {
                    Content = JsonContent.Create(request)
                };
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                if (!string.IsNullOrEmpty(HttpReferer))
                    message.Headers.TryAddWithoutValidation("HTTP-Referer", HttpReferer);
                if (!string.IsNullOrEmpty(AppTitle))
                    message.Headers.TryAddWithoutValidation("X-Title", AppTitle);

                using var response = await _httpClient.SendAsync(message, linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
                    var error = new HttpRequestException(
                        $"OpenRouter embeddings ({ModelName}) вернул {(int)response.StatusCode}: {(body ?? "").TruncateForLogging()}");

                    if (attempt >= MaxRetries || !IsTransient(response.StatusCode))
                    {
                        fatal = true;
                        throw error;
                    }

                    lastException = error;
                    await Task.Delay(GetRetryDelay(response, attempt), cancellationToken);
                    continue;
                }

                var content = await response.Content.ReadFromJsonAsync<OpenRouterEmbeddingsResponse>(linkedCts.Token);

                // Порядок элементов сервером не гарантируется, восстанавливаем по index
                var vectors = content?.Data?
                    .OrderBy(d => d.Index)
                    .Select(d => d.Embedding)
                    .ToArray() ?? [];

                if (vectors.Length != expectedCount || vectors.Any(v => v == null))
                {
                    fatal = true;
                    throw new InvalidOperationException(
                        $"OpenRouter вернул {vectors.Length} векторов вместо {expectedCount}");
                }

                LastUsage = content.Usage;

                return NormalizeVectors ? [.. vectors.Select(ToUnitLength)] : vectors;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (!fatal && attempt < MaxRetries)
            {
                lastException = ex;
                try
                {
                    await Task.Delay(GetBackoff(attempt), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw lastException;
                }
            }
        }
    }

    /// <summary>
    /// Ошибки, которые имеет смысл повторить: перегрузка провайдера, лимиты, таймауты шлюза
    /// </summary>
    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||
        statusCode == HttpStatusCode.RequestTimeout ||
        (int)statusCode >= 500;

    private static TimeSpan GetBackoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>
    /// Уважаем Retry-After, если сервер его прислал, иначе экспоненциальная пауза
    /// </summary>
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;
        if (retryAfter?.Date is DateTimeOffset date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                return wait;
        }
        return GetBackoff(attempt);
    }

    /// <summary>
    /// Приведение вектора к единичной длине (L2)
    /// </summary>
    private static Vector ToUnitLength(Vector vector)
    {
        var norm = vector.NormL2();
        return norm > 0 ? new Vector(vector.Select(x => x / norm).ToArray()) : vector;
    }

    /// <summary>
    /// Освобождает ресурсы, используемые HttpClient (только если клиент создан самим эмбеддером).
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        if (disposing && _ownsHttpClient)
            _httpClient?.Dispose();
    }
}

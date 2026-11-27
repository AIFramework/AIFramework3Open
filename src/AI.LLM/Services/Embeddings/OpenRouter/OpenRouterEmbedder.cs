using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Infrastructure.Resilience;
using AI.LLM.Services.Embeddings.Base;
using AI.LLM.Services.Embeddings.OpenRouter.Models;
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

    private readonly ProviderHttpClient _http;
    private readonly bool _ownsHttp;
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
    /// Число повторов при временных сбоях (сеть, 408, 425, 429, 5xx); правило — <see cref="Retry"/>.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Правило повторов (паузы, предел <c>Retry-After</c>); число попыток задаёт <see cref="MaxRetries"/>.
    /// </summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default;

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

        ModelName = modelName;
        Dimensions = dimensions;

        _ownsHttp = true;
        _http = new ProviderHttpClient(new ProviderHttpClientOptions
        {
            Keys = ApiKeyPool.Single(apiKey),
            Client = httpClient,
            RequestTimeout = TimeSpan.FromMinutes(7),
        });
    }

    /// <summary>
    /// Эмбеддер поверх общего клиента провайдера: пул ключей и прокси задаёт клиент.
    /// </summary>
    /// <param name="http">Клиент провайдера; им владеет вызывающий.</param>
    /// <param name="modelName">Слаг модели, см. <see cref="OpenRouterEmbeddingModels"/></param>
    /// <param name="dimensions">Требуемая размерность вектора (только для моделей с Matryoshka)</param>
    public OpenRouterEmbedder(
        ProviderHttpClient http,
        string modelName = OpenRouterEmbeddingModels.TextEmbedding3Small,
        int? dimensions = null)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentNullException(nameof(modelName), "Имя модели не может быть пустым");

        _http = http ?? throw new ArgumentNullException(nameof(http));
        ModelName = modelName;
        Dimensions = dimensions;
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

        RetryPolicy retry = (Retry ?? RetryPolicy.Default) with { MaxAttempts = Math.Max(0, MaxRetries) + 1 };
        return await retry.RunAsync(
            (_, token) => AttemptAsync(request, expectedCount, token), RepeatSafety.Safe, cancellationToken);
    }

    /// <summary>Одна попытка: векторы или исключение с кодом ответа (и сроком <c>Retry-After</c>).</summary>
    private async Task<Vector[]> AttemptAsync(OpenRouterEmbeddingsRequest request, int expectedCount, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(RequestTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var response = await _http.SendAsync(
            () =>
            {
                var message = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = JsonContent.Create(request) };
                if (!string.IsNullOrEmpty(HttpReferer))
                    message.Headers.TryAddWithoutValidation("HTTP-Referer", HttpReferer);
                if (!string.IsNullOrEmpty(AppTitle))
                    message.Headers.TryAddWithoutValidation("X-Title", AppTitle);
                return message;
            },
            HttpCompletionOption.ResponseContentRead,
            linkedCts.Token);

        await ProviderHttpClient.EnsureSuccessAsync(response, $"OpenRouter embeddings ({ModelName})", linkedCts.Token);

        var content = await response.Content.ReadFromJsonAsync<OpenRouterEmbeddingsResponse>(linkedCts.Token);

        // Порядок элементов сервером не гарантируется, восстанавливаем по index
        var vectors = content?.Data?
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToArray() ?? [];

        if (vectors.Length != expectedCount || vectors.Any(v => v == null))
            throw new InvalidOperationException($"OpenRouter вернул {vectors.Length} векторов вместо {expectedCount}");

        LastUsage = content.Usage;

        return NormalizeVectors ? [.. vectors.Select(ToUnitLength)] : vectors;
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

        if (disposing && _ownsHttp)
            _http.Dispose();
    }
}

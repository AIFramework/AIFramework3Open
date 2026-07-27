using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Providers.OpenRouter;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Services.Reranking.Base;
using Serilog;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AI.LLM.Services.Reranking.OpenRouter;

/// <summary>
/// Реранкер поверх OpenRouter (https://openrouter.ai/api/v1/rerank).
/// Кросс-энкодер переупорядочивает кандидатов, найденных эмбеддингами, — второй этап RAG.
/// Модель <see cref="OpenRouterRerankModels.NemotronRerankVL"/> оценивает и изображения,
/// поэтому страницы документа можно ранжировать как картинки, без OCR.
/// </summary>
public class OpenRouterReranker : RerankerBase<string, string>, IMultimodalRerankerService, IDisposable
{
    /// <summary>
    /// Эндпоинт реранкинга OpenRouter
    /// </summary>
    public const string DefaultApiUrl = "https://openrouter.ai/api/v1/rerank";

    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private int _disposed; // 0 = not disposed, 1 = disposed
    private int _imageWarningLogged; // предупреждение о картинках пишем один раз на экземпляр

    /// <summary>
    /// URL эндпоинта (можно переопределить на прокси-шлюз)
    /// </summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;

    /// <summary>
    /// Имя модели реранкера
    /// </summary>
    public string RerankerModelName { get; set; }

    /// <summary>
    /// Маршрутизация по провайдерам OpenRouter
    /// </summary>
    public ProviderPreference PreferredProvider { get; set; }

    /// <summary>
    /// Число повторов при сетевых ошибках, 429 и 5xx
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Таймаут одного запроса
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Заголовок HTTP-Referer для статистики OpenRouter (необязательный)
    /// </summary>
    public string HttpReferer { get; set; }

    /// <summary>
    /// Заголовок X-Title — имя приложения в статистике OpenRouter (необязательный)
    /// </summary>
    public string AppTitle { get; set; }

    /// <summary>
    /// Потребление и стоимость последнего успешного запроса. У Cohere заполняется search_units,
    /// а не токены — тарификация идёт по ним.
    /// </summary>
    public OpenRouterRerankUsage LastUsage { get; private set; }

    /// <summary>
    /// Заявлена ли за выбранной моделью работа с изображениями. Для модели вне каталога возвращает true.
    /// Флаг ни на что не влияет жёстко: картинки на «текстовой» модели дают предупреждение в лог,
    /// а запрос всё равно уходит — судит провайдер.
    /// </summary>
    public bool SupportsImages => OpenRouterRerankModels.Find(RerankerModelName)?.SupportsImages ?? true;

    /// <summary>
    /// Описание выбранной модели из каталога; null, если модель в каталоге не значится
    /// </summary>
    public OpenRouterRerankModel ModelInfo => OpenRouterRerankModels.Find(RerankerModelName);

    /// <summary>
    /// Реранкер поверх OpenRouter
    /// </summary>
    /// <param name="apiKey">Ключ OpenRouter</param>
    /// <param name="rerankerModelName">Слаг модели, см. <see cref="OpenRouterRerankModels"/></param>
    /// <param name="httpClient">Внешний HttpClient (например, с прокси). Если не задан — создаётся свой и утилизируется вместе с реранкером.</param>
    public OpenRouterReranker(
        string apiKey,
        string rerankerModelName = OpenRouterRerankModels.CohereRerankV35,
        HttpClient httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey), "Ключ OpenRouter не может быть пустым");
        if (string.IsNullOrWhiteSpace(rerankerModelName))
            throw new ArgumentNullException(nameof(rerankerModelName), "Имя модели не может быть пустым");

        _apiKey = apiKey;
        RerankerModelName = rerankerModelName;

        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(7) };
    }

    /// <summary>
    /// Отправляет запрос на ранжирование текстовых документов
    /// </summary>
    /// <param name="query">Запрос, относительно которого ранжируются документы</param>
    /// <param name="documents">Документы</param>
    /// <param name="topN">Сколько лучших вернуть; null — все</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ сервера, результаты по убыванию релевантности</returns>
    public Task<OpenRouterRerankResponse> RerankAsync(
        string query,
        IEnumerable<string> documents,
        int? topN = null,
        CancellationToken cancellationToken = default)
    {
        var items = documents?.ToArray() ?? throw new ArgumentNullException(nameof(documents));
        return SendAsync(query, items, items.Length, topN, cancellationToken);
    }

    /// <summary>
    /// Отправляет запрос на ранжирование документов, в том числе с изображениями
    /// </summary>
    /// <param name="query">Запрос, относительно которого ранжируются документы</param>
    /// <param name="documents">Документы (текст и/или изображение)</param>
    /// <param name="topN">Сколько лучших вернуть; null — все</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ сервера, результаты по убыванию релевантности</returns>
    public Task<OpenRouterRerankResponse> RerankAsync(
        string query,
        IEnumerable<RerankDocument> documents,
        int? topN = null,
        CancellationToken cancellationToken = default)
    {
        var items = documents?.ToArray() ?? throw new ArgumentNullException(nameof(documents));

        if (items.Any(d => d == null || d.IsEmpty))
            throw new ArgumentException("Документ должен содержать текст либо изображение", nameof(documents));

        if (!SupportsImages && items.Any(d => !string.IsNullOrEmpty(d.Image)))
            WarnAboutImages();

        return SendAsync(query, items, items.Length, topN, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<Vector> SimsAsync(string query, IEnumerable<string> documents, string instruct = null)
    {
        var items = documents?.ToArray() ?? throw new ArgumentNullException(nameof(documents));
        if (items.Length == 0)
            return new Vector(0);

        // top_n не передаём: нужны оценки всех документов, а не только лучших
        var response = await SendAsync(BuildQuery(query, instruct), items, items.Length, null, default);
        return ToScores(response, items.Length);
    }

    /// <inheritdoc/>
    public async Task<Vector> SimsAsync(
        string query,
        IEnumerable<RerankDocument> documents,
        string instruct = null,
        CancellationToken cancellationToken = default)
    {
        var items = documents?.ToArray() ?? throw new ArgumentNullException(nameof(documents));
        if (items.Length == 0)
            return new Vector(0);

        var response = await RerankAsync(BuildQuery(query, instruct), items, null, cancellationToken);
        return ToScores(response, items.Length);
    }

    /// <inheritdoc/>
    public override async Task<double> SimAsync(string query, string document, string instruct = null)
    {
        var scores = await SimsAsync(query, new[] { document }, instruct);
        return scores[0];
    }

    /// <summary>
    /// Мера схожести. Синхронная обёртка над <see cref="SimAsync"/> — блокирует поток,
    /// в асинхронном коде предпочтительнее асинхронный вариант.
    /// </summary>
    public override double Sim(string query, string document, string instruct = null) =>
        SimAsync(query, document, instruct).GetAwaiter().GetResult();

    /// <summary>
    /// Вектор близостей. Синхронная обёртка над <see cref="SimsAsync(string, IEnumerable{string}, string)"/> —
    /// блокирует поток, в асинхронном коде предпочтительнее асинхронный вариант.
    /// </summary>
    public override Vector Sims(string query, IEnumerable<string> documents, string instruct = null) =>
        SimsAsync(query, documents, instruct).GetAwaiter().GetResult();

    /// <summary>
    /// Топ-k документов. В отличие от базовой реализации отсечение делает сервер (top_n),
    /// поэтому по сети возвращается только нужное.
    /// </summary>
    public override async Task<List<(int, double)>> TopKSimsAsync(
        string query,
        IEnumerable<string> documents,
        int k = 5,
        string instruct = null)
    {
        var items = documents?.ToArray() ?? throw new ArgumentNullException(nameof(documents));
        if (items.Length == 0)
            return [];

        var response = await SendAsync(BuildQuery(query, instruct), items, items.Length, Math.Min(k, items.Length), default);
        return ToRanking(response);
    }

    /// <inheritdoc/>
    public async Task<List<(int Index, double Score)>> TopKAsync(
        string query,
        IEnumerable<RerankDocument> documents,
        int k = 5,
        string instruct = null,
        CancellationToken cancellationToken = default)
    {
        var items = documents?.ToArray() ?? throw new ArgumentNullException(nameof(documents));
        if (items.Length == 0)
            return [];

        var response = await RerankAsync(BuildQuery(query, instruct), items, Math.Min(k, items.Length), cancellationToken);
        return ToRanking(response);
    }

    /// <summary>
    /// Предупреждает о картинках на модели, за которой мультимодальность не заявлена.
    /// Запрос всё равно уходит: каталог может отставать от того, что модель умеет на самом деле,
    /// а окончательный ответ за провайдером. Логируем один раз на экземпляр, чтобы не забивать лог.
    /// </summary>
    private void WarnAboutImages()
    {
        if (Interlocked.CompareExchange(ref _imageWarningLogged, 1, 0) != 0) return;

        Log.Warning(
            "OpenRouterReranker: за моделью {Model} мультимодальность не заявлена, но в документах есть изображения. " +
            "Запрос отправлен как есть; при ошибке провайдера используйте {Multimodal}",
            RerankerModelName,
            string.Join(", ", OpenRouterRerankModels.Multimodal.Select(m => m.Id)));
    }

    /// <summary>
    /// В API реранкинга нет отдельного поля для инструкции, поэтому она уходит в начале запроса
    /// </summary>
    private static string BuildQuery(string query, string instruct) =>
        string.IsNullOrWhiteSpace(instruct) ? query : $"{instruct}\n{query}";

    /// <summary>
    /// Раскладывает оценки по исходным позициям документов
    /// </summary>
    private static Vector ToScores(OpenRouterRerankResponse response, int documentCount)
    {
        var scores = new double[documentCount];
        var filled = new bool[documentCount];

        foreach (var result in response.Results)
        {
            if (result.Index < 0 || result.Index >= documentCount)
                throw new InvalidOperationException(
                    $"OpenRouter вернул индекс {result.Index} вне диапазона документов (0..{documentCount - 1})");

            scores[result.Index] = result.RelevanceScore;
            filled[result.Index] = true;
        }

        if (filled.Any(f => !f))
            throw new InvalidOperationException(
                $"OpenRouter оценил {response.Results.Count} документов из {documentCount}");

        return new Vector(scores);
    }

    private static List<(int, double)> ToRanking(OpenRouterRerankResponse response) =>
        [.. response.Results
            .OrderByDescending(r => r.RelevanceScore)
            .Select(r => (r.Index, r.RelevanceScore))];

    private async Task<OpenRouterRerankResponse> SendAsync(
        string query,
        object documents,
        int documentCount,
        int? topN,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Запрос не может быть пустым", nameof(query));
        if (documentCount == 0)
            throw new ArgumentException("Список документов пуст", nameof(documents));

        var request = new OpenRouterRerankRequest
        {
            Model = RerankerModelName,
            Query = query,
            Documents = documents,
            TopN = topN,
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
                        $"OpenRouter rerank ({RerankerModelName}) вернул {(int)response.StatusCode}: {(body ?? "").TruncateForLogging()}");

                    if (attempt >= MaxRetries || !IsTransient(response.StatusCode))
                    {
                        fatal = true;
                        throw error;
                    }

                    lastException = error;
                    await Task.Delay(GetRetryDelay(response, attempt), cancellationToken);
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<OpenRouterRerankResponse>(linkedCts.Token);

                if (result?.Results == null)
                {
                    fatal = true;
                    throw new InvalidOperationException("OpenRouter rerank вернул пустой результат");
                }

                LastUsage = result.Usage;
                return result;
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
    /// Освобождает ресурсы, используемые HttpClient (только если клиент создан самим реранкером).
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

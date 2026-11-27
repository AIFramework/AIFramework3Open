#nullable enable

using System.Net;
using AI.LLM.Infrastructure.Resilience;

namespace AI.LLM.Infrastructure.Http;

/// <summary>Настройки <see cref="ProviderHttpClient"/>: ключи, маршруты, сроки и постоянные заголовки.</summary>
public sealed class ProviderHttpClientOptions
{
    /// <summary>Пул ключей; <see langword="null"/>: запросы без ключа (или с <see cref="ProviderHttpClient.Authentication"/>).</summary>
    public ApiKeyPool? Keys { get; init; }

    /// <summary>Прокси; <see langword="null"/> или пусто: прямой маршрут.</summary>
    public IEnumerable<WebProxy>? Proxies { get; init; }

    /// <summary>Сколько сетевых сбоев подряд выводят прокси в карантин.</summary>
    public int ProxyFailuresBeforeCooldown { get; init; } = 1;

    /// <summary>Срок карантина прокси.</summary>
    public TimeSpan ProxyCooldown { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Сколько прокси один запрос пробует при сетевых сбоях.</summary>
    public int MaxProxiesPerRequest { get; init; } = 5;

    /// <summary>
    /// Все прокси в карантине: <see langword="true"/> — отказ (<see cref="InvalidOperationException"/>), <see langword="false"/> —
    /// ближайший к возвращению (лучше отправить, чем не отправить).
    /// </summary>
    public bool RequireAvailableProxy { get; init; }

    /// <summary>Тайм-аут установки соединения.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Тайм-аут всего запроса (долгие рассуждающие модели).</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(18);

    /// <summary>Сколько ждать заголовков ответа: защита от сервера, который принял запрос и молчит.</summary>
    public TimeSpan ResponseHeadersTimeout { get; init; } = TimeSpan.FromSeconds(70);

    /// <summary>Общий срок на все попытки через разные маршруты; <see langword="null"/>: только <see cref="RequestTimeout"/>.</summary>
    public TimeSpan? GlobalTimeout { get; init; }

    /// <summary>Сколько запросов одновременно; <see langword="null"/>: без ограничения.</summary>
    public int? MaxConcurrentRequests { get; init; }

    /// <summary>User-Agent; <see langword="null"/>: обычный браузерный.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Постоянные заголовки (атрибуция приложения у провайдера и подобное).</summary>
    public IEnumerable<KeyValuePair<string, string>>? DefaultHeaders { get; init; }

    /// <summary>Автоматические перенаправления.</summary>
    public bool AllowAutoRedirect { get; init; } = true;

    /// <summary>Куки.</summary>
    public bool UseCookies { get; init; }

    /// <summary>Контейнер кук; <see langword="null"/>: свой.</summary>
    public CookieContainer? Cookie { get; init; }

    /// <summary>Распаковка ответа.</summary>
    public DecompressionMethods? Decompression { get; init; } = DecompressionMethods.GZip | DecompressionMethods.Deflate;

    /// <summary>ВНИМАНИЕ: без проверки сертификата (только для отладки).</summary>
    public bool DisableCertificateValidation { get; init; }

    /// <summary>Отладочные события <see cref="ProviderHttpClient.OnDebugLog"/>.</summary>
    public bool EnableDebugLogging { get; init; }

    /// <summary>
    /// Готовый клиент для прямого маршрута (свой транспорт, тесты). Клиент не освобождается: им владеет вызывающий.
    /// С прокси не сочетается.
    /// </summary>
    public HttpClient? Client { get; init; }

    /// <summary>Обработчик для прямого маршрута (тесты); освобождается вместе с клиентом. С прокси не сочетается.</summary>
    public HttpMessageHandler? Handler { get; init; }

    /// <summary>Базовый адрес для относительных адресов запросов; <see langword="null"/>: адреса абсолютные.</summary>
    public Uri? BaseAddress { get; init; }

    /// <summary>Часы карантина; <see langword="null"/>: <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? Time { get; init; }
}

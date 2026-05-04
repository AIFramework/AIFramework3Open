using System.Net;

namespace AI.LLM.Infrastructure.Http;

/// <summary>
/// Настройки для HTTP клиента с прокси
/// </summary>
public class ProxyHTTPClientOptions
{
    /// <summary>
    /// Разрешить автоматические редиректы
    /// </summary>
    public bool AllowAutoRedirect { get; set; } = true;

    /// <summary>
    /// Использовать куки
    /// </summary>
    public bool UseCookies { get; set; } = false;

    /// <summary>
    /// Контейнер для кук
    /// </summary>
    public CookieContainer Cookie { get; set; }

    /// <summary>
    /// Методы декомпрессии
    /// </summary>
    public DecompressionMethods? DecompressionMethods { get; set; } =
        System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;

    /// <summary>
    /// User Agent для запросов
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Таймаут на установку соединения (ConnectTimeout)
    /// Защита от зависших proxy или недоступных серверов
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Таймаут на один запрос (весь запрос включая получение ответа)
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = ProxyHTTPClient.DefaultRequestTimeout;

    /// <summary>
    /// Глобальный таймаут для всех попыток
    /// С учетом retry через разные прокси
    /// </summary>
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromMinutes(35);

    /// <summary>
    /// Максимум одновременных запросов
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Включить отладочное логирование через события
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// ВНИМАНИЕ: Отключить проверку SSL-сертификатов (небезопасно!)
    /// Используйте только для тестирования или когда абсолютно необходимо.
    /// По умолчанию: false (проверка включена)
    /// </summary>
    public bool DisableCertificateValidation { get; set; } = false;
}

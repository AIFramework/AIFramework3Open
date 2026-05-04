using System.Net;
using System.Security;

namespace AI.LLM.Clients.ImageGeneration;

/// <summary>
/// Настройки защиты от SSRF-атак при загрузке изображений по URL.
/// Позволяет ограничить допустимые хосты или полностью отключить проверку.
/// </summary>
public sealed class SsrfGuardOptions
{
    /// <summary>
    /// Включить проверку URL перед загрузкой. По умолчанию: true.
    /// Установите false только для полностью доверенных/изолированных сред.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Список разрешённых хостов (hostname без порта, например "cdn.openai.com").
    /// Если список пуст и Enabled=true — хост не ограничивается, но блокируются
    /// приватные диапазоны IP (loopback, RFC-1918, link-local).
    /// Если список непустой — пропускаются только указанные хосты.
    /// </summary>
    public IReadOnlyCollection<string> AllowedHosts { get; init; } = [];

    /// <summary>
    /// Блокировать loopback, приватные и link-local адреса (RFC-1918, 169.254.x.x).
    /// Применяется только когда Enabled=true. По умолчанию: true.
    /// </summary>
    public bool BlockPrivateRanges { get; init; } = true;

    /// <summary>
    /// Разрешённые схемы URI. По умолчанию только https и http.
    /// </summary>
    public IReadOnlyCollection<string> AllowedSchemes { get; init; } = ["https", "http"];

    // --- Фабричные методы ---

    /// <summary>Защита включена: блокируются приватные IP, хост не ограничен.</summary>
    public static SsrfGuardOptions Default => new();

    /// <summary>Проверка полностью отключена. Использовать только в локальной dev-среде.</summary>
    public static SsrfGuardOptions Disabled => new() { Enabled = false };

    /// <summary>Только хосты OpenAI DALL-E.</summary>
    public static SsrfGuardOptions OpenAiOnly => new()
    {
        AllowedHosts = [
            "oaidalleapiprodscus.blob.core.windows.net",
            "cdn.openai.com",
            "images.openai.com",
        ],
    };

    /// <summary>
    /// Создать настройки с явным списком разрешённых хостов.
    /// </summary>
    public static SsrfGuardOptions WithHosts(params string[] hosts) => new()
    {
        AllowedHosts = hosts,
    };

    // --- Внутренняя логика валидации ---

    /// <summary>
    /// Проверяет URL и выбрасывает <see cref="SecurityException"/> при нарушении политики.
    /// </summary>
    /// <exception cref="SecurityException">URL нарушает настроенную политику.</exception>
    public void Validate(string url)
    {
        if (!Enabled) return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new SecurityException($"SSRF Guard: некорректный формат URL '{url}'.");

        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            throw new SecurityException($"SSRF Guard: схема '{uri.Scheme}' не разрешена.");

        if (AllowedHosts.Count > 0 &&
            !AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                $"SSRF Guard: хост '{uri.Host}' отсутствует в allowlist. " +
                $"Разрешены: {string.Join(", ", AllowedHosts)}");
        }

        if (BlockPrivateRanges)
            EnsureNotPrivate(uri.Host);
    }

    private static void EnsureNotPrivate(string host)
    {
        var lower = host.ToLowerInvariant();

        if (lower is "localhost" or "ip6-localhost" or "ip6-loopback")
            Throw(host);

        if (!IPAddress.TryParse(host, out var ip))
            return; // hostname — не IP, дальнейшую проверку оставляем DNS

        if (IPAddress.IsLoopback(ip))
            Throw(host);

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 10) Throw(host);                                       // 10.0.0.0/8
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) Throw(host);         // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168) Throw(host);                      // 192.168.0.0/16
            if (b[0] == 169 && b[1] == 254) Throw(host);                      // 169.254.0.0/16 (link-local / cloud metadata)
            if (b[0] == 0) Throw(host);                                        // 0.0.0.0/8
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
                Throw(host);
        }

        static void Throw(string h) =>
            throw new SecurityException($"SSRF Guard: адрес '{h}' находится в блокируемом диапазоне (loopback / приватная сеть / link-local).");
    }
}

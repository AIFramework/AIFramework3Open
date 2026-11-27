using System.Net;
using System.Text.Json;

namespace AI.LLM.Infrastructure.Http;

/// <summary>Список прокси из файла и сборка <see cref="WebProxy"/> из данных.</summary>
public static class ProxyList
{
    /// <summary>Читает список прокси из JSON-файла (массив <see cref="ProxyData"/>).</summary>
    /// <param name="jsonFilePath">Путь к файлу.</param>
    public static List<WebProxy> FromJson(string jsonFilePath)
    {
        if (string.IsNullOrWhiteSpace(jsonFilePath))
            throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(jsonFilePath));

        if (!File.Exists(jsonFilePath))
            throw new FileNotFoundException($"Файл не найден: {jsonFilePath}");

        try
        {
            var proxyDataList = JsonSerializer.Deserialize<List<ProxyData>>(File.ReadAllText(jsonFilePath));

            if (proxyDataList == null || proxyDataList.Count == 0)
                throw new InvalidOperationException("Файл не содержит данных о прокси.");

            return proxyDataList.Select(ToWebProxy).ToList();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Ошибка при разборе JSON файла: {ex.Message}", ex);
        }
    }

    /// <summary>Собирает <see cref="WebProxy"/> из данных.</summary>
    /// <param name="proxyData">Адрес, порт, логин и пароль.</param>
    public static WebProxy ToWebProxy(ProxyData proxyData)
    {
        ArgumentNullException.ThrowIfNull(proxyData);

        if (string.IsNullOrWhiteSpace(proxyData.Address))
            throw new ArgumentException("Адрес прокси не может быть пустым.");

        if (proxyData.Port <= 0 || proxyData.Port > 65535)
            throw new ArgumentException($"Некорректный порт: {proxyData.Port}");

        try
        {
            var proxy = new WebProxy(new Uri($"http://{proxyData.Address}:{proxyData.Port}"), BypassOnLocal: false);

            if (!string.IsNullOrEmpty(proxyData.Login) && !string.IsNullOrEmpty(proxyData.Password))
            {
                proxy.UseDefaultCredentials = false;
                proxy.Credentials = new NetworkCredential(userName: proxyData.Login, password: proxyData.Password);
            }

            return proxy;
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException(
                $"Некорректный формат адреса прокси: {proxyData.Address}:{proxyData.Port}. Ошибка: {ex.Message}",
                nameof(proxyData),
                ex);
        }
    }
}

using System.Text.Json.Serialization;

namespace AI.LLM.Infrastructure.Http;

[Serializable]
public class ProxyData
{
    /// <summary>
    /// Качество или приоритет прокси
    /// </summary>
    [JsonPropertyName("q")]
    public double Quality { get; set; }

    /// <summary>
    /// Где находится прокси-сервер
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; set; }

    /// <summary>
    /// IP адрес или домен прокси
    /// </summary>
    [JsonPropertyName("ip")]
    public string Address { get; set; }

    /// <summary>
    /// Порт на котором слушает прокси (от 0 до 65535)
    /// </summary>
    [JsonPropertyName("port")]
    public int Port { get; set; }

    /// <summary>
    /// Логин для авторизации на прокси
    /// </summary>
    [JsonPropertyName("login")]
    public string Login { get; set; }

    /// <summary>
    /// Пароль для авторизации на прокси
    /// </summary>
    [JsonPropertyName("password")]
    public string Password { get; set; }
}

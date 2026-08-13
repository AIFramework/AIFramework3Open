using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages.Content;

/// <summary>
/// Звук, вложенный в сообщение: содержимое файла в base64 и его формат.
/// </summary>
/// <remarks>
/// Ссылкой звук не передаётся, в отличие от изображения: в формате chat/completions
/// у аудио-части есть только поле данных, и провайдеры принимают именно байты.
/// </remarks>
public class InputAudio
{
    /// <summary>Содержимое файла в base64, БЕЗ префикса data-URL.</summary>
    [JsonPropertyName("data")]
    public string Data { get; set; }

    /// <summary>Формат контейнера: <c>wav</c>, <c>mp3</c>, <c>ogg</c>, <c>flac</c>, <c>m4a</c>.</summary>
    [JsonPropertyName("format")]
    public string Format { get; set; }
}

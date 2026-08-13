using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages.Content;

/// <summary>
/// Звуковая часть сообщения (<c>input_audio</c>) — вход мультимодальной модели,
/// которая СЛЫШИТ запись: расшифровка речи, ответ по содержанию разговора.
/// </summary>
/// <remarks>
/// Отдельный тип части, а не изображение с другим MIME: у звука своя форма
/// (<c>{data, format}</c> вместо <c>{url}</c>), и формат провайдеру нужен явно —
/// по base64 он контейнер не угадывает.
/// </remarks>
[Serializable]
public class AudioContent : IContentItem
{
    [JsonIgnore]
    public string Type => "input_audio";

    [JsonPropertyName("input_audio")]
    public InputAudio InputAudio { get; set; }


    public AudioContent() { }

    public AudioContent(string base64, string format)
    {
        InputAudio = new InputAudio { Data = base64, Format = format };
    }

    public AudioContent(IEnumerable<byte> audio)
    {
        byte[] bytes = audio.ToArray();
        InputAudio = new InputAudio
        {
            Data = Convert.ToBase64String(bytes),
            Format = DetectFormat(bytes),
        };
    }

    /// <summary>
    /// Формат контейнера по сигнатуре файла; не распознан — <c>mp3</c> как самый частый.
    /// </summary>
    public static string DetectFormat(byte[] bytes)
    {
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E')
            return "wav";
        if (bytes.Length >= 4 && bytes[0] == 'O' && bytes[1] == 'g' && bytes[2] == 'g' && bytes[3] == 'S')
            return "ogg";
        if (bytes.Length >= 4 && bytes[0] == 'f' && bytes[1] == 'L' && bytes[2] == 'a' && bytes[3] == 'C')
            return "flac";
        // ...ftyp в четвёртом-восьмом байтах — семейство MP4 (m4a, aac в контейнере).
        if (bytes.Length >= 8 && bytes[4] == 'f' && bytes[5] == 't' && bytes[6] == 'y' && bytes[7] == 'p')
            return "m4a";
        // ID3-тег либо сразу кадр MPEG (0xFF 0xEx/0xFx).
        if (bytes.Length >= 3 && bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3')
            return "mp3";
        return "mp3";
    }
}

namespace AI.Script.Hosting;

/// <summary>
/// Службы медиа, которые дает хост: генерация, декодирование и распознавание.
/// </summary>
/// <remarks>
/// Движок объявляет, что умеет язык, но не зашивает в себя ни одной конкретной службы: модель
/// картинок, распознавание речи и декодер видео у каждого хоста свои. Нет службы — функция
/// остается в справке с пометкой «не подключено», а не пропадает.
/// <para>
/// Генерация и распознавание считаются внешними вызовами: проходят политику сети и потолки
/// расходов прогона, как запрос к языковой модели. Декодеры работают на месте и сети не просят.
/// </para>
/// </remarks>
/// <param name="Images">Генератор изображений по описанию.</param>
/// <param name="Voice">Озвучка текста.</param>
/// <param name="Audio">Декодер звука, кроме WAV: WAV движок читает сам.</param>
/// <param name="Video">Декодер видео: сведения, кадры, звуковая дорожка.</param>
/// <param name="Ocr">Распознавание текста на изображении.</param>
/// <param name="Transcriber">Расшифровка речи с таймкодами.</param>
public sealed record ScriptMediaServices(
    IScriptImageGenerator? Images = null,
    IScriptSpeechSynthesizer? Voice = null,
    IScriptAudioDecoder? Audio = null,
    IScriptVideoDecoder? Video = null,
    IScriptTextRecognizer? Ocr = null,
    IScriptTranscriber? Transcriber = null)
{
    /// <summary>Хост без медиа-служб: работает только чтение WAV и разбор кадров, которые уже есть.</summary>
    public static readonly ScriptMediaServices None = new();
}

/// <summary>Файл, полученный от службы, и расход на него.</summary>
/// <param name="Data">Содержимое файла.</param>
/// <param name="MediaType">Медиатип: <c>image/png</c>, <c>audio/mpeg</c>.</param>
/// <param name="Tokens">Токенов потрачено; 0 — служба не сообщила.</param>
/// <param name="Cost">Стоимость в единицах биллинга; 0 — служба не сообщила.</param>
public sealed record ScriptMedia(byte[] Data, string MediaType, long Tokens = 0, decimal Cost = 0);

/// <summary>Звук: отсчеты по каналам от -1 до 1 и частота дискретизации.</summary>
public sealed record ScriptAudio(IReadOnlyList<float[]> Channels, int Rate);

/// <summary>Сведения о ролике.</summary>
public sealed record ScriptVideoInfo(TimeSpan Duration, double Fps, int Width, int Height);

/// <summary>Кадр ролика: момент и картинка в PNG либо JPEG.</summary>
public sealed record ScriptVideoFrame(TimeSpan At, byte[] Image);

/// <summary>Строка распознанного текста и где она на изображении.</summary>
/// <param name="Text">Текст строки.</param>
/// <param name="Region">Область «x,y,ширина,высота» в пикселях; <c>null</c> — служба области не дает.</param>
public sealed record RecognizedText(string Text, string? Region = null);

/// <summary>Отрезок расшифровки.</summary>
public sealed record TimedText(TimeSpan Start, TimeSpan End, string Text);

/// <summary>Итог распознавания текста с расходом.</summary>
public sealed record ScriptRecognition(IReadOnlyList<RecognizedText> Lines, long Tokens = 0, decimal Cost = 0);

/// <summary>Итог расшифровки с расходом.</summary>
public sealed record ScriptTranscript(IReadOnlyList<TimedText> Segments, long Tokens = 0, decimal Cost = 0);

/// <summary>Генератор изображений по описанию.</summary>
public interface IScriptImageGenerator
{
    /// <summary>Рисует изображение; ошибку службы сообщает исключением.</summary>
    Task<ScriptMedia> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>Озвучка текста.</summary>
public interface IScriptSpeechSynthesizer
{
    /// <summary>Озвучивает текст в звуковой файл.</summary>
    Task<ScriptMedia> SpeakAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>Декодер сжатого звука.</summary>
public interface IScriptAudioDecoder
{
    /// <summary>Декодирует файл; формат определяется по имени и содержимому.</summary>
    Task<ScriptAudio> DecodeAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Декодер видео.</summary>
public interface IScriptVideoDecoder
{
    /// <summary>Длительность, частота кадров и размер.</summary>
    Task<ScriptVideoInfo> InfoAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Кадры через равный шаг; нулевой шаг — каждый кадр. Не больше <paramref name="max"/>.</summary>
    Task<IReadOnlyList<ScriptVideoFrame>> FramesAsync(
        byte[] data, string fileName, TimeSpan every, int max, CancellationToken cancellationToken = default);

    /// <summary>Звуковая дорожка.</summary>
    Task<ScriptAudio> AudioAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Распознавание текста на изображении.</summary>
public interface IScriptTextRecognizer
{
    /// <summary>Распознает строки текста по порядку чтения.</summary>
    Task<ScriptRecognition> ReadAsync(byte[] image, string mediaType, CancellationToken cancellationToken = default);
}

/// <summary>Расшифровка речи.</summary>
public interface IScriptTranscriber
{
    /// <summary>Расшифровывает запись; формат по имени файла.</summary>
    Task<ScriptTranscript> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken = default);
}

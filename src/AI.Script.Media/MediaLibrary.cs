using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Media;

/// <summary>
/// Подключение пространств <c>audio</c>, <c>video</c>, <c>gen</c>, <c>ocr</c> и <c>speech</c>.
/// </summary>
/// <remarks>
/// Пространства подключаются всегда целиком, а службы по мере того, что есть у хоста: функция
/// без службы видна в справке с пометкой «не подключено» и предупреждает при проверке.
/// </remarks>
public static class MediaLibrary
{
    /// <summary>Подключает медиа с данными службами хоста.</summary>
    /// <param name="host">Хост.</param>
    /// <param name="services">Службы; <c>null</c> — ни одной, работает только WAV.</param>
    public static ScriptHost UseMedia(this ScriptHost host, ScriptMediaServices? services = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        ScriptMediaServices given = services ?? ScriptMediaServices.None;

        return host
            .Use(ScriptModule.FromObject(new AudioModule(given.Audio)))
            .Use(ScriptModule.FromObject(new VideoModule(given.Video)))
            .Use(ScriptModule.FromObject(new GenModule(given.Images, given.Voice)))
            .Use(ScriptModule.FromObject(new OcrModule(given.Ocr)))
            .Use(ScriptModule.FromObject(new SpeechModule(given.Transcriber)));
    }
}

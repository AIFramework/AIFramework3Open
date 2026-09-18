using System.Security.Cryptography;
using System.Text;
using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Vision;
using SkiaSharp;

namespace AI.Script.Media;

/// <summary>
/// Пространство <c>gen</c>: изображение и голос через генераторы хоста.
/// </summary>
/// <remarks>
/// Движок не знает ни одной службы генерации: какую модель картинок или какой голос брать, решает
/// хост. Вызов при этом платный и сетевой, поэтому идет по тем же правилам, что запрос к языковой
/// модели: политика сети, потолок вызовов до запроса, расход после. Опыт с картинками тогда
/// останавливается бюджетом так же, как опыт с текстами.
/// </remarks>
[ScriptModule("gen", "Генерация службами хоста: изображение по описанию, озвучка", Version = "0.1", Group = "сигналы")]
public sealed class GenModule(IScriptImageGenerator? images, IScriptSpeechSynthesizer? voice) : IScriptAvailability
{
    private const string NoImages = "генератор изображений хостом не подключен";
    private const string NoVoice = "озвучка текста хостом не подключена";

    /// <inheritdoc/>
    public string? Unavailable(string function) => function switch
    {
        "image" when images is null => NoImages,
        "speech" when voice is null => NoVoice,
        _ => null,
    };

    [ScriptFn("image", "Изображение по описанию: изображение cv", Example = "gen.image(\"report cover, flat illustration\")",
        Returns = ColorImage.TypeName)]
    public async Task<ScriptHandle> Image(IScriptContext context, [ScriptParam("описание изображения")] string prompt)
    {
        IScriptImageGenerator generator = MediaCalls.Require(images, "gen.image", NoImages);
        ScriptMedia media = await MediaCalls
            .ExternalAsync(context, "gen.image", token => generator.GenerateAsync(prompt, token), result => (result.Tokens, result.Cost))
            .ConfigureAwait(false);

        using SKBitmap? bitmap = SKBitmap.Decode(media.Data);

        if (bitmap is null)
            throw new ScriptError(DiagnosticCodes.FunctionFailed, $"gen.image: генератор вернул не изображение ({media.MediaType})");

        ColorImage image = ColorImage.From(bitmap);

        context.CountAllocation(3L * image.Width * image.Height);

        return new ScriptHandle(ColorImage.TypeName, image, image.ToString());
    }

    /// <summary>
    /// Озвучка в файл.
    /// </summary>
    /// <remarks>
    /// Возвращается путь, а не звук: голос приходит сжатым (mp3, ogg), а сжатый звук язык без декодера
    /// хоста не откроет. Файл же можно показать пользователю, приложить к отчету или открыть
    /// <c>audio.load</c>, если декодер есть.
    /// </remarks>
    [ScriptFn("speech", "Озвучивает текст в звуковой файл и возвращает его путь", Example = "gen.speech(\"Hello\", path: \"hello.mp3\")")]
    public async Task<string> Speech(
        IScriptContext context,
        [ScriptParam("текст")] string text,
        [ScriptParam("путь файла; пусто — имя по тексту")] string path = "")
    {
        IScriptSpeechSynthesizer synthesizer = MediaCalls.Require(voice, "gen.speech", NoVoice);
        ScriptMedia media = await MediaCalls
            .ExternalAsync(context, "gen.speech", token => synthesizer.SpeakAsync(text, token), result => (result.Tokens, result.Cost))
            .ConfigureAwait(false);

        string file = path.Length > 0 ? path : $"speech-{Hash(text)}.{Extension(media.MediaType)}";

        await context.Sandbox.WriteAsync(file, media.Data, context.Cancellation).ConfigureAwait(false);
        context.FileSaved(new ScriptFileInfo(file, media.Data.LongLength, DateTimeOffset.UtcNow));

        return file;
    }

    private static string Extension(string mediaType) => mediaType switch
    {
        "audio/wav" or "audio/x-wav" or "audio/wave" => "wav",
        "audio/ogg" => "ogg",
        "audio/opus" => "opus",
        "audio/flac" => "flac",
        "audio/aac" => "aac",
        _ => "mp3",
    };

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8].ToLowerInvariant();
}

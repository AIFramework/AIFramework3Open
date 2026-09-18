using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Vision;

namespace AI.Script.Media;

/// <summary>
/// Пространство <c>video</c>: ролик как кадры и звук.
/// </summary>
/// <remarks>
/// Декодирование дает хост, а все, что дальше, язык уже умеет: кадр это изображение
/// <c>cv</c>, дорожка это звук <c>audio</c>. Свое здесь только поиск смен сцен, потому что
/// он нужен почти каждому опыту над роликом и собирать его из <c>cv</c> каждый раз незачем.
/// </remarks>
[ScriptModule("video", "Видео через декодер хоста: сведения, кадры, смены сцен, звук", Version = "0.1", Group = "сигналы")]
public sealed class VideoModule(IScriptVideoDecoder? decoder) : IScriptAvailability
{
    /// <summary>Больше стольких кадров за вызов не берется: час ролика по кадру в секунду это уже 3600 картинок.</summary>
    public const int MaxFrames = 600;

    private const string Reason = "декодер видео хостом не подключен";

    /// <inheritdoc/>
    public string? Unavailable(string function) => decoder is null ? Reason : null;

    [ScriptFn("info", "Длительность, частота кадров и размер ролика", Example = "video.info(\"clip.mp4\").seconds")]
    public async Task<ScriptRecord> Info(IScriptContext context, [ScriptParam("путь к ролику")] string path)
    {
        ScriptVideoInfo info = await Decoder("video.info")
            .InfoAsync(await Bytes(context, path).ConfigureAwait(false), path, context.Cancellation).ConfigureAwait(false);

        return ScriptRecord.From(
        [
            new("seconds", ScriptValue.Num(info.Duration.TotalSeconds)),
            new("fps", ScriptValue.Num(info.Fps)),
            new("width", ScriptValue.Num(info.Width)),
            new("height", ScriptValue.Num(info.Height)),
        ]);
    }

    [ScriptFn("frames", "Кадры через равный шаг списком изображений cv", Example = "video.frames(\"clip.mp4\", every: 2s)")]
    public async Task<ScriptList> Frames(
        IScriptContext context,
        [ScriptParam("путь к ролику")] string path,
        [ScriptParam("шаг между кадрами; 0 — каждый кадр")] TimeSpan every = default,
        [ScriptParam("не больше стольких кадров")] int max = 300)
    {
        IReadOnlyList<ScriptVideoFrame> frames = await Decode(context, path, every, max, "video.frames").ConfigureAwait(false);
        var images = new ScriptValue[frames.Count];

        for (int i = 0; i < frames.Count; i++)
        {
            ColorImage image = SceneCuts.Decode(frames[i]);

            context.CountAllocation(3L * image.Width * image.Height);
            images[i] = ScriptValue.Handle(new ScriptHandle(ColorImage.TypeName, image, image.ToString()));
        }

        return ScriptList.Own(images);
    }

    /// <summary>
    /// Смены сцен по разности соседних кадров.
    /// </summary>
    /// <remarks>
    /// Кадры сравниваются в сером и уменьшенными: смена сцены это смена всей картинки, а шум и
    /// сжатие меняют отдельные пиксели. Порог это средняя разность яркости в долях полной шкалы.
    /// </remarks>
    [ScriptFn("scenes", "Сцены ролика таблицей: номер, начало и конец в секундах",
        Example = "len(video.scenes(\"clip.mp4\", threshold: 0.3))", Columns = "scene,start,end")]
    public async Task<ScriptTable> Scenes(
        IScriptContext context,
        [ScriptParam("путь к ролику")] string path,
        [ScriptParam("порог разности соседних кадров от 0 до 1")] double threshold = 0.3,
        [ScriptParam("шаг между кадрами; 0 — каждый кадр")] TimeSpan every = default,
        [ScriptParam("не больше стольких кадров")] int max = MaxFrames)
    {
        IReadOnlyList<ScriptVideoFrame> frames = await Decode(context, path, every, max, "video.scenes").ConfigureAwait(false);

        return SceneCuts.Table(SceneCuts.Find(frames, threshold));
    }

    [ScriptFn("audio", "Звуковая дорожка ролика записью audio", Example = "audio.loudness(video.audio(\"clip.mp4\"))")]
    public async Task<ScriptRecord> Audio(IScriptContext context, [ScriptParam("путь к ролику")] string path)
    {
        ScriptAudio audio = await Decoder("video.audio")
            .AudioAsync(await Bytes(context, path).ConfigureAwait(false), path, context.Cancellation).ConfigureAwait(false);

        return AudioModule.Record(context, audio);
    }

    private async Task<IReadOnlyList<ScriptVideoFrame>> Decode(
        IScriptContext context, string path, TimeSpan every, int max, string what)
    {
        IScriptVideoDecoder video = Decoder(what);
        byte[] bytes = await Bytes(context, path).ConfigureAwait(false);

        return await video.FramesAsync(bytes, path, every, Math.Clamp(max, 1, MaxFrames), context.Cancellation).ConfigureAwait(false);
    }

    private IScriptVideoDecoder Decoder(string what) => MediaCalls.Require(decoder, what, Reason);

    private static Task<byte[]> Bytes(IScriptContext context, string path) =>
        context.Sandbox.ReadAsync(path, context.Cancellation);
}

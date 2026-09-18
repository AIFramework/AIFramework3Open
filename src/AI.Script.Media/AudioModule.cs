using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Media;

/// <summary>
/// Пространство <c>audio</c>: звук из файла как запись с отсчетами.
/// </summary>
/// <remarks>
/// Звук приходит записью <c>{ samples, rate, channels, seconds }</c>, а не дескриптором: дальше
/// с ним работает готовый <c>dsp</c> (<c>dsp.fft(a.samples, fs: a.rate)</c>), и прятать отсчеты за
/// методами значило бы переписывать спектральный анализ второй раз. Каналы сводятся в один:
/// опыты над записью разговора спрашивают про громкость, паузы и спектр, а не про стереобазу.
/// </remarks>
[ScriptModule("audio", "Звук из файла: отсчеты для dsp, отрезки, громкость, запись WAV", Version = "0.1", Group = "сигналы")]
public sealed class AudioModule(IScriptAudioDecoder? decoder)
{
    /// <summary>Уровень тишины: логарифм нуля не число, а сравнивать громкость надо и с тишиной.</summary>
    private const double Silence = -120;

    [ScriptFn("load", "Читает звук: WAV сам, сжатые форматы через декодер хоста",
        Example = "let a = audio.load(\"call.wav\")", Reads = "wav,mp3,ogg,m4a,flac,opus,aac")]
    public async Task<ScriptRecord> Load(
        IScriptContext context,
        [ScriptParam("путь к звуковому файлу")] string path)
    {
        byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

        ScriptAudio audio = ScriptFileKinds.Extension(path) == "wav"
            ? Wav.Read(bytes, path)
            : await MediaCalls.Require(decoder, "audio.load", $"декодер для .{ScriptFileKinds.Extension(path)} не подключен, WAV читается всегда")
                .DecodeAsync(bytes, path, context.Cancellation).ConfigureAwait(false);

        return Record(context, audio);
    }

    [ScriptFn("save", "Пишет звук в WAV: моно, 16 бит", Example = "audio.save(a, \"part.wav\")", Writes = "wav")]
    public static async Task<string> Save(
        IScriptContext context,
        [ScriptParam("звук: запись audio.load")] ScriptRecord a,
        [ScriptParam("путь файла .wav")] string path)
    {
        (float[] samples, int rate) = Samples(a, "audio.save");

        byte[] bytes = Wav.Write(samples, rate);

        await context.Sandbox.WriteAsync(path, bytes, context.Cancellation).ConfigureAwait(false);
        context.FileSaved(new ScriptFileInfo(path, bytes.LongLength, DateTimeOffset.UtcNow));

        return path;
    }

    [ScriptFn("slice", "Отрезок звука по времени", Example = "audio.slice(a, start: 5s, end: 10s)")]
    public static ScriptRecord Slice(
        IScriptContext context,
        [ScriptParam("звук")] ScriptRecord a,
        [ScriptParam("начало")] TimeSpan start,
        [ScriptParam("конец; 0 — до конца записи")] TimeSpan end = default)
    {
        (float[] samples, int rate) = Samples(a, "audio.slice");
        int from = Math.Clamp((int)(start.TotalSeconds * rate), 0, samples.Length);
        int to = end <= TimeSpan.Zero ? samples.Length : Math.Clamp((int)(end.TotalSeconds * rate), from, samples.Length);

        return Record(context, new ScriptAudio([samples[from..to]], rate));
    }

    /// <summary>Громкость как уровень среднеквадратичного значения относительно полной шкалы.</summary>
    [ScriptFn("loudness", "Громкость в dBFS: 0 — полная шкала, тишина — -120", Example = "audio.loudness(a)")]
    public static double Loudness([ScriptParam("звук")] ScriptRecord a)
    {
        (float[] samples, _) = Samples(a, "audio.loudness");

        if (samples.Length == 0) return Silence;

        double power = 0;

        foreach (float sample in samples) power += sample * (double)sample;

        double rms = Math.Sqrt(power / samples.Length);

        return rms <= 0 ? Silence : Math.Max(Silence, 20 * Math.Log10(rms));
    }

    /// <summary>Запись звука: каналы сведены в один.</summary>
    internal static ScriptRecord Record(IScriptContext context, ScriptAudio audio)
    {
        int length = audio.Channels.Count == 0 ? 0 : audio.Channels.Min(channel => channel.Length);
        var mono = new double[length];

        foreach (float[] channel in audio.Channels)
        {
            for (int i = 0; i < length; i++) mono[i] += channel[i] / (double)audio.Channels.Count;
        }

        context.CountAllocation(length);

        return ScriptRecord.From(
        [
            new("samples", ScriptValue.Vec(new Vector(mono))),
            new("rate", ScriptValue.Num(audio.Rate)),
            new("channels", ScriptValue.Num(audio.Channels.Count)),
            new("seconds", ScriptValue.Num(audio.Rate > 0 ? (double)length / audio.Rate : 0)),
        ]);
    }

    /// <summary>Отсчеты и частота из записи звука.</summary>
    internal static (float[] Samples, int Rate) Samples(ScriptRecord a, string what)
    {
        if (!a.TryGet("samples", out ScriptValue samples) || samples.Type != ScriptType.Vec
            || !a.TryGet("rate", out ScriptValue rate) || rate.Type != ScriptType.Num || rate.RawNumber <= 0)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"{what}: нужен звук, запись с полями samples и rate",
                "звук дает audio.load");
        }

        Vector vector = samples.AsVector(what);
        var values = new float[vector.Count];

        for (int i = 0; i < values.Length; i++) values[i] = (float)vector[i];

        return (values, (int)rate.RawNumber);
    }
}

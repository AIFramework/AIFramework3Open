using AI.BackEnds.DSP.NWaves.Audio;
using AI.BackEnds.DSP.NWaves.Signals;
using AI.Script.Hosting;
using AI.Script.Media;
using AI.Script.Office;
using AI.Script.Vision;
using AI.Script.Semantics;
using SkiaSharp;

namespace AI.Script.UnitTests;

/// <summary>
/// Медиа: звук из WAV, кадры и сцены через декодер хоста, генерация и распознавание через службы
/// хоста, пометка «не подключено» без служб.
/// </summary>
public sealed class MediaTests
{
    private const int Rate = 8000;

    // --- без служб ---

    /// <summary>Без генератора функция видна в справке с пометкой и предупреждает при проверке.</summary>
    [Fact]
    public void WithoutService_MarkedInHelpAndCheck()
    {
        RunResult help = Script.RunWith(Script.FullHost(), "emit h = help(\"gen\")", new RunOptions());
        CheckResult check = Script.FullHost().Check("emit img = gen.image(\"cover\")");

        Assert.Contains("[не подключено]", (string)help.Emitted["h"]!, StringComparison.Ordinal);
        Assert.Contains(check.Diagnostics, d => d.Code == DiagnosticCodes.NotConnected);
        Assert.True(check.Success);
    }

    [Fact]
    public void WithoutService_CallFailsClearly()
    {
        Diagnostic error = Script.FailsWith("emit d = ocr.read(\"scan.png\")", host: Script.FullHost());

        Assert.Equal(DiagnosticCodes.UnknownFunction, error.Code);
        Assert.Contains("не подключено", error.Message, StringComparison.Ordinal);
    }

    // --- звук ---

    /// <summary>Спектр тестового тона находит его частоту с точностью до бина.</summary>
    [Fact]
    public void Tone_SpectrumFindsFrequency()
    {
        var store = new MemorySandbox();

        store.Put("tone.wav", Tone(440, seconds: 1));

        RunResult result = Run(store, """
            let a = audio.load("tone.wav")
            let s = dsp.fft(a.samples, fs: a.rate)
            emit peak = s.freq[vec.argmax(s.amp)]
            emit bin = s.freq[1] - s.freq[0]
            emit seconds = a.seconds
            """);

        Assert.InRange((double)result.Emitted["peak"]!, 440 - (double)result.Emitted["bin"]!, 440 + (double)result.Emitted["bin"]!);
        Assert.Equal(1.0, (double)result.Emitted["seconds"]!, 3);
    }

    /// <summary>Синус полной шкалы это -3 dBFS; отрезок сохраняется и читается обратно.</summary>
    [Fact]
    public void Slice_Loudness_SaveRoundTrip()
    {
        var store = new MemorySandbox();

        store.Put("tone.wav", Tone(440, seconds: 2));

        RunResult result = Run(store, """
            let part = audio.slice(audio.load("tone.wav"), start: 500ms, end: 1s)
            audio.save(part, "part.wav")
            emit seconds = audio.load("part.wav").seconds
            emit level = audio.loudness(part)
            """);

        Assert.Equal(0.5, (double)result.Emitted["seconds"]!, 2);
        Assert.InRange((double)result.Emitted["level"]!, -3.2, -2.8);
    }

    [Fact]
    public void CompressedAudio_WithoutDecoder_Fails()
    {
        var store = new MemorySandbox();

        store.Put("call.mp3", [1, 2, 3]);

        RunResult result = Script.RunWith(Script.FullHost(), "emit a = audio.load(\"call.mp3\")", new RunOptions { Sandbox = store });

        Assert.False(result.Success);
        Assert.Contains("WAV", Script.Report(result), StringComparison.Ordinal);
    }

    // --- видео ---

    /// <summary>Смены сцен найдены с точностью до кадра.</summary>
    [Fact]
    public void Scenes_FoundToTheFrame()
    {
        var store = new MemorySandbox();
        byte[] black = Png(SKColors.Black), white = Png(SKColors.White), gray = Png(new SKColor(128, 128, 128));
        byte[][] frames = [black, black, black, black, white, white, white, gray, gray, gray];

        store.Put("clip.mp4", [0]);

        RunResult result = Run(store, """
            let s = video.scenes("clip.mp4")
            emit count = len(s)
            emit second = s[1].start
            emit third = s[2].start
            emit frames = len(video.frames("clip.mp4"))
            """, new ScriptMediaServices(Video: new FakeVideo(frames)));

        Assert.Equal(3.0, result.Emitted["count"]);
        Assert.Equal(0.4, (double)result.Emitted["second"]!, 6);
        Assert.Equal(0.7, (double)result.Emitted["third"]!, 6);
        Assert.Equal(10.0, result.Emitted["frames"]);
    }

    // --- распознавание и генерация ---

    /// <summary>Скан становится документом с адресом области у каждой строки; вызов учтен как внешний.</summary>
    [Fact]
    public void Ocr_ReturnsDocumentWithRegions()
    {
        var store = new MemorySandbox();

        store.Put("act.png", Png(SKColors.White));

        RunResult result = Run(store, """
            let d = ocr.read("act.png")
            let b = doc.blocks(d)
            emit first = b[0].text
            emit where = b[0].address
            emit words = doc.count(d, term: "руб")
            """, new ScriptMediaServices(Ocr: new FakeOcr()));

        Assert.Equal("Акт № 12", result.Emitted["first"]);
        Assert.Equal("область 10,20,300,40", result.Emitted["where"]);
        Assert.Equal(1, result.Stats.ExternalCalls);
    }

    /// <summary>Расшифровка это документ с таймкодами.</summary>
    [Fact]
    public void Transcribe_ReturnsTimecodes()
    {
        var store = new MemorySandbox();

        store.Put("call.mp3", [1, 2, 3]);

        RunResult result = Run(store, """
            let b = doc.blocks(speech.transcribe("call.mp3"))
            emit replies = len(b)
            emit when = b[1].address
            """, new ScriptMediaServices(Transcriber: new FakeTranscriber()));

        Assert.Equal(2.0, result.Emitted["replies"]);
        Assert.Equal("00:00:04-00:01:10", result.Emitted["when"]);
    }

    /// <summary>Генерация идет под сетевой политикой: без разрешения сети отказ, а не вызов.</summary>
    [Fact]
    public void GenImage_RespectsNetworkPolicy()
    {
        var host = Host(new ScriptMediaServices(Images: new FakeImages()));
        const string source = "emit w = mat.rows(cv.channel(gen.image(\"cover\"), \"red\"))";

        RunResult denied = Script.RunWith(host, source, new RunOptions());
        RunResult allowed = Script.RunWith(host, source, new RunOptions { Network = NetworkPolicy.Allowed });

        Assert.False(denied.Success);
        Assert.Equal(DiagnosticCodes.NetworkDenied, denied.Error!.Code);
        Assert.True(allowed.Success, Script.Report(allowed));
        Assert.Equal(4.0, allowed.Emitted["w"]);
    }

    private static RunResult Run(MemorySandbox store, string source, ScriptMediaServices? services = null)
    {
        RunResult result = Script.RunWith(Host(services), source, new RunOptions { Sandbox = store, Network = NetworkPolicy.Allowed });

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    /// <summary>Хост с медиа на данных службах: полный хост уже держит медиа без служб.</summary>
    private static ScriptHost Host(ScriptMediaServices? services) =>
        Script.Host().UseVision().UseOffice().UseMedia(services);

    private static byte[] Tone(double frequency, double seconds)
    {
        var samples = new float[(int)(Rate * seconds)];

        for (int i = 0; i < samples.Length; i++) samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / Rate);

        using var stream = new MemoryStream();

        new WaveFile(new DiscreteSignal(Rate, samples), 16).SaveTo(stream);

        return stream.ToArray();
    }

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(4, 4);

        bitmap.Erase(color);

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    private sealed class FakeVideo(byte[][] frames) : IScriptVideoDecoder
    {
        public Task<ScriptVideoInfo> InfoAsync(byte[] data, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScriptVideoInfo(TimeSpan.FromSeconds(frames.Length / 10.0), 10, 4, 4));

        public Task<IReadOnlyList<ScriptVideoFrame>> FramesAsync(
            byte[] data, string fileName, TimeSpan every, int max, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScriptVideoFrame>>(
                [.. frames.Take(max).Select((frame, i) => new ScriptVideoFrame(TimeSpan.FromMilliseconds(100 * i), frame))]);

        public Task<ScriptAudio> AudioAsync(byte[] data, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScriptAudio([new float[Rate]], Rate));
    }

    private sealed class FakeOcr : IScriptTextRecognizer
    {
        public Task<ScriptRecognition> ReadAsync(byte[] image, string mediaType, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScriptRecognition(
                [new RecognizedText("Акт № 12", "10,20,300,40"), new RecognizedText("Итого 1 200 руб.", "10,80,300,40")],
                Tokens: 120));
    }

    private sealed class FakeTranscriber : IScriptTranscriber
    {
        public Task<ScriptTranscript> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScriptTranscript(
            [
                new TimedText(TimeSpan.Zero, TimeSpan.FromSeconds(4), "Добрый день."),
                new TimedText(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(70), "Звоню по договору поставки."),
            ]));
    }

    private sealed class FakeImages : IScriptImageGenerator
    {
        public Task<ScriptMedia> GenerateAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScriptMedia(Png(SKColors.Orange), "image/png", Cost: 0.01m));
    }

    // --- найденное при проверке ---

    /// <summary>Отсчет полной шкалы не переворачивается при записи, а файл виден хосту.</summary>
    [Fact]
    public void Save_ClampsFullScale_AndReportsFile()
    {
        var store = new MemorySandbox();

        RunResult result = Run(store, """
            let a = { samples: <1, 1.5, -1, 0.5>, rate: 8000 }
            audio.save(a, "peak.wav")
            emit first = audio.load("peak.wav").samples[0]
            emit second = audio.load("peak.wav").samples[1]
            """);

        Assert.True((double)result.Emitted["first"]! > 0.99);
        Assert.True((double)result.Emitted["second"]! > 0.99);
        Assert.Contains(result.Artifacts, artifact => artifact.Kind == "file" && artifact.Title == "peak.wav");
    }
}

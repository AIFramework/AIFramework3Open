using AI.Script.Hosting;
using AI.Script.Semantics;
using System.Text.Json;

namespace AI.Script.UnitTests;

/// <summary>Генерация сигналов, спектральный анализ, фильтрация и графики.</summary>
public sealed class SignalTests
{
    private const string Mixture = """
        options { seed: 1 }

        let fs = 4000
        let t = signal.time(0.5, fs: fs)
        let clean = signal.sine(t, freq: 200, amp: 1)
        let noisy = clean + signal.sine(t, freq: 1500, amp: 0.5)
        """;

    [Fact]
    public void Signal_TimeAndSine()
    {
        RunResult result = Script.RunOk("""
            let t = signal.time(1, fs: 100)
            let s = signal.sine(t, freq: 1)
            emit count = len(t)
            emit start = t[0]
            emit peak = core.round(stat.max(s), digits: 2)
            """);

        Assert.Equal(100.0, result.Emitted["count"]);
        Assert.Equal(0.0, result.Emitted["start"]);
        Assert.Equal(1.0, result.Emitted["peak"]);
    }

    [Fact]
    public void Signal_NoiseIsReproducible()
    {
        const string source = """
            options { seed: 7 }
            emit r = core.round(stat.mean(signal.noise(1000, sigma: 1)), digits: 6)
            """;

        Assert.Equal(Script.RunOk(source).Emitted["r"], Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Signal_NoiseFollowsRequestedSigma()
    {
        RunResult result = Script.RunOk("""
            options { seed: 5 }
            let n = signal.noise(20000, sigma: 2)
            emit sigma = math.approx(stat.std(n), 2, eps: 0.1)
            emit mean = math.approx(stat.mean(n), 0, eps: 0.1)
            """);

        Assert.Equal(true, result.Emitted["sigma"]);
        Assert.Equal(true, result.Emitted["mean"]);
    }

    [Fact]
    public void Signal_ChirpAndSquareAndImpulse()
    {
        RunResult result = Script.RunOk("""
            let t = signal.time(0.1, fs: 1000)
            emit chirp = len(signal.chirp(t, from: 10, to: 100))
            emit square = stat.max(signal.square(t, freq: 10))
            emit impulse = vec.sum(signal.impulse(64, at: 3, amp: 2))
            """);

        Assert.Equal(100.0, result.Emitted["chirp"]);
        Assert.Equal(1.0, result.Emitted["square"]);
        Assert.Equal(2.0, result.Emitted["impulse"]);
    }

    /// <summary>
    /// Спектр обязан показать частоту, которую в сигнал заложили: это самая базовая проверка
    /// того, что привязка к FFT не перепутала оси и нормировку.
    /// </summary>
    [Fact]
    public void Dsp_Fft_FindsPlantedFrequency()
    {
        RunResult result = Script.RunOk("""
            let fs = 4000
            let t = signal.time(1, fs: fs)
            let s = signal.sine(t, freq: 250, amp: 1)
            let spectrum = dsp.fft(s, fs: fs)
            emit peak = spectrum.freq[vec.argmax(spectrum.amp)]
            emit amplitude = math.approx(stat.max(spectrum.amp), 1, eps: 0.05)
            """);

        Assert.Equal(250.0, (double)result.Emitted["peak"]!, 1);
        Assert.Equal(true, result.Emitted["amplitude"]);
    }

    [Fact]
    public void Dsp_Welch_FindsPlantedFrequency()
    {
        RunResult result = Script.RunOk($$"""
            {{Mixture}}
            let spectrum = dsp.welch(noisy, fs: fs, window: 512)
            emit peak = spectrum.freq[vec.argmax(spectrum.power)]
            emit points = len(spectrum.freq) == len(spectrum.power)
            """);

        // Разрешение по частоте при окне 512 и fs = 4000 — около 8 Гц, поэтому пик попадает
        // в ближайший бин, а не точно в 200.
        Assert.InRange((double)result.Emitted["peak"]!, 192.0, 208.0);
        Assert.Equal(true, result.Emitted["points"]);
    }

    [Fact]
    public void Dsp_LowPass_RemovesHighFrequency()
    {
        // Отношение сигнал/шум после ФНЧ обязано вырасти: иначе фильтр не работает.
        RunResult result = Script.RunOk($$"""
            {{Mixture}}
            let filtered = noisy |> dsp.lowpass(cutoff: 600, fs: fs)
            emit before = stat.snr_db(noisy, clean)
            emit after = stat.snr_db(filtered, clean)
            emit improved = stat.snr_db(filtered, clean) > stat.snr_db(noisy, clean)
            """);

        Assert.Equal(true, result.Emitted["improved"]);
        Assert.True((double)result.Emitted["after"]! > (double)result.Emitted["before"]!);
    }

    [Fact]
    public void Dsp_CutoffAboveNyquist_IsRejected()
    {
        Diagnostic error = Script.FailsWith($"{Mixture}\nemit r = noisy |> dsp.lowpass(cutoff: 5000, fs: fs)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("Найквиста", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Dsp_BandBoundsAreValidated()
    {
        Diagnostic error = Script.FailsWith($"{Mixture}\nemit r = noisy |> dsp.bandpass(low: 900, high: 300, fs: fs)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    [Fact]
    public void Dsp_SmoothingAndEnvelope()
    {
        RunResult result = Script.RunOk($$"""
            {{Mixture}}
            emit smooth = len(noisy |> dsp.smooth(keep: 0.9)) == len(noisy)
            emit average = len(noisy |> dsp.moving_average(window: 16)) > 0
            emit envelope = len(noisy |> dsp.envelope()) > 0
            """);

        Assert.Equal(true, result.Emitted["smooth"]);
        Assert.Equal(true, result.Emitted["average"]);
        Assert.Equal(true, result.Emitted["envelope"]);
    }

    [Fact]
    public void Dsp_WindowKinds()
    {
        RunResult result = Script.RunOk("""
            emit hann = len(dsp.window(64, kind: "hann"))
            emit rect = stat.min(dsp.window(64, kind: "rect"))
            """);

        Assert.Equal(64.0, result.Emitted["hann"]);
        Assert.Equal(1.0, result.Emitted["rect"]);
    }

    [Fact]
    public void Dsp_UnknownWindow_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = dsp.window(64, kind: \"кайзер\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("hann", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Dsp_Spectrogram_IsMatrix()
    {
        RunResult result = Script.RunOk($$"""
            {{Mixture}}
            let s = dsp.spectrogram(noisy, window: 256)
            emit rows = mat.rows(s) > 1
            emit cols = mat.cols(s) > 1
            """);

        Assert.Equal(true, result.Emitted["rows"]);
        Assert.Equal(true, result.Emitted["cols"]);
    }

    // --- графики ---

    [Fact]
    public void Plot_ProducesPlotlyArtifact()
    {
        RunResult result = Script.RunOk("""
            let t = signal.time(0.01, fs: 1000)
            show plot.line(signal.sine(t, freq: 100), x: t, title: "Сигнал")
            """);

        Assert.Single(result.Artifacts);
        Assert.Equal("plot", result.Artifacts[0].Kind);
        Assert.Equal("Сигнал", result.Artifacts[0].Title);

        // JSON разбирается, а не ищется подстрокой: сериализатор экранирует кириллицу
        // в \uXXXX, и поиск «Сигнал» в тексте провалился бы на исправном описании.
        using JsonDocument document = JsonDocument.Parse(Assert.IsType<string>(result.Artifacts[0].Value));

        Assert.Equal("Сигнал", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("traces").GetArrayLength());
    }

    [Fact]
    public void Plot_ScatterByLabels_MakesOneSeriesPerLabel()
    {
        RunResult result = Script.RunOk("""
            let x = <0, 1, 5, 6>
            let y = <0, 1, 5, 6>
            show plot.scatter_by(x: x, y: y, labels: <0, 0, 1, 1>)
            """);

        string json = Assert.IsType<string>(result.Artifacts[0].Value);

        Assert.Contains("\"name\":\"0\"", json.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("\"name\":\"1\"", json.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Plot_HeatmapFromConfusionMatrix()
    {
        RunResult result = Script.RunOk("""
            let y = <0, 0, 1, 1>
            let pred = <0, 1, 1, 1>
            show plot.heatmap(stat.confusion(y, pred), title: "Матрица ошибок")
            """);

        Assert.Equal("plot", result.Artifacts[0].Kind);
        Assert.Contains("heatmap", (string)result.Artifacts[0].Value!, StringComparison.Ordinal);
    }

    [Fact]
    public void Plot_Grid_CombinesFigures()
    {
        RunResult result = Script.RunOk("""
            let t = signal.time(0.01, fs: 1000)
            show plot.grid([
                plot.line(signal.sine(t, freq: 100), x: t, title: "Первый"),
                plot.line(signal.sine(t, freq: 200), x: t, title: "Второй")
            ], title: "Сравнение")
            """);

        using JsonDocument document = JsonDocument.Parse(Assert.IsType<string>(result.Artifacts[0].Value));

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal("Первый", document.RootElement[0].GetProperty("title").GetString());
        Assert.Equal("Второй", document.RootElement[1].GetProperty("title").GetString());
    }

    [Fact]
    public void Plot_Grid_RejectsNonFigures()
    {
        Diagnostic error = Script.FailsWith("emit r = plot.grid([1, 2])");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("plot.line", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Plot_Spectrum_ReadsWelchRecord()
    {
        RunResult result = Script.RunOk($$"""
            {{Mixture}}
            show plot.spectrum(dsp.welch(noisy, fs: fs, window: 256))
            """);

        Assert.Equal("plot", result.Artifacts[0].Kind);
    }

    [Fact]
    public void Plot_Spectrum_ReportsMissingField()
    {
        Diagnostic error = Script.FailsWith("emit r = plot.spectrum({ a: <1> })");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
    }

    [Fact]
    public void Show_TableProducesTableArtifact()
    {
        RunResult result = Script.RunOk("show table.of({ a: <1, 2> })");

        Assert.Equal("table", result.Artifacts[0].Kind);
    }
}

using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.DSP.Analyse;
using AI.DSP.DSPCore;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>dsp</c>: спектральный анализ и фильтрация.
/// </summary>
/// <remarks>
/// Привязка идёт к статическим функциям <c>AI.DSP</c>, а не к объектам-фильтрам: в прототипе
/// фильтр применяют один раз к целому сигналу, и заводить ради этого дескриптор с состоянием
/// значило бы усложнить запись, ничего не дав.
/// </remarks>
[ScriptModule("dsp", "Спектральный анализ и фильтрация сигналов", Version = "0.1")]
public static class DspModule
{
    [ScriptFn("fft", "Спектр сигнала: амплитуды и частоты", Example = "let s = dsp.fft(signal, fs: 8000)")]
    public static ScriptRecord Fft(
        IScriptContext context,
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("частота дискретизации")] double fs = 1)
    {
        RequireNotEmpty(signal, "dsp.fft");
        context.CountAllocation(signal.Count * 2L);

        ComplexVector spectrum = FFT.CalcFFT(signal);
        int half = spectrum.Count / 2;

        var amplitude = new Vector(half);
        var frequency = new Vector(half);

        for (int i = 0; i < half; i++)
        {
            amplitude[i] = spectrum[i].Magnitude * 2.0 / spectrum.Count;
            frequency[i] = i * fs / spectrum.Count;
        }

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("freq", ScriptValue.Vec(frequency)),
            new KeyValuePair<string, ScriptValue>("amp", ScriptValue.Vec(amplitude)),
        ]);
    }

    /// <summary>
    /// Оценка спектральной плотности мощности методом Уэлча.
    /// </summary>
    /// <remarks>
    /// Возвращает половину спектра: вторая половина у вещественного сигнала зеркальна первой,
    /// и рисовать её значит удваивать график без единого нового факта.
    /// </remarks>
    [ScriptFn("welch", "Спектральная плотность мощности методом Уэлча",
        Example = "dsp.welch(signal, fs: 8000, window: 1024)")]
    public static ScriptRecord Welch(
        IScriptContext context,
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("частота дискретизации")] double fs = 1,
        [ScriptParam("длина окна")] int window = 256,
        [ScriptParam("доля перекрытия от 0 до 1")] double overlap = 0.5)
    {
        RequireNotEmpty(signal, "dsp.welch");

        if (window < 2 || window > signal.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"dsp.welch: длина окна {window} при сигнале длиной {signal.Count}",
                "окно должно быть от 2 до длины сигнала");
        }

        if (overlap is < 0 or >= 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "dsp.welch: перекрытие должно лежать в [0, 1)");

        context.CountAllocation(signal.Count);

        Vector psd = AI.DSP.Analyse.Welch.WelchRun(signal, window, overlap, WindowForFFT.HannWindow(window));
        var data = new WelchData(psd, fs);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("freq", ScriptValue.Vec(data.HalfFreq)),
            new KeyValuePair<string, ScriptValue>("power", ScriptValue.Vec(data.HalfPSD)),
        ]);
    }

    [ScriptFn("spectrogram", "Спектрограмма: строки — окна времени, столбцы — частоты",
        Example = "dsp.spectrogram(signal, window: 512)")]
    public static Matrix Spectrogram(
        IScriptContext context,
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("длина окна")] int window = 1024)
    {
        RequireNotEmpty(signal, "dsp.spectrogram");
        context.CountAllocation(signal.Count);

        return FFT.TimeFrTransformHalf(signal, window);
    }

    [ScriptFn("lowpass", "Фильтр нижних частот", Example = "signal |> dsp.lowpass(cutoff: 800, fs: 8000)")]
    public static Vector LowPass(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("частота среза")] double cutoff,
        [ScriptParam("частота дискретизации")] int fs)
    {
        RequireCutoff(cutoff, fs, "dsp.lowpass");
        return Filters.FilterLow(signal, cutoff, fs);
    }

    [ScriptFn("highpass", "Фильтр верхних частот", Example = "signal |> dsp.highpass(cutoff: 50, fs: 8000)")]
    public static Vector HighPass(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("частота среза")] double cutoff,
        [ScriptParam("частота дискретизации")] int fs)
    {
        RequireCutoff(cutoff, fs, "dsp.highpass");
        return Filters.FilterHigh(signal, cutoff, fs);
    }

    [ScriptFn("bandpass", "Полосовой фильтр", Example = "signal |> dsp.bandpass(low: 300, high: 3400, fs: 8000)")]
    public static Vector BandPass(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("нижняя частота полосы")] double low,
        [ScriptParam("верхняя частота полосы")] double high,
        [ScriptParam("частота дискретизации")] int fs)
    {
        RequireBand(low, high, fs, "dsp.bandpass");
        return Filters.FilterBand(signal, low, high, fs);
    }

    [ScriptFn("bandstop", "Режекторный фильтр", Example = "signal |> dsp.bandstop(low: 45, high: 55, fs: 8000)")]
    public static Vector BandStop(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("нижняя частота полосы")] double low,
        [ScriptParam("верхняя частота полосы")] double high,
        [ScriptParam("частота дискретизации")] int fs)
    {
        RequireBand(low, high, fs, "dsp.bandstop");
        return Filters.FilterRezector(signal, low, high, fs);
    }

    [ScriptFn("butterworth", "Фильтр Баттерворта нижних частот", Example = "signal |> dsp.butterworth(cutoff: 800, fs: 8000, order: 3)")]
    public static Vector Butterworth(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("частота среза")] double cutoff,
        [ScriptParam("частота дискретизации")] int fs,
        [ScriptParam("порядок фильтра")] int order = 3)
    {
        RequireCutoff(cutoff, fs, "dsp.butterworth");
        return Filters.FilterLowButterworthAFH(signal, cutoff, fs, order);
    }

    [ScriptFn("moving_average", "Скользящее среднее сигнала по окну", Example = "signal |> dsp.moving_average(window: 20)")]
    public static Vector MovingAverage(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("длина окна")] int window = 10)
        => Filters.MAv(signal, window);

    [ScriptFn("smooth", "Экспоненциальное сглаживание", Example = "signal |> dsp.smooth(keep: 0.9)")]
    public static Vector Smooth(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("доля прежнего значения от 0 до 1")] double keep = 0.99)
        => Filters.ExpAv(signal, keep);

    [ScriptFn("envelope", "Огибающая сигнала", Example = "signal |> dsp.envelope()")]
    public static Vector Envelope(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("прореживание")] int decimation = 1)
        => Filters.GetEnvelope(signal, decimation);

    [ScriptFn("convolve", "Свёртка сигнала с ядром", Example = "dsp.convolve(signal, kernel)")]
    public static Vector Convolve(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("ядро")] Vector kernel)
        => FastConv.FastConvolution(signal, kernel);

    [ScriptFn("correlate", "Взаимная корреляция двух сигналов", Example = "dsp.correlate(a, b)")]
    public static Vector Correlate(
        [ScriptParam("первый сигнал")] Vector a,
        [ScriptParam("второй сигнал")] Vector b)
        => FastConv.FastCorrelation(a, b);

    [ScriptFn("window", "Оконная функция: hann, hamming, blackman, rect", Example = "dsp.window(1024, kind: \"hann\")")]
    public static Vector Window(
        [ScriptParam("длина окна")] int size,
        [ScriptParam("вид окна")] string kind = "hann")
        => kind switch
        {
            "hann" => WindowForFFT.HannWindow(size),
            "hamming" => WindowForFFT.HammingWindow(size),
            "blackman" => WindowForFFT.BlackmanWindow(size),
            "rect" => WindowForFFT.RectWindow(size),
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"dsp.window: неизвестное окно '{kind}'",
                "известны: hann, hamming, blackman, rect"),
        };

    private static void RequireNotEmpty(Vector signal, string what)
    {
        if (signal.Count > 0) return;

        throw new ScriptError(DiagnosticCodes.SizeMismatch, $"{what}: сигнал пуст");
    }

    /// <summary>
    /// Проверяет частоту среза против частоты Найквиста.
    /// </summary>
    /// <remarks>
    /// Срез выше половины частоты дискретизации физически бессмыслен, но фильтр от этого не
    /// падает — он молча возвращает мусор. Проверка здесь дешевле, чем разбирательство с
    /// «почему спектр не такой».
    /// </remarks>
    private static void RequireCutoff(double cutoff, int fs, string what)
    {
        if (fs <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: частота дискретизации должна быть больше нуля");

        if (cutoff > 0 && cutoff < fs / 2.0) return;

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: частота среза {ScriptFormatter.Number(cutoff)} при частоте дискретизации {fs}",
            $"срез должен лежать между 0 и частотой Найквиста ({fs / 2.0})");
    }

    private static void RequireBand(double low, double high, int fs, string what)
    {
        RequireCutoff(low, fs, what);
        RequireCutoff(high, fs, what);

        if (low < high) return;

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: нижняя граница {ScriptFormatter.Number(low)} не меньше верхней {ScriptFormatter.Number(high)}");
    }
}

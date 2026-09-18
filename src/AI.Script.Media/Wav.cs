using AI.BackEnds.DSP.NWaves.Audio;
using AI.BackEnds.DSP.NWaves.Signals;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Media;

/// <summary>
/// WAV своим кодом: формат открытый и простой, и декодер хоста для него не нужен.
/// </summary>
/// <remarks>Разбор и запись берутся из <c>AI.DSP</c> (NWaves), чтобы не держать второй разборщик RIFF.</remarks>
internal static class Wav
{
    public static ScriptAudio Read(byte[] bytes, string source)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var file = new WaveFile(stream);

            return new ScriptAudio([.. file.Signals.Select(signal => signal.Samples)], file.WaveFmt.SamplingRate);
        }
        catch (Exception error) when (error is not ScriptError)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"{source}: не WAV либо формат не поддержан ({error.Message})",
                "читаются WAV с отсчетами 8, 16, 24 и 32 бита");
        }
    }

    /// <summary>Моно WAV, 16 бит.</summary>
    /// <remarks>
    /// Отсчеты ограничиваются полной шкалой: запись переводит их в целые без проверки, и громкий
    /// отсчет 1.0 превращался в -1, то есть щелчок на пике.
    /// </remarks>
    public static byte[] Write(float[] samples, int rate)
    {
        using var stream = new MemoryStream();
        float[] clamped = Array.ConvertAll(samples, sample => Math.Clamp(sample, -1f, 32767f / 32768f));

        new WaveFile(new DiscreteSignal(rate, clamped), 16).SaveTo(stream);

        return stream.ToArray();
    }
}

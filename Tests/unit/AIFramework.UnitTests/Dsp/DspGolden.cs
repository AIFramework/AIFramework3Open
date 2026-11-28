using System.Text.Json;
using AI.DataStructs.Algebraic;

namespace AIFramework.UnitTests.Dsp;

/// <summary>
/// Эталонные значения, выгруженные скриптом Tools/DspGolden/export_dsp_golden.py
/// (код экспериментов диссертации, scipy). Python при тестах не нужен.
/// </summary>
internal static class DspGolden
{
    private static readonly Lazy<JsonElement> MultirateData = new(() => Load("multirate.json"));
    private static readonly Lazy<JsonElement> IirData = new(() => Load("iir.json"));

    public static JsonElement Multirate => MultirateData.Value;

    public static JsonElement Iir => IirData.Value;

    public static double[] Doubles(this JsonElement e) => [.. e.EnumerateArray().Select(v => v.GetDouble())];

    public static long[] Longs(this JsonElement e) => [.. e.EnumerateArray().Select(v => v.GetInt64())];

    public static Vector Vec(this JsonElement e) => new(e.Doubles());

    /// <summary>
    /// Полигармонический сигнал как test_signal в resampling_compare.py (тот же порядок операций).
    /// </summary>
    public static double[] TestSignal(int sampleRate, double duration, double[] tones, double[] phases, double amplitude)
    {
        int n = (int)(sampleRate * duration);
        var s = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)sampleRate;
            double sum = 0;
            for (int k = 0; k < tones.Length; k++)
            {
                sum += amplitude * Math.Cos((2 * Math.PI * tones[k] * t) + phases[k]);
            }

            s[i] = sum;
        }

        return s;
    }

    /// <summary>Корень репозитория (каталог с AIFramework.Full.sln).</summary>
    public static string RepositoryRoot
    {
        get
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "AIFramework.Full.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            return dir ?? throw new DirectoryNotFoundException("Не найден корень репозитория.");
        }
    }

    private static JsonElement Load(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Dsp", "Golden", name);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }
}

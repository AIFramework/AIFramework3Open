using System.Text.Json;
using AI.DataStructs.Algebraic;
using AI.DSP.Multirate;
using Xunit;

namespace AIFramework.UnitTests.Dsp;

/// <summary>
/// Сценарии главы 3 на сигнале длительностью 1 с: ОСШ, шум квантования и затраты против
/// experiments/results/compare.json (таблицы 3.2–3.4) и motion.json.
/// </summary>
public sealed class ChapterThreeScenarioTests
{
    private static JsonElement G => DspGolden.Multirate;

    [Theory]
    [InlineData(384000, 48000)]
    [InlineData(384000, 44100)]
    [InlineData(44100, 48000)]
    [InlineData(48000, 44100)]
    public void Scenario_MatchesDissertationNumbers(int fs, int ft)
    {
        JsonElement row = Row(fs, ft);
        double[] tones = G.GetProperty("tones_wide").Doubles();
        double[] phases = G.GetProperty("phases_wide").Doubles();
        var x = new Vector(DspGolden.TestSignal(fs, 1.0, tones, phases, 0.18));

        Vector s1 = ResamplerDesign.Standard(fs, ft, 20000).ProcessBlock(x);
        MultirateChain syn = ResamplerDesign.CicCorrectorFarrow(fs, ft, 20000);
        Vector s2 = syn.ProcessBlock(x);
        MultirateChain synQ = ResamplerDesign.CicCorrectorFarrow(fs, ft, 20000, arithmetic: Arithmetic.Q16, options: Q16Options.Author);
        Vector s2q = synQ.ProcessBlock(x);

        // Таблица 3.3: ОСШ синтезированной схемы относительно стандартной
        double snr = ResamplingMetrics.SnrFromCorrelation(ResamplingMetrics.MaxNormalizedCrossCorrelation(s1, s2));
        Assert.InRange(snr, row.GetProperty("syn_SNR").GetDouble() - 0.05, row.GetProperty("syn_SNR").GetDouble() + 0.05);

        // Контроль эталона: стандартная схема относительно идеального сигнала
        var ideal = new Vector(DspGolden.TestSignal(ft, s1.Count / (double)ft, tones, phases, 0.18).Take(s1.Count));
        double snrStd = ResamplingMetrics.SnrFromCorrelation(ResamplingMetrics.MaxNormalizedCrossCorrelation(ideal, s1));
        Assert.InRange(snrStd, row.GetProperty("std_SNRid").GetDouble() - 0.5, row.GetProperty("std_SNRid").GetDouble() + 0.5);

        // Таблица 3.4: шум квантования Q16 (умножение автора)
        double dn = ResamplingMetrics.QuantizationNoiseVariance(s2q, s2);
        double expectedDn = row.GetProperty("syn_DN").GetDouble();
        Assert.InRange(dn, expectedDn * 0.97, expectedDn * 1.03);

        // Таблица 3.2: в double ИГФ считается скользящими суммами, как в экспериментах, и затраты совпадают
        Assert.Equal(row.GetProperty("syn_mults").GetDouble(), syn.OperationsPerOutput.Multiplications * ft, 0);
        Assert.Equal(row.GetProperty("syn_ops").GetDouble(), syn.OperationsPerOutput.Total * ft, 0);

        // В Q16 ИГФ по Хогенауэру: сложений меньше на K·(F_выс − F_низ)
        int k = fs >= ft ? fs / ft : (ft + fs - 1) / fs;
        double high = fs >= ft ? fs : (double)fs * k;
        double low = fs >= ft ? fs / (double)k : fs;
        double saving = k > 1 ? 4 * (high - low) : 0;
        double adds = syn.OperationsPerOutput.Additions * ft;
        double addsQ16 = synQ.OperationsPerOutput.Additions * ft;
        Assert.Equal(adds - saving, addsQ16, 0);
    }

    [Theory]
    [InlineData(384000, 48000)]
    [InlineData(48000, 44100)]
    public void Standard_Q16Noise_MatchesTable34(int fs, int ft)
    {
        JsonElement row = Row(fs, ft);
        var x = new Vector(DspGolden.TestSignal(fs, 1.0, G.GetProperty("tones_wide").Doubles(), G.GetProperty("phases_wide").Doubles(), 0.18));
        Vector d = ResamplerDesign.Standard(fs, ft, 20000).ProcessBlock(x);
        PolyphaseResampler q = ResamplerDesign.Standard(fs, ft, 20000, arithmetic: Arithmetic.Q16, options: Q16Options.Author);
        double dn = ResamplingMetrics.QuantizationNoiseVariance(q.ProcessBlock(x), d);
        double expected = row.GetProperty("pph_DN").GetDouble();
        Assert.InRange(dn, expected * 0.99, expected * 1.01);
        Assert.Equal(row.GetProperty("pph_mults").GetDouble(), q.OperationsPerOutput.Multiplications * ft, 0);
    }

    [Fact]
    public void MovingSource_PolynomialTrajectory_MatchesMotionExperiment()
    {
        JsonElement m = G.GetProperty("motion");
        JsonElement p = m.GetProperty("params");
        double fs = p.GetProperty("FS").GetDouble();
        double c = p.GetProperty("C").GetDouble();
        double[] r = [p.GetProperty("R0").GetDouble(), p.GetProperty("V").GetDouble(), p.GetProperty("ACC").GetDouble() / 2, p.GetProperty("JERK").GetDouble() / 6];
        double[] tones = m.GetProperty("tones").Doubles();
        double[] phases = m.GetProperty("phases").Doubles();

        // Принятый сигнал r(mT) = s(e(mT)), e + R(e)/c = t — метод Ньютона, как в motion_compensation.py
        int total = (int)((2.0 + 0.1) * fs);
        var received = new double[total];
        for (int i = 0; i < total; i++)
        {
            double t = i / fs;
            double e = t - (r[0] / c);
            for (int it = 0; it < 30; it++)
            {
                double range = r[0] + (r[1] * e) + (r[2] * e * e) + (r[3] * e * e * e);
                double speed = r[1] + (2 * r[2] * e) + (3 * r[3] * e * e);
                e -= (e + (range / c) - t) / (1 + (speed / c));
            }

            received[i] = Source(e, tones, phases);
        }

        // motion_compensation.py импортирует methodology_experiments, и умножение там — с округлением к ближайшему
        var options = new Q16Options(Q16Rounding.TowardZero, Q16Rounding.Nearest, WideAccumulator: false, Saturate: false);
        var block = new MovingSourceResampler(new Vector(r), c, fs, 32, 256, arithmetic: Arithmetic.Q16, options: options);
        Vector y = block.Process(new Vector(received));

        int n = (int)(2.0 * fs) - 8;
        var ideal = new Vector(Enumerable.Range(1, n - 1).Select(k => Source(k / fs, tones, phases)));
        var restored = new Vector(y.Skip(1).Take(n - 1));
        double snr = Math.Min(ResamplingMetrics.SnrFromCorrelation(ResamplingMetrics.MaxNormalizedCrossCorrelation(ideal, restored, 4)), 150);

        double expected = m.GetProperty("C_recursive").EnumerateArray()
            .First(row => row.GetProperty("F").GetInt32() == 32 && row.GetProperty("B").ValueKind == JsonValueKind.Number && row.GetProperty("B").GetInt32() == 256)
            .GetProperty("SNR").GetDouble();
        Assert.InRange(snr, expected - 0.02, expected + 0.02);
        Assert.Equal(522.4, block.Position(n) - n - (r[0] * fs / c), 1);
    }

    private static double Source(double t, double[] tones, double[] phases)
    {
        double s = 0;
        for (int k = 0; k < tones.Length; k++)
        {
            s += 0.18 * Math.Cos((2 * Math.PI * tones[k] * t) + phases[k]);
        }

        return s;
    }

    private static JsonElement Row(int fs, int ft) => G.GetProperty("compare_main").EnumerateArray()
        .First(r => r.GetProperty("Fs").GetInt32() == fs && r.GetProperty("Ft").GetInt32() == ft);
}

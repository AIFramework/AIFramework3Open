using System.Text.Json;
using AI.DataStructs.Algebraic;
using AI.DSP.Multirate;
using Xunit;

namespace AIFramework.UnitTests.Dsp;

/// <summary>
/// Блоки AI.DSP.Multirate против эталонов: код экспериментов главы 3, потоковая модель resampler_mpy, scipy.
/// </summary>
public sealed class MultirateBlockTests
{
    private static JsonElement G => DspGolden.Multirate;

    // ---------------------------------------------------------------- Q16

    [Fact]
    public void Q16_Multiply_MatchesAuthorFloorAndMulRound()
    {
        foreach (JsonElement row in G.GetProperty("q16_mul").EnumerateArray())
        {
            var a = Q16.FromDouble(row.GetProperty("a").GetDouble(), Q16Rounding.TowardZero);
            var b = Q16.FromDouble(row.GetProperty("b").GetDouble(), Q16Rounding.TowardZero);
            Assert.Equal(row.GetProperty("ia").GetInt64(), a.Raw);
            Assert.Equal(row.GetProperty("floor").GetInt64(), Q16.Multiply(a, b, Q16Rounding.Floor).Raw);
            Assert.Equal(row.GetProperty("nearest").GetInt64(), Q16.Multiply(a, b, Q16Rounding.Nearest).Raw);
        }
    }

    [Fact]
    public void Q16_Multiply_DoesNotOverflowLikeInt32AuthorCode()
    {
        // FixedPoint.Mul автора на int: f1·f2 = 58982² > 2^31, и 0,9 × 0,9 даёт −0,19
        var x = Q16.FromDouble(0.9, Q16Rounding.TowardZero);
        int f = x.Raw & 0xFFFF;
        int int32Fraction = unchecked(f * f) >> 16;
        Assert.True(int32Fraction < 0);

        Assert.Equal(0.81, Q16.Multiply(x, x, Q16Rounding.Floor).ToDouble(), 4);
        Assert.Equal(0.81, (x * x).ToDouble(), 4);
    }

    [Fact]
    public void Q16_Saturation_IsOptional()
    {
        var big = Q16.FromDouble(30000);
        Assert.Equal(int.MaxValue, Q16.Add(big, big, saturate: true).Raw);
        Assert.True(Q16.Add(big, big).Raw < 0);
        Assert.Equal(int.MaxValue, Q16.Multiply(big, big, saturate: true).Raw);
        Assert.Throws<OverflowException>(() => Q16.FromDouble(1e6, saturate: false));
        Assert.Equal(-1, Q16.FromDouble(-1e-9, Q16Rounding.Floor).Raw);
        Assert.Equal(0, Q16.FromDouble(-1e-9, Q16Rounding.TowardZero).Raw);
    }

    // ---------------------------------------------------------------- Кайзер, корректор, Лагранж

    [Fact]
    public void Kaiser_BetaLengthWindow_MatchExperiments()
    {
        foreach (JsonElement r in G.GetProperty("kaiser_beta").EnumerateArray())
        {
            Assert.Equal(r.GetProperty("beta").GetDouble(), KaiserFir.Beta(r.GetProperty("a").GetDouble()), 12);
        }

        foreach (JsonElement r in G.GetProperty("kaiser_order").EnumerateArray())
        {
            Assert.Equal(r.GetProperty("n").GetInt32(), KaiserFir.Length(r.GetProperty("a").GetDouble(), r.GetProperty("dw").GetDouble()));
        }

        JsonElement w = G.GetProperty("kaiser_window");
        AssertClose(w.GetProperty("w").Doubles(), KaiserFir.Window(31, 5.0).ToArray(), 1e-14);
        Assert.Equal(1.0, KaiserFir.BesselI0(0));
        Assert.Equal(27.239871823604442, KaiserFir.BesselI0(5.0), 12);   // таблицы Абрамовица и Стиган
    }

    [Fact]
    public void Standard_Filter_MatchesExperiments()
    {
        JsonElement g = G.GetProperty("std_filter_384_48");
        PolyphaseResampler std = ResamplerDesign.Standard(384000, 48000, 20000);
        Assert.Equal(1, std.Interpolation);
        Assert.Equal(8, std.Decimation);
        Assert.Equal(483, std.Length);

        JsonElement g2 = G.GetProperty("std_filter_48_441");
        int length = KaiserFir.Length(80, (22050 - 20000) / (147.0 * 48000));
        Assert.Equal(g2.GetProperty("L").GetInt32(), length);
        Vector h = KaiserFir.LowPass(length, (20000 + 22050) / 2.0 / (147 * 48000 / 2.0), KaiserFir.Beta(80), 147);
        AssertClose(g2.GetProperty("h_head").Doubles(), h.Take(64).ToArray(), 1e-15);
        AssertClose(g2.GetProperty("h_center").Doubles(), h.Skip((length / 2) - 32).Take(65).ToArray(), 1e-12);

        Vector h1 = KaiserFir.LowPass(483, (20000 + 24000) / 2.0 / 192000, KaiserFir.Beta(80));
        AssertClose(g.GetProperty("h").Doubles(), h1.ToArray(), 1e-15);
    }

    [Fact]
    public void CorrectionFilter_MatchesFormulas25To28()
    {
        foreach (JsonElement r in G.GetProperty("correction").EnumerateArray())
        {
            Vector h = CicDesign.CorrectionFilter(r.GetProperty("N").GetInt32(), r.GetProperty("K").GetInt32(), r.GetProperty("R").GetInt32(),
                r.GetProperty("O").GetInt32(), r.GetProperty("band").GetDouble());
            AssertClose(r.GetProperty("h").Doubles(), h.ToArray(), 1e-11);
        }
    }

    [Fact]
    public void Lagrange_MatrixAndCost_MatchTable21()
    {
        foreach (int p in new[] { 1, 2, 3, 5 })
        {
            JsonElement r = G.GetProperty("lagrange").GetProperty(p.ToString());
            var kernel = new LagrangeKernel(p);
            Assert.Equal(r.GetProperty("nodes")[0].GetInt32(), kernel.FirstNode);
            Matrix c = kernel.Coefficients;
            int k = 0;
            foreach (JsonElement row in r.GetProperty("C").EnumerateArray())
            {
                double[] expected = row.Doubles();
                for (int j = 0; j < expected.Length; j++)
                {
                    Assert.Equal(expected[j], c[k, j], 12);
                }

                k++;
            }
        }

        foreach (int p in new[] { 1, 3, 5 })
        {
            long[] cost = G.GetProperty("farrow_cost").GetProperty(p.ToString()).Longs();
            OperationCount ops = new LagrangeKernel(p).Operations(Arithmetic.Double, null);
            Assert.Equal(cost[0], ops.Multiplications);
            Assert.Equal(cost[1], ops.Additions);
        }
    }

    // ---------------------------------------------------------------- полифазный фильтр

    [Theory]
    [InlineData(1, 1, 31)]
    [InlineData(1, 4, 16)]
    [InlineData(3, 1, 40)]
    [InlineData(3, 7, 61)]
    [InlineData(5, 3, 23)]
    [InlineData(147, 160, 600)]
    public void Polyphase_EqualsZeroInsertionFilteringDecimation(int p, int q, int length)
    {
        var rnd = new Random(1);
        double[] x = [.. Enumerable.Range(0, 500).Select(_ => rnd.NextDouble() - 0.5)];
        double[] h = [.. Enumerable.Range(0, length).Select(_ => rnd.NextDouble() - 0.5)];
        int d = (length - 1) / 2;

        // Прямая схема: вставка нулей (2.2), свёртка, прореживание (2.1) со смещением D
        var up = new double[x.Length * p];
        for (int i = 0; i < x.Length; i++)
        {
            up[i * p] = x[i];
        }

        int outputs = x.Length * p / q;
        var expected = new double[outputs];
        for (int m = 0; m < outputs; m++)
        {
            long t = ((long)m * q) + d;
            double acc = 0;
            for (int k = 0; k < length; k++)
            {
                long i = t - k;
                if (i >= 0 && i < up.Length)
                {
                    acc += h[k] * up[i];
                }
            }

            expected[m] = acc;
        }

        var block = new PolyphaseResampler(p, q, new Vector(h));
        AssertClose(expected, block.ProcessBlock(new Vector(x)).ToArray(), 1e-12);

        // Потоком по фрагментам разной длины — то же самое
        block.Reset();
        var streamed = new List<double>();
        int pos = 0;
        foreach (int chunk in new[] { 1, 7, 33, 100, 359 })
        {
            streamed.AddRange(block.Process(new Vector(x.Skip(pos).Take(chunk))));
            pos += chunk;
        }

        streamed.AddRange(block.Flush());
        AssertClose(expected, [.. streamed], 1e-12);
    }

    [Fact]
    public void Polyphase_AuthorDecimationCase_HasNoBranchOrderError()
    {
        // Таблица 3.7: децимация в 4 раза, 16 отводов, меандр 15 Гц при 240 Гц
        double[] n16 = [.. Enumerable.Range(0, 16).Select(i => i - 7.5)];
        double[] hamming = [.. Enumerable.Range(0, 16).Select(i => 0.54 - (0.46 * Math.Cos(2 * Math.PI * i / 15)))];
        double[] h = [.. n16.Select((v, i) => 0.125 * Sinc(0.125 * v) * hamming[i])];
        double sum = h.Sum();
        h = [.. h.Select(v => v / sum)];
        double[] x = [.. Enumerable.Range(0, 240).Select(i => Math.Sin(2 * Math.PI * 15 * i / 240) >= 0 ? 1.0 : -1.0)];

        var direct = new double[60];
        for (int m = 0; m < 60; m++)
        {
            for (int k = 0; k < 16; k++)
            {
                int i = (4 * m) - k;
                if (i >= 0)
                {
                    direct[m] += h[k] * x[i];
                }
            }
        }

        var block = new PolyphaseResampler(1, 4, new Vector(h), offset: 0);
        Vector y = block.ProcessBlock(new Vector(x));
        Assert.Equal(60, y.Count);
        AssertClose(direct, y.ToArray(), 1e-15);
        Assert.True(G.GetProperty("author_polyphase").GetProperty("author_max_abs_diff").GetDouble() > 0.3);
    }

    // ---------------------------------------------------------------- потоковая модель resampler_mpy (Q16, побитно)

    [Fact]
    public void Blocks_MatchStreamingIntegerModelBitExact()
    {
        JsonElement m = G.GetProperty("mpy");
        Vector x = Raw(m.GetProperty("x").Longs());
        Vector h = Raw(m.GetProperty("h").Longs());
        Q16Options o = Q16Options.Recommended;

        AssertRaw(m.GetProperty("cic_dec_8_4"), new CicDecimator(8, 4, offset: 7, arithmetic: Arithmetic.Q16, options: o).Process(x));
        AssertRaw(m.GetProperty("cic_dec_6_3"), new CicDecimator(6, 3, offset: 5, arithmetic: Arithmetic.Q16, options: o).Process(x));
        AssertRaw(m.GetProperty("cic_int_8_4"), new CicInterpolator(8, 4, skip: 0, arithmetic: Arithmetic.Q16, options: o).Process(x));
        AssertRaw(m.GetProperty("cic_int_9_4"), new CicInterpolator(9, 4, skip: 0, arithmetic: Arithmetic.Q16, options: o).Process(x));
        AssertRaw(m.GetProperty("fir_3_7"), new PolyphaseResampler(3, 7, h, 0, Arithmetic.Q16, o).Process(x));
        AssertRaw(m.GetProperty("fir_1_1"), new PolyphaseResampler(1, 1, h, 0, Arithmetic.Q16, o).Process(x));
        AssertRaw(m.GetProperty("fir_2_1"), new PolyphaseResampler(2, 1, h, 0, Arithmetic.Q16, o).Process(x));

        var farrow = new FarrowResampler(48000 / 44100.0, arithmetic: Arithmetic.Q16, options: o);
        Assert.Equal(m.GetProperty("farrow_inc").GetInt64(), farrow.Increment);
        AssertRaw(m.GetProperty("farrow"), farrow.Process(x));
    }

    // ---------------------------------------------------------------- схемы главы 3 на коротких сигналах

    [Fact]
    public void Schemes_ShortSignals_MatchExperimentsDoubleAndQ16()
    {
        foreach (JsonElement r in G.GetProperty("short").EnumerateArray())
        {
            int fs = r.GetProperty("Fs").GetInt32();
            int ft = r.GetProperty("Ft").GetInt32();
            Vector x = r.GetProperty("x").Vec();

            Vector synD = ResamplerDesign.CicCorrectorFarrow(fs, ft, 20000).ProcessBlock(x);
            Vector synQ = ResamplerDesign.CicCorrectorFarrow(fs, ft, 20000, arithmetic: Arithmetic.Q16, options: Q16Options.Author).ProcessBlock(x);
            double[] expD = r.GetProperty("syn_double").Doubles();
            double[] expQ = r.GetProperty("syn_q16").Doubles();

            int n = Math.Min(expD.Length, synD.Count);
            // Эксперименты обрезают выход Фэрроу по длине всего входа (m < ⌊(N − 3 − d)/r⌋); поток выдаёт
            // все отсчёты, для которых пришли узлы, — на 1–2 отсчёта больше в самом конце
            Assert.True(synD.Count - expD.Length is >= 0 and <= 2, $"{fs}→{ft}: длина {synD.Count}, эталон {expD.Length}");
            AssertClose(expD[..n], synD.Take(n).ToArray(), 1e-6, $"{fs}→{ft} double");
            // Q16 побитно: ИГФ точен, корректор и Фэрроу повторяют умножение автора, фаза Фэрроу точна
            AssertCloseLsb(expQ[..n], synQ.Take(n).ToArray(), 0, $"{fs}→{ft} Q16");

            Vector stdD = ResamplerDesign.Standard(fs, ft, 20000).ProcessBlock(x);
            AssertClose(r.GetProperty("std_double").Doubles(), stdD.ToArray(), 1e-12, $"{fs}→{ft} СТД");
            if (r.GetProperty("std_q16").ValueKind == JsonValueKind.Array)
            {
                Vector stdQ = ResamplerDesign.Standard(fs, ft, 20000, arithmetic: Arithmetic.Q16, options: Q16Options.Author).ProcessBlock(x);
                AssertCloseLsb(r.GetProperty("std_q16").Doubles(), stdQ.ToArray(), 0, $"{fs}→{ft} СТД Q16");
            }
        }
    }

    // ---------------------------------------------------------------- Акима (scipy)

    [Fact]
    public void Akima_MatchesScipyAkima1DInterpolator()
    {
        JsonElement a = G.GetProperty("akima");
        double[] y = a.GetProperty("y").Doubles();
        double[] t = a.GetProperty("t").Doubles();
        double[] v = a.GetProperty("v").Doubles();
        var kernel = new AkimaKernel();

        for (int i = 0; i < t.Length; i++)
        {
            int b = (int)Math.Floor(t[i]);
            var nodes = new Vector(y.Skip(b - 2).Take(6));
            Assert.Equal(v[i], kernel.Interpolate(nodes, t[i] - b), 12);
        }
    }

    [Fact]
    public void Akima_InFarrow_Q16TracksDouble()
    {
        double[] x = DspGolden.TestSignal(48000, 0.05, [1000, 3000], [0.3, 1.1], 0.4);
        var d = new FarrowResampler(48000 / 44100.0, new AkimaKernel());
        var q = new FarrowResampler(48000 / 44100.0, new AkimaKernel(), arithmetic: Arithmetic.Q16, options: Q16Options.Recommended);
        Vector yd = d.ProcessBlock(new Vector(x));
        Vector yq = q.ProcessBlock(new Vector(x));
        Assert.Equal(yd.Count, yq.Count);
        Assert.True(ResamplingMetrics.QuantizationSnr(yq, yd) > 70);
    }

    // ---------------------------------------------------------------- правила и цепочка

    [Fact]
    public void Rules_MatchChapter2Examples()
    {
        Assert.Equal(20.8, CicDesign.StageAliasAttenuation(8, 0.083), 1);
        Assert.Equal(3, CicDesign.MinimumStages(8, 0.083, 60));
        Assert.Equal(5.4, CicDesign.StageAliasAttenuation(8, 0.42), 1);
        Assert.Equal(12, CicDesign.MinimumStages(8, 0.42, 60));

        Assert.Equal(0.025, LagrangeKernel.MaxRelativeFrequency(1, 60), 3);
        Assert.Equal(0.097, LagrangeKernel.MaxRelativeFrequency(3, 60), 3);
        Assert.Equal(0.151, LagrangeKernel.MaxRelativeFrequency(5, 60), 3);
    }

    [Fact]
    public void Chain_IsBlock_AndCountsPerChainOutput()
    {
        MultirateChain chain = ResamplerDesign.CicCorrectorFarrow(384000, 44100, 20000);
        Assert.Equal(3, chain.Blocks.Count);
        Assert.Equal(44100 / 384000.0, chain.RateFactor, 9);

        // Умножения на 1 с выходного сигнала — как в таблице 3.2: 31·48000 + 12·44100
        Assert.Equal(2_017_200, chain.OperationsPerOutput.Multiplications * 44100, 0);

        var nested = new MultirateChain(chain, new PolyphaseResampler(1, 1, new Vector(1.0)));
        Assert.Equal(chain.OperationsPerOutput.Multiplications + 1, nested.OperationsPerOutput.Multiplications, 9);
    }

    // ---------------------------------------------------------------- вспомогательное

    internal static void AssertClose(double[] expected, double[] actual, double tol, string what = "")
    {
        Assert.True(expected.Length == actual.Length, $"{what}: длина {actual.Length}, ожидалось {expected.Length}");
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(expected[i] - actual[i]) <= tol, $"{what}: [{i}] {actual[i]:R} ≠ {expected[i]:R}");
        }
    }

    private static void AssertCloseLsb(double[] expected, double[] actual, int lsb, string what)
    {
        Assert.True(expected.Length == actual.Length, $"{what}: длина {actual.Length}, ожидалось {expected.Length}");
        int differ = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            long d = Math.Abs((long)Math.Round((expected[i] - actual[i]) * Q16.One));
            Assert.True(d <= lsb, $"{what}: [{i}] разница {d} младших разрядов");
            differ += d > 0 ? 1 : 0;
        }

        Assert.True(differ <= expected.Length / 50, $"{what}: отличаются {differ} отсчётов из {expected.Length}");
    }

    private static Vector Raw(long[] raw) => new(raw.Select(v => v / (double)Q16.One));

    private static void AssertRaw(JsonElement expected, Vector actual)
    {
        long[] e = expected.Longs();
        Assert.Equal(e.Length, actual.Count);
        for (int i = 0; i < e.Length; i++)
        {
            Assert.Equal(e[i], (long)(actual[i] * Q16.One));
        }
    }

    private static double Sinc(double x) => x == 0 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);
}

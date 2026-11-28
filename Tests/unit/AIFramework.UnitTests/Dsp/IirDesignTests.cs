using System.Numerics;
using System.Text.Json;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using Vector = AI.DataStructs.Algebraic.Vector;
using AI.DSP.IIR;
using AI.DSP.Multirate;
using AI.MathUtils.SpecialFunction;
using Xunit;

namespace AIFramework.UnitTests.Dsp;

/// <summary>
/// Дизайн БИХ-фильтров по ТЗ против scipy.signal: butter, cheby1, cheby2, ellip, bessel (output='sos'),
/// buttord … ellipord, zpk2sos, sosfilt, sosfiltfilt, частотные характеристики.
/// </summary>
public sealed class IirDesignTests
{
    private const double Fs = 600.0;

    private static JsonElement G => DspGolden.Iir;

    public static TheoryData<int> DesignIndices()
    {
        var data = new TheoryData<int>();
        int n = G.GetProperty("designs").GetArrayLength();
        for (int i = 0; i < n; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DesignIndices))]
    public void Design_MatchesScipySos(int index)
    {
        JsonElement d = G.GetProperty("designs")[index];
        IirFamily family = Family(d.GetProperty("kind").GetString()!);
        IirBandType band = Band(d.GetProperty("btype").GetString()!);
        int order = d.GetProperty("order").GetInt32();
        Vector wn = d.GetProperty("wn").ValueKind == JsonValueKind.Array ? d.GetProperty("wn").Vec() : new Vector(d.GetProperty("wn").GetDouble());
        double rp = d.GetProperty("rp").ValueKind == JsonValueKind.Number ? d.GetProperty("rp").GetDouble() : 1;
        double rs = d.GetProperty("rs").ValueKind == JsonValueKind.Number ? d.GetProperty("rs").GetDouble() : 40;
        BesselNormalization norm = d.GetProperty("norm").GetString() switch
        {
            "mag" => BesselNormalization.Magnitude,
            "delay" => BesselNormalization.Delay,
            _ => BesselNormalization.Phase,
        };

        IirFilterDesign design = IirDesign.Design(family, band, order, Fs, wn, rp, rs, norm);
        double[][] expected = [.. d.GetProperty("sos").EnumerateArray().Select(r => r.Doubles())];
        Matrix actual = design.Sections.Coefficients;
        string what = $"{family} {band} n={order}";
        Assert.True(expected.Length == actual.Height, $"{what}: звеньев {actual.Height}, scipy {expected.Length}");

        double scale = expected.SelectMany(r => r).Max(Math.Abs);
        double tol = norm == BesselNormalization.Magnitude ? 1e-7 : 1e-9;
        for (int i = 0; i < expected.Length; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double e = expected[i][j];
                Assert.True(Math.Abs(e - actual[i, j]) <= tol * Math.Max(1, Math.Abs(e)) + (1e-12 * scale),
                    $"{what}: sos[{i},{j}] = {actual[i, j]:R}, scipy {e:R}");
            }
        }

        Assert.True(design.Sections.IsStable, $"{what}: неустойчив");
    }

    [Fact]
    public void Order_MatchesScipyOrdFunctions()
    {
        foreach (JsonElement row in G.GetProperty("orders").EnumerateArray())
        {
            IirBandType band = Band(row.GetProperty("btype").GetString()!);
            Vector wp = Edges(row.GetProperty("wp"));
            Vector ws = Edges(row.GetProperty("ws"));
            var spec = new IirSpecification(band, Fs, wp, ws, row.GetProperty("gpass").GetDouble(), row.GetProperty("gstop").GetDouble());

            foreach ((string name, IirFamily family) in new[] { ("butter", IirFamily.Butterworth), ("cheby1", IirFamily.ChebyshevI), ("cheby2", IirFamily.ChebyshevII), ("ellip", IirFamily.Elliptic) })
            {
                JsonElement e = row.GetProperty(name);
                IirOrderEstimate est = IirOrder.Estimate(family, spec);
                string what = $"{name} {band} {string.Join("/", wp)}";
                Assert.True(e.GetProperty("n").GetInt32() == est.Order, $"{what}: порядок {est.Order}, scipy {e.GetProperty("n").GetInt32()}");
                MultirateBlockTests.AssertClose(e.GetProperty("wn").Doubles(), est.Cutoff.ToArray(), 1e-8, what);

                // Фильтр минимального порядка выполняет ТЗ (Баттерворт узкой полосы требует порядка 402 — вне предела 256)
                if (est.Order > 256)
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => IirDesign.Design(family, spec));
                    continue;
                }

                IirFilterDesign design = IirDesign.Design(family, spec);
                Assert.True(design.Verify(spec).Satisfied, $"{what}: ТЗ не выполнено {design.Verify(spec)}");
            }
        }
    }

    [Fact]
    public void Filtering_MatchesSosfiltAndSosfiltfilt()
    {
        JsonElement f = G.GetProperty("filtering");
        var sos = new SecondOrderSections(Mat(f.GetProperty("sos")));
        Vector x = f.GetProperty("x").Vec();
        MultirateBlockTests.AssertClose(f.GetProperty("sosfilt").Doubles(), sos.Filter(x).ToArray(), 1e-12, "sosfilt");
        MultirateBlockTests.AssertClose(f.GetProperty("sosfiltfilt").Doubles(), sos.FilterZeroPhase(x).ToArray(), 1e-11, "sosfiltfilt");

        // Потоком по частям — то же самое
        SosFilter stream = sos.CreateFilter();
        var parts = new List<double>();
        parts.AddRange(stream.Process(new Vector(x.Take(37))));
        parts.AddRange(stream.Process(new Vector(x.Skip(37))));
        MultirateBlockTests.AssertClose(f.GetProperty("sosfilt").Doubles(), [.. parts], 1e-12, "поток");
    }

    [Fact]
    public void Response_MatchesSosfreqzAndGroupDelay()
    {
        JsonElement f = G.GetProperty("filtering");
        JsonElement r = G.GetProperty("response");
        var sos = new SecondOrderSections(Mat(f.GetProperty("sos")));
        Vector freqs = r.GetProperty("f").Vec();
        ComplexVector h = sos.FrequencyResponse(freqs, Fs);
        MultirateBlockTests.AssertClose(r.GetProperty("re").Doubles(), [.. h.Select(c => c.Real)], 1e-12, "Re H");
        MultirateBlockTests.AssertClose(r.GetProperty("im").Doubles(), [.. h.Select(c => c.Imaginary)], 1e-12, "Im H");
        // scipy считает задержку по перемноженной передаточной функции 12-го порядка и теряет до 10⁻⁷ отн.;
        // здесь — по звеньям
        double[] gd = r.GetProperty("gd").Doubles();
        double[] mine = sos.GroupDelay(freqs, Fs).ToArray();
        for (int i = 0; i < gd.Length; i++)
        {
            Assert.True(Math.Abs(gd[i] - mine[i]) <= 1e-6 * Math.Max(1, Math.Abs(gd[i])), $"τ[{i}] {mine[i]:R} ≠ {gd[i]:R}");
        }

        Vector phase = sos.Phase(freqs, Fs);
        Assert.True(phase.Zip(phase.Skip(1)).All(p => Math.Abs(p.Second - p.First) < Math.PI));
    }

    [Fact]
    public void ZpkToSos_MatchesScipyPairing()
    {
        JsonElement g = G.GetProperty("zpk2sos");
        var zpk = new ZeroPoleGain(Cv(g.GetProperty("z")), Cv(g.GetProperty("p")), g.GetProperty("k").GetDouble());
        Matrix actual = SecondOrderSections.FromZeroPoleGain(zpk).Coefficients;
        Matrix expected = Mat(g.GetProperty("sos"));
        Assert.Equal(expected.Height, actual.Height);
        for (int i = 0; i < expected.Height; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(expected[i, j], actual[i, j], 12);
            }
        }
    }

    [Fact]
    public void EllipticFunctions_MatchScipySpecial()
    {
        foreach (JsonElement r in G.GetProperty("ellipk").EnumerateArray())
        {
            Assert.Equal(r.GetProperty("k").GetDouble(), EllipticFunctions.CompleteK(r.GetProperty("m").GetDouble()), 13);
        }

        foreach (JsonElement r in G.GetProperty("ellipj").EnumerateArray())
        {
            (double sn, double cn, double dn, _) = EllipticFunctions.Jacobi(r.GetProperty("u").GetDouble(), r.GetProperty("m").GetDouble());
            Assert.Equal(r.GetProperty("sn").GetDouble(), sn, 13);
            Assert.Equal(r.GetProperty("cn").GetDouble(), cn, 13);
            Assert.Equal(r.GetProperty("dn").GetDouble(), dn, 13);
        }
    }

    [Fact]
    public void LivePrototypeSpec_FourTypes_MeetSpecification()
    {
        // ФНЧ: fs = 600 Гц, пропускание до 45 Гц (1 дБ), задерживание от 55 Гц (40 дБ)
        IirSpecification spec = IirSpecification.LowPass(600, 45, 55, 1, 40);
        var orders = new Dictionary<IirFamily, int>();
        foreach (IirFamily family in new[] { IirFamily.Butterworth, IirFamily.ChebyshevI, IirFamily.ChebyshevII, IirFamily.Elliptic })
        {
            IirFilterDesign d = IirDesign.Design(family, spec);
            Assert.True(d.Verify(spec).Satisfied, $"{family}: {d.Verify(spec)}");
            orders[family] = d.Order;
        }

        Assert.Equal(26, orders[IirFamily.Butterworth]);
        Assert.Equal(9, orders[IirFamily.ChebyshevI]);
        Assert.Equal(9, orders[IirFamily.ChebyshevII]);
        Assert.Equal(5, orders[IirFamily.Elliptic]);

        // У Бесселя затухание растёт медленно: ТЗ 20/150 Гц, 1/40 дБ выполняет порядок 4 (scipy, norm='mag')
        IirSpecification gentle = IirSpecification.LowPass(600, 20, 150, 1, 40);
        IirFilterDesign bessel = IirDesign.Design(IirFamily.Bessel, gentle);
        Assert.Equal(4, bessel.Order);
        Assert.True(bessel.Verify(gentle).Satisfied);
    }

    [Fact]
    public void Specification_RejectsContradictions_WithClearMessage()
    {
        Assert.Contains("ниже границы задерживания", Assert.Throws<ArgumentException>(() => IirSpecification.LowPass(600, 55, 45, 1, 40)).Message);
        Assert.Contains("половиной частоты", Assert.Throws<ArgumentException>(() => IirSpecification.LowPass(600, 45, 400, 1, 40)).Message);
        Assert.Contains("превышать", Assert.Throws<ArgumentException>(() => IirSpecification.LowPass(600, 45, 55, 3, 2)).Message);
        Assert.Throws<ArgumentException>(() => IirSpecification.BandPass(600, 80, 160, 90, 190, 1, 40));
        Assert.Throws<ArgumentException>(() => IirDesign.Design(IirFamily.Butterworth, IirBandType.BandPass, 4, 600, new Vector(50.0)));

        IirSpecification lin = IirSpecification.FromLinear(IirBandType.LowPass, 600, new Vector(45.0), new Vector(55.0), 0.1, 0.01);
        Assert.Equal(-20 * Math.Log10(0.9), lin.PassbandRippleDb, 12);
        Assert.Equal(40, lin.StopbandAttenuationDb, 12);
    }

    [Fact]
    public void Q16Sos_TracksDouble_AndLegacyIirFilterAgrees()
    {
        SecondOrderSections sos = IirDesign.Design(IirFamily.Butterworth, IirBandType.LowPass, 4, Fs, new Vector(50.0)).Sections;
        var rnd = new Random(3);
        var x = new Vector(Enumerable.Range(0, 2000).Select(_ => (rnd.NextDouble() - 0.5) * 0.5));
        Vector yd = sos.Filter(x);
        Vector yq = sos.CreateFilter(Arithmetic.Q16, Q16Options.Recommended).ProcessBlock(x);
        Assert.True(ResamplingMetrics.QuantizationSnr(yq, yd) > 40);

        Vector legacy = sos.ToIirFilter().FilterOutp(x);
        MultirateBlockTests.AssertClose(yd.ToArray(), legacy.ToArray(), 1e-10, "IIRFilter(a, b)");
    }

    [Fact]
    public void Biquads_HaveCookbookProperties()
    {
        SecondOrderSections lp = Biquad.LowPass(48000, 1000, 2.0);
        Assert.Equal(1.0, lp.Response(0, 48000).Magnitude, 12);
        Assert.Equal(2.0, lp.Response(1000, 48000).Magnitude, 9);
        Assert.Equal(0.0, Biquad.Notch(48000, 1000, 5).Response(1000, 48000).Magnitude, 9);
        Assert.Equal(1.0, Biquad.AllPass(48000, 1000, 1).Response(3000, 48000).Magnitude, 12);
        Assert.Equal(6.0, 20 * Math.Log10(Biquad.Peaking(48000, 1000, 1, 6).Response(1000, 48000).Magnitude), 9);
        Assert.Equal(6.0, 20 * Math.Log10(Biquad.LowShelf(48000, 1000, 6).Response(0, 48000).Magnitude), 9);
        Assert.Equal(6.0, 20 * Math.Log10(Biquad.HighShelf(48000, 1000, 6).Response(24000, 48000).Magnitude), 9);
        Assert.Equal(1.0, Biquad.BandPass(48000, 1000, 3).Response(1000, 48000).Magnitude, 9);
        Assert.Equal(1.0, Biquad.HighPass(48000, 1000).Response(24000, 48000).Magnitude, 12);
    }

    private static IirFamily Family(string kind) => kind switch
    {
        "butter" => IirFamily.Butterworth,
        "cheby1" => IirFamily.ChebyshevI,
        "cheby2" => IirFamily.ChebyshevII,
        "ellip" => IirFamily.Elliptic,
        _ => IirFamily.Bessel,
    };

    private static IirBandType Band(string b) => b switch
    {
        "lowpass" => IirBandType.LowPass,
        "highpass" => IirBandType.HighPass,
        "bandpass" => IirBandType.BandPass,
        _ => IirBandType.BandStop,
    };

    private static Vector Edges(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.Vec() : new Vector(e.GetDouble());

    private static Matrix Mat(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(x => x.Doubles())];
        var m = new Matrix(r.Length, 6);
        for (int i = 0; i < r.Length; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }

    private static ComplexVector Cv(JsonElement e) => new(e.EnumerateArray().Select(v => new Complex(v[0].GetDouble(), v[1].GetDouble())));
}

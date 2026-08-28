using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Signals;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Сглаживание, базовая линия, поиск и интегрирование пиков.</summary>
public class SignalTests
{
    private const double Step = 0.005;
    private const double Sigma = 0.1;
    private const double Center = 5.0;
    private const double Height = 100.0;
    private const int Points = 2000;

    private static double GaussianArea => Height * Sigma * Math.Sqrt(2 * Math.PI);

    private static double[] Axis()
    {
        var time = new double[Points];

        for (int i = 0; i < Points; i++)
            time[i] = i * Step;

        return time;
    }

    private static double[] Gaussian(double center = Center, double height = Height, double sigma = Sigma)
    {
        var signal = new double[Points];

        for (int i = 0; i < Points; i++)
        {
            double t = i * Step;
            signal[i] = height * Math.Exp(-Math.Pow(t - center, 2) / (2 * sigma * sigma));
        }

        return signal;
    }

    /// <summary>Классические коэффициенты для окна 5 и полинома 2: (-3, 12, 17, 12, -3)/35.</summary>
    [Fact]
    public void SavitzkyGolay_ReproducesClassicCoefficients()
    {
        double[] expected = { -3.0, 12, 17, 12, -3 };
        double[] actual = SavitzkyGolay.Coefficients(5, 2);

        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i] / 35, actual[i], 1e-12);

        Assert.Equal(1.0, actual.Sum(), 1e-12);
    }

    /// <summary>
    /// Полином степени не выше порядка фильтра проходит без искажений, включая края:
    /// у границ используется полином крайнего окна, а не отражение сигнала.
    /// </summary>
    [Fact]
    public void SavitzkyGolay_PreservesStraightLineIncludingEdges()
    {
        double[] line = Enumerable.Range(0, 50).Select(i => 3.0 + (0.5 * i)).ToArray();
        double[] smoothed = SavitzkyGolay.Apply(line, 9, 2);

        for (int i = 0; i < line.Length; i++)
            Assert.Equal(line[i], smoothed[i], 1e-9);
    }

    [Fact]
    public void SavitzkyGolay_FirstDerivativeOfLineIsSlope()
    {
        double[] line = Enumerable.Range(0, 50).Select(i => 3.0 + (0.5 * i)).ToArray();
        double[] derivative = SavitzkyGolay.Apply(line, 9, 2, derivative: 1);

        foreach (double value in derivative.Skip(5).Take(40))
            Assert.Equal(0.5, value, 1e-9);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(5, 5)]
    [InlineData(5, 0)]
    public void SavitzkyGolay_RejectsBadParameters(int window, int order)
        => Assert.Throws<ArgumentException>(() => SavitzkyGolay.Coefficients(window, order));

    [Fact]
    public void Peak_MatchesAnalyticalGaussian()
    {
        IReadOnlyList<Peak> peaks = PeakDetector.Detect(Axis(), Gaussian());

        Peak peak = Assert.Single(peaks);
        Assert.Equal(Center, peak.RetentionTime, 0.005);
        Assert.Equal(Height, peak.Height, 0.5);
        Assert.Equal(GaussianArea, peak.Area, 0.3);
        Assert.Equal(2.3548 * Sigma, peak.WidthAtHalfHeight, 0.005);
        Assert.Equal(100.0, peak.AreaPercent, 1e-9);
    }

    [Fact]
    public void Peak_PlatesAndSymmetryFollowDefinitions()
    {
        Peak peak = PeakDetector.Detect(Axis(), Gaussian()).Single();

        Assert.Equal(5.54 * Math.Pow(Center / (2.3548 * Sigma), 2), peak.TheoreticalPlates, 20.0);
        Assert.Equal(1.0, peak.AsymmetryFactor, 0.05);
        Assert.Equal(1.0, peak.UspTailing, 0.05);
    }

    private static double[] TwoPeaks(double separation)
    {
        double[] first = Gaussian(4.0, 100);
        double[] second = Gaussian(4.0 + separation, 80);

        return first.Zip(second, (a, b) => a + b).ToArray();
    }

    [Fact]
    public void ResolvedPeaks_AreSeparatedAndProportional()
    {
        IReadOnlyList<Peak> peaks = PeakDetector.Detect(Axis(), TwoPeaks(1.0));

        Assert.Equal(2, peaks.Count);
        Assert.True(Peak.Resolution(peaks[0], peaks[1]) > 1.5,
            $"Rs = {Peak.Resolution(peaks[0], peaks[1]):F2}");
        Assert.Equal(0.8, peaks[1].Area / peaks[0].Area, 0.02);
        Assert.Equal(100.0, peaks.Sum(p => p.AreaPercent), 1e-9);
    }

    [Fact]
    public void OverlappingPeaks_AreReportedAsPoorlyResolved()
    {
        IReadOnlyList<Peak> peaks = PeakDetector.Detect(Axis(), TwoPeaks(0.12));

        if (peaks.Count == 2)
            Assert.True(Peak.Resolution(peaks[0], peaks[1]) < 1.0);
        else
            Assert.Single(peaks);
    }

    /// <summary>Асимметричный МНК снимает дрейф, не съедая площадь пика.</summary>
    [Fact]
    public void AsymmetricBaseline_RemovesDriftAndKeepsArea()
    {
        double[] time = Axis();
        double[] peak = Gaussian();
        var drifted = new double[Points];

        for (int i = 0; i < Points; i++)
            drifted[i] = peak[i] + 20 + (0.8 * time[i]) + (0.05 * time[i] * time[i]);

        double[] baseline = BaselineCorrection.AsymmetricLeastSquares(drifted, smoothness: 1e8);
        double[] restored = BaselineCorrection.Subtract(drifted, baseline);

        Assert.True(Math.Abs(restored[0]) < 1.0 && Math.Abs(restored[Points - 1]) < 1.0,
            $"края: {restored[0]:F3} / {restored[Points - 1]:F3}");

        Peak found = Assert.Single(PeakDetector.Detect(time, restored));
        Assert.Equal(GaussianArea, found.Area, 0.03 * GaussianArea);
    }

    [Fact]
    public void SmoothnessForPeakWidth_ScalesWithFourthPower()
        => Assert.Equal(Math.Pow(3 * 47, 4), BaselineCorrection.SmoothnessForPeakWidth(47), 1.0);

    /// <summary>Оценка шума по медиане разностей устойчива и не зависит от пиков.</summary>
    [Fact]
    public void NoiseEstimate_RecoversStandardDeviation()
    {
        var random = new Random(7);
        var noise = new double[500];

        for (int i = 0; i < noise.Length; i++)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            noise[i] = 0.1 * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        Assert.Equal(0.1, BaselineCorrection.EstimateNoise(noise), 0.02);
    }

    [Fact]
    public void Chromatogram_ReportsPeaksAndResolution()
    {
        var chromatogram = new Chromatogram(Axis(), TwoPeaks(1.0)) { Name = "проба" };
        IReadOnlyList<Peak> peaks = chromatogram.FindPeaks();
        string report = chromatogram.Report(peaks);

        Assert.Equal(2, peaks.Count);
        Assert.Contains("проба", report, StringComparison.Ordinal);
        Assert.Contains("Rs =", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalStandard_UsesCalibration()
    {
        var calibration = new AnalyticalCalibration(
            new[] { 0.0, 1, 2, 5, 10 },
            new[] { 0.012, 0.105, 0.203, 0.501, 0.998 });

        Assert.Equal(5.0, Quantification.ExternalStandard(0.501, calibration).Value, 0.05);
    }

    [Fact]
    public void InternalStandard_ScalesByAreaRatio()
        => Assert.Equal(20.0, Quantification.InternalStandard(500, 250, 10.0), 1e-9);

    [Fact]
    public void ResponseFactor_IsRelativeSensitivity()
        => Assert.Equal(2.0, Quantification.ResponseFactor(200, 1, 100, 1), 1e-9);

    /// <summary>Метод добавок: концентрация равна модулю точки пересечения с осью абсцисс.</summary>
    [Fact]
    public void StandardAddition_FindsInterceptOnConcentrationAxis()
    {
        ConcentrationEstimate estimate = Quantification.StandardAddition(
            new[] { 0.0, 1.0, 2.0, 3.0 },
            new[] { 0.20, 0.30, 0.40, 0.50 });

        Assert.Equal(2.0, estimate.Value, 1e-6);
    }

    [Fact]
    public void MassFraction_ConvertsToPercent()
        => Assert.Equal(1.0, Quantification.MassFractionPercent(50, 100, 500), 1e-9);

    [Fact]
    public void AreaNormalization_AccountsForResponseFactors()
    {
        var peaks = PeakDetector.Detect(Axis(), TwoPeaks(1.0)).ToList();
        peaks[0].Name = "A";
        peaks[1].Name = "B";

        var factors = new Dictionary<string, double> { ["B"] = 0.8 };
        var normalized = Quantification.AreaNormalization(peaks, factors);

        Assert.Equal(100.0, normalized.Sum(n => n.Percent), 1e-9);
        Assert.Equal(50.0, normalized[0].Percent, 1.0);
    }
}

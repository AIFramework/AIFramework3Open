using AI.DataStructs.Algebraic;
using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Квазислучайные последовательности Холтона и Соболя и выборка Пуассона по заданному равномерному числу.
/// </summary>
public class LowDiscrepancyTests
{
    [Fact]
    public void HaltonSequence_Next_GivesKnownFirstValues()
    {
        var halton = new HaltonSequence(2);
        double[] base2 = [0, 0.5, 0.25, 0.75, 0.125, 0.625, 0.375, 0.875];
        double[] base3 = [0, 1.0 / 3, 2.0 / 3, 1.0 / 9, 4.0 / 9, 7.0 / 9, 2.0 / 9, 5.0 / 9];

        for (int i = 0; i < base2.Length; i++)
        {
            var point = halton.Next();
            Assert.Equal(base2[i], point[0], 15);
            Assert.Equal(base3[i], point[1], 15);
        }

        Assert.Equal(8, halton.Index);
    }

    [Fact]
    public void HaltonSequence_Skip_StartsFromGivenIndex()
    {
        var skipped = new HaltonSequence(3, skip: 5).Next();
        var direct = new HaltonSequence(3).Point(5);

        Assert.Equal(direct.ToArray(), skipped.ToArray());
    }

    [Fact]
    public void HaltonSequence_Scrambled_KeepsStratificationOnEachAxis()
    {
        var plain = new HaltonSequence(5);
        var scrambled = new HaltonSequence(5, scramble: new Random(7));
        int[] counts = [1024, 729, 625, 343, 1331];
        bool differs = false;

        for (int j = 0; j < counts.Length; j++)
        {
            var cells = new HashSet<int>();

            for (int i = 0; i < counts[j]; i++)
            {
                double x = scrambled.Point(i)[j];
                Assert.InRange(x, 0.0, 1.0 - 1e-15);
                // Первые b^k точек дают ровно дроби m/b^k, каждую по разу
                cells.Add((int)Math.Round(x * counts[j]));
                differs |= Math.Abs(x - plain.Point(i)[j]) > 1e-12;
            }

            Assert.Equal(counts[j], cells.Count);
        }

        Assert.True(differs);
    }

    [Fact]
    public void HaltonSequence_Integration_BeatsPseudoRandom()
    {
        const int n = 4096;
        double exact = (Math.E - 1) * (Math.E - 1);
        double Integrand(double x, double y) => Math.Exp(x + y);

        var halton = new HaltonSequence(2, skip: 1);
        double sum = 0;

        for (int i = 0; i < n; i++)
        {
            var p = halton.Next();
            sum += Integrand(p[0], p[1]);
        }

        double haltonError = Math.Abs((sum / n) - exact);
        double randomSquares = 0;

        for (int seed = 0; seed < 20; seed++)
        {
            var rng = new Random(seed);
            double s = 0;

            for (int i = 0; i < n; i++)
                s += Integrand(rng.NextDouble(), rng.NextDouble());

            randomSquares += Math.Pow((s / n) - exact, 2);
        }

        double randomError = Math.Sqrt(randomSquares / 20);

        Assert.True(haltonError < randomError / 4, $"Холтон {haltonError}, случайные {randomError}");
    }

    [Fact]
    public void HaltonSequence_Generate_ReturnsPointsAsRows()
    {
        var matrix = new HaltonSequence(3).Generate(4);

        Assert.Equal(4, matrix.Height);
        Assert.Equal(3, matrix.Width);
        Assert.Equal(0.75, matrix[3, 0], 15);
        Assert.Equal(1.0 / 9, matrix[3, 1], 15);
        Assert.Equal(0.6, matrix[3, 2], 15);
    }

    [Fact]
    public void HaltonSequence_RadicalInverse_ReversesDigits()
    {
        Assert.Equal(0.3125, HaltonSequence.RadicalInverse(10, 2), 15);
        Assert.Equal(0.0, HaltonSequence.RadicalInverse(0, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => HaltonSequence.RadicalInverse(3, 1));
    }

    [Fact]
    public void SobolSequence_Next_MatchesJoeKuoReferencePoints()
    {
        var sobol = new SobolSequence(21);
        int[] columns = [0, 1, 2, 5, 10, 20];
        double[,] expected =
        {
            { 0, 0, 0, 0, 0, 0 },
            { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 },
            { 0.75, 0.25, 0.25, 0.75, 0.75, 0.25 },
            { 0.25, 0.75, 0.75, 0.25, 0.25, 0.75 },
            { 0.375, 0.375, 0.625, 0.125, 0.875, 0.125 },
            { 0.875, 0.875, 0.125, 0.625, 0.375, 0.625 },
            { 0.625, 0.125, 0.875, 0.875, 0.125, 0.375 },
            { 0.125, 0.625, 0.375, 0.375, 0.625, 0.875 },
        };

        for (int i = 0; i < 8; i++)
        {
            var point = sobol.Next();

            for (int c = 0; c < columns.Length; c++)
                Assert.Equal(expected[i, c], point[columns[c]], 15);
        }
    }

    [Fact]
    public void SobolSequence_Point_MatchesJoeKuoReferenceInAllDimensions()
    {
        // Точки 100 и 777 последовательности с таблицей new-joe-kuo-6.21201 без перемешивания
        double[] point100 =
        [
            0.4140625, 0.2578125, 0.7734375, 0.7265625, 0.8828125, 0.7421875, 0.0234375, 0.4765625, 0.6328125, 0.6953125,
            0.4609375, 0.6796875, 0.4765625, 0.8515625, 0.3203125, 0.4921875, 0.6796875, 0.7421875, 0.8359375, 0.3359375,
            0.7578125,
        ];
        double[] point777 =
        [
            0.6923828125, 0.9365234375, 0.1630859375, 0.2744140625, 0.6357421875, 0.3564453125, 0.1904296875, 0.7626953125,
            0.3486328125, 0.3232421875, 0.7451171875, 0.6962890625, 0.3837890625, 0.4736328125, 0.5693359375, 0.5146484375,
            0.4033203125, 0.8642578125, 0.3701171875, 0.7529296875, 0.2373046875,
        ];

        var sobol = new SobolSequence(21);

        Assert.Equal(point100, sobol.Point(100).ToArray());
        Assert.Equal(point777, sobol.Point(777).ToArray());
    }

    [Fact]
    public void SobolSequence_NextAndSkip_AgreeWithDirectPoint()
    {
        var sobol = new SobolSequence(7);

        for (int i = 0; i < 300; i++)
            Assert.Equal(sobol.Point(i).ToArray(), sobol.Next().ToArray());

        var skipped = new SobolSequence(7, skip: 37);
        Assert.Equal(new SobolSequence(7).Point(37).ToArray(), skipped.Next().ToArray());
    }

    [Fact]
    public void SobolSequence_FirstPowerOfTwoPoints_StratifyEveryAxisAndFirstPlane()
    {
        var sobol = new SobolSequence(SobolSequence.MaxDimension);
        var points = sobol.Generate(1024);

        for (int j = 0; j < SobolSequence.MaxDimension; j++)
        {
            var cells = new HashSet<int>();

            for (int i = 0; i < 1024; i++)
                cells.Add((int)(points[i, j] * 1024));

            Assert.Equal(1024, cells.Count);
        }

        var squares = new HashSet<int>();

        for (int i = 0; i < 256; i++)
            squares.Add(((int)(points[i, 0] * 16) * 16) + (int)(points[i, 1] * 16));

        Assert.Equal(256, squares.Count);
    }

    [Fact]
    public void SobolSequence_DigitalShift_KeepsStratification()
    {
        var sobol = new SobolSequence(4, shift: new Random(3));
        var cells = new HashSet<int>[4];

        for (int j = 0; j < 4; j++)
            cells[j] = [];

        bool nonZeroStart = false;

        for (int i = 0; i < 512; i++)
        {
            var point = sobol.Next();

            for (int j = 0; j < 4; j++)
            {
                Assert.InRange(point[j], 0.0, 1.0 - 1e-15);
                cells[j].Add((int)(point[j] * 512));
                nonZeroStart |= i == 0 && point[j] != 0;
            }
        }

        Assert.All(cells, set => Assert.Equal(512, set.Count));
        Assert.True(nonZeroStart);
    }

    [Fact]
    public void SobolSequence_Integration_BeatsPseudoRandomInFiveDimensions()
    {
        const int n = 4096;
        const int d = 5;
        double exact = Math.Pow(2 / Math.PI, d);

        double Integrand(Vector p)
        {
            double product = 1;

            for (int j = 0; j < d; j++)
                product *= Math.Sin(Math.PI * p[j]);

            return product;
        }

        var sobol = new SobolSequence(d);
        double sum = 0;

        for (int i = 0; i < n; i++)
            sum += Integrand(sobol.Next());

        double sobolError = Math.Abs((sum / n) - exact);
        double randomSquares = 0;

        for (int seed = 0; seed < 10; seed++)
        {
            var rng = new Random(seed);
            double s = 0;

            for (int i = 0; i < n; i++)
            {
                var p = new Vector(d);

                for (int j = 0; j < d; j++)
                    p[j] = rng.NextDouble();

                s += Integrand(p);
            }

            randomSquares += Math.Pow((s / n) - exact, 2);
        }

        double randomError = Math.Sqrt(randomSquares / 10);

        Assert.True(sobolError < randomError / 5, $"Соболь {sobolError}, случайные {randomError}");
    }

    [Fact]
    public void SobolSequence_Constructor_RejectsUnsupportedDimension()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SobolSequence(22));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SobolSequence(0));
    }

    [Fact]
    public void InverseTransformSampling_Poisson_SwitchesAtCdfSteps()
    {
        double f0 = Math.Exp(-2);
        double f1 = 3 * Math.Exp(-2);

        Assert.Equal(0, InverseTransformSampling.Poisson(0, 2));
        Assert.Equal(0, InverseTransformSampling.Poisson(f0 - 1e-9, 2));
        Assert.Equal(1, InverseTransformSampling.Poisson(f0 + 1e-9, 2));
        Assert.Equal(1, InverseTransformSampling.Poisson(f1 - 1e-9, 2));
        Assert.Equal(2, InverseTransformSampling.Poisson(f1 + 1e-9, 2));
        Assert.Equal(0, InverseTransformSampling.Poisson(0.7, 0));
    }

    [Theory]
    [InlineData(3.5)]
    [InlineData(40.0)]
    [InlineData(5000.0)]
    public void InverseTransformSampling_Poisson_UniformGridGivesMeanAndVarianceLambda(double lambda)
    {
        const int n = 100_000;
        double sum = 0, squares = 0;
        int previous = 0;

        for (int i = 0; i < n; i++)
        {
            int k = InverseTransformSampling.Poisson((i + 0.5) / n, lambda);
            Assert.True(k >= previous);
            previous = k;
            sum += k;
            squares += (double)k * k;
        }

        double mean = sum / n;
        double variance = (squares / n) - (mean * mean);

        Assert.Equal(lambda, mean, lambda * 1e-3 + 1e-3);
        Assert.Equal(lambda, variance, lambda * 0.01 + 0.01);
    }

    [Fact]
    public void InverseTransformSampling_Poisson_HandlesLargeLambdaAndRejectsBadInput()
    {
        int median = InverseTransformSampling.Poisson(0.5, 800);

        Assert.InRange(median, 799, 801);
        Assert.Throws<ArgumentOutOfRangeException>(() => InverseTransformSampling.Poisson(1.0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => InverseTransformSampling.Poisson(0.5, -1));
    }
}

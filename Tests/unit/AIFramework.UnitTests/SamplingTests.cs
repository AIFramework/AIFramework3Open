using AI.Geometry.Primitives;
using AI.Geometry.Sampling;
using AI.Statistics;
using Xunit;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AIFramework.UnitTests;

/// <summary>
/// Равномерные точки на фигурах и направления фон Мизеса-Фишера: статистические свойства выборок.
/// </summary>
public class SamplingTests
{
    private const int N = 40_000;

    [Fact]
    public void PlaneSampling_InDisk_RadialCdfIsQuadratic()
    {
        var rng = new Random(1);
        var disk = new Circle(new Vector(1.0, -2.0), 3.0);
        int inner = 0, outer = 0;
        double mx = 0, my = 0;

        for (int i = 0; i < N; i++)
        {
            var p = PlaneSampling.InDisk(rng, disk);
            double r = Math.Sqrt(Math.Pow(p[0] - 1, 2) + Math.Pow(p[1] + 2, 2)) / 3;
            Assert.True(r <= 1 + 1e-12);
            inner += r < 0.5 ? 1 : 0;
            outer += r < 0.9 ? 1 : 0;
            mx += p[0];
            my += p[1];
        }

        Assert.Equal(0.25, (double)inner / N, 0.01);
        Assert.Equal(0.81, (double)outer / N, 0.01);
        Assert.Equal(1.0, mx / N, 0.05);
        Assert.Equal(-2.0, my / N, 0.05);
    }

    [Fact]
    public void PlaneSampling_InDiskFromHalton_IntegratesSecondMoment()
    {
        var halton = new HaltonSequence(2, skip: 1);
        var disk = new Circle(new Vector(0.0, 0.0), 2.0);
        double sum = 0;

        for (int i = 0; i < 4096; i++)
        {
            var u = halton.Next();
            var p = PlaneSampling.InDisk(u[0], u[1], disk);
            sum += p[0] * p[0];
        }

        Assert.Equal(1.0, sum / 4096, 0.005);
    }

    [Fact]
    public void PlaneSampling_OnCircle_LiesOnCircleWithCentralMean()
    {
        var rng = new Random(2);
        var circle = new Circle(new Vector(0.5, 0.5), 2.0);
        double mx = 0, my = 0;

        for (int i = 0; i < N; i++)
        {
            var p = PlaneSampling.OnCircle(rng, circle);
            Assert.Equal(2.0, Math.Sqrt(Math.Pow(p[0] - 0.5, 2) + Math.Pow(p[1] - 0.5, 2)), 12);
            mx += p[0];
            my += p[1];
        }

        Assert.Equal(0.5, mx / N, 0.03);
        Assert.Equal(0.5, my / N, 0.03);
    }

    [Fact]
    public void PlaneSampling_InTriangle_BarycentricsAreUniform()
    {
        var rng = new Random(3);
        var triangle = new Triangle(new Vector(0.0, 0.0), new Vector(4.0, 1.0), new Vector(1.0, 3.0));
        double[] means = new double[3];
        int nearA = 0;

        for (int i = 0; i < N; i++)
        {
            var (a, b, c) = triangle.BarycentricCoords(PlaneSampling.InTriangle(rng, triangle));
            Assert.True(a >= -1e-12 && b >= -1e-12 && c >= -1e-12);
            means[0] += a;
            means[1] += b;
            means[2] += c;
            nearA += a > 0.5 ? 1 : 0;
        }

        Assert.All(means, m => Assert.Equal(1.0 / 3, m / N, 0.01));
        Assert.Equal(0.25, (double)nearA / N, 0.01);
    }

    [Fact]
    public void PlaneSampling_InTriangle_WorksInSpace()
    {
        var rng = new Random(4);
        var triangle = new Triangle(new Vector(1.0, 0.0, 0.0), new Vector(0.0, 1.0, 0.0), new Vector(0.0, 0.0, 1.0));

        for (int i = 0; i < 1000; i++)
        {
            var p = PlaneSampling.InTriangle(rng, triangle);
            Assert.Equal(1.0, p[0] + p[1] + p[2], 12);
        }
    }

    [Fact]
    public void PolygonSampler_Convex_AreaWeightedAndMapsUniforms()
    {
        Vector[] quad = [new(0.0, 0.0), new(4.0, 0.0), new(3.0, 2.0), new(0.0, 2.0)];
        var sampler = new PolygonSampler(quad);
        var rng = new Random(5);
        int strip = 0;

        Assert.Equal(7.0, sampler.Area, 12);

        for (int i = 0; i < N; i++)
            strip += sampler.Sample(rng)[0] < 1 ? 1 : 0;

        Assert.Equal(2.0 / 7, (double)strip / N, 0.01);

        var halton = new HaltonSequence(2, skip: 1);
        int haltonStrip = 0;

        for (int i = 0; i < 4096; i++)
        {
            var u = halton.Next();
            var p = sampler.Sample(u[0], u[1]);
            Assert.True(AI.Geometry.Polygons.PointInPolygon.Contains(p, quad) || p[0] <= 1e-12 || p[1] <= 1e-12);
            haltonStrip += p[0] < 1 ? 1 : 0;
        }

        Assert.Equal(2.0 / 7, haltonStrip / 4096.0, 0.005);
    }

    [Fact]
    public void PolygonSampler_NonConvex_IsUniformForRandomAndGivenUniforms()
    {
        Vector[] shape = [new(0.0, 0.0), new(2.0, 0.0), new(2.0, 1.0), new(1.0, 1.0), new(1.0, 2.0), new(0.0, 2.0)];
        var sampler = new PolygonSampler(shape);
        var rng = new Random(6);
        int top = 0;

        Assert.Equal(3.0, sampler.Area, 12);

        for (int i = 0; i < N; i++)
        {
            var p = sampler.Sample(rng);
            Assert.False(p[0] > 1 + 1e-12 && p[1] > 1 + 1e-12);
            top += p[1] > 1 ? 1 : 0;
        }

        Assert.Equal(1.0 / 3, (double)top / N, 0.01);

        var halton = new HaltonSequence(2, skip: 1);
        int haltonTop = 0;

        for (int i = 0; i < 4096; i++)
        {
            var u = halton.Next();
            var p = sampler.Sample(u[0], u[1]);
            Assert.False(p[0] > 1 + 1e-12 && p[1] > 1 + 1e-12);
            haltonTop += p[1] > 1 ? 1 : 0;
        }

        Assert.Equal(1.0 / 3, haltonTop / 4096.0, 0.005);
    }

    [Fact]
    public void PolygonSampler_WithHole_AvoidsHoleAndSubtractsItsArea()
    {
        Vector[] square = [new(0.0, 0.0), new(4.0, 0.0), new(4.0, 4.0), new(0.0, 4.0)];
        Vector[] hole = [new(1.0, 1.0), new(3.0, 1.0), new(3.0, 3.0), new(1.0, 3.0)];
        var sampler = new PolygonSampler(square, hole);
        var rng = new Random(14);
        int strip = 0;

        Assert.Equal(12.0, sampler.Area, 9);

        for (int i = 0; i < N; i++)
        {
            var p = sampler.Sample(rng);
            Assert.False(p[0] > 1 + 1e-9 && p[0] < 3 - 1e-9 && p[1] > 1 + 1e-9 && p[1] < 3 - 1e-9);
            strip += p[0] < 1 ? 1 : 0;
        }

        Assert.Equal(1.0 / 3, (double)strip / N, 0.01);
    }

    [Fact]
    public void SpaceSampling_Direction_HasZeroMeanAndIsotropicSpread()
    {
        var rng = new Random(7);
        double mx = 0, my = 0, mz = 0, zz = 0;

        for (int i = 0; i < N; i++)
        {
            var d = SpaceSampling.Direction(rng);
            Assert.Equal(1.0, d.Length, 12);
            mx += d.X;
            my += d.Y;
            mz += d.Z;
            zz += d.Z * d.Z;
        }

        Assert.Equal(0.0, mx / N, 0.015);
        Assert.Equal(0.0, my / N, 0.015);
        Assert.Equal(0.0, mz / N, 0.015);
        Assert.Equal(1.0 / 3, zz / N, 0.01);
    }

    [Fact]
    public void SpaceSampling_DirectionInHigherDimension_IsUnitWithZeroMean()
    {
        var rng = new Random(8);
        var mean = new double[5];

        for (int i = 0; i < 10_000; i++)
        {
            var d = SpaceSampling.Direction(rng, 5);
            Assert.Equal(1.0, Math.Sqrt(Vector.Dot(d, d)), 12);

            for (int j = 0; j < 5; j++)
                mean[j] += d[j];
        }

        Assert.All(mean, m => Assert.Equal(0.0, m / 10_000, 0.02));
    }

    [Fact]
    public void SpaceSampling_SphereAndBall_HaveCorrectRadii()
    {
        var rng = new Random(9);
        var center = new Vector3(1, 2, 3);
        int inner = 0;

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(2.0, SpaceSampling.OnSphere(rng, center, 2).DistanceTo(center), 12);

            double r = SpaceSampling.InBall(rng, center, 2).DistanceTo(center) / 2;
            Assert.True(r <= 1 + 1e-12);
            inner += r < 0.5 ? 1 : 0;
        }

        Assert.Equal(0.125, (double)inner / N, 0.008);
    }

    [Fact]
    public void SpaceSampling_InBox_StaysInsideWithCentralMean()
    {
        var rng = new Random(10);
        var box = new Aabb(new Vector(-1.0, 0.0, 2.0, 5.0), new Vector(1.0, 4.0, 3.0, 5.5));
        var mean = new double[4];

        for (int i = 0; i < N; i++)
        {
            var p = SpaceSampling.InBox(rng, box);
            Assert.True(box.Contains(p));

            for (int j = 0; j < 4; j++)
                mean[j] += p[j];
        }

        for (int j = 0; j < 4; j++)
            Assert.Equal(box.Center[j], mean[j] / N, 0.03);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(50.0)]
    public void SpaceSampling_VonMisesFisher_MeanCosineIsLangevin(double kappa)
    {
        var rng = new Random(11);
        var mu = new Vector3(1, 2, -2) / 3;
        double expected = kappa == 0 ? 0 : (1 / Math.Tanh(kappa)) - (1 / kappa);
        double sum = 0;

        for (int i = 0; i < N; i++)
        {
            var d = SpaceSampling.VonMisesFisher(rng, mu, kappa);
            Assert.Equal(1.0, d.Length, 12);
            sum += d.Dot(mu);
        }

        Assert.Equal(expected, sum / N, 0.006);
    }

    [Fact]
    public void SpaceSampling_VonMisesFisherFromSobol_MeanCosineIsLangevin()
    {
        var sobol = new SobolSequence(2, skip: 1);
        var mu = new Vector3(0, 0, -1);
        double sum = 0;

        for (int i = 0; i < 4096; i++)
        {
            var u = sobol.Next();
            sum += SpaceSampling.VonMisesFisher(u[0], u[1], mu, 5).Dot(mu);
        }

        Assert.Equal((1 / Math.Tanh(5)) - 0.2, sum / 4096, 0.001);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(50.0)]
    public void SpaceSampling_VonMisesFisherWood_MatchesThreeDimensionalMeanCosine(double kappa)
    {
        var rng = new Random(12);
        var mu = new Vector(0.0, 3.0, 4.0);
        double sum = 0;

        for (int i = 0; i < N; i++)
        {
            var d = SpaceSampling.VonMisesFisher(rng, mu, kappa);
            Assert.Equal(1.0, Math.Sqrt(Vector.Dot(d, d)), 12);
            sum += Vector.Dot(d, mu) / 5;
        }

        Assert.Equal((1 / Math.Tanh(kappa)) - (1 / kappa), sum / N, 0.006);
    }

    [Fact]
    public void SpaceSampling_VonMisesFisherWood_ConcentratesInHigherDimension()
    {
        var rng = new Random(13);
        var mu = new Vector(1.0, 0.0, 0.0, 0.0, 0.0);
        double weak = 0, strong = 0;

        for (int i = 0; i < 10_000; i++)
        {
            weak += SpaceSampling.VonMisesFisher(rng, mu, 1)[0];
            strong += SpaceSampling.VonMisesFisher(rng, mu, 100)[0];
        }

        // При больших κ среднее косинуса близко к 1 − (p − 1)/(2κ)
        Assert.InRange(weak / 10_000, 0.1, 0.4);
        Assert.Equal(1 - (4.0 / 200), strong / 10_000, 0.003);
    }
}

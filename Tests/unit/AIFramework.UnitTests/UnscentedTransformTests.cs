#nullable enable

using AI.DataStructs.Algebraic;
using AI.MathUtils.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Сигма-точки: линейная функция переносится точно, квадратичная получает верное среднее, произведение независимых
/// входов дает известные дисперсию и градиент, нулевое СКО входа не ломает перенос.
/// </summary>
public class UnscentedTransformTests
{
    [Fact]
    public void Points_HaveExpectedCountAndWeights()
    {
        SigmaPoints points = UnscentedTransform.Points(new Vector(1.0, 2.0), new Vector(0.5, 0.0));

        Assert.Equal(5, points.Count);
        Assert.Equal(1, points.MeanWeights.Sum(), 12);
        Assert.Equal(new Vector(1.0, 2.0), points.Points[0]);
        // Вход с нулевым СКО не разбрасывается: его пара точек совпадает с центральной
        Assert.Equal(points.Points[0], points.Points[2]);
        Assert.Equal(points.Points[0], points.Points[4]);
    }

    [Fact]
    public void LinearFunction_IsTransferredExactly()
    {
        var covariance = new Matrix(new double[,] { { 4, 1 }, { 1, 9 } });
        // y = (2x₁ − x₂, x₂)
        UnscentedResult result = UnscentedTransform.Transform(new Vector(1.0, 3.0), covariance,
            x => new Vector((2 * x[0]) - x[1], x[1]));

        Assert.Equal(-1, result.Mean[0], 10);
        Assert.Equal(3, result.Mean[1], 10);
        // Ковариация образа A·P·Aᵀ
        Assert.Equal((4 * 4) - (2 * 2 * 1) + 9, result.Covariance[0, 0], 8);
        Assert.Equal((2 * 1) - 9, result.Covariance[0, 1], 8);
        Assert.Equal(9, result.Covariance[1, 1], 8);
        // Линеаризация совпадает с матрицей функции
        Assert.Equal(2, result.Linearization[0, 0], 8);
        Assert.Equal(-1, result.Linearization[0, 1], 8);
        Assert.Equal(0, result.Linearization[1, 0], 8);
        Assert.Equal(1, result.Linearization[1, 1], 8);
    }

    [Fact]
    public void Square_GetsSecondOrderMean_AndGradientAtMean()
    {
        // v ~ 20 ± 10: E[v²] = 400 + 100, производная статистической линеаризации 2·20
        var (mean, variance, gradient) = UnscentedTransform.Transform(new Vector(20.0), new Vector(10.0), x => x[0] * x[0]);

        Assert.Equal(500, mean, 8);
        Assert.Equal(40, gradient[0], 8);
        Assert.True(variance > 0);
    }

    [Fact]
    public void Product_OfIndependentInputs_HasKnownVarianceAndGradient()
    {
        // путь = скорость · время, 20 ± 10 и 2 ± 0,5: дисперсия по линеаризации (2·10)² + (20·0,5)². Точный взаимный
        // член (10·0,5)² = 25 точки вдоль осей не видят: он второго порядка по разбросу, здесь 5 % дисперсии
        var (mean, variance, gradient) = UnscentedTransform.Transform(new Vector(20.0, 2.0), new Vector(10.0, 0.5), x => x[0] * x[1]);

        Assert.Equal(40, mean, 8);
        Assert.Equal(400 + 100, variance, 6);
        Assert.Equal(2, gradient[0], 8);
        Assert.Equal(20, gradient[1], 8);
    }

    [Fact]
    public void ZeroStd_GivesZeroGradient_ForThatInput()
    {
        var (mean, variance, gradient) = UnscentedTransform.Transform(new Vector(3.0, 4.0), new Vector(1.0, 0.0), x => x[0] + (5 * x[1]));

        Assert.Equal(23, mean, 10);
        Assert.Equal(1, variance, 10);
        Assert.Equal(1, gradient[0], 10);
        Assert.Equal(0, gradient[1], 10);
    }

    [Fact]
    public void FullCovariance_MatchesDiagonalOverload_WhenIndependent()
    {
        var mean = new Vector(1.0, -2.0);
        var covariance = new Matrix(new double[,] { { 0.25, 0 }, { 0, 4 } });
        Vector f(Vector x) => new Vector(Math.Exp(0.1 * x[0]) + x[1] * x[1]);

        UnscentedResult full = UnscentedTransform.Transform(mean, covariance, f);
        UnscentedResult diagonal = UnscentedTransform.Transform(mean, new Vector(0.5, 2.0), f);

        Assert.Equal(diagonal.Mean[0], full.Mean[0], 10);
        Assert.Equal(diagonal.Covariance[0, 0], full.Covariance[0, 0], 8);
        Assert.Equal(diagonal.Linearization[0, 1], full.Linearization[0, 1], 8);
    }
}

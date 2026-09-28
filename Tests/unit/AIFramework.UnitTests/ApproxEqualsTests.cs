using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.Extensions;
using Complex = System.Numerics.Complex;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Приближённое равенство чисел и структур: допуски, NaN, бесконечности, пустые структуры и форма.
/// </summary>
public class ApproxEqualsTests
{
    [Theory]
    [InlineData(1.0, 1.0 + 1e-10, true)]
    [InlineData(1e9, 1e9 + 0.5, true)]
    [InlineData(1.0, 1.001, false)]
    [InlineData(0.0, 1e-13, true)]
    [InlineData(0.0, 1e-9, false)]
    [InlineData(double.NaN, double.NaN, true)]
    [InlineData(double.NaN, 0.0, false)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, true)]
    [InlineData(double.PositiveInfinity, double.NegativeInfinity, false)]
    [InlineData(double.PositiveInfinity, 1e308, false)]
    public void Numbers(double a, double b, bool equal)
    {
        Assert.Equal(equal, AlgebraicStructsExtensions.ApproxEquals(a, b));
        Assert.Equal(equal, AlgebraicStructsExtensions.ApproxEquals(b, a));
    }

    [Fact]
    public void TolerancesAreParameters()
    {
        Assert.True(AlgebraicStructsExtensions.ApproxEquals(1.0, 1.001, relTol: 1e-2));
        Assert.True(AlgebraicStructsExtensions.ApproxEquals(0.0, 1e-9, absTol: 1e-6));
    }

    [Fact]
    public void Vectors()
    {
        Assert.True(new Vector(1.0, double.NaN, 3.0).ApproxEquals(new Vector(1.0 + 1e-12, double.NaN, 3.0)));
        Assert.False(new Vector(1.0, 2.0).ApproxEquals(new Vector(1.0, 2.0, 0.0)));
        Assert.False(new Vector(1.0, double.PositiveInfinity).ApproxEquals(new Vector(1.0, 1e300)));
        Assert.True(new Vector().ApproxEquals(new Vector()));
        Assert.False(new Vector().ApproxEquals(null!));
    }

    [Fact]
    public void MatricesCompareShape()
    {
        var wide = new Matrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        var tall = new Matrix(new double[,] { { 1, 2 }, { 3, 4 }, { 5, 6 } });

        Assert.Equal(wide.Data, tall.Data);
        Assert.False(wide.ApproxEquals(tall));
        Assert.True(wide.ApproxEquals(new Matrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 + 1e-11 } })));
        Assert.True(new Matrix(0, 0).ApproxEquals(new Matrix(0, 0)));
    }

    [Fact]
    public void ComplexVectors()
    {
        var a = new ComplexVector([new Complex(1, 2), new Complex(double.NaN, 0)]);

        Assert.True(a.ApproxEquals(new ComplexVector([new Complex(1, 2 + 1e-12), new Complex(double.NaN, 0)])));
        Assert.False(a.ApproxEquals(new ComplexVector([new Complex(1, -2), new Complex(double.NaN, 0)])));
        Assert.True(new ComplexVector(0).ApproxEquals(new ComplexVector(0)));
    }
}

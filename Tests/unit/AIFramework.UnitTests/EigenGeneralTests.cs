using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using Xunit;
using Complex = System.Numerics.Complex;

namespace AIFramework.UnitTests;

/// <summary>
/// Собственные значения несимметричных матриц проверяются корнями известного многочлена
/// (сопровождающая матрица), поворотом и инвариантами — следом, следом квадрата и определителем,
/// посчитанным LU-разложением независимо от QR-итераций.
/// </summary>
public class EigenGeneralTests
{
    [Fact]
    public void Companion_RecoversKnownRoots()
    {
        Complex[] roots = [1, -2, new Complex(3, 4), new Complex(3, -4), 0.5, new Complex(-0.2, 0.9), new Complex(-0.2, -0.9)];
        Complex[] coefficients = Expand(roots);
        int n = roots.Length;
        var companion = new Matrix(n, n);

        for (int i = 0; i + 1 < n; i++)
            companion[i, i + 1] = 1;

        for (int j = 0; j < n; j++)
            companion[n - 1, j] = -coefficients[j].Real;

        AssertSameSpectrum(roots, Eigen.General(companion), 1e-8);
    }

    [Fact]
    public void Rotation_HasConjugatePairOnUnitCircle()
    {
        double angle = 0.7;
        var rotation = new Matrix(new double[,] { { Math.Cos(angle), -Math.Sin(angle) }, { Math.Sin(angle), Math.Cos(angle) } });

        AssertSameSpectrum([Complex.FromPolarCoordinates(1, angle), Complex.FromPolarCoordinates(1, -angle)], Eigen.General(rotation), 1e-12);
        Assert.Equal(1.0, Eigen.SpectralRadius(rotation), 12);
    }

    [Fact]
    public void RandomMatrices_PreserveTraceTraceOfSquareAndDeterminant()
    {
        var rng = new Random(1);

        for (int trial = 0; trial < 40; trial++)
        {
            int n = 2 + rng.Next(7);
            var a = new Matrix(n, n);

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    a[i, j] = (rng.NextDouble() * 2) - 1;

            Complex[] values = Eigen.General(a);
            Complex sum = Complex.Zero, squares = Complex.Zero, product = Complex.One;

            foreach (Complex value in values)
            {
                sum += value;
                squares += value * value;
                product *= value;
            }

            Matrix square = a * a;
            double trace = 0, traceSquare = 0;

            for (int i = 0; i < n; i++)
            {
                trace += a[i, i];
                traceSquare += square[i, i];
            }

            Assert.Equal(n, values.Length);
            Assert.Equal(trace, sum.Real, 9);
            Assert.Equal(0, sum.Imaginary, 9);
            Assert.Equal(traceSquare, squares.Real, 8);
            Assert.Equal(LU.Determinant(a), product.Real, 8);
            Assert.Equal(0, product.Imaginary, 8);
        }
    }

    [Fact]
    public void Symmetric_AgreesWithJacobi()
    {
        var rng = new Random(2);
        var a = new Matrix(6, 6);

        for (int i = 0; i < 6; i++)
            for (int j = i; j < 6; j++)
                a[i, j] = a[j, i] = (rng.NextDouble() * 2) - 1;

        double[] jacobi = Eigen.Symmetric(a).Values.ToArray().OrderBy(v => v).ToArray();
        double[] general = Eigen.General(a).Select(z => z.Real).OrderBy(v => v).ToArray();

        Assert.All(Eigen.General(a), z => Assert.Equal(0, z.Imaginary, 10));

        for (int i = 0; i < 6; i++)
            Assert.Equal(jacobi[i], general[i], 10);
    }

    [Fact]
    public void Triangular_And_DefectiveMatrices()
    {
        var triangular = new Matrix(new double[,] { { 3, 1, 4 }, { 0, -1, 5 }, { 0, 0, 2 } });
        AssertSameSpectrum([3, -1, 2], Eigen.General(triangular), 1e-12);

        // Жорданова клетка: собственное число 2 кратности 3, чувствительность порядка ε^(1/3)
        var jordan = new Matrix(new double[,] { { 2, 1, 0 }, { 0, 2, 1 }, { 0, 0, 2 } });
        Assert.All(Eigen.General(jordan), z => Assert.True(Complex.Abs(z - 2) < 1e-4, z.ToString()));

        Assert.Empty(Eigen.General(new Matrix(0, 0)));
    }

    private static Complex[] Expand(Complex[] roots)
    {
        var coefficients = new Complex[roots.Length + 1];
        coefficients[0] = 1;

        for (int d = 1; d <= roots.Length; d++)
        {
            for (int k = d; k >= 0; k--)
                coefficients[k] = (k > 0 ? coefficients[k - 1] : 0) - (roots[d - 1] * coefficients[k]);
        }

        return coefficients;
    }

    private static void AssertSameSpectrum(Complex[] expected, Complex[] actual, double tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        var remaining = actual.ToList();

        foreach (Complex value in expected)
        {
            Complex nearest = remaining.OrderBy(z => Complex.Abs(z - value)).First();
            Assert.True(Complex.Abs(nearest - value) <= tolerance * Math.Max(1, Complex.Abs(value)), $"{value} не найдено, ближайшее {nearest}");
            remaining.Remove(nearest);
        }
    }
}

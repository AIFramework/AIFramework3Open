using Xunit;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AIFramework.UnitTests;

/// <summary>Решатели собственных значений: спектр, векторы, ортогональность.</summary>
public class EigenTests
{
    private static Matrix Symmetric(int size, int seed)
    {
        var random = new Random(seed);
        var matrix = new Matrix(size, size);

        for (int i = 0; i < size; i++)
        {
            for (int j = i; j < size; j++)
            {
                double value = (random.NextDouble() * 4) - 2;
                matrix[i, j] = value;
                matrix[j, i] = value;
            }
        }

        return matrix;
    }

    private static double ResidualNorm(Matrix a, Vector eigenvalues, Matrix eigenvectors)
    {
        int n = a.Height;
        double worst = 0;

        for (int k = 0; k < n; k++)
        {
            for (int i = 0; i < n; i++)
            {
                double sum = 0;

                for (int j = 0; j < n; j++)
                    sum += a[i, j] * eigenvectors[j, k];

                worst = Math.Max(worst, Math.Abs(sum - (eigenvalues[k] * eigenvectors[i, k])));
            }
        }

        return worst;
    }

    [Fact]
    public void Jacobi_DiagonalMatrixKeepsItsValues()
    {
        var matrix = new Matrix(3, 3);
        matrix[0, 0] = 5;
        matrix[1, 1] = -2;
        matrix[2, 2] = 7;

        var (eigenvalues, _) = JacobiEigen.ComputeVector(matrix);

        Assert.Equal(new[] { -2.0, 5.0, 7.0 }, eigenvalues.OrderBy(v => v).Select(v => Math.Round(v, 9)));
    }

    /// <summary>Матрица [[2,1],[1,2]] имеет собственные значения 3 и 1.</summary>
    [Fact]
    public void Jacobi_TwoByTwoMatchesAnalyticalSolution()
    {
        var matrix = new Matrix(new[,] { { 2.0, 1.0 }, { 1.0, 2.0 } });

        var (eigenvalues, vectors) = JacobiEigen.ComputeVector(matrix);
        double[] sorted = eigenvalues.OrderBy(v => v).ToArray();

        Assert.Equal(1.0, sorted[0], 1e-12);
        Assert.Equal(3.0, sorted[1], 1e-12);
        Assert.Equal(0.0, ResidualNorm(matrix, eigenvalues, vectors), 1e-12);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(20)]
    public void Jacobi_SolvesEigenproblem(int size)
    {
        Matrix matrix = Symmetric(size, size * 7);

        var (eigenvalues, vectors) = JacobiEigen.ComputeVector(matrix);

        Assert.True(ResidualNorm(matrix, eigenvalues, vectors) < 1e-9,
            $"невязка A·v - λ·v велика для матрицы {size}x{size}");
    }

    [Fact]
    public void Jacobi_EigenvectorsAreOrthonormal()
    {
        Matrix matrix = Symmetric(8, 3);
        var (_, vectors) = JacobiEigen.ComputeVector(matrix);

        for (int p = 0; p < 8; p++)
        {
            for (int q = p; q < 8; q++)
            {
                double dot = 0;

                for (int i = 0; i < 8; i++)
                    dot += vectors[i, p] * vectors[i, q];

                Assert.Equal(p == q ? 1.0 : 0.0, dot, 1e-9);
            }
        }
    }

    /// <summary>Сумма собственных значений равна следу матрицы.</summary>
    [Fact]
    public void Jacobi_PreservesTrace()
    {
        Matrix matrix = Symmetric(10, 21);
        var (eigenvalues, _) = JacobiEigen.ComputeVector(matrix);

        double trace = 0;

        for (int i = 0; i < 10; i++)
            trace += matrix[i, i];

        Assert.Equal(trace, eigenvalues.Sum(), 1e-9);
    }

    [Fact]
    public void Jacobi_RejectsNonSquareMatrix()
        => Assert.Throws<ArgumentException>(() => JacobiEigen.Compute(new Matrix(2, 3)));

    /// <summary>Общий QR-решатель на симметричной матрице даёт тот же спектр.</summary>
    [Fact]
    public void QrSolver_MatchesJacobiSpectrum()
    {
        Matrix matrix = Symmetric(5, 11);

        var (jacobiValues, _) = JacobiEigen.ComputeVector(matrix);
        var qr = new EigenValuesVectors(matrix, iterations: 400);

        double[] expected = jacobiValues.OrderBy(v => v).ToArray();
        double[] actual = qr.Eigenvalues.OrderBy(v => v).ToArray();

        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], actual[i], 1e-6);
    }
}

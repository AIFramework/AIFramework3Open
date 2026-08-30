using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Общий решатель симметричной задачи на собственные значения: порядок спектра,
/// невязка собственных пар, обобщённая задача и спектральные функции матрицы.
/// </summary>
public class EigenSolverTests
{
    private static Matrix FromRows(params double[][] rows)
    {
        var matrix = new Matrix(rows.Length, rows[0].Length);

        for (int i = 0; i < rows.Length; i++)
            for (int j = 0; j < rows[i].Length; j++)
                matrix[i, j] = rows[i][j];

        return matrix;
    }

    private static double Residual(Matrix a, Matrix b, Vector values, Matrix vectors)
    {
        int n = a.Height;
        double worst = 0;

        for (int k = 0; k < n; k++)
        {
            for (int i = 0; i < n; i++)
            {
                double left = 0;
                double right = 0;

                for (int j = 0; j < n; j++)
                {
                    left += a[i, j] * vectors[j, k];
                    right += b[i, j] * vectors[j, k];
                }

                worst = Math.Max(worst, Math.Abs(left - (values[k] * right)));
            }
        }

        return worst;
    }

    private static Matrix Identity(int n)
    {
        var matrix = new Matrix(n, n);

        for (int i = 0; i < n; i++)
            matrix[i, i] = 1;

        return matrix;
    }

    #region Обычная задача

    [Fact]
    public void Symmetric_OrdersSpectrumBothWays()
    {
        Matrix matrix = FromRows([2, 1, 0], [1, 2, 1], [0, 1, 2]);

        (Vector ascending, _) = Eigen.Symmetric(matrix);
        (Vector descending, _) = Eigen.Symmetric(matrix, EigenOrder.Descending);

        // Точный спектр: 2, 2 ± sqrt(2)
        Assert.Equal(2 - Math.Sqrt(2), ascending[0], tolerance: 1e-10);
        Assert.Equal(2.0, ascending[1], tolerance: 1e-10);
        Assert.Equal(2 + Math.Sqrt(2), ascending[2], tolerance: 1e-10);

        for (int i = 0; i < 3; i++)
            Assert.Equal(ascending[i], descending[2 - i], tolerance: 1e-12);
    }

    [Fact]
    public void Symmetric_EigenPairsHaveSmallResidual()
    {
        Matrix matrix = FromRows([4, 1, -2], [1, 3, 0.5], [-2, 0.5, 6]);

        (Vector values, Matrix vectors) = Eigen.Symmetric(matrix);

        Assert.True(Residual(matrix, Identity(3), values, vectors) < 1e-10);
    }

    [Fact]
    public void Symmetric_VectorsAreOrthonormal()
    {
        Matrix matrix = FromRows([4, 1, -2], [1, 3, 0.5], [-2, 0.5, 6]);

        (_, Matrix vectors) = Eigen.Symmetric(matrix);

        for (int a = 0; a < 3; a++)
        {
            for (int b = a; b < 3; b++)
            {
                double dot = 0;

                for (int i = 0; i < 3; i++)
                    dot += vectors[i, a] * vectors[i, b];

                Assert.Equal(a == b ? 1.0 : 0.0, dot, tolerance: 1e-10);
            }
        }
    }

    [Fact]
    public void Symmetric_RejectsNonSquareMatrix()
    {
        _ = Assert.Throws<ArgumentException>(() => Eigen.Symmetric(new Matrix(2, 3)));
    }

    #endregion

    #region Обобщённая задача

    [Fact]
    public void GeneralizedSymmetric_WithIdentityMetric_MatchesOrdinaryProblem()
    {
        Matrix matrix = FromRows([4, 1, -2], [1, 3, 0.5], [-2, 0.5, 6]);

        (Vector plain, _) = Eigen.Symmetric(matrix);
        (Vector generalized, _) = Eigen.GeneralizedSymmetric(matrix, Identity(3));

        for (int i = 0; i < 3; i++)
            Assert.Equal(plain[i], generalized[i], tolerance: 1e-9);
    }

    [Fact]
    public void GeneralizedSymmetric_SatisfiesDefiningEquation()
    {
        Matrix a = FromRows([6, 2, 1], [2, 5, 1], [1, 1, 4]);
        Matrix b = FromRows([2, 0.3, 0.1], [0.3, 1.5, 0.2], [0.1, 0.2, 1.0]);

        (Vector values, Matrix vectors) = Eigen.GeneralizedSymmetric(a, b);

        // A·x = λ·B·x с точностью до округления
        Assert.True(Residual(a, b, values, vectors) < 1e-9);
    }

    [Fact]
    public void GeneralizedSymmetric_ScalingMetricScalesSpectrum()
    {
        Matrix a = FromRows([3, 1], [1, 2]);
        Matrix b = FromRows([2, 0], [0, 2]);

        (Vector scaled, _) = Eigen.GeneralizedSymmetric(a, b);
        (Vector plain, _) = Eigen.Symmetric(a);

        // B = 2I означает деление спектра пополам
        for (int i = 0; i < 2; i++)
            Assert.Equal(plain[i] / 2, scaled[i], tolerance: 1e-10);
    }

    [Fact]
    public void GeneralizedSymmetric_RejectsSingularMetric()
    {
        Matrix a = FromRows([1, 0], [0, 1]);
        Matrix singular = FromRows([1, 1], [1, 1]);

        _ = Assert.Throws<ArgumentException>(() => Eigen.GeneralizedSymmetric(a, singular));
    }

    [Fact]
    public void GeneralizedSymmetric_RejectsSizeMismatch()
    {
        _ = Assert.Throws<ArgumentException>(
            () => Eigen.GeneralizedSymmetric(new Matrix(3, 3), new Matrix(2, 2)));
    }

    #endregion

    #region Функции от матрицы

    [Fact]
    public void SquareRoot_SquaresBackToOriginal()
    {
        Matrix matrix = FromRows([4, 1], [1, 3]);
        Matrix root = Eigen.SquareRoot(matrix);
        Matrix squared = root * root;

        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                Assert.Equal(matrix[i, j], squared[i, j], tolerance: 1e-10);
    }

    [Fact]
    public void InverseSquareRoot_WhitensTheMatrix()
    {
        Matrix matrix = FromRows([4, 1, 0.5], [1, 3, 0.2], [0.5, 0.2, 2]);
        Matrix root = Eigen.InverseSquareRoot(matrix);
        Matrix whitened = root * matrix * root;

        // M^(-1/2)·M·M^(-1/2) = I
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.Equal(i == j ? 1.0 : 0.0, whitened[i, j], tolerance: 1e-9);
    }

    [Fact]
    public void InverseSquareRoot_IsSymmetric()
    {
        Matrix matrix = FromRows([4, 1, 0.5], [1, 3, 0.2], [0.5, 0.2, 2]);
        Matrix root = Eigen.InverseSquareRoot(matrix);

        // Ортогонализация по Лёвдину сохраняет симметрию, в отличие от Холецкого
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.Equal(root[i, j], root[j, i], tolerance: 1e-12);
    }

    [Fact]
    public void InverseSquareRoot_RejectsSingularMatrix()
    {
        _ = Assert.Throws<ArgumentException>(() => Eigen.InverseSquareRoot(FromRows([1, 1], [1, 1])));
    }

    [Fact]
    public void SymmetricFunction_AppliesToSpectrum()
    {
        Matrix matrix = FromRows([2, 0], [0, 5]);
        Matrix doubled = Eigen.SymmetricFunction(matrix, value => 2 * value);

        Assert.Equal(4.0, doubled[0, 0], tolerance: 1e-12);
        Assert.Equal(10.0, doubled[1, 1], tolerance: 1e-12);
        Assert.Equal(0.0, doubled[0, 1], tolerance: 1e-12);
    }

    [Fact]
    public void SquareRoot_RejectsNegativeSpectrum()
    {
        _ = Assert.Throws<ArgumentException>(() => Eigen.SquareRoot(FromRows([-4, 0], [0, 1])));
    }

    #endregion
}

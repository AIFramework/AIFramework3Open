using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Определитель и обратная матрица ядра. Опора — матрицы с известным ответом, независимое
/// LU-разложение из AI.ClassicMath и произведение A·A⁻¹, которое обязано дать единичную матрицу.
/// </summary>
public class MatrixInverseDeterminantTests
{
    [Fact]
    public void ControllabilityMatrixWithZeroCorner_HasCorrectDeterminantAndInverse()
    {
        // Матрица управляемости третьего порядка с шагом 0.1: обусловлена хорошо, det = −1e-6.
        // Без перестановки строк исключение делило на ноль в углу, и определитель получался равным 0
        var a = new Matrix(new double[,] { { 0, 0, 0.001 }, { 0, 0.01, 0.019 }, { 0.1, 0.09, 0.081 } });

        Assert.Equal(-1e-6, a.Determinant, 1e-18);
        AssertIdentity(a * a.GetInvertMatrix(), 1e-12);
        Assert.Equal(-1e-6, Diagonal(a.ToTriangularMatr()) * -1, 1e-18);   // одна перестановка строк
    }

    [Fact]
    public void SmallScaleIdentity_IsInvertible()
    {
        // det = 1e-12 меньше прежнего абсолютного порога 1e-10, хотя обусловленность идеальна
        var a = new Matrix(new double[,] { { 1e-6, 0 }, { 0, 1e-6 } });
        Matrix inverse = a.GetInvertMatrix();

        Assert.Equal(1e-12, a.Determinant, 1e-27);
        Assert.Equal(1e6, inverse[0, 0], 1e-6);
        Assert.Equal(1e6, inverse[1, 1], 1e-6);
        Assert.Equal(0.0, inverse[0, 1]);
    }

    [Fact]
    public void ZeroDiagonalPermutation_DeterminantAndInverse()
    {
        var a = new Matrix(new double[,] { { 0, 1 }, { 1, 0 } });
        Matrix inverse = a.GetInvertMatrix();

        Assert.Equal(-1.0, a.Determinant);
        Assert.Equal(1.0, inverse[0, 1]);
        Assert.Equal(1.0, inverse[1, 0]);
        Assert.Equal(0.0, inverse[0, 0]);
    }

    [Fact]
    public void NearlyDiagonalMatrix_IsNotTreatedAsDiagonal()
    {
        // Прежде матрица с внедиагональной долей меньше 0,1 % считалась диагональной: определитель
        // равнялся произведению диагонали, а обратная теряла внедиагональные элементы
        var a = new Matrix(new double[,] { { 1, 1e-4 }, { 1e-4, 1 } });
        Matrix inverse = a.GetInvertMatrix();

        Assert.Equal(1 - 1e-8, a.Determinant, 1e-15);
        Assert.Equal(-1e-4 / (1 - 1e-8), inverse[0, 1], 1e-15);
        AssertIdentity(a * inverse, 1e-14);
    }

    [Fact]
    public void TriangularMatrices_DeterminantIsExactDiagonalProduct()
    {
        Assert.Equal(-24.0, new Matrix(new double[,] { { 2, 5, -1 }, { 0, 3, 7 }, { 0, 0, -4 } }).Determinant);
        Assert.Equal(-24.0, new Matrix(new double[,] { { 2, 0, 0 }, { 50, 3, 0 }, { -9, 70, -4 } }).Determinant);
    }

    [Fact]
    public void RandomMatrices_AgreeWithClassicMathLu_AndInvert()
    {
        var rng = new Random(11);

        for (int trial = 0; trial < 80; trial++)
        {
            int n = 1 + rng.Next(8);
            double scale = Math.Pow(10, rng.Next(-8, 9));
            var a = new Matrix(n, n);

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    a[i, j] = ((rng.NextDouble() * 2) - 1) * scale;

            double expected = LU.Determinant(a);
            double actual = a.Determinant;

            Assert.True(Math.Abs(actual - expected) <= 1e-10 * Math.Abs(expected), $"попытка {trial}: {actual} против {expected}");
            AssertIdentity(a * a.GetInvertMatrix(), 1e-8);
        }
    }

    [Fact]
    public void Inverse_IsScaleInvariant()
    {
        var a = new Matrix(new double[,] { { 4, -2, 1, 0.5 }, { 3, 6, -4, 2 }, { 2, 1, 8, -1 }, { -1, 0.5, 2, 5 } });
        Matrix inverse = a.GetInvertMatrix();
        Matrix scaledInverse = (a * 1e-9).GetInvertMatrix();

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                Assert.Equal(inverse[i, j] * 1e9, scaledInverse[i, j], Math.Abs(inverse[i, j] * 1e9) * 1e-12 + 1e-6);
    }

    [Fact]
    public void SingularMatrices_Throw_AndHaveZeroDeterminant()
    {
        var rankOne = new Matrix(new double[,] { { 1, 2 }, { 2, 4 } });
        Assert.Equal(0.0, Math.Abs(rankOne.Determinant));
        _ = Assert.Throws<InvalidOperationException>(() => rankOne.GetInvertMatrix());

        // Вырожденная в точной арифметике; после округления ведущий элемент порядка 1e-16 — ниже порога n·ε·‖A‖∞
        var classic = new Matrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } });
        Assert.True(Math.Abs(classic.Determinant) < 1e-12);
        _ = Assert.Throws<InvalidOperationException>(() => classic.GetInvertMatrix());

        var withNan = new Matrix(new double[,] { { 1, double.NaN }, { 0, 1 } });
        _ = Assert.Throws<InvalidOperationException>(() => withNan.GetInvertMatrix());

        _ = Assert.Throws<InvalidOperationException>(() => new Matrix(2, 2).GetInvertMatrix());
        _ = Assert.Throws<InvalidOperationException>(() => new Matrix(2, 3).GetInvertMatrix());
        _ = Assert.Throws<InvalidOperationException>(() => new Matrix(2, 3).Determinant);
        Assert.Equal(0.0, new Matrix(3, 3).Determinant);
    }

    private static double Diagonal(Matrix m)
    {
        double product = 1;
        for (int i = 0; i < m.Height; i++)
            product *= m[i, i];
        return product;
    }

    private static void AssertIdentity(Matrix product, double tolerance)
    {
        for (int i = 0; i < product.Height; i++)
            for (int j = 0; j < product.Width; j++)
                Assert.True(Math.Abs(product[i, j] - (i == j ? 1 : 0)) <= tolerance, $"[{i},{j}] = {product[i, j]}");
    }
}

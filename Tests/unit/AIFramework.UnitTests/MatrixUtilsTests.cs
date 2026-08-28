using Xunit;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AIFramework.UnitTests;

/// <summary>Разложения из <c>AI.ClassicMath.MatrixUtils</c> на неквадратных и вырожденных матрицах.</summary>
public class MatrixUtilsTests
{
    private static Matrix FromArray(double[,] values) => new(values);

    private static double MaxDeviation(Matrix left, Matrix right)
    {
        double worst = 0;

        for (int i = 0; i < left.Height; i++)
        {
            for (int j = 0; j < left.Width; j++)
                worst = Math.Max(worst, Math.Abs(left[i, j] - right[i, j]));
        }

        return worst;
    }

    /// <summary>Восстановление матрицы из её сингулярного разложения: A = U·Σ·Vᵀ.</summary>
    [Fact]
    public void Svd_ReconstructsMatrix()
    {
        var random = new Random(42);
        double worst = 0;

        for (int test = 0; test < 30; test++)
        {
            int height = 2 + random.Next(4);
            int width = 2 + random.Next(4);
            var a = new Matrix(height, width);

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                    a[i, j] = test % 3 == 0 ? random.Next(-3, 4) : (random.NextDouble() * 4) - 2;
            }

            var (u, sigma, v) = Svd.Decompose(a);

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    double sum = 0;

                    for (int k = 0; k < width; k++)
                        sum += u[i, k] * sigma[k] * v[j, k];

                    worst = Math.Max(worst, Math.Abs(sum - a[i, j]));
                }
            }
        }

        Assert.True(worst < 1e-9, $"максимальное отклонение {worst:E3}");
    }

    /// <summary>
    /// Столбцы одинаковой нормы: угол поворота Якоби здесь равен 45°, и при
    /// неверном знаке нуля разложение молча возвращало бы единичную матрицу.
    /// </summary>
    [Fact]
    public void Svd_FindsNullSpaceOfEqualNormColumns()
    {
        var a = FromArray(new[,]
        {
            { 1.0, 0.0, -1.0 },
            { 0.0, 1.0, -1.0 },
            { 1.0, -1.0, 0.0 }
        });

        var (_, sigma, v) = Svd.Decompose(a);

        int minIndex = Array.IndexOf(sigma, sigma.Min());
        Assert.True(sigma[minIndex] < 1e-9, $"наименьшее сингулярное число {sigma[minIndex]:E3}");

        // Ядро натянуто на вектор (1, 1, 1)/√3
        for (int i = 1; i < 3; i++)
            Assert.Equal(Math.Abs(v[0, minIndex]), Math.Abs(v[i, minIndex]), 1e-9);
    }

    /// <summary>Свойство Мура-Пенроуза на прямоугольной матрице: A·A⁺·A = A.</summary>
    [Theory]
    [InlineData(5, 3)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    public void Pseudoinverse_SatisfiesMoorePenrose(int height, int width)
    {
        var random = new Random(11);
        var a = new Matrix(height, width);

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
                a[i, j] = (random.NextDouble() * 4) - 2;
        }

        Matrix plus = Pseudoinverse.Compute(a);

        Assert.Equal(width, plus.Height);
        Assert.Equal(height, plus.Width);
        Assert.True(MaxDeviation(a * plus * a, a) < 1e-9, "A·A⁺·A должно совпадать с A");
    }

    /// <summary>Псевдообратная решает переопределённую систему методом наименьших квадратов.</summary>
    [Fact]
    public void Pseudoinverse_SolvesLeastSquares()
    {
        // Точки (0,1), (1,3), (2,5), (3,7) лежат на прямой y = 1 + 2x
        var design = FromArray(new[,] { { 1.0, 0.0 }, { 1.0, 1.0 }, { 1.0, 2.0 }, { 1.0, 3.0 } });
        double[] y = { 1, 3, 5, 7 };

        Matrix plus = Pseudoinverse.Compute(design);
        var coefficients = new double[2];

        for (int k = 0; k < 2; k++)
        {
            for (int i = 0; i < y.Length; i++)
                coefficients[k] += plus[k, i] * y[i];
        }

        Assert.Equal(1.0, coefficients[0], 1e-9);
        Assert.Equal(2.0, coefficients[1], 1e-9);
    }
}

using AI.DataStructs.Algebraic;
using AI.ML.DataHandling.FeaturesTransforms;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Свойства метода главных компонент, не зависящие от реализации решателя:
/// упорядоченность спектра, сохранение следа, ортонормальность компонент.
/// Знак собственного вектора произволен, поэтому ни один тест на него не опирается.
/// </summary>
public class PcaTests
{
    /// <summary>Коррелированное облако: первая компонента должна забрать почти всю дисперсию.</summary>
    private static Matrix CorrelatedCloud(int count = 200, int seed = 7)
    {
        var random = new Random(seed);
        var data = new Matrix(count, 3);

        for (int i = 0; i < count; i++)
        {
            double t = (random.NextDouble() * 10) - 5;

            data[i, 0] = t + (random.NextDouble() * 0.1);
            data[i, 1] = (2 * t) + (random.NextDouble() * 0.1);
            data[i, 2] = (-0.5 * t) + (random.NextDouble() * 0.1);
        }

        return data;
    }

    [Fact]
    public void Pca_Eigenvalues_AreSortedDescending()
    {
        var pca = new PCA();
        _ = pca.Train(CorrelatedCloud());

        for (int i = 1; i < pca.Eigenvalues.Count; i++)
            Assert.True(pca.Eigenvalues[i - 1] >= pca.Eigenvalues[i],
                $"Собственные числа не упорядочены: {pca.Eigenvalues[i - 1]} < {pca.Eigenvalues[i]}");
    }

    [Fact]
    public void Pca_EigenvaluesSum_EqualsTraceOfCovariance()
    {
        Matrix data = CorrelatedCloud();
        Matrix covariance = Matrix.GetCovMatrixFromColumns(data);

        double trace = 0;
        for (int i = 0; i < covariance.Height; i++)
            trace += covariance[i, i];

        var pca = new PCA();
        _ = pca.Train(data);

        double sum = 0;
        for (int i = 0; i < pca.Eigenvalues.Count; i++)
            sum += pca.Eigenvalues[i];

        // След матрицы инвариантен: сумма собственных чисел равна сумме дисперсий
        Assert.Equal(trace, sum, tolerance: 1e-6 * Math.Max(1, Math.Abs(trace)));
    }

    [Fact]
    public void Pca_FirstComponent_CapturesDominantVariance()
    {
        var pca = new PCA();
        _ = pca.Train(CorrelatedCloud());

        double total = 0;
        for (int i = 0; i < pca.Eigenvalues.Count; i++)
            total += pca.Eigenvalues[i];

        // Данные построены вокруг одной прямой: первая компонента забирает почти всё
        Assert.True(pca.Eigenvalues[0] / total > 0.98,
            $"Первая компонента объясняет лишь {pca.Eigenvalues[0] / total:P1} дисперсии");
    }

    [Fact]
    public void Pca_Components_AreOrthonormal()
    {
        var pca = new PCA();
        _ = pca.Train(CorrelatedCloud());

        Matrix components = pca.Components;
        int k = components.Width;

        for (int a = 0; a < k; a++)
        {
            for (int b = a; b < k; b++)
            {
                double dot = 0;

                for (int i = 0; i < components.Height; i++)
                    dot += components[i, a] * components[i, b];

                double expected = a == b ? 1.0 : 0.0;

                Assert.True(Math.Abs(dot - expected) < 1e-6,
                    $"Компоненты {a} и {b} не ортонормальны: скалярное произведение {dot:E3}");
            }
        }
    }

    [Fact]
    public void Pca_ExplainedVariance_SumsToTotal()
    {
        var pca = new PCA(2);
        PCAInfo info = pca.Train(CorrelatedCloud());

        Matrix covariance = Matrix.GetCovMatrixFromColumns(CorrelatedCloud());

        double trace = 0;
        for (int i = 0; i < covariance.Height; i++)
            trace += covariance[i, i];

        // Объяснённая плюс остаточная дисперсия дают полную
        Assert.Equal(trace, info.SaveVar + info.LastVar, tolerance: 1e-6 * Math.Max(1, Math.Abs(trace)));
    }
}

using System.Text.Json;
using AI.DataStructs.Algebraic;
using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Описательная статистика против numpy, scipy и pandas.
/// </summary>
/// <remarks>
/// Эталоны выгружает Tools/MathGolden/export_stats_golden.py в Golden/descriptive_statistics.json;
/// Python при тестах не нужен. Выборки подобраны под известные расхождения прежних копий: чётное
/// число элементов (медиана), одиннадцать элементов (квартили по девяти правилам), сдвиг на 1e9
/// (формула E[x²] − E[x]² теряет там все знаки), связки (ранги).
/// </remarks>
public class DescriptiveStatisticsGoldenTests
{
    private static readonly Lazy<JsonElement> s_data = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "descriptive_statistics.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    });

    private static IEnumerable<JsonElement> Samples => s_data.Value.GetProperty("samples").EnumerateArray();

    private static IEnumerable<JsonElement> Pairs => s_data.Value.GetProperty("pairs").EnumerateArray();

    private static double[] Levels => Doubles(s_data.Value.GetProperty("levels"));

    // --- квантиль и медиана ---

    [Fact]
    public void Quantile_AllNineTypes_MatchNumpy()
    {
        foreach (JsonElement sample in Samples)
        {
            double[] values = Doubles(sample.GetProperty("values"));

            foreach (QuantileMethod method in Enum.GetValues<QuantileMethod>())
            {
                double[] expected = Doubles(sample.GetProperty("quantiles").GetProperty(((int)method).ToString(System.Globalization.CultureInfo.InvariantCulture)));

                for (int i = 0; i < Levels.Length; i++)
                {
                    AssertClose(expected[i], Quantile.Of(values, Levels[i], method), 1e-12, $"{Name(sample)}: квантиль {Levels[i]} по правилу {method}");
                }
            }
        }
    }

    [Fact]
    public void Quantile_DefaultIsType7_AsNumpyAndPandas()
    {
        double[] eleven = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

        Assert.Equal(3.5, Quantile.Of(eleven, 0.25));
        Assert.Equal(8.5, Quantile.Of(eleven, 0.75));
        Assert.Equal(3.5, new Quantile(new Vector(eleven)).GetQuantile(0.25));
    }

    [Fact]
    public void Median_MatchesNumpy_IncludingEvenCount()
    {
        foreach (JsonElement sample in Samples)
        {
            AssertClose(sample.GetProperty("median").GetDouble(), Quantile.Median(Doubles(sample.GetProperty("values"))), 1e-13, $"{Name(sample)}: медиана");
        }

        // Чётное число элементов: полусумма двух средних, а не один из них
        Assert.Equal(2.5, Quantile.Median([1.0, 2.0, 3.0, 4.0]));
        Assert.Equal(2.5, new Quantile(new Vector(4.0, 1.0, 3.0, 2.0)).GetQuantile(0.5));
    }

    [Fact]
    public void Quantile_Edges_AreDocumented()
    {
        Assert.True(double.IsNaN(Quantile.Of([], 0.5)));
        Assert.True(double.IsNaN(Quantile.Of([1.0, double.NaN, 3.0], 0.5)));
        Assert.Equal(7.0, Quantile.Of([7.0], 0.3));
        Assert.Equal(double.PositiveInfinity, Quantile.Of([1.0, double.PositiveInfinity, double.PositiveInfinity], 0.9));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Quantile.Of([1.0, 2.0], 1.5));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Quantile.Of([1.0, 2.0], double.NaN));
        _ = Assert.Throws<InvalidOperationException>(() => new Quantile(new Vector(0)).GetQuantile(0.5));
    }

    [Fact]
    public void Quantile_DoesNotModifyInput()
    {
        double[] values = [3, 1, 2];

        _ = Quantile.Of(values, 0.5);

        Assert.Equal([3.0, 1.0, 2.0], values);
    }

    // --- дисперсия и СКО ---

    [Fact]
    public void Variance_SampleByDefault_PopulationByParameter_MatchNumpy()
    {
        foreach (JsonElement sample in Samples)
        {
            double[] values = Doubles(sample.GetProperty("values"));
            var vector = new Vector(values);

            AssertClose(sample.GetProperty("variance_sample").GetDouble(), Statistic.CalcVariance(values), 1e-12, $"{Name(sample)}: выборочная дисперсия");
            AssertClose(sample.GetProperty("variance_population").GetDouble(), Statistic.CalcVariance(values, population: true), 1e-12, $"{Name(sample)}: генеральная дисперсия");
            AssertClose(sample.GetProperty("std_sample").GetDouble(), Statistic.CalcStd(values), 1e-12, $"{Name(sample)}: выборочное СКО");
            AssertClose(sample.GetProperty("std_population").GetDouble(), Statistic.CalcStd(values, population: true), 1e-12, $"{Name(sample)}: генеральное СКО");

            // Вектор и структура идут в ту же реализацию
            Assert.Equal(Statistic.CalcVariance(values), vector.Dispersion());
            Assert.Equal(Statistic.CalcVariance(values), Statistic.CalcVariance(vector));
            Assert.Equal(Statistic.CalcStd(values), vector.Std());
        }
    }

    /// <summary>Сдвиг на 1e9: наивная формула E[x²] − E[x]² давала здесь −128 и 0 вместо 31.67.</summary>
    [Fact]
    public void Variance_OfShiftedData_KeepsDigits()
    {
        double[] shifted = [.. Enumerable.Range(1, 19).Select(i => 1e9 + i)];

        Assert.Equal(31.666666666666668, Statistic.CalcVariance(shifted), 1e-9);
        Assert.Equal(30.0, Statistic.CalcVariance(shifted, population: true), 1e-9);
    }

    [Fact]
    public void Variance_Edges_HaveOneRule()
    {
        Assert.Equal(0, Statistic.CalcVariance(ReadOnlySpan<double>.Empty));
        Assert.Equal(0, Statistic.CalcVariance([5.0]));
        Assert.Equal(0, Statistic.CalcVariance([5.0], population: true));
        Assert.Equal(0, Statistic.CalcVariance([2.0, 2.0, 2.0]));

        // NaN не входит в расчёт
        Assert.Equal(Statistic.CalcVariance([1.0, 2.0, 4.0]), Statistic.CalcVariance([1.0, double.NaN, 2.0, 4.0]));
    }

    // --- ковариация и корреляция ---

    [Fact]
    public void CovarianceAndPearson_MatchNumpy()
    {
        foreach (JsonElement pair in Pairs)
        {
            double[] x = Doubles(pair.GetProperty("x")), y = Doubles(pair.GetProperty("y"));

            AssertRelative(pair.GetProperty("covariance_sample").GetDouble(), Statistic.Cov(x, y), 1e-11, $"{Name(pair)}: ковариация");
            AssertRelative(pair.GetProperty("covariance_population").GetDouble(), Statistic.Cov(x, y, population: true), 1e-11, $"{Name(pair)}: генеральная ковариация");
            AssertClose(pair.GetProperty("pearson").GetDouble(), Statistic.Pearson(x, y), 1e-12, $"{Name(pair)}: Пирсон");
            Assert.Equal(Statistic.Pearson(x, y), Statistic.CorrelationCoefficient(new Vector(x), new Vector(y)));
        }
    }

    [Fact]
    public void Pearson_Edges_HaveOneRule()
    {
        Assert.Equal(0, Statistic.Pearson([1.0, 2.0, 3.0], [5.0, 5.0, 5.0]));
        Assert.Equal(0, Statistic.Pearson([1.0], [2.0]));
        _ = Assert.Throws<ArgumentException>(() => Statistic.Pearson([1.0, 2.0], [1.0]));
        _ = Assert.Throws<ArgumentException>(() => Statistic.Cov([1.0, 2.0], [1.0]));
    }

    // --- асимметрия и эксцесс ---

    [Fact]
    public void SkewnessAndKurtosis_BothEstimators_MatchScipy()
    {
        foreach (JsonElement sample in Samples)
        {
            if (!sample.TryGetProperty("skewness_biased", out JsonElement skewness)) continue;

            double[] values = Doubles(sample.GetProperty("values"));

            AssertClose(skewness.GetDouble(), Statistic.Skewness(values), 1e-10, $"{Name(sample)}: асимметрия g₁");
            AssertClose(sample.GetProperty("skewness_unbiased").GetDouble(), Statistic.Skewness(values, biasCorrected: true), 1e-10, $"{Name(sample)}: асимметрия G₁");
            AssertClose(sample.GetProperty("excess_kurtosis_biased").GetDouble(), Statistic.ExcessKurtosis(values), 1e-9, $"{Name(sample)}: эксцесс g₂");
            AssertClose(sample.GetProperty("excess_kurtosis_unbiased").GetDouble(), Statistic.ExcessKurtosis(values, biasCorrected: true), 1e-9, $"{Name(sample)}: эксцесс G₂");
        }
    }

    [Fact]
    public void SkewnessAndKurtosis_Undefined_AreNaN()
    {
        Assert.True(double.IsNaN(Statistic.Skewness([3.0, 3.0, 3.0])));
        Assert.True(double.IsNaN(Statistic.ExcessKurtosis([3.0, 3.0, 3.0])));
        Assert.True(double.IsNaN(Statistic.Skewness([1.0, 2.0], biasCorrected: true)));
        Assert.True(double.IsNaN(Statistic.ExcessKurtosis([1.0, 2.0, 4.0], biasCorrected: true)));
        Assert.True(double.IsNaN(Statistic.Skewness(ReadOnlySpan<double>.Empty)));
    }

    // --- нормировка и ранги ---

    [Fact]
    public void ZScores_MatchScipy()
    {
        foreach (JsonElement sample in Samples)
        {
            double[] values = Doubles(sample.GetProperty("values"));

            AssertAll(Doubles(sample.GetProperty("zscore_sample")), Statistic.ZScores(values), 1e-9, $"{Name(sample)}: z-оценки (n − 1)");
            AssertAll(Doubles(sample.GetProperty("zscore_population")), Statistic.ZScores(values, population: true), 1e-9, $"{Name(sample)}: z-оценки (n)");
            AssertAll(Statistic.ZScores(values), [.. new Vector(values).ZNormalise()], 0, $"{Name(sample)}: Vector.ZNormalise");
        }
    }

    [Fact]
    public void MinMaxScale_MatchesFormula()
    {
        foreach (JsonElement sample in Samples)
        {
            AssertAll(Doubles(sample.GetProperty("minmax")), Statistic.MinMaxScale(Doubles(sample.GetProperty("values"))), 1e-12, $"{Name(sample)}: min-max");
        }
    }

    /// <summary>Одно правило нулевого разброса: постоянная выборка переходит в нули.</summary>
    [Fact]
    public void Scaling_OfConstantSample_IsZeros()
    {
        Assert.Equal([0.0, 0.0, 0.0], Statistic.ZScores([4.0, 4.0, 4.0]));
        Assert.Equal([0.0, 0.0, 0.0], Statistic.MinMaxScale([4.0, 4.0, 4.0]));
        Assert.Equal([0.0], Statistic.ZScores([4.0]));
        Assert.Empty(Statistic.ZScores(ReadOnlySpan<double>.Empty));

        double[] withGap = Statistic.MinMaxScale([1.0, double.NaN, 3.0]);

        Assert.Equal(0.0, withGap[0]);
        Assert.True(double.IsNaN(withGap[1]));
        Assert.Equal(1.0, withGap[2]);
    }

    [Fact]
    public void AverageRanks_MatchScipy()
    {
        foreach (JsonElement sample in Samples)
        {
            AssertAll(Doubles(sample.GetProperty("ranks_average")), Statistic.AverageRanks(Doubles(sample.GetProperty("values"))), 0, $"{Name(sample)}: ранги");
        }

        _ = Assert.Throws<ArgumentException>(() => Statistic.AverageRanks([1.0, double.NaN]));
    }

    // --- гистограмма ---

    [Fact]
    public void HistogramCounts_MatchNumpy_AndDensityFollows()
    {
        foreach (JsonElement item in s_data.Value.GetProperty("histograms").EnumerateArray())
        {
            double[] values = Doubles(item.GetProperty("values"));
            int bins = item.GetProperty("bins").GetInt32();

            (double[] edges, double[] counts) = Statistic.HistogramCounts(values, bins);

            AssertAll(Doubles(item.GetProperty("edges")), edges, 1e-13, $"{Name(item)}: границы");
            AssertAll(Doubles(item.GetProperty("counts")), counts, 0, $"{Name(item)}: счётчики");

            // Плотность экземпляра Statistic — те же счётчики, делённые на n·ширину
            double[] density = Doubles(item.GetProperty("density"));
            Histogramm histogram = new Statistic(new Vector(values)).Histogramm(bins);

            AssertAll(density, [.. histogram.Y], 1e-12, $"{Name(item)}: плотность");
        }
    }

    [Fact]
    public void HistogramCounts_Edges()
    {
        (double[] edges, double[] counts) = Statistic.HistogramCounts([7.0, 7.0, 7.0], 4);

        // Выборка без размаха: отрезок шириной 1 вокруг значения, как в numpy
        Assert.Equal([6.5, 6.75, 7.0, 7.25, 7.5], edges);
        Assert.Equal([0.0, 0.0, 3.0, 0.0], counts);
        Assert.Equal(3, Statistic.HistogramCounts([1.0, double.NaN, 2.0, 3.0], 2).Counts.Sum());
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Statistic.HistogramCounts([1.0], 0));
    }

    // --- расстояния ---

    [Fact]
    public void Distances_MatchScipy()
    {
        foreach (JsonElement pair in Pairs)
        {
            double[] x = Doubles(pair.GetProperty("x")), y = Doubles(pair.GetProperty("y"));

            AssertRelative(pair.GetProperty("euclidean").GetDouble(), AI.Distances.BaseDist.EuclideanDistance(x, y), 1e-13, $"{Name(pair)}: евклидово");
            AssertRelative(pair.GetProperty("rss").GetDouble(), AI.Distances.BaseDist.SquaredEuclidean(x, y), 1e-13, $"{Name(pair)}: квадрат расстояния");
            AssertClose(pair.GetProperty("cosine_similarity").GetDouble(), AI.Distances.BaseDist.CosineSimilarity(x, y), 1e-13, $"{Name(pair)}: косинус");

            var a = new Vector(x);
            var b = new Vector(y);

            Assert.Equal(AI.Distances.BaseDist.EuclideanDistance(x, y), AI.Distances.BaseDist.EuclideanDistance(a, b));
            Assert.Equal(AI.Distances.BaseDist.CosineSimilarity(x, y), AI.Distances.BaseDist.Cos(a, b), 15);
            Assert.Equal(-AI.Distances.BaseDist.Cos(a, b), AI.Distances.BaseDist.NegativeCosine(a, b));
        }
    }

    [Fact]
    public void Cosine_OfZeroVector_IsUndefined_AndCallerChooses()
    {
        Assert.True(double.IsNaN(AI.Distances.BaseDist.CosineSimilarity([0.0, 0.0], [1.0, 2.0])));
        Assert.Equal(0, AI.Distances.BaseDist.CosineSimilarity([0.0, 0.0], [1.0, 2.0], whenUndefined: 0));
        _ = Assert.Throws<ArgumentException>(() => AI.Distances.BaseDist.Cos(new Vector(0.0, 0.0), new Vector(1.0, 2.0)));
        _ = Assert.Throws<ArgumentException>(() => AI.Distances.BaseDist.CosineSimilarity([1.0], [1.0, 2.0]));
        _ = Assert.Throws<ArgumentException>(() => AI.Distances.BaseDist.EuclideanDistance([1.0], [1.0, 2.0]));
    }

    // --- матрица, тензор, массивы, ансамбль идут в ту же дисперсию ---

    [Fact]
    public void EveryContainer_UsesTheSameVariance()
    {
        double[] values = [3.0, 1.0, 4.0, 1.0, 5.0, 9.0];
        double sample = Statistic.CalcVariance(values), population = Statistic.CalcVariance(values, population: true);
        var matrix = new Matrix(2, 3);

        for (int i = 0; i < values.Length; i++) matrix.Data[i] = values[i];

        Assert.Equal(sample, matrix.Dispersion());
        Assert.Equal(population, matrix.Dispersion(population: true));
        Assert.Equal(Math.Sqrt(sample), matrix.Std());
        Assert.Equal(sample, AI.Extensions.StatisticsExtensions.Disp(values));
        Assert.Equal(sample, AI.Extensions.StatisticsExtensions.Disp(new[] { 3, 1, 4, 1, 5, 9 }));

        Vector[] ensemble = [new Vector(3.0, 10.0), new Vector(1.0, 10.0), new Vector(4.0, 10.0)];
        Vector spread = Statistic.EnsembleDispersion(ensemble);

        Assert.Equal(Statistic.CalcVariance([3.0, 1.0, 4.0]), spread[0]);
        Assert.Equal(0.0, spread[1]);

        // Компонента без разброса при нормировке ансамбля переходит в нули, а не в шум округления
        Vector[] scaled = Vector.ScaleData(ensemble);

        Assert.All(scaled, vector => Assert.Equal(0.0, vector[1]));
        AssertAll(Statistic.ZScores([3.0, 1.0, 4.0]), [scaled[0][0], scaled[1][0], scaled[2][0]], 0, "ScaleData");
        Assert.Equal([0.0, 0.0], [.. new Vector(5.0, 5.0).Minimax()]);
    }

    // --- нормальный генератор ---

    /// <summary>
    /// Последовательность определяется генератором, а не потоком: запас второй величины полярного
    /// метода хранится при самом экземпляре Random.
    /// </summary>
    [Fact]
    public void NextGaussian_SequenceDoesNotDependOnThread()
    {
        double[] expected = new double[9];
        var reference = new Random(123);

        for (int i = 0; i < expected.Length; i++) expected[i] = RandomEngine.NextGaussian(reference);

        var hopping = new Random(123);
        double[] actual = new double[9];

        // Каждое значение берётся на новом потоке: запас, привязанный к потоку, здесь терялся бы
        for (int i = 0; i < actual.Length; i++)
        {
            int index = i;
            var thread = new Thread(() => actual[index] = RandomEngine.NextGaussian(hopping));

            thread.Start();
            thread.Join();
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NextGaussian_TwoGenerators_DoNotShareSpare()
    {
        var first = new Random(1);
        var second = new Random(2);
        double a1 = RandomEngine.NextGaussian(first);

        _ = RandomEngine.NextGaussian(second);

        double a2 = RandomEngine.NextGaussian(first);
        var alone = new Random(1);

        Assert.Equal(a1, RandomEngine.NextGaussian(alone));
        Assert.Equal(a2, RandomEngine.NextGaussian(alone));
    }

    // --- служебное ---

    private static void AssertAll(double[] expected, double[] actual, double tolerance, string what)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++) AssertClose(expected[i], actual[i], tolerance, $"{what}[{i}]");
    }

    /// <summary>Относительное отклонение не больше допуска — для величин, далёких от единицы.</summary>
    private static void AssertRelative(double expected, double actual, double tolerance, string what) =>
        Assert.True(Math.Abs(actual - expected) <= tolerance * Math.Abs(expected), $"{what}: ожидалось {expected:R}, получено {actual:R}");


    internal static string Name(JsonElement item) => item.GetProperty("name").GetString()!;

    internal static double[] Doubles(JsonElement array) => [.. array.EnumerateArray().Select(value => value.GetDouble())];

    /// <summary>Относительное отклонение не больше допуска (для чисел по модулю меньше единицы — абсолютное).</summary>
    internal static void AssertClose(double expected, double actual, double tolerance, string what)
    {
        double error = Math.Abs(actual - expected) / Math.Max(1.0, Math.Abs(expected));

        Assert.True(error <= tolerance, $"{what}: ожидалось {expected:R}, получено {actual:R}");
    }
}

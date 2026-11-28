using System.Text.Json;
using AI.ClassicMath.AlgorithmAnalysis;
using AI.DataStructs.Algebraic;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Метрики качества против scikit-learn и statsmodels.
/// </summary>
/// <remarks>
/// Эталоны выгружает Tools/MathGolden/export_metrics_golden.py в Golden/metrics.json. Главный случай —
/// прогноз <c>2y + 10</c>: корреляция с фактом единица, а коэффициент детерминации −78. Пока под именем
/// R² жил квадрат корреляции, такой прогноз получал оценку «идеально».
/// </remarks>
public class MetricsGoldenTests
{
    private static readonly Lazy<JsonElement> s_data = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "metrics.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    });

    private static IEnumerable<JsonElement> Regression => s_data.Value.GetProperty("regression").EnumerateArray();

    [Fact]
    public void RSquared_MatchesSklearn()
    {
        foreach (JsonElement item in Regression)
        {
            double[] y = Doubles(item, "y"), pred = Doubles(item, "pred");

            AssertClose(item.GetProperty("r2").GetDouble(), MetricsForRegression.RSquared(y, pred), 1e-11, $"{Name(item)}: R²");
            Assert.Equal(MetricsForRegression.RSquared(y, pred), MetricsForRegression.RSquared(new Vector(y), new Vector(pred)));
        }
    }

    [Fact]
    public void RSquared_IsNotSquaredCorrelation()
    {
        JsonElement item = Regression.Single(x => Name(x) == "shifted_and_scaled");
        double[] y = Doubles(item, "y"), pred = Doubles(item, "pred");

        Assert.True(MetricsForRegression.RSquared(y, pred) < -70);
        Assert.Equal(1.0, MetricsForRegression.PearsonSquared(y, pred), 12);

#pragma warning disable CS0618 // Старое имя проверяется намеренно: оно обязано давать прежнее значение
        Assert.Equal(MetricsForRegression.PearsonSquared(y, pred), MetricsForRegression.R2(new Vector(y), new Vector(pred)));
#pragma warning restore CS0618
    }

    [Fact]
    public void PearsonSquared_MatchesNumpy()
    {
        foreach (JsonElement item in Regression)
        {
            if (!item.TryGetProperty("pearson_squared", out JsonElement expected) || Name(item) == "mean_prediction") continue;

            AssertClose(expected.GetDouble(), MetricsForRegression.PearsonSquared(Doubles(item, "y"), Doubles(item, "pred")), 1e-12, $"{Name(item)}: r²");
        }
    }

    [Fact]
    public void ErrorMetrics_MatchSklearn()
    {
        foreach (JsonElement item in Regression)
        {
            double[] y = Doubles(item, "y"), pred = Doubles(item, "pred");

            AssertClose(item.GetProperty("mae").GetDouble(), MetricsForRegression.MAE(y, pred), 1e-12, $"{Name(item)}: MAE");
            AssertClose(item.GetProperty("mse").GetDouble(), MetricsForRegression.MSE(y, pred), 1e-12, $"{Name(item)}: MSE");
            AssertClose(item.GetProperty("rmse").GetDouble(), MetricsForRegression.RMSE(y, pred), 1e-12, $"{Name(item)}: RMSE");
            AssertClose(item.GetProperty("mape").GetDouble(), MetricsForRegression.MAPE(y, pred), 1e-12, $"{Name(item)}: MAPE");
        }
    }

    /// <summary>Нулевой факт: делитель — машинный эпсилон, ошибка видна огромным значением (правило sklearn).</summary>
    [Fact]
    public void Mape_OnZeroTarget_IsHugeNotHidden()
    {
        Assert.True(MetricsForRegression.MAPE([0.0, 1.0], [0.5, 1.0]) > 1e14);
        Assert.Equal(0.0, MetricsForRegression.MAPE([0.0, 1.0], [0.0, 1.0]));
    }

    [Fact]
    public void Mase_MatchesDefinition()
    {
        foreach (JsonElement item in s_data.Value.GetProperty("mase").EnumerateArray())
        {
            double value = MetricsForRegression.MASE(
                Doubles(item, "actual"), Doubles(item, "forecast"), Doubles(item, "training"), item.GetProperty("period").GetInt32());

            AssertClose(item.GetProperty("value").GetDouble(), value, 1e-12, $"{Name(item)}: MASE");
        }

        // Наивный прогноз «как вчера» на самой обучающей выборке даёт ровно единицу
        double[] series = [3, 5, 4, 8, 7, 9];

        Assert.Equal(1.0, MetricsForRegression.MASE(series.AsSpan(1), series.AsSpan(0, 5), series), 12);
        Assert.True(double.IsNaN(MetricsForRegression.MASE([1.0], [1.0], [5.0, 5.0, 5.0])));
        Assert.True(double.IsNaN(MetricsForRegression.MASE([1.0], [1.0], [5.0])));
    }

    [Fact]
    public void Accuracy_MatchesSklearn()
    {
        foreach (JsonElement item in s_data.Value.GetProperty("classification").EnumerateArray())
        {
            int[] y = [.. item.GetProperty("y").EnumerateArray().Select(v => v.GetInt32())];
            int[] pred = [.. item.GetProperty("pred").EnumerateArray().Select(v => v.GetInt32())];

            Assert.Equal(item.GetProperty("accuracy").GetDouble(), MetricsForClassification.Accuracy(y, pred), 14);
        }
    }

    [Fact]
    public void McFadden_MatchesStatsmodels()
    {
        foreach (JsonElement item in s_data.Value.GetProperty("mcfadden").EnumerateArray())
        {
            double value = MetricsForRegression.McFaddenRSquared(
                item.GetProperty("log_likelihood").GetDouble(), item.GetProperty("null_log_likelihood").GetDouble());

            AssertClose(item.GetProperty("value").GetDouble(), value, 1e-13, $"{Name(item)}: псевдо-R²");
        }

        Assert.Equal(0, MetricsForRegression.McFaddenRSquared(-10, 0));
    }

    [Fact]
    public void Metrics_RejectDifferentLengths()
    {
        _ = Assert.Throws<ArgumentException>(() => MetricsForRegression.MAE([1.0, 2.0], [1.0]));
        _ = Assert.Throws<ArgumentException>(() => MetricsForRegression.RSquared([1.0, 2.0], [1.0]));
        _ = Assert.Throws<ArgumentException>(() => MetricsForRegression.MASE([1.0, 2.0], [1.0], [1.0, 2.0, 3.0]));
    }

    private static string Name(JsonElement item) => item.GetProperty("name").GetString()!;

    private static double[] Doubles(JsonElement item, string property) =>
        [.. item.GetProperty(property).EnumerateArray().Select(value => value.GetDouble())];

    private static void AssertClose(double expected, double actual, double tolerance, string what) =>
        Assert.True(
            Math.Abs(actual - expected) <= tolerance * Math.Max(1.0, Math.Abs(expected)),
            $"{what}: ожидалось {expected:R}, получено {actual:R}");
}

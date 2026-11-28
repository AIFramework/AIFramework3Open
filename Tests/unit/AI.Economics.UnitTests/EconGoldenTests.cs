using System.Text.Json;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Econometrics;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>
/// Сверка эконометрики с эталонами numpy и statsmodels.
/// </summary>
/// <remarks>
/// Значения выгружены скриптом Tools/EconGolden/export_econ_golden.py в Golden/econometrics.json;
/// Python при тестах не нужен.
/// </remarks>
public class EconGoldenTests
{
    private static readonly Lazy<JsonElement> s_data = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "econometrics.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    });

    public static TheoryData<string> Cases(string section)
    {
        var data = new TheoryData<string>();

        foreach (JsonElement item in s_data.Value.GetProperty(section).EnumerateArray())
            data.Add(item.GetProperty("name").GetString()!);

        return data;
    }

    // --- устойчивость VAR: numpy.linalg.eigvals сопровождающей матрицы ---

    [Theory]
    [MemberData(nameof(Cases), "var_stability")]
    public void Var_SpectralRadius_MatchesNumpyEigvals(string name)
    {
        JsonElement item = Case("var_stability", name);
        Matrix companion = VectorAutoregression.Companion(ToMatrix(item.GetProperty("coefficients")), item.GetProperty("order").GetInt32());

        Assert.Equal(item.GetProperty("spectral_radius").GetDouble(), Eigen.SpectralRadius(companion), 1e-10);

        double[][] expected = [.. item.GetProperty("eigenvalues").EnumerateArray().Select(Doubles)];
        System.Numerics.Complex[] actual = Eigen.General(companion);

        Assert.Equal(expected.Length, actual.Length);

        // Порядок сопряжённых пар у numpy и Eigen может отличаться: каждому эталонному числу ищется ближайшее
        foreach (double[] value in expected)
        {
            var reference = new System.Numerics.Complex(value[0], value[1]);

            Assert.True(actual.Min(z => System.Numerics.Complex.Abs(z - reference)) < 1e-10, $"нет собственного числа {reference}");
        }
    }

    /// <summary>
    /// Случай из разведки: степенной метод со стартом (1/2, 1/2) давал 0.30 и признавал систему
    /// устойчивой, потому что этот старт — собственный вектор меньшего корня.
    /// </summary>
    [Fact]
    public void Var_SymmetricUnstable_IsNotReportedStable()
    {
        Matrix companion = VectorAutoregression.Companion(new Matrix(new double[,] { { 0, 0.675, -0.375 }, { 0, -0.375, 0.675 } }), 1);

        Assert.Equal(1.05, Eigen.SpectralRadius(companion), 1e-12);
    }

    [Theory]
    [MemberData(nameof(Cases), "var_fit")]
    public void Var_Fit_SpectralRadius_MatchesNumpyLeastSquares(string name)
    {
        JsonElement item = Case("var_fit", name);
        double expected = item.GetProperty("spectral_radius").GetDouble();

        VarResult result = VectorAutoregression.Fit(ToMatrix(item.GetProperty("data")), item.GetProperty("order").GetInt32());

        Assert.Equal(expected, result.SpectralRadius, 1e-8);
        Assert.Equal(expected < 1, result.IsStable);
    }

    // --- интервалы прогноза: statsmodels UnobservedComponents при тех же дисперсиях ---

    [Theory]
    [MemberData(nameof(Cases), "state_space")]
    public void StateSpace_ForecastInterval_MatchesStatsmodels(string name)
    {
        JsonElement item = Case("state_space", name);
        StateSpaceModel model = Enum.Parse<StateSpaceModel>(item.GetProperty("model").GetString()!);
        int horizon = item.GetProperty("horizon").GetInt32();

        StateSpaceResult result = StateSpace.Smooth(
            new Vector(Doubles(item.GetProperty("series"))), model,
            item.GetProperty("observation_variance").GetDouble(),
            item.GetProperty("level_variance").GetDouble(),
            item.GetProperty("slope_variance").GetDouble(),
            horizon);

        double[] forecast = Doubles(item.GetProperty("forecast"));
        double[] standardError = Doubles(item.GetProperty("forecast_se"));

        for (int h = 0; h < horizon; h++)
        {
            double halfWidth = (result.ForecastUpper[h] - result.ForecastLower[h]) / 2;

            AssertRelative(forecast[h], result.Forecast[h], 1e-6);
            AssertRelative(1.96 * standardError[h], halfWidth, 1e-6);
        }
    }

    /// <summary>
    /// Неопределённость последнего состояния входит в интервал: на шаге 1 дисперсия прогноза
    /// равна <c>P_n + sigma_eta^2 + sigma_eps^2</c>, а не <c>sigma_eta^2 + sigma_eps^2</c>.
    /// </summary>
    [Fact]
    public void StateSpace_LocalLevel_FirstStepVariance_IncludesStateUncertainty()
    {
        JsonElement item = Case("state_space", "local_level");
        double observation = item.GetProperty("observation_variance").GetDouble();
        double level = item.GetProperty("level_variance").GetDouble();
        double stateVariance = item.GetProperty("filtered_state_variance").GetDouble();

        StateSpaceResult result = StateSpace.Smooth(
            new Vector(Doubles(item.GetProperty("series"))), StateSpaceModel.LocalLevel, observation, level, horizon: 3);

        for (int h = 0; h < 3; h++)
        {
            double halfWidth = (result.ForecastUpper[h] - result.ForecastLower[h]) / 2;
            double expected = 1.96 * Math.Sqrt(stateVariance + ((h + 1) * level) + observation);

            AssertRelative(expected, halfWidth, 1e-6);
            Assert.True(halfWidth > 1.96 * Math.Sqrt(((h + 1) * level) + observation) * 1.05, "интервал не шире формулы без P_n");
        }
    }

    [Fact]
    public void StateSpace_Fit_UsesTheSameForecastAsSmooth()
    {
        JsonElement item = Case("state_space", "local_linear_trend");
        var series = new Vector(Doubles(item.GetProperty("series")));

        StateSpaceResult fit = StateSpace.Fit(series, StateSpaceModel.LocalLinearTrend, horizon: 4);
        StateSpaceResult smooth = StateSpace.Smooth(
            series, StateSpaceModel.LocalLinearTrend, fit.ObservationVariance, fit.LevelVariance, fit.SlopeVariance, horizon: 4);

        Assert.Equal(smooth.ForecastUpper.ToArray(), fit.ForecastUpper.ToArray());
        Assert.Equal(smooth.LogLikelihood, fit.LogLikelihood);
    }

    [Fact]
    public void StateSpace_Smooth_RejectsNegativeVariance()
    {
        var series = new Vector(Enumerable.Range(0, 20).Select(i => (double)i).ToArray());

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => StateSpace.Smooth(series, StateSpaceModel.LocalLevel, -1, 0.1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => StateSpace.Smooth(series, StateSpaceModel.LocalLevel, 1, double.NaN));
    }

    private static void AssertRelative(double expected, double actual, double tolerance) =>
        Assert.True(
            Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected)),
            $"ожидалось {expected:R}, получено {actual:R}");

    private static JsonElement Case(string section, string name) =>
        s_data.Value.GetProperty(section).EnumerateArray().Single(item => item.GetProperty("name").GetString() == name);

    private static double[] Doubles(JsonElement array) => [.. array.EnumerateArray().Select(value => value.GetDouble())];

    private static Matrix ToMatrix(JsonElement rows)
    {
        double[][] values = [.. rows.EnumerateArray().Select(Doubles)];
        var matrix = new Matrix(values.Length, values[0].Length);

        for (int i = 0; i < values.Length; i++)
            for (int j = 0; j < values[i].Length; j++)
                matrix[i, j] = values[i][j];

        return matrix;
    }
}

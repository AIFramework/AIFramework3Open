using AI.Script.Hosting;

namespace AI.Script.UnitTests;

/// <summary>
/// Функции <c>stat</c>, <c>mat</c>, <c>math</c> и <c>signal</c> считают ядром фреймворка.
/// </summary>
/// <remarks>
/// Ожидаемые значения — numpy и pandas на тех же данных (в комментариях у каждого числа).
/// Проверяется привязка: что функция скрипта отдаёт ответ общего ядра, а не своей копии формулы,
/// и что правила по умолчанию те, что решил владелец: дисперсия выборочная, квантиль типа 7,
/// гистограмма счётчиками.
/// </remarks>
public sealed class StatCanonTests
{
    private static double Number(RunResult result, string name) => (double)result.Emitted[name]!;

    private static double[] Numbers(RunResult result, string name) =>
        [.. ((IEnumerable<double>)result.Emitted[name]!)];

    [Fact]
    public void Variance_IsSampleByDefault()
    {
        RunResult result = Script.RunOk("""
            let v = <3, 1, 4, 1, 5, 9, 2, 6, 5, 3.5>

            emit var = stat.var(v)
            emit std = stat.std(v)
            """);

        // numpy.var(v, ddof=1) = 6.025; pandas.Series(v).var() — то же
        Assert.Equal(6.025, Number(result, "var"), 12);
        Assert.Equal(Math.Sqrt(6.025), Number(result, "std"), 12);
    }

    [Fact]
    public void Median_OfEvenCount_IsMeanOfTwoMiddle()
    {
        RunResult result = Script.RunOk("""
            emit even = stat.median(<1, 2, 3, 4>)
            emit odd = stat.median(<5, 1, 3>)
            emit ten = stat.median(<3, 1, 4, 1, 5, 9, 2, 6, 5, 3.5>)
            """);

        Assert.Equal(2.5, Number(result, "even"));
        Assert.Equal(3.0, Number(result, "odd"));

        // numpy.median = 3.75
        Assert.Equal(3.75, Number(result, "ten"), 12);
    }

    [Fact]
    public void Quantile_IsType7()
    {
        RunResult result = Script.RunOk("""
            let v = vec.of(range(11) |> core.map(i => i + 1))

            emit q25 = stat.quantile(v, q: 0.25)
            emit q75 = stat.quantile(v, q: 0.75)
            emit q90 = stat.quantile(v, q: 0.9)
            """);

        // numpy.quantile(arange(1, 12), [0.25, 0.75, 0.9]) = 3.5, 8.5, 10.0
        Assert.Equal(3.5, Number(result, "q25"), 12);
        Assert.Equal(8.5, Number(result, "q75"), 12);
        Assert.Equal(10.0, Number(result, "q90"), 12);
    }

    [Fact]
    public void Hist_IsCounts_DensityByParameter()
    {
        RunResult result = Script.RunOk("""
            let v = <1, 2, 2, 2, 3, 7, 7, 10>
            let h = stat.hist(v, bins: 4)
            let d = stat.hist(v, bins: 4, density: true)

            emit edges = h.edges
            emit counts = h.counts
            emit density = d.counts
            """);

        // numpy.histogram(v, bins=4): границы 1, 3.25, 5.5, 7.75, 10; счётчики 5, 0, 2, 1
        Assert.Equal([1.0, 3.25, 5.5, 7.75, 10.0], Numbers(result, "edges"));
        Assert.Equal([5.0, 0.0, 2.0, 1.0], Numbers(result, "counts"));

        // density=True: счётчик / (8 · 2.25)
        double[] density = Numbers(result, "density");

        Assert.Equal(5.0 / 18.0, density[0], 12);
        Assert.Equal(1.0 / 18.0, density[3], 12);
    }

    [Fact]
    public void Scaling_OfConstantData_IsZeros_Everywhere()
    {
        RunResult result = Script.RunOk("""
            let m = mat.from_rows([<5, 1>, <5, 2>, <5, 6>])

            emit z = stat.zscore(<4, 4, 4>)
            emit mm = stat.minmax(<4, 4, 4>)
            emit mz = mat.zscore(m)[0, 0]
            emit mmm = mat.minmax(m)[2, 0]
            emit column = mat.zscore(m)[2, 1]
            """);

        Assert.Equal([0.0, 0.0, 0.0], Numbers(result, "z"));
        Assert.Equal([0.0, 0.0, 0.0], Numbers(result, "mm"));
        Assert.Equal(0.0, Number(result, "mz"));
        Assert.Equal(0.0, Number(result, "mmm"));

        // scipy.stats.zscore([1, 2, 6], ddof=1)[2] = 1.1338934190276817
        Assert.Equal(1.1338934190276817, Number(result, "column"), 12);
    }

    [Fact]
    public void Corr_MatchesNumpy_AndIsZeroWithoutSpread()
    {
        RunResult result = Script.RunOk("""
            emit r = stat.corr(<1, 2, 4>, <2, 1, 5>)
            emit flat = stat.corr(<1, 2, 3>, <5, 5, 5>)
            """);

        // numpy.corrcoef([1, 2, 4], [2, 1, 5])[0, 1] = 0.8386278693775346
        Assert.Equal(0.8386278693775346, Number(result, "r"), 12);
        Assert.Equal(0.0, Number(result, "flat"));
    }

    // --- метрики ---

    /// <summary>
    /// R² — доля объяснённого разброса, а не квадрат корреляции: прогноз 2y + 10 коррелирует с фактом
    /// идеально и при этом никуда не годится.
    /// </summary>
    [Fact]
    public void R2_IsOneMinusResidualOverTotal()
    {
        RunResult result = Script.RunOk("""
            let y = <1, 2, 3, 4>

            emit exact = stat.r2(y, y)
            emit mean = stat.r2(y, <2.5, 2.5, 2.5, 2.5>)
            emit reversed = stat.r2(y, <4, 3, 2, 1>)
            emit shifted = stat.r2(y, 2 * y + 10)
            emit corr = stat.corr(y, 2 * y + 10)
            """);

        // sklearn.metrics.r2_score: 1, 0, -3, -125
        Assert.Equal(1.0, Number(result, "exact"));
        Assert.Equal(0.0, Number(result, "mean"), 12);
        Assert.Equal(-3.0, Number(result, "reversed"), 12);
        Assert.Equal(-125.0, Number(result, "shifted"), 10);
        Assert.Equal(1.0, Number(result, "corr"), 12);
    }

    [Fact]
    public void ErrorMetrics_MatchSklearn()
    {
        RunResult result = Script.RunOk("""
            let y = <0, 1, 2, 4>
            let p = <0.5, 1, 1.5, 5>

            emit mae = stat.mae(y, p)
            emit rmse = stat.rmse(y, p)
            emit mse = stat.mse(y, p)
            emit mape = stat.mape(<1, 2, 4>, <1, 1.5, 5>)
            emit constant = stat.r2(<3, 3, 3>, <3, 3, 3>)
            """);

        // sklearn: MAE 0.5, MSE 0.375, RMSE 0.6123724356957945; MAPE (0 + 0.25 + 0.25) / 3
        Assert.Equal(0.5, Number(result, "mae"), 12);
        Assert.Equal(0.375, Number(result, "mse"), 12);
        Assert.Equal(0.6123724356957945, Number(result, "rmse"), 12);
        Assert.Equal(0.5 / 3, Number(result, "mape"), 12);

        // Постоянный факт и точный прогноз: 1, как в sklearn (раньше 0)
        Assert.Equal(1.0, Number(result, "constant"));
    }

    /// <summary>Один генератор на <c>math.gauss</c> и <c>signal.noise</c>: при одном зерне первые числа совпадают.</summary>
    [Fact]
    public void Gauss_AndNoise_ShareOneGenerator_AndFollowSeed()
    {
        const string single = "options { seed: 7 }\nemit g = math.gauss()";
        const string noise = "options { seed: 7 }\nemit n = signal.noise(3, sigma: 1)";

        double first = Number(Script.RunOk(single), "g");

        Assert.Equal(first, Number(Script.RunOk(single), "g"));
        Assert.Equal(first, Numbers(Script.RunOk(noise), "n")[0]);
    }

    [Fact]
    public void Noise_HasRequestedMoments()
    {
        RunResult result = Script.RunOk("""
            options { seed: 3 }

            let n = signal.noise(20000, sigma: 2, mean: 5)

            emit mean = stat.mean(n)
            emit std = stat.std(n)
            """);

        Assert.InRange(Number(result, "mean"), 4.95, 5.05);
        Assert.InRange(Number(result, "std"), 1.96, 2.04);
    }
}

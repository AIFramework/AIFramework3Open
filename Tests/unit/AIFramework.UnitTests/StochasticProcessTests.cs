using AI.Simulation.Stochastic;
using AI.Simulation.SystemDynamics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Процесс Орнштейна — Уленбека и запасы с границами и шумом. Точный переход процесса и метод
/// Эйлера — Маруямы в модели запасов проверяют друг друга, а моменты длинной траектории, найденные по
/// самой траектории, сверяются с замкнутыми формулами σ²/(2θ) и e^(−θτ).
/// </summary>
public class StochasticProcessTests
{
    [Fact]
    public void OrnsteinUhlenbeck_LongPath_HasStationaryVarianceAndAutocorrelation()
    {
        var process = new OrnsteinUhlenbeckProcess(mean: 1.0, reversionRate: 0.5, volatility: 0.8);
        const double step = 0.05;
        double[] path = process.Path(process.Mean, step, 200_000, new Random(11));

        double mean = path.Average();
        double variance = path.Sum(x => (x - mean) * (x - mean)) / (path.Length - 1);

        // Время корреляции 1/θ = 2, наблюдение 10 000: около 2 500 независимых отсчётов, ошибка дисперсии ~3 %
        Assert.Equal(process.StationaryVariance, variance, process.StationaryVariance * 0.1);
        Assert.Equal(0.64, process.StationaryVariance, 12);
        Assert.Equal(process.Mean, mean, 0.05);

        foreach (int lagSteps in new[] { 10, 20, 40 })
        {
            double covariance = 0;

            for (int k = 0; k + lagSteps < path.Length; k++)
                covariance += (path[k] - mean) * (path[k + lagSteps] - mean);

            double correlation = covariance / (path.Length - lagSteps) / variance;
            Assert.Equal(process.Autocorrelation(lagSteps * step), correlation, 0.04);
        }
    }

    [Fact]
    public void StockFlowModel_EulerMaruyama_MatchesExactOrnsteinUhlenbeckTransition()
    {
        var process = new OrnsteinUhlenbeckProcess(mean: 1.0, reversionRate: 0.5, volatility: 0.8);
        const double start = 5.0, horizon = 2.0;
        const int runs = 4_000;
        var random = new Random(3);
        var finals = new double[runs];

        for (int r = 0; r < runs; r++)
        {
            StockFlowModel model = new StockFlowModel().AddStock("x", start,
                (_, levels) => process.ReversionRate * (process.Mean - levels[0]),
                double.NegativeInfinity, double.PositiveInfinity, (_, _) => process.Volatility);
            finals[r] = model.Run(horizon, random, points: 2, stepsPerInterval: 400)[^1].Levels[0];
        }

        double mean = finals.Average();
        double variance = finals.Sum(x => (x - mean) * (x - mean)) / (runs - 1);

        // Слабый порядок Эйлера — Маруямы 1: при 400 шагах смещение моментов ~0,1 %, разброс выборки больше
        Assert.Equal(process.ExpectedValue(start, horizon), mean, 4 * Math.Sqrt(process.Variance(horizon) / runs));
        Assert.Equal(process.Variance(horizon), variance, process.Variance(horizon) * 0.07);
    }

    [Fact]
    public void OrnsteinUhlenbeck_FromRelaxation_AndTimeToExpected_AgreeWithIntegratedDrift()
    {
        var process = OrnsteinUhlenbeckProcess.FromRelaxation(mean: 2, relaxationTime: 10, stationaryDeviation: 1.5);

        Assert.Equal(0.1, process.ReversionRate, 12);
        Assert.Equal(1.5, process.StationaryDeviation, 12);
        Assert.Equal(10 * Math.Log(2), process.HalfLife, 12);

        // Ожидание от 7 доходит до 3 за 10·ln(5/1); уровень за средним и уровень «назад» недостижимы
        double time = process.TimeToExpected(7, 3);
        Assert.Equal(10 * Math.Log(5), time, 10);
        Assert.Equal(double.PositiveInfinity, process.TimeToExpected(7, 1.5));
        Assert.Equal(double.PositiveInfinity, process.TimeToExpected(7, 8));

        // Тот же снос, проинтегрированный Рунге — Куттой без шума, приходит в 3 к тому же моменту
        StockFlowModel model = new StockFlowModel().AddStock("x", 7, (_, levels) => -(levels[0] - 2) / 10);
        Assert.Equal(3, model.Run(time, points: 2, stepsPerInterval: 200)[^1].Levels[0], 8);
    }

    [Fact]
    public void BoundedStock_LinearDecline_StopsAtFloor_AndReturnsInsideWhenFlowTurns()
    {
        // «Раздражение 7 из 10 спадает на 0,2 в сутки»: без границы через 50 суток было бы −3
        StockFlowModel declining = new StockFlowModel().AddStock("раздражение", 7, (_, _) => -0.2, lower: 0, upper: 10);
        IReadOnlyList<SystemState> states = declining.Run(50, points: 51, stepsPerInterval: 10);

        Assert.Equal(3, states[20].Levels[0], 9);
        // На 35-е сутки уровень доходит до нуля с точностью округления, дальше прижат к нему точно
        Assert.Equal(0, states[35].Levels[0], 9);
        Assert.All(states.Skip(36), state => Assert.Equal(0, state.Levels[0]));

        StockFlowModel rising = new StockFlowModel().AddStock("x", 9, (_, _) => 1, lower: 0, upper: 10);
        Assert.Equal(10, rising.Run(5, points: 6)[^1].Levels[0]);

        // На границе отсекается только поток наружу: возврат к фону 2 со дна идёт как обычно
        StockFlowModel relaxing = new StockFlowModel().AddStock("x", 0, (_, levels) => -(levels[0] - 2) / 5, lower: 0, upper: 10);
        Assert.Equal(2 * (1 - Math.Exp(-1)), relaxing.Run(5, points: 2, stepsPerInterval: 200)[^1].Levels[0], 8);
    }

    [Fact]
    public void BoundedNoisyStock_NeverLeavesItsScale()
    {
        var random = new Random(5);
        StockFlowModel model = new StockFlowModel().AddStock("настроение", 0.5,
            (_, levels) => -(levels[0] - 1) / 2, lower: 0, upper: 10, noise: (_, _) => 3);

        IReadOnlyList<SystemState> states = model.Run(200, random, points: 2001, stepsPerInterval: 5);

        Assert.All(states, state => Assert.InRange(state.Levels[0], 0, 10));
        Assert.Contains(states, state => state.Levels[0] == 0);
    }

    [Fact]
    public void StockFlowModel_WithoutNoiseOrGenerator_KeepsDeterministicBehaviour()
    {
        StockFlowModel decay = new StockFlowModel().AddStock("x", 5, (_, levels) => -0.3 * levels[0]);
        double deterministic = decay.Run(4, points: 2, stepsPerInterval: 100)[^1].Levels[0];

        Assert.Equal(5 * Math.Exp(-1.2), deterministic, 9);
        Assert.False(decay.IsStochastic);
        Assert.Equal(deterministic, decay.Run(4, new Random(1), points: 2, stepsPerInterval: 100)[^1].Levels[0]);

        // С шумом, но без генератора считается ожидаемый снос
        StockFlowModel noisy = new StockFlowModel().AddStock("x", 5, (_, levels) => -0.3 * levels[0],
            double.NegativeInfinity, double.PositiveInfinity, (_, _) => 1);
        Assert.True(noisy.IsStochastic);
        Assert.Equal(deterministic, noisy.Run(4, points: 2, stepsPerInterval: 100)[^1].Levels[0], 12);

        Assert.Throws<ArgumentOutOfRangeException>(() => new StockFlowModel().AddStock("x", 12, (_, _) => 0, 0, 10));
    }
}

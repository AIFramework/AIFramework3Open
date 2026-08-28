using AI.DataStructs.Algebraic;
using AI.MathUtils.ODE;
using AI.Solvers.Chem.Kinetics;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Интегрирование кинетических схем, подгонка констант, Аррениус, тепловой разгон.</summary>
public class KineticsTests
{
    private static readonly NonlinearFitOptions FastFit = new() { AnnealingIterations = 60 };

    /// <summary>Векторный решатель фреймворка: dy/dt = -y даёт экспоненту.</summary>
    [Fact]
    public void SolveSystem_IntegratesExponentialDecay()
    {
        double[] times = { 0, 0.5, 1, 2, 5 };

        Vector[] solution = RungeKutta.SolveSystem(
            (_, y) => new Vector(-y[0]),
            0,
            new Vector(1.0),
            times);

        for (int i = 0; i < times.Length; i++)
        {
            double exact = Math.Exp(-times[i]);
            Assert.Equal(exact, solution[i][0], exact * 1e-4);
        }

        // Метод четвёртого порядка: учащение сетки резко повышает точность
        Vector[] fine = RungeKutta.SolveSystem(
            (_, y) => new Vector(-y[0]),
            0,
            new Vector(1.0),
            times,
            stepsPerInterval: 200);

        Assert.Equal(Math.Exp(-5), fine[^1][0], Math.Exp(-5) * 1e-8);
    }

    [Fact]
    public void FirstOrderScheme_MatchesAnalyticalSolution()
    {
        var scheme = KineticScheme.Simple();
        double[] times = { 0, 1, 5, 10, 20 };

        double[] a = scheme.SimulateSpecies("A", new[] { 1.0, 0.0 }, new[] { 0.1 }, times);

        for (int i = 0; i < times.Length; i++)
            Assert.Equal(Math.Exp(-0.1 * times[i]), a[i], 1e-8);
    }

    [Fact]
    public void FirstOrderScheme_ConservesMass()
    {
        var scheme = KineticScheme.Simple();
        Vector[] states = scheme.Simulate(new[] { 1.0, 0.0 }, new[] { 0.3 }, new[] { 0.0, 1.0, 5.0 });

        foreach (Vector state in states)
            Assert.Equal(1.0, state[0] + state[1], 1e-8);
    }

    /// <summary>Реакция второго порядка при равных концентрациях: 1/c = 1/c0 + kt.</summary>
    [Fact]
    public void BimolecularScheme_MatchesAnalyticalSolution()
    {
        var scheme = KineticScheme.Bimolecular();
        double[] times = { 0, 10, 30, 60 };

        double[] a = scheme.SimulateSpecies("A", new[] { 1.0, 1.0, 0.0 }, new[] { 0.02 }, times);

        for (int i = 0; i < times.Length; i++)
            Assert.Equal(1.0 / (1 + (0.02 * times[i])), a[i], 1e-7);
    }

    [Fact]
    public void Fit_RecoversRateConstant()
    {
        var scheme = KineticScheme.Simple();
        double[] times = Enumerable.Range(0, 13).Select(i => i * 5.0).ToArray();
        // Небольшой детерминированный шум: на точных данных доверительный интервал вырождается в точку
        double[] measured = times.Select((t, i) => Math.Exp(-0.05 * t) + (0.002 * Math.Sin(i))).ToArray();

        var data = new KineticDataset
        {
            Times = times,
            Initial = new[] { 1.0, 0.0 },
            Measurements = new Dictionary<string, double[]> { ["A"] = measured }
        };

        KineticFitResult fit = KineticFit.Fit(scheme, data, null, FastFit);

        Assert.Equal(0.05, fit.RateConstants[0], 2e-3);
        Assert.True(fit.R2 > 0.9999, $"R2 = {fit.R2:F6}");
        Assert.True(fit.Intervals[0].Lower < 0.05 && fit.Intervals[0].Upper > 0.05,
            $"интервал [{fit.Intervals[0].Lower:G4}; {fit.Intervals[0].Upper:G4}] не накрывает истинное значение");
    }

    /// <summary>Две константы последовательной схемы разделяются по кривой промежуточного продукта.</summary>
    [Fact]
    public void Fit_RecoversConsecutiveRateConstants()
    {
        var scheme = KineticScheme.Consecutive();
        double[] times = Enumerable.Range(0, 16).Select(i => i * 2.0).ToArray();
        double[] truth = { 0.3, 0.1 };

        Vector[] exact = scheme.Simulate(new[] { 1.0, 0.0, 0.0 }, truth, times);

        var data = new KineticDataset
        {
            Times = times,
            Initial = new[] { 1.0, 0.0, 0.0 },
            Measurements = new Dictionary<string, double[]>
            {
                ["A"] = exact.Select(s => s[0]).ToArray(),
                ["B"] = exact.Select(s => s[1]).ToArray()
            }
        };

        KineticFitResult fit = KineticFit.Fit(scheme, data, new[] { 0.5, 0.05 }, FastFit);

        Assert.Equal(0.3, fit.RateConstants[0], 5e-3);
        Assert.Equal(0.1, fit.RateConstants[1], 5e-3);
    }

    [Fact]
    public void DetermineOrder_FindsSecondOrder()
    {
        double[] times = Enumerable.Range(0, 13).Select(i => i * 5.0).ToArray();
        double[] concentrations = times.Select(t => 1.0 / (1 + (0.02 * t))).ToArray();

        ReactionOrderResult result = KineticFit.DetermineOrder(times, concentrations, null, FastFit);

        Assert.Equal(2.0, result.Order, 1e-9);
        Assert.Equal(0.02, result.RateConstant, 1e-3);
    }

    [Fact]
    public void DetermineOrder_FindsFirstOrder()
    {
        double[] times = Enumerable.Range(0, 13).Select(i => i * 5.0).ToArray();
        double[] concentrations = times.Select(t => Math.Exp(-0.05 * t)).ToArray();

        ReactionOrderResult result = KineticFit.DetermineOrder(times, concentrations, null, FastFit);

        Assert.Equal(1.0, result.Order, 1e-9);
    }

    [Fact]
    public void Fit_RejectsInconsistentData()
    {
        var scheme = KineticScheme.Simple();
        var data = new KineticDataset
        {
            Times = new[] { 0.0, 1.0, 2.0 },
            Initial = new[] { 1.0 },
            Measurements = new Dictionary<string, double[]> { ["A"] = new[] { 1.0, 0.5, 0.2 } }
        };

        Assert.Throws<ArgumentException>(() => KineticFit.Fit(scheme, data, null, FastFit));
    }

    [Fact]
    public void Fit_RejectsUnknownSpecies()
    {
        var scheme = KineticScheme.Simple();
        var data = new KineticDataset
        {
            Times = new[] { 0.0, 1.0, 2.0 },
            Initial = new[] { 1.0, 0.0 },
            Measurements = new Dictionary<string, double[]> { ["Z"] = new[] { 1.0, 0.5, 0.2 } }
        };

        Assert.Throws<ArgumentException>(() => KineticFit.Fit(scheme, data, null, FastFit));
    }

    /// <summary>Энергия активации восстанавливается по синтетической зависимости k(T).</summary>
    [Fact]
    public void Arrhenius_RecoversActivationEnergy()
    {
        const double ea = 80, factor = 1e12;
        double[] temperatures = { 300, 310, 320, 330, 340 };

        double[] rateConstants = temperatures
            .Select(t => factor * Math.Exp(-ea * 1000 / (ArrheniusAnalysis.GasConstant * t)))
            .ToArray();

        ArrheniusResult result = ArrheniusAnalysis.Fit(temperatures, rateConstants);

        Assert.Equal(ea, result.ActivationEnergy, 1e-6);
        Assert.Equal(factor, result.PreExponentialFactor, factor * 1e-6);
        Assert.Equal(1.0, result.R2, 1e-9);
        Assert.True(result.ActivationEnergyInterval.Lower <= ea && result.ActivationEnergyInterval.Upper >= ea);
    }

    [Fact]
    public void Arrhenius_PredictsRateConstant()
    {
        double[] temperatures = { 300, 310, 320, 330 };
        double[] rateConstants = temperatures
            .Select(t => 1e12 * Math.Exp(-80000 / (ArrheniusAnalysis.GasConstant * t)))
            .ToArray();

        ArrheniusResult result = ArrheniusAnalysis.Fit(temperatures, rateConstants);

        Assert.Equal(rateConstants[0], result.RateConstantAt(300), rateConstants[0] * 1e-6);
        Assert.True(result.AccelerationFactor(300, 10) > 1);
    }

    [Fact]
    public void ArrheniusFromTwoPoints_MatchesDefinition()
    {
        double k1 = 1e12 * Math.Exp(-80000 / (ArrheniusAnalysis.GasConstant * 300));
        double k2 = 1e12 * Math.Exp(-80000 / (ArrheniusAnalysis.GasConstant * 320));

        Assert.Equal(80.0, ArrheniusAnalysis.ActivationEnergyFromTwoPoints(300, k1, 320, k2), 1e-6);
    }

    [Theory]
    [InlineData(new[] { 300.0, 310.0 }, new[] { 1.0, 2.0 })]
    [InlineData(new[] { 300.0, 310.0, 320.0 }, new[] { 1.0, 2.0 })]
    public void Arrhenius_RejectsBadInput(double[] temperatures, double[] rateConstants)
        => Assert.Throws<ArgumentException>(() => ArrheniusAnalysis.Fit(temperatures, rateConstants));

    private static RunawayParameters Runaway() => new()
    {
        ReactionHeat = 800_000,
        HeatCapacity = 1800,
        ActivationEnergy = 120,
        PreExponentialFactor = 1e13,
        InitialTemperature = 350
    };

    [Fact]
    public void AdiabaticRise_IsHeatOverHeatCapacity()
        => Assert.Equal(800_000.0 / 1800, Runaway().AdiabaticTemperatureRise, 1e-9);

    [Fact]
    public void TimeToMaximumRate_MatchesClassicFormula()
    {
        RunawayParameters parameters = Runaway();
        double heatRate = parameters.HeatReleaseRate(parameters.InitialTemperature);

        double expected = parameters.HeatCapacity * ArrheniusAnalysis.GasConstant
            * parameters.InitialTemperature * parameters.InitialTemperature
            / (heatRate * parameters.ActivationEnergy * 1000);

        Assert.Equal(expected, ThermalRunaway.TimeToMaximumRateEstimate(parameters), expected * 1e-9);
    }

    /// <summary>
    /// В адиабатическом режиме температура поднимается на весь тепловой эффект,
    /// а момент максимальной скорости лежит рядом с аналитической оценкой.
    /// </summary>
    [Fact]
    public void Simulation_ReachesAdiabaticRise()
    {
        RunawayParameters parameters = Runaway();
        double estimate = ThermalRunaway.TimeToMaximumRateEstimate(parameters);

        RunawayResult result = ThermalRunaway.Simulate(parameters, estimate * 5);

        Assert.True(result.RunawayWithinWindow, "разгон должен состояться внутри окна");
        Assert.Equal(parameters.InitialTemperature + parameters.AdiabaticTemperatureRise,
            result.MaximumTemperature, 1.0);
        Assert.InRange(result.TimeToMaximumRate, estimate * 0.3, estimate * 3);
    }

    [Fact]
    public void Simulation_ReportsNoRunawayWhenColdEnough()
    {
        var cold = new RunawayParameters
        {
            ReactionHeat = 800_000,
            HeatCapacity = 1800,
            ActivationEnergy = 120,
            PreExponentialFactor = 1e13,
            InitialTemperature = 250
        };

        RunawayResult result = ThermalRunaway.Simulate(cold, 3600);

        Assert.False(result.RunawayWithinWindow);
        Assert.Equal(250, result.MaximumTemperature, 0.5);
    }

    /// <summary>Показатель T_D24: при найденной температуре время до разгона равно суткам.</summary>
    [Fact]
    public void TemperatureForTimeToMaximumRate_InvertsTheEstimate()
    {
        RunawayParameters parameters = Runaway();
        double temperature = ThermalRunaway.TemperatureForTimeToMaximumRate(parameters);

        Assert.True(temperature < parameters.InitialTemperature,
            "суточный запас достигается при более низкой температуре");
        Assert.Equal(24 * 3600.0, ThermalRunaway.TimeToMaximumRateEstimate(parameters, temperature), 60.0);
    }

    [Fact]
    public void Scheme_RejectsUndeclaredSpecies()
        => Assert.Throws<ArgumentException>(() => new KineticScheme(
            new[] { "A" },
            new[]
            {
                new ReactionStep
                {
                    Reactants = new Dictionary<string, double> { ["A"] = 1 },
                    Products = new Dictionary<string, double> { ["B"] = 1 }
                }
            }));

    [Fact]
    public void Scheme_DescribesItself()
    {
        string text = KineticScheme.Consecutive().ToString();

        Assert.Contains("A -> B", text, StringComparison.Ordinal);
        Assert.Contains("B -> C", text, StringComparison.Ordinal);
    }
}

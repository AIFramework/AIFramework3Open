using AI.Algorithms.NetworkFlow;
using AI.Biology.Networks;
using AI.Solvers.Chem.Kinetics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Сетевые модели проверяются задачами с известным ответом и независимыми эталонами: аттракторы
/// булевых сетей — прямым моделированием из каждого состояния, балансовый анализ потоков —
/// максимальным потоком по алгоритму Диница на той же сети, метод Гиллеспи — распределением
/// Пуассона, числом пар при димеризации и уравнениями скорости для линейной схемы.
/// </summary>
public class BiologicalNetworksTests
{
    #region Булевы сети

    [Fact]
    public void ToggleSwitch_HasTwoFixedPointsAndOneTwoCycle()
    {
        var network = new BooleanNetwork(["A", "B"]);
        network.SetRule("A", "!B");
        network.SetRule("B", "NOT A");

        IReadOnlyList<BooleanAttractor> attractors = network.Attractors();

        Assert.Equal(["01", "10"], network.FixedPoints().Select(s => BooleanNetwork.Format(s)).OrderBy(s => s));

        BooleanAttractor cycle = Assert.Single(attractors, a => !a.IsFixedPoint);
        Assert.Equal(["00", "11"], cycle.States.Select(s => BooleanNetwork.Format(s)).OrderBy(s => s));
        Assert.Equal(4, attractors.Sum(a => a.BasinSize));
    }

    [Fact]
    public void Repressilator_OscillatesWithPeriodSix()
    {
        // Кольцо из трёх репрессоров: A подавляет B, B — C, C — A
        var network = new BooleanNetwork(["A", "B", "C"]);
        network.SetRule("A", "!C");
        network.SetRule("B", "!A");
        network.SetRule("C", "!B");

        IReadOnlyList<BooleanAttractor> attractors = network.Attractors();

        Assert.Empty(network.FixedPoints());
        Assert.Equal([2, 6], attractors.Select(a => a.Length).OrderBy(l => l));
        Assert.Equal([2, 6], attractors.Select(a => a.BasinSize).OrderBy(b => b));
    }

    [Fact]
    public void Attractors_AgreeWithDirectSimulation()
    {
        var rng = new Random(81);

        for (int trial = 0; trial < 20; trial++)
        {
            int n = 3 + rng.Next(6);
            string[] genes = Enumerable.Range(0, n).Select(i => $"g{i}").ToArray();
            var network = new BooleanNetwork(genes);

            foreach (string gene in genes)
            {
                string[] inputs = genes.OrderBy(_ => rng.Next()).Take(1 + rng.Next(3)).ToArray();
                bool[] table = Enumerable.Range(0, 1 << inputs.Length).Select(_ => rng.Next(2) == 1).ToArray();

                network.SetRule(gene, value =>
                {
                    int row = 0;

                    for (int k = 0; k < inputs.Length; k++)
                        row |= value(inputs[k]) ? 1 << k : 0;

                    return table[row];
                });
            }

            // Эталон: из каждого состояния шагаем, пока состояние не повторится
            var expected = new Dictionary<string, int>();

            for (int code = 0; code < 1 << n; code++)
            {
                bool[] state = Enumerable.Range(0, n).Select(i => ((code >> i) & 1) == 1).ToArray();
                var seen = new Dictionary<string, int>();
                var path = new List<string>();

                while (!seen.ContainsKey(BooleanNetwork.Format(state)))
                {
                    seen[BooleanNetwork.Format(state)] = path.Count;
                    path.Add(BooleanNetwork.Format(state));
                    state = network.Step(state);
                }

                string key = string.Join("|", path.Skip(seen[BooleanNetwork.Format(state)]).OrderBy(s => s, StringComparer.Ordinal));
                expected[key] = expected.GetValueOrDefault(key) + 1;
            }

            IReadOnlyList<BooleanAttractor> attractors = network.Attractors();
            var actual = attractors.ToDictionary(
                a => string.Join("|", a.States.Select(s => BooleanNetwork.Format(s)).OrderBy(s => s, StringComparer.Ordinal)),
                a => a.BasinSize);

            Assert.Equal(expected.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));

            foreach (BooleanAttractor attractor in attractors)
            {
                for (int k = 0; k < attractor.Length; k++)
                    Assert.Equal(attractor.States[(k + 1) % attractor.Length], network.Step(attractor.States[k]));
            }
        }
    }

    [Fact]
    public void Formula_FollowsPrecedence_AndRejectsErrors()
    {
        var network = new BooleanNetwork(["A", "B", "C", "D"]);
        network.SetRule("D", "A | B & !C");
        network.SetRule("C", "(A OR B) AND NOT (A && B)");

        for (int code = 0; code < 16; code++)
        {
            bool[] state = Enumerable.Range(0, 4).Select(i => ((code >> i) & 1) == 1).ToArray();
            bool[] next = network.Step(state);

            Assert.Equal(state[0] || (state[1] && !state[2]), next[3]);
            Assert.Equal(state[0] ^ state[1], next[2]);
            Assert.Equal(state[0], next[0]);
        }

        _ = Assert.Throws<FormatException>(() => network.SetRule("A", "A & E"));
        _ = Assert.Throws<FormatException>(() => network.SetRule("A", "(A | B"));
        _ = Assert.Throws<ArgumentException>(() => new BooleanNetwork(["A", "A"]));
        _ = Assert.Throws<ArgumentException>(() => new BooleanNetwork(["and"]));
    }

    #endregion

    #region Балансовый анализ потоков

    [Fact]
    public void FluxBalance_ToyNetwork_KnownOptimumVariabilityAndEssentials()
    {
        var network = new MetabolicNetwork();
        network.AddReaction("EX_A", "-> A", 0, 10);
        network.AddReaction("R1", "A -> B");
        network.AddReaction("R2", "A -> C");
        network.AddReaction("R3", "B -> D", 0, 4);
        network.AddReaction("R4", "C -> D");
        network.AddReaction("BIO", "D ->");

        FluxBalanceResult result = network.Optimize("BIO");

        Assert.True(result.IsOptimal);
        Assert.Equal(10, result.ObjectiveValue, 9);
        Assert.Contains("EX_A", result.Limiting);
        AssertSteadyState(network, result);

        IReadOnlyDictionary<string, (double Minimum, double Maximum)> variability = network.FluxVariability("BIO");

        AssertRange(variability["R1"], 0, 4);
        AssertRange(variability["R3"], 0, 4);
        AssertRange(variability["R2"], 6, 10);
        AssertRange(variability["R4"], 6, 10);
        AssertRange(variability["EX_A"], 10, 10);

        Assert.Equal(["EX_A"], network.EssentialReactions("BIO"));
        Assert.Equal(4, network.Optimize("BIO", knockouts: ["R4"]).ObjectiveValue, 9);
        Assert.Contains(result.Interpret().Warnings, w => w.Contains("FluxVariability", StringComparison.Ordinal));
    }

    [Fact]
    public void FluxBalance_ParsesEquations()
    {
        var network = new MetabolicNetwork();
        MetabolicReaction reaction = network.AddReaction("r", "2 A + B -> C + 0.5 D", -5, 5);

        Assert.Equal(-2, reaction.Stoichiometry["A"]);
        Assert.Equal(-1, reaction.Stoichiometry["B"]);
        Assert.Equal(0.5, reaction.Stoichiometry["D"]);
        Assert.True(reaction.IsReversible);
        Assert.Equal(["A", "B", "C", "D"], network.Metabolites);
        _ = Assert.Throws<FormatException>(() => network.AddReaction("bad", "A = B"));
    }

    [Fact]
    public void FluxBalance_OnTransportNetwork_EqualsMaximumFlow()
    {
        var rng = new Random(91);

        for (int trial = 0; trial < 25; trial++)
        {
            int nodes = 4 + rng.Next(5);
            var edges = new List<(int U, int V, double Capacity)>();

            for (int u = 0; u < nodes; u++)
            {
                for (int v = 0; v < nodes; v++)
                {
                    if (u != v && rng.NextDouble() < 0.35)
                        edges.Add((u, v, 1 + rng.Next(10)));
                }
            }

            var network = new MetabolicNetwork();
            network.AddReaction("uptake", "-> m0", 0, 1000);

            for (int k = 0; k < edges.Count; k++)
                network.AddReaction($"e{k}", $"m{edges[k].U} -> m{edges[k].V}", 0, edges[k].Capacity);

            network.AddReaction("export", $"m{nodes - 1} ->", 0, 1000);

            var flow = new FlowNetwork(nodes);

            foreach ((int u, int v, double capacity) in edges)
                flow.AddEdge(new FlowEdge(u, v, capacity));

            FluxBalanceResult result = network.Optimize("export");

            Assert.True(result.IsOptimal);
            Assert.Equal(new Dinic(flow, 0, nodes - 1).MaxFlow, result.ObjectiveValue, 6);
            AssertSteadyState(network, result);
        }
    }

    #endregion

    #region Стохастическая кинетика

    [Fact]
    public void Gillespie_BirthDeath_IsPoisson()
    {
        var scheme = new KineticScheme(["X"],
        [
            new ReactionStep { Reactants = new Dictionary<string, double>(), Products = new Dictionary<string, double> { ["X"] = 1 }, RateConstantIndex = 0 },
            new ReactionStep { Reactants = new Dictionary<string, double> { ["X"] = 1 }, Products = new Dictionary<string, double>(), RateConstantIndex = 1 }
        ]);

        StochasticTrajectory run = StochasticKinetics.Simulate(scheme, [0], [10.0, 1.0], 5000, new Random(101));

        // Стационарное распределение — Пуассон со средним k/γ: дисперсия равна среднему
        Assert.Equal(10, run.TimeAverage("X", from: 20), 0.3);
        Assert.Equal(10, run.TimeVariance("X", from: 20), 1.0);
        Assert.False(run.Truncated);
    }

    [Fact]
    public void Gillespie_CountsDistinctPairsForDimerization()
    {
        // 2A → B: у десяти молекул 45 различных пар, и первое событие ждёт в среднем 1/45, а не 1/100
        var scheme = new KineticScheme(["A", "B"],
        [
            new ReactionStep
            {
                Reactants = new Dictionary<string, double> { ["A"] = 2 },
                Products = new Dictionary<string, double> { ["B"] = 1 },
                RateConstantIndex = 0
            }
        ]);

        var rng = new Random(102);
        double sum = 0;
        const int Runs = 4000;

        for (int r = 0; r < Runs; r++)
            sum += StochasticKinetics.Simulate(scheme, [10, 0], [1.0], 10, rng).Times[1];

        Assert.Equal(1.0 / 45, sum / Runs, 0.05 / 45);

        StochasticTrajectory complete = StochasticKinetics.Simulate(scheme, [10, 0], [1.0], 100, rng);
        Assert.Equal([0, 5], complete.CountsAt(complete.Events));
    }

    [Fact]
    public void Gillespie_MeanFollowsRateEquationsForLinearScheme()
    {
        KineticScheme scheme = KineticScheme.Reversible();
        double[] constants = [1.0, 0.5];
        double deterministic = scheme.SimulateSpecies("A", [60, 0], constants, [0, 1.0])[1];

        var rng = new Random(103);
        double[] samples = Enumerable.Range(0, 400)
            .Select(_ => (double)StochasticKinetics.Simulate(scheme, [60, 0], constants, 1.0, rng).CountAt("A", 1.0))
            .ToArray();

        double mean = samples.Average();
        double error = Math.Sqrt(samples.Sum(x => (x - mean) * (x - mean)) / (samples.Length - 1) / samples.Length);

        // Для линейной схемы среднее стохастической модели точно подчиняется уравнениям скорости
        Assert.Equal(60 * (0.5 + Math.Exp(-1.5)) / 1.5, deterministic, 1e-4);
        Assert.True(Math.Abs(mean - deterministic) < 4 * error, $"среднее {mean}, уравнения {deterministic}, ошибка {error}");
    }

    [Fact]
    public void Gillespie_RejectsNonElementarySteps()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            StochasticKinetics.Simulate(KineticScheme.Simple(order: 0.5), [10, 0], [1.0], 1, new Random(1)));
    }

    [Fact]
    public void GeneExpression_ProteinNoiseMatchesTheory()
    {
        const double Km = 2, Gm = 0.2, Kp = 10, Gp = 0.5;
        GeneExpressionMoments theory = GeneExpression.StationaryMoments(Km, Gm, Kp, Gp);

        Assert.Equal(10, theory.MeanMrna, 12);
        Assert.Equal(200, theory.MeanProtein, 9);
        Assert.Equal(1 + (10 / 0.7), theory.ProteinFano, 12);

        StochasticTrajectory run = GeneExpression.Simulate(Km, Gm, Kp, Gp, 1500, new Random(104), 10, 200);
        double mean = run.TimeAverage(GeneExpression.Protein, from: 50);
        double fano = run.TimeVariance(GeneExpression.Protein, from: 50) / mean;

        Assert.Equal(theory.MeanProtein, mean, theory.MeanProtein * 0.07);
        Assert.Equal(theory.ProteinFano, fano, theory.ProteinFano * 0.25);
        Assert.Equal(theory.MeanMrna, run.TimeAverage(GeneExpression.Mrna, from: 50), 0.8);

        // Детерминированный предел той же схемы приходит к тем же средним
        var states = GeneExpression.TwoStage().Simulate([0, 0], [Km, Gm, Kp, Gp], [0, 50, 100, 200]);
        Assert.Equal(10, states[^1][0], 1e-6);
        Assert.Equal(200, states[^1][1], 1e-4);
    }

    #endregion

    #region Инструменты

    private static void AssertRange((double Minimum, double Maximum) range, double minimum, double maximum)
    {
        Assert.Equal(minimum, range.Minimum, 6);
        Assert.Equal(maximum, range.Maximum, 6);
    }

    private static void AssertSteadyState(MetabolicNetwork network, FluxBalanceResult result)
    {
        foreach (string metabolite in network.Metabolites)
        {
            double balance = network.Reactions.Sum(r => r.Stoichiometry.GetValueOrDefault(metabolite) * result[r.Id]);
            Assert.True(Math.Abs(balance) < 1e-7, $"баланс {metabolite}: {balance}");
        }

        foreach (MetabolicReaction reaction in network.Reactions)
        {
            Assert.InRange(result[reaction.Id], reaction.LowerBound - 1e-7, reaction.UpperBound + 1e-7);
        }
    }

    #endregion
}

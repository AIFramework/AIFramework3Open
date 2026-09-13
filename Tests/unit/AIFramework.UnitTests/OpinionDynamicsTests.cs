using AI.DataStructs.Algebraic;
using AI.Simulation.Social;
using AI.Simulation.Space;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Динамика мнений проверяется ответами, полученными другим путём. Согласие Де Грота — итерацией против
/// линейной системы, а на неориентированной сети — против известного ответа «социальная сила
/// пропорциональна степени». Скорость согласия на кольце — против спектра циркулянта. Фридкин — Джонсен —
/// итерацией против LU. У ограниченного доверия — инварианты: порядок мнений, среднее у Деффюана,
/// разрыв между группами больше ε. Модель избирателя — Монте-Карло против мартингала Σ dᵢ·xᵢ.
/// </summary>
public class OpinionDynamicsTests
{
    [Fact]
    public void DeGroot_IteratedOpinions_ConvergeToSocialPowerWeightedAverage()
    {
        var random = new Random(4);
        const int n = 6;
        var influence = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            double[] row = [.. Enumerable.Range(0, n).Select(_ => random.NextDouble())];
            double sum = row.Sum();

            for (int j = 0; j < n; j++)
                influence[i, j] = row[j] / sum;
        }

        var model = new DeGrootModel(influence);
        double[] initial = [.. Enumerable.Range(0, n).Select(_ => random.NextDouble() * 10)];
        double consensus = model.Consensus(initial);
        IReadOnlyList<double> power = model.SocialPower();

        Assert.True(model.ReachesConsensus);
        Assert.Equal(1, power.Sum(), 12);

        // πW = π: левый собственный вектор проверяется самим определением
        for (int j = 0; j < n; j++)
            Assert.Equal(power[j], Enumerable.Range(0, n).Sum(i => power[i] * influence[i, j]), 12);

        Assert.All(model.Run(initial, 400)[^1], value => Assert.Equal(consensus, value, 9));
    }

    [Fact]
    public void DeGroot_OnUndirectedNetwork_SocialPowerIsProportionalToDegree()
    {
        // Детальный баланс: πᵢ·(1 − s)/dᵢ = πⱼ·(1 − s)/dⱼ, поэтому π ∝ d независимо от доли своего мнения s
        ContactNetwork network = ContactNetwork.BarabasiAlbert(30, 2, new Random(8));
        var model = DeGrootModel.FromNetwork(network, selfWeight: 0.3);
        double totalDegree = Enumerable.Range(0, 30).Sum(network.Degree);
        IReadOnlyList<double> power = model.SocialPower();

        for (int i = 0; i < 30; i++)
            Assert.Equal(network.Degree(i) / totalDegree, power[i], 10);

        double[] initial = [.. Enumerable.Range(0, 30).Select(i => (double)(i % 7))];
        double degreeWeighted = Enumerable.Range(0, 30).Sum(i => network.Degree(i) * initial[i]) / totalDegree;
        Assert.Equal(degreeWeighted, model.Consensus(initial), 10);
    }

    [Fact]
    public void DeGroot_OnRing_SecondEigenvalue_MatchesCirculantSpectrum_AndGovernsSpeed()
    {
        // W = ½·I + ¼·(сдвиг + обратный сдвиг): собственные числа ½ + ½·cos(2πk/n)
        const int n = 10;
        var model = DeGrootModel.FromNetwork(ContactNetwork.Ring(n, 2), selfWeight: 0.5);
        double expected = 0.5 + (0.5 * Math.Cos(2 * Math.PI / n));

        Assert.Equal(expected, model.SecondEigenvalueModulus, 9);

        double[] initial = [.. Enumerable.Range(0, n).Select(i => i < n / 2 ? 1.0 : 0.0)];
        double[][] trajectory = model.Run(initial, 80);
        double Spread(double[] x) => x.Max() - x.Min();

        Assert.Equal(expected, Spread(trajectory[80]) / Spread(trajectory[79]), 3);

        InfluenceAnalysis analysis = model.Analyze();
        Assert.Equal(Math.Log(0.01) / Math.Log(expected), analysis.StepsToHundredfold, 9);
        Assert.Equal(n, analysis.EffectiveVoices, 9);
        Assert.Contains("одному мнению", analysis.Interpret().ToString());
    }

    [Fact]
    public void DeGroot_PeriodicAndDisconnectedNetworks_AreRecognised()
    {
        var swap = new Matrix(2, 2);
        swap[0, 1] = 1;
        swap[1, 0] = 1;
        var periodic = new DeGrootModel(swap);

        Assert.True(periodic.IsStronglyConnected);
        Assert.Equal(2, periodic.Period);
        Assert.False(periodic.ReachesConsensus);
        Assert.Equal([3.0, 1.0], periodic.Run([1.0, 3.0], 1)[1]);
        Assert.Throws<InvalidOperationException>(() => periodic.Consensus([1.0, 3.0]));

        var split = new ContactNetwork(4);
        split.Connect(0, 1);
        split.Connect(2, 3);
        var disconnected = DeGrootModel.FromNetwork(split);

        Assert.False(disconnected.IsStronglyConnected);
        Assert.Throws<InvalidOperationException>(() => disconnected.SocialPower());
        Assert.Contains("распадается", disconnected.Analyze().Interpret().ToString());

        var bad = new Matrix(2, 2);
        bad[0, 0] = 0.7;
        bad[1, 1] = 1;
        Assert.Throws<ArgumentException>(() => new DeGrootModel(bad));
    }

    [Fact]
    public void FriedkinJohnsen_IterationConvergesToLinearEquilibrium()
    {
        var random = new Random(6);
        ContactNetwork network = ContactNetwork.WattsStrogatz(12, 4, 0.2, random);
        var influence = InfluenceMatrix.FromNetwork(network, 0.4);
        double[] susceptibility = [.. Enumerable.Range(0, 12).Select(_ => 0.2 + (0.7 * random.NextDouble()))];
        double[] prejudices = [.. Enumerable.Range(0, 12).Select(_ => random.NextDouble())];
        var model = new FriedkinJohnsenModel(influence, susceptibility);

        double[] equilibrium = model.Equilibrium(prejudices);
        double[] iterated = model.Run(prejudices, 400)[^1];

        for (int i = 0; i < 12; i++)
            Assert.Equal(equilibrium[i], iterated[i], 10);

        // Упрямцы остаются при своём, а полностью восприимчивая сеть — это Де Грот без единственного равновесия
        var stubborn = new FriedkinJohnsenModel(influence, new double[12]);
        Assert.Equal(prejudices, stubborn.Equilibrium(prejudices));
        Assert.Throws<InvalidOperationException>(() =>
            new FriedkinJohnsenModel(influence, Enumerable.Repeat(1.0, 12).ToArray()).Equilibrium(prejudices));

        // Общение сближает, но не уравнивает: разброс меньше исходного, но не нулевой
        double Spread(double[] x) => x.Max() - x.Min();
        Assert.InRange(Spread(equilibrium), 1e-3, Spread(prejudices));
    }

    [Fact]
    public void HegselmannKrause_ClustersOrConsensus_DependOnConfidence()
    {
        double[] evenly = [.. Enumerable.Range(0, 101).Select(i => i / 100.0)];

        OpinionDynamicsResult wide = BoundedConfidence.HegselmannKrause(evenly, 0.25);
        Assert.True(wide.Converged);
        Assert.True(wide.IsConsensus);

        OpinionDynamicsResult narrow = BoundedConfidence.HegselmannKrause(evenly, 0.05);
        Assert.True(narrow.Converged);
        Assert.True(narrow.Clusters.Count >= 3);

        // В конце группы не слышат друг друга: разрыв между соседними больше ε
        double[] positions = [.. narrow.Clusters.Select(cluster => cluster.Position)];
        for (int k = 1; k < positions.Length; k++)
            Assert.True(positions[k] - positions[k - 1] > 0.05);

        Assert.Equal(101, narrow.Clusters.Sum(cluster => cluster.Size));
        Assert.Contains("групп", narrow.Interpret().ToString());

        // При доверии шире разброса все за один шаг приходят к среднему
        OpinionDynamicsResult trusting = BoundedConfidence.HegselmannKrause([0.1, 0.4, 0.9], 1.0);
        Assert.All(trusting.Opinions, value => Assert.Equal(1.4 / 3, value, 12));
    }

    [Fact]
    public void HegselmannKrause_PreservesOrderOfOpinions()
    {
        var random = new Random(9);
        double[] opinions = [.. Enumerable.Range(0, 200).Select(_ => random.NextDouble())];

        for (int step = 0; step < 5; step++)
        {
            double[] next = BoundedConfidence.HegselmannKrauseStep(opinions, 0.1);

            for (int i = 0; i < opinions.Length; i++)
            {
                for (int j = 0; j < opinions.Length; j++)
                {
                    if (opinions[i] < opinions[j])
                        Assert.True(next[i] <= next[j] + 1e-15);
                }
            }

            opinions = next;
        }
    }

    [Fact]
    public void Deffuant_ConservesMean_AndSplitsIntoAboutOneOverTwoEpsilonGroups()
    {
        var random = new Random(12);
        double[] initial = [.. Enumerable.Range(0, 1000).Select(_ => random.NextDouble())];

        OpinionDynamicsResult consensus = BoundedConfidence.Deffuant(initial, 0.35, 0.5, 400_000, random);
        Assert.Equal(consensus.InitialMean, consensus.FinalMean, 12);
        Assert.Equal(1, consensus.MajorClusterCount());

        OpinionDynamicsResult split = BoundedConfidence.Deffuant(initial, 0.1, 0.5, 600_000, random);
        Assert.Equal(split.InitialMean, split.FinalMean, 12);
        Assert.InRange(split.MajorClusterCount(), 3, 6);
    }

    [Fact]
    public void Voter_ConsensusProbability_IsDegreeWeighted_NotHeadcount()
    {
        // Звезда: центр «да», шесть лучей «нет». По головам 1/7, по степеням 6/12
        var star = new ContactNetwork(7);
        for (int leaf = 1; leaf < 7; leaf++)
            star.Connect(0, leaf);

        bool[] opinions = [true, false, false, false, false, false, false];
        Assert.Equal(0.5, VoterModel.ConsensusProbability(star, opinions), 12);

        var random = new Random(21);
        const int runs = 4000;
        int agreed = 0;

        for (int r = 0; r < runs; r++)
        {
            VoterOutcome outcome = VoterModel.Run(star, opinions, random);
            Assert.NotNull(outcome.Consensus);

            if (outcome.Consensus == true)
                agreed++;
        }

        Assert.Equal(0.5, (double)agreed / runs, 0.04);

        // На кольце степени равны, и вероятность совпадает с простой долей
        bool[] ring = [.. Enumerable.Range(0, 10).Select(i => i < 3)];
        Assert.Equal(0.3, VoterModel.ConsensusProbability(ContactNetwork.Ring(10, 2), ring), 12);

        var split = new ContactNetwork(4);
        split.Connect(0, 1);
        split.Connect(2, 3);
        Assert.Throws<ArgumentException>(() => VoterModel.ConsensusProbability(split, [true, false, true, false]));
    }
}

using AI.Psychology.Emotion;
using AI.Psychology.Social;
using AI.Psychology.Traits;
using AI.Simulation.SystemDynamics;
using AI.Simulation.Space;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Эмоции и социальный уровень. Модель OCC проверяется по своей структуре: каждая из 22 эмоций
/// возникает в своей ситуации и только в ней. Возврат аффекта — против экспоненты и стационарного
/// разброса процесса Орнштейна — Уленбека. Заражение — решением уравнений против линейной алгебры
/// и против замкнутого ответа π ∝ ε/δ. Агенты — против модели заражения, в которую они вырождаются.
/// </summary>
public class EmotionDynamicsTests
{
    #region OCC и PAD

    [Fact]
    public void Occ_BlameworthyHarm_GivesDistressReproachAndAnger()
    {
        // «Коллега сорвал мой проект»
        IReadOnlyList<ElicitedEmotion> emotions = OccModel.Appraise(new Appraisal
        {
            Desirability = -0.8,
            Agent = Agency.Other,
            Praiseworthiness = -0.6
        });

        Assert.Equal(OccEmotion.Distress, emotions[0].Emotion);
        Assert.Equal(0.8, emotions[0].Intensity, 12);
        Assert.Equal(0.7, Intensity(emotions, OccEmotion.Anger), 12);
        Assert.Equal(0.6, Intensity(emotions, OccEmotion.Reproach), 12);
        Assert.DoesNotContain(emotions, e => OccModel.IsPositive(e.Emotion));
    }

    [Fact]
    public void Occ_EachSituation_ElicitsItsOwnEmotions_AndAllTwentyTwoAreReachable()
    {
        var reached = new HashSet<OccEmotion>();

        void Expect(Appraisal appraisal, params OccEmotion[] expected)
        {
            OccEmotion[] actual = [.. OccModel.Appraise(appraisal).Select(e => e.Emotion).Order()];
            Assert.Equal(expected.Order(), actual);
            reached.UnionWith(actual);
        }

        Expect(new Appraisal { Desirability = 0.6, Agent = Agency.Self, Praiseworthiness = 0.8 },
            OccEmotion.Joy, OccEmotion.Pride, OccEmotion.Gratification);
        Expect(new Appraisal { Desirability = -0.5, Agent = Agency.Self, Praiseworthiness = -0.7 },
            OccEmotion.Distress, OccEmotion.Shame, OccEmotion.Remorse);
        Expect(new Appraisal { Desirability = 0.7, Agent = Agency.Other, Praiseworthiness = 0.5 },
            OccEmotion.Joy, OccEmotion.Admiration, OccEmotion.Gratitude);
        Expect(new Appraisal { Desirability = -0.7, Agent = Agency.Other, Praiseworthiness = -0.5 },
            OccEmotion.Distress, OccEmotion.Reproach, OccEmotion.Anger);
        Expect(new Appraisal { Desirability = 0.5, Status = EventStatus.Prospective, Likelihood = 0.4 }, OccEmotion.Hope);
        Expect(new Appraisal { Desirability = -0.5, Status = EventStatus.Prospective, Likelihood = 0.4 }, OccEmotion.Fear);
        Expect(new Appraisal { Desirability = 0.5, Status = EventStatus.Confirmed }, OccEmotion.Joy, OccEmotion.Satisfaction);
        Expect(new Appraisal { Desirability = -0.5, Status = EventStatus.Confirmed }, OccEmotion.Distress, OccEmotion.FearsConfirmed);
        Expect(new Appraisal { Desirability = 0.5, Status = EventStatus.Disconfirmed }, OccEmotion.Disappointment);
        Expect(new Appraisal { Desirability = -0.5, Status = EventStatus.Disconfirmed }, OccEmotion.Relief);
        Expect(new Appraisal { DesirabilityForOther = 0.6, LikingOfOther = 0.8 }, OccEmotion.HappyFor);
        Expect(new Appraisal { DesirabilityForOther = -0.6, LikingOfOther = 0.8 }, OccEmotion.Pity);
        Expect(new Appraisal { DesirabilityForOther = 0.6, LikingOfOther = -0.8 }, OccEmotion.Resentment);
        Expect(new Appraisal { DesirabilityForOther = -0.6, LikingOfOther = -0.8 }, OccEmotion.Gloating);
        Expect(new Appraisal { Appeal = 0.4 }, OccEmotion.Love);
        Expect(new Appraisal { Appeal = -0.4 }, OccEmotion.Hate);

        Assert.Equal(Enum.GetValues<OccEmotion>().Length, reached.Count);
        Assert.Equal(22, OccModel.Labels.Count);

        // Злорадство приятно тому, кто злорадствует, жалость — нет
        Assert.True(OccModel.IsPositive(OccEmotion.Gloating));
        Assert.False(OccModel.IsPositive(OccEmotion.Pity));
    }

    [Fact]
    public void Occ_IntensityFollowsAppraisalVariables_AndRejectsOutOfScaleInput()
    {
        // Страх растёт с вероятностью беды, сочувствие — с симпатией к пострадавшему
        double Fear(double likelihood) => Intensity(OccModel.Appraise(
            new Appraisal { Desirability = -0.8, Status = EventStatus.Prospective, Likelihood = likelihood }), OccEmotion.Fear);

        Assert.Equal(0.2, Fear(0.25), 12);
        Assert.True(Fear(0.9) > Fear(0.3));
        Assert.Equal(0.72, Intensity(OccModel.Appraise(new Appraisal { DesirabilityForOther = -0.9, LikingOfOther = 0.8 }), OccEmotion.Pity), 12);

        Assert.Empty(OccModel.Appraise(new Appraisal { Desirability = 0.05 }, threshold: 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => OccModel.Appraise(new Appraisal { Desirability = 1.5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => OccModel.Appraise(new Appraisal { Likelihood = 1.2 }));
    }

    [Fact]
    public void Affect_OctantsAndGeometry()
    {
        Assert.Equal(MoodOctant.Exuberant, new Affect(0.5, 0.5, 0.5).Octant);
        Assert.Equal(MoodOctant.Bored, new Affect(-0.5, -0.5, -0.5).Octant);
        Assert.Equal(MoodOctant.Dependent, new Affect(0.5, 0.5, -0.5).Octant);
        Assert.Equal(MoodOctant.Disdainful, new Affect(-0.5, -0.5, 0.5).Octant);
        Assert.Equal(MoodOctant.Relaxed, new Affect(0.5, -0.5, 0.5).Octant);
        Assert.Equal(MoodOctant.Anxious, new Affect(-0.5, 0.5, -0.5).Octant);
        Assert.Equal(MoodOctant.Docile, new Affect(0.5, -0.5, -0.5).Octant);
        Assert.Equal(MoodOctant.Hostile, new Affect(-0.5, 0.5, 0.5).Octant);

        Assert.Equal(1, new Affect(1, -1, 1).Intensity, 12);
        Assert.Equal(new Affect(1, -1, 0.2), new Affect(1.7, -3, 0.2).Clamp());
        Assert.Equal(0.5, new Affect(0.3, 0, 0).DistanceTo(new Affect(0, 0.4, 0)), 12);
        Assert.Contains("тревога", new Affect(-0.4, 0.6, -0.3).ToString());
    }

    #endregion

    #region Динамика аффекта

    [Fact]
    public void AffectDynamics_DeviationDecaysExponentially_AndNoiseKeepsStationarySpread()
    {
        var baseline = new Affect(0.1, 0, -0.2);
        var dynamics = new AffectDynamics(baseline, relaxationTime: 4, spread: 0.1);
        var shaken = AffectDynamics.Stimulate(baseline, new Affect(0.6, 0.7, 0.5));

        // Через одно время возврата отклонение от базовой линии — в e раз меньше
        Affect later = dynamics.Expected(shaken, 4);
        Assert.Equal(0.6 / Math.E, later.Pleasure - baseline.Pleasure, 12);
        Assert.Equal(0.7 / Math.E, later.Arousal - baseline.Arousal, 12);
        Assert.Equal(dynamics.HalfLife, dynamics.TimeToFraction(0.5), 12);

        var random = new Random(17);
        Affect state = baseline;
        var pleasure = new List<double>();

        for (int k = 0; k < 60_000; k++)
        {
            state = dynamics.Next(state, 0.5, random);
            pleasure.Add(state.Pleasure);
        }

        double mean = pleasure.Average();
        double deviation = Math.Sqrt(pleasure.Sum(p => (p - mean) * (p - mean)) / (pleasure.Count - 1));

        Assert.Equal(baseline.Pleasure, mean, 0.01);
        Assert.Equal(0.1, deviation, 0.012);
    }

    #endregion

    #region Заражение и агенты

    [Fact]
    public void Contagion_SolvedEquations_MatchLinearAlgebra_AndClosedFormPower()
    {
        var random = new Random(23);
        ContactNetwork network = ContactNetwork.WattsStrogatz(15, 4, 0.2, random);
        double[] expressiveness = [.. Enumerable.Range(0, 15).Select(_ => 0.2 + random.NextDouble())];
        double[] openness = [.. Enumerable.Range(0, 15).Select(_ => 0.2 + random.NextDouble())];
        double[] initial = [.. Enumerable.Range(0, 15).Select(_ => (2 * random.NextDouble()) - 1)];
        var contagion = new EmotionalContagion(network, expressiveness, openness, channel: 0.5);

        // Симметричные связи: вес чувства человека в общем итоге пропорционален εᵢ/δᵢ
        double total = Enumerable.Range(0, 15).Sum(i => expressiveness[i] / openness[i]);
        IReadOnlyList<double> power = contagion.SocialPower();

        for (int i = 0; i < 15; i++)
            Assert.Equal(expressiveness[i] / openness[i] / total, power[i], 10);

        // Горизонт — 25 времён самой медленной моды: разногласия убывают как e^(−rt), r = −ln|λ₂|/h
        double step = contagion.MaxStableStep / 2;
        double slowest = -Math.Log(contagion.Discretize(step).SecondEigenvalueModulus) / step;
        double horizon = 25 / slowest;
        double consensus = contagion.Consensus(initial);
        IReadOnlyList<SystemState> solved = contagion.Simulate(initial, horizon, points: 2, stepsPerInterval: (int)Math.Ceiling(horizon / 0.02));
        Assert.All(solved[^1].Levels, level => Assert.Equal(consensus, level, 6));

        // При равных выразительности и открытости итог — простое среднее, а не взвешенное степенями, как у Де Грота
        var equal = new EmotionalContagion(network, Enumerable.Repeat(0.5, 15).ToArray(), Enumerable.Repeat(0.5, 15).ToArray());
        Assert.Equal(initial.Average(), equal.Consensus(initial), 10);
    }

    [Fact]
    public void Agents_WithoutRelaxationAndNoise_ReduceExactlyToContagion()
    {
        var random = new Random(31);
        ContactNetwork network = ContactNetwork.BarabasiAlbert(20, 2, random);
        var agents = Enumerable.Range(0, 20).Select(node => new SocialAgent(node,
            new AgentParameters(Affect.Neutral, RelaxationTime: 1e12, Spread: 0,
                Expressiveness: 0.3 + random.NextDouble(), Openness: 0.3 + random.NextDouble()),
            new Affect((2 * random.NextDouble()) - 1, 0, 0), opinion: 0)).ToList();

        var world = new SocialWorld(network, timeStep: 0.02, noise: false);
        var model = SocialSimulation.Create(world, agents, seed: 1);
        double expected = world.Contagion().Consensus([.. agents.Select(agent => agent.Affect.Pleasure)]);

        model.Run(20_000);

        Assert.All(world.Agents, agent => Assert.Equal(expected, agent.Affect.Pleasure, 5));
    }

    [Fact]
    public void Agents_WithoutContagion_ReturnToOwnBaselines()
    {
        ContactNetwork network = ContactNetwork.Ring(8, 2);
        var agents = Enumerable.Range(0, 8).Select(node =>
        {
            var baseline = new Affect((node - 4) / 5.0, 0.1, 0);
            return new SocialAgent(node, new AgentParameters(baseline, RelaxationTime: 2, Spread: 0, Expressiveness: 1, Openness: 1),
                new Affect(0.9, -0.9, 0.5), opinion: node);
        }).ToList();

        var world = new SocialWorld(network, timeStep: 0.1, confidence: 0.5, channel: 0, noise: false);
        SocialSimulation.Create(world, agents).Run(400);

        foreach (SocialAgent agent in world.Agents)
            Assert.True(agent.Affect.DistanceTo(agent.Parameters.Baseline) < 1e-6);

        // Мнения 0…7 на кольце с порогом 0,5: соседние отличаются на 1, никто никого не слышит
        Assert.Equal(Enumerable.Range(0, 8).Select(i => (double)i), world.Agents.Select(agent => agent.Opinion));
    }

    [Fact]
    public void Agents_TrustingEveryContact_ReachDeGrootConsensusWeightedByDegreePlusOne()
    {
        // Агент усредняет себя и всех контактов поровну: wᵢⱼ = 1/(dᵢ + 1), и детальный баланс даёт π ∝ d + 1
        var random = new Random(41);
        ContactNetwork network = ContactNetwork.WattsStrogatz(40, 4, 0.2, random);
        var parameters = new AgentParameters(Affect.Neutral, 5, 0.05, 0.5, 0.5);
        double[] initial = [.. Enumerable.Range(0, 40).Select(_ => random.NextDouble())];
        var agents = Enumerable.Range(0, 40).Select(node => new SocialAgent(node, parameters, Affect.Neutral, initial[node])).ToList();
        var world = new SocialWorld(network, timeStep: 0.05);

        SocialSimulation.Create(world, agents, seed: 2).Run(3000);

        double weights = Enumerable.Range(0, 40).Sum(i => network.Degree(i) + 1.0);
        double expected = Enumerable.Range(0, 40).Sum(i => (network.Degree(i) + 1) * initial[i]) / weights;

        // Шум аффекта в мнения не проникает
        Assert.All(world.Agents, agent => Assert.Equal(expected, agent.Opinion, 9));
        Assert.All(world.Agents, agent => Assert.True(agent.Affect.IsWithinBounds));
    }

    [Fact]
    public void Agents_OnCompleteNetwork_UpdateOpinionsExactlyLikeHegselmannKrause()
    {
        var random = new Random(43);
        double[] initial = [.. Enumerable.Range(0, 30).Select(_ => random.NextDouble())];
        var parameters = new AgentParameters(Affect.Neutral, 5, 0, 0.5, 0.5);
        var agents = Enumerable.Range(0, 30).Select(node => new SocialAgent(node, parameters, Affect.Neutral, initial[node])).ToList();
        var world = new SocialWorld(ContactNetwork.Complete(30), timeStep: 0.01, confidence: 0.12, noise: false);
        var model = SocialSimulation.Create(world, agents, seed: 3);

        model.Step();
        double[] expectedStep = AI.Simulation.Social.BoundedConfidence.HegselmannKrauseStep(initial, 0.12);
        Assert.Equal(expectedStep, world.Agents.Select(agent => agent.Opinion), new ToleranceComparer(1e-12));

        model.Run(200);
        var final = AI.Simulation.Social.BoundedConfidence.HegselmannKrause(initial, 0.12);
        Assert.Equal(final.Opinions, world.Agents.Select(agent => agent.Opinion), new ToleranceComparer(1e-9));
    }

    private sealed class ToleranceComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => 0;
    }

    [Fact]
    public void Agent_FromPersonality_UsesCallerMapping()
    {
        Personality extravert = Personality.FromScale([3, 3, 5, 4, 2]);
        Assert.Equal(1, extravert.Extraversion, 12);
        Assert.Equal(-0.5, extravert.Neuroticism, 12);

        SocialAgent agent = SocialAgent.FromPersonality(0, extravert, traits =>
            new AgentParameters(new Affect(0.2 * traits.Extraversion, 0, 0), 3 * (1 + traits.Neuroticism), 0.1,
                Expressiveness: 1 + traits.Extraversion, Openness: 1));

        Assert.Equal(2, agent.Parameters.Expressiveness, 12);
        Assert.Equal(1.5, agent.Dynamics.RelaxationTime, 12);
        Assert.Equal(new Affect(0.2, 0, 0), agent.Affect);
        Assert.Same(extravert, agent.Traits);
    }

    #endregion

    private static double Intensity(IReadOnlyList<ElicitedEmotion> emotions, OccEmotion emotion) =>
        emotions.Single(e => e.Emotion == emotion).Intensity;
}

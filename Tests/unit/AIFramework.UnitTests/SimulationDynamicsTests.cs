using AI.Biology.Populations;
using AI.Insights;
using AI.Simulation.DiscreteEvent;
using AI.Simulation.Experiments;
using AI.Simulation.Learning;
using AI.Simulation.Markov;
using AI.Simulation.Planning;
using AI.Simulation.Queueing;
using AI.Simulation.Space;
using AI.Statistics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Каждая новая часть симуляции сверяется с независимым ответом: интервал по прогонам — с теорией
/// M/M/1 и с заявленной доверительной вероятностью, приоритеты — с формулами Кобэма, обучение —
/// с точным решением марковского процесса, эпидемия на полном графе — с уравнением итогового
/// размера из <c>AI.Biology</c>, планировщик — с известной длиной оптимального плана.
/// </summary>
public class SimulationDynamicsTests
{
    #region Серии прогонов

    private static ServiceStatistics SingleServerRun(int seed, double arrival, double service, double warmup, double horizon)
    {
        var engine = new SimulationEngine(seed);
        var station = new ServiceStation(engine) { ServiceTime = () => engine.Exponential(service) };

        void Arrive()
        {
            _ = station.Arrive();
            engine.Schedule(engine.Exponential(arrival), Arrive);
        }

        engine.Schedule(engine.Exponential(arrival), Arrive);
        _ = engine.Run(warmup);
        station.ResetStatistics();
        _ = engine.Run(warmup + horizon);

        return station.Statistics();
    }

    [Fact]
    public void Replications_SingleServer_IntervalCoversTheory()
    {
        QueueMetrics theory = QueueingTheory.SingleServer(0.5, 1.0);

        ReplicationEstimate wait = Replications.Run("Ожидание", 20,
            seed => SingleServerRun(seed, 0.5, 1.0, warmup: 500, horizon: 5_000).AverageWait);

        Assert.True(wait.Contains(theory.WaitTime),
            $"Интервал [{wait.Lower:F4}; {wait.Upper:F4}] не накрывает {theory.WaitTime:F4}");
        Assert.True(wait.RelativePrecision < 0.15, $"Точность {wait.RelativePrecision:P1}");
    }

    [Fact]
    public void Replications_CoverageIsCloseToNominal()
    {
        // Показатель прогона — среднее двадцати показательных величин с истинным средним 1
        static double Run(int seed)
        {
            var random = new Random(seed);
            double sum = 0;

            for (int i = 0; i < 20; i++)
                sum += RandomEngine.NextExponential(random, 1.0);

            return sum / 20;
        }

        const int Series = 400;
        int covered = 0;

        for (int series = 0; series < Series; series++)
        {
            if (Replications.Run("Среднее", 10, Run, firstSeed: (series * 10) + 1).Contains(1.0))
                covered++;
        }

        // Заявленные 95 % должны выполняться на деле, а не только на бумаге
        Assert.InRange((double)covered / Series, 0.90, 0.98);
    }

    [Fact]
    public void Replications_EstimateMatchesStudentInterval()
    {
        ReplicationEstimate estimate = Replications.Estimate("1…10", [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0]);

        // s = √(55/6), t(0.975; 9) = 2.2622
        double expectedHalfWidth = 2.2622 * Math.Sqrt(55.0 / 6.0) / Math.Sqrt(10);

        Assert.Equal(5.5, estimate.Mean, tolerance: 1e-12);
        Assert.Equal(expectedHalfWidth, estimate.HalfWidth, tolerance: 1e-3);
    }

    [Fact]
    public void Replications_RequiredCount_GrowsAsSquareOfPrecision()
    {
        ReplicationEstimate pilot = Replications.Estimate("Пилот", [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0]);

        // Сузить интервал вдвое — вчетверо больше прогонов
        Assert.Equal(40, Replications.RequiredReplications(pilot, pilot.HalfWidth / 2));
    }

    [Fact]
    public void Replications_SeveralMetrics_ComeFromSameRuns()
    {
        IReadOnlyDictionary<string, ReplicationEstimate> result = Replications.Run(10, seed =>
        {
            ServiceStatistics run = SingleServerRun(seed, 0.5, 1.0, warmup: 500, horizon: 5_000);

            return new Dictionary<string, double>
            {
                ["L_q"] = run.AverageQueueLength,
                ["λ·W_q"] = 0.5 * run.AverageWait,
            };
        });

        Assert.Equal(2, result.Count);
        Assert.All(result.Values, estimate => Assert.Equal(10, estimate.Replications));

        // Формула Литтла выполняется в каждом прогоне, поэтому и оценки совпадают
        Assert.Equal(result["L_q"].Mean, result["λ·W_q"].Mean, tolerance: 0.05);
    }

    [Fact]
    public void Replications_WarmupRemovesInitialBias()
    {
        QueueMetrics theory = QueueingTheory.SingleServer(0.9, 1.0);

        ReplicationEstimate cold = Replications.Run("С пустой системы", 40,
            seed => SingleServerRun(seed, 0.9, 1.0, warmup: 0, horizon: 200).AverageQueueLength);

        ReplicationEstimate warm = Replications.Run("После разгона", 40,
            seed => SingleServerRun(seed, 0.9, 1.0, warmup: 3_000, horizon: 200).AverageQueueLength);

        // Пустой старт тянет среднее вниз, и число прогонов этого не исправит: интервал лежит ниже истины
        Assert.True(cold.Upper < theory.QueueLength, $"[{cold.Lower:F2}; {cold.Upper:F2}] при L_q = {theory.QueueLength:F2}");
        Assert.True(warm.Mean > cold.Mean, $"после разгона {warm.Mean:F2}, с пустой {cold.Mean:F2}");
    }

    [Fact]
    public void Replications_Interpretation_ExplainsWhyRunsNotCustomers()
    {
        Interpretation text = Replications.Estimate("Ожидание", [1.0, 1.2, 0.9]).Interpret();

        Assert.Contains(text.Findings, f => f.Contains("прогон", StringComparison.Ordinal));
        Assert.Contains(text.Warnings, w => w.Contains("меньше десяти", StringComparison.Ordinal));
    }

    [Fact]
    public void TimeWeighted_Reset_StartsAverageFromGivenMoment()
    {
        var accumulator = new TimeWeightedAccumulator();
        accumulator.Update(0, 10);
        accumulator.Update(5, 2);
        accumulator.Reset(5);
        accumulator.Update(8, 4);

        // После сброса: 2 на [5, 8] и 4 на [8, 10]; разгонное значение 10 забыто
        Assert.Equal(14.0 / 5.0, accumulator.Average(10), tolerance: 1e-12);
        Assert.Equal(4, accumulator.Maximum);
    }

    [Fact]
    public void ResetStatistics_KeepsCustomersButForgetsCounts()
    {
        var engine = new SimulationEngine(seed: 1);
        var station = new ServiceStation(engine) { ServiceTime = () => 10 };

        engine.ScheduleAt(0, () => station.Arrive());
        engine.ScheduleAt(1, () => station.Arrive());
        _ = engine.Run(5);

        station.ResetStatistics();

        Assert.Equal(0, station.Statistics().Arrivals);
        Assert.Equal(1, station.QueueLength);

        engine.RunToCompletion();
        ServiceStatistics after = station.Statistics();

        // Обе заявки ушли после сброса; очередь из одной стояла с 5 до 10, потом пусто до 20
        Assert.Equal(2, after.Served);
        Assert.Equal(5.0 / 15.0, after.AverageQueueLength, tolerance: 1e-12);
        Assert.Equal(1.0, after.Utilisation, tolerance: 1e-12);
    }

    #endregion

    #region Приоритеты

    private static ServiceStation PriorityRun(
        int seed, QueueDiscipline discipline, double[] rates, double service, double warmup, double horizon,
        int servers = 1, int capacity = int.MaxValue)
    {
        var engine = new SimulationEngine(seed);
        var station = new ServiceStation(engine, servers, capacity, discipline, rates.Length)
        {
            ServiceTime = () => engine.Exponential(service),
        };

        double total = rates.Sum();

        // Один простейший поток, класс разыгрывается пропорционально интенсивностям
        void Arrive()
        {
            double u = engine.Random.NextDouble() * total;
            int priorityClass = 0;

            while (priorityClass < rates.Length - 1 && u >= rates[priorityClass])
            {
                u -= rates[priorityClass];
                priorityClass++;
            }

            _ = station.Arrive(priorityClass);
            engine.Schedule(engine.Exponential(total), Arrive);
        }

        engine.Schedule(engine.Exponential(total), Arrive);
        _ = engine.Run(warmup);
        station.ResetStatistics();
        _ = engine.Run(warmup + horizon);

        return station;
    }

    [Fact]
    public void Priority_Cobham_MatchesHandCalculation()
    {
        IReadOnlyList<QueueMetrics> classes = QueueingTheory.NonPreemptivePriority([0.3, 0.4], [1.0, 1.0]);

        // W₀ = Σ λ·E[S²]/2 = (0.3·2 + 0.4·2)/2 = 0.7
        Assert.Equal(0.7 / (1.0 * 0.7), classes[0].WaitTime, tolerance: 1e-12);
        Assert.Equal(0.7 / (0.7 * 0.3), classes[1].WaitTime, tolerance: 1e-12);
        Assert.Equal(0.3 * classes[0].WaitTime, classes[0].QueueLength, tolerance: 1e-12);
    }

    [Fact]
    public void Priority_Preemptive_TopClassIgnoresLowerOnes()
    {
        // Младший класс с другой скоростью обслуживания не должен влиять на старший вовсе
        IReadOnlyList<QueueMetrics> classes = QueueingTheory.PreemptiveResumePriority([0.3, 0.4], [1.0, 2.0]);
        QueueMetrics alone = QueueingTheory.SingleServer(0.3, 1.0);

        Assert.Equal(alone.WaitTime, classes[0].WaitTime, tolerance: 1e-12);
        Assert.Equal(alone.SystemTime, classes[0].SystemTime, tolerance: 1e-12);
    }

    [Fact]
    public void Priority_ConservationLaw_WorkDoesNotDependOnOrder()
    {
        double[] rates = [0.2, 0.3, 0.25];
        double[] service = [1.0, 2.0, 1.5];

        static double Weighted(double[] l, double[] m)
        {
            IReadOnlyList<QueueMetrics> classes = QueueingTheory.NonPreemptivePriority(l, m);

            return Enumerable.Range(0, l.Length).Sum(k => l[k] / m[k] * classes[k].WaitTime);
        }

        double direct = Weighted(rates, service);
        double reversed = Weighted([.. rates.Reverse()], [.. service.Reverse()]);

        // Закон сохранения Клейнрока: Σ ρ_k·W_k не зависит от того, кому отдан приоритет,
        // и равен ρ·W₀/(1−ρ)
        double rho = rates.Zip(service, (l, m) => l / m).Sum();
        double residual = rates.Zip(service, (l, m) => l / (m * m)).Sum();

        Assert.Equal(direct, reversed, tolerance: 1e-10);
        Assert.Equal(rho * residual / (1 - rho), direct, tolerance: 1e-10);
    }

    [Theory]
    [InlineData(QueueDiscipline.NonPreemptivePriority)]
    [InlineData(QueueDiscipline.PreemptiveResumePriority)]
    public void Priority_SimulationMatchesTheory(QueueDiscipline discipline)
    {
        double[] rates = [0.3, 0.4];
        double[] service = [1.0, 1.0];

        IReadOnlyList<QueueMetrics> theory = discipline == QueueDiscipline.NonPreemptivePriority
            ? QueueingTheory.NonPreemptivePriority(rates, service)
            : QueueingTheory.PreemptiveResumePriority(rates, service);

        IReadOnlyDictionary<string, ReplicationEstimate> simulated = Replications.Run(10, seed =>
        {
            ServiceStation station = PriorityRun(seed, discipline, rates, 1.0, warmup: 1_000, horizon: 30_000);

            return new Dictionary<string, double>
            {
                ["старший"] = station.Statistics(0).AverageWait,
                ["младший"] = station.Statistics(1).AverageWait,
            };
        });

        Assert.Equal(theory[0].WaitTime, simulated["старший"].Mean, tolerance: (0.05 * theory[0].WaitTime) + 0.01);
        Assert.Equal(theory[1].WaitTime, simulated["младший"].Mean, tolerance: 0.05 * theory[1].WaitTime);
    }

    [Fact]
    public void Fcfs_WithClasses_EveryClassWaitsTheSame()
    {
        ServiceStation station = PriorityRun(3, QueueDiscipline.FirstComeFirstServed, [0.3, 0.4], 1.0,
            warmup: 1_000, horizon: 200_000);

        double theory = QueueingTheory.SingleServer(0.7, 1.0).WaitTime;

        // Без приоритета класс — только метка: оба ждут как в общей очереди M/M/1
        Assert.Equal(theory, station.Statistics(0).AverageWait, tolerance: 0.15);
        Assert.Equal(theory, station.Statistics(1).AverageWait, tolerance: 0.15);
        Assert.Equal(theory, station.Statistics().AverageWait, tolerance: 0.15);
    }

    [Fact]
    public void Priority_Preemption_ResumesInterruptedWork()
    {
        var engine = new SimulationEngine(seed: 1);
        var station = new ServiceStation(engine, discipline: QueueDiscipline.PreemptiveResumePriority, classes: 2)
        {
            ServiceTime = () => 10,
        };

        engine.ScheduleAt(0, () => station.Arrive(1));
        engine.ScheduleAt(2, () => station.Arrive(0));
        engine.RunToCompletion();

        ServiceStatistics high = station.Statistics(0);
        ServiceStatistics low = station.Statistics(1);

        Assert.Equal(1, station.Preemptions);
        Assert.Equal(0, high.AverageWait, tolerance: 1e-12);
        Assert.Equal(10, high.AverageSystemTime, tolerance: 1e-12);

        // Младшая отработала 2, ждала 10, дообслужила остаток 8: в системе 20, из них ожидания 10
        Assert.Equal(20, low.AverageSystemTime, tolerance: 1e-12);
        Assert.Equal(10, low.AverageWait, tolerance: 1e-12);
    }

    [Fact]
    public void Priority_NonPreemptive_OvertakesQueueButNotServer()
    {
        var engine = new SimulationEngine(seed: 1);
        var station = new ServiceStation(engine, discipline: QueueDiscipline.NonPreemptivePriority, classes: 2)
        {
            ServiceTime = () => 10,
        };

        engine.ScheduleAt(0, () => station.Arrive(1));
        engine.ScheduleAt(1, () => station.Arrive(1));
        engine.ScheduleAt(2, () => station.Arrive(0));
        engine.RunToCompletion();

        Assert.Equal(0, station.Preemptions);

        // Старшая ждёт окончания начатого обслуживания, но обходит младшую в очереди
        Assert.Equal(8, station.Statistics(0).AverageWait, tolerance: 1e-12);
        Assert.Equal((0 + 19) / 2.0, station.Statistics(1).AverageWait, tolerance: 1e-12);
    }

    [Fact]
    public void ServiceStation_RejectsUnknownClassAndMissingServiceTime()
    {
        var engine = new SimulationEngine(seed: 1);
        var station = new ServiceStation(engine, classes: 2) { ServiceTime = () => 1 };
        var bare = new ServiceStation(engine);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => station.Arrive(2));
        _ = Assert.Throws<InvalidOperationException>(() => bare.Arrive());
    }

    #endregion

    #region Обучение с подкреплением

    private static MarkovDecisionProcess TwoStateProcess() => new(
        [
            [[0.7, 0.3], [0.1, 0.9]],
            [[0.4, 0.6], [0.8, 0.2]],
        ],
        [[5.0, 1.0], [-1.0, 3.0]],
        discount: 0.9);

    private static MarkovDecisionProcess RandomProcess(int states, int actions, int seed)
    {
        var random = new Random(seed);
        var transitions = new double[states][][];
        var rewards = new double[states][];

        for (int state = 0; state < states; state++)
        {
            transitions[state] = new double[actions][];
            rewards[state] = new double[actions];

            for (int action = 0; action < actions; action++)
            {
                double[] row = Enumerable.Range(0, states).Select(_ => random.NextDouble()).ToArray();
                double sum = row.Sum();

                transitions[state][action] = row.Select(p => p / sum).ToArray();
                rewards[state][action] = (random.NextDouble() * 10) - 5;
            }
        }

        return new MarkovDecisionProcess(transitions, rewards, discount: 0.9);
    }

    [Fact]
    public void Mdp_Sample_FollowsTransitionProbabilities()
    {
        MarkovDecisionProcess process = TwoStateProcess();
        var random = new Random(5);
        const int Draws = 100_000;
        int toSecond = 0;

        for (int i = 0; i < Draws; i++)
        {
            (int next, double reward) = process.Sample(0, 0, random);

            Assert.Equal(5.0, reward);

            if (next == 1)
                toSecond++;
        }

        Assert.Equal(0.3, (double)toSecond / Draws, tolerance: 0.005);
    }

    [Theory]
    [InlineData(LearningMethod.QLearning)]
    [InlineData(LearningMethod.Sarsa)]
    public void Learning_FindsSamePolicyAsValueIteration(LearningMethod method)
    {
        MarkovDecisionProcess process = TwoStateProcess();
        MdpSolution exact = process.SolveByValueIteration();

        LearningResult learned = new TemporalDifferenceLearner { Method = method, Discount = process.Discount }
            .Train(new MdpEnvironment(process), episodes: 2_000, stepsPerEpisode: 50, seed: 3);

        // Обучающийся не видел ни вероятностей, ни наград — только исходы, — а пришёл к тому же
        Assert.Equal(1.0, learned.PolicyAgreement(exact.Policy));
        Assert.Equal(0, learned.UnvisitedPairs);

        for (int state = 0; state < process.StateCount; state++)
            Assert.Equal(exact.Values[state], learned.Value(state), tolerance: 0.1 * Math.Abs(exact.Values[state]));

        Assert.NotEmpty(learned.Interpret().Findings);
    }

    [Fact]
    public void QLearning_OnRandomProcess_LosesAlmostNothing()
    {
        MarkovDecisionProcess process = RandomProcess(8, 3, seed: 21);
        MdpSolution exact = process.SolveByValueIteration();

        LearningResult learned = new TemporalDifferenceLearner { Discount = process.Discount }
            .Train(new MdpEnvironment(process), episodes: 4_000, stepsPerEpisode: 100, seed: 4);

        double[] achieved = process.EvaluatePolicy(learned.Policy);

        // Потеря стратегии — насколько её точная ценность ниже оптимальной; масштаб — наибольшая
        // возможная ценность, 5/(1−γ)
        double scale = 5.0 / (1 - process.Discount);

        for (int state = 0; state < process.StateCount; state++)
        {
            double loss = exact.Values[state] - achieved[state];

            Assert.True(loss <= 0.01 * scale, $"Состояние {state}: потеря {loss:F4}");
        }
    }

    [Fact]
    public void Learning_ReportsStatesItNeverSaw()
    {
        // Второе состояние поглощающее: начав в нём, агент первого не увидит
        var process = new MarkovDecisionProcess(
            [
                [[1.0, 0.0], [0.0, 1.0]],
                [[0.0, 1.0], [0.0, 1.0]],
            ],
            [[0.0, 1.0], [2.0, 2.0]],
            discount: 0.9);

        LearningResult learned = new TemporalDifferenceLearner { Discount = 0.9 }
            .Train(new MdpEnvironment(process, startState: 1), episodes: 50, stepsPerEpisode: 20, seed: 1);

        Assert.Equal(2, learned.UnvisitedPairs);
        Assert.Contains(learned.Interpret().Warnings, w => w.Contains("непосещённых", StringComparison.Ordinal));
    }

    #endregion

    #region Пространство агентов

    [Fact]
    public void Grid_NeighbourCounts_DependOnNeighbourhoodAndEdges()
    {
        var mooreTorus = new GridSpace<string>(10, 10, torus: true, Neighbourhood.Moore);
        var neumannTorus = new GridSpace<string>(10, 10, torus: true, Neighbourhood.VonNeumann);
        var mooreFlat = new GridSpace<string>(10, 10, torus: false, Neighbourhood.Moore);
        var neumannFlat = new GridSpace<string>(10, 10, torus: false, Neighbourhood.VonNeumann);
        var narrow = new GridSpace<string>(2, 2, torus: true, Neighbourhood.Moore);

        Assert.Equal(8, mooreTorus.Neighbours(new Cell(0, 0)).Count);
        Assert.Equal(4, neumannTorus.Neighbours(new Cell(0, 0)).Count);
        Assert.Equal(3, mooreFlat.Neighbours(new Cell(0, 0)).Count);
        Assert.Equal(5, mooreFlat.Neighbours(new Cell(0, 5)).Count);
        Assert.Equal(2, neumannFlat.Neighbours(new Cell(9, 9)).Count);

        // На торе 2×2 все сдвиги приводят в три другие клетки — без повторов
        Assert.Equal(3, narrow.Neighbours(new Cell(0, 0)).Count);
    }

    [Fact]
    public void Grid_Distance_WrapsAroundTorus()
    {
        var moore = new GridSpace<string>(10, 10, torus: true, Neighbourhood.Moore);
        var neumann = new GridSpace<string>(10, 10, torus: true, Neighbourhood.VonNeumann);
        var flat = new GridSpace<string>(10, 10, torus: false, Neighbourhood.Moore);

        Assert.Equal(1, moore.Distance(new Cell(0, 0), new Cell(9, 9)));
        Assert.Equal(2, neumann.Distance(new Cell(0, 0), new Cell(9, 9)));
        Assert.Equal(9, flat.Distance(new Cell(0, 0), new Cell(9, 9)));
    }

    [Fact]
    public void Grid_PlaceMoveRemove_TrackNeighbours()
    {
        var grid = new GridSpace<string>(10, 10);

        grid.Place("а", new Cell(0, 0));
        grid.Place("б", new Cell(1, 1));
        grid.Place("в", new Cell(5, 5));

        Assert.Equal(["б"], grid.NeighbourAgents("а"));

        grid.Move("в", new Cell(9, 9));
        Assert.Equal(["б", "в"], grid.NeighbourAgents("а").OrderBy(a => a, StringComparer.Ordinal));

        Assert.True(grid.Remove("б"));
        Assert.Equal(["в"], grid.NeighbourAgents("а"));
        Assert.True(grid.IsEmpty(new Cell(1, 1)));
        Assert.Equal(2, grid.Count);
        _ = Assert.Throws<ArgumentException>(() => grid.Place("а", new Cell(3, 3)));
    }

    [Fact]
    public void Grid_RandomEmptyCell_FindsLastFreeCell()
    {
        var grid = new GridSpace<int>(3, 3);
        int agent = 0;

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                if (x != 2 || y != 1)
                    grid.Place(agent++, new Cell(x, y));
            }
        }

        var random = new Random(1);

        Assert.Equal(new Cell(2, 1), grid.RandomEmptyCell(random));

        grid.Place(agent, new Cell(2, 1));
        Assert.Null(grid.RandomEmptyCell(random));
    }

    [Fact]
    public void Network_Generators_HaveExpectedStructure()
    {
        Assert.Equal(45, ContactNetwork.Complete(10).EdgeCount);

        ContactNetwork ring = ContactNetwork.Ring(20, 4);
        Assert.All(Enumerable.Range(0, 20), node => Assert.Equal(4, ring.Degree(node)));
        Assert.True(ring.AreConnected(0, 19) && ring.AreConnected(0, 18) && !ring.AreConnected(0, 10));

        // Перекидывание связей меняет, куда они ведут, но не их число
        Assert.Equal(600, ContactNetwork.WattsStrogatz(200, 6, 0.1, new Random(1)).EdgeCount);

        ContactNetwork random = ContactNetwork.ErdosRenyi(400, 0.02, new Random(2));
        Assert.Equal(0.02 * 399, random.MeanDegree, tolerance: 0.6);

        // Безмасштабная сеть: у немногих узлов связей в разы больше среднего
        ContactNetwork scaleFree = ContactNetwork.BarabasiAlbert(1000, 2, new Random(3));
        Assert.Equal(3 + (997 * 2), scaleFree.EdgeCount);
        Assert.True(Enumerable.Range(0, 1000).Max(scaleFree.Degree) > 5 * scaleFree.MeanDegree);
    }

    /// <summary>
    /// Цепная модель Рида — Фроста на сети: каждый заразный за одно поколение заражает каждого
    /// восприимчивого соседа с заданной вероятностью и выздоравливает
    /// </summary>
    private static double ReedFrostFinalSize(ContactNetwork network, double transmissibility, int initialInfected, Random random)
    {
        int nodes = network.NodeCount;
        var contacts = new int[nodes][];

        for (int node = 0; node < nodes; node++)
            contacts[node] = network.Contacts(node);

        var infected = new bool[nodes];
        var current = new List<int>();

        while (current.Count < initialInfected)
        {
            int node = random.Next(nodes);

            if (!infected[node])
            {
                infected[node] = true;
                current.Add(node);
            }
        }

        int total = current.Count;

        while (current.Count > 0)
        {
            var next = new List<int>();

            foreach (int source in current)
            {
                foreach (int target in contacts[source])
                {
                    if (!infected[target] && random.NextDouble() < transmissibility)
                    {
                        infected[target] = true;
                        next.Add(target);
                    }
                }
            }

            total += next.Count;
            current = next;
        }

        return (double)total / nodes;
    }

    [Fact]
    public void Network_ReedFrostOnCompleteGraph_MatchesFinalSizeEquation()
    {
        const int Nodes = 400;
        const double R0 = 2.0;

        ContactNetwork everyone = ContactNetwork.Complete(Nodes);
        var random = new Random(11);

        double[] sizes = Enumerable.Range(0, 20)
            .Select(_ => ReedFrostFinalSize(everyone, R0 / Nodes, 5, random))
            .ToArray();

        // Малые вспышки, угасшие сразу, отделяются: уравнение описывает большую эпидемию
        double major = sizes.Where(size => size > 0.2).Average();

        // Полный граф — это и есть допущение «все встречаются со всеми», на котором стоит ОДУ-модель
        Assert.Equal(EpidemicModels.FinalEpidemicSize(R0), major, tolerance: 0.04);
    }

    [Fact]
    public void Network_Structure_ChangesEpidemicWithSameContactCount()
    {
        const int Nodes = 400;

        ContactNetwork ring = ContactNetwork.Ring(Nodes, 6);
        ContactNetwork random = ContactNetwork.ErdosRenyi(Nodes, 6.0 / (Nodes - 1), new Random(4));
        var rng = new Random(12);

        double onRing = Enumerable.Range(0, 20).Average(_ => ReedFrostFinalSize(ring, 0.4, 5, rng));
        double onRandom = Enumerable.Range(0, 20).Average(_ => ReedFrostFinalSize(random, 0.4, 5, rng));

        // Контактов у каждого поровну, но на кольце болезнь ходит только к соседям и глохнет,
        // а дальние связи случайной сети разносят её по всей популяции
        Assert.True(onRing < onRandom / 2, $"кольцо {onRing:F3}, случайная сеть {onRandom:F3}");
    }

    #endregion

    #region Планирование

    private static StripsProblem Blocks(string[] initial, string[] goal)
    {
        string[] blocks = ["A", "B", "C"];
        var actions = new List<StripsAction>();

        foreach (string block in blocks)
        {
            string[] places = [.. blocks.Where(b => b != block), "Стол"];

            foreach (string from in places)
            {
                foreach (string to in places)
                {
                    if (from == to)
                        continue;

                    var preconditions = new List<string> { $"на({block},{from})", $"свободен({block})" };
                    var add = new List<string> { $"на({block},{to})" };
                    var delete = new List<string> { $"на({block},{from})" };

                    if (from != "Стол")
                        add.Add($"свободен({from})");

                    if (to != "Стол")
                    {
                        preconditions.Add($"свободен({to})");
                        delete.Add($"свободен({to})");
                    }

                    actions.Add(new StripsAction($"{block}: {from} → {to}", preconditions, add, delete));
                }
            }
        }

        return new StripsProblem(initial, goal, actions);
    }

    [Fact]
    public void Strips_SussmanAnomaly_SolvedInThreeMoves()
    {
        // C лежит на A, B — на столе; нужно A на B, а B на C
        StripsProblem problem = Blocks(
            ["на(C,A)", "на(A,Стол)", "на(B,Стол)", "свободен(C)", "свободен(B)"],
            ["на(A,B)", "на(B,C)"]);

        Plan<FactState> plan = problem.Solve();

        Assert.True(plan.Found);
        Assert.True(plan.Optimal);
        Assert.Equal(["C: A → Стол", "B: Стол → C", "A: Стол → B"], plan.Actions);
        Assert.Equal(3, plan.Cost);
        Assert.True(problem.IsGoal(plan.States[^1]));
        Assert.Contains(plan.Interpret().Findings, f => f.Contains("оптимален", StringComparison.Ordinal));
    }

    [Fact]
    public void Strips_UnreachableGoal_IsProvenNotGuessed()
    {
        StripsProblem problem = Blocks(
            ["на(A,Стол)", "на(B,Стол)", "на(C,Стол)", "свободен(A)", "свободен(B)", "свободен(C)"],
            ["на(A,A)"]);

        Plan<FactState> plan = problem.Solve();

        // Все тринадцать расстановок трёх кубиков перебраны: недостижимость доказана, а не предположена
        Assert.False(plan.Found);
        Assert.False(plan.LimitReached);
        Assert.Equal(13, plan.Expanded);
    }

    [Fact]
    public void AStar_Heuristic_KeepsOptimalCostButExpandsLess()
    {
        const int Size = 30;
        (int X, int Y) start = (0, 0);
        (int X, int Y) goal = (Size - 1, 0);

        // Стена поперёк поля с проходом у нижнего края
        static bool Wall(int x, int y) => x == 15 && y < Size - 2;

        static IEnumerable<(string, (int X, int Y), double)> Moves((int X, int Y) p)
        {
            foreach ((int dx, int dy, string name) in new[] { (1, 0, "→"), (-1, 0, "←"), (0, 1, "↓"), (0, -1, "↑") })
            {
                int x = p.X + dx;
                int y = p.Y + dy;

                if (x >= 0 && x < Size && y >= 0 && y < Size && !Wall(x, y))
                    yield return (name, (x, y), 1.0);
            }
        }

        Plan<(int X, int Y)> blind = StateSpaceSearch.AStar(start, p => p == goal, Moves);
        Plan<(int X, int Y)> guided = StateSpaceSearch.AStar(start, p => p == goal, Moves,
            p => Math.Abs(p.X - goal.X) + Math.Abs(p.Y - goal.Y));

        // Обход стены: 29 вправо, 28 вниз до прохода и 28 обратно
        Assert.Equal(29 + (2 * 28), blind.Cost);
        Assert.Equal(blind.Cost, guided.Cost);
        Assert.True(guided.Expanded < blind.Expanded, $"с эвристикой {guided.Expanded}, без {blind.Expanded}");
    }

    [Fact]
    public void AStar_Limit_IsReportedAsUnknownNotImpossible()
    {
        Plan<int> plan = StateSpaceSearch.AStar(0, s => s == -1, s => new[] { ("+1", s + 1, 1.0) }, maxExpansions: 100);

        Assert.False(plan.Found);
        Assert.True(plan.LimitReached);
        Assert.Contains(plan.Interpret().Warnings, w => w.Contains("предел", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AStar_Limit_CountsOnlyRealExpansions()
    {
        // Предел в одно раскрытие: начальное состояние раскрывается, и цель в один шаг находится
        Plan<int> plan = StateSpaceSearch.AStar(0, s => s == 1, s => new[] { ("+1", s + 1, 1.0) }, maxExpansions: 1);

        Assert.True(plan.Found);
        Assert.Equal(1, plan.Expanded);
    }

    #endregion

    #region Крайние случаи и воспроизводимость

    [Fact]
    public void Priority_PreemptiveOnSeveralServers_MatchesMultiServerTheory()
    {
        // Формул Кобэма для нескольких приборов нет, но два точных факта остаются. Старший класс
        // при прерывании не видит младших и ведёт себя как M/M/c со своим потоком; а при равных
        // показательных длительностях число заявок в системе не зависит от дисциплины
        double[] rates = [0.6, 0.8];

        IReadOnlyDictionary<string, ReplicationEstimate> simulated = Replications.Run(10, seed =>
        {
            ServiceStation station = PriorityRun(seed, QueueDiscipline.PreemptiveResumePriority, rates, 1.0,
                warmup: 1_000, horizon: 20_000, servers: 2);

            return new Dictionary<string, double>
            {
                ["старший"] = station.Statistics(0).AverageWait,
                ["все"] = station.Statistics().AverageWait,
            };
        });

        double topAlone = QueueingTheory.MultiServer(0.6, 1.0, 2).WaitTime;
        double overall = QueueingTheory.MultiServer(1.4, 1.0, 2).WaitTime;

        Assert.Equal(topAlone, simulated["старший"].Mean, tolerance: (0.05 * topAlone) + 0.01);
        Assert.Equal(overall, simulated["все"].Mean, tolerance: 0.05 * overall);
    }

    [Fact]
    public void Priority_WithoutWaitingRoom_RejectsLikeErlangLoss()
    {
        // Без накопителя очереди нет, и приоритету нечего решать: доля отказов обоих классов —
        // формула Эрланга для одного прибора, a / (1 + a)
        ServiceStation station = PriorityRun(9, QueueDiscipline.NonPreemptivePriority, [0.3, 0.4], 1.0,
            warmup: 100, horizon: 100_000, capacity: 0);

        double blocking = 0.7 / 1.7;

        Assert.Equal(blocking, station.Statistics(0).RejectionRate, tolerance: 0.01);
        Assert.Equal(blocking, station.Statistics(1).RejectionRate, tolerance: 0.01);
        Assert.Equal(0, station.Statistics().MaxQueueLength);
    }

    [Fact]
    public void SameSeed_ReproducesEveryNewPart()
    {
        ServiceStatistics first = PriorityRun(5, QueueDiscipline.PreemptiveResumePriority, [0.3, 0.4], 1.0, 100, 5_000)
            .Statistics(1);
        ServiceStatistics second = PriorityRun(5, QueueDiscipline.PreemptiveResumePriority, [0.3, 0.4], 1.0, 100, 5_000)
            .Statistics(1);

        Assert.Equal(first, second);

        var learner = new TemporalDifferenceLearner { Discount = 0.9 };
        LearningResult a = learner.Train(new MdpEnvironment(TwoStateProcess()), 200, 20, seed: 8);
        LearningResult b = learner.Train(new MdpEnvironment(TwoStateProcess()), 200, 20, seed: 8);

        Assert.Equal(a.QValues[0], b.QValues[0]);
        Assert.Equal(a.QValues[1], b.QValues[1]);

        ContactNetwork one = ContactNetwork.BarabasiAlbert(300, 2, new Random(6));
        ContactNetwork two = ContactNetwork.BarabasiAlbert(300, 2, new Random(6));

        Assert.All(Enumerable.Range(0, 300), node => Assert.Equal(one.Contacts(node).Order(), two.Contacts(node).Order()));
    }

    [Fact]
    public void Grid_AgentsAt_IsSnapshotSafeToMoveWhileIterating()
    {
        var grid = new GridSpace<int>(5, 5);

        for (int agent = 0; agent < 4; agent++)
            grid.Place(agent, new Cell(2, 2));

        // Обычный шаг агентной модели: обойти клетку и разогнать её обитателей
        foreach (int agent in grid.AgentsAt(new Cell(2, 2)))
            grid.Move(agent, new Cell(agent, 0));

        Assert.True(grid.IsEmpty(new Cell(2, 2)));
        Assert.Equal(4, Enumerable.Range(0, 5).Count(x => !grid.IsEmpty(new Cell(x, 0))));
    }

    [Fact]
    public void EdgeCases_AreRejectedOrExplained()
    {
        var engine = new SimulationEngine(seed: 1);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => engine.Schedule(double.NaN, () => { }));

        var broken = new ServiceStation(engine) { ServiceTime = () => double.NaN };
        _ = Assert.Throws<InvalidOperationException>(() => broken.Arrive());

        var careless = new TemporalDifferenceLearner { InitialExploration = 1.5 };
        _ = Assert.Throws<InvalidOperationException>(() => careless.Train(new MdpEnvironment(TwoStateProcess()), 1, 1));

        var undefined = new TemporalDifferenceLearner { Discount = double.NaN };
        _ = Assert.Throws<InvalidOperationException>(() => undefined.Train(new MdpEnvironment(TwoStateProcess()), 1, 1));

        // Нулевое среднее: относительная точность не определена и не выдаётся за «бесконечность процентов»
        Interpretation zero = Replications.Estimate("Разность", [-1.0, 1.0]).Interpret();

        Assert.DoesNotContain(zero.Findings, f => f.Contains("бесконечность", StringComparison.Ordinal));
        Assert.DoesNotContain(zero.Metrics, m => m.Value.Contains("бесконечность", StringComparison.Ordinal));
    }

    #endregion
}

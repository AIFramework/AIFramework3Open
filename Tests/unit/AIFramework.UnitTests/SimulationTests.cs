using AI.Insights;
using AI.Simulation.Agents;
using AI.Simulation.DiscreteEvent;
using AI.Simulation.Markov;
using AI.Simulation.Queueing;
using AI.Simulation.SystemDynamics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Имитационное моделирование проверяется теорией: прогон системы массового обслуживания
/// обязан сойтись к аналитическим формулам, запасы — сохраняться, а марковский процесс —
/// давать одну и ту же стратегию при решении двумя разными методами.
/// </summary>
public class SimulationTests
{
    #region Дискретно-событийное ядро

    [Fact]
    public void Engine_ExecutesEventsInTimeOrder()
    {
        var engine = new SimulationEngine(seed: 1);
        var log = new List<double>();

        engine.Schedule(5, () => log.Add(engine.Now));
        engine.Schedule(1, () => log.Add(engine.Now));
        engine.Schedule(3, () => log.Add(engine.Now));

        engine.RunToCompletion();

        Assert.Equal([1.0, 3.0, 5.0], log);
        Assert.Equal(5.0, engine.Now);
        Assert.Equal(3, engine.ProcessedEvents);
    }

    [Fact]
    public void Engine_SimultaneousEvents_KeepSchedulingOrder()
    {
        var engine = new SimulationEngine();
        var log = new List<string>();

        engine.Schedule(2, () => log.Add("первое"));
        engine.Schedule(2, () => log.Add("второе"));
        engine.Schedule(2, () => log.Add("третье"));

        engine.RunToCompletion();

        // Одновременные события выполняются в порядке постановки — иначе прогон невоспроизводим
        Assert.Equal(["первое", "второе", "третье"], log);
    }

    [Fact]
    public void Engine_Run_StopsAtGivenTime()
    {
        var engine = new SimulationEngine();
        int executed = 0;

        for (int i = 1; i <= 10; i++)
            engine.Schedule(i, () => executed++);

        _ = engine.Run(4.5);

        Assert.Equal(4, executed);
        Assert.True(engine.HasEvents);
    }

    [Fact]
    public void Engine_RejectsEventsInThePast()
    {
        var engine = new SimulationEngine();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => engine.Schedule(-1, () => { }));
    }

    [Fact]
    public void Engine_SameSeed_GivesSameResult()
    {
        static double Run(int seed)
        {
            var engine = new SimulationEngine(seed);
            double total = 0;

            for (int i = 0; i < 100; i++)
                total += engine.Exponential(2.0);

            return total;
        }

        Assert.Equal(Run(42), Run(42), tolerance: 1e-12);
        Assert.NotEqual(Run(42), Run(43));
    }

    [Fact]
    public void TimeWeighted_AverageAccountsForDuration()
    {
        var accumulator = new TimeWeightedAccumulator();

        accumulator.Update(0, 10);   // десять единиц держатся одну секунду
        accumulator.Update(1, 0);    // затем ноль девять секунд

        // Среднее по времени равно единице, среднее по наблюдениям было бы пятёркой
        Assert.Equal(1.0, accumulator.Average(10), tolerance: 1e-12);
        Assert.Equal(10, accumulator.Maximum);
    }

    #endregion

    #region Согласие имитации с теорией

    [Fact]
    public void SingleServerQueue_SimulationMatchesTheory()
    {
        const double Arrival = 0.6;
        const double Service = 1.0;

        var engine = new SimulationEngine(seed: 20240830);
        var station = new ServiceStation(engine) { ServiceTime = () => engine.Exponential(Service) };

        void Arrive()
        {
            _ = station.Arrive();
            engine.Schedule(engine.Exponential(Arrival), Arrive);
        }

        engine.Schedule(engine.Exponential(Arrival), Arrive);
        _ = engine.Run(200_000);

        ServiceStatistics simulated = station.Statistics();
        QueueMetrics theory = QueueingTheory.SingleServer(Arrival, Service);

        // Имитация обязана сойтись к формулам M/M/1: это взаимная проверка модели и теории
        Assert.Equal(theory.Utilisation, simulated.Utilisation, tolerance: 0.02);
        Assert.Equal(theory.QueueLength, simulated.AverageQueueLength, tolerance: 0.1);
        Assert.Equal(theory.WaitTime, simulated.AverageWait, tolerance: 0.1);
        Assert.Equal(theory.SystemTime, simulated.AverageSystemTime, tolerance: 0.1);
    }

    [Fact]
    public void MultiServerQueue_SimulationMatchesTheory()
    {
        const double Arrival = 1.6;
        const double Service = 1.0;
        const int Servers = 2;

        var engine = new SimulationEngine(seed: 7);
        var station = new ServiceStation(engine, Servers) { ServiceTime = () => engine.Exponential(Service) };

        void Arrive()
        {
            _ = station.Arrive();
            engine.Schedule(engine.Exponential(Arrival), Arrive);
        }

        engine.Schedule(engine.Exponential(Arrival), Arrive);
        _ = engine.Run(200_000);

        ServiceStatistics simulated = station.Statistics();
        QueueMetrics theory = QueueingTheory.MultiServer(Arrival, Service, Servers);

        Assert.Equal(theory.Utilisation, simulated.Utilisation, tolerance: 0.03);
        Assert.Equal(theory.WaitTime, simulated.AverageWait, tolerance: 0.2);
    }

    [Fact]
    public void LimitedQueue_SimulationReproducesBlocking()
    {
        const double Arrival = 1.0;
        const double Service = 1.0;
        const int Capacity = 3;

        var engine = new SimulationEngine(seed: 99);
        var station = new ServiceStation(engine, servers: 1, capacity: Capacity - 1)
        {
            ServiceTime = () => engine.Exponential(Service),
        };

        void Arrive()
        {
            _ = station.Arrive();
            engine.Schedule(engine.Exponential(Arrival), Arrive);
        }

        engine.Schedule(engine.Exponential(Arrival), Arrive);
        _ = engine.Run(100_000);

        ServiceStatistics simulated = station.Statistics();
        (QueueMetrics _, double blocking) = QueueingTheory.LimitedQueue(Arrival, Service, Capacity);

        // При равных интенсивностях и трёх местах отказ получает четверть заявок
        Assert.Equal(0.25, blocking, tolerance: 1e-9);
        Assert.Equal(blocking, simulated.RejectionRate, tolerance: 0.02);
    }

    #endregion

    #region Теория массового обслуживания

    [Fact]
    public void Queueing_SingleServer_MatchesClosedForm()
    {
        QueueMetrics metrics = QueueingTheory.SingleServer(0.5, 1.0);

        Assert.Equal(0.5, metrics.Utilisation, tolerance: 1e-12);
        Assert.Equal(1.0, metrics.SystemLength, tolerance: 1e-12);
        Assert.Equal(0.5, metrics.QueueLength, tolerance: 1e-12);
        Assert.Equal(2.0, metrics.SystemTime, tolerance: 1e-12);
    }

    [Fact]
    public void Queueing_QueueGrowsNonlinearlyWithLoad()
    {
        double half = QueueingTheory.SingleServer(0.5, 1.0).SystemLength;
        double ninety = QueueingTheory.SingleServer(0.9, 1.0).SystemLength;
        double ninetyNine = QueueingTheory.SingleServer(0.99, 1.0).SystemLength;

        // Рост как ρ/(1−ρ): 1, 9, 99 — вот почему нельзя планировать загрузку под завязку
        Assert.Equal(1.0, half, tolerance: 1e-9);
        Assert.Equal(9.0, ninety, tolerance: 1e-9);
        Assert.Equal(99.0, ninetyNine, tolerance: 1e-9);
    }

    [Fact]
    public void Queueing_SaturatedSystem_IsRefused()
    {
        _ = Assert.Throws<ArgumentException>(() => QueueingTheory.SingleServer(1.0, 1.0));
        _ = Assert.Throws<ArgumentException>(() => QueueingTheory.SingleServer(1.5, 1.0));
    }

    [Fact]
    public void Queueing_LittleLaw_HoldsForAllModels()
    {
        QueueMetrics single = QueueingTheory.SingleServer(0.7, 1.0);
        QueueMetrics multi = QueueingTheory.MultiServer(1.8, 1.0, 2);

        Assert.Equal(single.SystemLength, QueueingTheory.LittleLaw(0.7, single.SystemTime), tolerance: 1e-9);
        Assert.Equal(multi.SystemLength, QueueingTheory.LittleLaw(1.8, multi.SystemTime), tolerance: 1e-9);
    }

    [Fact]
    public void Queueing_MultiServer_BeatsTwoSeparateQueues()
    {
        // Общая очередь к двум приборам лучше двух отдельных очередей — известный результат
        QueueMetrics shared = QueueingTheory.MultiServer(1.6, 1.0, 2);
        QueueMetrics separate = QueueingTheory.SingleServer(0.8, 1.0);

        Assert.True(shared.WaitTime < separate.WaitTime);
    }

    [Fact]
    public void Queueing_ErlangB_MatchesReferenceValue()
    {
        // Нагрузка 10 эрлангов на 10 приборов даёт около 21 процента отказов
        Assert.Equal(0.2146, QueueingTheory.ErlangB(10, 10), tolerance: 1e-4);
        Assert.Equal(0.5, QueueingTheory.ErlangB(1, 1), tolerance: 1e-12);
    }

    [Fact]
    public void Interpret_Queue_WarnsAboutHighLoad()
    {
        Interpretation interpretation = QueueingTheory.SingleServer(0.95, 1.0).Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("1/(1−ρ)", StringComparison.Ordinal));
        Assert.Contains(interpretation.Metrics, m => m.Name == "ρ" && m.Quality == MetricQuality.Critical);
    }

    #endregion

    #region Системная динамика

    [Fact]
    public void StockFlow_ExponentialGrowth_MatchesAnalyticSolution()
    {
        var model = new StockFlowModel("Рост вклада");
        _ = model.AddStock("капитал", 1000, (_, levels) => 0.05 * levels[0]);

        IReadOnlyList<SystemState> states = model.Run(finalTime: 10, points: 101);

        Assert.Equal(1000 * Math.Exp(0.5), model.Final(states, "капитал"), tolerance: 1e-6);
    }

    [Fact]
    public void StockFlow_ConservesTotalInClosedSystem()
    {
        // Переток между двумя запасами: сумма обязана сохраняться
        var model = new StockFlowModel("Переток");

        _ = model.AddStock("первый", 100, (_, levels) => -0.3 * levels[0]);
        _ = model.AddStock("второй", 0, (_, levels) => 0.3 * levels[0]);

        IReadOnlyList<SystemState> states = model.Run(finalTime: 20, points: 201);

        foreach (SystemState state in states)
            Assert.Equal(100.0, state.Levels[0] + state.Levels[1], tolerance: 1e-6);
    }

    [Fact]
    public void StockFlow_NegativeFeedback_ReachesEquilibrium()
    {
        // Наполнение с оттоком, пропорциональным уровню: равновесие на притоке, делённом на отток
        var model = new StockFlowModel("Бак");
        _ = model.AddStock("уровень", 0, (_, levels) => 10 - (0.5 * levels[0]));

        IReadOnlyList<SystemState> states = model.Run(finalTime: 50, points: 101);

        Assert.Equal(20.0, model.Final(states, "уровень"), tolerance: 1e-6);
    }

    [Fact]
    public void StockFlow_RejectsUnknownStock()
    {
        var model = new StockFlowModel();
        _ = model.AddStock("а", 1, (_, _) => 0);

        _ = Assert.Throws<KeyNotFoundException>(() => model.IndexOf("б"));
        _ = Assert.Throws<ArgumentException>(() => model.AddStock("а", 2, (_, _) => 0));
    }

    #endregion

    #region Марковские процессы

    [Fact]
    public void Mdp_ValueIteration_FindsObviousPolicy()
    {
        // Два состояния: в первом действие 1 ведёт во второе с наградой, действие 0 остаётся
        double[][][] transitions =
        [
            [[1.0, 0.0], [0.0, 1.0]],
            [[0.0, 1.0], [0.0, 1.0]],
        ];

        double[][] rewards = [[0.0, 1.0], [2.0, 2.0]];

        var process = new MarkovDecisionProcess(transitions, rewards, discount: 0.9);
        MdpSolution solution = process.SolveByValueIteration();

        Assert.True(solution.Converged);
        Assert.Equal(1, solution.Policy[0]);

        // Ценность второго состояния — сумма геометрической прогрессии наград: 2/(1−0.9)
        Assert.Equal(20.0, solution.Values[1], tolerance: 1e-6);
        Assert.Equal(1 + (0.9 * 20.0), solution.Values[0], tolerance: 1e-6);
    }

    [Fact]
    public void Mdp_PolicyIteration_AgreesWithValueIteration()
    {
        double[][][] transitions =
        [
            [[0.7, 0.3], [0.1, 0.9]],
            [[0.4, 0.6], [0.8, 0.2]],
        ];

        double[][] rewards = [[5.0, 1.0], [-1.0, 3.0]];

        var process = new MarkovDecisionProcess(transitions, rewards, discount: 0.9);

        MdpSolution byValue = process.SolveByValueIteration();
        MdpSolution byPolicy = process.SolveByPolicyIteration();

        Assert.Equal(byValue.Policy, byPolicy.Policy);

        for (int state = 0; state < process.StateCount; state++)
            Assert.Equal(byValue.Values[state], byPolicy.Values[state], tolerance: 1e-6);
    }

    [Fact]
    public void Mdp_DiscountAffectsPatience()
    {
        // Действие 0 даёт награду сразу, действие 1 ведёт в богатое состояние
        double[][][] transitions =
        [
            [[1.0, 0.0], [0.0, 1.0]],
            [[0.0, 1.0], [0.0, 1.0]],
        ];

        double[][] rewards = [[3.0, 0.0], [5.0, 5.0]];

        var shortSighted = new MarkovDecisionProcess(transitions, rewards, discount: 0.1);
        var farSighted = new MarkovDecisionProcess(transitions, rewards, discount: 0.95);

        // При малом дисконте выгоднее взять награду сразу, при большом — потерпеть
        Assert.Equal(0, shortSighted.SolveByValueIteration().Policy[0]);
        Assert.Equal(1, farSighted.SolveByValueIteration().Policy[0]);
    }

    [Fact]
    public void Mdp_RejectsInvalidTransitions()
    {
        double[][][] broken = [[[0.5, 0.2]]];
        double[][] rewards = [[1.0]];

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new MarkovDecisionProcess(broken, rewards));

        Assert.Contains("вместо единицы", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mdp_RejectsDiscountOutsideRange()
    {
        double[][][] transitions = [[[1.0]]];
        double[][] rewards = [[1.0]];

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new MarkovDecisionProcess(transitions, rewards, 1.0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new MarkovDecisionProcess(transitions, rewards, 0));
    }

    #endregion

    #region Агентное моделирование

    private sealed class Walker : IAgent<List<int>>
    {
        internal int Position;

        public void Step(List<int> world, Random random)
        {
            Position += random.Next(2) == 0 ? -1 : 1;
            world.Add(Position);
        }
    }

    [Fact]
    public void AgentModel_RunsEveryAgentEachStep()
    {
        var world = new List<int>();
        var model = new AgentBasedModel<List<int>>(world, seed: 5);

        for (int i = 0; i < 10; i++)
            model.Add(new Walker());

        int steps = model.Run(20);

        Assert.Equal(20, steps);
        Assert.Equal(20, model.Steps);
        Assert.Equal(200, world.Count);
    }

    [Fact]
    public void AgentModel_SameSeed_ReproducesRun()
    {
        static int Run(int seed)
        {
            var world = new List<int>();
            var model = new AgentBasedModel<List<int>>(world, seed);

            for (int i = 0; i < 5; i++)
                model.Add(new Walker());

            _ = model.Run(50);

            return world.Sum();
        }

        Assert.Equal(Run(11), Run(11));
    }

    [Fact]
    public void AgentModel_StopWhen_HaltsEarly()
    {
        var world = new List<int>();
        var model = new AgentBasedModel<List<int>>(world, seed: 3)
        {
            StopWhen = m => m.Steps >= 7,
        };

        model.Add(new Walker());

        Assert.Equal(7, model.Run(100));
    }

    [Fact]
    public void AgentModel_AfterStep_CollectsMetrics()
    {
        var world = new List<int>();
        var history = new List<int>();

        var model = new AgentBasedModel<List<int>>(world, seed: 1)
        {
            AfterStep = m => history.Add(m.Steps),
        };

        model.Add(new Walker());
        _ = model.Run(4);

        Assert.Equal([1, 2, 3, 4], history);
    }

    #endregion
}

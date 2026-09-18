using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Simulation.DiscreteEvent;
using AI.Simulation.Experiments;
using AI.Simulation.Learning;
using AI.Simulation.Markov;
using AI.Simulation.Planning;
using AI.Simulation.Queueing;
using AI.Simulation.Social;
using AI.Simulation.Space;
using AI.Simulation.Stochastic;
using AI.Simulation.SystemDynamics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>sim</c>: имитационное моделирование.
/// </summary>
/// <remarks>
/// Всё случайное здесь берёт зерно из <c>options.seed</c>: повторный прогон даёт тот же путь,
/// ту же очередь и тот же исход голосования, и отличие двух прогонов — это отличие во входе.
/// <para>
/// Агентные модели и сетка из <c>AI.Simulation</c> сюда не вошли: агент — объект с поведением,
/// и выразить его в языке без классов значило бы написать второй язык внутри первого. Фильтр
/// частиц не вошёл по близкой причине — он обобщён по типу состояния, а линейный гауссов случай
/// уже закрывают <c>ts.state_space</c> и <c>ctrl.kalman</c>. Сеть контактов передаётся матрицей
/// смежности: её умеют печатать, резать и рисовать без нового типа.
/// </para>
/// </remarks>
[ScriptModule("sim", "Симуляция: очереди, Монте-Карло, MDP, мнения, системная динамика", Version = "0.1", Group = "решатели")]
public static class SimModule
{
    // --- очереди ---

    /// <summary>
    /// Очередь по формулам теории массового обслуживания.
    /// </summary>
    /// <remarks>
    /// Главное в ответе — нелинейность: при загрузке 0.5 в очереди одна заявка, при 0.9 — девять.
    /// Формулы точны для простейшего потока и показательного обслуживания; для остального есть
    /// <c>sim.station</c>, и сравнение двух ответов показывает, насколько допущения важны.
    /// </remarks>
    [ScriptFn("queue", "Очередь по формулам: загрузка, длина, ожидание для M/M/c и M/M/1/K",
        Example = "let q = sim.queue(arrival: 8, service: 10, servers: 1)")]
    public static ScriptRecord Queue(
        [ScriptParam("интенсивность потока заявок в единицу времени")] double arrival,
        [ScriptParam("интенсивность обслуживания одним прибором")] double service,
        [ScriptParam("число приборов")] int servers = 1,
        [ScriptParam("мест в системе; 0 — без ограничения")] int capacity = 0)
    {
        const string function = "sim.queue";

        RequireRates(arrival, service, servers, capacity, function);

        if (capacity > 0)
        {
            ScriptData.Require(servers == 1, $"{function}: ограниченная очередь считается для одного прибора");

            (QueueMetrics limited, double blocking) = QueueingTheory.LimitedQueue(arrival, service, capacity);

            return QueueRecord(limited, blocking);
        }

        if (arrival >= servers * service)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: поток {arrival} не меньше пропускной способности {servers * service} — очередь растёт без предела",
                "добавьте приборы (servers) либо ограничьте места (capacity)");
        }

        QueueMetrics metrics = servers == 1
            ? QueueingTheory.SingleServer(arrival, service)
            : QueueingTheory.MultiServer(arrival, service, servers);

        return QueueRecord(metrics, 0);
    }

    /// <summary>
    /// Дискретно-событийная имитация станции обслуживания.
    /// </summary>
    /// <remarks>
    /// Прибытия и обслуживание — показательные, как в <c>sim.queue</c>: при одинаковых входах
    /// имитация обязана сойтись к формулам, и это взаимная проверка модели и теории. Разгон
    /// (<c>warmup</c>) отбрасывает начало, когда система ещё пуста и ожидание занижено.
    /// </remarks>
    [ScriptFn("station", "Имитация станции обслуживания: очередь по событиям, а не по формулам",
        Example = "let s = sim.station(arrival: 8, service: 10, servers: 2, until: 10000)")]
    public static ScriptRecord Station(
        IScriptContext context,
        [ScriptParam("интенсивность потока заявок в единицу времени")] double arrival,
        [ScriptParam("интенсивность обслуживания одним прибором")] double service,
        [ScriptParam("число приборов")] int servers = 1,
        [ScriptParam("мест в системе; 0 — без ограничения")] int capacity = 0,
        [ScriptParam("длительность имитации")] double until = 10000,
        [ScriptParam("разгон: время, статистика до которого отбрасывается")] double warmup = 0)
    {
        const string function = "sim.station";

        RequireRates(arrival, service, servers, capacity, function);
        ScriptData.Require(until > 0 && warmup >= 0 && warmup < until, $"{function}: нужно 0 ≤ warmup < until");
        ScriptData.Require(arrival * until <= 5_000_000,
            $"{function}: около {arrival * until:G3} прибытий — слишком долгая имитация; уменьшите until");

        var engine = new SimulationEngine(context.Random.Next());
        var station = new ServiceStation(engine, servers, capacity > 0 ? capacity : int.MaxValue)
        {
            ServiceTime = () => engine.Exponential(service),
        };

        void Arrive()
        {
            _ = station.Arrive();
            engine.Schedule(engine.Exponential(arrival), Arrive);
        }

        engine.Schedule(engine.Exponential(arrival), Arrive);

        if (warmup > 0)
        {
            _ = engine.Run(warmup);
            station.ResetStatistics();
        }

        _ = engine.Run(until);

        ServiceStatistics statistics = station.Statistics();

        return ScriptData.Record(statistics,
            ("arrivals", statistics.Arrivals),
            ("served", statistics.Served),
            ("rejected", statistics.Rejected),
            ("rejection_rate", statistics.RejectionRate),
            ("utilisation", statistics.Utilisation),
            ("queue_length", statistics.AverageQueueLength),
            ("max_queue", statistics.MaxQueueLength),
            ("wait", statistics.AverageWait),
            ("system_time", statistics.AverageSystemTime));
    }

    // --- опыты ---

    /// <summary>
    /// Повторы стохастического опыта.
    /// </summary>
    /// <remarks>
    /// Функция получает номер повтора и возвращает число; случайность внутри неё берётся из
    /// прогона, поэтому повторы независимы между собой и воспроизводимы все вместе. Одно число
    /// из одного прогона ничего не говорит о разбросе — интервал говорит. С <c>target</c>
    /// возвращается, сколько повторов нужно для интервала такой полуширины.
    /// </remarks>
    [ScriptFn("replicate", "Повторы стохастического опыта: среднее и доверительный интервал",
        Example = "let r = sim.replicate(i => stat.mean(signal.noise(100, sigma: 1)), n: 50)")]
    public static ScriptRecord Replicate(
        IScriptContext context,
        [ScriptParam("опыт: функция номера повтора, возвращающая число")] ScriptCallable f,
        [ScriptParam("число повторов")] int n = 30,
        [ScriptParam("уровень доверия")] double confidence = 0.95,
        [ScriptParam("желаемая полуширина интервала; 0 — не считать")] double target = 0)
    {
        const string function = "sim.replicate";

        ScriptData.Require(n >= 2, $"{function}: повторов нужно хотя бы два — иначе разброс не оценить");
        ScriptData.Require(confidence is > 0 and < 1, $"{function}: уровень доверия лежит строго между 0 и 1");
        ScriptData.Require(target >= 0, $"{function}: полуширина не может быть отрицательной");

        var values = new double[n];

        for (int i = 0; i < n; i++)
            values[i] = ScriptCallbacks.Number(context, f, $"{function}: значение опыта", i);

        ReplicationEstimate estimate = Replications.Estimate("опыт", values, confidence);

        return ScriptData.Record(estimate,
            ("mean", estimate.Mean),
            ("std", estimate.StandardDeviation),
            ("lower", estimate.Lower),
            ("upper", estimate.Upper),
            ("half_width", estimate.HalfWidth),
            ("relative_precision", estimate.RelativePrecision),
            ("n", n),
            ("required", target > 0 ? Replications.RequiredReplications(estimate, target) : double.NaN),
            ("values", new Vector(values)));
    }

    // --- решения ---

    /// <summary>
    /// Марковский процесс принятия решений.
    /// </summary>
    /// <remarks>
    /// Модель — таблица переходов <c>state, action, next, p, reward</c>: строка на исход действия.
    /// Так её можно прочитать из журнала или собрать конвейером. Состояние, из которого не
    /// выходит ни одного действия, — поглощающее: остаётся в себе с нулевой наградой.
    /// </remarks>
    [ScriptFn("mdp", "Марковский процесс решений: лучшая стратегия и ценность состояний",
        Example = "let best = sim.mdp(model, discount: 0.9)")]
    public static ScriptRecord Mdp(
        [ScriptParam("переходы: state, action, next, p, reward")] ScriptTable transitions,
        [ScriptParam("дисконт будущих наград, от 0 до 1")] double discount = 0.95,
        [ScriptParam("метод: \"value\" — итерация ценности, \"policy\" — итерация стратегии")] string kind = "value")
    {
        const string function = "sim.mdp";

        bool policyIteration = ScriptData.Kind<bool>(kind, "kind", function, ("value", false), ("policy", true));
        MdpTable model = ReadMdp(transitions, discount, function);
        MdpSolution solution = policyIteration
            ? model.Process.SolveByPolicyIteration()
            : model.Process.SolveByValueIteration();

        return ScriptData.Record(solution,
            ("policy", PolicyTable(model, solution.Policy, state => solution.Values[state])),
            ("iterations", solution.Iterations),
            ("residual", solution.Residual),
            ("converged", solution.Converged));
    }

    /// <summary>
    /// Обучение с подкреплением на модели MDP.
    /// </summary>
    /// <remarks>
    /// Агент не видит таблицу переходов — только исходы своих действий, как в жизни. Поле
    /// <c>agreement</c> сравнивает выученную стратегию с точным решением той же модели: доля
    /// состояний, где они совпали. Это и есть проверка, научился ли агент, а не только сколько
    /// эпизодов прошло.
    /// </remarks>
    [ScriptFn("learn", "Обучение с подкреплением на модели MDP: Q-learning или SARSA",
        Example = "let agent = sim.learn(model, episodes: 2000, kind: \"sarsa\")")]
    public static ScriptRecord Learn(
        IScriptContext context,
        [ScriptParam("переходы: state, action, next, p, reward")] ScriptTable transitions,
        [ScriptParam("число эпизодов")] int episodes = 1000,
        [ScriptParam("шагов в эпизоде")] int steps = 100,
        [ScriptParam("метод: \"q_learning\" либо \"sarsa\"")] string kind = "q_learning",
        [ScriptParam("дисконт будущих наград, от 0 до 1")] double discount = 0.95,
        [ScriptParam("начальная доля случайных действий")] double exploration = 0.2)
    {
        const string function = "sim.learn";

        LearningMethod method = ScriptData.Kind<LearningMethod>(kind, "kind", function,
            ("q_learning", LearningMethod.QLearning),
            ("sarsa", LearningMethod.Sarsa));

        ScriptData.Require(episodes >= 1 && steps >= 1, $"{function}: эпизодов и шагов — хотя бы по одному");
        ScriptData.Require((long)episodes * steps <= 10_000_000, $"{function}: больше десяти миллионов шагов — уменьшите episodes или steps");
        ScriptData.Require(exploration is >= 0 and <= 1, $"{function}: доля случайных действий лежит в [0, 1]");

        MdpTable model = ReadMdp(transitions, discount, function);
        var learner = new TemporalDifferenceLearner
        {
            Method = method,
            Discount = discount,
            InitialExploration = exploration,
        };

        LearningResult result = learner.Train(new MdpEnvironment(model.Process), episodes, steps, context.Random.Next());
        IReadOnlyList<int> exact = model.Process.SolveByValueIteration().Policy;

        return ScriptData.Record(result,
            ("policy", PolicyTable(model, result.Policy, result.Value)),
            ("agreement", result.PolicyAgreement(exact)),
            ("unvisited", result.UnvisitedPairs),
            ("final_exploration", result.FinalExploration),
            ("episodes", result.Episodes));
    }

    /// <summary>
    /// Планирование в постановке STRIPS.
    /// </summary>
    /// <remarks>
    /// Действие — запись <c>{ name, pre, add, delete, cost }</c>: предусловия, добавляемые и
    /// удаляемые факты, стоимость. Лишнее поле в записи — отказ: опечатка в <c>delete</c> иначе
    /// оставила бы факт на месте, и план выходил бы «найденным» в мире, которого нет.
    /// </remarks>
    [ScriptFn("plan", "Планирование STRIPS: цепочка действий от начальных фактов к цели",
        Example = "let route = sim.plan([\"at_home\"], [\"at_work\"], actions)")]
    public static ScriptRecord MakePlan(
        [ScriptParam("начальные факты")] string[] initial,
        [ScriptParam("факты цели")] string[] goal,
        [ScriptParam("действия: записи { name, pre, add, delete, cost }")] ScriptList actions,
        [ScriptParam("предел раскрытых состояний")] int max_expansions = 1000000)
    {
        const string function = "sim.plan";

        ScriptData.Require(goal.Length > 0, $"{function}: цель пуста");
        ScriptData.Require(max_expansions >= 1, $"{function}: раскрытий — хотя бы одно");

        var parsed = new List<StripsAction>(actions.Count);

        for (int i = 0; i < actions.Count; i++)
        {
            ScriptRecord action = actions[i].AsRecord($"actions[{i}]");

            foreach (string key in action.Keys)
            {
                if (key is "name" or "pre" or "add" or "delete" or "cost") continue;

                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"{function}: в действии {i} поле '{key}'",
                    "поля действия: name, pre, add, delete, cost");
            }

            ScriptData.Require(action.TryGet("name", out ScriptValue name), $"{function}: у действия {i} нет name");

            parsed.Add(new StripsAction(
                name.AsString($"actions[{i}].name"),
                Facts(action, "pre", i),
                Facts(action, "add", i),
                Facts(action, "delete", i),
                action.TryGet("cost", out ScriptValue cost) ? cost.AsNumber($"actions[{i}].cost") : 1.0));
        }

        Plan<FactState> plan = new StripsProblem(initial, goal, parsed).Solve(max_expansions);

        return ScriptData.Record(plan,
            ("found", plan.Found),
            ("actions", plan.Actions.ToArray()),
            ("length", plan.Length),
            ("cost", plan.Cost),
            ("expanded", plan.Expanded),
            ("optimal", plan.Optimal),
            ("limit_reached", plan.LimitReached));
    }

    // --- мнения ---

    /// <summary>
    /// Динамика мнений с ограниченным доверием.
    /// </summary>
    /// <remarks>
    /// Агент слушает только тех, чьё мнение отличается от его собственного не больше чем на
    /// <c>confidence</c>. Малое доверие раскалывает общество на кластеры, большое сводит к
    /// согласию; таблица <c>clusters</c> показывает, на какие. Модель Деффуана идёт парами
    /// случайных встреч и может ограничиться сетью контактов (<c>network</c>).
    /// </remarks>
    [ScriptFn("opinions", "Динамика мнений с ограниченным доверием: согласие или расколы",
        Example = "let o = sim.opinions(vec.linspace(0, 1, n: 100), confidence: 0.2)")]
    public static ScriptRecord Opinions(
        IScriptContext context,
        [ScriptParam("начальные мнения на отрезке [0, 1]")] Vector initial,
        [ScriptParam("порог доверия: с кем агент вообще говорит")] double confidence,
        [ScriptParam("модель: \"hk\" — Хегсельман — Краузе, \"deffuant\" — случайные пары")] string kind = "hk",
        [ScriptParam("для deffuant: доля сближения при встрече, до 0.5")] double convergence = 0.5,
        [ScriptParam("для deffuant: число встреч")] int interactions = 100000,
        [ScriptParam("для deffuant: матрица смежности сети; по умолчанию все со всеми")] Matrix? network = null)
    {
        const string function = "sim.opinions";

        bool deffuant = ScriptData.Kind<bool>(kind, "kind", function, ("hk", false), ("deffuant", true));

        ScriptData.Require(initial.Count >= 2, $"{function}: агентов нужно хотя бы два");
        ScriptData.Require(confidence > 0, $"{function}: порог доверия должен быть положительным");

        if (!deffuant && network != null)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{function}: сеть учитывает только kind: \"deffuant\"");

        OpinionDynamicsResult result = deffuant
            ? BoundedConfidence.Deffuant(initial, confidence, convergence, interactions, context.Random,
                network == null ? null : Network(network, initial.Count, function))
            : BoundedConfidence.HegselmannKrause(initial, confidence);

        return ScriptData.Record(result,
            ("model", result.Model),
            ("consensus", result.IsConsensus),
            ("clusters", ScriptData.Table(result.Clusters,
                ("position", c => c.Position),
                ("size", c => c.Size))),
            ("major_clusters", result.MajorClusterCount()),
            ("opinions", new Vector([.. result.Opinions])),
            ("steps", (double)result.Steps),
            ("converged", result.Converged),
            ("initial_mean", result.InitialMean),
            ("final_mean", result.FinalMean));
    }

    /// <summary>
    /// Влияние в сети по модели ДеГрута.
    /// </summary>
    /// <remarks>
    /// Строка матрицы — кого и насколько слушает агент, строки в сумме дают единицу. Ответ —
    /// придёт ли группа к согласию, чей голос в нём весомее (<c>social_power</c>) и за сколько
    /// шагов. С <c>self_weight</c> матрица читается как смежность сети из <c>sim.network</c>:
    /// агент доверяет себе эту долю, а остальное делит поровну между соседями.
    /// </remarks>
    [ScriptFn("influence", "Влияние по ДеГруту: придёт ли группа к согласию и чей голос весомее",
        Example = "let g = sim.influence(weights, opinions: <0.2, 0.9, 0.5>)")]
    public static ScriptRecord Influence(
        [ScriptParam("матрица доверия: строки в сумме дают 1")] Matrix weights,
        [ScriptParam("начальные мнения — чтобы узнать, к чему придут")] Vector? opinions = null,
        [ScriptParam("доля доверия себе; больше 0 — матрица читается как смежность")] double self_weight = 0)
    {
        const string function = "sim.influence";

        Matrix influence = self_weight > 0
            ? InfluenceMatrix.FromNetwork(Network(weights, weights.Height, function), self_weight)
            : weights;

        if (!InfluenceMatrix.IsRowStochastic(influence))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: строки матрицы доверия не дают в сумме единицу",
                "нормируйте строки либо передайте смежность сети с self_weight: 0.5");
        }

        var model = new DeGrootModel(influence);
        InfluenceAnalysis analysis = model.Analyze();

        if (opinions != null && opinions.Count != model.Size)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{function}: мнений {opinions.Count}, а агентов {model.Size}");
        }

        return ScriptData.Record(analysis,
            ("consensus", analysis.ReachesConsensus),
            ("strongly_connected", analysis.IsStronglyConnected),
            ("period", analysis.Period),
            ("social_power", analysis.SocialPower == null ? new Vector(0) : new Vector([.. analysis.SocialPower])),
            ("effective_voices", analysis.EffectiveVoices),
            ("second_eigenvalue", analysis.SecondEigenvalueModulus),
            ("steps_to_hundredfold", analysis.StepsToHundredfold),
            ("consensus_value", opinions != null && analysis.ReachesConsensus ? model.Consensus(opinions) : double.NaN));
    }

    /// <summary>
    /// Модель Фридкина — Джонсена: равновесие мнений с упрямством.
    /// </summary>
    /// <remarks>
    /// В отличие от ДеГрута, агент не забывает исходного мнения: восприимчивость 0 — слушает
    /// только себя прежнего, 1 — только других. Поэтому согласия обычно нет, и ответ —
    /// равновесные мнения, которые реальные опросы воспроизводят лучше, чем полный консенсус.
    /// </remarks>
    [ScriptFn("friedkin_johnsen", "Мнения с упрямством: равновесие, где исходные взгляды не забыты",
        Example = "let settled = sim.friedkin_johnsen(weights, <0.5, 0.5, 0.9>, <0, 1, 0.5>)")]
    public static Vector FriedkinJohnsen(
        [ScriptParam("матрица доверия: строки в сумме дают 1")] Matrix weights,
        [ScriptParam("восприимчивость каждого агента, от 0 до 1")] Vector susceptibility,
        [ScriptParam("исходные мнения, которых агент держится")] Vector prejudices)
    {
        const string function = "sim.friedkin_johnsen";

        if (susceptibility.Count != weights.Height || prejudices.Count != weights.Height)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{function}: агентов {weights.Height}, восприимчивостей {susceptibility.Count}, мнений {prejudices.Count}");
        }

        return new Vector(new FriedkinJohnsenModel(weights, susceptibility).Equilibrium(prejudices));
    }

    /// <summary>
    /// Модель избирателя на сети.
    /// </summary>
    /// <remarks>
    /// Агент на каждом шаге перенимает мнение случайного соседа. Вероятность прийти к единодушию
    /// «за» считается точно, а сам исход — имитацией; на сети без связности единодушия нет вовсе.
    /// </remarks>
    [ScriptFn("voter", "Модель избирателя на сети: вероятность и исход единодушия",
        Example = "let vote = sim.voter(graph, opinions)")]
    public static ScriptRecord Voter(
        IScriptContext context,
        [ScriptParam("матрица смежности сети")] Matrix network,
        [ScriptParam("мнения агентов: 1 — за, 0 — против")] Vector opinions,
        [ScriptParam("предел числа обновлений")] int max_updates = 10000000)
    {
        const string function = "sim.voter";

        ContactNetwork graph = Network(network, opinions.Count, function);
        var votes = new bool[opinions.Count];

        for (int i = 0; i < votes.Length; i++)
        {
            ScriptData.Require(opinions[i] is 0 or 1, $"{function}: мнение агента {i} — {opinions[i]}, а ждутся 0 и 1");
            votes[i] = opinions[i] == 1;
        }

        VoterOutcome outcome = VoterModel.Run(graph, votes, context.Random, max_updates);

        return ScriptData.Plain(
            ("consensus_probability", VoterModel.ConsensusProbability(graph, votes)),
            ("outcome", outcome.Consensus is bool consensus ? (consensus ? 1.0 : 0.0) : double.NaN),
            ("reached", outcome.Consensus.HasValue),
            ("updates", (double)outcome.Updates));
    }

    /// <summary>
    /// Случайная сеть контактов.
    /// </summary>
    /// <remarks>
    /// Сеть отдаётся симметричной матрицей смежности из нулей и единиц. Параметры, которых
    /// выбранная модель не использует, не влияют ни на что: <c>neighbors</c> нужен кольцу и
    /// малому миру, <c>probability</c> — Эрдёшу — Реньи, <c>rewiring</c> — малому миру,
    /// <c>links</c> — Барабаши — Альберт.
    /// </remarks>
    [ScriptFn("network", "Случайная сеть контактов матрицей смежности",
        Example = "let graph = sim.network(100, kind: \"watts_strogatz\", neighbors: 4, rewiring: 0.1)")]
    public static Matrix NetworkOf(
        IScriptContext context,
        [ScriptParam("число узлов")] int nodes,
        [ScriptParam("модель: complete, ring, erdos_renyi, watts_strogatz, barabasi_albert")] string kind = "complete",
        [ScriptParam("соседей у узла: кольцо и малый мир")] int neighbors = 2,
        [ScriptParam("вероятность связи: erdos_renyi")] double probability = 0.1,
        [ScriptParam("доля перекинутых связей: watts_strogatz")] double rewiring = 0.1,
        [ScriptParam("связей у нового узла: barabasi_albert")] int links = 2)
    {
        const string function = "sim.network";

        ScriptData.Require(nodes >= 2, $"{function}: узлов нужно хотя бы два");

        context.CountAllocation((long)nodes * nodes);

        ContactNetwork graph = ScriptData.Kind<Func<ContactNetwork>>(kind, "kind", function,
            ("complete", () => ContactNetwork.Complete(nodes)),
            ("ring", () => ContactNetwork.Ring(nodes, neighbors)),
            ("erdos_renyi", () => ContactNetwork.ErdosRenyi(nodes, probability, context.Random)),
            ("watts_strogatz", () => ContactNetwork.WattsStrogatz(nodes, neighbors, rewiring, context.Random)),
            ("barabasi_albert", () => ContactNetwork.BarabasiAlbert(nodes, links, context.Random)))();

        var adjacency = new Matrix(nodes, nodes);

        for (int i = 0; i < nodes; i++)
        {
            foreach (int j in graph.Contacts(i)) adjacency[i, j] = 1;
        }

        return adjacency;
    }

    // --- процессы ---

    /// <summary>
    /// Процесс Орнштейна — Уленбека.
    /// </summary>
    /// <remarks>
    /// Случайное блуждание с возвратом к среднему: курс к паритету, температура к норме,
    /// настроение к привычному. Половина пути до среднего проходится за <c>half_life</c> —
    /// по этому числу модель и сверяют с данными.
    /// </remarks>
    [ScriptFn("ou", "Процесс Орнштейна — Уленбека: случайный путь с возвратом к среднему",
        Example = "let walk = sim.ou(mean: 100, reversion: 0.5, volatility: 2, start: 90, steps: 250)")]
    public static ScriptRecord Ou(
        IScriptContext context,
        [ScriptParam("среднее, к которому возвращается процесс")] double mean,
        [ScriptParam("скорость возврата: доля отклонения за единицу времени")] double reversion,
        [ScriptParam("волатильность шума")] double volatility,
        [ScriptParam("начальное значение; nan — среднее")] double start = double.NaN,
        [ScriptParam("шаг по времени")] double step = 1,
        [ScriptParam("число шагов")] int steps = 100)
    {
        const string function = "sim.ou";

        ScriptData.Require(reversion > 0 && volatility >= 0, $"{function}: скорость возврата положительна, волатильность неотрицательна");
        ScriptData.Require(step > 0 && steps >= 1, $"{function}: шаг положителен, шагов — хотя бы один");

        context.CountAllocation(steps);

        var process = new OrnsteinUhlenbeckProcess(mean, reversion, volatility);
        double[] path = process.Path(double.IsNaN(start) ? mean : start, step, steps, context.Random);
        var time = new Vector(path.Length);

        for (int i = 0; i < path.Length; i++) time[i] = i * step;

        return ScriptData.Plain(
            ("path", new Vector(path)),
            ("time", time),
            ("half_life", process.HalfLife),
            ("relaxation_time", process.RelaxationTime),
            ("stationary_sd", process.StationaryDeviation));
    }

    /// <summary>
    /// Системная динамика: запасы и потоки.
    /// </summary>
    /// <remarks>
    /// Поток каждого запаса — функция скрипта <c>(t, s) =&gt; ...</c>: <c>t</c> — время, <c>s</c> —
    /// запись текущих уровней по именам. Так записываются хищник и жертва, эпидемия, склад и
    /// заказы — ровно формулами из учебника. Ответ — таблица: время и колонка на запас, её сразу
    /// можно рисовать.
    /// </remarks>
    [ScriptFn("stock_flow", "Системная динамика: запасы и потоки во времени",
        Example = "let run = sim.stock_flow({ prey: 40, predators: 9 }, { prey: (t, s) => 0.1 * s.prey - 0.02 * s.prey * s.predators, predators: (t, s) => 0.01 * s.prey * s.predators - 0.1 * s.predators }, until: 100)")]
    public static ScriptTable StockFlow(
        IScriptContext context,
        [ScriptParam("запасы: запись «имя: начальный уровень»")] ScriptRecord stocks,
        [ScriptParam("потоки: запись «имя: (t, s) => чистый приток»")] ScriptRecord flows,
        [ScriptParam("конечное время")] double until,
        [ScriptParam("сколько точек вернуть")] int points = 100,
        [ScriptParam("нижние границы запасов; по умолчанию без границы")] ScriptRecord? lower = null,
        [ScriptParam("верхние границы запасов; по умолчанию без границы")] ScriptRecord? upper = null)
    {
        const string function = "sim.stock_flow";

        ScriptData.Require(stocks.Count > 0, $"{function}: нет ни одного запаса");
        ScriptData.Require(until > 0 && points >= 2, $"{function}: нужно until > 0 и хотя бы две точки");

        IReadOnlyList<string> names = stocks.Keys;

        foreach (string name in flows.Keys)
        {
            if (stocks.Has(name)) continue;

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: поток '{name}' для запаса, которого нет",
                $"запасы: {string.Join(", ", names)}");
        }

        var model = new StockFlowModel();

        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];

            if (!flows.TryGet(name, out ScriptValue flow))
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"{function}: у запаса '{name}' нет потока",
                    $"постоянный запас — поток ноль: {name}: (t, s) => 0");
            }

            ScriptCallable callable = flow.AsCallable($"поток '{name}'");

            double NetFlow(double time, IReadOnlyList<double> levels) =>
                ScriptCallbacks.Invoke(context, callable, ScriptValue.Num(time), ScriptValue.Record(Levels(names, levels)))
                    .AsNumber($"{function}: поток '{name}'");

            _ = model.AddStock(
                name,
                stocks.Values[index].AsNumber($"начальный уровень '{name}'"),
                NetFlow,
                Bound(lower, name, double.NegativeInfinity, names, "lower", function),
                Bound(upper, name, double.PositiveInfinity, names, "upper", function));
        }

        context.CountAllocation((long)points * (names.Count + 1));

        IReadOnlyList<SystemState> states = model.Run(until, points);

        var columns = new List<ScriptColumn>(names.Count + 1)
        {
            ScriptColumn.FromVector("time", new Vector([.. states.Select(state => state.Time)])),
        };

        for (int j = 0; j < names.Count; j++)
            columns.Add(ScriptColumn.FromVector(names[j], new Vector([.. states.Select(state => state.Levels[j])])));

        return ScriptTable.Create(columns);
    }

    // --- внутреннее ---

    /// <summary>Модель MDP из таблицы с именами состояний и действий.</summary>
    private sealed record MdpTable(MarkovDecisionProcess Process, string[] States, string[][] Actions);

    private static MdpTable ReadMdp(ScriptTable table, double discount, string function)
    {
        ScriptData.Require(discount is >= 0 and < 1, $"{function}: дисконт лежит в [0, 1)");

        foreach (string column in new[] { "state", "action", "next", "p", "reward" })
        {
            if (table.TryGet(column, out _)) continue;

            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: в таблице переходов нет колонки {column}",
                "колонки: state, action, next, p, reward — строка на исход действия");
        }

        ScriptColumn stateColumn = table.Column("state"), actionColumn = table.Column("action"), nextColumn = table.Column("next");
        Vector probability = ScriptData.Column(table, "p", function);
        Vector reward = ScriptData.Column(table, "reward", function);

        var states = new List<string>();
        var stateIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        int StateOf(string label)
        {
            if (!stateIndex.TryGetValue(label, out int index))
            {
                index = states.Count;
                stateIndex[label] = index;
                states.Add(label);
            }

            return index;
        }

        var rows = new (int State, string Action, int Next, double P, double Reward)[table.RowCount];

        for (int i = 0; i < rows.Length; i++)
        {
            ScriptData.Require(probability[i] is >= 0 and <= 1, $"{function}: вероятность в строке {i} вне [0, 1]");

            rows[i] = (StateOf(Label(stateColumn[i])), Label(actionColumn[i]), StateOf(Label(nextColumn[i])), probability[i], reward[i]);
        }

        var actions = new List<string>[states.Count];

        for (int s = 0; s < states.Count; s++) actions[s] = [];

        foreach (var row in rows)
        {
            if (!actions[row.State].Contains(row.Action)) actions[row.State].Add(row.Action);
        }

        var transitions = new double[states.Count][][];
        var rewards = new double[states.Count][];

        for (int s = 0; s < states.Count; s++)
        {
            // Состояние без действий — поглощающее: остаётся в себе с нулевой наградой
            if (actions[s].Count == 0) actions[s].Add("stay");

            transitions[s] = new double[actions[s].Count][];
            rewards[s] = new double[actions[s].Count];

            for (int a = 0; a < actions[s].Count; a++) transitions[s][a] = new double[states.Count];
        }

        foreach (var row in rows)
        {
            int a = actions[row.State].IndexOf(row.Action);

            transitions[row.State][a][row.Next] += row.P;
            rewards[row.State][a] += row.P * row.Reward;
        }

        for (int s = 0; s < states.Count; s++)
        {
            for (int a = 0; a < actions[s].Count; a++)
            {
                double total = transitions[s][a].Sum();

                if (total == 0)
                {
                    transitions[s][a][s] = 1;
                    continue;
                }

                if (Math.Abs(total - 1) > 1e-6)
                {
                    throw new ScriptError(
                        DiagnosticCodes.BadOperand,
                        $"{function}: вероятности исходов '{states[s]}' → '{actions[s][a]}' дают в сумме {total:G6}",
                        "исходы одного действия в одном состоянии должны давать в сумме 1");
                }
            }
        }

        return new MdpTable(
            new MarkovDecisionProcess(transitions, rewards, discount),
            [.. states],
            [.. actions.Select(list => list.ToArray())]);
    }

    private static ScriptTable PolicyTable(MdpTable model, IReadOnlyList<int> policy, Func<int, double> value) =>
        ScriptData.Table([.. Enumerable.Range(0, model.States.Length)],
            ("state", s => model.States[s]),
            ("action", s => model.Actions[s][policy[s]]),
            ("value", s => value(s)));

    /// <summary>Подпись состояния или действия: строка как есть, число — в записи языка.</summary>
    private static string Label(ScriptValue value) =>
        value.Type == ScriptType.Str ? value.AsString() : ScriptFormatter.Number(value.AsNumber("имя состояния"));

    private static ScriptRecord QueueRecord(QueueMetrics metrics, double blocking) => ScriptData.Record(metrics,
        ("utilisation", metrics.Utilisation),
        ("queue_length", metrics.QueueLength),
        ("system_length", metrics.SystemLength),
        ("wait", metrics.WaitTime),
        ("system_time", metrics.SystemTime),
        ("idle_probability", metrics.IdleProbability),
        ("blocking", blocking));

    private static void RequireRates(double arrival, double service, int servers, int capacity, string function)
    {
        ScriptData.Require(arrival > 0 && service > 0, $"{function}: интенсивности потока и обслуживания положительны");
        ScriptData.Require(servers >= 1, $"{function}: приборов — хотя бы один");
        ScriptData.Require(capacity >= 0, $"{function}: мест не может быть отрицательное число");
        ScriptData.Require(capacity == 0 || capacity >= servers, $"{function}: мест в системе меньше, чем приборов");
    }

    /// <summary>Сеть контактов из матрицы смежности: связь там, где элемент не ноль.</summary>
    private static ContactNetwork Network(Matrix adjacency, int agents, string function)
    {
        if (adjacency.Height != adjacency.Width || adjacency.Height != agents)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{function}: матрица смежности {adjacency.Height}×{adjacency.Width}, а агентов {agents}",
                "смежность — квадратная матрица по числу агентов; её даёт sim.network");
        }

        var network = new ContactNetwork(agents);

        for (int i = 0; i < agents; i++)
        {
            for (int j = i + 1; j < agents; j++)
            {
                if (adjacency[i, j] != 0 || adjacency[j, i] != 0) network.Connect(i, j);
            }
        }

        return network;
    }

    private static string[] Facts(ScriptRecord action, string field, int index)
    {
        if (!action.TryGet(field, out ScriptValue value)) return [];

        ScriptList list = value.AsList($"actions[{index}].{field}");
        var facts = new string[list.Count];

        for (int i = 0; i < list.Count; i++) facts[i] = list[i].AsString($"actions[{index}].{field}[{i}]");

        return facts;
    }

    private static ScriptRecord Levels(IReadOnlyList<string> names, IReadOnlyList<double> levels)
    {
        var fields = new List<KeyValuePair<string, ScriptValue>>(names.Count);

        for (int j = 0; j < names.Count; j++) fields.Add(new(names[j], ScriptValue.Num(levels[j])));

        return ScriptRecord.From(fields);
    }

    private static double Bound(
        ScriptRecord? bounds, string name, double fallback, IReadOnlyList<string> names, string parameter, string function)
    {
        if (bounds == null) return fallback;

        foreach (string key in bounds.Keys)
        {
            if (names.Contains(key)) continue;

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: в {parameter} запас '{key}', которого нет",
                $"запасы: {string.Join(", ", names)}");
        }

        return bounds.TryGet(name, out ScriptValue value) ? value.AsNumber($"граница '{name}'") : fallback;
    }
}

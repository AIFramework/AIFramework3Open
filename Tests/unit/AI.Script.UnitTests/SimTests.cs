using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространство <c>sim</c> над AI.Simulation.
/// </summary>
/// <remarks>
/// Там, где ответ известен точно — формулы очередей, ценность простого MDP, экспонента в
/// системной динамике, — сверяется число. Там, где он случаен, — сходимость имитации к формуле
/// и воспроизводимость под одним зерном: иначе привязка могла бы брать случайность не из прогона.
/// </remarks>
public sealed class SimTests
{
    private static RunResult Run(string source) => Script.RunOk(source);

    private static double Number(RunResult result, string name) => (double)result.Emitted[name]!;

    // --- очереди ---

    /// <summary>M/M/1 при λ = 8, μ = 10: ρ = 0.8, Lq = 3.2, Wq = 0.4, W = 0.5.</summary>
    [Fact]
    public void Queue_SingleServer_MatchesFormulas()
    {
        RunResult result = Run("""
            let q = sim.queue(arrival: 8, service: 10)

            emit rho = q.utilisation
            emit lq = q.queue_length
            emit wq = q.wait
            emit w = q.system_time
            """);

        Assert.Equal(0.8, Number(result, "rho"), 9);
        Assert.Equal(3.2, Number(result, "lq"), 9);
        Assert.Equal(0.4, Number(result, "wq"), 9);
        Assert.Equal(0.5, Number(result, "w"), 9);
    }

    [Fact]
    public void Queue_Unstable_SuggestsServersOrCapacity()
    {
        Diagnostic error = Script.FailsWith("emit q = sim.queue(arrival: 10, service: 8)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("servers", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Queue_LimitedCapacity_ReportsBlocking()
    {
        RunResult result = Run("emit b = sim.queue(arrival: 10, service: 8, capacity: 3).blocking");

        Assert.InRange(Number(result, "b"), 0.1, 0.9);
    }

    /// <summary>Имитация M/M/1 обязана сойтись к формулам — взаимная проверка модели и теории.</summary>
    [Fact]
    public void Station_ConvergesToFormulas()
    {
        RunResult result = Run("""
            options { seed: 3 }

            let s = sim.station(arrival: 8, service: 10, until: 20000, warmup: 500)

            emit rho = s.utilisation
            emit wq = s.wait
            """);

        Assert.InRange(Number(result, "rho"), 0.77, 0.83);
        Assert.InRange(Number(result, "wq"), 0.32, 0.48);
    }

    [Fact]
    public void Station_IsReproducible()
    {
        const string source = "options { seed: 4 }\nemit w = sim.station(arrival: 2, service: 3, until: 500).wait";

        Assert.Equal(Run(source).Emitted["w"], Run(source).Emitted["w"]);
    }

    // --- опыты ---

    [Fact]
    public void Replicate_GivesIntervalAroundTheTruth()
    {
        RunResult result = Run("""
            options { seed: 5 }

            let r = sim.replicate(i => stat.mean(signal.noise(200, sigma: 1)), n: 40, target: 0.01)

            emit lower = r.lower
            emit upper = r.upper
            emit n = len(r.values)
            emit required = r.required
            """);

        Assert.True(Number(result, "lower") < 0 && Number(result, "upper") > 0);
        Assert.Equal(40.0, result.Emitted["n"]);
        Assert.True(Number(result, "required") > 40);
    }

    [Fact]
    public void Replicate_PassesReplicationNumber()
    {
        RunResult result = Run("emit m = sim.replicate(i => i, n: 5).mean");

        Assert.Equal(2.0, Number(result, "m"), 9);
    }

    // --- решения ---

    /// <summary>
    /// Из A можно брать 1 каждый шаг либо уйти в B, где дают 2. При дисконте 0.9 уход выгоднее:
    /// V(B) = 2 / 0.1 = 20, V(A) = 0.9 · 20 = 18 против 1 / 0.1 = 10.
    /// </summary>
    private const string Model = """
        let model = table.of({
            state: ["A", "A", "B"],
            action: ["stay", "go", "stay"],
            next: ["A", "B", "B"],
            p: <1, 1, 1>,
            reward: <1, 0, 2>
        })
        """;

    [Fact]
    public void Mdp_FindsKnownPolicyAndValues()
    {
        RunResult result = Run(Model + """

            let best = sim.mdp(model, discount: 0.9)
            let a = (best.policy |> table.filter(r => r.state == "A"))[0]

            emit action = a.action
            emit value = a.value
            emit converged = best.converged
            """);

        Assert.Equal("go", result.Emitted["action"]);
        Assert.Equal(18, Number(result, "value"), 6);
        Assert.Equal(true, result.Emitted["converged"]);
    }

    [Fact]
    public void Mdp_PolicyIteration_Agrees()
    {
        RunResult result = Run(Model + "\nemit v = sim.mdp(model, discount: 0.9, kind: \"policy\").policy[0].value");

        Assert.Equal(18, Number(result, "v"), 6);
    }

    /// <summary>Состояние, откуда нет действий, поглощающее: награда 5 за вход — вся ценность.</summary>
    [Fact]
    public void Mdp_TerminalState_IsAbsorbing()
    {
        RunResult result = Run("""
            let model = table.of({ state: ["A"], action: ["finish"], next: ["END"], p: <1>, reward: <5> })

            emit v = sim.mdp(model, discount: 0.9).policy[0].value
            """);

        Assert.Equal(5, Number(result, "v"), 6);
    }

    [Fact]
    public void Mdp_ProbabilitiesNotSummingToOne_AreRejected()
    {
        Diagnostic error = Script.FailsWith("""
            let model = table.of({ state: ["A", "A"], action: ["go", "go"], next: ["A", "B"], p: <0.5, 0.4>, reward: <0, 1> })
            emit r = sim.mdp(model)
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("'A' → 'go'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Из A два пути в конец: с наградой 1 и с наградой 10. Разрыв заметный, и агент обязан
    /// выбрать второй — иначе стратегия прочитана не по тем индексам, что у точного решения.
    /// </summary>
    /// <remarks>
    /// Модель своя, а не общая заготовка выше: у той ценности действий в A — 18 и 17.2, и
    /// разрыв в 0.8 обучение с затухающим шагом может не различить за разумное число эпизодов,
    /// так что тест проверял бы удачу, а не привязку. Блок <c>options</c> стоит первым — язык
    /// требует этого, и первая версия теста, где он шёл после заготовки, падала именно на этом.
    /// </remarks>
    [Fact]
    public void Learn_QLearning_AgreesWithExactPolicy()
    {
        RunResult result = Run("""
            options { seed: 6 }

            let model = table.of({
                state: ["A", "A"],
                action: ["small", "large"],
                next: ["END", "END"],
                p: <1, 1>,
                reward: <1, 10>
            })

            let agent = sim.learn(model, episodes: 500, steps: 5, discount: 0.9)
            let a = (agent.policy |> table.filter(r => r.state == "A"))[0]

            emit agreement = agent.agreement
            emit action = a.action
            """);

        Assert.Equal(1.0, Number(result, "agreement"), 9);
        Assert.Equal("large", result.Emitted["action"]);
    }

    /// <summary>Кофе по дороге на работу: кратчайший план — зайти в кафе, купить, дойти.</summary>
    [Fact]
    public void Plan_FindsShortestChain()
    {
        RunResult result = Run("""
            let actions = [
                { name: "go_cafe", pre: ["at_home"], add: ["at_cafe"], delete: ["at_home"] },
                { name: "buy", pre: ["at_cafe"], add: ["has_coffee"] },
                { name: "walk_work", pre: ["at_cafe"], add: ["at_work"], delete: ["at_cafe"] },
                { name: "commute", pre: ["at_home"], add: ["at_work"], delete: ["at_home"] }
            ]

            let route = sim.plan(["at_home"], ["at_work", "has_coffee"], actions)

            emit found = route.found
            emit length = route.length
            emit second = route.actions[1]
            """);

        Assert.Equal(true, result.Emitted["found"]);
        Assert.Equal(3.0, result.Emitted["length"]);
        Assert.Equal("buy", result.Emitted["second"]);
    }

    [Fact]
    public void Plan_TypoInActionField_IsRejected()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = sim.plan([\"a\"], [\"b\"], [{ name: \"x\", pre: [\"a\"], add: [\"b\"], delet: [\"a\"] }])");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("delet", error.Message, StringComparison.Ordinal);
    }

    // --- мнения ---

    [Fact]
    public void Opinions_ConfidenceDecidesBetweenConsensusAndSplit()
    {
        RunResult result = Run("""
            let people = vec.linspace(0, 1, n: 50)

            emit wide = sim.opinions(people, confidence: 0.3).consensus
            emit narrow = len(sim.opinions(people, confidence: 0.05).clusters)
            """);

        Assert.Equal(true, result.Emitted["wide"]);
        Assert.True(Number(result, "narrow") > 1);
    }

    [Fact]
    public void Opinions_Deffuant_OnNetwork_Runs()
    {
        RunResult result = Run("""
            options { seed: 7 }

            let graph = sim.network(30, kind: "ring", neighbors: 2)
            let o = sim.opinions(vec.linspace(0, 1, n: 30), confidence: 0.5, kind: "deffuant", interactions: 20000, network: graph)

            emit model = type(o.model)
            emit size = len(o.opinions)
            """);

        Assert.Equal("str", result.Emitted["model"]);
        Assert.Equal(30.0, result.Emitted["size"]);
    }

    /// <summary>Сильно связная непериодическая сеть: согласие, веса голосов дают в сумме 1.</summary>
    [Fact]
    public void Influence_DeGroot_ReachesConsensus()
    {
        RunResult result = Run("""
            let weights = mat.of([<0.5, 0.5, 0>, <0.3, 0.4, 0.3>, <0, 0.5, 0.5>])
            let g = sim.influence(weights, opinions: <0, 1, 0.5>)

            emit consensus = g.consensus
            emit power = vec.sum(g.social_power)
            emit value = g.consensus_value
            emit check = vec.dot(g.social_power, <0, 1, 0.5>)
            """);

        Assert.Equal(true, result.Emitted["consensus"]);
        Assert.Equal(1, Number(result, "power"), 9);
        Assert.Equal(Number(result, "check"), Number(result, "value"), 9);
    }

    [Fact]
    public void Influence_NonStochasticRows_AreRejected()
    {
        Diagnostic error = Script.FailsWith("emit g = sim.influence(mat.of([<1, 1>, <0, 1>]))");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("self_weight", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Невосприимчивые агенты остаются при своём: равновесие совпадает с исходными мнениями.</summary>
    [Fact]
    public void FriedkinJohnsen_ZeroSusceptibility_KeepsPrejudices()
    {
        RunResult result = Run("""
            let weights = mat.of([<0.5, 0.5>, <0.5, 0.5>])
            let settled = sim.friedkin_johnsen(weights, <0, 0>, <0.1, 0.9>)

            emit a = settled[0]
            emit b = settled[1]
            """);

        Assert.Equal(0.1, Number(result, "a"), 9);
        Assert.Equal(0.9, Number(result, "b"), 9);
    }

    [Fact]
    public void Network_Complete_ConnectsEveryone()
    {
        RunResult result = Run("emit degree = vec.sum(sim.network(5)[0, :])");

        Assert.Equal(4.0, Number(result, "degree"), 9);
    }

    [Fact]
    public void Voter_Unanimous_StaysUnanimous()
    {
        RunResult result = Run("""
            options { seed: 8 }

            let vote = sim.voter(sim.network(10), vec.ones(10))

            emit p = vote.consensus_probability
            emit outcome = vote.outcome
            """);

        Assert.Equal(1, Number(result, "p"), 9);
        Assert.Equal(1.0, result.Emitted["outcome"]);
    }

    // --- процессы ---

    [Fact]
    public void Ou_PathAndHalfLife()
    {
        RunResult result = Run("""
            options { seed: 9 }

            let walk = sim.ou(mean: 100, reversion: 0.5, volatility: 2, start: 90, steps: 250)

            emit same = len(walk.path) == len(walk.time)
            emit first = walk.path[0]
            emit half = walk.half_life
            """);

        Assert.Equal(true, result.Emitted["same"]);
        Assert.Equal(90, Number(result, "first"), 9);
        Assert.Equal(Math.Log(2) / 0.5, Number(result, "half"), 9);
    }

    /// <summary>Рост 10% за единицу времени: за 10 единиц — ровно в e раз.</summary>
    [Fact]
    public void StockFlow_ExponentialGrowth_MatchesE()
    {
        RunResult result = Run("""
            let run = sim.stock_flow({ x: 1 }, { x: (t, s) => 0.1 * s.x }, until: 10)

            emit last = run[len(run) - 1].x
            emit time = run[len(run) - 1].time
            """);

        Assert.Equal(10, Number(result, "time"), 9);
        Assert.Equal(Math.E, Number(result, "last"), 3);
    }

    [Fact]
    public void StockFlow_MissingFlow_ShowsTheZeroForm()
    {
        Diagnostic error = Script.FailsWith("emit r = sim.stock_flow({ x: 1, y: 2 }, { x: (t, s) => 0 }, until: 1)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("y: (t, s) => 0", error.Hint, StringComparison.Ordinal);
    }
}

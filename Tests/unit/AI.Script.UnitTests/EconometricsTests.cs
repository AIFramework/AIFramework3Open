using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространства <c>regress</c>, <c>ts</c> и <c>causal</c> над AI.Econometrics.
/// </summary>
/// <remarks>
/// Данные строятся в самом скрипте с известным ответом: коэффициент, заложенный в генератор,
/// обязан вернуться из оценки. Это проверяет не библиотеку — у неё свои тесты, — а привязку:
/// что колонки не перепутаны, свободный член стоит там, где обещано, а воздействие и отклик
/// не поменялись местами.
/// </remarks>
public sealed class EconometricsTests
{
    private static RunResult Run(string source) => Script.RunOk(source);

    private static double Number(RunResult result, string name) => (double)result.Emitted[name]!;

    /// <summary>Отклик 2 + 3x − z с шумом: оценки обязаны вернуть заложенное.</summary>
    private const string Linear = """
        options { seed: 1 }

        let n = 200
        let x = signal.noise(n, sigma: 1)
        let z = signal.noise(n, sigma: 1)
        let y = 2 + 3 * x - z + signal.noise(n, sigma: 0.5)
        let data = table.of({ x: x, z: z, y: y })
        """;

    // --- regress ---

    [Fact]
    public void Ols_RecoversCoefficients_InDeclaredOrder()
    {
        RunResult result = Run(Linear + """

            let m = regress.ols(data, "y", ["x", "z"])

            emit first = m.coefficients[0].name
            emit second = m.coefficients[1].name
            emit b0 = m.coefficients[0].estimate
            emit bx = m.coefficients[1].estimate
            emit bz = m.coefficients[2].estimate
            emit p = m.coefficients[1].p
            emit r2 = m.r2
            """);

        Assert.Equal("const", result.Emitted["first"]);
        Assert.Equal("x", result.Emitted["second"]);
        Assert.InRange(Number(result, "b0"), 1.8, 2.2);
        Assert.InRange(Number(result, "bx"), 2.8, 3.2);
        Assert.InRange(Number(result, "bz"), -1.2, -0.8);
        Assert.True(Number(result, "p") < 1e-6);
        Assert.True(Number(result, "r2") > 0.9);
    }

    /// <summary>Устойчивая ковариация меняет ошибки, но не оценки.</summary>
    [Fact]
    public void Ols_RobustVariance_ChangesErrorsNotEstimates()
    {
        RunResult result = Run(Linear + """

            let classic = regress.ols(data, "y", ["x"])
            let robust = regress.ols(data, "y", ["x"], variance: "hc3")

            emit same = classic.coefficients[1].estimate == robust.coefficients[1].estimate
            emit se_classic = classic.coefficients[1].std_error
            emit se_robust = robust.coefficients[1].std_error
            """);

        Assert.Equal(true, result.Emitted["same"]);
        Assert.NotEqual(Number(result, "se_classic"), Number(result, "se_robust"));
    }

    [Fact]
    public void Ols_ClusterVarianceWithoutColumn_IsReported()
    {
        Diagnostic error = Script.FailsWith(Linear + "\nemit m = regress.ols(data, \"y\", [\"x\"], variance: \"cluster\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("cluster:", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Ols_UnknownColumn_SuggestsClosest()
    {
        Diagnostic error = Script.FailsWith(Linear + "\nemit m = regress.ols(data, \"y\", [\"xx\"])");

        Assert.Contains("xx", error.Message, StringComparison.Ordinal);
        Assert.Contains("x", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Ols_UnknownVariance_ListsKnown()
    {
        Diagnostic error = Script.FailsWith(Linear + "\nemit m = regress.ols(data, \"y\", [\"x\"], variance: \"white\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("\"hc1\"", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Разбор библиотеки доходит до скрипта: без него остались бы одни числа.</summary>
    [Fact]
    public void Results_CarrySummaryAndWarnings()
    {
        RunResult result = Run(Linear + """

            let m = regress.ols(data, "y", ["x", "z"])

            emit summary = m.summary
            emit warnings = type(m.warnings)
            """);

        Assert.False(string.IsNullOrWhiteSpace((string)result.Emitted["summary"]!));
        Assert.Equal("list", result.Emitted["warnings"]);
    }

    [Fact]
    public void Quantile_Median_RecoversSlope()
    {
        RunResult result = Run(Linear + """

            let m = regress.quantile(data, "y", ["x", "z"], q: 0.5, bootstrap: 50)

            emit bx = m.coefficients[1].estimate
            emit q = m.q
            """);

        Assert.InRange(Number(result, "bx"), 2.7, 3.3);
        Assert.Equal(0.5, result.Emitted["q"]);
    }

    [Fact]
    public void Binary_Logit_FindsPositiveEffect()
    {
        RunResult result = Run("""
            options { seed: 2 }

            let n = 400
            let x = signal.noise(n, sigma: 1)
            let eps = signal.noise(n, sigma: 0.5)
            let y = vec.of(range(n) |> core.map(i => if x[i] + eps[i] > 0 { 1 } else { 0 }))
            let m = regress.binary(table.of({ x: x, y: y }), "y", ["x"])

            emit bx = m.coefficients[1].estimate
            emit accuracy = m.accuracy
            emit effect = m.marginal_effects[0].effect
            """);

        Assert.True(Number(result, "bx") > 1);
        Assert.True(Number(result, "accuracy") > 0.8);
        Assert.True(Number(result, "effect") > 0);
    }

    [Fact]
    public void Binary_RejectsNonBinaryResponse()
    {
        Diagnostic error = Script.FailsWith(Linear + "\nemit m = regress.binary(data, \"y\", [\"x\"])");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("0 и 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Count_Poisson_RecoversRate()
    {
        RunResult result = Run("""
            let x = vec.linspace(0, 2, n: 60)
            let y = vec.of(range(60) |> core.map(i => core.round(math.exp(1 + 0.5 * x[i]))))
            let m = regress.count(table.of({ x: x, y: y }), "y", ["x"])

            emit bx = m.coefficients[1].estimate
            emit kind = m.kind
            """);

        Assert.InRange(Number(result, "bx"), 0.4, 0.6);
        Assert.Equal("poisson", result.Emitted["kind"]);
    }

    [Fact]
    public void Tobit_HandlesCensoredResponse()
    {
        RunResult result = Run("""
            options { seed: 3 }

            let n = 300
            let x = signal.noise(n, sigma: 1)
            let latent = x + signal.noise(n, sigma: 0.5)
            let y = vec.of(range(n) |> core.map(i => if latent[i] > 0 { latent[i] } else { 0 }))
            let m = regress.tobit(table.of({ x: x, y: y }), "y", ["x"])

            emit bx = m.coefficients[1].estimate
            emit share = m.censored_share
            """);

        Assert.InRange(Number(result, "bx"), 0.7, 1.3);
        Assert.InRange(Number(result, "share"), 0.3, 0.7);
    }

    /// <summary>
    /// Эффект объекта коррелирует с регрессором: объединённый МНК обязан ошибиться,
    /// фиксированные эффекты — нет. Иначе привязка не передаёт колонку объекта.
    /// </summary>
    /// <remarks>
    /// Эффект объекта соизмерим с шумом намеренно. Будь он огромным, случайные эффекты
    /// совпали бы с фиксированными (θ → 1), и тесту Хаусмана нечего было бы отвергать:
    /// первая версия этих данных именно так и провалила проверку.
    /// </remarks>
    private const string Panel = """
        options { seed: 4 }

        let n = 300
        let unit = vec.of(range(n) |> core.map(i => math.floor(i / 10)))
        let period = vec.of(range(n) |> core.map(i => i % 10))
        let effects = signal.noise(30, sigma: 1)
        let alpha = vec.of(range(n) |> core.map(i => effects[math.floor(i / 10)]))
        let x = alpha + signal.noise(n, sigma: 1)
        let y = alpha + 2 * x + signal.noise(n, sigma: 0.5)
        let data = table.of({ unit: unit, period: period, x: x, y: y })
        """;

    [Fact]
    public void Panel_FixedEffects_RemoveUnitBias()
    {
        RunResult result = Run(Panel + """

            let fixed = regress.panel(data, "y", ["x"], "unit", "period")
            let pooled = regress.panel(data, "y", ["x"], "unit", "period", kind: "pooled")

            emit fixed_x = (fixed.coefficients |> table.filter(c => c.name == "x"))[0].estimate
            emit pooled_x = (pooled.coefficients |> table.filter(c => c.name == "x"))[0].estimate
            emit units = fixed.units
            """);

        Assert.InRange(Number(result, "fixed_x"), 1.8, 2.2);
        Assert.True(Number(result, "pooled_x") > 2.2);
        Assert.Equal(30.0, result.Emitted["units"]);
    }

    [Fact]
    public void Hausman_PrefersFixedEffects_WhenUnitEffectIsCorrelated()
    {
        RunResult result = Run(Panel + """

            let h = regress.hausman(data, "y", ["x"], "unit", "period")

            emit prefers = h.prefers_fixed
            emit p = h.p
            """);

        Assert.Equal(true, result.Emitted["prefers"]);
        Assert.True(Number(result, "p") < 0.05);
    }

    /// <summary>Объект, названный словом, — такой же объект, как названный числом.</summary>
    [Fact]
    public void Panel_AcceptsTextUnits()
    {
        RunResult result = Run(Panel + """

            let names = range(n) |> core.map(i => "firm" + core.to_str(math.floor(i / 10)))
            let named = table.of({ firm: names, period: period, x: x, y: y })
            let m = regress.panel(named, "y", ["x"], "firm", "period")

            emit units = m.units
            emit fixed_x = (m.coefficients |> table.filter(c => c.name == "x"))[0].estimate
            """);

        Assert.Equal(30.0, result.Emitted["units"]);
        Assert.InRange(Number(result, "fixed_x"), 1.8, 2.2);
    }

    [Fact]
    public void Diagnose_ReportsTestsAndCollinearity()
    {
        RunResult result = Run(Linear + """

            let d = regress.diagnose(data, "y", ["x", "z"])

            emit tests = len(d.tests)
            emit vif = len(d.vif)
            emit dw = d.durbin_watson
            """);

        Assert.True(Number(result, "tests") >= 3);
        Assert.Equal(2.0, result.Emitted["vif"]);
        Assert.InRange(Number(result, "dw"), 1.5, 2.5);
    }

    // --- causal ---

    /// <summary>Половина объектов получает воздействие силой 3 с пятого периода.</summary>
    private const string Staggered = """
        options { seed: 5 }

        let n = 240
        let unit = vec.of(range(n) |> core.map(i => math.floor(i / 8)))
        let period = vec.of(range(n) |> core.map(i => (i % 8) + 1))
        let start = vec.of(range(n) |> core.map(i => if unit[i] < 15 { 5 } else { 0 }))
        let active = vec.of(range(n) |> core.map(i => if start[i] > 0 { if period[i] >= start[i] { 1 } else { 0 } } else { 0 }))
        let y = 0.3 * unit + 0.5 * period + 3 * active + signal.noise(n, sigma: 0.5)
        let data = table.of({ unit: unit, period: period, start: start, y: y })
        let est = causal.did(data, unit: "unit", period: "period", outcome: "y", first_treated: "start", bootstrap: 50)
        """;

    [Fact]
    public void Did_RecoversEffect()
    {
        RunResult result = Run(Staggered + """

            emit att = est.att
            emit treated = est.treated
            emit event_rows = len(est.event_study)
            """);

        Assert.InRange(Number(result, "att"), 2.6, 3.4);
        Assert.True(Number(result, "event_rows") > 0);
    }

    /// <summary>Бутстреп привязан к зерну прогона: тот же скрипт даёт ту же ошибку.</summary>
    [Fact]
    public void Did_IsReproducible()
    {
        string source = Staggered + "\nemit se = est.std_error";

        Assert.Equal(Run(source).Emitted["se"], Run(source).Emitted["se"]);
    }

    [Fact]
    public void Rdd_RecoversJump()
    {
        RunResult result = Run("""
            options { seed: 6 }

            let n = 400
            let r = vec.linspace(-1, 1, n: n)
            let base = vec.of(range(n) |> core.map(i => 1 + r[i] + (if r[i] >= 0 { 2 } else { 0 })))
            let y = base + signal.noise(n, sigma: 0.3)
            let est = causal.rdd(table.of({ r: r, y: y }), "r", "y")

            emit effect = est.effect
            emit cutoff = est.cutoff
            """);

        Assert.InRange(Number(result, "effect"), 1.6, 2.4);
        Assert.Equal(0.0, result.Emitted["cutoff"]);
    }

    /// <summary>
    /// Воздействие чаще получают объекты с большим x, а x сам растит отклик: наивная разность
    /// завышена, сопоставление обязано её исправить.
    /// </summary>
    [Fact]
    public void Matching_CorrectsConfounding()
    {
        RunResult result = Run("""
            options { seed: 7 }

            let n = 400
            let x = signal.noise(n, sigma: 1)
            let u = signal.noise(n, sigma: 1)
            let t = vec.of(range(n) |> core.map(i => if x[i] + u[i] > 0 { 1 } else { 0 }))
            let y = x + 2 * t + signal.noise(n, sigma: 0.5)
            let est = causal.matching(table.of({ x: x, t: t, y: y }), "t", "y", ["x"])

            emit att = est.att
            emit naive = est.naive
            emit balance_rows = len(est.balance)
            """);

        Assert.InRange(Number(result, "att"), 1.5, 2.5);
        Assert.True(Number(result, "naive") > Number(result, "att"));
        Assert.Equal(1.0, result.Emitted["balance_rows"]);
    }

    [Fact]
    public void Matching_RejectsNonBinaryTreatment()
    {
        Diagnostic error = Script.FailsWith(Linear + "\nemit e = causal.matching(data, \"x\", \"y\", [\"z\"])");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("0 и 1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Обработанный объект — смесь двух доноров плюс сдвиг 5 с двадцатой строки.</summary>
    private const string Synthetic = """
        options { seed: 8 }

        let n = 30
        let a = vec.linspace(10, 20, n: n) + signal.noise(n, sigma: 0.2)
        let b = vec.linspace(5, 25, n: n) + signal.noise(n, sigma: 0.2)
        let c = vec.linspace(30, 10, n: n) + signal.noise(n, sigma: 0.2)
        let lift = vec.of(range(n) |> core.map(i => if i >= 20 { 5 } else { 0 }))
        let sales = table.of({ target: 0.5 * a + 0.5 * b + lift, a: a, b: b, c: c })
        """;

    [Fact]
    public void Synthetic_RecoversShift()
    {
        RunResult result = Run(Synthetic + """

            let est = causal.synthetic(sales, "target", 20)

            emit effect = est.effect
            emit donors = len(est.weights)
            """);

        Assert.InRange(Number(result, "effect"), 4.4, 5.6);
        Assert.Equal(3.0, result.Emitted["donors"]);
    }

    [Fact]
    public void Synthetic_RejectsTreatedAmongDonors()
    {
        Diagnostic error = Script.FailsWith(Synthetic + "\nemit e = causal.synthetic(sales, \"target\", 20, donors: [\"target\", \"a\"])");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("донором", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Forest_RecoversConstantEffect()
    {
        RunResult result = Run("""
            options { seed: 9 }

            let n = 400
            let x = signal.noise(n, sigma: 1)
            let t = vec.of(range(n) |> core.map(i => i % 2))
            let y = x + 2 * t + signal.noise(n, sigma: 0.5)
            let est = causal.forest(table.of({ x: x, t: t, y: y }), "t", "y", ["x"], trees: 50)

            emit ate = est.ate
            emit effects = len(est.effects)
            """);

        Assert.InRange(Number(result, "ate"), 1.5, 2.5);
        Assert.Equal(400.0, result.Emitted["effects"]);
    }

    // --- ts ---

    [Fact]
    public void Stationarity_TellsNoiseFromRandomWalk()
    {
        RunResult result = Run("""
            options { seed: 10 }

            let noise = signal.noise(300, sigma: 1)

            emit white = ts.stationarity(noise).order
            emit walk = ts.stationarity(vec.cumsum(noise)).order
            emit adf = type(ts.stationarity(noise).adf)
            """);

        Assert.Equal(0.0, result.Emitted["white"]);
        Assert.Equal(1.0, result.Emitted["walk"]);
        Assert.Equal("record", result.Emitted["adf"]);
    }

    [Fact]
    public void Garch_ForecastsVolatility()
    {
        RunResult result = Run("""
            options { seed: 11 }

            let g = ts.garch(signal.noise(400, sigma: 0.01), horizon: 10)

            emit forecast = len(g.forecast)
            emit volatility = len(g.volatility)
            emit persistence = g.persistence
            """);

        Assert.Equal(10.0, result.Emitted["forecast"]);
        Assert.Equal(400.0, result.Emitted["volatility"]);
        Assert.True(Number(result, "persistence") < 1);
    }

    [Fact]
    public void Garch_UnknownKind_ListsKnown()
    {
        Diagnostic error = Script.FailsWith("emit g = ts.garch(signal.noise(100, sigma: 1), kind: \"arch\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("\"gjr\"", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>b повторяет вчерашнее a: причинность по Грейнджеру обязана найтись от a к b.</summary>
    private const string Lagged = """
        options { seed: 12 }

        let n = 300
        let a = signal.noise(n, sigma: 1)
        let eps = signal.noise(n, sigma: 0.3)
        let b = vec.of(range(n) |> core.map(i => if i == 0 { 0 } else { 0.8 * a[i - 1] + eps[i] }))
        let series = table.of({ a: a, b: b })
        """;

    [Fact]
    public void Var_FindsGrangerCausality()
    {
        RunResult result = Run(Lagged + """

            let v = ts.var(series, order: 1, horizon: 5)
            let ab = v.granger |> table.filter(g => g.from == "a") |> table.filter(g => g.to == "b")
            let ba = v.granger |> table.filter(g => g.from == "b") |> table.filter(g => g.to == "a")

            emit ab = ab[0].causes
            emit ba = ba[0].causes
            emit share = v.fevd[1].a + v.fevd[1].b
            emit irf = len(v.irf)
            """);

        Assert.Equal(true, result.Emitted["ab"]);
        Assert.Equal(false, result.Emitted["ba"]);
        Assert.InRange(Number(result, "share"), 0.99, 1.01);
        Assert.True(Number(result, "irf") >= 20);
    }

    [Fact]
    public void Var_SelectsOrder_WhenNotGiven()
    {
        RunResult result = Run(Lagged + "\nemit order = ts.var(series).order");

        Assert.InRange(Number(result, "order"), 1, 6);
    }

    [Fact]
    public void Var_NeedsTwoSeries()
    {
        Diagnostic error = Script.FailsWith("emit v = ts.var(table.of({ a: signal.noise(50, sigma: 1) }))");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("два ряда", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Johansen_FindsOneCommonTrend()
    {
        RunResult result = Run("""
            options { seed: 13 }

            let n = 300
            let trend = vec.cumsum(signal.noise(n, sigma: 1))
            let prices = table.of({ x: trend, y: trend + signal.noise(n, sigma: 0.5) })

            emit rank = ts.johansen(prices).rank
            emit vecm_rank = ts.vecm(prices).rank
            """);

        Assert.Equal(1.0, result.Emitted["rank"]);
        Assert.Equal(1.0, result.Emitted["vecm_rank"]);
    }

    [Fact]
    public void StateSpace_ForecastsWithInterval()
    {
        RunResult result = Run("""
            options { seed: 14 }

            let y = vec.linspace(10, 40, n: 60) + signal.noise(60, sigma: 1)
            let s = ts.state_space(y, kind: "trend", horizon: 6)

            emit forecast = len(s.forecast)
            emit lower = s.lower[0] < s.forecast[0]
            emit upper = s.upper[0] > s.forecast[0]
            """);

        Assert.Equal(6.0, result.Emitted["forecast"]);
        Assert.Equal(true, result.Emitted["lower"]);
        Assert.Equal(true, result.Emitted["upper"]);
    }
}

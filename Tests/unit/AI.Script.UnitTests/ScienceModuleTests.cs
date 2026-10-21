using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Science;

namespace AI.Script.UnitTests;

/// <summary>
/// Естественнонаучные пространства: <c>ode</c>, <c>fit</c>, <c>phys</c> и <c>bio</c>.
/// </summary>
/// <remarks>
/// Как и в <see cref="DomainTests"/>, проверяется привязка, а не сами библиотечные расчёты:
/// попали ли аргументы на свои места и значат ли поля ответа то, что написано в их именах.
/// Ожидания — ответы задачника, которые сверяются на бумаге: дальность броска при 45°, вторая
/// космическая Земли, трансляция известной рамки, точное решение линейного уравнения.
/// </remarks>
public sealed class ScienceModuleTests
{
    private static readonly ScriptHost Host = Script.FullHost();

    private static RunResult Run(string source)
    {
        RunResult result = Script.RunWith(Host, source, new RunOptions { Seed = 7 });

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    private static double Number(string expression) => Assert.IsType<double>(Run($"emit r = {expression}").Emitted["r"]);

    private static string Text(string expression) => Assert.IsType<string>(Run($"emit r = {expression}").Emitted["r"]);

    private static bool Flag(string expression) => Assert.IsType<bool>(Run($"emit r = {expression}").Emitted["r"]);

    // --- примеры ---

    /// <summary>
    /// Каждый пример справки новых пространств исполняется, а не только разбирается.
    /// </summary>
    /// <remarks>
    /// Общие проверки стиля сверяют пример с сигнатурами, но допускают несвязанные имена вроде
    /// <c>data</c>. Примеры этих пространств написаны самодостаточными, и здесь это закреплено:
    /// модель копирует пример дословно, и он обязан отработать.
    /// </remarks>
    [Fact]
    public void EveryExample_Runs()
    {
        var failed = new List<string>();

        foreach (IScriptModule module in ScienceLibrary.Modules)
        {
            foreach (ScriptFunction function in module.Functions)
            {
                string example = function.Example!;
                string source = example.StartsWith("let ", StringComparison.Ordinal) ? example : $"emit r = {example}";
                RunResult result = Script.RunWith(Host, source);

                if (!result.Success) failed.Add($"{function.FullName}: {result.Error?.Message}");
            }
        }

        Assert.True(failed.Count == 0, "примеры не исполняются:\n  " + string.Join("\n  ", failed));
    }

    // --- ode ---

    /// <summary>y' = −y/2, y(0) = 1: в момент 10 точное решение e⁻⁵.</summary>
    [Fact]
    public void Ode_ExponentialDecay_MatchesClosedForm()
    {
        RunResult result = Run("""
            let r = ode.solve((t, s) => { y: -0.5 * s.y }, { y: 1 }, until: 10, points: 11, rtol: 1e-10, atol: 1e-12)

            emit last = r.final.y
            emit rows = len(r.table)
            emit middle = r.table[5].y
            emit ok = r.ok
            emit stopped = r.stopped_by
            """);

        Assert.Equal(Math.Exp(-5), (double)result.Emitted["last"]!, 9);
        Assert.Equal(11.0, result.Emitted["rows"]);
        Assert.Equal(Math.Exp(-2.5), (double)result.Emitted["middle"]!, 8);
        Assert.Equal(true, result.Emitted["ok"]);
        Assert.Equal(string.Empty, result.Emitted["stopped"]);
    }

    /// <summary>Гармонический осциллятор x'' = −x: энергия за восемь периодов не уходит.</summary>
    [Fact]
    public void Ode_HarmonicOscillator_ConservesEnergy()
    {
        RunResult result = Run("""
            let r = ode.solve((t, s) => { x: s.v, v: -s.x }, { x: 1, v: 0 }, until: 16 * pi, rtol: 1e-10, atol: 1e-12)

            emit energy = 0.5 * (r.final.x * r.final.x + r.final.v * r.final.v)
            emit x = r.final.x
            """);

        Assert.Equal(0.5, (double)result.Emitted["energy"]!, 7);
        Assert.Equal(1.0, (double)result.Emitted["x"]!, 6);
    }

    /// <summary>Падение с 20 м: событие «земля» останавливает расчёт при t = √(2h/g).</summary>
    [Fact]
    public void Ode_Event_StopsAtGround()
    {
        RunResult result = Run("""
            let r = ode.solve((t, s) => { y: s.v, v: -9.81 }, { y: 20, v: 0 }, until: 10,
                events: { ground: (t, s) => s.y })

            emit stopper = r.stopped_by
            emit when = r.t_event
            emit end = r.t_end
            emit last_y = r.table[len(r.table) - 1].y
            """);

        double expected = Math.Sqrt(2 * 20 / 9.81);

        Assert.Equal("ground", result.Emitted["stopper"]);
        Assert.Equal(expected, (double)result.Emitted["when"]!, 6);
        Assert.Equal(expected, (double)result.Emitted["end"]!, 6);
        Assert.Equal(0.0, (double)result.Emitted["last_y"]!, 6);
    }

    [Fact]
    public void Ode_MissingDerivative_IsExplained()
    {
        RunResult result = Script.RunWith(Host, "emit r = ode.solve((t, s) => { x: s.v }, { x: 1, v: 0 }, until: 1)");

        Assert.False(result.Success);
        Assert.Contains("'v'", result.Error!.Message, StringComparison.Ordinal);
    }

    // --- fit ---

    /// <summary>Точные данные y = 3·e^(−0.7x): подгонка возвращает 3 и 0.7, R² = 1.</summary>
    [Fact]
    public void Fit_Curve_RecoversExponentialParameters()
    {
        RunResult result = Run("""
            let x = vec.linspace(0, 4, n: 20)
            let y = vec.of(x |> core.map(v => 3 * math.exp(-0.7 * v)))
            let r = fit.curve((x, p) => p.a * math.exp(-p.b * x), x, y, { a: 1, b: 0.1 })

            emit a = r.params.a
            emit b = r.params.b
            emit r2 = r.r2
            emit converged = r.converged
            """);

        Assert.Equal(3.0, (double)result.Emitted["a"]!, 6);
        Assert.Equal(0.7, (double)result.Emitted["b"]!, 6);
        Assert.Equal(1.0, (double)result.Emitted["r2"]!, 9);
        Assert.Equal(true, result.Emitted["converged"]);
    }

    /// <summary>Два множителя при одном слагаемом не различимы: предупреждение и nan в ошибках.</summary>
    [Fact]
    public void Fit_Curve_WarnsWhenParametersAreNotIdentifiable()
    {
        RunResult result = Run("""
            let r = fit.curve((x, p) => p.a * p.b * x, <1, 2, 3, 4>, <2, 4.1, 5.9, 8>, { a: 1, b: 1 })

            emit warned = len(r.warnings) > 0
            emit error_a = r.errors.a
            """);

        Assert.Equal(true, result.Emitted["warned"]);
        Assert.True(double.IsNaN((double)result.Emitted["error_a"]!));
    }

    // --- phys ---

    /// <summary>50 м/с под 45°: дальность v²/g ≈ 254.8 м при g = 9.81.</summary>
    [Fact]
    public void Phys_Projectile_RangeAt45Degrees()
    {
        Assert.Equal(2500 / 9.81, Number("phys.projectile(50, 45, gravity: 9.81).range"), 6);
        Assert.Equal(2500 / 9.80665, Number("phys.projectile(50, 45).range"), 6);
        Assert.Equal(2500 / (4 * 9.81), Number("phys.projectile(50, 45, gravity: 9.81).max_height"), 6);
    }

    /// <summary>Вторая космическая Земли — около 11.19 км/с, геостационар — около 42 164 км.</summary>
    [Fact]
    public void Phys_Orbits_MatchEarthValues()
    {
        Assert.InRange(Number("phys.escape_speed()"), 11170, 11200);
        Assert.InRange(Number("phys.orbit_radius(86164)"), 4.2155e7, 4.2175e7);
        Assert.InRange(Number("phys.orbit(6.371e6 + 4.2e5).period"), 5500, 5600);
        Assert.InRange(Number("phys.hohmann(6.678e6, 4.2164e7).delta_v"), 3800, 4000);
    }

    [Fact]
    public void Phys_Thermodynamics_KnownAnswers()
    {
        Assert.Equal(0.022414, Number("phys.ideal_gas(moles: 1, temperature: 273.15, pressure: 101325).volume"), 5);
        Assert.Equal(1.0, Number("phys.ideal_gas(temperature: 273.15, volume: 0.0224140, pressure: 101325).moles"), 3);
        Assert.Equal(0.4, Number("phys.carnot(500, 300).efficiency"), 12);
        Assert.Equal(1.5, Number("phys.carnot(500, 300).cop_cooling"), 12);
        Assert.Equal(64.0, Number("phys.heat_conduction(0.8, 20, 0.25)"), 9);
        Assert.Equal(900 * Math.Log(70.0 / 30.0), Number("phys.cooling_time(90, 50, 20, tau: 900)"), 6);
    }

    [Fact]
    public void Phys_IdealGas_RequiresExactlyOneUnknown()
    {
        RunResult result = Script.RunWith(Host, "emit r = phys.ideal_gas(moles: 1, temperature: 300)");

        Assert.False(result.Success);
    }

    [Fact]
    public void Phys_Circuits_KnownAnswers()
    {
        Assert.Equal(4.4, Number("phys.ohm(220, 50).current"), 12);
        Assert.Equal(968.0, Number("phys.ohm(220, 50).power"), 9);
        Assert.Equal(1e-3, Number("phys.rc(1000, 1e-6).tau"), 12);
        Assert.Equal(1 - Math.Exp(-1), Number("phys.rc(1000, 1e-6, supply: 5, time: 0.001).fraction"), 12);
        Assert.Equal(1 / (2 * Math.PI * Math.Sqrt(1e-9)), Number("phys.rlc(1e-3, 1e-6).frequency"), 6);
        Assert.Equal("underdamped", Text("phys.rlc(1e-3, 1e-6, resistance: 10).regime"));
    }

    [Fact]
    public void Phys_DecayAtmosphereRelativity_KnownAnswers()
    {
        Assert.Equal(0.5, Number("phys.decay(5730, 5730).fraction"), 12);
        Assert.Equal(11460.0, Number("phys.decay_age(5730, 0.25)"), 6);
        Assert.Equal(216.65, Number("phys.atmosphere(11000).temperature"), 2);
        Assert.InRange(Number("phys.atmosphere(11000).pressure"), 22630, 22635);
        Assert.Equal(1.25, Number("phys.lorentz(0.6 * 299792458).gamma"), 12);
    }

    /// <summary>Масса 1 кг на пружине 100 Н/м: ω = 10 рад/с, период 2π/10.</summary>
    [Fact]
    public void Phys_Oscillator_PeriodAndRegime()
    {
        Assert.Equal(2 * Math.PI / 10, Number("phys.oscillator(1, 100).period"), 12);
        Assert.Equal("critical", Text("phys.oscillator(1, 100, damping: 20).regime"));
        Assert.Equal(0.15, Number("phys.lens(0.1, 0.3).image_distance"), 12);
        Assert.True(Flag("math.is_nan(phys.refraction(60, 1.5, 1))"));
    }

    // --- bio ---

    [Fact]
    public void Bio_Sequences_KnownAnswers()
    {
        Assert.Equal(4.0 / 6, Number("bio.gc(\"ATGGCC\")"), 12);
        Assert.Equal("GGCCAT", Text("bio.reverse_complement(\"atggcc\")"));
        Assert.Equal("AUGGCC", Text("bio.transcribe(\"ATGGCC\")"));
        Assert.Equal("MAIVMGR", Text("bio.translate(\"ATGGCCATTGTAATGGGCCGCTGAAAGGGTGCCCGATAG\")"));
        Assert.Equal("MAIVMGR", Text("bio.translate(\"AUGGCCAUUGUAAUGGGCCGCUGA\")"));
        Assert.Equal("MAIVMGR*KGAR*", Text("bio.translate(\"ATGGCCATTGTAATGGGCCGCTGAAAGGGTGCCCGATAG\", to_stop: false)"));
    }

    [Fact]
    public void Bio_Orfs_FindsFrameWithProtein()
    {
        RunResult result = Run("""
            let t = bio.orfs("CCATGGCCATTGTAATGGGCTGAAA", min_length: 3)

            emit start = t[0].start
            emit protein = t[0].protein
            """);

        Assert.Equal(2.0, result.Emitted["start"]);
        Assert.Equal("MAIVMG", result.Emitted["protein"]);
    }

    [Fact]
    public void Bio_Comparison_KnownAnswers()
    {
        Assert.Equal(2.0, Number("bio.hamming(\"GATTACA\", \"GACTATA\")"));
        Assert.Equal(1.0, Number("bio.align(\"GATTACA\", \"GATTACA\").identity"), 12);
        Assert.Equal(-0.75 * Math.Log(1 - (4.0 / 3 * 0.1)), Number("bio.distance(\"ACGTACGTAC\", \"ACGTACGTAA\")"), 9);
        Assert.Equal(0.1, Number("bio.distance(\"ACGTACGTAC\", \"ACGTACGTAA\", kind: \"p\")"), 12);
        Assert.True(Number("bio.align(\"HEAGAWGHEE\", \"PAWHEAE\", kind: \"local\", matrix: \"blosum62\").score") > 0);
    }

    [Fact]
    public void Bio_Tree_ListsEveryLeaf()
    {
        string newick = Text("bio.tree({ a: \"ACGTACGTAC\", b: \"ACGTACGTTC\", c: \"ACGAACTTTC\", d: \"TCGAACTTTA\" }, kind: \"upgma\").newick");

        foreach (string leaf in new[] { "a", "b", "c", "d" })
            Assert.Contains(leaf, newick, StringComparison.Ordinal);

        Assert.EndsWith(";", newick, StringComparison.Ordinal);
    }

    /// <summary>Логистический рост выходит на ёмкость среды и не превышает её.</summary>
    [Fact]
    public void Bio_LogisticGrowth_ReachesCapacity()
    {
        RunResult result = Run("""
            let t = bio.growth(10, 0.5, until: 60, capacity: 1000)

            emit last = t[len(t) - 1].population
            emit first = t[0].population
            """);

        Assert.Equal(1000.0, (double)result.Emitted["last"]!, 3);
        Assert.Equal(10.0, (double)result.Emitted["first"]!, 9);
        Assert.Equal(Math.Log(2) / 0.03, Number("bio.doubling_time(0.03)"), 9);
    }

    /// <summary>SIR при R₀ = 3: пик внутри расчёта, итоговый охват — корень 1 − R = e^(−3R) ≈ 0.94.</summary>
    [Fact]
    public void Bio_Sir_HasPeakAndFinalSize()
    {
        RunResult result = Run("""
            let e1 = bio.sir(0.3, 0.1)

            emit r0 = e1.r0
            emit peak = e1.peak
            emit peak_time = e1.peak_time
            emit final_size = e1.final_size
            emit rows = len(e1.table)
            """);

        Assert.Equal(3.0, (double)result.Emitted["r0"]!, 12);
        Assert.InRange((double)result.Emitted["peak"]!, 0.2, 0.4);
        Assert.InRange((double)result.Emitted["peak_time"]!, 10, 170);
        Assert.Equal(0.9405, (double)result.Emitted["final_size"]!, 2);
        Assert.Equal(361.0, result.Emitted["rows"]);
    }

    [Fact]
    public void Bio_LotkaVolterra_ReturnsBothPopulations()
    {
        RunResult result = Run("""
            let t = bio.lotka_volterra(40, 9, alpha: 0.1, beta: 0.02, gamma: 0.1, delta: 0.01, until: 200)

            emit rows = len(t)
            emit prey0 = t[0].prey
            emit predators0 = t[0].predators
            """);

        Assert.Equal(200.0, result.Emitted["rows"]);
        Assert.Equal(40.0, result.Emitted["prey0"]);
        Assert.Equal(9.0, result.Emitted["predators0"]);
    }

    [Fact]
    public void Bio_EcologyAndGenetics_KnownAnswers()
    {
        Assert.Equal(Math.Log(4), Number("bio.diversity(<10, 10, 10, 10>).shannon"), 12);
        Assert.Equal(0.75, Number("bio.diversity(<10, 10, 10, 10>).simpson"), 12);
        Assert.Equal(0.0, Number("bio.bray_curtis(<1, 2, 3>, <1, 2, 3>)"), 12);
        Assert.Equal(0.5, Number("bio.hardy_weinberg(25, 50, 25).p"), 12);
        Assert.True(Flag("bio.hardy_weinberg(25, 50, 25).equilibrium"));
        Assert.Equal(0.0, Number("bio.mendel(<75, 25>).chi2"), 12);
        Assert.True(Flag("bio.mendel(<315, 108, 101, 32>, ratio: <9, 3, 3, 1>).fits"));
        Assert.InRange(Number("bio.carrier_frequency(1 / 10000)"), 0.0195, 0.0199);
    }

    [Fact]
    public void Bio_AmbiguousNucleotide_IsRejected()
    {
        RunResult result = Script.RunWith(Host, "emit r = bio.gc(\"ATGNNC\")");

        Assert.False(result.Success);
        Assert.Contains("N", result.Error!.Message, StringComparison.Ordinal);
    }
}

using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Прикладные пространства: экономика, СВЧ и химия.
/// </summary>
/// <remarks>
/// Проверяются не сами библиотечные расчёты — у них свои тесты, — а привязка: попали ли
/// аргументы туда, куда должны, и означают ли поля результата то, что написано в их именах.
/// Поэтому ожидания заданы величинами, которые проверяются на бумаге: удвоение цены при
/// единичной эластичности, критическая частота волновода по его ширине, молярная масса воды.
/// </remarks>
public sealed class DomainTests
{
    private static ScriptHost Host() => Script.FullHost();

    private static RunResult Run(string source) => Script.RunOk(source, new RunOptions { Seed = 7 });

    // --- экономика ---

    /// <summary>
    /// Юнит-экономика при оттоке 20 % и вкладе 500: клиент живёт пять периодов, LTV = 2500,
    /// стоимость привлечения = 100000 / 200 = 500, отношение LTV к CAC = 5.
    /// </summary>
    [Fact]
    public void Econ_Unit_ComputesCacAndLtv()
    {
        RunResult result = Run("""
            let u = econ.unit(marketing: 100000, customers: 200, revenue: 500, churn: 0.2)

            emit cac = u.cac
            emit ltv = u.ltv
            emit отношение = u.ltv_к_cac
            emit жизнь = u.срок_жизни_периодов
            """);

        Assert.Equal(500.0, (double)result.Emitted["cac"]!, 6);
        Assert.Equal(2500.0, (double)result.Emitted["ltv"]!, 6);
        Assert.Equal(5.0, (double)result.Emitted["отношение"]!, 6);
        Assert.Equal(5.0, (double)result.Emitted["жизнь"]!, 6);
    }

    [Fact]
    public void Econ_Ltv_MatchesClosedForm()
    {
        Assert.Equal(2500.0, Script.Number("econ.ltv(contribution: 500, churn: 0.2)"), 6);
    }

    [Fact]
    public void Econ_Unit_RejectsImpossibleChurn()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = econ.unit(marketing: 1, customers: 1, revenue: 1, churn: 1.5)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    /// <summary>Поток −1000, +600, +600 при ставке 0: приведённая стоимость ровно 200.</summary>
    [Fact]
    public void Econ_Npv_AtZeroRateIsPlainSum()
    {
        Assert.Equal(200.0, Script.Number("econ.npv(<-1000, 600, 600>, rate: 0)"), 9);
    }

    /// <summary>Вложили 1000, вернули 1200 через период — внутренняя норма ровно 20 %.</summary>
    [Fact]
    public void Econ_Irr_IsExactOnSimpleFlow()
    {
        Assert.Equal(0.2, Script.Number("econ.irr(<-1000, 1200>)"), 6);
    }

    [Fact]
    public void Econ_Appraise_ReportsSignChangesAndVerdict()
    {
        RunResult result = Run("""
            let a = econ.appraise(<-5000, 1500, 2000, 2500, 1200>, rate: 0.1)

            emit npv = a.npv
            emit смен = a.смен_знака
            emit принят = a.принимается
            """);

        Assert.True((double)result.Emitted["npv"]! > 0);
        Assert.Equal(1.0, result.Emitted["смен"]);
        Assert.Equal(1.0, result.Emitted["принят"]);
    }

    /// <summary>Постоянные 2000 при вкладе 500 с единицы — безубыточность на четырёх единицах.</summary>
    [Fact]
    public void Econ_BreakEven_IsFixedCostsOverContribution()
    {
        RunResult result = Run("""
            let b = econ.break_even(price: 1000, variable_cost: 500, fixed_costs: 2000, volume: 10)

            emit точка = b.точка_единиц
            emit вклад = b.вклад_на_единицу
            emit прибыль = b.операционная_прибыль
            """);

        Assert.Equal(4.0, (double)result.Emitted["точка"]!, 9);
        Assert.Equal(500.0, (double)result.Emitted["вклад"]!, 9);
        Assert.Equal(3000.0, (double)result.Emitted["прибыль"]!, 9);
    }

    [Fact]
    public void Econ_Loan_SumsToTotalPaid()
    {
        RunResult result = Run("""
            let l = econ.loan(principal: 1200000, rate: 0.12, periods: 12)

            emit платежей = len(l.платёж)
            emit сумма = core.round(vec.sum(l.платёж), digits: 2)
            emit всего = core.round(l.всего_выплат, digits: 2)
            emit остаток = core.round(l.остаток[11], digits: 6)
            """);

        Assert.Equal(12.0, result.Emitted["платежей"]);
        Assert.Equal(result.Emitted["всего"], result.Emitted["сумма"]);

        // Долг к последнему платежу погашен: график сходится, а не обрывается.
        Assert.Equal(0.0, (double)result.Emitted["остаток"]!, 6);
    }

    [Fact]
    public void Econ_Loan_UnknownKind_IsReported()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = econ.loan(principal: 1, rate: 0.1, periods: 2, kind: \"пополам\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("annuity", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Единичная эластичность: рост цены вдвое роняет спрос вдвое.</summary>
    [Fact]
    public void Econ_DemandAt_FollowsElasticity()
    {
        Assert.Equal(
            250.0,
            Script.Number("econ.demand_at(price: 100, quantity: 500, elasticity: -1, new_price: 200)"),
            6);
    }

    [Fact]
    public void Econ_Elasticity_RecoversPlantedSlope()
    {
        // Спрос построен по правилу q = 1000 * p^(-1.5): оценка обязана вернуть показатель.
        RunResult result = Run("""
            let p = <100, 120, 140, 160, 180, 200, 220>
            let q = vec.of(range(len(p)) |> core.map(i => 1000 * math.pow(p[i], -1.5)))
            let оценка = econ.elasticity(p, q)

            emit эластичность = core.round(оценка.эластичность, digits: 3)
            emit r2 = core.round(оценка.r2, digits: 3)
            """);

        Assert.Equal(-1.5, (double)result.Emitted["эластичность"]!, 2);
        Assert.Equal(1.0, (double)result.Emitted["r2"]!, 2);
    }

    [Fact]
    public void Econ_Forecast_ContinuesTrendInsideInterval()
    {
        RunResult result = Run("""
            let ряд = vec.of(range(24) |> core.map(i => 100 + (5 * i)))
            let f = econ.forecast(ряд, horizon: 3)

            emit точек = len(f.прогноз)
            emit первый = f.прогноз[0]
            emit низ = f.низ[0]
            emit верх = f.верх[0]
            emit модель = f.модель
            """);

        Assert.Equal(3.0, result.Emitted["точек"]);
        Assert.InRange((double)result.Emitted["первый"]!, 210.0, 230.0);
        Assert.True((double)result.Emitted["низ"]! <= (double)result.Emitted["первый"]!);
        Assert.True((double)result.Emitted["верх"]! >= (double)result.Emitted["первый"]!);
        Assert.NotEmpty((string)result.Emitted["модель"]!);
    }

    /// <summary>Расход денег без роста: 1000 при чистом сжигании 100 в месяц — десять месяцев.</summary>
    [Fact]
    public void Econ_Runway_IsDeterministicWithoutVolatility()
    {
        RunResult result = Run("""
            let r = econ.runway(cash: 1000, revenue: 0, costs: 100, horizon: 24, simulations: 200)

            emit месяцев = r.месяцев
            emit выжить = r.вероятность_выжить
            """);

        Assert.Equal(10.0, (double)result.Emitted["месяцев"]!, 6);
        Assert.Equal(0.0, (double)result.Emitted["выжить"]!, 6);
    }

    /// <summary>Имитации привязаны к зерну прогона, иначе один и тот же скрипт врал бы по-разному.</summary>
    [Fact]
    public void Econ_Runway_IsReproducible()
    {
        const string source = """
            options { seed: 3 }

            let r = econ.runway(cash: 5000, revenue: 300, costs: 800,
                                growth: 0.05, growth_sigma: 0.2, horizon: 24, simulations: 300)

            emit p50 = r.месяцев_p50
            """;

        Assert.Equal(Script.RunOk(source).Emitted["p50"], Script.RunOk(source).Emitted["p50"]);
    }

    [Fact]
    public void Econ_RiskMeasures_AreOrdered()
    {
        RunResult result = Run("""
            options { seed: 11 }

            let доходности = signal.noise(500, sigma: 0.02)

            emit var = econ.var(доходности, confidence: 0.95)
            emit cvar = econ.cvar(доходности, confidence: 0.95)
            emit просадка = econ.drawdown(доходности).максимальная
            emit шарп = econ.performance(доходности).шарп
            """);

        // Ожидаемые потери в хвосте не меньше порога: это определение, а не свойство данных.
        Assert.True((double)result.Emitted["cvar"]! >= (double)result.Emitted["var"]!);
        Assert.True((double)result.Emitted["просадка"]! >= 0);
        Assert.True(double.IsFinite((double)result.Emitted["шарп"]!));
    }

    // --- СВЧ ---

    /// <summary>На 2.45 ГГц длина волны в свободном пространстве — 122 мм.</summary>
    [Fact]
    public void Mw_Wavelength_IsKnownValue()
    {
        Assert.Equal(0.1224, Script.Number("mw.wavelength(2.45e9)"), 4);
    }

    /// <summary>КСВ и коэффициент отражения — взаимно обратные преобразования.</summary>
    [Fact]
    public void Mw_VswrAndReflection_AreInverse()
    {
        Assert.Equal(2.0, Script.Number("mw.vswr(gamma: mw.reflection(vswr: 2))"), 9);
        Assert.Equal(0.0, Script.Number("mw.reflection(vswr: 1)"), 9);
        Assert.Equal(1.0, Script.Number("mw.mismatch_efficiency(vswr: 1)"), 9);
    }

    [Fact]
    public void Mw_Vswr_RejectsImpossibleValues()
    {
        Assert.Equal(DiagnosticCodes.BadOperand, Script.FailsWith("emit r = mw.reflection(vswr: 0.5)").Code);
        Assert.Equal(DiagnosticCodes.BadOperand, Script.FailsWith("emit r = mw.vswr(gamma: 1)").Code);
    }

    /// <summary>
    /// Критическая частота основной моды определяется шириной волновода: c / (2a).
    /// Для WR-340 шириной 86.36 мм это 1.736 ГГц, и 2.45 ГГц лежит в его рабочей полосе.
    /// </summary>
    [Fact]
    public void Mw_Waveguide_SelectsByFrequencyAndReportsCutoff()
    {
        RunResult result = Run("""
            let w = mw.waveguide(2.45e9)

            emit стандарт = w.стандарт
            emit критическая = w.критическая_частота
            emit распространяется = w.распространяется
            emit одномодовый = w.одномодовый
            emit длина_волны = w.длина_волны
            """);

        Assert.Equal(true, result.Emitted["распространяется"]);
        Assert.Equal(true, result.Emitted["одномодовый"]);
        Assert.True((double)result.Emitted["критическая"]! < 2.45e9);

        // В волноводе длина волны всегда больше, чем в свободном пространстве.
        Assert.True((double)result.Emitted["длина_волны"]! > 0.1224);
        Assert.NotEmpty((string)result.Emitted["стандарт"]!);
    }

    [Fact]
    public void Mw_Waveguide_UnknownStandard_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = mw.waveguide(2.45e9, standard: \"WR-нет\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("WR-", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Нагрев двух килограммов воды на 60 К мощностью 1 кВт — около 502 секунд.</summary>
    [Fact]
    public void Mw_HeatingTime_MatchesHeatBalance()
    {
        RunResult result = Run("""
            let вода = mw.material("вода")

            emit время = mw.heating_time(mass: 2, delta: 60, power: 1000,
                                         heat_capacity: вода.теплоёмкость)
            emit теплоёмкость = вода.теплоёмкость
            """);

        Assert.Equal(4186.0, result.Emitted["теплоёмкость"]);
        Assert.Equal(2 * 4186.0 * 60 / 1000, (double)result.Emitted["время"]!, 6);
    }

    [Fact]
    public void Mw_Material_UnknownName_ListsKnown()
    {
        Diagnostic error = Script.FailsWith("emit r = mw.material(\"плазма\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("Вода", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Центр слоя лежит на половине его толщины, поэтому при толщине, равной глубине
    /// проникновения, поверхность получает в √e ≈ 1.65 раза больше центра.
    /// </summary>
    [Fact]
    public void Mw_Uniformity_FollowsPenetrationDepth()
    {
        double ratio = Script.Number("mw.uniformity(thickness: 0.02, penetration: 0.02)");

        Assert.Equal(Math.Sqrt(Math.E), ratio, 6);

        // Двустороннее облучение всегда равномернее одностороннего.
        Assert.True(Script.Number("mw.uniformity(thickness: 0.02, penetration: 0.02, two_sided: true)") < ratio);
    }

    /// <summary>
    /// Пределы разных нормативов расходятся, и это ровно та причина, по которой норматив
    /// приходится называть явно.
    /// </summary>
    [Fact]
    public void Mw_ExposureLimits_DifferBetweenStandards()
    {
        double sanpin = Script.Number("mw.exposure_limit(2.45e9, standard: \"sanpin\")");
        double icnirp = Script.Number("mw.exposure_limit(2.45e9, standard: \"icnirp\")");

        Assert.True(sanpin > 0 && icnirp > 0);
        Assert.NotEqual(sanpin, icnirp);

        // Персоналу разрешено не меньше, чем населению.
        Assert.True(
            Script.Number("mw.exposure_limit(2.45e9, standard: \"sanpin\", category: \"occupational\")") >= sanpin);
    }

    [Fact]
    public void Mw_ExposureLimit_UnknownStandard_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = mw.exposure_limit(2.45e9, standard: \"гост\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("icnirp", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Mw_Antenna_NarrowerBeamNeedsBiggerAperture()
    {
        RunResult result = Run("""
            let широкая = mw.antenna("horn", frequency: 2.45e9, beamwidth: 20)
            let узкая = mw.antenna("horn", frequency: 2.45e9, beamwidth: 6)

            emit усиление_широкой = широкая.усиление_дби
            emit усиление_узкой = узкая.усиление_дби
            emit апертура_широкой = широкая.апертура_ширина
            emit апертура_узкой = узкая.апертура_ширина
            emit тип = узкая.тип
            """);

        Assert.True((double)result.Emitted["усиление_узкой"]! > (double)result.Emitted["усиление_широкой"]!);
        Assert.True((double)result.Emitted["апертура_узкой"]! > (double)result.Emitted["апертура_широкой"]!);
        Assert.Contains("рупор", (string)result.Emitted["тип"]!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mw_Antenna_UnknownKind_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = mw.antenna(\"рамочная\", frequency: 2.45e9)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("parabolic", error.Hint, StringComparison.Ordinal);
    }

    // --- химия ---

    /// <summary>Молярная масса воды — 18.015 г/моль; она же проверяет разбор формулы.</summary>
    [Fact]
    public void Chem_Mass_IsKnownValue()
    {
        RunResult result = Script.RunWith(Host(), """
            emit вода = core.round(chem.mass("H2O"), digits: 3)
            emit купорос = core.round(chem.mass("CuSO4·5H2O"), digits: 2)
            """);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(18.015, result.Emitted["вода"]);
        Assert.Equal(249.68, (double)result.Emitted["купорос"]!, 1);
    }

    [Fact]
    public void Chem_Formula_ReportsComposition()
    {
        RunResult result = Script.RunWith(Host(), """
            let f = chem.formula("Ca(OH)2")

            emit масса = core.round(f.mass, digits: 3)
            emit элементов = len(f.composition)
            emit известна = f.known
            """);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(74.093, (double)result.Emitted["масса"]!, 2);
        Assert.Equal(3.0, result.Emitted["элементов"]);
        Assert.Equal(true, result.Emitted["известна"]);
    }

    /// <summary>
    /// Баланс уравнения — то, что языковая модель пишет по памяти и ошибается: здесь он
    /// считается и проверяется сохранением атомов.
    /// </summary>
    [Fact]
    public void Chem_Balance_AddsCoefficients()
    {
        RunResult result = Script.RunWith(Host(), "emit r = chem.balance(\"Fe + O2 = Fe2O3\")");

        Assert.True(result.Success, Script.Report(result));

        string balanced = (string)result.Emitted["r"]!;

        Assert.Contains("4", balanced, StringComparison.Ordinal);
        Assert.Contains("Fe2O3", balanced, StringComparison.Ordinal);
    }

    [Fact]
    public void Chem_Check_RejectsUnbalancedEquation()
    {
        RunResult result = Script.RunWith(Host(), """
            emit неверное = chem.check("Fe + O2 = Fe2O3")
            emit верное = chem.check("4Fe + 3O2 = 2Fe2O3")
            """);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(false, Field(result.Emitted["неверное"], "balanced"));
        Assert.Equal(true, Field(result.Emitted["верное"], "balanced"));
    }

    /// <summary>Химия видна тому же хосту, что и остальные пространства.</summary>
    [Fact]
    public void Chem_IsPartOfFullHost()
    {
        string index = Host().DescribeCapabilities(AI.Script.Docs.ManifestOptions.Index);

        Assert.Contains("**chem**", index, StringComparison.Ordinal);
        Assert.Contains("**econ**", index, StringComparison.Ordinal);
        Assert.Contains("**mw**", index, StringComparison.Ordinal);
    }

    private static object? Field(object? record, string name)
    {
        var fields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(record);

        return fields[name];
    }
}

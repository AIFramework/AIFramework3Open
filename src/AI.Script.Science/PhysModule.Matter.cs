using AI.Physics.Electromagnetism;
using AI.Physics.Fluids;
using AI.Physics.Nuclear;
using AI.Physics.Thermodynamics;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;
using AI.Units;

namespace AI.Script.Science;

/// <summary>
/// Вещество и поля: газы, теплота, электрические цепи, радиоактивный распад и атмосфера.
/// </summary>
public static partial class PhysModule
{
    // --- газы и теплота ---

    /// <summary>
    /// Уравнение состояния идеального газа <c>pV = nRT</c>.
    /// </summary>
    /// <remarks>
    /// Задайте три величины из четырёх — четвёртая найдётся; ответ — запись всех четырёх. Объём в
    /// кубометрах (литр — 0.001), температура в кельвинах (0 °C — 273.15).
    /// </remarks>
    [ScriptFn("ideal_gas", "Идеальный газ pV = nRT: недостающая величина по трём известным",
        Example = "phys.ideal_gas(moles: 1, temperature: 273.15, pressure: 101325).volume")]
    public static ScriptRecord IdealGasState(
        [ScriptParam("количество вещества, моль")] double moles = double.NaN,
        [ScriptParam("температура, К")] double temperature = double.NaN,
        [ScriptParam("объём, м³")] double volume = double.NaN,
        [ScriptParam("давление, Па")] double pressure = double.NaN)
    {
        const string function = "phys.ideal_gas";

        int unknown = (double.IsNaN(moles) ? 1 : 0) + (double.IsNaN(temperature) ? 1 : 0)
            + (double.IsNaN(volume) ? 1 : 0) + (double.IsNaN(pressure) ? 1 : 0);

        if (unknown != 1)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: известных величин должно быть ровно три, а неизвестных — одна; неизвестных {unknown}",
                "например: phys.ideal_gas(moles: 1, temperature: 300, volume: 0.025)");
        }

        var n = new Quantity(moles, Dimension.AmountDim);
        var t = new Quantity(temperature, Dimension.TemperatureDim);
        var v = new Quantity(volume, Dimension.Volume);
        var p = new Quantity(pressure, Dimension.Pressure);

        if (double.IsNaN(pressure)) pressure = IdealGas.Pressure(n, t, v).SiValue;
        else if (double.IsNaN(volume)) volume = IdealGas.Volume(n, t, p).SiValue;
        else if (double.IsNaN(temperature)) temperature = IdealGas.Temperature(p, v, n).SiValue;
        else moles = pressure * volume / (PhysicalConstants.GasConstant.SiValue * temperature);

        return ScriptData.Plain(("moles", moles), ("temperature", temperature), ("volume", volume), ("pressure", pressure));
    }

    [ScriptFn("heat_conduction", "Тепловой поток через стенку по закону Фурье, Вт",
        Example = "phys.heat_conduction(0.8, 20, 0.25, area: 10)")]
    public static double HeatConduction(
        [ScriptParam("теплопроводность материала, Вт/(м·К)")] double conductivity,
        [ScriptParam("разность температур по сторонам стенки, К")] double delta_t,
        [ScriptParam("толщина стенки, м")] double thickness,
        [ScriptParam("площадь стенки, м²")] double area = 1)
        => HeatTransfer.Conduction(
            new Quantity(conductivity, HeatTransfer.ConductivityDimension),
            new Quantity(delta_t, Dimension.TemperatureDim),
            new Quantity(thickness, Dimension.LengthDim)).SiValue * area;

    /// <summary>
    /// Остывание по закону Ньютона: сколько ждать, пока тело остынет до заданной температуры.
    /// </summary>
    /// <remarks>
    /// Постоянная времени <c>tau</c> — за неё превышение над средой падает в e раз; её берут из
    /// опыта либо считают как <c>m·c / (α·S)</c>. Закон верен, пока тело прогрето равномерно
    /// (малое число Био): чашка — да, толстая отливка — нет.
    /// </remarks>
    [ScriptFn("cooling_time", "Время остывания тела до заданной температуры по закону Ньютона, с",
        Example = "phys.cooling_time(90, 50, 20, tau: 900)")]
    public static double CoolingTime(
        [ScriptParam("начальная температура тела")] double start,
        [ScriptParam("температура, до которой остывает")] double target,
        [ScriptParam("температура среды")] double ambient,
        [ScriptParam("постоянная времени остывания, с")] double tau)
        => HeatTransfer.CoolingTime(
            new Quantity(start - ambient, Dimension.TemperatureDim),
            new Quantity(target - ambient, Dimension.TemperatureDim),
            new Quantity(tau, Dimension.TimeDim)).SiValue;

    /// <summary>
    /// Идеальная тепловая машина между двумя температурами.
    /// </summary>
    /// <remarks>
    /// <c>efficiency</c> — предел КПД любого двигателя, <c>cop_cooling</c> — холодильника,
    /// <c>cop_heating</c> — теплового насоса между теми же температурами. Температуры — в кельвинах.
    /// </remarks>
    [ScriptFn("carnot", "Цикл Карно: предельный КПД и холодильный коэффициент, температуры в К",
        Example = "phys.carnot(500, 300).efficiency")]
    public static ScriptRecord Carnot(
        [ScriptParam("температура нагревателя, К")] double hot,
        [ScriptParam("температура холодильника, К")] double cold)
    {
        var hotTemperature = new Quantity(hot, Dimension.TemperatureDim);
        var coldTemperature = new Quantity(cold, Dimension.TemperatureDim);
        CycleResult cycle = Cycles.Carnot(hotTemperature, coldTemperature);

        return ScriptData.Record(cycle,
            ("efficiency", cycle.Efficiency),
            ("cop_cooling", Cycles.RefrigeratorCoefficient(hotTemperature, coldTemperature)),
            ("cop_heating", Cycles.HeatPumpCoefficient(hotTemperature, coldTemperature)));
    }

    // --- электричество ---

    [ScriptFn("ohm", "Закон Ома: ток и выделяемая мощность по напряжению и сопротивлению",
        Example = "phys.ohm(220, 50).current")]
    public static ScriptRecord Ohm(
        [ScriptParam("напряжение, В")] double voltage,
        [ScriptParam("сопротивление, Ом")] double resistance)
    {
        var u = new Quantity(voltage, Dimension.Voltage);
        Quantity current = Circuits.Current(u, new Quantity(resistance, Dimension.Resistance));

        return ScriptData.Plain(("current", current.SiValue), ("power", Circuits.Power(u, current).SiValue));
    }

    /// <summary>
    /// Заряд конденсатора через резистор.
    /// </summary>
    /// <remarks>
    /// <c>tau = R·C</c>; за одну постоянную времени конденсатор заряжается на 63 %, за пять — на
    /// 99 %. С <c>time</c> в ответе ещё напряжение и доля заряда в этот момент.
    /// </remarks>
    [ScriptFn("rc", "RC-цепь: постоянная времени и напряжение на конденсаторе при заряде",
        Example = "phys.rc(1000, 1e-6, supply: 5, time: 0.002).voltage")]
    public static ScriptRecord Rc(
        [ScriptParam("сопротивление, Ом")] double resistance,
        [ScriptParam("ёмкость, Ф")] double capacitance,
        [ScriptParam("напряжение источника, В")] double supply = 1,
        [ScriptParam("время от начала заряда, с")] double time = 0)
    {
        Quantity tau = Circuits.TimeConstantRC(new Quantity(resistance, Dimension.Resistance), new Quantity(capacitance, Dimension.Capacitance));
        double voltage = Circuits.ChargingVoltage(new Quantity(supply, Dimension.Voltage), tau, new Quantity(time, Dimension.TimeDim)).SiValue;

        return ScriptData.Plain(
            ("tau", tau.SiValue),
            ("voltage", voltage),
            ("fraction", supply == 0 ? double.NaN : voltage / supply));
    }

    /// <summary>
    /// Последовательный колебательный контур.
    /// </summary>
    /// <remarks>
    /// <c>regime</c> — <c>underdamped</c> (затухающие колебания), <c>critical</c> либо
    /// <c>overdamped</c>; без сопротивления добротность бесконечна.
    /// </remarks>
    [ScriptFn("rlc", "Колебательный контур: резонансная частота, добротность и режим",
        Example = "phys.rlc(1e-3, 1e-6, resistance: 10).frequency")]
    public static ScriptRecord Rlc(
        [ScriptParam("индуктивность, Гн")] double inductance,
        [ScriptParam("ёмкость, Ф")] double capacitance,
        [ScriptParam("сопротивление, Ом")] double resistance = 0)
    {
        var l = new Quantity(inductance, Dimension.Inductance);
        var c = new Quantity(capacitance, Dimension.Capacitance);
        var r = new Quantity(resistance, Dimension.Resistance);

        return ScriptData.Plain(
            ("frequency", Circuits.ResonanceFrequency(l, c).SiValue),
            ("quality", Circuits.QualityFactor(l, c, r)),
            ("regime", Circuits.Regime(l, c, r) switch
            {
                CircuitRegime.Underdamped => "underdamped",
                CircuitRegime.Critical => "critical",
                _ => "overdamped",
            }));
    }

    // --- распад ---

    /// <summary>
    /// Радиоактивный распад за заданное время.
    /// </summary>
    /// <remarks>
    /// Время и период полураспада — в одних единицах, любых: годы так годы. <c>remaining</c> —
    /// сколько осталось от <c>amount</c>, <c>decay_constant</c> и <c>mean_life</c> — в тех же
    /// единицах времени.
    /// </remarks>
    [ScriptFn("decay", "Радиоактивный распад: остаток через время, постоянная распада",
        Example = "phys.decay(5730, 10000).fraction")]
    public static ScriptRecord Decay(
        [ScriptParam("период полураспада")] double half_life,
        [ScriptParam("прошедшее время, в тех же единицах")] double time,
        [ScriptParam("начальное количество: ядра, граммы, беккерели")] double amount = 1)
    {
        var halfLife = new Quantity(half_life, Dimension.TimeDim);
        double fraction = RadioactiveDecay.RemainingFraction(halfLife, new Quantity(time, Dimension.TimeDim));

        return ScriptData.Plain(
            ("remaining", amount * fraction),
            ("fraction", fraction),
            ("decay_constant", RadioactiveDecay.DecayConstant(halfLife).SiValue),
            ("mean_life", RadioactiveDecay.MeanLife(halfLife).SiValue));
    }

    /// <summary>
    /// Возраст по оставшейся доле радионуклида — радиоуглеродный и иной датировки.
    /// </summary>
    /// <remarks>
    /// Предполагается, что начальная доля известна и ядра не приносились извне: нарушение любого
    /// из допущений сдвигает возраст сильнее, чем погрешность измерения.
    /// </remarks>
    [ScriptFn("decay_age", "Возраст образца по оставшейся доле радионуклида",
        Example = "phys.decay_age(5730, 0.25)")]
    public static double DecayAge(
        [ScriptParam("период полураспада")] double half_life,
        [ScriptParam("оставшаяся доля, от 0 до 1")] double fraction)
        => RadioactiveDecay.ElapsedTime(new Quantity(half_life, Dimension.TimeDim), fraction).SiValue;

    [ScriptFn("activity", "Активность радионуклида, распадов в секунду (Бк)",
        Example = "phys.activity(9.5e8, 2.56e21)")]
    public static double Activity(
        [ScriptParam("период полураспада, с")] double half_life,
        [ScriptParam("число ядер")] double atoms)
        => RadioactiveDecay.Activity(new Quantity(half_life, Dimension.TimeDim), atoms).SiValue;

    // --- атмосфера ---

    /// <summary>
    /// Международная стандартная атмосфера ISA (ГОСТ 4401-81 ниже 47 км).
    /// </summary>
    /// <remarks>
    /// Высота геопотенциальная, от −5 до 84.852 км; для самолётов и аэростатов она практически
    /// совпадает с геометрической. Температура в кельвинах, давление в паскалях, плотность в кг/м³,
    /// вязкость динамическая, Па·с.
    /// </remarks>
    [ScriptFn("atmosphere", "Стандартная атмосфера на высоте: температура, давление, плотность",
        Example = "phys.atmosphere(11000).pressure")]
    public static ScriptRecord Atmosphere([ScriptParam("высота над уровнем моря, м")] double altitude)
    {
        var h = new Quantity(altitude, Dimension.LengthDim);

        return ScriptData.Plain(
            ("temperature", StandardAtmosphere.Temperature(h).SiValue),
            ("pressure", StandardAtmosphere.Pressure(h).SiValue),
            ("density", StandardAtmosphere.Density(h).SiValue),
            ("speed_of_sound", StandardAtmosphere.SpeedOfSound(h).SiValue),
            ("viscosity", StandardAtmosphere.DynamicViscosity(h).SiValue));
    }
}

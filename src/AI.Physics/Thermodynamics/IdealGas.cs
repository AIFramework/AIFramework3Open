using AI.Units;

namespace AI.Physics.Thermodynamics;

/// <summary>Число степеней свободы молекулы</summary>
public enum GasKind
{
    /// <summary>Одноатомный: три поступательные степени свободы, γ = 5/3</summary>
    Monatomic,

    /// <summary>Двухатомный: добавляются две вращательные, γ = 7/5</summary>
    Diatomic,

    /// <summary>Многоатомный: три вращательные, γ = 4/3</summary>
    Polyatomic
}

/// <summary>
/// Идеальный газ: уравнение состояния и термодинамические процессы.
/// </summary>
/// <remarks>
/// <para>
/// Уравнение состояния и работа процессов записаны через универсальную газовую постоянную
/// из <see cref="PhysicalConstants"/> — своей копии числа 8.314 здесь нет.
/// </para>
/// <para>
/// Модель верна для разреженного газа вдали от конденсации. Вблизи критической точки
/// и при высоких давлениях нужны уравнения с поправками на объём молекул и притяжение.
/// </para>
/// </remarks>
public static class IdealGas
{
    /// <summary>Показатель адиабаты γ</summary>
    /// <param name="kind">Род газа</param>
    public static double HeatCapacityRatio(GasKind kind) => kind switch
    {
        GasKind.Monatomic => 5.0 / 3.0,
        GasKind.Diatomic => 7.0 / 5.0,
        _ => 4.0 / 3.0
    };

    /// <summary>Молярная теплоёмкость при постоянном объёме</summary>
    /// <param name="kind">Род газа</param>
    public static Quantity MolarHeatCapacityAtConstantVolume(GasKind kind)
    {
        double degrees = kind switch
        {
            GasKind.Monatomic => 3.0,
            GasKind.Diatomic => 5.0,
            _ => 6.0
        };

        return PhysicalConstants.GasConstant * (degrees / 2.0);
    }

    /// <summary>Молярная теплоёмкость при постоянном давлении: <c>C_p = C_v + R</c></summary>
    /// <param name="kind">Род газа</param>
    public static Quantity MolarHeatCapacityAtConstantPressure(GasKind kind)
        => MolarHeatCapacityAtConstantVolume(kind) + PhysicalConstants.GasConstant;

    /// <summary>Давление по уравнению состояния: <c>p = nRT/V</c></summary>
    /// <param name="amount">Количество вещества</param>
    /// <param name="temperature">Температура</param>
    /// <param name="volume">Объём</param>
    public static Quantity Pressure(Quantity amount, Quantity temperature, Quantity volume)
    {
        (double n, double t) = ReadState(amount, temperature);
        double v = volume.RequireSi(Dimension.Volume, nameof(volume));

        return new Quantity(n * PhysicalConstants.GasConstant.SiValue * t / v, Dimension.Pressure);
    }

    /// <summary>Объём по уравнению состояния: <c>V = nRT/p</c></summary>
    /// <param name="amount">Количество вещества</param>
    /// <param name="temperature">Температура</param>
    /// <param name="pressure">Давление</param>
    public static Quantity Volume(Quantity amount, Quantity temperature, Quantity pressure)
    {
        (double n, double t) = ReadState(amount, temperature);
        double p = pressure.RequireSi(Dimension.Pressure, nameof(pressure));

        return new Quantity(n * PhysicalConstants.GasConstant.SiValue * t / p, Dimension.Volume);
    }

    /// <summary>Температура по уравнению состояния: <c>T = pV/(nR)</c></summary>
    /// <param name="pressure">Давление</param>
    /// <param name="volume">Объём</param>
    /// <param name="amount">Количество вещества</param>
    public static Quantity Temperature(Quantity pressure, Quantity volume, Quantity amount)
    {
        double p = pressure.RequireSi(Dimension.Pressure, nameof(pressure));
        double v = volume.RequireSi(Dimension.Volume, nameof(volume));
        double n = amount.RequireSi(Dimension.AmountDim, nameof(amount));

        return new Quantity(p * v / (n * PhysicalConstants.GasConstant.SiValue), Dimension.TemperatureDim);
    }

    /// <summary>Внутренняя энергия: <c>U = n·C_v·T</c></summary>
    /// <param name="amount">Количество вещества</param>
    /// <param name="temperature">Температура</param>
    /// <param name="kind">Род газа</param>
    public static Quantity InternalEnergy(Quantity amount, Quantity temperature, GasKind kind = GasKind.Diatomic)
    {
        (double n, double t) = ReadState(amount, temperature);

        return new Quantity(n * MolarHeatCapacityAtConstantVolume(kind).SiValue * t, Dimension.Energy);
    }

    /// <summary>
    /// Работа газа при изотермическом расширении: <c>A = nRT·ln(V₂/V₁)</c>
    /// </summary>
    /// <param name="amount">Количество вещества</param>
    /// <param name="temperature">Температура</param>
    /// <param name="initialVolume">Начальный объём</param>
    /// <param name="finalVolume">Конечный объём</param>
    public static Quantity IsothermalWork(
        Quantity amount, Quantity temperature, Quantity initialVolume, Quantity finalVolume)
    {
        (double n, double t) = ReadState(amount, temperature);
        double v1 = initialVolume.RequireSi(Dimension.Volume, nameof(initialVolume));
        double v2 = finalVolume.RequireSi(Dimension.Volume, nameof(finalVolume));

        return new Quantity(n * PhysicalConstants.GasConstant.SiValue * t * Math.Log(v2 / v1), Dimension.Energy);
    }

    /// <summary>
    /// Работа при изобарном процессе: <c>A = p·ΔV</c>
    /// </summary>
    /// <param name="pressure">Давление</param>
    /// <param name="initialVolume">Начальный объём</param>
    /// <param name="finalVolume">Конечный объём</param>
    public static Quantity IsobaricWork(Quantity pressure, Quantity initialVolume, Quantity finalVolume)
    {
        double p = pressure.RequireSi(Dimension.Pressure, nameof(pressure));
        double v1 = initialVolume.RequireSi(Dimension.Volume, nameof(initialVolume));
        double v2 = finalVolume.RequireSi(Dimension.Volume, nameof(finalVolume));

        return new Quantity(p * (v2 - v1), Dimension.Energy);
    }

    /// <summary>
    /// Температура после адиабатного процесса: <c>T·V^(γ−1) = const</c>
    /// </summary>
    /// <param name="temperature">Начальная температура</param>
    /// <param name="initialVolume">Начальный объём</param>
    /// <param name="finalVolume">Конечный объём</param>
    /// <param name="kind">Род газа</param>
    public static Quantity AdiabaticTemperature(
        Quantity temperature, Quantity initialVolume, Quantity finalVolume, GasKind kind = GasKind.Diatomic)
    {
        double t = temperature.RequireSi(Dimension.TemperatureDim, nameof(temperature));
        double v1 = initialVolume.RequireSi(Dimension.Volume, nameof(initialVolume));
        double v2 = finalVolume.RequireSi(Dimension.Volume, nameof(finalVolume));
        double gamma = HeatCapacityRatio(kind);

        return new Quantity(t * Math.Pow(v1 / v2, gamma - 1), Dimension.TemperatureDim);
    }

    /// <summary>
    /// Давление после адиабатного процесса: <c>p·V^γ = const</c>
    /// </summary>
    /// <param name="pressure">Начальное давление</param>
    /// <param name="initialVolume">Начальный объём</param>
    /// <param name="finalVolume">Конечный объём</param>
    /// <param name="kind">Род газа</param>
    public static Quantity AdiabaticPressure(
        Quantity pressure, Quantity initialVolume, Quantity finalVolume, GasKind kind = GasKind.Diatomic)
    {
        double p = pressure.RequireSi(Dimension.Pressure, nameof(pressure));
        double v1 = initialVolume.RequireSi(Dimension.Volume, nameof(initialVolume));
        double v2 = finalVolume.RequireSi(Dimension.Volume, nameof(finalVolume));
        double gamma = HeatCapacityRatio(kind);

        return new Quantity(p * Math.Pow(v1 / v2, gamma), Dimension.Pressure);
    }

    /// <summary>
    /// Изменение энтропии идеального газа между двумя состояниями
    /// </summary>
    /// <param name="amount">Количество вещества</param>
    /// <param name="initialTemperature">Начальная температура</param>
    /// <param name="finalTemperature">Конечная температура</param>
    /// <param name="initialVolume">Начальный объём</param>
    /// <param name="finalVolume">Конечный объём</param>
    /// <param name="kind">Род газа</param>
    public static Quantity EntropyChange(
        Quantity amount,
        Quantity initialTemperature, Quantity finalTemperature,
        Quantity initialVolume, Quantity finalVolume,
        GasKind kind = GasKind.Diatomic)
    {
        double n = amount.RequireSi(Dimension.AmountDim, nameof(amount));
        double t1 = initialTemperature.RequireSi(Dimension.TemperatureDim, nameof(initialTemperature));
        double t2 = finalTemperature.RequireSi(Dimension.TemperatureDim, nameof(finalTemperature));
        double v1 = initialVolume.RequireSi(Dimension.Volume, nameof(initialVolume));
        double v2 = finalVolume.RequireSi(Dimension.Volume, nameof(finalVolume));

        double cv = MolarHeatCapacityAtConstantVolume(kind).SiValue;
        double r = PhysicalConstants.GasConstant.SiValue;

        double change = n * ((cv * Math.Log(t2 / t1)) + (r * Math.Log(v2 / v1)));

        return new Quantity(change, Dimension.Energy / Dimension.TemperatureDim);
    }

    /// <summary>
    /// Скорость звука в идеальном газе: <c>c = √(γRT/M)</c>
    /// </summary>
    /// <param name="temperature">Температура</param>
    /// <param name="molarMass">Молярная масса</param>
    /// <param name="kind">Род газа</param>
    public static Quantity SpeedOfSound(Quantity temperature, Quantity molarMass, GasKind kind = GasKind.Diatomic)
    {
        double t = temperature.RequireSi(Dimension.TemperatureDim, nameof(temperature));
        double m = molarMass.RequireSi(Dimension.MassDim / Dimension.AmountDim, nameof(molarMass));
        double gamma = HeatCapacityRatio(kind);

        return new Quantity(Math.Sqrt(gamma * PhysicalConstants.GasConstant.SiValue * t / m), Dimension.Velocity);
    }

    private static (double Amount, double Temperature) ReadState(Quantity amount, Quantity temperature)
    {
        double n = amount.RequireSi(Dimension.AmountDim, nameof(amount));
        double t = temperature.RequireSi(Dimension.TemperatureDim, nameof(temperature));

        if (t <= 0)
            throw new ArgumentException("Температура задаётся по абсолютной шкале и должна быть положительной", nameof(temperature));

        return (n, t);
    }
}

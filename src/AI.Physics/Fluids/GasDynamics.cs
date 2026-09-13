using AI.Insights;
using AI.Physics.Thermodynamics;
using AI.Units;

namespace AI.Physics.Fluids;

/// <summary>Отношения параметров изоэнтропического течения к параметрам торможения</summary>
/// <param name="Mach">Число Маха</param>
/// <param name="Temperature">T/T₀</param>
/// <param name="Pressure">p/p₀</param>
/// <param name="Density">ρ/ρ₀</param>
/// <param name="Area">A/A* — площадь сечения, отнесённая к критической</param>
public readonly record struct IsentropicRatios(double Mach, double Temperature, double Pressure, double Density, double Area);

/// <summary>Прямой скачок уплотнения</summary>
public sealed class NormalShock : IInterpretable
{
    internal NormalShock(double upstream, double gamma)
    {
        double m2 = upstream * upstream;

        UpstreamMach = upstream;
        Gamma = gamma;
        DownstreamMach = Math.Sqrt((1 + ((gamma - 1) / 2 * m2)) / ((gamma * m2) - ((gamma - 1) / 2)));
        PressureRatio = 1 + (2 * gamma / (gamma + 1) * (m2 - 1));
        DensityRatio = (gamma + 1) * m2 / (((gamma - 1) * m2) + 2);
        TemperatureRatio = PressureRatio / DensityRatio;
        StagnationPressureRatio = Math.Pow(DensityRatio, gamma / (gamma - 1))
            * Math.Pow((gamma + 1) / ((2 * gamma * m2) - (gamma - 1)), 1 / (gamma - 1));
    }

    /// <summary>Число Маха перед скачком</summary>
    public double UpstreamMach { get; }

    /// <summary>Число Маха за скачком — всегда дозвуковое</summary>
    public double DownstreamMach { get; }

    /// <summary>Показатель адиабаты</summary>
    public double Gamma { get; }

    /// <summary>p₂/p₁</summary>
    public double PressureRatio { get; }

    /// <summary>ρ₂/ρ₁ — не больше (γ + 1)/(γ − 1), шести для воздуха, при любой силе скачка</summary>
    public double DensityRatio { get; }

    /// <summary>T₂/T₁</summary>
    public double TemperatureRatio { get; }

    /// <summary>p₀₂/p₀₁ — потеря давления торможения, мера необратимости</summary>
    public double StagnationPressureRatio { get; }

    /// <summary>Рост энтропии Δs/R = −ln(p₀₂/p₀₁)</summary>
    public double EntropyRise => -Math.Log(StagnationPressureRatio);

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Прямой скачок уплотнения")
            .Summary($"Поток с M = {Fmt.Num(UpstreamMach, 3)} тормозится до M = {Fmt.Num(DownstreamMach, 3)}: давление растёт "
                + $"в {Fmt.Num(PressureRatio, 3)} раза, плотность — в {Fmt.Num(DensityRatio, 3)}, температура — в "
                + $"{Fmt.Num(TemperatureRatio, 3)}. Теряется {Fmt.Pct(1 - StagnationPressureRatio)} давления торможения.")
            .Metric("M₁", Fmt.Num(UpstreamMach, 4), null, "перед скачком")
            .Metric("M₂", Fmt.Num(DownstreamMach, 4), null, "за скачком — дозвуковое")
            .Metric("p₂/p₁", Fmt.Num(PressureRatio, 4), null, null)
            .Metric("ρ₂/ρ₁", Fmt.Num(DensityRatio, 4), null, $"не больше {Fmt.Num((Gamma + 1) / (Gamma - 1), 3)}")
            .Metric("T₂/T₁", Fmt.Num(TemperatureRatio, 4), null, null)
            .Metric("p₀₂/p₀₁", Fmt.Num(StagnationPressureRatio, 4), null, "потеря давления торможения",
                StagnationPressureRatio < 0.9 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Finding("Скачок необратим: энтропия растёт, и давление торможения не восстанавливается. Поэтому "
                + "сверхзвуковые воздухозаборники тормозят поток системой косых скачков, а не одним прямым.")
            .WarningIf(UpstreamMach > 5,
                "При M > 5 воздух за скачком нагревается настолько, что диссоциирует, и показатель адиабаты "
                + "перестаёт быть постоянным: формулы для совершенного газа завышают температуру.")
            .Build();
}

/// <summary>
/// Газовая динамика совершенного газа: изоэнтропические течения, сопла, скачки уплотнения.
/// </summary>
/// <remarks>
/// <para>
/// Одномерное течение без трения и теплообмена. Параметры торможения — те, что имел бы газ,
/// заторможенный без потерь, — вдоль такого течения постоянны; отношения к ним зависят только
/// от числа Маха и показателя адиабаты γ. Площадь сечения при M = 1 наименьшая: чтобы разогнать газ
/// до сверхзвука, сопло сначала сужают, потом расширяют — сопло Лаваля.
/// </para>
/// <para>
/// Показатель адиабаты берётся из <see cref="IdealGas.HeatCapacityRatio"/>, скорость звука — из
/// <see cref="IdealGas.SpeedOfSound"/>: своей термодинамики газа здесь нет.
/// </para>
/// </remarks>
public static class GasDynamics
{
    /// <summary>Изоэнтропические отношения при числе Маха M</summary>
    /// <param name="mach">Число Маха</param>
    /// <param name="gamma">Показатель адиабаты; для воздуха 1,4</param>
    public static IsentropicRatios Isentropic(double mach, double gamma = 1.4)
    {
        RequireGamma(gamma);

        if (!(mach >= 0) || double.IsInfinity(mach))
            throw new ArgumentOutOfRangeException(nameof(mach), "Число Маха не может быть отрицательным");

        double temperature = 1 / (1 + ((gamma - 1) / 2 * mach * mach));
        double area = mach == 0
            ? double.PositiveInfinity
            : Math.Pow(2 / (gamma + 1) / temperature, (gamma + 1) / (2 * (gamma - 1))) / mach;

        return new IsentropicRatios(
            mach, temperature, Math.Pow(temperature, gamma / (gamma - 1)), Math.Pow(temperature, 1 / (gamma - 1)), area);
    }

    /// <summary>Число Маха по отношению площадей A/A*: дозвуковой или сверхзвуковой корень</summary>
    /// <param name="areaRatio">A/A* ≥ 1</param>
    /// <param name="supersonic">Сверхзвуковая ветвь — расширяющаяся часть сопла Лаваля</param>
    /// <param name="gamma">Показатель адиабаты</param>
    public static double MachFromAreaRatio(double areaRatio, bool supersonic, double gamma = 1.4)
    {
        RequireGamma(gamma);

        if (!(areaRatio >= 1) || double.IsInfinity(areaRatio))
            throw new ArgumentOutOfRangeException(nameof(areaRatio), "Площадь не может быть меньше критической: A/A* ≥ 1");

        double low = supersonic ? 1 : 1e-12;
        double high = supersonic ? 2 : 1;

        if (supersonic)
        {
            while (Isentropic(high, gamma).Area < areaRatio)
                high *= 2;
        }

        // На каждой ветви A/A* монотонна: убывает к M = 1 слева и растёт справа
        for (int i = 0; i < 200; i++)
        {
            double middle = (low + high) / 2;
            bool above = Isentropic(middle, gamma).Area > areaRatio;

            if (above == supersonic)
                high = middle;
            else
                low = middle;
        }

        return (low + high) / 2;
    }

    /// <summary>
    /// Критическое отношение давлений (2/(γ + 1))^(γ/(γ − 1)): ниже него сопло «запирается»,
    /// и расход перестаёт зависеть от давления за ним; для воздуха 0,528
    /// </summary>
    /// <param name="gamma">Показатель адиабаты</param>
    public static double CriticalPressureRatio(double gamma = 1.4)
    {
        RequireGamma(gamma);

        return Math.Pow(2 / (gamma + 1), gamma / (gamma - 1));
    }

    /// <summary>Прямой скачок уплотнения</summary>
    /// <param name="upstreamMach">Число Маха перед скачком, больше единицы</param>
    /// <param name="gamma">Показатель адиабаты</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// M ≤ 1: скачок разрежения уменьшал бы энтропию и запрещён вторым началом
    /// </exception>
    public static NormalShock Shock(double upstreamMach, double gamma = 1.4)
    {
        RequireGamma(gamma);

        if (!(upstreamMach > 1) || double.IsInfinity(upstreamMach))
            throw new ArgumentOutOfRangeException(nameof(upstreamMach),
                "Прямой скачок возможен только в сверхзвуковом потоке: скачок разрежения уменьшал бы энтропию");

        return new NormalShock(upstreamMach, gamma);
    }

    /// <summary>Функция Прандтля — Майера ν(M), градусы: угол поворота сверхзвукового потока в волне разрежения</summary>
    /// <param name="mach">Число Маха, не меньше единицы</param>
    /// <param name="gamma">Показатель адиабаты</param>
    public static double PrandtlMeyerAngle(double mach, double gamma = 1.4)
    {
        RequireGamma(gamma);

        if (!(mach >= 1) || double.IsInfinity(mach))
            throw new ArgumentOutOfRangeException(nameof(mach), "Функция Прандтля — Майера определена при M ≥ 1");

        double k = Math.Sqrt((gamma + 1) / (gamma - 1));
        double s = Math.Sqrt((mach * mach) - 1);

        return ((k * Math.Atan(s / k)) - Math.Atan(s)) * 180 / Math.PI;
    }

    /// <summary>Число Маха по углу Прандтля — Майера</summary>
    /// <param name="angleDegrees">Угол ν, градусы</param>
    /// <param name="gamma">Показатель адиабаты</param>
    public static double MachFromPrandtlMeyer(double angleDegrees, double gamma = 1.4)
    {
        RequireGamma(gamma);

        double limit = (Math.Sqrt((gamma + 1) / (gamma - 1)) - 1) * 90;

        if (!(angleDegrees >= 0 && angleDegrees < limit))
            throw new ArgumentOutOfRangeException(nameof(angleDegrees),
                $"Угол должен лежать на [0; {limit:F2}°): больший поворот уводит поток в вакуум");

        double low = 1, high = 2;

        while (PrandtlMeyerAngle(high, gamma) < angleDegrees)
            high *= 2;

        for (int i = 0; i < 200; i++)
        {
            double middle = (low + high) / 2;

            if (PrandtlMeyerAngle(middle, gamma) < angleDegrees)
                low = middle;
            else
                high = middle;
        }

        return (low + high) / 2;
    }

    /// <summary>
    /// Расход через запертое сопло: A*·p₀·√(γ/(R_уд·T₀))·(2/(γ + 1))^((γ + 1)/(2(γ − 1)))
    /// </summary>
    /// <param name="throatArea">Площадь критического сечения</param>
    /// <param name="stagnationPressure">Давление торможения</param>
    /// <param name="stagnationTemperature">Температура торможения</param>
    /// <param name="molarMass">Молярная масса газа</param>
    /// <param name="gamma">Показатель адиабаты</param>
    public static Quantity ChokedMassFlow(
        Quantity throatArea, Quantity stagnationPressure, Quantity stagnationTemperature, Quantity molarMass, double gamma = 1.4)
    {
        RequireGamma(gamma);

        double a = throatArea.RequireSi(Dimension.Area, nameof(throatArea));
        double p0 = stagnationPressure.RequireSi(Dimension.Pressure, nameof(stagnationPressure));
        double t0 = stagnationTemperature.RequireSi(Dimension.TemperatureDim, nameof(stagnationTemperature));
        double m = molarMass.RequireSi(Dimension.MassDim / Dimension.AmountDim, nameof(molarMass));
        double specificGasConstant = PhysicalConstants.GasConstant.SiValue / m;

        double flow = a * p0 * Math.Sqrt(gamma / (specificGasConstant * t0))
            * Math.Pow(2 / (gamma + 1), (gamma + 1) / (2 * (gamma - 1)));

        return new Quantity(flow, Dimension.MassDim / Dimension.TimeDim);
    }

    /// <summary>Число Маха по скорости и температуре газа; скорость звука — из термодинамики идеального газа</summary>
    /// <param name="speed">Скорость потока</param>
    /// <param name="temperature">Статическая температура</param>
    /// <param name="molarMass">Молярная масса</param>
    /// <param name="kind">Тип молекул — определяет γ</param>
    public static double Mach(Quantity speed, Quantity temperature, Quantity molarMass, GasKind kind = GasKind.Diatomic)
        => speed.RequireSi(Dimension.Velocity, nameof(speed)) / IdealGas.SpeedOfSound(temperature, molarMass, kind).SiValue;

    private static void RequireGamma(double gamma)
    {
        if (!(gamma > 1) || double.IsInfinity(gamma))
            throw new ArgumentOutOfRangeException(nameof(gamma), "Показатель адиабаты больше единицы");
    }
}

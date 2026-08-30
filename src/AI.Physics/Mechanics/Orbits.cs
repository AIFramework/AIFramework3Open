using AI.Units;

namespace AI.Physics.Mechanics;

/// <summary>
/// Задача двух тел: круговые и эллиптические орбиты, переходы между ними.
/// </summary>
/// <remarks>
/// Всё сводится к гравитационному параметру <c>μ = G·M</c>: он измеряется точнее, чем масса
/// и постоянная тяготения по отдельности, и именно через него записаны все формулы.
/// </remarks>
public static class Orbits
{
    /// <summary>Гравитационный параметр <c>μ = G·M</c></summary>
    /// <param name="mass">Масса центрального тела</param>
    public static Quantity GravitationalParameter(Quantity mass)
    {
        double m = mass.RequireSi(Dimension.MassDim, nameof(mass));

        return new Quantity(PhysicalConstants.GravitationalConstant.SiValue * m, Dimension.Volume / Dimension.TimeDim.Pow(2));
    }

    /// <summary>Скорость на круговой орбите: <c>v = √(μ/r)</c></summary>
    /// <param name="parameter">Гравитационный параметр центрального тела</param>
    /// <param name="radius">Радиус орбиты</param>
    public static Quantity CircularSpeed(Quantity parameter, Quantity radius)
    {
        (double mu, double r) = Read(parameter, radius);

        return new Quantity(Math.Sqrt(mu / r), Dimension.Velocity);
    }

    /// <summary>Вторая космическая скорость: <c>v = √(2μ/r)</c></summary>
    /// <param name="parameter">Гравитационный параметр</param>
    /// <param name="radius">Расстояние от центра</param>
    public static Quantity EscapeSpeed(Quantity parameter, Quantity radius)
    {
        (double mu, double r) = Read(parameter, radius);

        return new Quantity(Math.Sqrt(2 * mu / r), Dimension.Velocity);
    }

    /// <summary>
    /// Период обращения по третьему закону Кеплера: <c>T = 2π·√(a³/μ)</c>
    /// </summary>
    /// <param name="parameter">Гравитационный параметр</param>
    /// <param name="semiMajorAxis">Большая полуось</param>
    public static Quantity Period(Quantity parameter, Quantity semiMajorAxis)
    {
        (double mu, double a) = Read(parameter, semiMajorAxis);

        return new Quantity(2 * Math.PI * Math.Sqrt(a * a * a / mu), Dimension.TimeDim);
    }

    /// <summary>
    /// Радиус орбиты по заданному периоду — обратная задача к третьему закону Кеплера
    /// </summary>
    /// <param name="parameter">Гравитационный параметр</param>
    /// <param name="period">Период обращения</param>
    public static Quantity RadiusForPeriod(Quantity parameter, Quantity period)
    {
        double mu = parameter.RequireSi(Dimension.Volume / Dimension.TimeDim.Pow(2), nameof(parameter));
        double t = period.RequireSi(Dimension.TimeDim, nameof(period));

        return new Quantity(Math.Cbrt(mu * t * t / (4 * Math.PI * Math.PI)), Dimension.LengthDim);
    }

    /// <summary>
    /// Скорость на эллиптической орбите по уравнению энергии: <c>v² = μ(2/r − 1/a)</c>
    /// </summary>
    /// <param name="parameter">Гравитационный параметр</param>
    /// <param name="radius">Текущее расстояние</param>
    /// <param name="semiMajorAxis">Большая полуось</param>
    public static Quantity VisViva(Quantity parameter, Quantity radius, Quantity semiMajorAxis)
    {
        (double mu, double r) = Read(parameter, radius);
        double a = semiMajorAxis.RequireSi(Dimension.LengthDim, nameof(semiMajorAxis));

        double square = mu * ((2.0 / r) - (1.0 / a));

        return new Quantity(square <= 0 ? double.NaN : Math.Sqrt(square), Dimension.Velocity);
    }

    /// <summary>
    /// Суммарное приращение скорости для перехода Гомана между круговыми орбитами
    /// </summary>
    /// <param name="parameter">Гравитационный параметр</param>
    /// <param name="fromRadius">Радиус начальной орбиты</param>
    /// <param name="toRadius">Радиус конечной орбиты</param>
    public static Quantity HohmannDeltaV(Quantity parameter, Quantity fromRadius, Quantity toRadius)
    {
        (double mu, double r1) = Read(parameter, fromRadius);
        double r2 = toRadius.RequireSi(Dimension.LengthDim, nameof(toRadius));

        double transfer = (r1 + r2) / 2.0;

        double first = Math.Sqrt(mu / r1) * (Math.Sqrt(2 * r2 / (r1 + r2)) - 1);
        double second = Math.Sqrt(mu / r2) * (1 - Math.Sqrt(2 * r1 / (r1 + r2)));

        _ = transfer;

        return new Quantity(Math.Abs(first) + Math.Abs(second), Dimension.Velocity);
    }

    /// <summary>Эксцентриситет орбиты по расстояниям в перицентре и апоцентре</summary>
    /// <param name="periapsis">Наименьшее расстояние</param>
    /// <param name="apoapsis">Наибольшее расстояние</param>
    public static double Eccentricity(Quantity periapsis, Quantity apoapsis)
    {
        double rp = periapsis.RequireSi(Dimension.LengthDim, nameof(periapsis));
        double ra = apoapsis.RequireSi(Dimension.LengthDim, nameof(apoapsis));

        return (ra - rp) / (ra + rp);
    }

    private static (double Parameter, double Radius) Read(Quantity parameter, Quantity radius)
    {
        double mu = parameter.RequireSi(Dimension.Volume / Dimension.TimeDim.Pow(2), nameof(parameter));
        double r = radius.RequireSi(Dimension.LengthDim, nameof(radius));

        if (r <= 0)
            throw new ArgumentException("Расстояние должно быть положительным", nameof(radius));

        return (mu, r);
    }
}

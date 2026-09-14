#nullable enable
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;
using AI.MathUtils.ODE;

namespace AI.Physics.Mechanics.Ballistics;

/// <summary>
/// Баллистика точечного снаряда: постоянная тяжесть и квадратичное сопротивление воздуха по скорости относительно ветра.
/// </summary>
/// <remarks>
/// <para>
/// Уравнения движения: dr/dt = v, dv/dt = −g·e_z − ρ(h)·C_x(M)·S/(2m)·|v − w|·(v − w), где w есть ветер,
/// h = высота земли + z, M = |v − w|/a(h). Шесть уравнений интегрирует адаптивный метод Дормана-Принса
/// (<see cref="DormandPrince"/>); падение на землю есть останавливающее событие z = 0 при спуске, вершина есть
/// событие v_z = 0, и оба момента находятся по плотной выдаче интегратора, а не интерполяцией между узлами.
/// </para>
/// <para>
/// Модель точечная: нет вращения снаряда, деривации, подъемной силы, эффекта Магнуса, кривизны и вращения Земли,
/// изменения тяжести с высотой. Без сопротивления траектория совпадает с замкнутой формулой
/// <see cref="Projectile"/> до ошибок округления.
/// </para>
/// </remarks>
public static class PointMassBallistics
{
    private const double Tolerance = 1e-9;

    /// <summary>Наименьшее число шагов на безвоздушное время полета: столько узлов будет хотя бы у траектории</summary>
    private const int MinPathSteps = 64;

    /// <summary>Рассчитывает полет до падения на землю</summary>
    /// <param name="launch">Выстрел</param>
    public static BallisticTrajectory Fly(BallisticLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        Validate(launch);

        double elevation = launch.ElevationDegrees * Math.PI / 180;
        double azimuth = launch.AzimuthDegrees * Math.PI / 180;
        Vector3 velocity = new Vector3(Math.Cos(elevation) * Math.Sin(azimuth), Math.Cos(elevation) * Math.Cos(azimuth), Math.Sin(elevation))
            * launch.Speed;
        var start = new Vector3(0, 0, launch.Height);

        if (launch.Height <= 0 && velocity.Z <= 0)
        {
            return new BallisticTrajectory(true, 0, 0, velocity.Length, Angle(velocity), start, velocity, 0, 0, start,
                new Vector(0.0), [start], [velocity]);
        }

        bool drag = !launch.Drag.IsNone && launch.ReferenceArea > 0;
        double areaPerMass = launch.ReferenceArea / launch.Mass;
        Func<double, double> density = launch.AirDensity ?? (_ => BallisticLaunch.SeaLevelDensity);
        Func<double, double> sound = launch.SpeedOfSound ?? (_ => BallisticLaunch.SeaLevelSpeedOfSound);

        Vector Derivative(double time, Vector y)
        {
            double ax = 0, ay = 0, az = -launch.Gravity;

            if (drag)
            {
                double altitude = launch.GroundAltitude + y[2];
                Vector3 relative = new Vector3(y[3], y[4], y[5]) - launch.Wind;
                double airspeed = relative.Length;
                double k = 0.5 * density(altitude) * launch.Drag.Coefficient(airspeed / sound(altitude)) * areaPerMass * airspeed;
                (ax, ay, az) = (-k * relative.X, -k * relative.Y, az - (k * relative.Z));
            }

            return new Vector(y[3], y[4], y[5], ax, ay, az);
        }

        // Безвоздушное время полета задает наибольший шаг: траектория не будет из трех точек
        double vacuumTime = (velocity.Z + Math.Sqrt((velocity.Z * velocity.Z) + (2 * launch.Gravity * Math.Max(launch.Height, 0)))) / launch.Gravity;
        var options = new DormandPrinceOptions
        {
            RelativeTolerance = Tolerance,
            AbsoluteTolerance = Tolerance,
            MaxStep = Math.Max(vacuumTime, 1e-3) / MinPathSteps,
        };
        OdeEvent[] events =
        [
            new OdeEvent((_, y) => y[2], isTerminal: true, direction: -1),
            new OdeEvent((_, y) => y[5], isTerminal: false, direction: -1),
        ];

        OdeSolution solution = DormandPrince.Integrate(
            Derivative,
            0,
            new Vector(start.X, start.Y, start.Z, velocity.X, velocity.Y, velocity.Z),
            launch.MaxFlightTime,
            options,
            events);

        var path = solution.States.Select(state => new Vector3(state[0], state[1], state[2])).ToList();
        var velocities = solution.States.Select(state => new Vector3(state[3], state[4], state[5])).ToList();

        // Вершина: наивысшая из точек v_z = 0 и узлов траектории (у выстрела вниз это точка выстрела)
        (double apexTime, Vector3 apex) = solution.Events
            .Where(hit => hit.EventIndex == 1)
            .Select(hit => (hit.Time, new Vector3(hit.State[0], hit.State[1], hit.State[2])))
            .Concat(path.Select((point, i) => (solution.Times[i], point)))
            .MaxBy(candidate => candidate.Item2.Z);

        bool landed = solution.StoppedByEvent;
        if (!landed)
        {
            return new BallisticTrajectory(false, double.NaN, double.PositiveInfinity, double.NaN, double.NaN,
                new Vector3(double.NaN, double.NaN, double.NaN), new Vector3(double.NaN, double.NaN, double.NaN),
                apex.Z, apexTime, apex, solution.Times, path, velocities);
        }

        Vector3 impact = path[^1] with { Z = 0 };
        path[^1] = impact;
        Vector3 landing = velocities[^1];

        return new BallisticTrajectory(
            true,
            Math.Sqrt((impact.X * impact.X) + (impact.Y * impact.Y)),
            solution.FinalTime,
            landing.Length,
            Angle(landing),
            impact,
            landing,
            apex.Z,
            apexTime,
            apex,
            solution.Times,
            path,
            velocities);
    }

    /// <summary>Угол скорости ниже горизонта, градусы</summary>
    private static double Angle(Vector3 velocity)
        => Math.Atan2(-velocity.Z, Math.Sqrt((velocity.X * velocity.X) + (velocity.Y * velocity.Y))) * 180 / Math.PI;

    private static void Validate(BallisticLaunch launch)
    {
        if (!(launch.Speed >= 0) || !double.IsFinite(launch.Speed))
            throw new ArgumentOutOfRangeException(nameof(launch), "Скорость должна быть конечной и неотрицательной");
        if (!(launch.Height >= 0))
            throw new ArgumentOutOfRangeException(nameof(launch), "Высота выстрела над землей не может быть отрицательной");
        if (!(launch.Gravity > 0))
            throw new ArgumentOutOfRangeException(nameof(launch), "Ускорение свободного падения должно быть положительным");
        if (!(launch.Mass > 0))
            throw new ArgumentOutOfRangeException(nameof(launch), "Масса должна быть положительной");
        if (!(launch.ReferenceArea >= 0))
            throw new ArgumentOutOfRangeException(nameof(launch), "Площадь миделя не может быть отрицательной");
        if (!(launch.MaxFlightTime > 0))
            throw new ArgumentOutOfRangeException(nameof(launch), "Предельное время полета должно быть положительным");
    }
}

using AI.Geometry.Primitives;
using AI.Physics.Mechanics;
using AI.Physics.Mechanics.Ballistics;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Баллистика точечного снаряда: без сопротивления совпадает с замкнутой формулой <see cref="Projectile"/>, при
/// вертикальном падении с сопротивлением совпадает с точным решением через гиперболические функции; ветер, разреженный воздух
/// и зависимость сопротивления от числа Маха меняют дальность в ожидаемую сторону.
/// </summary>
public class BallisticsTests
{
    private const double G = 9.80665;

    [Fact]
    public void PointMassBallistics_Fly_WithoutDragMatchesClosedFormProjectile()
    {
        BallisticTrajectory flight = PointMassBallistics.Fly(new BallisticLaunch(50, 30));
        TrajectoryResult exact = Projectile.Launch(Quantity.Of(50, "m/s"), 30);

        Assert.True(flight.Landed);
        Assert.Equal(1, flight.Range / exact.Range.SiValue, 1e-9);
        Assert.Equal(1, flight.ApexHeight / exact.MaxHeight.SiValue, 1e-9);
        Assert.Equal(1, flight.FlightTime / exact.FlightTime.SiValue, 1e-9);
        Assert.Equal(1, flight.ImpactSpeed / exact.ImpactSpeed.SiValue, 1e-9);
        Assert.Equal(30, flight.ImpactAngleDegrees, 1e-7);
        Assert.Equal(exact.FlightTime.SiValue / 2, flight.ApexTime, 1e-9);
        Assert.True(flight.Path.Count > 30);
    }

    [Fact]
    public void PointMassBallistics_Fly_FromHeightMatchesClosedForm()
    {
        const double speed = 40, height = 25;
        var launch = new BallisticLaunch(speed, 20) { AzimuthDegrees = 90, Height = height };

        BallisticTrajectory flight = PointMassBallistics.Fly(launch);

        double vx = speed * Math.Cos(20 * Math.PI / 180);
        double vz = speed * Math.Sin(20 * Math.PI / 180);
        double time = (vz + Math.Sqrt((vz * vz) + (2 * G * height))) / G;
        Assert.Equal(time, flight.FlightTime, 1e-9);
        Assert.Equal(vx * time, flight.ImpactPoint.X, 1e-7);
        Assert.Equal(0, flight.ImpactPoint.Y, 1e-9);
        Assert.Equal(height + (vz * vz / (2 * G)), flight.ApexHeight, 1e-8);
        Assert.Equal(Math.Sqrt((speed * speed) + (2 * G * height)), flight.ImpactSpeed, 1e-8);
    }

    [Fact]
    public void PointMassBallistics_Fly_VerticalDropReachesTerminalSpeed()
    {
        // Падение с сопротивлением: v(t) = v_т·th(g·t/v_т), путь (v_т²/g)·ln ch(g·t/v_т)
        const double mass = 0.1, area = 0.01, cd = 0.5, height = 500;
        var launch = new BallisticLaunch(0, -90) { Height = height, Mass = mass, ReferenceArea = area, Drag = DragModel.Constant(cd) };

        BallisticTrajectory flight = PointMassBallistics.Fly(launch);

        double terminal = Math.Sqrt(2 * mass * G / (BallisticLaunch.SeaLevelDensity * cd * area));
        double time = terminal / G * Math.Acosh(Math.Exp(G * height / (terminal * terminal)));
        Assert.Equal(time, flight.FlightTime, 1e-7);
        Assert.Equal(terminal * Math.Tanh(G * time / terminal), flight.ImpactSpeed, 1e-7);
        Assert.Equal(90, flight.ImpactAngleDegrees, 1e-9);
        Assert.Equal(height, flight.ApexHeight, 1e-12);
    }

    [Fact]
    public void PointMassBallistics_Fly_DragShortensRangeWindShiftsItAndDescentIsSteeper()
    {
        var ball = new BallisticLaunch(30, 30) { Mass = 0.45, ReferenceArea = 0.038, Drag = DragModel.Constant(0.25) };

        BallisticTrajectory still = PointMassBallistics.Fly(ball);
        BallisticTrajectory tail = PointMassBallistics.Fly(ball with { Wind = new Vector3(0, 8, 0) });
        BallisticTrajectory head = PointMassBallistics.Fly(ball with { Wind = new Vector3(0, -8, 0) });

        Assert.True(still.Range < PointMassBallistics.Fly(new BallisticLaunch(30, 30)).Range * 0.9);
        Assert.True(tail.Range > still.Range && still.Range > head.Range);
        Assert.True(still.ImpactAngleDegrees > 30);
        Assert.True(still.ApexTime < still.FlightTime / 2);
    }

    [Fact]
    public void PointMassBallistics_Fly_InThinMountainAirFliesFarther()
    {
        static double Exponential(double altitude) => 1.225 * Math.Exp(-altitude / 8500);
        var shot = new BallisticLaunch(300, 30) { Mass = 0.01, ReferenceArea = 5e-5, Drag = DragModel.Constant(0.3), AirDensity = Exponential };

        BallisticTrajectory sea = PointMassBallistics.Fly(shot);
        BallisticTrajectory mountain = PointMassBallistics.Fly(shot with { GroundAltitude = 3000 });

        Assert.True(mountain.Range > sea.Range * 1.05, $"у моря {sea.Range:0} м, в горах {mountain.Range:0} м");
    }

    [Fact]
    public void PointMassBallistics_Fly_MachDependentDragLiesBetweenItsBranches()
    {
        var bullet = new BallisticLaunch(700, 5) { Mass = 0.01, ReferenceArea = 5e-5 };

        double Range(DragModel drag) => PointMassBallistics.Fly(bullet with { Drag = drag }).Range;

        double subsonic = Range(DragModel.Constant(0.2));
        double supersonic = Range(DragModel.Constant(0.4));
        double stepped = Range(DragModel.FromMach(mach => mach < 1 ? 0.2 : 0.4));

        Assert.True(supersonic < stepped && stepped < subsonic, $"{supersonic:0} < {stepped:0} < {subsonic:0}");
        Assert.Equal(subsonic, Range(DragModel.FromMach(_ => 0.2)), 1e-9);

        // Скорость звука входит в число Маха: при очень быстром звуке весь полет дозвуковой
        double fastSound = PointMassBallistics.Fly(bullet with { Drag = DragModel.FromMach(mach => mach < 1 ? 0.2 : 0.4), SpeedOfSound = _ => 1e4 }).Range;
        Assert.Equal(subsonic, fastSound, 1e-9);
    }

    [Fact]
    public void PointMassBallistics_Fly_UpdraftKeepsProjectileAloft()
    {
        var feather = new BallisticLaunch(1, 45)
        {
            Mass = 1e-3,
            ReferenceArea = 1e-2,
            Drag = DragModel.Constant(1),
            Wind = new Vector3(0, 0, 10),
            MaxFlightTime = 20,
        };

        BallisticTrajectory flight = PointMassBallistics.Fly(feather);

        Assert.False(flight.Landed);
        Assert.True(double.IsNaN(flight.Range));
        Assert.True(flight.ApexHeight > 10);
    }
}

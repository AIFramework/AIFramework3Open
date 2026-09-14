using AI.Geometry.Collision;
using AI.Geometry.MassProperties;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using AI.Physics.Mechanics.RigidBodies;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Движок твердых тел против аналитических ответов и законов сохранения: падение, отскок, покой без сползания,
/// стопки, маятники на связях, гироскоп и эффект Джанибекова, трение на наклоне, быстрые тела, плавучесть,
/// сопротивление среды, качение шара, цилиндра и капсулы, сон
/// </summary>
public class RigidBodyEngineTests
{
    private const double Dt = 1e-3;
    private static readonly double G = PhysicalConstants.StandardGravity.SiValue;
    private static readonly Vector3 Up = new(0, 0, 1);

    [Fact]
    public void PhysicsWorld_Step_FreeFallMatchesAnalytic()
    {
        var world = new PhysicsWorld();
        var ball = world.Add(Sphere(0.1, 1, new Vector3(0, 0, 10)));
        world.AddForce(Forces.Gravity());

        world.Run(1, Dt);

        // Полунеявный Эйлер: скорость точна, высота отстает на g·dt·t/2
        Assert.Equal(-G, ball.Velocity.Z, 1e-9);
        Assert.Equal(10 - (G / 2), ball.Position.Z, 0.01);
    }

    [Fact]
    public void PhysicsWorld_Step_ElasticBallKeepsBounceHeight()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var ball = world.Add(new Body(new SphereShape(new Vector3(0, 0, 1.1), 0.1), 1) { Restitution = 1 });
        world.AddForce(Forces.Gravity());

        var peaks = new List<double>();
        var rising = false;
        world.Run(5, Dt, _ =>
        {
            if (rising && ball.Velocity.Z <= 0)
                peaks.Add(ball.Position.Z);
            rising = ball.Velocity.Z > 0;
            return false;
        });

        // Скорость удара берется до прибавки тяжести за подшаг, поэтому мяч не прыгает выше
        Assert.True(peaks.Count >= 3);
        Assert.InRange(peaks[^1], 1.1 * 0.97, 1.1 * 1.01);
    }

    [Fact]
    public void PhysicsWorld_Step_BoxComesToRestOnPlaneWithoutDrift()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var box = world.Add(Box(new Vector3(0.2, 0.2, 0.2), 1, new Vector3(0, 0, 0.3)));
        world.AddForce(Forces.Gravity());

        world.Run(3, Dt);

        Assert.InRange(box.Position.Z, 0.2 - 0.002, 0.2);
        Assert.True(Math.Abs(box.Position.X) < 1e-3 && Math.Abs(box.Position.Y) < 1e-3, $"сдвиг {box.Position}");
        Assert.True(Tilt(box) < 0.1, $"наклон {Tilt(box):0.###}°");
        Assert.True(box.IsSleeping);
    }

    [Fact]
    public void PhysicsWorld_Step_BoxStartingInGroundComesOutWithoutJump()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var box = world.Add(Box(new Vector3(0.2, 0.2, 0.2), 1, new Vector3(0, 0, 0.195)));
        world.AddForce(Forces.Gravity());

        var highest = 0.0;
        world.Run(1, Dt, _ =>
        {
            highest = Math.Max(highest, box.Position.Z);
            return false;
        });

        Assert.True(highest < 0.2 + 1e-3, $"подскочила до {highest:0.0000}");
    }

    [Fact]
    public void PhysicsWorld_Step_StackOfFiveStands()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var boxes = Enumerable.Range(0, 5)
            .Select(k => world.Add(Box(new Vector3(0.25, 0.25, 0.25), 1, new Vector3(0, 0, 0.25 + (0.501 * k)))))
            .ToList();
        world.AddForce(Forces.Gravity());

        world.Run(3, Dt);

        Assert.Equal(2.25, boxes[^1].Position.Z, 0.02);
        Assert.All(boxes, box => Assert.True(Math.Abs(box.Position.X) < 0.01 && Math.Abs(box.Position.Y) < 0.01, $"сдвиг {box.Position}"));
    }

    [Fact]
    public void PhysicsWorld_Step_CubeOnTurnedCubeStands()
    {
        // Ни одна вершина верхнего кубика не входит в нижний: касание держит только пятно грани
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(0.5, 0.5, 0.5), 0, Vector3.Zero));
        var top = world.Add(new Body(new BoxShape(Pose.At(new Vector3(0, 0, 0.751)), new Vector3(0.25, 0.25, 0.25)), 2)
        {
            Orientation = Quaternion.FromAxisAngle(Up, Math.PI / 4),
        });
        world.AddForce(Forces.Gravity());

        world.Run(2, Dt);

        Assert.InRange(top.Position.Z, 0.745, 0.7511);
        Assert.True(top.Velocity.Length < 0.01);
    }

    [Fact]
    public void PhysicsWorld_Step_CrossedPlanksHold()
    {
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(1, 0.1, 0.1), 0, Vector3.Zero));
        var top = world.Add(Box(new Vector3(0.1, 1, 0.1), 5, new Vector3(0, 0, 0.201)));
        world.AddForce(Forces.Gravity());

        world.Run(1, Dt);

        Assert.InRange(top.Position.Z, 0.195, 0.2011);
    }

    [Fact]
    public void PhysicsWorld_Step_FreeAsymmetricBodyKeepsEnergyAndMomentum()
    {
        var world = new PhysicsWorld();
        var body = world.Add(new Body(new BoxShape(Pose.Identity, new Vector3(0.05, 0.1, 0.2)), 1) { AngularVelocity = new Vector3(3, 4, 5) });
        var (energy, momentum) = (body.KineticEnergy, body.AngularMomentum);

        world.Run(20, Dt);

        Assert.Equal(energy, body.KineticEnergy, energy * 0.02);
        Assert.True((body.AngularMomentum - momentum).Length < momentum.Length * 0.02, $"момент {body.AngularMomentum} вместо {momentum}");
    }

    [Fact]
    public void PhysicsWorld_Step_SpinAboutIntermediateAxisAtCoarseStepStaysBounded()
    {
        // Вращение вокруг средней оси неустойчиво, но энергия от крупного шага расти не должна
        var world = new PhysicsWorld();
        var body = world.Add(new Body(new BoxShape(Pose.Identity, new Vector3(0.05, 0.1, 0.2)), 1) { AngularVelocity = new Vector3(0.01, 10, 0.01) });
        var energy = body.KineticEnergy;

        world.Run(20, 1.0 / 60);

        Assert.True(double.IsFinite(body.KineticEnergy));
        Assert.True(body.KineticEnergy < energy * 1.02, $"энергия {body.KineticEnergy / energy:P1} от начальной");
    }

    [Fact]
    public void PhysicsWorld_Step_IntermediateAxisSpinFlipsWhileMajorAxisSpinIsStable()
    {
        // Эффект Джанибекова: у коробки 0.1×0.2×0.4 наибольший момент у оси x, средний у y
        var intermediate = FreeSpin(new Vector3(0.01, 10, 0.01));
        var lowestY = double.PositiveInfinity;
        intermediate.World.Run(5, Dt, _ =>
        {
            lowestY = Math.Min(lowestY, BodySpin(intermediate.Body).Y);
            return false;
        });

        var major = FreeSpin(new Vector3(10, 0.01, 0.01));
        var worstAlignment = 1.0;
        major.World.Run(5, Dt, _ =>
        {
            var spin = BodySpin(major.Body);
            worstAlignment = Math.Min(worstAlignment, spin.X / spin.Length);
            return false;
        });

        Assert.True(lowestY < -9, $"вращение вокруг средней оси не перевернулось: наименьшая проекция {lowestY:0.##} рад/с");
        Assert.True(worstAlignment > 0.999, $"вращение вокруг большой оси ушло: косинус {worstAlignment:0.#####}");
    }

    [Fact]
    public void BallJoint_Step_SpinningDiskPrecessesAtGyroscopicRate()
    {
        // Волчок на шарнире горизонтально: скорость прецессии Ω = m·g·d / (I₃·ω₃). Волчок пущен уже прецессирующим,
        // иначе на прецессию наложилась бы нутация с размахом около 3 см
        const double mass = 1, radius = 0.1, arm = 0.2, spin = 200, time = 1.5;
        var rate = mass * G * arm / (0.5 * mass * radius * radius * spin);
        var world = new PhysicsWorld();
        var pivot = world.Add(Sphere(0.01, 0, Vector3.Zero));
        var pose = new Pose(new Vector3(arm, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 0));
        var disk = world.Add(new Body(new CylinderShape(pose, 0.02, radius), mass)
        {
            AngularVelocity = new Vector3(spin, 0, rate),
            Velocity = new Vector3(0, rate * arm, 0),
        });
        world.Connect(new BallJoint(pivot, Vector3.Zero, disk, new Vector3(0, 0, -arm)));
        world.AddForce(Forces.Gravity());
        var energy = disk.KineticEnergy;

        var lowest = 0.0;
        world.Run(time, Dt, _ =>
        {
            lowest = Math.Min(lowest, disk.Position.Z);
            return false;
        });

        Assert.Equal(rate * time, Math.Atan2(disk.Position.Y, disk.Position.X), rate * time * 0.02);

        // Связь по скоростям гасит энергию пропорционально шагу, как у маятника: при шаге 1 мс около 0.3 % в секунду,
        // и волчок медленно опускается, примерно на 2.5 см за полторы секунды
        Assert.True(lowest > -0.035, $"волчок опустился на {-lowest:0.###} м");
        Assert.InRange(disk.KineticEnergy + (mass * G * disk.Position.Z), energy * 0.99, energy * 1.001);
    }

    [Fact]
    public void BallJoint_Step_PendulumHasTextbookPeriod()
    {
        var angle = 0.1;
        var (world, bob) = Pendulum(angle);

        var period = Period(world, () => bob.Velocity.X);

        Assert.Equal(2 * Math.PI * Math.Sqrt(1 / G) * (1 + (angle * angle / 16)), period, 0.005);
    }

    [Fact]
    public void BallJoint_Step_PendulumLongRunKeepsEnergy()
    {
        var (world, bob) = Pendulum(1.0);
        var energy = Energy(bob);

        world.Run(60, Dt);

        // Проекция скорости на связь гасит около 0.1 % энергии в секунду при шаге 1 мс, пропорционально шагу; не растет
        Assert.InRange(Energy(bob), energy * 0.94, energy * 1.001);
    }

    [Fact]
    public void BallJoint_Step_TinyStepDoesNotKickJoint()
    {
        var (world, bob) = Pendulum(1.0);
        world.Run(1, Dt);
        var speed = bob.Velocity.Length;

        world.Step(1e-10);

        Assert.Equal(speed, bob.Velocity.Length, 0.01);
    }

    [Fact]
    public void HingeJoint_Step_RodWithOverlappingPivotSwingsAsPhysicalPendulum()
    {
        // Шар оси перекрывает верхний конец стержня: соединенные тела друг с другом не сталкиваются
        var world = new PhysicsWorld();
        var pivot = world.Add(Sphere(0.05, 0, new Vector3(0, 0, 2)));
        var angle = 0.1;
        var along = new Vector3(0, 1, 0);
        var rod = world.Add(new Body(new BoxShape(Pose.At(new Vector3(-0.5 * Math.Sin(angle), 0, 2 - (0.5 * Math.Cos(angle)))), new Vector3(0.02, 0.02, 0.5)), 1)
        {
            Orientation = Quaternion.FromAxisAngle(along, angle),
        });
        world.Connect(new HingeJoint(pivot, Vector3.Zero, along, rod, new Vector3(0, 0, 0.5), along));
        world.AddForce(Forces.Gravity());

        var period = Period(world, () => rod.Velocity.X);

        // Физический маятник: I = m·L²/3 у оси, T = 2π·√(I/(m·g·L/2))
        Assert.Equal(2 * Math.PI * Math.Sqrt(2 / (3 * G)) * (1 + (angle * angle / 16)), period, 0.01);
    }

    [Fact]
    public void HingeJoint_Step_MotorReachesSpeedAndLimitStopsIt()
    {
        var (world, door, hinge) = Door(limits: null);
        hinge.Motor = (2, 200);
        world.Run(0.5, Dt);
        Assert.Equal(2, door.AngularVelocity.Z, 0.05);

        var (limited, _, stopped) = Door(limits: (0, 1));
        stopped.Motor = (2, 200);
        limited.Run(2, Dt);
        Assert.InRange(stopped.Angle, 0.95, 1.03);
    }

    [Fact]
    public void FixedJoint_Step_HoldsBeamOnWall()
    {
        var world = new PhysicsWorld();
        var wall = world.Add(Box(new Vector3(0.1, 1, 1), 0, Vector3.Zero));
        var beam = world.Add(Box(new Vector3(0.5, 0.05, 0.05), 2, new Vector3(0.6, 0, 0)));
        world.Connect(new FixedJoint(wall, new Vector3(0.1, 0, 0), beam, new Vector3(-0.5, 0, 0)));
        world.AddForce(Forces.Gravity());

        world.Run(1, Dt);

        Assert.True(Tilt(beam) < 1, $"консоль повернулась на {Tilt(beam):0.##}°");
        Assert.Equal(0, beam.Position.Z, 0.01);
    }

    [Fact]
    public void DistanceJoint_Step_KeepsLengthWhileSwinging()
    {
        var world = new PhysicsWorld();
        var pivot = world.Add(Sphere(0.01, 0, new Vector3(0, 0, 2)));
        var bob = world.Add(Sphere(0.05, 1, new Vector3(1, 0, 2)));
        var rod = new DistanceJoint(pivot, Vector3.Zero, bob, Vector3.Zero, 1);
        world.Connect(rod);
        world.AddForce(Forces.Gravity());

        var (worst, lowest) = (0.0, double.PositiveInfinity);
        world.Run(2, Dt, _ =>
        {
            worst = Math.Max(worst, Math.Abs((rod.PointB - rod.PointA).Length - 1));
            lowest = Math.Min(lowest, bob.Position.Z);
            return false;
        });

        Assert.True(worst < 2e-3, $"длина ушла на {worst:0.#####} м");
        Assert.InRange(lowest, 0.99, 1.01);
    }

    [Fact]
    public void SpringJoint_Step_StiffSpringStaysStable()
    {
        var world = new PhysicsWorld();
        var anchor = world.Add(Sphere(0.05, 0, Vector3.Zero));
        var weight = world.Add(Sphere(0.05, 1, new Vector3(1.01, 0, 0)));
        world.Connect(new SpringJoint(anchor, Vector3.Zero, weight, Vector3.Zero, stiffness: 5e6, restLength: 1));

        world.Run(1, Dt);

        Assert.InRange(weight.Position.X, 0.98, 1.02);
    }

    [Fact]
    public void SpringJoint_Step_OscillatesWithHarmonicPeriod()
    {
        var world = new PhysicsWorld();
        var anchor = world.Add(Sphere(0.05, 0, Vector3.Zero));
        var weight = world.Add(Sphere(0.05, 2, new Vector3(1.5, 0, 0)));
        world.Connect(new SpringJoint(anchor, Vector3.Zero, weight, Vector3.Zero, stiffness: 50, restLength: 1));

        var period = Period(world, () => weight.Velocity.X);

        Assert.Equal(2 * Math.PI * Math.Sqrt(2.0 / 50), period, 0.02);
    }

    [Fact]
    public void PhysicsWorld_Step_BoxSlidesDownInclineAsAnalytic()
    {
        const double angle = Math.PI / 6, mu = 0.3;
        var (world, box, downhill) = Incline(angle, mu);
        var start = box.Position;

        world.Run(1, Dt);

        // s = ½·g·(sin θ − μ·cos θ)·t²
        var expected = 0.5 * G * (Math.Sin(angle) - (mu * Math.Cos(angle)));
        Assert.Equal(expected, (box.Position - start).Dot(downhill), expected * 0.03);
    }

    [Fact]
    public void PhysicsWorld_Step_BoxHoldsOnInclineWhenFrictionSuffices()
    {
        var (world, box, downhill) = Incline(Math.PI / 6, 0.8);
        var start = box.Position;

        world.Run(1, Dt);

        var slide = (box.Position - start).Dot(downhill);
        Assert.True(Math.Abs(slide) < 2e-3, $"сползла на {slide:0.#####} м при μ больше tg θ");
    }

    [Theory]
    [InlineData("sphere")]
    [InlineData("cylinder")]
    [InlineData("capsule")]
    public void PhysicsWorld_Step_SlidingBodyEndsRollingWithoutSlip(string kind)
    {
        // Трение раскручивает скользящее тело до качения: v = v₀ / (1 + I/(m·r²))
        const double r = 0.1, mass = 2, v0 = 2, half = 0.2;
        var lying = new Pose(new Vector3(0, 0, r), new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0));
        IConvexShape shape = kind switch
        {
            "sphere" => new SphereShape(lying.Position, r),
            "cylinder" => new CylinderShape(lying, half, r),
            _ => new CapsuleShape(lying, half, r),
        };
        var axial = kind switch
        {
            "sphere" => InertiaTensor.Sphere(mass, r)[2, 2],
            "cylinder" => InertiaTensor.Cylinder(mass, r, 2 * half)[2, 2],
            _ => InertiaTensor.Capsule(mass, r, 2 * half)[2, 2],
        };
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var body = world.Add(new Body(shape, mass) { Velocity = new Vector3(v0, 0, 0) });
        world.AddForce(Forces.Gravity());

        world.Run(1.5, Dt);

        var expected = v0 / (1 + (axial / (mass * r * r)));
        Assert.Equal(expected, body.Velocity.X, expected * 0.02);
        Assert.Equal(expected, body.AngularVelocity.Y * r, expected * 0.02);
        Assert.True(Math.Abs(body.Velocity.Y) < 0.01 && Math.Abs(body.Position.Y) < 0.01, $"ушло вбок: {body.Position}");
        Assert.InRange(body.Position.Z, r - 0.002, r + 0.001);
    }

    [Fact]
    public void PhysicsWorld_Step_RollingResistanceSlowsRollingBall()
    {
        double Speed(double resistance)
        {
            var world = new PhysicsWorld();
            world.Add(Body.Ground());
            var ball = world.Add(new Body(new SphereShape(new Vector3(0, 0, 0.1), 0.1), 1)
            {
                RollingResistance = resistance,
                Velocity = new Vector3(2, 0, 0),
                AngularVelocity = new Vector3(0, 20, 0),
            });
            world.AddForce(Forces.Gravity());
            world.Run(3, Dt);
            return ball.Velocity.Length;
        }

        Assert.Equal(2, Speed(0), 0.02);
        Assert.InRange(Speed(0.002), 0.5, 1.95);
    }

    [Fact]
    public void PhysicsWorld_Step_BulletAtCoarseStepDoesNotTunnelThroughSheet()
    {
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(0.0005, 1, 1), 0, new Vector3(1, 0, 0)));
        var bullet = world.Add(new Body(new SphereShape(Vector3.Zero, 0.004), 0.009) { Restitution = 0, Velocity = new Vector3(800, 0, 0) });

        world.Run(0.1, 1.0 / 60);

        // Пуля остановлена листом, а не застряла перед ним с прежней скоростью
        Assert.InRange(bullet.Position.X, 0.99, 1 - 0.0005);
        Assert.True(bullet.Velocity.Length < 1, $"скорость {bullet.Velocity.Length:0.##} м/с");
    }

    [Fact]
    public void PhysicsWorld_Step_FastBoxDoesNotTunnelThroughThinWall()
    {
        // Коробка против коробки: момент касания по консервативному продвижению на GJK
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(0.0005, 1, 1), 0, new Vector3(1, 0, 0)));
        var brick = world.Add(new Body(new BoxShape(Pose.Identity, new Vector3(0.01, 0.01, 0.01)), 0.1) { Restitution = 0, Velocity = new Vector3(1000, 0, 0) });

        world.Run(0.1, 1.0 / 60);

        Assert.InRange(brick.Position.X, 0.98, 1 - 0.0005 - 0.009);
        Assert.True(brick.Velocity.Length < 1, $"скорость {brick.Velocity.Length:0.##} м/с");
    }

    [Fact]
    public void PhysicsWorld_Step_SpinningRodDoesNotPassThroughPlate()
    {
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(0.05, 0.5, 0.5), 0, new Vector3(0.6, 0.6, 0)));
        var rod = world.Add(new Body(new BoxShape(Pose.Identity, new Vector3(1, 0.02, 0.02)), 1) { AngularVelocity = new Vector3(0, 0, 100) });

        world.Run(0.05, 1.0 / 60);

        Assert.True(rod.AngularVelocity.Z < 95, $"стержень вращается с {rod.AngularVelocity.Z:0.#} рад/с, как будто плиты нет");
    }

    [Fact]
    public void PhysicsWorld_Step_DeadBodyDoesNotBounceOffHarderOne()
    {
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(0.05, 1, 1), 0, new Vector3(1, 0, 0)));
        var lump = world.Add(new Body(new SphereShape(Vector3.Zero, 0.05), 1) { Restitution = 0, Velocity = new Vector3(20, 0, 0) });

        world.Run(0.2, Dt);

        Assert.True(lump.Velocity.X > -0.5, $"отскок {lump.Velocity.X:0.##} м/с");
    }

    [Fact]
    public void PhysicsWorld_Step_MovingPlatformMovesByItsVelocity()
    {
        var world = new PhysicsWorld();
        var platform = world.Add(new Body(new BoxShape(Pose.Identity, new Vector3(1, 1, 0.1)), 0) { Velocity = new Vector3(1, 0, 0) });

        world.Run(1, Dt);

        Assert.Equal(1, platform.Position.X, 1e-6);
    }

    [Fact]
    public void PhysicsWorld_Step_BrokenStateIsRejected()
    {
        var world = new PhysicsWorld();
        world.Add(new Body(new SphereShape(Vector3.Zero, 0.1), 1) { Velocity = new Vector3(double.NaN, 0, 0) });

        Assert.Throws<InvalidOperationException>(() => world.Step(Dt));
    }

    [Fact]
    public void Forces_Water_TiltedRaftRightsItself()
    {
        var world = new PhysicsWorld();
        var half = new Vector3(1, 1, 0.1);
        var raft = world.Add(new Body(new BoxShape(Pose.Identity, half), 500 * 8 * half.X * half.Y * half.Z)
        {
            Orientation = Quaternion.FromAxisAngle(new Vector3(1, 0, 0), 17 * Math.PI / 180),
        });
        world.AddForce(Forces.Gravity());
        world.AddForce(Forces.Water(surface: 0));

        world.Run(15, Dt);

        Assert.True(Tilt(raft) < 3, $"наклон {Tilt(raft):0.#}°");
    }

    [Fact]
    public void Forces_Water_TallColumnCapsizes()
    {
        var world = new PhysicsWorld();
        var half = new Vector3(0.1, 0.1, 1);
        var column = world.Add(new Body(new BoxShape(Pose.Identity, half), 500 * 8 * half.X * half.Y * half.Z)
        {
            Orientation = Quaternion.FromAxisAngle(new Vector3(1, 0, 0), 3 * Math.PI / 180),
        });
        world.AddForce(Forces.Gravity());
        world.AddForce(Forces.Water(surface: 0));

        world.Run(15, Dt);

        Assert.True(Tilt(column) > 45, $"наклон {Tilt(column):0.#}°");
    }

    [Fact]
    public void Forces_Drag_FallingBallReachesTerminalSpeed()
    {
        const double r = 0.1, mass = 0.5, rho = 1.2;
        var world = new PhysicsWorld();
        var ball = world.Add(Sphere(r, mass, new Vector3(0, 0, 1000)));
        world.AddForce(Forces.Gravity());
        world.AddForce(Forces.Drag(_ => rho));

        world.Run(15, 0.01);

        // m·g = ½·ρ·Cd·S·v²
        var terminal = Math.Sqrt(2 * mass * G / (rho * 0.47 * Math.PI * r * r));
        Assert.Equal(-terminal, ball.Velocity.Z, terminal * 0.005);
    }

    [Fact]
    public void Forces_Drag_WindCarriesBodyAlongAsAnalytic()
    {
        const double r = 0.1, mass = 0.05, wind = 5, time = 5;
        var world = new PhysicsWorld();
        var ball = world.Add(Sphere(r, mass, Vector3.Zero));
        world.AddForce(Forces.Drag(Forces.SeaLevelAirDensity, new Vector3(wind, 0, 0)));

        world.Run(time, 0.01);

        // dv/dt = c·(w − v)², откуда v = w − 1/(1/w + c·t)
        var c = 0.5 * Forces.SeaLevelAirDensity * 0.47 * Math.PI * r * r / mass;
        var expected = wind - (1 / ((1 / wind) + (c * time)));
        Assert.Equal(expected, ball.Velocity.X, expected * 0.01);
    }

    [Fact]
    public void PhysicsWorld_Step_RestingStackFallsAsleepAndWakesOnImpact()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var boxes = Enumerable.Range(0, 3)
            .Select(k => world.Add(Box(new Vector3(0.25, 0.25, 0.25), 1, new Vector3(0, 0, 0.25 + (0.501 * k)))))
            .ToList();
        world.AddForce(Forces.Gravity());

        world.Run(3, Dt);
        Assert.All(boxes, box => Assert.True(box.IsSleeping, $"не уснула коробка на высоте {box.Position.Z:0.###}"));

        world.Add(Sphere(0.1, 1, new Vector3(0, 0, 2.5)));
        var woke = false;
        world.Run(1, Dt, _ =>
        {
            woke |= boxes.All(box => !box.IsSleeping);
            return false;
        });
        Assert.True(woke, "удар мяча не разбудил стопку");

        world.Run(5, Dt);
        Assert.All(boxes, box => Assert.True(box.IsSleeping));
        Assert.Equal(1.25, boxes[^1].Position.Z, 0.02);
        Assert.All(boxes, box => Assert.True(Math.Abs(box.Position.X) < 0.02 && Math.Abs(box.Position.Y) < 0.02, $"сдвиг {box.Position}"));
    }

    [Fact]
    public void Body_ApplyImpulse_WakesSleepingBody()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var box = world.Add(Box(new Vector3(0.1, 0.1, 0.1), 1, new Vector3(0, 0, 0.1)));
        world.AddForce(Forces.Gravity());
        world.Run(2, Dt);
        Assert.True(box.IsSleeping);

        box.ApplyImpulse(new Vector3(1, 0, 0), box.Position);

        Assert.False(box.IsSleeping);
        world.Run(0.5, Dt);
        // Скольжение до остановки: v²/(2·μ·g)
        Assert.Equal(1 / (2 * 0.5 * G), box.Position.X, 0.02);
    }

    [Fact]
    public void PhysicsWorld_Remove_WakesBodiesThatRestedOnIt()
    {
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        var shelf = world.Add(Box(new Vector3(1, 1, 0.05), 0, new Vector3(0, 0, 1)));
        var box = world.Add(Box(new Vector3(0.1, 0.1, 0.1), 1, new Vector3(0, 0, 1.151)));
        world.AddForce(Forces.Gravity());
        world.Run(2, Dt);
        Assert.True(box.IsSleeping);

        Assert.True(world.Remove(shelf));
        world.Run(1.5, Dt);

        Assert.InRange(box.Position.Z, 0.095, 0.1011);
    }

    [Fact]
    public void Body_ConvexPointSet_MassPropertiesMatchBoxAndRestsOnGround()
    {
        var corners = new List<Vector3>();
        foreach (var x in new[] { -0.2, 0.2 })
        {
            foreach (var y in new[] { -0.2, 0.2 })
            {
                foreach (var z in new[] { -0.2, 0.2 })
                    corners.Add(new Vector3(x, y, z));
            }
        }

        var body = new Body(new ConvexPointSet(Pose.At(new Vector3(1, 2, 0.5)), corners), 2) { AngularVelocity = new Vector3(1, 0, 0) };

        Assert.True((body.Position - new Vector3(1, 2, 0.5)).Length < 1e-12, $"центр масс {body.Position}");
        Assert.Equal(0.064, body.Volume, 1e-12);
        Assert.Equal(0.5 * InertiaTensor.Box(2, new Vector3(0.2, 0.2, 0.2))[0, 0], body.KineticEnergy, 1e-12);

        body.AngularVelocity = Vector3.Zero;
        var world = new PhysicsWorld();
        world.Add(Body.Ground());
        world.Add(body);
        world.AddForce(Forces.Gravity());
        world.Run(2, Dt);

        Assert.InRange(body.Position.Z, 0.195, 0.2011);
        Assert.True(Tilt(body) < 1, $"наклон {Tilt(body):0.##}°");
    }

    [Fact]
    public void Body_Constructor_ShapeWithoutInertiaFormulaRequiresTensor()
    {
        var flat = new ConvexPointSet(Pose.Identity, [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)]);

        Assert.Throws<ArgumentException>(() => new Body(flat, 1));
        var body = new Body(flat, 1, InertiaTensor.Box(1, new Vector3(0.5, 0.5, 0.01)));
        Assert.Equal(1, body.Mass);
    }

    [Fact]
    public void PhysicsWorld_Step_ElasticCollisionMatchesClosedForm()
    {
        var world = new PhysicsWorld();
        var light = world.Add(new Body(new SphereShape(Vector3.Zero, 0.1), 1) { Restitution = 1, Velocity = new Vector3(2, 0, 0) });
        var heavy = world.Add(new Body(new SphereShape(new Vector3(0.5, 0, 0), 0.1), 3) { Restitution = 1 });

        world.Run(0.5, Dt);
        var exact = AI.Physics.Mechanics.Collisions.Collide(Quantity.Of(1, "kg"), Quantity.Of(2, "m/s"), Quantity.Of(3, "kg"), Quantity.Of(0, "m/s"), 1.0);

        Assert.Equal(exact.FirstSpeed.SiValue, light.Velocity.X, 1e-3);
        Assert.Equal(exact.SecondSpeed.SiValue, heavy.Velocity.X, 1e-3);
    }

    [Fact]
    public void Body_ApplyTorque_SpinsUpByInertia()
    {
        var world = new PhysicsWorld();
        var ball = world.Add(Sphere(0.5, 2, Vector3.Zero));
        world.AddForce(_ => ball.ApplyTorque(Up));

        world.Run(1, Dt);

        // ω = τ·t / I, у шара I = 0,4·m·r²
        Assert.Equal(1 / (0.4 * 2 * 0.25), ball.AngularVelocity.Z, 0.01);
    }

    [Fact]
    public void PhysicsWorld_Step_BoxesTouchingEdgeToEdgeDoNotPassThrough()
    {
        // Нижняя коробка стоит ребром вверх вдоль x, верхняя ребром вниз вдоль y: вершины друг в друга не входят
        var world = new PhysicsWorld();
        var half = new Vector3(0.5, 0.5, 0.5);
        world.Add(new Body(new BoxShape(Pose.Identity, half), 0) { Orientation = Quaternion.FromAxisAngle(new Vector3(1, 0, 0), Math.PI / 4) });
        var top = world.Add(new Body(new BoxShape(Pose.At(new Vector3(0, 0, Math.Sqrt(2) + 0.05)), half), 5)
        {
            Orientation = Quaternion.FromAxisAngle(new Vector3(0, 1, 0), Math.PI / 4),
        });
        world.AddForce(Forces.Gravity());

        var lowest = double.PositiveInfinity;
        world.Run(0.4, Dt, _ =>
        {
            lowest = Math.Min(lowest, top.Position.Z);
            return false;
        });

        Assert.True(lowest > Math.Sqrt(2) - 0.02, $"опустилась до {lowest:0.###} м");
    }

    [Fact]
    public void PhysicsWorld_Step_SmallBoxStandsOnBigStaticBox()
    {
        var world = new PhysicsWorld();
        world.Add(Box(new Vector3(1, 1, 0.5), 0, Vector3.Zero));
        var box = world.Add(Box(new Vector3(0.2, 0.2, 0.2), 2, new Vector3(0.3, 0, 0.7)));
        world.AddForce(Forces.Gravity());

        world.Run(2, Dt);

        Assert.InRange(box.Position.Z, 0.695, 0.7001);
        Assert.True(box.Velocity.Length < 0.01);
    }

    [Fact]
    public void Forces_Water_HalfDenseBoxFloatsHalfSubmerged()
    {
        var world = new PhysicsWorld();
        var raft = world.Add(Box(new Vector3(0.5, 0.5, 0.5), 500, new Vector3(0, 0, 0.6)));
        world.AddForce(Forces.Gravity());
        world.AddForce(Forces.Water(surface: 0));

        world.Run(15, Dt);
        var heights = new List<double>();
        world.Run(5, Dt, _ =>
        {
            heights.Add(raft.Position.Z);
            return false;
        });

        // Плотность вдвое меньше воды: равновесие с половиной высоты под водой, центр на поверхности
        Assert.Equal(0, heights.Average(), 0.01);
        Assert.True(heights.Max() - heights.Min() < 0.1);
    }

    [Fact]
    public void HingeJoint_Step_TurnsOnlyAboutItsAxis()
    {
        // Дверь висит с зазором 5 см от косяка: при повороте ее кромка не задевает его
        var world = new PhysicsWorld();
        var frame = world.Add(Box(new Vector3(0.05, 0.05, 1), 0, Vector3.Zero));
        var door = world.Add(Box(new Vector3(0.4, 0.02, 1), 20, new Vector3(0.5, 0, 0)));
        world.Connect(new HingeJoint(frame, new Vector3(0.1, 0, 0), Up, door, new Vector3(-0.4, 0, 0), Up));
        world.AddForce(_ => door.ApplyTorque(new Vector3(5, 0, 5)));

        world.Run(1, Dt);

        // Момент поперек петли гасит петля: дверь вращается только вокруг вертикали
        Assert.True(door.AngularVelocity.Z > 0.5);
        Assert.True(Math.Abs(door.AngularVelocity.X) < 0.02 && Math.Abs(door.AngularVelocity.Y) < 0.02);
    }

    private static Body Sphere(double radius, double mass, Vector3 center) => new(new SphereShape(center, radius), mass);

    private static Body Box(Vector3 half, double mass, Vector3 center) => new(new BoxShape(Pose.At(center), half), mass);

    private static (PhysicsWorld World, Body Bob) Pendulum(double angle)
    {
        var world = new PhysicsWorld();
        var pivot = world.Add(Sphere(0.01, 0, new Vector3(0, 0, 2)));
        var bob = world.Add(Sphere(0.02, 1, new Vector3(Math.Sin(angle), 0, 2 - Math.Cos(angle))));
        world.Connect(new BallJoint(pivot, Vector3.Zero, bob, pivot.Position - bob.Position));
        world.AddForce(Forces.Gravity());
        return (world, bob);
    }

    private static (PhysicsWorld World, Body Door, HingeJoint Hinge) Door((double Lower, double Upper)? limits)
    {
        var world = new PhysicsWorld();
        var frame = world.Add(Box(new Vector3(0.05, 0.05, 1), 0, Vector3.Zero));
        var door = world.Add(Box(new Vector3(0.4, 0.02, 1), 20, new Vector3(0.45, 0, 0)));
        var hinge = new HingeJoint(frame, new Vector3(0.05, 0, 0), Up, door, new Vector3(-0.4, 0, 0), Up) { Limits = limits };
        world.Connect(hinge);
        return (world, door, hinge);
    }

    /// <summary>Коробка 0.2 м на плоскости с уклоном angle вниз по оси x; трение у обоих тел mu</summary>
    private static (PhysicsWorld World, Body Box, Vector3 Downhill) Incline(double angle, double mu)
    {
        var normal = new Vector3(Math.Sin(angle), 0, Math.Cos(angle));
        var world = new PhysicsWorld();
        var ground = world.Add(Body.Ground(Plane.FromGeneral(normal.X, normal.Y, normal.Z, 0)));
        ground.Friction = mu;
        var box = world.Add(new Body(new BoxShape(Pose.At(normal * 0.1), new Vector3(0.1, 0.1, 0.1)), 1)
        {
            Orientation = Quaternion.FromAxisAngle(new Vector3(0, 1, 0), angle),
            Friction = mu,
        });
        world.AddForce(Forces.Gravity());
        return (world, box, new Vector3(Math.Cos(angle), 0, -Math.Sin(angle)));
    }

    private static (PhysicsWorld World, Body Body) FreeSpin(Vector3 angularVelocity)
    {
        var world = new PhysicsWorld();
        var body = world.Add(new Body(new BoxShape(Pose.Identity, new Vector3(0.05, 0.1, 0.2)), 1) { AngularVelocity = angularVelocity });
        return (world, body);
    }

    /// <summary>Период по двум переходам скорости через ноль сверху вниз</summary>
    private static double Period(PhysicsWorld world, Func<double> speed)
    {
        var turns = new List<double>();
        var previous = speed();
        world.Run(12, Dt, w =>
        {
            var now = speed();
            if (previous > 0 && now <= 0)
                turns.Add(w.Time);
            previous = now;
            return turns.Count >= 3;
        });
        return (turns[2] - turns[0]) / 2;
    }

    private static Vector3 BodySpin(Body body) => body.Orientation.Conjugate.Rotate(body.AngularVelocity);

    private static double Energy(Body body) => body.KineticEnergy + (body.Mass * G * body.Position.Z);

    private static double Tilt(Body body) => Math.Acos(Math.Clamp(body.Orientation.Rotate(Up).Z, -1, 1)) * 180 / Math.PI;
}

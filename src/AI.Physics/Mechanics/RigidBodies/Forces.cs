#nullable enable
using AI.Geometry.Primitives;
using AI.Physics.Fluids;
using AI.Units;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Силы мира для <see cref="PhysicsWorld.AddForce"/>: каждая перед подшагом прикладывается к бодрствующим подвижным
/// телам. Сила это делегат: новый вид силы добавляется методом здесь или лямбдой у вызывающего. Все в СИ, ось z вверх
/// </summary>
/// <example>
/// <code>
/// world.AddForce(Forces.Gravity());
/// world.AddForce(Forces.Drag(p => StandardAtmosphere.Density(new Quantity(p.Z, Dimension.LengthDim)).SiValue));
/// </code>
/// </example>
public static class Forces
{
    /// <summary>Плотность воздуха на уровне моря в стандартной атмосфере, кг/м³</summary>
    public const double SeaLevelAirDensity = 1.225;

    /// <summary>Плотность пресной воды, кг/м³</summary>
    public const double WaterDensity = 1000;

    /// <summary>Однородная тяжесть вниз по оси z со стандартным ускорением свободного падения</summary>
    public static Action<IReadOnlyList<Body>> Gravity() => Gravity(new Vector3(0, 0, -StandardGravity));

    /// <summary>Однородная тяжесть</summary>
    /// <param name="acceleration">Ускорение свободного падения, м/с²</param>
    public static Action<IReadOnlyList<Body>> Gravity(Vector3 acceleration) => bodies =>
    {
        foreach (var body in bodies)
            body.ApplyForce(acceleration * body.Mass);
    };

    /// <summary>
    /// Квадратичное сопротивление среды ½·ρ·Cd·S·v² против скорости тела относительно ветра и момент ½·ρ·Cd·R⁵·ω²
    /// против вращения. Плотность берется в центре тела: так подключается стандартная атмосфера
    /// </summary>
    /// <param name="density">Плотность среды в мировой точке, кг/м³</param>
    /// <param name="wind">Скорость ветра, м/с</param>
    public static Action<IReadOnlyList<Body>> Drag(Func<Vector3, double> density, Vector3 wind = default)
    {
        ArgumentNullException.ThrowIfNull(density);
        return bodies =>
        {
            foreach (var body in bodies)
            {
                if (!(body.DragCoefficient > 0))
                    continue;

                var rho = density(body.Position);
                var relative = body.Velocity - wind;
                body.ApplyForce(relative * (-0.5 * rho * body.DragCoefficient * body.Area * relative.Length));
                body.ApplyTorque(body.AngularVelocity * (-0.5 * rho * body.DragCoefficient * Math.Pow(body.Reach, 5) * body.AngularVelocity.Length));
            }
        };
    }

    /// <summary>Квадратичное сопротивление среды постоянной плотности</summary>
    /// <param name="density">Плотность среды, кг/м³</param>
    /// <param name="wind">Скорость ветра, м/с</param>
    public static Action<IReadOnlyList<Body>> Drag(double density = SeaLevelAirDensity, Vector3 wind = default) => Drag(_ => density, wind);

    /// <summary>
    /// Вода с горизонтальной поверхностью на высоте surface: сила Архимеда по объему под водой в центре этого объема
    /// и сопротивление воды движению и вращению по погруженной доле тела. Сила в центре погруженного объема дает
    /// момент: широкий плот она выпрямляет, высокую колонну валит. Погруженный объем считается у шара, коробки,
    /// капсулы, цилиндра и набора точек; прочие формы плавучести не имеют
    /// </summary>
    /// <param name="surface">Высота поверхности, м</param>
    /// <param name="density">Плотность жидкости, кг/м³</param>
    /// <param name="gravity">Ускорение свободного падения, м/с²; по умолчанию стандартное</param>
    public static Action<IReadOnlyList<Body>> Water(double surface, double density = WaterDensity, double? gravity = null)
    {
        var g = gravity ?? StandardGravity;
        return bodies =>
        {
            foreach (var body in bodies)
            {
                if (!(body.Volume > 0))
                    continue;

                var (submerged, centroid) = body.Immersed(surface);
                if (submerged <= 0)
                    continue;

                var lift = Hydrostatics.Buoyancy(
                    new Quantity(density, Dimension.Density),
                    new Quantity(submerged, Dimension.Volume),
                    new Quantity(g, Dimension.Acceleration)).SiValue;
                var share = submerged / body.Volume;
                var resistance = -0.5 * density * body.DragCoefficient * body.Area * share * body.Velocity.Length;
                body.ApplyForce(new Vector3(0, 0, lift), centroid);
                body.ApplyForce(body.Velocity * resistance);
                var turning = -0.5 * density * body.DragCoefficient * share * Math.Pow(body.Reach, 5) * body.AngularVelocity.Length;
                body.ApplyTorque(body.AngularVelocity * turning);
            }
        };
    }

    /// <summary>
    /// Пружина с демпфером между точками двух тел явной силой. Жесткая пружина при крупном шаге так раскачивается: для
    /// нее <see cref="SpringJoint"/>, мягкая связь, устойчивая при любой жесткости. Спящие тела пружина не будит, пока
    /// оба спят
    /// </summary>
    /// <param name="a">Первое тело</param>
    /// <param name="anchorA">Точка крепления в осях первого тела, м</param>
    /// <param name="b">Второе тело</param>
    /// <param name="anchorB">Точка крепления в осях второго тела, м</param>
    /// <param name="stiffness">Жесткость, Н/м</param>
    /// <param name="restLength">Длина в покое, м</param>
    /// <param name="damping">Демпфирование, Н·с/м</param>
    public static Action<IReadOnlyList<Body>> Spring(Body a, Vector3 anchorA, Body b, Vector3 anchorB, double stiffness, double restLength, double damping = 0)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return _ =>
        {
            if (!a.IsAwake && !b.IsAwake)
                return;

            var pointA = a.Position + a.Orientation.Rotate(anchorA);
            var pointB = b.Position + b.Orientation.Rotate(anchorB);
            var span = pointB - pointA;
            var length = span.Length;
            if (length < 1e-12)
                return;

            var direction = span / length;
            var tension = (stiffness * (length - restLength)) + (damping * (b.VelocityAt(pointB) - a.VelocityAt(pointA)).Dot(direction));
            a.ApplyForce(direction * tension, pointA);
            b.ApplyForce(direction * -tension, pointB);
        };
    }

    private static double StandardGravity => PhysicalConstants.StandardGravity.SiValue;
}

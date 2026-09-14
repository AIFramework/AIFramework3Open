#nullable enable
using AI.Geometry.Collision;
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Точка касания двух тел с накопленными за подшаг импульсами: нормальным, трения (вектор в касательной плоскости)
/// и качения. Нормаль от A к B
/// </summary>
internal sealed class ContactConstraint
{
    private readonly Vector3 _localA;
    private readonly Vector3 _localB;
    private readonly Vector3 _first;
    private readonly Vector3 _second;
    private readonly double _normalMass;
    private readonly double _firstMass;
    private readonly double _secondMass;
    private readonly double _friction;
    private readonly double _rolling;
    private double _normalImpulse;
    private Vector3 _tangentImpulse;
    private Vector3 _rollingImpulse;

    public ContactConstraint(Body a, Body b, ContactPoint point, Vector3 normal, double bounce)
    {
        (A, B, Point, Normal, Depth, Feature, Bounce) = (a, b, point.Position, normal, point.Depth, point.FeatureId, bounce);
        _localA = a.Orientation.Conjugate.Rotate(Point - a.Position);
        _localB = b.Orientation.Conjugate.Rotate(Point - b.Position);
        var helper = Math.Abs(normal.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        _first = normal.Cross(helper).Normalized;
        _second = normal.Cross(_first);
        var (ra, rb) = (Point - a.Position, Point - b.Position);
        _normalMass = PhysicsWorld.EffectiveMass(a, b, ra, rb, normal);
        _firstMass = PhysicsWorld.EffectiveMass(a, b, ra, rb, _first);
        _secondMass = PhysicsWorld.EffectiveMass(a, b, ra, rb, _second);
        _friction = Math.Sqrt(a.Friction * b.Friction);
        _rolling = Math.Max(a.RollingResistance, b.RollingResistance);
    }

    public Body A { get; }

    public Body B { get; }

    public Vector3 Point { get; }

    public Vector3 Normal { get; }

    public double Depth { get; }

    /// <summary>Номер пары элементов касания из узкой фазы; <c>null</c>, если неизвестен</summary>
    public int? Feature { get; }

    /// <summary>Скорость разлета после удара, м/с</summary>
    public double Bounce { get; }

    /// <summary>Импульсы той же точки из прошлого подшага: с ними стопка начинает подшаг почти в равновесии</summary>
    public void Inherit(ContactConstraint earlier)
    {
        _normalImpulse = earlier._normalImpulse;
        _tangentImpulse = earlier._tangentImpulse - (Normal * earlier._tangentImpulse.Dot(Normal));
        _rollingImpulse = earlier._rollingImpulse;
    }

    public void WarmStart()
    {
        var impulse = (Normal * _normalImpulse) + _tangentImpulse;
        A.Push(-impulse, Point);
        B.Push(impulse, Point);
        A.Spin(-_rollingImpulse);
        B.Spin(_rollingImpulse);
    }

    /// <summary>
    /// Нормальный импульс с накоплением (тела не притягиваются), трение по двум касательным с общим пределом μ·N
    /// и сопротивление качению с пределом плеча на нормальный импульс
    /// </summary>
    public void Resolve()
    {
        var (a, b, p, n) = (A, B, Point, Normal);

        var approach = (b.VelocityAt(p) - a.VelocityAt(p)).Dot(n);
        var total = Math.Max(_normalImpulse + ((Bounce - approach) * _normalMass), 0);
        var push = n * (total - _normalImpulse);
        _normalImpulse = total;
        a.Push(-push, p);
        b.Push(push, p);

        var relative = b.VelocityAt(p) - a.VelocityAt(p);
        var change = -((_first * (relative.Dot(_first) * _firstMass)) + (_second * (relative.Dot(_second) * _secondMass)));
        var friction = Limit(_tangentImpulse + change, _friction * _normalImpulse);
        var drag = friction - _tangentImpulse;
        _tangentImpulse = friction;
        a.Push(-drag, p);
        b.Push(drag, p);

        var spin = b.AngularVelocity - a.AngularVelocity;
        var size = spin.Length;
        if (_rolling <= 0 || size < 1e-12)
            return;

        var direction = spin / size;
        var stiffness = a.InverseInertiaTimes(direction).Dot(direction) + b.InverseInertiaTimes(direction).Dot(direction);
        if (stiffness <= 0)
            return;

        var rolling = Limit(_rollingImpulse - (spin / stiffness), _rolling * _normalImpulse);
        var resist = rolling - _rollingImpulse;
        _rollingImpulse = rolling;
        a.Spin(-resist);
        b.Spin(resist);
    }

    /// <summary>
    /// Проникновение сверх допуска по нынешнему положению точек крепления убирается сдвигом и поворотом тел. При
    /// отскоке не убирается: тело само уходит от преграды на следующем подшаге, а выталкивание добавило бы ему
    /// высоты, и упругий мяч подпрыгивал бы выше, чем падал
    /// </summary>
    public void Correct(double share, double slop, double maxCorrection)
    {
        if (Bounce > 0)
            return;

        var (a, b, n) = (A, B, Normal);
        var pointA = a.Position + a.Orientation.Rotate(_localA);
        var pointB = b.Position + b.Orientation.Rotate(_localB);
        var depth = Depth - (pointB - pointA).Dot(n);
        var error = Math.Min(share * (depth - slop), maxCorrection);
        if (error <= 0)
            return;

        var impulse = n * (error * PhysicsWorld.EffectiveMass(a, b, pointA - a.Position, pointB - b.Position, n));
        a.Displace(-impulse, pointA);
        b.Displace(impulse, pointB);
    }

    private static Vector3 Limit(Vector3 value, double size)
    {
        var length = value.Length;
        return length > size ? value * (size / length) : value;
    }
}

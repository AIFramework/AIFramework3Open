#nullable enable
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Пружина с демпфером как мягкая связь: жесткость k, Н/м, и демпфирование c, Н·с/м, входят в импульс неявно по шагу
/// (γ = 1/(dt·(c + dt·k))), поэтому пружина устойчива при любой жесткости и любом шаге. Для явной силы есть
/// <see cref="Forces.Spring"/>
/// </summary>
public sealed class SpringJoint : Joint
{
    private readonly double _stiffness;
    private readonly double _restLength;
    private readonly double _damping;
    private Vector3 _direction;
    private double _gamma;
    private double _bias;
    private double _mass;
    private double _impulse;

    /// <summary>Пружина между точками двух тел</summary>
    /// <param name="a">Первое тело</param>
    /// <param name="anchorA">Точка крепления в осях первого тела, м</param>
    /// <param name="b">Второе тело</param>
    /// <param name="anchorB">Точка крепления в осях второго тела, м</param>
    /// <param name="stiffness">Жесткость, Н/м</param>
    /// <param name="restLength">Длина в покое, м</param>
    /// <param name="damping">Демпфирование, Н·с/м</param>
    public SpringJoint(Body a, Vector3 anchorA, Body b, Vector3 anchorB, double stiffness, double restLength, double damping = 0)
        : base(a, anchorA, b, anchorB)
    {
        if (!(stiffness >= 0) || !(damping >= 0) || !(restLength >= 0))
            throw new ArgumentOutOfRangeException(nameof(stiffness), "Жесткость, демпфирование и длина пружины неотрицательны");

        (_stiffness, _restLength, _damping) = (stiffness, restLength, damping);
    }

    internal override void Begin(double dt)
    {
        var (pointA, pointB) = (PointA, PointB);
        var span = pointB - pointA;
        var length = span.Length;
        _impulse = 0;
        _mass = 0;
        if (length < 1e-12 || !(_stiffness + _damping > 0))
            return;

        _direction = span / length;
        var inverse = 1 / PhysicsWorld.EffectiveMass(A, B, pointA - A.Position, pointB - B.Position, _direction);
        _gamma = 1 / (dt * (_damping + (dt * _stiffness)));
        _bias = (length - _restLength) * dt * _stiffness * _gamma;
        _mass = double.IsFinite(inverse) ? 1 / (inverse + _gamma) : 0;
    }

    internal override void SolveVelocity(double dt)
    {
        if (_mass <= 0)
            return;

        var (pointA, pointB) = (PointA, PointB);
        var approach = (B.VelocityAt(pointB) - A.VelocityAt(pointA)).Dot(_direction);
        var impulse = -_mass * (approach + _bias + (_gamma * _impulse));
        _impulse += impulse;
        A.Push(_direction * -impulse, pointA);
        B.Push(_direction * impulse, pointB);
    }

    internal override void SolvePosition(double share)
    {
    }
}

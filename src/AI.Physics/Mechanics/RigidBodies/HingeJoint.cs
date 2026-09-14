#nullable enable
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Петля: шаровой шарнир плюс общая ось вращения, заданная в осях каждого тела. Поворот поперек оси запрещен. Угол
/// отсчитывается от взаимного положения тел при сборке; можно задать пределы угла и мотор со скоростью и наибольшим
/// моментом
/// </summary>
public sealed class HingeJoint : Joint
{
    private readonly BallJoint _pivot;
    private readonly Vector3 _axisA;
    private readonly Vector3 _axisB;
    private readonly Vector3 _referenceA;
    private readonly Vector3 _referenceB;
    private double _motorImpulse;
    private double _limitImpulse;

    /// <summary>Петля между двумя телами</summary>
    /// <param name="a">Первое тело</param>
    /// <param name="anchorA">Точка оси в осях первого тела, м</param>
    /// <param name="axisA">Направление оси в осях первого тела</param>
    /// <param name="b">Второе тело</param>
    /// <param name="anchorB">Точка оси в осях второго тела, м</param>
    /// <param name="axisB">Направление оси в осях второго тела</param>
    public HingeJoint(Body a, Vector3 anchorA, Vector3 axisA, Body b, Vector3 anchorB, Vector3 axisB)
        : base(a, anchorA, b, anchorB)
    {
        if (!(axisA.Length > 0) || !(axisB.Length > 0))
            throw new ArgumentException("Ось петли должна быть ненулевой", nameof(axisA));

        _pivot = new BallJoint(a, anchorA, b, anchorB);
        (_axisA, _axisB) = (axisA.Normalized, axisB.Normalized);
        var along = a.Orientation.Rotate(_axisA);
        var reference = along.Cross(Math.Abs(along.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0)).Normalized;
        _referenceA = a.Orientation.Conjugate.Rotate(reference);
        _referenceB = b.Orientation.Conjugate.Rotate(reference);
    }

    /// <summary>Пределы угла, рад: например, дверь от 0 до 1.6</summary>
    public (double Lower, double Upper)? Limits { get; set; }

    /// <summary>Мотор: скорость поворота B относительно A вокруг оси, рад/с, и наибольший момент, Н·м</summary>
    public (double Speed, double MaxTorque)? Motor { get; set; }

    /// <summary>Угол поворота B относительно A вокруг оси, рад, от положения при сборке</summary>
    public double Angle
    {
        get
        {
            var axis = A.Orientation.Rotate(_axisA);
            var (referenceA, referenceB) = (A.Orientation.Rotate(_referenceA), B.Orientation.Rotate(_referenceB));
            return Math.Atan2(referenceA.Cross(referenceB).Dot(axis), referenceA.Dot(referenceB));
        }
    }

    internal override void Begin(double dt) => (_motorImpulse, _limitImpulse) = (0, 0);

    internal override void SolveVelocity(double dt)
    {
        _pivot.SolveVelocity(dt);

        var axis = A.Orientation.Rotate(_axisA).Normalized;
        foreach (var direction in Across(axis))
        {
            var stiffness = Turning(direction);
            if (stiffness > 1e-12)
                Twist(direction * (-(B.AngularVelocity - A.AngularVelocity).Dot(direction) / stiffness));
        }

        var turning = Turning(axis);
        if (turning <= 1e-12)
            return;

        if (Motor is { } motor)
        {
            var spin = (B.AngularVelocity - A.AngularVelocity).Dot(axis);
            var total = Math.Clamp(_motorImpulse + ((motor.Speed - spin) / turning), -motor.MaxTorque * dt, motor.MaxTorque * dt);
            Twist(axis * (total - _motorImpulse));
            _motorImpulse = total;
        }

        if (Limits is { } limits && Angle is var angle && (angle <= limits.Lower || angle >= limits.Upper))
        {
            // Упор держит только в одну сторону: у нижнего предела не дает уменьшать угол, у верхнего увеличивать
            var spin = (B.AngularVelocity - A.AngularVelocity).Dot(axis);
            var candidate = _limitImpulse - (spin / turning);
            var total = angle <= limits.Lower ? Math.Max(candidate, 0) : Math.Min(candidate, 0);
            Twist(axis * (total - _limitImpulse));
            _limitImpulse = total;
        }
    }

    internal override void SolvePosition(double share)
    {
        _pivot.SolvePosition(share);

        var axis = A.Orientation.Rotate(_axisA).Normalized;
        var drift = axis.Cross(B.Orientation.Rotate(_axisB).Normalized);
        foreach (var direction in Across(axis))
        {
            var stiffness = Turning(direction);
            if (stiffness > 1e-12)
                Turn(direction * (-share * drift.Dot(direction) / stiffness));
        }

        if (Limits is not { } limits || Turning(axis) <= 1e-12)
            return;

        var angle = Angle;
        var error = angle < limits.Lower ? limits.Lower - angle : angle > limits.Upper ? limits.Upper - angle : 0;
        if (error != 0)
            Turn(axis * (share * error / Turning(axis)));
    }

    private static Vector3[] Across(Vector3 axis)
    {
        var first = axis.Cross(Math.Abs(axis.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0)).Normalized;
        return [first, axis.Cross(first)];
    }
}

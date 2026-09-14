#nullable enable
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Жесткое соединение: точки крепления совпадают и взаимный поворот запрещен, как у сварки или болта
/// </summary>
public sealed class FixedJoint : Joint
{
    private static readonly Vector3[] Axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];

    private readonly BallJoint _pivot;
    private readonly Quaternion _relative;

    /// <summary>Жесткое соединение двух тел в их нынешнем взаимном повороте</summary>
    /// <param name="a">Первое тело</param>
    /// <param name="anchorA">Точка крепления в осях первого тела, м</param>
    /// <param name="b">Второе тело</param>
    /// <param name="anchorB">Точка крепления в осях второго тела, м</param>
    public FixedJoint(Body a, Vector3 anchorA, Body b, Vector3 anchorB)
        : base(a, anchorA, b, anchorB)
    {
        _pivot = new BallJoint(a, anchorA, b, anchorB);
        _relative = b.Orientation * a.Orientation.Conjugate;
    }

    internal override void SolveVelocity(double dt)
    {
        _pivot.SolveVelocity(dt);
        foreach (var axis in Axes)
        {
            var stiffness = Turning(axis);
            if (stiffness > 1e-12)
                Twist(axis * (-(B.AngularVelocity - A.AngularVelocity).Dot(axis) / stiffness));
        }
    }

    internal override void SolvePosition(double share)
    {
        _pivot.SolvePosition(share);

        // Ошибка взаимного поворота как вектор поворота по кратчайшей дуге
        var angle = (B.Orientation * A.Orientation.Conjugate * _relative.Conjugate).ToRotationVector();
        foreach (var axis in Axes)
        {
            var stiffness = Turning(axis);
            if (stiffness > 1e-12)
                Turn(axis * (-share * angle.Dot(axis) / stiffness));
        }
    }
}

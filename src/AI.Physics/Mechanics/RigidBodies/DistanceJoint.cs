#nullable enable
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Стержень: расстояние между точками крепления постоянно
/// </summary>
public sealed class DistanceJoint : Joint
{
    /// <summary>Стержень заданной длины</summary>
    /// <param name="a">Первое тело</param>
    /// <param name="anchorA">Точка крепления в осях первого тела, м</param>
    /// <param name="b">Второе тело</param>
    /// <param name="anchorB">Точка крепления в осях второго тела, м</param>
    /// <param name="length">Длина, м</param>
    public DistanceJoint(Body a, Vector3 anchorA, Body b, Vector3 anchorB, double length)
        : base(a, anchorA, b, anchorB)
    {
        if (!(length >= 0) || double.IsInfinity(length))
            throw new ArgumentOutOfRangeException(nameof(length), "Длина стержня должна быть неотрицательным числом");

        Length = length;
    }

    /// <summary>Длина, м</summary>
    public double Length { get; }

    internal override void SolveVelocity(double dt)
    {
        var (pointA, pointB) = (PointA, PointB);
        var span = pointB - pointA;
        var distance = span.Length;
        if (distance < 1e-12)
            return;

        var direction = span / distance;
        var approach = (B.VelocityAt(pointB) - A.VelocityAt(pointA)).Dot(direction);
        var impulse = direction * (-approach * PhysicsWorld.EffectiveMass(A, B, pointA - A.Position, pointB - B.Position, direction));
        A.Push(-impulse, pointA);
        B.Push(impulse, pointB);
    }

    internal override void SolvePosition(double share)
    {
        var (pointA, pointB) = (PointA, PointB);
        var span = pointB - pointA;
        var distance = span.Length;
        if (distance < 1e-12)
            return;

        var direction = span / distance;
        var impulse = direction * (-(distance - Length) * share * PhysicsWorld.EffectiveMass(A, B, pointA - A.Position, pointB - B.Position, direction));
        A.Displace(-impulse, pointA);
        B.Displace(impulse, pointB);
    }
}

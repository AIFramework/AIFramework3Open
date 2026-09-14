#nullable enable
using AI.Geometry.Collision;
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Произвольная выпуклая форма, перенесенная в положение тела: опорная функция исходной формы в осях тела, начало
/// которых в точке origin. Так тело принимает любую <see cref="IConvexShape"/>, даже ту, что не умеет менять положение
/// </summary>
/// <param name="shape">Исходная форма в начальном положении</param>
/// <param name="origin">Центр масс исходной формы: начало осей тела</param>
/// <param name="pose">Положение тела</param>
internal sealed class PlacedShape(IConvexShape shape, Vector3 origin, Pose pose) : IConvexShape
{
    /// <inheritdoc/>
    public Vector3 Center => pose.Position;

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction) => pose.ToWorld(shape.Support(pose.InverseRotate(direction)) - origin);
}

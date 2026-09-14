#nullable enable
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Капсула: отрезок вдоль локальной оси Z, раздутый на радиус
/// </summary>
/// <param name="Pose">Середина отрезка и оси капсулы</param>
/// <param name="HalfLength">Половина длины осевого отрезка</param>
/// <param name="Radius">Радиус</param>
public sealed record CapsuleShape(Pose Pose, double HalfLength, double Radius) : IConvexShape
{
    /// <inheritdoc/>
    public Vector3 Center => Pose.Position;

    /// <summary>Начало осевого отрезка</summary>
    public Vector3 Start => Pose.Position - (Pose.AxisZ * HalfLength);

    /// <summary>Конец осевого отрезка</summary>
    public Vector3 End => Pose.Position + (Pose.AxisZ * HalfLength);

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction)
    {
        var end = direction.Dot(Pose.AxisZ) < 0 ? Start : End;
        double length = direction.Length;
        return length > 0 ? end + (direction * (Radius / length)) : end;
    }
}

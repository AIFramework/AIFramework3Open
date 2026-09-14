#nullable enable
using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Прямой круговой цилиндр с осью вдоль локальной оси Z
/// </summary>
/// <param name="Pose">Центр и оси цилиндра</param>
/// <param name="HalfHeight">Половина высоты</param>
/// <param name="Radius">Радиус основания</param>
public sealed record CylinderShape(Pose Pose, double HalfHeight, double Radius) : IConvexShape
{
    /// <inheritdoc/>
    public Vector3 Center => Pose.Position;

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction)
    {
        var local = Pose.InverseRotate(direction);
        double radial = Math.Sqrt((local.X * local.X) + (local.Y * local.Y));
        double scale = radial > 0 ? Radius / radial : 0;
        return Pose.ToWorld(new Vector3(local.X * scale, local.Y * scale, local.Z < 0 ? -HalfHeight : HalfHeight));
    }
}

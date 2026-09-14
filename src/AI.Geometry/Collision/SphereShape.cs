#nullable enable
using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Шар для поиска столкновений
/// </summary>
/// <param name="Center">Центр шара</param>
/// <param name="Radius">Радиус</param>
public sealed record SphereShape(Vector3 Center, double Radius) : IConvexShape
{
    /// <summary>Шар из примитива <see cref="Sphere"/></summary>
    /// <param name="sphere">Сфера с центром в трехмерном векторе</param>
    public static SphereShape FromSphere(Sphere sphere)
    {
        ArgumentNullException.ThrowIfNull(sphere);
        return new SphereShape(Vector3.FromVector(sphere.Center), sphere.Radius);
    }

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction)
    {
        double length = direction.Length;
        return length > 0 ? Center + (direction * (Radius / length)) : Center + new Vector3(Radius, 0, 0);
    }
}

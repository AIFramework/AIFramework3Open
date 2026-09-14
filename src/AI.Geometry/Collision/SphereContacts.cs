#nullable enable
using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Касания шара: с шаром и с плоскостью. Точка касания лежит посередине между поверхностями
/// </summary>
public static class SphereContacts
{
    /// <summary>Касание двух шаров: нормаль по линии центров, глубина равна сумме радиусов без расстояния</summary>
    /// <param name="a">Первый шар</param>
    /// <param name="b">Второй шар</param>
    public static ContactManifold SphereSphere(SphereShape a, SphereShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Pair(a.Center, a.Radius, b.Center, b.Radius, new Vector3(0, 0, 1), 0);
    }

    /// <summary>
    /// Касание шара с полупространством под плоскостью (со стороны, противоположной нормали плоскости).
    /// Нормаль касания от шара к плоскости, то есть против нормали плоскости
    /// </summary>
    /// <param name="sphere">Шар</param>
    /// <param name="plane">Плоскость; нормаль не обязательно единичная</param>
    public static ContactManifold SpherePlane(SphereShape sphere, Plane plane)
    {
        ArgumentNullException.ThrowIfNull(sphere);
        var (normal, offset) = CollisionDispatcher.UnitPlane(plane);
        double height = normal.Dot(sphere.Center) + offset;
        if (height >= sphere.Radius)
            return ContactManifold.Empty;

        double depth = sphere.Radius - height;
        return new ContactManifold(-normal, depth, [new ContactPoint(sphere.Center - (normal * ((sphere.Radius + height) / 2)), depth, 0)]);
    }

    /// <summary>
    /// Касание двух шаров по центрам и радиусам; при совпадающих центрах нормаль берется запасная. Общий шаг для
    /// капсул: их ближайшие точки осевых отрезков касаются как шары
    /// </summary>
    internal static ContactManifold Pair(Vector3 centerA, double radiusA, Vector3 centerB, double radiusB, Vector3 fallback, int featureId)
    {
        var span = centerB - centerA;
        double distance = span.Length;
        double depth = radiusA + radiusB - distance;
        if (depth <= 0)
            return ContactManifold.Empty;

        var normal = distance > 1e-12 * (radiusA + radiusB) ? span / distance : fallback;
        return new ContactManifold(normal, depth, [new ContactPoint(centerA + (normal * (radiusA - (depth / 2))), depth, featureId)]);
    }
}

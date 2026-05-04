using AI.Geometry.Primitives;

namespace AI.Geometry.Intersections;

/// <summary>
/// Тест пересечения двух AABB.
/// </summary>
public static class AabbAabbIntersection
{
    /// <summary>
    /// Проверяет, пересекаются ли два AABB.
    /// </summary>
    public static bool Test(Aabb a, Aabb b)
    {
        return a.Intersects(b);
    }
}

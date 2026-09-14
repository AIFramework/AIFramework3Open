#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Пятно касания двух тел: общая нормаль от A к B, глубина проникновения и не больше четырех точек
/// </summary>
public sealed class ContactManifold
{
    /// <summary>Наибольшее число точек пятна</summary>
    public const int MaxPoints = 4;

    /// <summary>
    /// Создает пятно касания. Если точек больше четырех, остаются самая глубокая, самая дальняя от нее и две,
    /// дающие наибольшую площадь по обе стороны от отрезка между ними
    /// </summary>
    /// <param name="normal">Единичная нормаль от A к B</param>
    /// <param name="depth">Глубина проникновения вдоль нормали</param>
    /// <param name="points">Точки касания</param>
    public ContactManifold(Vector3 normal, double depth, IEnumerable<ContactPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Normal = normal;
        Depth = depth;
        Points = Reduce([.. points], normal);
    }

    /// <summary>Отсутствие касания</summary>
    public static ContactManifold Empty { get; } = new(Vector3.Zero, 0, []);

    /// <summary>Единичная нормаль от A к B: сдвиг B вдоль нее на глубину разводит тела</summary>
    public Vector3 Normal { get; }

    /// <summary>Глубина проникновения вдоль нормали</summary>
    public double Depth { get; }

    /// <summary>Точки касания, не больше четырех</summary>
    public IReadOnlyList<ContactPoint> Points { get; }

    /// <summary>Есть ли касание</summary>
    public bool HasContact => Points.Count > 0;

    /// <summary>То же пятно для пары в обратном порядке: нормаль от B к A</summary>
    public ContactManifold Flipped() => HasContact ? new ContactManifold(-Normal, Depth, Points) : this;

    private static ContactPoint[] Reduce(ContactPoint[] points, Vector3 normal)
    {
        if (points.Length <= MaxPoints)
            return points;

        var deepest = points.MaxBy(point => point.Depth);
        var farthest = points.MaxBy(point => (point.Position - deepest.Position).LengthSquared);
        double Area(ContactPoint point) => (farthest.Position - deepest.Position).Cross(point.Position - deepest.Position).Dot(normal);
        return new[] { deepest, farthest, points.MaxBy(Area), points.MinBy(Area) }.Distinct().ToArray();
    }
}

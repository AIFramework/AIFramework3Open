#nullable enable
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Опорная точка разности Минковского A − B вместе с опорными точками самих форм, из которых она получена
/// </summary>
/// <param name="W">Точка разности A − B</param>
/// <param name="A">Опорная точка формы A</param>
/// <param name="B">Опорная точка формы B</param>
internal readonly record struct SupportPoint(Vector3 W, Vector3 A, Vector3 B)
{
    /// <summary>Опорная точка разности в направлении direction; форма B сдвинута на offsetB</summary>
    public static SupportPoint Of(IConvexShape a, IConvexShape b, Vector3 offsetB, Vector3 direction)
    {
        var onA = a.Support(direction);
        var onB = b.Support(-direction) + offsetB;
        return new SupportPoint(onA - onB, onA, onB);
    }

    /// <summary>Взвешенная сумма точек симплекса: точки на A, на B и на разности</summary>
    public static (Vector3 W, Vector3 A, Vector3 B) Combine(SupportPoint[] points, double[] weights)
    {
        var (w, a, b) = (Vector3.Zero, Vector3.Zero, Vector3.Zero);
        for (int i = 0; i < points.Length; i++)
        {
            w += points[i].W * weights[i];
            a += points[i].A * weights[i];
            b += points[i].B * weights[i];
        }

        return (w, a, b);
    }
}

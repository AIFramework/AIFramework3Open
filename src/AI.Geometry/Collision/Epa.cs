#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Алгоритм расширяющегося многогранника: глубина и нормаль проникновения пересекающихся выпуклых форм. Многогранник
/// внутри разности Минковского A − B, содержащий начало координат, раздувается к ее границе: грань, ближайшая к началу,
/// выталкивается опорной точкой по своей нормали, пока граница не перестанет отодвигаться. Нормаль ближайшей грани
/// это нормаль касания от A к B, расстояние до нее это глубина
/// </summary>
public static class Epa
{
    private const int MaxIterations = 128;

    /// <summary>Допуск сходимости в долях размера разности Минковского</summary>
    private const double Tolerance = 1e-10;

    /// <summary>
    /// Пятно касания пересекающихся форм с одной точкой: середина между точками на A и на B, найденными по весам
    /// ближайшей грани. Для разделенных форм пятно пустое
    /// </summary>
    /// <param name="a">Первая форма</param>
    /// <param name="b">Вторая форма</param>
    public static ContactManifold Penetration(IConvexShape a, IConvexShape b)
    {
        var gjk = Gjk.Run(a, b, Vector3.Zero);
        return gjk.Intersecting ? Expand(a, b, gjk.Simplex) : ContactManifold.Empty;
    }

    private static ContactManifold Expand(IConvexShape a, IConvexShape b, SupportPoint[] simplex)
    {
        var vertices = new List<SupportPoint>(simplex);
        double scale = Math.Max(vertices.Max(point => point.W.Length), (a.Center - b.Center).Length);
        scale = Math.Max(scale, 1e-300);
        double tolerance = Tolerance * scale;
        if (!BlowUp(a, b, vertices, tolerance))
        {
            var (_, onA, onB) = SupportPoint.Combine(simplex, [.. simplex.Select(_ => 1.0 / simplex.Length)]);
            return new ContactManifold((b.Center - a.Center).Normalized, 0, [new ContactPoint((onA + onB) / 2, 0)]);
        }

        var interior = (vertices[0].W + vertices[1].W + vertices[2].W + vertices[3].W) / 4;
        var faces = new List<(int I, int J, int K, Vector3 Normal, double Distance)>
        {
            Face(vertices, interior, 0, 1, 2), Face(vertices, interior, 0, 3, 1), Face(vertices, interior, 0, 2, 3), Face(vertices, interior, 1, 3, 2),
        };

        var best = faces.MinBy(face => face.Distance);
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            best = faces.MinBy(face => face.Distance);
            var next = SupportPoint.Of(a, b, Vector3.Zero, best.Normal);
            if (next.W.Dot(best.Normal) - best.Distance <= tolerance || vertices.Any(point => point.W == next.W))
                break;

            var visible = faces.Where(face => face.Normal.Dot(next.W - vertices[face.I].W) > tolerance * 1e-2).ToList();
            if (visible.Count == 0)
                break;

            // Горизонт: ребра, принадлежащие ровно одной видимой грани
            var horizon = new List<(int, int)>();
            foreach (var face in visible)
            {
                foreach (var edge in new[] { (face.I, face.J), (face.J, face.K), (face.K, face.I) })
                {
                    var key = (Math.Min(edge.Item1, edge.Item2), Math.Max(edge.Item1, edge.Item2));
                    if (!horizon.Remove(key))
                        horizon.Add(key);
                }
            }

            faces.RemoveAll(visible.Contains);
            vertices.Add(next);
            faces.AddRange(horizon.Select(edge => Face(vertices, interior, edge.Item1, edge.Item2, vertices.Count - 1)));
        }

        var (u, v, w) = Gjk.TriangleWeights(best.Normal * best.Distance, vertices[best.I].W, vertices[best.J].W, vertices[best.K].W);
        var (_, pointA, pointB) = SupportPoint.Combine([vertices[best.I], vertices[best.J], vertices[best.K]], [u, v, w]);
        double depth = Math.Max(best.Distance, 0);
        return new ContactManifold(best.Normal, depth, [new ContactPoint((pointA + pointB) / 2, depth)]);
    }

    /// <summary>
    /// Достраивает симплекс GJK до тетраэдра: начало лежит на вершине, ребре или грани симплекса, поэтому остается
    /// внутри тетраэдра или на его границе. Ложь, если разность Минковского плоская
    /// </summary>
    private static bool BlowUp(IConvexShape a, IConvexShape b, List<SupportPoint> vertices, double tolerance)
    {
        if (vertices.Count == 1)
        {
            Vector3[] axes = [new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)];
            TryAdd(axes, point => (point.W - vertices[0].W).Length);
        }

        if (vertices.Count == 2)
        {
            var line = (vertices[1].W - vertices[0].W).Normalized;
            var across = line.Cross(Math.Abs(line.X) < 0.57 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0)).Normalized;
            var third = line.Cross(across);
            TryAdd([across, -across, third, -third], point => (point.W - vertices[0].W).Cross(line).Length);
        }

        if (vertices.Count == 3)
        {
            var normal = (vertices[1].W - vertices[0].W).Cross(vertices[2].W - vertices[0].W).Normalized;
            TryAdd([normal, -normal], point => Math.Abs((point.W - vertices[0].W).Dot(normal)));
        }

        return vertices.Count == 4;

        void TryAdd(Vector3[] directions, Func<SupportPoint, double> offset)
        {
            foreach (var direction in directions)
            {
                var point = SupportPoint.Of(a, b, Vector3.Zero, direction);
                if (offset(point) > tolerance)
                {
                    vertices.Add(point);
                    return;
                }
            }
        }
    }

    /// <summary>Грань с нормалью наружу от внутренней точки; у вырожденной грани расстояние бесконечно</summary>
    private static (int, int, int, Vector3, double) Face(List<SupportPoint> vertices, Vector3 interior, int i, int j, int k)
    {
        var (a, b, c) = (vertices[i].W, vertices[j].W, vertices[k].W);
        var normal = (b - a).Cross(c - a);
        double length = normal.Length;
        if (length <= 1e-300)
            return (i, j, k, Vector3.Zero, double.PositiveInfinity);

        normal /= length;
        if (normal.Dot(a - interior) < 0)
            (normal, j, k) = (-normal, k, j);

        return (i, j, k, normal, normal.Dot(a));
    }
}

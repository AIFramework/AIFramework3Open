using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Ближайшая точка тетраэдра к заданной точке.
/// </summary>
public static class PointTetrahedron
{
    /// <summary>
    /// Ближайшая точка на поверхности или внутри тетраэдра к заданной точке.
    /// </summary>
    public static Vector ClosestPoint(Vector p, Tetrahedron tet)
    {
        var faces = new[]
        {
            new Triangle(tet.A, tet.B, tet.C),
            new Triangle(tet.A, tet.B, tet.D),
            new Triangle(tet.A, tet.C, tet.D),
            new Triangle(tet.B, tet.C, tet.D)
        };

        // Проверяем, внутри ли тетраэдра
        if (IsInside(p, tet))
            return p;

        Vector bestPoint = null;
        double bestDist = double.MaxValue;

        foreach (var face in faces)
        {
            var cp = PointTriangle.ClosestPoint(p, face);
            var diff = p - cp;
            double dist = Vector.Dot(diff, diff);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestPoint = cp;
            }
        }

        return bestPoint;
    }

    private static bool IsInside(Vector p, Tetrahedron tet)
    {
        double d0 = SignedVolume6(tet.A, tet.B, tet.C, tet.D);
        double d1 = SignedVolume6(p, tet.B, tet.C, tet.D);
        double d2 = SignedVolume6(tet.A, p, tet.C, tet.D);
        double d3 = SignedVolume6(tet.A, tet.B, p, tet.D);
        double d4 = SignedVolume6(tet.A, tet.B, tet.C, p);

        bool allPos = d1 >= 0 && d2 >= 0 && d3 >= 0 && d4 >= 0;
        bool allNeg = d1 <= 0 && d2 <= 0 && d3 <= 0 && d4 <= 0;

        return (d0 > 0) ? allPos : allNeg;
    }

    private static double SignedVolume6(Vector a, Vector b, Vector c, Vector d)
    {
        var ab = b - a;
        var ac = c - a;
        var ad = d - a;
        return Vector.Dot(Vector.Cross(ab, ac), ad);
    }
}

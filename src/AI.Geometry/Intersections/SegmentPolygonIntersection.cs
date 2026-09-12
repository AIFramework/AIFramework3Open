#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Numerics;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Части отрезка внутри многоугольника (2D): сколько пути приходится на лес, озеро, застройку.
/// </summary>
/// <remarks>
/// Отрезок режется точками пересечения со всеми сторонами, и каждый кусок проверяется серединой
/// на принадлежность многоугольнику. Так обрабатываются и невыпуклые многоугольники, в которые путь
/// входит и выходит несколько раз. Участок, идущий точно по стороне, неоднозначен: отнесение его
/// внутрь или наружу зависит от округления.
/// </remarks>
public static class SegmentPolygonIntersection
{
    /// <summary>
    /// Куски отрезка внутри многоугольника в порядке от A к B; соприкасающиеся куски сливаются.
    /// </summary>
    public static IReadOnlyList<Segment> Inside(Segment segment, Vector[] polygon)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.Length < 3)
            throw new ArgumentException("У многоугольника меньше трёх вершин", nameof(polygon));

        if (Vector.Dot(segment.Direction, segment.Direction) == 0)
            return Array.Empty<Segment>();

        var cuts = new List<double> { 0.0, 1.0 };
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var hit = SegmentSegmentIntersection.Intersect(segment, new Segment(polygon[j], polygon[i]));
            if (hit is not null)
                cuts.Add(Parameter(segment, hit));
        }

        cuts.Sort();

        var pieces = new List<Segment>();
        double? start = null;
        double end = 0;

        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            double from = cuts[k];
            double to = cuts[k + 1];
            if (to - from <= Eps.Default)
                continue;

            if (PointInPolygon.Contains(segment.PointAt((from + to) / 2), polygon))
            {
                start ??= from;
                end = to;
            }
            else if (start is not null)
            {
                pieces.Add(new Segment(segment.PointAt(start.Value), segment.PointAt(end)));
                start = null;
            }
        }

        if (start is not null)
            pieces.Add(new Segment(segment.PointAt(start.Value), segment.PointAt(end)));

        return pieces;
    }

    /// <summary>
    /// Длина части отрезка внутри многоугольника.
    /// </summary>
    public static double LengthInside(Segment segment, Vector[] polygon)
    {
        return Inside(segment, polygon).Sum(piece => piece.Length);
    }

    /// <summary>Параметр t точки на отрезке: A + t·(B − A)</summary>
    private static double Parameter(Segment segment, Vector point)
    {
        var direction = segment.Direction;
        return Math.Clamp(Vector.Dot(point - segment.A, direction) / Vector.Dot(direction, direction), 0.0, 1.0);
    }
}

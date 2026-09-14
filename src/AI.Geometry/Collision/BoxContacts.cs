#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Distances;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Касания коробки: с плоскостью, шаром и другой коробкой. Коробки проверяются по 15 разделяющим осям (три грани каждой
/// и девять произведений ребер): ось наименьшего перекрытия дает нормаль и глубину. При касании гранью грань другой
/// коробки, обращенная навстречу, обрезается боковыми плоскостями опорной грани (алгоритм Сазерленда и Ходжмана), и точки ниже
/// опорной грани дают пятно до четырех точек. Ребро о ребро касается в середине между ближайшими точками ребер
/// </summary>
public static class BoxContacts
{
    /// <summary>Уступка оси ребер: при почти равном перекрытии касание гранью устойчивее</summary>
    private const double EdgePreference = 1.05;

    /// <summary>Номера признаков ребро о ребро идут после всех номеров касания гранью</summary>
    private const int EdgeFeatureBase = 12 * 6 * 64;

    /// <summary>
    /// Касание коробки с полупространством под плоскостью: каждая вершина ниже плоскости дает точку, номер признака
    /// равен номеру вершины. Нормаль от коробки к плоскости
    /// </summary>
    /// <param name="box">Коробка</param>
    /// <param name="plane">Плоскость; нормаль не обязательно единичная</param>
    public static ContactManifold BoxPlane(BoxShape box, Plane plane)
    {
        ArgumentNullException.ThrowIfNull(box);
        var (normal, offset) = CollisionDispatcher.UnitPlane(plane);
        var points = new List<ContactPoint>();
        for (int i = 0; i < 8; i++)
        {
            var corner = box.Corner(i);
            double height = normal.Dot(corner) + offset;
            if (height < 0)
                points.Add(new ContactPoint(corner - (normal * (height / 2)), -height, i));
        }

        return points.Count == 0 ? ContactManifold.Empty : new ContactManifold(-normal, points.Max(point => point.Depth), points);
    }

    /// <summary>
    /// Касание коробки с шаром по ближайшей к центру шара точке коробки. Если центр внутри коробки, шар выталкивается
    /// через ближайшую грань. Нормаль от коробки к шару
    /// </summary>
    /// <param name="box">Коробка</param>
    /// <param name="sphere">Шар</param>
    public static ContactManifold BoxSphere(BoxShape box, SphereShape sphere)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(sphere);
        var closest = box.ClosestPoint(sphere.Center);
        var span = sphere.Center - closest;
        double distance = span.Length;
        if (distance >= sphere.Radius)
            return ContactManifold.Empty;

        if (distance > 1e-12 * (sphere.Radius + box.HalfExtents.Length))
        {
            var normal = span / distance;
            double depth = sphere.Radius - distance;
            return new ContactManifold(normal, depth, [new ContactPoint(closest - (normal * (depth / 2)), depth, 0)]);
        }

        // Центр шара внутри коробки: выталкивание через ближайшую грань
        var local = box.Pose.ToLocal(sphere.Center);
        var half = box.HalfExtents;
        double[] gaps = [half.X - Math.Abs(local.X), half.Y - Math.Abs(local.Y), half.Z - Math.Abs(local.Z)];
        int nearest = Array.IndexOf(gaps, gaps.Min());
        var face = box.Pose.Axis(nearest) * Sign(local[nearest]);
        double inside = gaps[nearest] + sphere.Radius;
        return new ContactManifold(face, inside, [new ContactPoint(sphere.Center + (face * ((gaps[nearest] - sphere.Radius) / 2)), inside, 0)]);
    }

    /// <summary>Касание двух коробок по 15 разделяющим осям с пятном до четырех точек. Нормаль от A к B</summary>
    /// <param name="a">Первая коробка</param>
    /// <param name="b">Вторая коробка</param>
    public static ContactManifold BoxBox(BoxShape a, BoxShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        Vector3[] axesA = [a.Pose.AxisX, a.Pose.AxisY, a.Pose.AxisZ];
        Vector3[] axesB = [b.Pose.AxisX, b.Pose.AxisY, b.Pose.AxisZ];
        double[] halfA = [a.HalfExtents.X, a.HalfExtents.Y, a.HalfExtents.Z];
        double[] halfB = [b.HalfExtents.X, b.HalfExtents.Y, b.HalfExtents.Z];
        if (Separation(a.Center, axesA, halfA, b.Center, axesB, halfB) is not { } found)
            return ContactManifold.Empty;

        var (depth, normal, i, j) = found;
        if (j >= 0)
            return EdgeContact(a.Center, axesA, halfA, i, b.Center, axesB, halfB, j, normal, depth);

        return i < 3
            ? Face(a.Center, axesA, halfA, i, b.Center, axesB, halfB, normal, depth, true)
            : Face(b.Center, axesB, halfB, i - 3, a.Center, axesA, halfA, -normal, depth, false);
    }

    /// <summary>
    /// Пятно касания гранью: грань другой коробки, обращенная навстречу опорной, обрезается четырьмя боковыми
    /// плоскостями опорной грани; точки ниже опорной грани дают касания посередине проникновения. outward это
    /// наружная нормаль опорной коробки к другой
    /// </summary>
    private static ContactManifold Face(Vector3 reference, Vector3[] referenceAxes, double[] referenceHalf, int k,
        Vector3 incident, Vector3[] incidentAxes, double[] incidentHalf, Vector3 outward, double depth, bool referenceIsA)
    {
        int faceSign = Sign(referenceAxes[k].Dot(outward));
        var faceAxis = referenceAxes[k] * faceSign;
        var faceCenter = reference + (faceAxis * referenceHalf[k]);

        // Грань другой коробки, смотрящая навстречу опорной
        int m = Enumerable.Range(0, 3).MaxBy(axis => Math.Abs(incidentAxes[axis].Dot(faceAxis)));
        int incidentSign = Sign(incidentAxes[m].Dot(faceAxis));
        var incidentCenter = incident - (incidentAxes[m] * (incidentHalf[m] * incidentSign));
        var (p, q) = ((m + 1) % 3, (m + 2) % 3);
        var (sideP, sideQ) = (incidentAxes[p] * incidentHalf[p], incidentAxes[q] * incidentHalf[q]);
        var polygon = new List<(Vector3 Point, int Id)>
        {
            (incidentCenter + sideP + sideQ, 0), (incidentCenter - sideP + sideQ, 1), (incidentCenter - sideP - sideQ, 2), (incidentCenter + sideP - sideQ, 3),
        };

        int plane = 0;
        foreach (int side in new[] { (k + 1) % 3, (k + 2) % 3 })
        {
            double offset = reference.Dot(referenceAxes[side]);
            polygon = Clip(polygon, referenceAxes[side], offset + referenceHalf[side], plane++);
            polygon = Clip(polygon, -referenceAxes[side], -offset + referenceHalf[side], plane++);
        }

        int referenceFace = ((referenceIsA ? k : k + 3) * 2) + (faceSign > 0 ? 1 : 0);
        int feature = ((referenceFace * 6) + (m * 2) + (incidentSign > 0 ? 1 : 0)) * 64;
        var points = polygon
            .Select(vertex => (vertex.Point, vertex.Id, Depth: (faceCenter - vertex.Point).Dot(faceAxis)))
            .Where(vertex => vertex.Depth > 0)
            .Select(vertex => new ContactPoint(vertex.Point + (faceAxis * (vertex.Depth / 2)), vertex.Depth, feature + vertex.Id));
        return new ContactManifold(referenceIsA ? faceAxis : -faceAxis, depth, points);
    }

    /// <summary>
    /// Отсечение многоугольника полупространством normal·x ≤ offset (шаг алгоритма Сазерленда и Ходжмана). Новая точка
    /// получает номер по плоскости отсечения и исходному ребру: остаток от деления номера на 4 сохраняет ребро
    /// </summary>
    private static List<(Vector3 Point, int Id)> Clip(List<(Vector3 Point, int Id)> polygon, Vector3 normal, double offset, int plane)
    {
        var kept = new List<(Vector3, int)>();
        for (int k = 0; k < polygon.Count; k++)
        {
            var (from, to) = (polygon[k], polygon[(k + 1) % polygon.Count]);
            double before = from.Point.Dot(normal) - offset, after = to.Point.Dot(normal) - offset;
            if (before <= 0)
                kept.Add(from);
            if (before * after < 0)
                kept.Add((from.Point + ((to.Point - from.Point) * (before / (before - after))), 4 + (4 * plane) + (from.Id % 4)));
        }

        return kept;
    }

    /// <summary>
    /// Ось наименьшего перекрытия по 15 разделяющим осям; <c>null</c>, если коробки разделены. Номер i от 0 до 2 это
    /// грань A, от 3 до 5 грань B; j не меньше нуля у оси из произведения ребра i коробки A и ребра j коробки B
    /// </summary>
    private static (double Depth, Vector3 Normal, int I, int J)? Separation(
        Vector3 centerA, Vector3[] axesA, double[] halfA, Vector3 centerB, Vector3[] axesB, double[] halfB)
    {
        var candidates = new List<(Vector3 Axis, int I, int J)>();
        candidates.AddRange(axesA.Select((axis, i) => (axis, i, -1)));
        candidates.AddRange(axesB.Select((axis, i) => (axis, i + 3, -1)));
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                var cross = axesA[i].Cross(axesB[j]);
                if (cross.LengthSquared > 1e-6)
                    candidates.Add((cross.Normalized, i, j));
            }
        }

        var span = centerB - centerA;
        (double Depth, Vector3 Normal, int I, int J)? best = null;
        double bestScore = double.PositiveInfinity;
        foreach (var (axis, i, j) in candidates)
        {
            double distance = span.Dot(axis);
            double depth = Reach(axesA, halfA, axis) + Reach(axesB, halfB, axis) - Math.Abs(distance);
            if (depth <= 0)
                return null;

            double score = j >= 0 ? depth * EdgePreference : depth;
            if (score < bestScore)
                (best, bestScore) = ((depth, distance < 0 ? -axis : axis, i, j), score);
        }

        return best;
    }

    /// <summary>
    /// Касание ребром о ребро: ребро A вдоль оси i, ближайшее к B, и ребро B вдоль оси j, ближайшее к A;
    /// точка касания посередине между ближайшими точками ребер
    /// </summary>
    private static ContactManifold EdgeContact(Vector3 centerA, Vector3[] axesA, double[] halfA, int i,
        Vector3 centerB, Vector3[] axesB, double[] halfB, int j, Vector3 normal, double depth)
    {
        var (edgeA, edgeB) = (centerA, centerB);
        for (int k = 0; k < 3; k++)
        {
            if (k != i)
                edgeA += axesA[k] * (halfA[k] * Sign(axesA[k].Dot(normal)));
            if (k != j)
                edgeB -= axesB[k] * (halfB[k] * Sign(axesB[k].Dot(normal)));
        }

        var (onA, onB) = LineLine.ClosestPoints(new Line3D(edgeA.ToVector(), axesA[i].ToVector()), new Line3D(edgeB.ToVector(), axesB[j].ToVector()));
        var pointA = Clamp(Vector3.FromVector(onA), edgeA, axesA[i], halfA[i]);
        var pointB = Clamp(Vector3.FromVector(onB), edgeB, axesB[j], halfB[j]);
        return new ContactManifold(normal, depth, [new ContactPoint((pointA + pointB) / 2, depth, EdgeFeatureBase + (i * 3) + j)]);
    }

    /// <summary>Полупроекция коробки на ось</summary>
    private static double Reach(Vector3[] axes, double[] half, Vector3 direction) =>
        (half[0] * Math.Abs(axes[0].Dot(direction))) + (half[1] * Math.Abs(axes[1].Dot(direction))) + (half[2] * Math.Abs(axes[2].Dot(direction)));

    private static Vector3 Clamp(Vector3 point, Vector3 middle, Vector3 axis, double half) =>
        middle + (axis * Math.Clamp((point - middle).Dot(axis), -half, half));

    private static int Sign(double value) => value < 0 ? -1 : 1;
}

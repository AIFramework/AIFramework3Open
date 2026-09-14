#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Касания капсулы. Капсула это осевой отрезок, раздутый на радиус, поэтому касание сводится к ближайшим точкам
/// осевого отрезка и другой формы: дальше они касаются как шары. Параллельные капсулы и капсула, лежащая на грани
/// коробки, дают две точки по краям общего участка
/// </summary>
public static class CapsuleContacts
{
    /// <summary>Допуск параллельности: квадрат синуса угла между осями</summary>
    private const double ParallelTolerance = 1e-6;

    /// <summary>Касание капсулы с шаром. Нормаль от капсулы к шару</summary>
    /// <param name="capsule">Капсула</param>
    /// <param name="sphere">Шар</param>
    public static ContactManifold CapsuleSphere(CapsuleShape capsule, SphereShape sphere)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        ArgumentNullException.ThrowIfNull(sphere);
        var closest = ClosestOnSegment(capsule.Start, capsule.End, sphere.Center);
        return SphereContacts.Pair(closest, capsule.Radius, sphere.Center, sphere.Radius, Perpendicular(capsule.Pose.AxisZ), 0);
    }

    /// <summary>Касание двух капсул. Нормаль от A к B</summary>
    /// <param name="a">Первая капсула</param>
    /// <param name="b">Вторая капсула</param>
    public static ContactManifold CapsuleCapsule(CapsuleShape a, CapsuleShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var (directionA, directionB) = (a.End - a.Start, b.End - b.Start);
        var cross = directionA.Cross(directionB);
        var fallback = cross.LengthSquared > 0 ? cross.Normalized : Perpendicular(a.Pose.AxisZ);
        var (s, t) = SegmentSegment(a.Start, a.End, b.Start, b.End);
        var single = SphereContacts.Pair(a.Start + (directionA * s), a.Radius, b.Start + (directionB * t), b.Radius, fallback, 0);
        double lengthA = directionA.LengthSquared, lengthB = directionB.LengthSquared;
        if (!single.HasContact || lengthA == 0 || lengthB == 0 || cross.LengthSquared > ParallelTolerance * lengthA * lengthB)
            return single;

        // Параллельные оси: концы общего участка
        double t0 = (b.Start - a.Start).Dot(directionA) / lengthA, t1 = (b.End - a.Start).Dot(directionA) / lengthA;
        double low = Math.Max(0, Math.Min(t0, t1)), high = Math.Min(1, Math.Max(t0, t1));
        if (high - low <= 1e-9)
            return single;

        var points = new List<ContactPoint>();
        foreach (var (along, id) in new[] { (low, 1), (high, 2) })
        {
            var onA = a.Start + (directionA * along);
            var pair = SphereContacts.Pair(onA, a.Radius, ClosestOnSegment(b.Start, b.End, onA), b.Radius, single.Normal, id);
            if (pair.HasContact)
                points.Add(pair.Points[0]);
        }

        return points.Count == 0 ? single : new ContactManifold(single.Normal, single.Depth, points);
    }

    /// <summary>
    /// Касание капсулы с коробкой: ближайшие точки осевого отрезка и коробки ищутся GJK, для коробки он сходится за
    /// конечное число шагов. Если ближайшая точка на грани коробки, осевой отрезок обрезается боковыми плоскостями
    /// грани, и концы обрезка ниже поверхности капсулы дают до двух точек. Если осевой отрезок входит в коробку,
    /// глубину дает EPA. Нормаль от капсулы к коробке
    /// </summary>
    /// <param name="capsule">Капсула</param>
    /// <param name="box">Коробка</param>
    public static ContactManifold CapsuleBox(CapsuleShape capsule, BoxShape box)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        ArgumentNullException.ThrowIfNull(box);
        double radius = capsule.Radius;
        var (distance, onA, onB) = Gjk.Distance(capsule with { Radius = 0 }, box);
        if (distance >= radius)
            return ContactManifold.Empty;
        if (distance <= 1e-12 * (radius + box.HalfExtents.Length))
            return Epa.Penetration(capsule, box);

        var normal = (onB - onA) / distance;
        double depth = radius - distance;
        var single = new ContactManifold(normal, depth, [new ContactPoint((onA + (normal * radius) + onB) / 2, depth, 0)]);

        // Грань коробки, обращенная к капсуле
        int k = 0;
        for (int axis = 1; axis < 3; axis++)
        {
            if (Math.Abs(box.Pose.Axis(axis).Dot(normal)) > Math.Abs(box.Pose.Axis(k).Dot(normal)))
                k = axis;
        }

        var faceOut = box.Pose.Axis(k) * (box.Pose.Axis(k).Dot(normal) > 0 ? -1 : 1);
        if (faceOut.Dot(normal) > -1 + 1e-9)
            return single;

        var direction = capsule.End - capsule.Start;
        double low = 0, high = 1;
        foreach (int side in new[] { (k + 1) % 3, (k + 2) % 3 })
        {
            var axis = box.Pose.Axis(side);
            double half = box.HalfExtents[side], center = box.Center.Dot(axis);
            ClipSegment(capsule.Start.Dot(axis) - center - half, direction.Dot(axis), ref low, ref high);
            ClipSegment(-capsule.Start.Dot(axis) + center - half, -direction.Dot(axis), ref low, ref high);
        }

        if (high - low <= 1e-9)
            return single;

        var faceCenter = box.Center + (faceOut * box.HalfExtents[k]);
        var points = new List<ContactPoint>();
        foreach (var (along, id) in new[] { (low, 1), (high, 2) })
        {
            var point = capsule.Start + (direction * along);
            double height = (point - faceCenter).Dot(faceOut);
            if (radius - height > 0)
                points.Add(new ContactPoint(point - (faceOut * ((radius + height) / 2)), radius - height, id));
        }

        return points.Count == 0 ? single : new ContactManifold(-faceOut, depth, points);
    }

    /// <summary>Параметры ближайших точек двух отрезков p1q1 и p2q2 (Эриксон, раздел 5.1.9)</summary>
    internal static (double S, double T) SegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        var (d1, d2, r) = (q1 - p1, q2 - p2, p1 - p2);
        double a = d1.LengthSquared, e = d2.LengthSquared, f = d2.Dot(r);
        if (a <= 0 && e <= 0)
            return (0, 0);
        if (a <= 0)
            return (0, Math.Clamp(f / e, 0, 1));

        double c = d1.Dot(r);
        if (e <= 0)
            return (Math.Clamp(-c / a, 0, 1), 0);

        double b = d1.Dot(d2), denominator = (a * e) - (b * b);
        double s = denominator > 0 ? Math.Clamp(((b * f) - (c * e)) / denominator, 0, 1) : 0;
        double t = ((b * s) + f) / e;
        if (t < 0)
            return (Math.Clamp(-c / a, 0, 1), 0);
        if (t > 1)
            return (Math.Clamp((b - c) / a, 0, 1), 1);
        return (s, t);
    }

    private static Vector3 ClosestOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        var direction = end - start;
        double length = direction.LengthSquared;
        return length > 0 ? start + (direction * Math.Clamp((point - start).Dot(direction) / length, 0, 1)) : start;
    }

    /// <summary>Сужает отрезок параметров [low, high] до части, где value + slope·t ≤ 0</summary>
    private static void ClipSegment(double value, double slope, ref double low, ref double high)
    {
        if (slope == 0)
        {
            if (value > 0)
                (low, high) = (1, 0);
            return;
        }

        double root = -value / slope;
        if (slope > 0)
            high = Math.Min(high, root);
        else
            low = Math.Max(low, root);
    }

    private static Vector3 Perpendicular(Vector3 axis) =>
        axis.Cross(Math.Abs(axis.X) < 0.57 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0)).Normalized;
}

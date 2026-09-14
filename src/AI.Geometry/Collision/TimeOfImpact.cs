#nullable enable
using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Момент первого касания при поступательном движении за шаг. Результат это доля шага от 0 до 1: 0, если тела уже
/// касаются в начале, и <c>null</c>, если за шаг они не сойдутся. Шар против плоскости и шара решается точно;
/// в остальных случаях работает консервативное продвижение: расстояние между выпуклыми телами при поступательном
/// движении выпукло по времени, поэтому шаг до нуля касательной никогда не перескакивает касание и сходится
/// квадратично
/// </summary>
public static class TimeOfImpact
{
    /// <summary>Допуск по расстоянию, при котором тела считаются коснувшимися</summary>
    public const double DefaultTolerance = 1e-9;

    private const int MaxIterations = 64;

    /// <summary>Касание движущегося шара с полупространством под плоскостью</summary>
    /// <param name="sphere">Шар в начале шага</param>
    /// <param name="displacement">Смещение шара за шаг</param>
    /// <param name="plane">Неподвижная плоскость</param>
    public static double? SpherePlane(SphereShape sphere, Vector3 displacement, Plane plane)
    {
        ArgumentNullException.ThrowIfNull(sphere);
        var (normal, offset) = CollisionDispatcher.UnitPlane(plane);
        double gap = normal.Dot(sphere.Center) + offset - sphere.Radius;
        if (gap <= 0)
            return 0;

        double approach = -normal.Dot(displacement);
        return approach > 0 && gap <= approach ? gap / approach : null;
    }

    /// <summary>Касание движущегося шара с неподвижным: корень квадратного уравнения |c + t·d|² = (r₁ + r₂)²</summary>
    /// <param name="moving">Движущийся шар в начале шага</param>
    /// <param name="displacement">Смещение движущегося шара за шаг</param>
    /// <param name="target">Неподвижный шар</param>
    public static double? SphereSphere(SphereShape moving, Vector3 displacement, SphereShape target)
    {
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(target);
        var span = moving.Center - target.Center;
        double reach = moving.Radius + target.Radius;
        double c = span.LengthSquared - (reach * reach);
        if (c <= 0)
            return 0;

        double b = span.Dot(displacement), a = displacement.LengthSquared;
        double discriminant = (b * b) - (a * c);
        if (b >= 0 || discriminant < 0)
            return null;

        double t = c / (-b + Math.Sqrt(discriminant));
        return t <= 1 ? t : null;
    }

    /// <summary>Касание движущегося шара с неподвижной коробкой по точному расстоянию до ближайшей точки коробки</summary>
    /// <param name="sphere">Шар в начале шага</param>
    /// <param name="displacement">Смещение шара за шаг</param>
    /// <param name="box">Неподвижная коробка</param>
    /// <param name="tolerance">Допуск по расстоянию</param>
    public static double? SphereBox(SphereShape sphere, Vector3 displacement, BoxShape box, double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(sphere);
        ArgumentNullException.ThrowIfNull(box);
        return Advance(
            t =>
            {
                var center = sphere.Center + (displacement * t);
                var span = center - box.ClosestPoint(center);
                double length = span.Length;
                return length > 0 ? (length - sphere.Radius, span / length) : (-sphere.Radius, Vector3.Zero);
            },
            displacement,
            tolerance);
    }

    /// <summary>
    /// Консервативное продвижение для любых выпуклых форм при поступательном движении: расстояние и нормаль на
    /// каждом шаге дает GJK
    /// </summary>
    /// <param name="a">Первая форма в начале шага</param>
    /// <param name="displacementA">Смещение первой формы за шаг</param>
    /// <param name="b">Вторая форма в начале шага</param>
    /// <param name="displacementB">Смещение второй формы за шаг</param>
    /// <param name="tolerance">Допуск по расстоянию</param>
    public static double? Translational(IConvexShape a, Vector3 displacementA, IConvexShape b, Vector3 displacementB,
        double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var relative = displacementB - displacementA;
        return Advance(
            t =>
            {
                var run = Gjk.Run(a, b, relative * t);
                return run.Intersecting || run.Distance <= 0 ? (0, Vector3.Zero) : (run.Distance, (run.OnB - run.OnA) / run.Distance);
            },
            relative,
            tolerance);
    }

    /// <summary>
    /// Шаг к нулю касательной расстояния: normal это направление, в котором сдвиг увеличивает расстояние, поэтому
    /// скорость сближения равна −normal·motion. Если за отведенное число шагов касание не достигнуто (скользящее
    /// касание), возвращается последний момент, заведомо не позже касания
    /// </summary>
    private static double? Advance(Func<double, (double Distance, Vector3 Normal)> distanceAt, Vector3 motion, double tolerance)
    {
        double t = 0;
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            var (distance, normal) = distanceAt(t);
            if (distance <= tolerance)
                return t;

            double approach = -normal.Dot(motion);
            if (approach <= 0)
                return null;

            t += distance / approach;
            if (t > 1)
                return null;
        }

        return t;
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;

namespace AI.Geometry.Constraints;

/// <summary>
/// Пространственная часть эскиза: точка на плоскости и расстояние до нее, поворот вокруг прямой и перенос,
/// а также помощники, которыми общие ограничения выбирают между плоским и пространственным счетом.
/// </summary>
public sealed partial class GeometricSketch
{
    /// <summary>
    /// Фиксирует положение точки в пространстве. У плоской точки аппликата равна нулю.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="x">Требуемая абсцисса.</param>
    /// <param name="y">Требуемая ордината.</param>
    /// <param name="z">Требуемая аппликата.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint FixPoint(string point, double x, double y, double z, string? name = null) =>
        Add("fix", name, [point], [.. Spatial(point), Constant(x), Constant(y), Constant(z)], 3,
            v => [v[0] - v[3], v[1] - v[4], v[2] - v[5]],
            _ => new double[,] { { 1, 0, 0, -1, 0, 0 }, { 0, 1, 0, 0, -1, 0 }, { 0, 0, 1, 0, 0, -1 } });

    /// <summary>
    /// Точка лежит на плоскости (невязка: знаковое расстояние до плоскости).
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="plane">Плоскость.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint PointOnPlane(string point, string plane, string? name = null) =>
        AddPlaneDistance("pointOnPlane", name, point, plane, null);

    /// <summary>
    /// Расстояние от точки до плоскости равно числу (больше нуля; для нуля используйте <see cref="PointOnPlane"/>).
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="plane">Плоскость.</param>
    /// <param name="value">Расстояние.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint DistanceToPlane(string point, string plane, double value, string? name = null) =>
        DistanceToPlane(point, plane, Constant(value), name);

    /// <summary>
    /// Расстояние от точки до плоскости равно параметру.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="plane">Плоскость.</param>
    /// <param name="parameter">Имя параметра со значением расстояния.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint DistanceToPlane(string point, string plane, string parameter, string? name = null) =>
        AddPlaneDistance("distanceToPlane", name, point, plane, parameter);

    /// <summary>
    /// Точка image есть точка point, повернутая вокруг прямой axis на угол по правилу правой руки
    /// (большой палец от первой точки прямой ко второй).
    /// </summary>
    /// <param name="image">Образ.</param>
    /// <param name="point">Исходная точка.</param>
    /// <param name="axis">Ось поворота.</param>
    /// <param name="radians">Угол в радианах.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Rotation(string image, string point, string axis, double radians, string? name = null) =>
        Rotation(image, point, axis, Constant(radians), name);

    /// <summary>
    /// Точка image есть точка point, повернутая вокруг прямой axis на угол, равный параметру.
    /// </summary>
    /// <param name="image">Образ.</param>
    /// <param name="point">Исходная точка.</param>
    /// <param name="axis">Ось поворота.</param>
    /// <param name="parameter">Имя параметра со значением угла в радианах.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Rotation(string image, string point, string axis, string parameter, string? name = null)
    {
        string[] unknowns = [.. Coordinates([image, point, .. Corners(axis)], true).SelectMany(p => p), parameter];
        return Add("rotation", name, [image, point, axis], unknowns, 3, v =>
        {
            Vector3 start = At(v, 6);
            Vector3 turned = RigidTransform.AboutAxis(start, At(v, 9) - start, v[12]).Apply(At(v, 3));
            return Components(At(v, 0) - turned);
        });
    }

    /// <summary>
    /// Точка image есть точка point, перенесенная на вектор от первой точки прямой ко второй.
    /// </summary>
    /// <param name="image">Образ.</param>
    /// <param name="point">Исходная точка.</param>
    /// <param name="line">Прямая, задающая вектор переноса.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Translation(string image, string point, string line, string? name = null)
    {
        var (start, end) = LinePoints(line);
        return AddLinear("translation", name, [image, point, line], [image, point, end, start], [1, -1, -1, 1]);
    }

    // Неориентированный угол между прямыми (две прямые или две нормали) или между прямой и плоскостью, от 0 до π/2
    internal static double SpatialAngle((Vector3 U, Vector3 W) pair, bool mixed)
    {
        double angle = Math.Atan2(pair.U.Cross(pair.W).Length, Math.Abs(pair.U.Dot(pair.W)));
        return mixed ? (Math.PI / 2) - angle : angle;
    }

    // Направление прямой по двум точкам или нормаль плоскости по трем
    internal static Vector3 Direction(IReadOnlyList<Vector3> corners) =>
        corners.Count == 3 ? (corners[1] - corners[0]).Cross(corners[2] - corners[0]) : corners[1] - corners[0];

    // Координаты точки в пространстве; у плоской точки аппликата это скрытый ноль
    private string[] Spatial(string point)
    {
        RequirePoint(point);
        return [X(point), Y(point), _spatial.Contains(point) ? Z(point) : _zero ??= Constant(0)];
    }

    // Координаты точек в общей размерности: три, если хоть одна точка пространственная или так велено
    private string[][] Coordinates(IReadOnlyList<string> points, bool spatial = false)
    {
        spatial |= points.Any(_spatial.Contains);
        return points.Select(point => spatial ? Spatial(point) : Point(point)).ToArray();
    }

    // Неизвестные двух прямых или плоскостей; в пространстве еще их направления по локальному массиву.
    // Mixed: прямая и плоскость, тогда направление прямой сравнивается с нормалью плоскости
    private (string[] Unknowns, Func<double[], (Vector3 U, Vector3 W)>? Units, bool Mixed) Directions(string first, string second)
    {
        string[] a = Corners(first);
        string[] b = Corners(second);
        string[][] c = Coordinates([.. a, .. b], a.Length == 3 || b.Length == 3);
        string[] unknowns = [.. c.SelectMany(p => p)];

        if (c[0].Length == 2)
            return (unknowns, null, false);

        int offset = 3 * a.Length;
        return (unknowns, v => (Unit(Axis(v, 0, a.Length)), Unit(Axis(v, offset, b.Length))), a.Length != b.Length);
    }

    // Неизвестные концов двух отрезков в общей размерности
    private (string[] Unknowns, int Dimension) Segments(string first, string second)
    {
        var (s1, e1) = LinePoints(first);
        var (s2, e2) = LinePoints(second);
        string[][] c = Coordinates([s1, e1, s2, e2]);
        return ([.. c.SelectMany(p => p)], c[0].Length);
    }

    private SketchConstraint AddPlaneDistance(string kind, string? name, string point, string plane, string? value)
    {
        string[] unknowns = [.. Coordinates([point, .. Corners(plane)], true).SelectMany(p => p)];
        return value == null
            ? Add(kind, name, [point, plane], unknowns, 1, v => [PlaneOffset(v)])
            : Add(kind, name, [point, plane], [.. unknowns, value], 1, v => [Math.Abs(PlaneOffset(v)) - v[12]]);
    }

    // Линейное соотношение точек Σ wₖ·pₖ = 0 покоординатно, с постоянными производными
    private SketchConstraint AddLinear(string kind, string? name, string[] label, string[] points, double[] weights)
    {
        string[][] c = Coordinates(points);
        int d = c[0].Length;
        var derivatives = new double[d, d * points.Length];

        for (int k = 0; k < points.Length; k++)
        {
            for (int i = 0; i < d; i++)
                derivatives[i, (k * d) + i] = weights[k];
        }

        return Add(kind, name, label, [.. c.SelectMany(p => p)], d,
            v =>
            {
                var residuals = new double[d];

                for (int k = 0; k < points.Length; k++)
                {
                    for (int i = 0; i < d; i++)
                        residuals[i] += weights[k] * v[(k * d) + i];
                }

                return residuals;
            },
            _ => derivatives);
    }

    // Расстояние между точками размерности d, записанными подряд с позиции offset
    private static double Span(double[] v, int offset, int d)
    {
        double sum = 0;

        for (int i = 0; i < d; i++)
        {
            double delta = v[offset + d + i] - v[offset + i];
            sum += delta * delta;
        }

        return Math.Sqrt(sum);
    }

    private static Vector3 At(double[] v, int offset) => new(v[offset], v[offset + 1], v[offset + 2]);

    private static Vector3 Axis(double[] v, int offset, int corners)
    {
        Vector3 origin = At(v, offset);
        Vector3 first = At(v, offset + 3) - origin;
        return corners == 3 ? first.Cross(At(v, offset + 6) - origin) : first;
    }

    private static Vector3 Unit(Vector3 a) => a / Math.Max(a.Length, TinyLength);

    private static double[] Components(Vector3 a) => [a.X, a.Y, a.Z];

    // Невязка параллельности векторов (векторное произведение единичных) или их перпендикулярности (скалярное)
    private static double[] Relation((Vector3 U, Vector3 W) pair, bool parallel) =>
        parallel ? Components(pair.U.Cross(pair.W)) : [pair.U.Dot(pair.W)];

    // Смещение точки (первые три значения) от прямой через две следующие точки
    private static Vector3 LineOffset(double[] v)
    {
        Vector3 start = At(v, 3);
        return (At(v, 0) - start).Cross(Unit(At(v, 6) - start));
    }

    // Знаковое расстояние от точки (первые три значения) до плоскости через три следующие точки
    private static double PlaneOffset(double[] v) => (At(v, 0) - At(v, 3)).Dot(Unit(Axis(v, 3, 3)));

    // Симметрия относительно оси в пространстве: середина на оси, отрезок между точками перпендикулярен оси
    private static double[] SymmetricResiduals(double[] v)
    {
        Vector3 a = At(v, 0);
        Vector3 b = At(v, 3);
        Vector3 start = At(v, 6);
        Vector3 direction = Unit(At(v, 9) - start);
        return [.. Components((((a + b) * 0.5) - start).Cross(direction)), (b - a).Dot(direction)];
    }
}

#nullable enable

using AI.DataStructs.Algebraic;
using AI.Geometry.Hull;
using AI.Geometry.MassProperties;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Geometry.Constraints;

/// <summary>
/// Решение эскиза: значения неизвестных, отчет о выполнении ограничений и запросы к найденной геометрии.
/// </summary>
public sealed class SketchSolution
{
    private readonly GeometricSketch _sketch;

    internal SketchSolution(
        GeometricSketch sketch,
        double[] values,
        IReadOnlyList<ConstraintResidual> residuals,
        IReadOnlyList<string> violated,
        IReadOnlyList<string> undetermined)
    {
        _sketch = sketch;
        FullValues = values;
        Residuals = residuals;
        ViolatedConstraints = violated;
        UndeterminedUnknowns = undetermined;
        MaxResidual = residuals.Select(r => r.Value).DefaultIfEmpty(0).Max();

        var visible = new Dictionary<string, double>(StringComparer.Ordinal);

        for (int i = 0; i < values.Length; i++)
        {
            string name = sketch.AllNames[i];

            if (!GeometricSketch.IsHidden(name))
                visible[name] = values[i];
        }

        Values = visible;
    }

    /// <summary>
    /// Выполнен ли критерий сходимости метода Левенберга-Марквардта (а не исчерпан лимит итераций).
    /// </summary>
    public bool Converged { get; init; }

    /// <summary>
    /// Все ли ограничения выполнены с заданным допуском.
    /// </summary>
    public bool Satisfied { get; init; }

    /// <summary>
    /// Состояние системы: определена, недоопределена, избыточна или противоречива.
    /// </summary>
    public SketchStatus Status { get; init; }

    /// <summary>
    /// Число степеней свободы: число незафиксированных неизвестных минус ранг якобиана.
    /// </summary>
    public int DegreesOfFreedom { get; init; }

    /// <summary>
    /// Численный ранг якобиана ограничений в решении.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Число зависимых уравнений: число скалярных уравнений минус ранг якобиана.
    /// </summary>
    public int Redundancy { get; init; }

    /// <summary>
    /// Сумма квадратов всех невязок.
    /// </summary>
    public double Cost { get; init; }

    /// <summary>
    /// Число итераций решателя.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Наибольшая невязка ограничения.
    /// </summary>
    public double MaxResidual { get; }

    /// <summary>
    /// Невязки всех ограничений в порядке их добавления.
    /// </summary>
    public IReadOnlyList<ConstraintResidual> Residuals { get; }

    /// <summary>
    /// Имена невыполненных ограничений по убыванию невязки; для противоречивой системы первыми идут наиболее вероятные виновники.
    /// </summary>
    public IReadOnlyList<string> ViolatedConstraints { get; }

    /// <summary>
    /// Неизвестные, которые ограничения не определяют (имеют составляющую в ядре якобиана).
    /// Пусто, если степеней свободы нет.
    /// </summary>
    public IReadOnlyList<string> UndeterminedUnknowns { get; }

    /// <summary>
    /// Значения всех видимых неизвестных: «A.x», «A.y», «c.r», параметры.
    /// </summary>
    public IReadOnlyDictionary<string, double> Values { get; }

    internal double[] FullValues { get; }

    /// <summary>
    /// Значение неизвестной по имени.
    /// </summary>
    /// <param name="unknown">Имя неизвестной.</param>
    public double Value(string unknown) => FullValues[_sketch.IndexOf(unknown)];

    /// <summary>
    /// Координаты точки.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <returns>Вектор (x, y) плоской точки или (x, y, z) пространственной.</returns>
    public Vector Point(string point)
    {
        _sketch.RequirePoint(point);
        double x = Value(GeometricSketch.X(point));
        double y = Value(GeometricSketch.Y(point));
        return _sketch.IsSpatialPoint(point) ? new Vector(x, y, Value(GeometricSketch.Z(point))) : new Vector(x, y);
    }

    /// <summary>
    /// Точка в пространстве; у плоской точки аппликата равна нулю.
    /// </summary>
    /// <param name="point">Точка.</param>
    public Vector3 Point3(string point)
    {
        Vector p = Point(point);
        return new Vector3(p[0], p[1], p.Count > 2 ? p[2] : 0);
    }

    /// <summary>
    /// Расстояние между двумя точками.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    public double Distance(string a, string b) => Point3(a).DistanceTo(Point3(b));

    /// <summary>
    /// Длина отрезка между определяющими точками прямой.
    /// </summary>
    /// <param name="line">Прямая или отрезок.</param>
    public double Length(string line)
    {
        var (start, end) = _sketch.LinePoints(line);
        return Distance(start, end);
    }

    /// <summary>
    /// Расстояние от точки до прямой.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="line">Прямая.</param>
    public double DistanceToLine(string point, string line)
    {
        var (start, end) = _sketch.LinePoints(line);

        if (!IsSpatial(point, start, end))
        {
            Vector p = Point(point);
            return Math.Abs(GeometricSketch.SignedDistance(p[0], p[1], LineValues(line), 0));
        }

        Vector3 s = Point3(start);
        return (Point3(point) - s).Cross((Point3(end) - s).Normalized).Length;
    }

    /// <summary>
    /// Расстояние от точки до плоскости.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="plane">Плоскость.</param>
    public double DistanceToPlane(string point, string plane)
    {
        var (a, _, _) = _sketch.PlanePoints(plane);
        return Math.Abs((Point3(point) - Point3(a)).Dot(Direction(plane).Normalized));
    }

    /// <summary>
    /// Неориентированный угол между прямыми в радианах, от 0 до π/2. В пространстве сущностями могут быть
    /// прямые и плоскости: угол прямой с плоскостью есть угол с ее проекцией, угол плоскостей есть угол нормалей.
    /// </summary>
    /// <param name="first">Первая прямая или плоскость.</param>
    /// <param name="second">Вторая прямая или плоскость.</param>
    public double Angle(string first, string second)
    {
        string[] a = _sketch.Corners(first);
        string[] b = _sketch.Corners(second);

        if (a.Length == 2 && b.Length == 2 && !IsSpatial([.. a, .. b]))
            return GeometricSketch.UnorientedAngle([.. LineValues(first), .. LineValues(second)], 0, 4);

        return GeometricSketch.SpatialAngle((Direction(first), Direction(second)), a.Length != b.Length);
    }

    /// <summary>
    /// Ориентированный угол от направления первой прямой до направления второй в радианах, от -π до π.
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    public double OrientedAngle(string first, string second) =>
        GeometricSketch.DirectedAngle([.. LineValues(first), .. LineValues(second)], 0, 4);

    /// <summary>
    /// Угол при вершине между лучами к двум точкам (угол ABC при вершине B) в радианах, от 0 до π.
    /// </summary>
    /// <param name="a">Точка на первом луче.</param>
    /// <param name="vertex">Вершина угла.</param>
    /// <param name="c">Точка на втором луче.</param>
    public double AngleAt(string a, string vertex, string c)
    {
        if (IsSpatial(a, vertex, c))
        {
            Vector3 u = Point3(a) - Point3(vertex);
            Vector3 w = Point3(c) - Point3(vertex);
            return Math.Atan2(u.Cross(w).Length, u.Dot(w));
        }

        Vector p = Point(a);
        Vector b = Point(vertex);
        Vector q = Point(c);
        double[] rays = [b[0], b[1], p[0], p[1], b[0], b[1], q[0], q[1]];
        return Math.Abs(GeometricSketch.DirectedAngle(rays, 0, 4));
    }

    /// <summary>
    /// Радиус окружности.
    /// </summary>
    /// <param name="circle">Окружность.</param>
    public double Radius(string circle) => Value(_sketch.CircleParts(circle).Radius);

    /// <summary>
    /// Площадь многоугольника с вершинами в указанных точках (в порядке обхода). В пространстве многоугольник
    /// считается плоским, площадь берется по формуле Ньюэлла: половина длины суммы векторных произведений соседних вершин.
    /// </summary>
    /// <param name="points">Вершины многоугольника, не меньше трех.</param>
    public double Area(params string[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Length < 3)
            throw new ArgumentException("Многоугольник должен иметь не меньше трех вершин.", nameof(points));

        if (!IsSpatial(points))
            return ShoelaceArea.Area(points.Select(Point).ToArray());

        Vector3 sum = Vector3.Zero;

        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            sum += Point3(points[j]).Cross(Point3(points[i]));

        return 0.5 * sum.Length;
    }

    /// <summary>
    /// Объем выпуклой оболочки точек (выпуклого тела на них). Точки в одной плоскости дают нулевой объем.
    /// </summary>
    /// <param name="points">Точки, не меньше четырех.</param>
    public double Volume(params string[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        try
        {
            ConvexHull3D hull = ConvexHull3D.Build(points.Select(Point3).ToArray());
            return MeshMass.Compute(hull.Vertices, hull.Triangles).Volume;
        }
        catch (ArgumentException) when (points.Length >= 4)
        {
            // Оболочка не строится, если все точки на одной плоскости, прямой или совпадают: тело вырождено
            return 0;
        }
    }

    /// <summary>
    /// Произвольный запрос: выражение над значениями видимых неизвестных.
    /// </summary>
    /// <param name="query">Выражение над словарем <see cref="Values"/>.</param>
    public double Evaluate(Func<IReadOnlyDictionary<string, double>, double> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query(Values);
    }

    private bool IsSpatial(params string[] points) => points.Any(_sketch.IsSpatialPoint);

    // Направление прямой или нормаль плоскости
    private Vector3 Direction(string id) => GeometricSketch.Direction(_sketch.Corners(id).Select(Point3).ToArray());

    private double[] LineValues(string line)
    {
        var (start, end) = _sketch.LinePoints(line);
        Vector p = Point(start);
        Vector q = Point(end);
        return [p[0], p[1], q[0], q[1]];
    }
}

#nullable enable

using AI.DataStructs.Algebraic;
using AI.Geometry.Polygons;
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
    /// <returns>Вектор (x, y).</returns>
    public Vector Point(string point)
    {
        _sketch.RequirePoint(point);
        return new Vector(Value(GeometricSketch.X(point)), Value(GeometricSketch.Y(point)));
    }

    /// <summary>
    /// Расстояние между двумя точками.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    public double Distance(string a, string b)
    {
        Vector p = Point(a);
        Vector q = Point(b);
        return GeometricSketch.Hypot(p[0] - q[0], p[1] - q[1]);
    }

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
        Vector p = Point(point);
        return Math.Abs(GeometricSketch.SignedDistance(p[0], p[1], LineValues(line), 0));
    }

    /// <summary>
    /// Неориентированный угол между прямыми в радианах, от 0 до π/2.
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    public double Angle(string first, string second) =>
        GeometricSketch.UnorientedAngle([.. LineValues(first), .. LineValues(second)], 0, 4);

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
    /// Площадь многоугольника с вершинами в указанных точках (в порядке обхода).
    /// </summary>
    /// <param name="points">Вершины многоугольника, не меньше трех.</param>
    public double Area(params string[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Length < 3)
            throw new ArgumentException("Многоугольник должен иметь не меньше трех вершин.", nameof(points));

        return ShoelaceArea.Area(points.Select(Point).ToArray());
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

    private double[] LineValues(string line)
    {
        var (start, end) = _sketch.LinePoints(line);
        Vector p = Point(start);
        Vector q = Point(end);
        return [p[0], p[1], q[0], q[1]];
    }
}

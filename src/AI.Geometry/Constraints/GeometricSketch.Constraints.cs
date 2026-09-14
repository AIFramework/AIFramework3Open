#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Geometry.Constraints;

/// <summary>
/// Ограничения эскиза. Каждый метод добавляет блок невязок и возвращает его; имя по умолчанию
/// строится из вида ограничения и идентификаторов сущностей, например «distance(A,B)».
/// Методы с числовым значением имеют перегрузку, где вместо числа стоит имя параметра.
/// </summary>
public sealed partial class GeometricSketch
{
    // Защита от деления на ноль, когда две точки прямой временно совпали
    private const double TinyLength = 1e-300;

    /// <summary>
    /// Фиксирует одну неизвестную (координату, радиус, параметр): значение равно заданному.
    /// </summary>
    /// <param name="unknown">Имя неизвестной, например «A.x».</param>
    /// <param name="value">Требуемое значение.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Fix(string unknown, double value, string? name = null) =>
        Add("fix", name, [unknown], [unknown, Constant(value)], 1,
            v => [v[0] - v[1]],
            _ => new double[,] { { 1, -1 } });

    /// <summary>
    /// Фиксирует положение точки.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="x">Требуемая абсцисса.</param>
    /// <param name="y">Требуемая ордината.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint FixPoint(string point, double x, double y, string? name = null) =>
        Add("fix", name, [point], [.. Point(point), Constant(x), Constant(y)], 2,
            v => [v[0] - v[2], v[1] - v[3]],
            _ => new double[,] { { 1, 0, -1, 0 }, { 0, 1, 0, -1 } });

    /// <summary>
    /// Совпадение двух точек.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Coincident(string a, string b, string? name = null) =>
        Add("coincident", name, [a, b], [.. Point(a), .. Point(b)], 2,
            v => [v[0] - v[2], v[1] - v[3]],
            _ => new double[,] { { 1, 0, -1, 0 }, { 0, 1, 0, -1 } });

    /// <summary>
    /// Расстояние между двумя точками равно числу. Для нулевого расстояния используйте <see cref="Coincident"/>.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <param name="value">Расстояние.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Distance(string a, string b, double value, string? name = null) =>
        Distance(a, b, Constant(value), name);

    /// <summary>
    /// Расстояние между двумя точками равно параметру.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <param name="parameter">Имя параметра (или другой неизвестной) со значением расстояния.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Distance(string a, string b, string parameter, string? name = null) =>
        AddDistance("distance", name, [a, b], a, b, parameter);

    /// <summary>
    /// Длина отрезка (расстояние между определяющими точками прямой) равна числу.
    /// </summary>
    /// <param name="line">Прямая или отрезок.</param>
    /// <param name="value">Длина.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Length(string line, double value, string? name = null) =>
        Length(line, Constant(value), name);

    /// <summary>
    /// Длина отрезка равна параметру.
    /// </summary>
    /// <param name="line">Прямая или отрезок.</param>
    /// <param name="parameter">Имя параметра со значением длины.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Length(string line, string parameter, string? name = null)
    {
        var (start, end) = LinePoints(line);
        return AddDistance("length", name, [line], start, end, parameter);
    }

    /// <summary>
    /// Точка лежит на окружности.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="circle">Окружность.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint PointOnCircle(string point, string circle, string? name = null)
    {
        var (center, radius) = CircleParts(circle);
        return AddDistance("pointOnCircle", name, [point, circle], point, center, radius);
    }

    /// <summary>
    /// Радиус окружности равен числу.
    /// </summary>
    /// <param name="circle">Окружность.</param>
    /// <param name="value">Радиус.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Radius(string circle, double value, string? name = null) =>
        Radius(circle, Constant(value), name);

    /// <summary>
    /// Радиус окружности равен параметру.
    /// </summary>
    /// <param name="circle">Окружность.</param>
    /// <param name="parameter">Имя параметра со значением радиуса.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Radius(string circle, string parameter, string? name = null) =>
        Add("radius", name, [circle], [CircleParts(circle).Radius, parameter], 1,
            v => [v[0] - v[1]],
            _ => new double[,] { { 1, -1 } });

    /// <summary>
    /// Равенство радиусов двух окружностей.
    /// </summary>
    /// <param name="first">Первая окружность.</param>
    /// <param name="second">Вторая окружность.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint EqualRadius(string first, string second, string? name = null) =>
        Add("equalRadius", name, [first, second], [CircleParts(first).Radius, CircleParts(second).Radius], 1,
            v => [v[0] - v[1]],
            _ => new double[,] { { 1, -1 } });

    /// <summary>
    /// Прямая горизонтальна: ординаты ее точек равны.
    /// </summary>
    /// <param name="line">Прямая.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Horizontal(string line, string? name = null) =>
        Add("horizontal", name, [line], Line(line), 1,
            v => [v[1] - v[3]],
            _ => new double[,] { { 0, 1, 0, -1 } });

    /// <summary>
    /// Прямая вертикальна: абсциссы ее точек равны.
    /// </summary>
    /// <param name="line">Прямая.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Vertical(string line, string? name = null) =>
        Add("vertical", name, [line], Line(line), 1,
            v => [v[0] - v[2]],
            _ => new double[,] { { 1, 0, -1, 0 } });

    /// <summary>
    /// Параллельность двух прямых (невязка: синус угла между ними).
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Parallel(string first, string second, string? name = null) =>
        Add("parallel", name, [first, second], [.. Line(first), .. Line(second)], 1,
            v => [LineSinCos(v, 0, 4).Sin]);

    /// <summary>
    /// Перпендикулярность двух прямых (невязка: косинус угла между ними).
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Perpendicular(string first, string second, string? name = null) =>
        Add("perpendicular", name, [first, second], [.. Line(first), .. Line(second)], 1,
            v => [LineSinCos(v, 0, 4).Cos]);

    /// <summary>
    /// Неориентированный угол между прямыми (от 0 до π/2 радиан) равен числу.
    /// Для 0 и π/2 надежнее <see cref="Parallel"/> и <see cref="Perpendicular"/>.
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    /// <param name="radians">Угол в радианах.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Angle(string first, string second, double radians, string? name = null) =>
        Angle(first, second, Constant(radians), name);

    /// <summary>
    /// Неориентированный угол между прямыми (от 0 до π/2 радиан) равен параметру.
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    /// <param name="parameter">Имя параметра со значением угла в радианах.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Angle(string first, string second, string parameter, string? name = null) =>
        Add("angle", name, [first, second], [.. Line(first), .. Line(second), parameter], 1,
            v => [UnorientedAngle(v, 0, 4) - v[8]]);

    /// <summary>
    /// Ориентированный угол от направления первой прямой (от ее первой точки ко второй) до направления второй
    /// равен числу; положительное направление против часовой стрелки.
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    /// <param name="radians">Угол в радианах.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint OrientedAngle(string first, string second, double radians, string? name = null) =>
        OrientedAngle(first, second, Constant(radians), name);

    /// <summary>
    /// Ориентированный угол от направления первой прямой до направления второй равен параметру.
    /// </summary>
    /// <param name="first">Первая прямая.</param>
    /// <param name="second">Вторая прямая.</param>
    /// <param name="parameter">Имя параметра со значением угла в радианах.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint OrientedAngle(string first, string second, string parameter, string? name = null) =>
        Add("orientedAngle", name, [first, second], [.. Line(first), .. Line(second), parameter], 1,
            v => [WrapAngle(DirectedAngle(v, 0, 4) - v[8])]);

    /// <summary>
    /// Точка лежит на прямой (невязка: знаковое расстояние до прямой).
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="line">Прямая.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint PointOnLine(string point, string line, string? name = null) =>
        Add("pointOnLine", name, [point, line], [.. Point(point), .. Line(line)], 1,
            v => [SignedDistance(v[0], v[1], v, 2)]);

    /// <summary>
    /// Расстояние от точки до прямой равно числу (больше нуля; для нуля используйте <see cref="PointOnLine"/>).
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="line">Прямая.</param>
    /// <param name="value">Расстояние.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint DistanceToLine(string point, string line, double value, string? name = null) =>
        DistanceToLine(point, line, Constant(value), name);

    /// <summary>
    /// Расстояние от точки до прямой равно параметру.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="line">Прямая.</param>
    /// <param name="parameter">Имя параметра со значением расстояния.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint DistanceToLine(string point, string line, string parameter, string? name = null) =>
        AddDistanceToLine("distanceToLine", name, [point, line], point, line, parameter);

    /// <summary>
    /// Касание прямой и окружности: расстояние от центра до прямой равно радиусу.
    /// </summary>
    /// <param name="line">Прямая.</param>
    /// <param name="circle">Окружность.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Tangent(string line, string circle, string? name = null)
    {
        var (center, radius) = CircleParts(circle);
        return AddDistanceToLine("tangent", name, [line, circle], center, line, radius);
    }

    /// <summary>
    /// Касание двух окружностей: внешнее (расстояние между центрами равно сумме радиусов)
    /// или внутреннее (равно модулю разности радиусов).
    /// </summary>
    /// <param name="first">Первая окружность.</param>
    /// <param name="second">Вторая окружность.</param>
    /// <param name="isInternal">true для внутреннего касания.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint TangentCircles(string first, string second, bool isInternal = false, string? name = null)
    {
        var (c1, r1) = CircleParts(first);
        var (c2, r2) = CircleParts(second);
        return Add(isInternal ? "tangentInternal" : "tangentExternal", name, [first, second],
            [.. Point(c1), .. Point(c2), r1, r2], 1,
            v =>
            {
                double gap = isInternal ? Math.Abs(v[4] - v[5]) : v[4] + v[5];
                return [Hypot(v[0] - v[2], v[1] - v[3]) - gap];
            });
    }

    /// <summary>
    /// Равенство длин двух отрезков.
    /// </summary>
    /// <param name="first">Первый отрезок.</param>
    /// <param name="second">Второй отрезок.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint EqualLength(string first, string second, string? name = null) =>
        Add("equalLength", name, [first, second], [.. Line(first), .. Line(second)], 1,
            v => [SegmentLength(v, 0) - SegmentLength(v, 4)]);

    /// <summary>
    /// Отношение длин отрезков: длина первого равна числу, умноженному на длину второго.
    /// </summary>
    /// <param name="first">Первый отрезок.</param>
    /// <param name="second">Второй отрезок.</param>
    /// <param name="ratio">Отношение длины первого к длине второго.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint LengthRatio(string first, string second, double ratio, string? name = null) =>
        LengthRatio(first, second, Constant(ratio), name);

    /// <summary>
    /// Отношение длин отрезков равно параметру.
    /// </summary>
    /// <param name="first">Первый отрезок.</param>
    /// <param name="second">Второй отрезок.</param>
    /// <param name="parameter">Имя параметра со значением отношения.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint LengthRatio(string first, string second, string parameter, string? name = null) =>
        Add("lengthRatio", name, [first, second], [.. Line(first), .. Line(second), parameter], 1,
            v => [SegmentLength(v, 0) - (v[8] * SegmentLength(v, 4))]);

    /// <summary>
    /// Точка является серединой отрезка между двумя точками.
    /// </summary>
    /// <param name="middle">Середина.</param>
    /// <param name="a">Первый конец.</param>
    /// <param name="b">Второй конец.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Midpoint(string middle, string a, string b, string? name = null) =>
        Add("midpoint", name, [middle, a, b], [.. Point(middle), .. Point(a), .. Point(b)], 2,
            v => [v[0] - (0.5 * (v[2] + v[4])), v[1] - (0.5 * (v[3] + v[5]))],
            _ => new double[,] { { 1, 0, -0.5, 0, -0.5, 0 }, { 0, 1, 0, -0.5, 0, -0.5 } });

    /// <summary>
    /// Точки симметричны относительно прямой: середина между ними лежит на прямой,
    /// а соединяющий их отрезок перпендикулярен прямой.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <param name="line">Ось симметрии.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    public SketchConstraint Symmetric(string a, string b, string line, string? name = null) =>
        Add("symmetric", name, [a, b, line], [.. Point(a), .. Point(b), .. Line(line)], 2,
            v =>
            {
                double dx = v[6] - v[4];
                double dy = v[7] - v[5];
                double length = Math.Max(Hypot(dx, dy), TinyLength);
                double along = ((dx * (v[2] - v[0])) + (dy * (v[3] - v[1]))) / length;
                return [SignedDistance(0.5 * (v[0] + v[2]), 0.5 * (v[1] + v[3]), v, 4), along];
            });

    /// <summary>
    /// Произвольное ограничение: выражение от именованных значений должно равняться нулю.
    /// </summary>
    /// <param name="references">
    /// Идентификаторы, от которых зависит выражение: имена неизвестных («A.x», «c.r», параметр) или сущностей.
    /// Точка раскрывается в «id.x», «id.y»; окружность в координаты центра и «id.r»; прямая в координаты своих точек.
    /// </param>
    /// <param name="residual">Выражение над словарем значений; решение ищется там, где оно равно нулю.</param>
    /// <param name="name">Имя ограничения; если null, строится автоматически.</param>
    /// <example>
    /// <code>
    /// // Площадь треугольника ABC равна 6
    /// sketch.Expression(["A", "B", "C"], v =>
    ///     0.5 * ((v["B.x"] - v["A.x"]) * (v["C.y"] - v["A.y"]) - (v["C.x"] - v["A.x"]) * (v["B.y"] - v["A.y"])) - 6);
    /// </code>
    /// </example>
    public SketchConstraint Expression(
        IReadOnlyList<string> references,
        Func<IReadOnlyDictionary<string, double>, double> residual,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(residual);

        string[] unknowns = references.SelectMany(Expand).Distinct(StringComparer.Ordinal).ToArray();

        return Add("expression", name, [.. references], unknowns, 1, v =>
        {
            var values = new Dictionary<string, double>(unknowns.Length, StringComparer.Ordinal);

            for (int k = 0; k < unknowns.Length; k++)
                values[unknowns[k]] = v[k];

            return [residual(values)];
        });
    }

    internal IEnumerable<string> Expand(string reference)
    {
        if (_points.Contains(reference))
            return Point(reference);

        if (_lines.ContainsKey(reference))
            return Line(reference);

        if (_circles.TryGetValue(reference, out var circle))
            return [.. Point(circle.Center), circle.Radius];

        IndexOf(reference);
        return [reference];
    }

    internal static double Hypot(double x, double y) => Math.Sqrt((x * x) + (y * y));

    // Знаковое расстояние от точки (px, py) до прямой, заданной четырьмя значениями начиная с offset
    internal static double SignedDistance(double px, double py, IReadOnlyList<double> v, int offset)
    {
        double dx = v[offset + 2] - v[offset];
        double dy = v[offset + 3] - v[offset + 1];
        double length = Math.Max(Hypot(dx, dy), TinyLength);
        return ((dx * (py - v[offset + 1])) - (dy * (px - v[offset]))) / length;
    }

    // Синус и косинус угла между направлениями двух прямых
    internal static (double Sin, double Cos) LineSinCos(IReadOnlyList<double> v, int first, int second)
    {
        double ax = v[first + 2] - v[first];
        double ay = v[first + 3] - v[first + 1];
        double bx = v[second + 2] - v[second];
        double by = v[second + 3] - v[second + 1];
        double norm = Math.Max(Hypot(ax, ay) * Hypot(bx, by), TinyLength);
        return (((ax * by) - (ay * bx)) / norm, ((ax * bx) + (ay * by)) / norm);
    }

    internal static double DirectedAngle(IReadOnlyList<double> v, int first, int second)
    {
        var (sin, cos) = LineSinCos(v, first, second);
        return Math.Atan2(sin, cos);
    }

    internal static double UnorientedAngle(IReadOnlyList<double> v, int first, int second)
    {
        var (sin, cos) = LineSinCos(v, first, second);
        return Math.Atan2(Math.Abs(sin), Math.Abs(cos));
    }

    internal static double SegmentLength(IReadOnlyList<double> v, int offset) =>
        Hypot(v[offset + 2] - v[offset], v[offset + 3] - v[offset + 1]);

    // Приведение угла к промежутку (-π, π]
    private static double WrapAngle(double angle)
    {
        double wrapped = Math.IEEERemainder(angle, 2 * Math.PI);
        return wrapped <= -Math.PI ? wrapped + (2 * Math.PI) : wrapped;
    }

    private string[] Point(string point)
    {
        RequirePoint(point);
        return [X(point), Y(point)];
    }

    private string[] Line(string line)
    {
        var (start, end) = LinePoints(line);
        return [X(start), Y(start), X(end), Y(end)];
    }

    // |AB| - value с аналитическими производными; в совпадающих точках производная не определена
    private SketchConstraint AddDistance(string kind, string? name, string[] label, string a, string b, string value) =>
        Add(kind, name, label, [.. Point(a), .. Point(b), value], 1,
            v => [Hypot(v[0] - v[2], v[1] - v[3]) - v[4]],
            v =>
            {
                double dx = v[0] - v[2];
                double dy = v[1] - v[3];
                double d = Hypot(dx, dy);
                return d == 0 ? null : new double[,] { { dx / d, dy / d, -dx / d, -dy / d, -1 } };
            });

    private SketchConstraint AddDistanceToLine(string kind, string? name, string[] label, string point, string line, string value) =>
        Add(kind, name, label, [.. Point(point), .. Line(line), value], 1,
            v => [Math.Abs(SignedDistance(v[0], v[1], v, 2)) - v[6]]);

    private SketchConstraint Add(
        string kind,
        string? name,
        string[] label,
        string[] unknowns,
        int count,
        Func<double[], double[]> residuals,
        Func<double[], double[,]?>? derivatives = null)
    {
        foreach (string unknown in unknowns)
            IndexOf(unknown);

        string unique;

        if (name != null)
        {
            if (_constraints.Any(c => c.Name == name))
                throw new ArgumentException($"Ограничение «{name}» уже существует.", nameof(name));

            unique = name;
        }
        else
        {
            string baseName = $"{kind}({string.Join(",", label)})";
            unique = baseName;

            for (int k = 2; _constraints.Any(c => c.Name == unique); k++)
                unique = $"{baseName}#{k}";
        }

        var constraint = new SketchConstraint(unique, kind, unknowns, count, residuals, derivatives);
        _constraints.Add(constraint);
        return constraint;
    }
}

#nullable enable

using AI.Units;

namespace AI.Earth.Geodesy;

/// <summary>
/// Фигуры на сфере: угол сферического треугольника и площадь сферического многоугольника.
/// </summary>
/// <remarks>
/// Стороны фигур это меньшие дуги больших кругов между соседними вершинами. Вершины переводятся в единичные
/// векторы, и формулы работают с ними, так что полюса и переход через линию смены дат особыми случаями не являются.
/// </remarks>
public static class SphericalGeometry
{
    private const double Degree = Math.PI / 180.0;

    /// <summary>
    /// Угол сферического треугольника abc при вершине b: угол между дугами больших кругов из b в a и из b в c.
    /// </summary>
    /// <param name="a">Первая соседняя вершина</param>
    /// <param name="b">Вершина угла</param>
    /// <param name="c">Вторая соседняя вершина</param>
    /// <returns>Угол в градусах от 0 до 180; NaN, если соседняя вершина совпадает с b или противоположна ей</returns>
    public static double VertexAngle(GeoPoint a, GeoPoint b, GeoPoint c)
    {
        var (ua, ub, uc) = (Unit(a), Unit(b), Unit(c));

        // Нормали плоскостей больших кругов через b; угол между ними равен углу между дугами
        var toA = Cross(ub, ua);
        var toC = Cross(ub, uc);
        if (Dot(toA, toA) < 1e-30 || Dot(toC, toC) < 1e-30)
            return double.NaN;

        var normal = Cross(toA, toC);
        return Math.Atan2(Math.Sqrt(Dot(normal, normal)), Dot(toA, toC)) / Degree;
    }

    /// <summary>
    /// Площадь сферического многоугольника. Замкнутая ломаная из дуг делит сферу на две части; возвращается меньшая,
    /// так что направление обхода не важно.
    /// </summary>
    /// <param name="vertices">Вершины по порядку обхода, хотя бы три; ни одна не противоположна первой</param>
    /// <param name="radius">Радиус сферы, метры; по умолчанию средний радиус Земли, как в
    /// <see cref="Geodesy.GreatCircleDistance"/></param>
    /// <returns>Площадь, квадратные метры</returns>
    /// <exception cref="ArgumentNullException">Вершины не заданы</exception>
    /// <exception cref="ArgumentException">Вершин меньше трех</exception>
    /// <remarks>
    /// Многоугольник разбивается веером из первой вершины на треугольники, и их ориентированные сферические избытки
    /// складываются. Избыток треугольника берется по формуле ван Остерома и Страккее:
    /// tg(E/2) = det(a, b, c) / (1 + a·b + b·c + c·a). Она устойчива и для малых, и для больших треугольников, а знак
    /// определителя дает вычитание треугольников, лежащих вне невыпуклого многоугольника.
    /// </remarks>
    public static Quantity PolygonArea(IReadOnlyList<GeoPoint> vertices, double radius = 6371008.8)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count < 3)
            throw new ArgumentException("У многоугольника должно быть хотя бы три вершины", nameof(vertices));

        var first = Unit(vertices[0]);
        var previous = Unit(vertices[1]);
        double excess = 0;
        for (int i = 2; i < vertices.Count; i++)
        {
            var next = Unit(vertices[i]);
            double determinant = Dot(first, Cross(previous, next));
            double denominator = 1 + Dot(first, previous) + Dot(previous, next) + Dot(next, first);
            excess += 2 * Math.Atan2(determinant, denominator);
            previous = next;
        }

        double sphere = 4 * Math.PI;
        double part = Math.Abs(excess) % sphere;
        return new Quantity(Math.Min(part, sphere - part) * radius * radius, Dimension.Area);
    }

    /// <summary>Единичный вектор из центра сферы в точку</summary>
    private static (double X, double Y, double Z) Unit(GeoPoint point)
    {
        double latitude = point.LatitudeRadians, longitude = point.LongitudeRadians;
        return (Math.Cos(latitude) * Math.Cos(longitude), Math.Cos(latitude) * Math.Sin(longitude), Math.Sin(latitude));
    }

    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) u, (double X, double Y, double Z) v) =>
        ((u.Y * v.Z) - (u.Z * v.Y), (u.Z * v.X) - (u.X * v.Z), (u.X * v.Y) - (u.Y * v.X));

    private static double Dot((double X, double Y, double Z) u, (double X, double Y, double Z) v) =>
        (u.X * v.X) + (u.Y * v.Y) + (u.Z * v.Z);
}

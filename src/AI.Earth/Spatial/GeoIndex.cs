using AI.Earth.Geodesy;
using AI.Units;

namespace AI.Earth.Spatial;

/// <summary>
/// Прямоугольная область в географических координатах
/// </summary>
/// <param name="South">Южная граница</param>
/// <param name="West">Западная граница</param>
/// <param name="North">Северная граница</param>
/// <param name="East">Восточная граница</param>
/// <remarks>
/// Область может пересекать линию перемены дат: тогда западная граница численно больше
/// восточной. Проверка вхождения это учитывает — иначе любая рамка вокруг Чукотки
/// оказывалась бы пустой.
/// </remarks>
public readonly record struct GeoBounds(double South, double West, double North, double East)
{
    /// <summary>Пересекает ли область линию перемены дат</summary>
    public bool CrossesAntimeridian => West > East;

    /// <summary>Содержит ли область точку</summary>
    /// <param name="point">Точка</param>
    public bool Contains(GeoPoint point)
    {
        if (point.Latitude < South || point.Latitude > North)
            return false;

        return CrossesAntimeridian
            ? point.Longitude >= West || point.Longitude <= East
            : point.Longitude >= West && point.Longitude <= East;
    }

    /// <summary>Область вокруг точки заданного радиуса</summary>
    /// <param name="centre">Центр</param>
    /// <param name="radius">Радиус</param>
    public static GeoBounds Around(GeoPoint centre, Quantity radius)
    {
        double metres = radius.RequireSi(Dimension.LengthDim, nameof(radius));
        double latitudeSpan = metres / 111320.0;

        double cosine = Math.Cos(centre.LatitudeRadians);
        double longitudeSpan = Math.Abs(cosine) < 1e-9 ? 180 : metres / (111320.0 * cosine);

        return new GeoBounds(
            centre.Latitude - latitudeSpan,
            GeoPoint.Normalize(centre.Longitude - longitudeSpan),
            centre.Latitude + latitudeSpan,
            GeoPoint.Normalize(centre.Longitude + longitudeSpan));
    }
}

/// <summary>
/// Сеточный указатель точек на поверхности Земли.
/// </summary>
/// <remarks>
/// <para>
/// Точки раскладываются по ячейкам равномерной сетки в градусах, и поиск в радиусе
/// просматривает лишь ячейки, накрытые рамкой. На равномерно рассеянных данных это
/// сокращает перебор в сотни раз; на данных, собранных в одном городе, выигрыш пропадает —
/// там нужно дерево с адаптивным разбиением.
/// </para>
/// <para>
/// Ячейки задаются в градусах, поэтому у полюсов они сильно уже по расстоянию, чем у экватора.
/// Отбор кандидатов от этого не портится: расстояние всё равно проверяется точно.
/// </para>
/// </remarks>
/// <typeparam name="T">Тип связанных с точкой данных</typeparam>
public sealed class GeoIndex<T>
{
    private readonly Dictionary<(int Row, int Column), List<(GeoPoint Point, T Value)>> _cells = [];
    private readonly double _cellSize;
    private int _count;

    /// <summary>Создаёт указатель</summary>
    /// <param name="cellSizeDegrees">Размер ячейки в градусах</param>
    public GeoIndex(double cellSizeDegrees = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSizeDegrees);
        _cellSize = cellSizeDegrees;
    }

    /// <summary>Число точек в указателе</summary>
    public int Count => _count;

    /// <summary>Добавляет точку</summary>
    /// <param name="point">Координаты</param>
    /// <param name="value">Связанные данные</param>
    public void Add(GeoPoint point, T value)
    {
        (int Row, int Column) key = Cell(point);

        if (!_cells.TryGetValue(key, out List<(GeoPoint, T)>? bucket))
        {
            bucket = [];
            _cells[key] = bucket;
        }

        bucket.Add((point, value));
        _count++;
    }

    /// <summary>Точки внутри области</summary>
    /// <param name="bounds">Область</param>
    public IEnumerable<(GeoPoint Point, T Value)> Within(GeoBounds bounds)
    {
        foreach (KeyValuePair<(int Row, int Column), List<(GeoPoint Point, T Value)>> cell in _cells)
        {
            foreach ((GeoPoint point, T value) in cell.Value)
            {
                if (bounds.Contains(point))
                    yield return (point, value);
            }
        }
    }

    /// <summary>
    /// Точки в круге заданного радиуса вокруг центра
    /// </summary>
    /// <param name="centre">Центр</param>
    /// <param name="radius">Радиус</param>
    public IReadOnlyList<(GeoPoint Point, T Value, Quantity Distance)> WithinRadius(GeoPoint centre, Quantity radius)
    {
        double limit = radius.RequireSi(Dimension.LengthDim, nameof(radius));
        GeoBounds bounds = GeoBounds.Around(centre, radius);

        var found = new List<(GeoPoint, T, Quantity)>();

        foreach ((GeoPoint point, T value) in Within(bounds))
        {
            Quantity distance = Geodesy.Geodesy.GreatCircleDistance(centre, point);

            if (distance.SiValue <= limit)
                found.Add((point, value, distance));
        }

        found.Sort((left, right) => left.Item3.SiValue.CompareTo(right.Item3.SiValue));

        return found;
    }

    /// <summary>
    /// Ближайшие к точке объекты
    /// </summary>
    /// <param name="centre">Центр поиска</param>
    /// <param name="count">Сколько объектов вернуть</param>
    /// <remarks>
    /// Радиус поиска расширяется вдвое, пока не наберётся нужное число объектов: так
    /// перебор остаётся ограниченным, если объекты рядом, и всё же завершается, если их мало.
    /// </remarks>
    public IReadOnlyList<(GeoPoint Point, T Value, Quantity Distance)> Nearest(GeoPoint centre, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        double radius = _cellSize * 111320.0;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            IReadOnlyList<(GeoPoint Point, T Value, Quantity Distance)> found =
                WithinRadius(centre, new Quantity(radius, Dimension.LengthDim));

            if (found.Count >= count)
                return found.Take(count).ToList();

            radius *= 2;
        }

        // Радиус вырос до половины окружности планеты: перебираем всё, что есть
        var all = new List<(GeoPoint Point, T Value, Quantity Distance)>();

        foreach (KeyValuePair<(int Row, int Column), List<(GeoPoint Point, T Value)>> cell in _cells)
            foreach ((GeoPoint point, T value) in cell.Value)
                all.Add((point, value, Geodesy.Geodesy.GreatCircleDistance(centre, point)));

        all.Sort((left, right) => left.Distance.SiValue.CompareTo(right.Distance.SiValue));

        return all.Take(count).ToList();
    }

    private (int Row, int Column) Cell(GeoPoint point)
        => ((int)Math.Floor(point.Latitude / _cellSize), (int)Math.Floor(point.Longitude / _cellSize));
}

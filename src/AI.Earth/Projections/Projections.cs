using AI.Earth.Geodesy;

namespace AI.Earth.Projections;

/// <summary>Точка на плоскости в метрах проекции</summary>
/// <param name="Easting">Координата на восток</param>
/// <param name="Northing">Координата на север</param>
public readonly record struct PlanarPoint(double Easting, double Northing);

/// <summary>Номер плитки веб-карты</summary>
/// <param name="X">Номер по горизонтали</param>
/// <param name="Y">Номер по вертикали</param>
/// <param name="Zoom">Уровень масштабирования</param>
public readonly record struct TileIndex(int X, int Y, int Zoom);

/// <summary>
/// Проекция Меркатора в варианте веб-карт.
/// </summary>
/// <remarks>
/// <para>
/// Земля считается шаром, а не эллипсоидом: строго говоря, это не проекция Меркатора,
/// а её упрощение, принятое ради простоты нарезки на плитки. Расхождение с эллипсоидальным
/// вариантом доходит до двадцати километров по северной координате — для картинки это
/// неважно, для геодезии недопустимо.
/// </para>
/// <para>
/// Полюса недостижимы: северная координата растёт до бесконечности, поэтому карта обрезается
/// широтой около 85.05°, при которой мир становится квадратным.
/// </para>
/// </remarks>
public static class WebMercator
{
    /// <summary>Предельная широта, при которой карта становится квадратной</summary>
    public const double MaxLatitude = 85.05112877980659;

    /// <summary>Радиус сферы, принятый в проекции</summary>
    public const double Radius = 6378137.0;

    /// <summary>Прямое преобразование в метры проекции</summary>
    /// <param name="point">Географическая точка</param>
    public static PlanarPoint Project(GeoPoint point)
    {
        double latitude = Math.Clamp(point.Latitude, -MaxLatitude, MaxLatitude) * Math.PI / 180;

        double easting = Radius * point.Longitude * Math.PI / 180;
        double northing = Radius * Math.Log(Math.Tan((Math.PI / 4) + (latitude / 2)));

        return new PlanarPoint(easting, northing);
    }

    /// <summary>Обратное преобразование в географические координаты</summary>
    /// <param name="point">Точка проекции</param>
    public static GeoPoint Unproject(PlanarPoint point)
    {
        double longitude = point.Easting / Radius * 180 / Math.PI;
        double latitude = ((2 * Math.Atan(Math.Exp(point.Northing / Radius))) - (Math.PI / 2)) * 180 / Math.PI;

        return new GeoPoint(latitude, GeoPoint.Normalize(longitude));
    }

    /// <summary>Номер плитки, содержащей точку</summary>
    /// <param name="point">Географическая точка</param>
    /// <param name="zoom">Уровень масштабирования</param>
    public static TileIndex Tile(GeoPoint point, int zoom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(zoom);

        double latitude = Math.Clamp(point.Latitude, -MaxLatitude, MaxLatitude) * Math.PI / 180;
        int count = 1 << zoom;

        int x = (int)Math.Floor((point.Longitude + 180) / 360 * count);
        int y = (int)Math.Floor(
            (1 - (Math.Log(Math.Tan(latitude) + (1 / Math.Cos(latitude))) / Math.PI)) / 2 * count);

        return new TileIndex(Math.Clamp(x, 0, count - 1), Math.Clamp(y, 0, count - 1), zoom);
    }

    /// <summary>Северо-западный угол плитки</summary>
    /// <param name="tile">Номер плитки</param>
    public static GeoPoint TileCorner(TileIndex tile)
    {
        int count = 1 << tile.Zoom;

        double longitude = ((double)tile.X / count * 360) - 180;
        double n = Math.PI - (2 * Math.PI * tile.Y / count);
        double latitude = 180 / Math.PI * Math.Atan(Math.Sinh(n));

        return new GeoPoint(latitude, longitude);
    }

    /// <summary>
    /// Размер пикселя на местности при заданной широте и масштабе
    /// </summary>
    /// <param name="latitude">Широта в градусах</param>
    /// <param name="zoom">Уровень масштабирования</param>
    /// <param name="tileSize">Размер плитки в точках</param>
    /// <remarks>
    /// Множитель косинуса широты — источник главного заблуждения о картах: у полярных
    /// областей один и тот же экранный пиксель покрывает во много раз меньшую площадь,
    /// поэтому Гренландия и выглядит с Африку.
    /// </remarks>
    public static double GroundResolution(double latitude, int zoom, int tileSize = 256)
        => Math.Cos(latitude * Math.PI / 180) * 2 * Math.PI * Radius / (tileSize * (1 << zoom));
}

/// <summary>Координаты в проекции Гаусса — Крюгера с номером зоны</summary>
/// <param name="Zone">Номер зоны</param>
/// <param name="IsNorthern">Северное ли полушарие</param>
/// <param name="Easting">Восточная координата, метры</param>
/// <param name="Northing">Северная координата, метры</param>
public readonly record struct UtmPoint(int Zone, bool IsNorthern, double Easting, double Northing)
{
    /// <summary>Запись координат с номером зоны</summary>
    public override string ToString()
        => $"{Zone}{(IsNorthern ? 'N' : 'S')} {Easting:F1} {Northing:F1}";
}

/// <summary>
/// Универсальная поперечная проекция Меркатора.
/// </summary>
/// <remarks>
/// <para>
/// Земля делится на шестьдесят зон по шесть градусов долготы, и в каждой строится своя
/// поперечная проекция. Внутри зоны искажения не превышают одной тысячной, но координаты
/// из разных зон несравнимы напрямую — это плата за точность.
/// </para>
/// <para>
/// Ряды разложения оборваны на четвёртом порядке: точность около миллиметра в пределах зоны
/// и быстрое ухудшение за её границами. Для точек далеко за пределами своей зоны нужен
/// другой подход.
/// </para>
/// </remarks>
public static class Utm
{
    private const double ScaleFactor = 0.9996;
    private const double FalseEasting = 500000.0;
    private const double FalseNorthing = 10000000.0;

    /// <summary>Номер зоны для заданной долготы</summary>
    /// <param name="longitude">Долгота в градусах</param>
    public static int ZoneFor(double longitude)
        => (int)Math.Floor((GeoPoint.Normalize(longitude) + 180) / 6) + 1;

    /// <summary>Осевой меридиан зоны</summary>
    /// <param name="zone">Номер зоны</param>
    public static double CentralMeridian(int zone) => ((zone - 1) * 6) - 180 + 3;

    /// <summary>Прямое преобразование географических координат</summary>
    /// <param name="point">Точка</param>
    /// <param name="ellipsoid">Референц-эллипсоид</param>
    public static UtmPoint Project(GeoPoint point, Ellipsoid ellipsoid = default)
    {
        Ellipsoid model = ellipsoid.SemiMajorAxis <= 0 ? Ellipsoid.Wgs84 : ellipsoid;

        int zone = ZoneFor(point.Longitude);
        double lambda0 = CentralMeridian(zone) * Math.PI / 180;

        double a = model.SemiMajorAxis;
        double e2 = model.EccentricitySquared;
        double ep2 = e2 / (1 - e2);

        double phi = point.LatitudeRadians;
        double lambda = point.LongitudeRadians;

        double sinPhi = Math.Sin(phi), cosPhi = Math.Cos(phi), tanPhi = Math.Tan(phi);

        double n = a / Math.Sqrt(1 - (e2 * sinPhi * sinPhi));
        double t = tanPhi * tanPhi;
        double c = ep2 * cosPhi * cosPhi;
        double a1 = (lambda - lambda0) * cosPhi;

        double m = a * (((1 - (e2 / 4) - (3 * e2 * e2 / 64) - (5 * e2 * e2 * e2 / 256)) * phi)
            - (((3 * e2 / 8) + (3 * e2 * e2 / 32) + (45 * e2 * e2 * e2 / 1024)) * Math.Sin(2 * phi))
            + ((((15 * e2 * e2) / 256) + (45 * e2 * e2 * e2 / 1024)) * Math.Sin(4 * phi))
            - ((35 * e2 * e2 * e2 / 3072) * Math.Sin(6 * phi)));

        double easting = (ScaleFactor * n * (a1
            + ((1 - t + c) * a1 * a1 * a1 / 6)
            + ((5 - (18 * t) + (t * t) + (72 * c) - (58 * ep2)) * Math.Pow(a1, 5) / 120))) + FalseEasting;

        double northing = ScaleFactor * (m + (n * tanPhi * ((a1 * a1 / 2)
            + ((5 - t + (9 * c) + (4 * c * c)) * Math.Pow(a1, 4) / 24)
            + ((61 - (58 * t) + (t * t) + (600 * c) - (330 * ep2)) * Math.Pow(a1, 6) / 720))));

        bool northern = point.Latitude >= 0;

        return new UtmPoint(zone, northern, easting, northern ? northing : northing + FalseNorthing);
    }

    /// <summary>Обратное преобразование в географические координаты</summary>
    /// <param name="point">Точка проекции</param>
    /// <param name="ellipsoid">Референц-эллипсоид</param>
    public static GeoPoint Unproject(UtmPoint point, Ellipsoid ellipsoid = default)
    {
        Ellipsoid model = ellipsoid.SemiMajorAxis <= 0 ? Ellipsoid.Wgs84 : ellipsoid;

        double a = model.SemiMajorAxis;
        double e2 = model.EccentricitySquared;
        double ep2 = e2 / (1 - e2);
        double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

        double x = point.Easting - FalseEasting;
        double y = point.IsNorthern ? point.Northing : point.Northing - FalseNorthing;

        double m = y / ScaleFactor;
        double mu = m / (a * (1 - (e2 / 4) - (3 * e2 * e2 / 64) - (5 * e2 * e2 * e2 / 256)));

        double phi1 = mu
            + ((((3 * e1) / 2) - (27 * e1 * e1 * e1 / 32)) * Math.Sin(2 * mu))
            + ((((21 * e1 * e1) / 16) - (55 * Math.Pow(e1, 4) / 32)) * Math.Sin(4 * mu))
            + ((151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu))
            + ((1097 * Math.Pow(e1, 4) / 512) * Math.Sin(8 * mu));

        double sinPhi1 = Math.Sin(phi1), cosPhi1 = Math.Cos(phi1), tanPhi1 = Math.Tan(phi1);

        double c1 = ep2 * cosPhi1 * cosPhi1;
        double t1 = tanPhi1 * tanPhi1;
        double n1 = a / Math.Sqrt(1 - (e2 * sinPhi1 * sinPhi1));
        double r1 = a * (1 - e2) / Math.Pow(1 - (e2 * sinPhi1 * sinPhi1), 1.5);
        double d = x / (n1 * ScaleFactor);

        double latitude = phi1 - (n1 * tanPhi1 / r1 * ((d * d / 2)
            - ((5 + (3 * t1) + (10 * c1) - (4 * c1 * c1) - (9 * ep2)) * Math.Pow(d, 4) / 24)
            + ((61 + (90 * t1) + (298 * c1) + (45 * t1 * t1) - (252 * ep2) - (3 * c1 * c1)) * Math.Pow(d, 6) / 720)));

        double longitude = (d
            - ((1 + (2 * t1) + c1) * d * d * d / 6)
            + ((5 - (2 * c1) + (28 * t1) - (3 * c1 * c1) + (8 * ep2) + (24 * t1 * t1)) * Math.Pow(d, 5) / 120)) / cosPhi1;

        return new GeoPoint(
            latitude * 180 / Math.PI,
            GeoPoint.Normalize((CentralMeridian(point.Zone) * Math.PI / 180 + longitude) * 180 / Math.PI));
    }
}

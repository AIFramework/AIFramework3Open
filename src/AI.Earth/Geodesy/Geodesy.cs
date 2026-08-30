using AI.Geometry.Primitives;
using AI.Units;

namespace AI.Earth.Geodesy;

/// <summary>
/// Референц-эллипсоид: форма Земли, к которой привязаны координаты
/// </summary>
/// <param name="Name">Название</param>
/// <param name="SemiMajorAxis">Большая полуось, метры</param>
/// <param name="Flattening">Сжатие</param>
public readonly record struct Ellipsoid(string Name, double SemiMajorAxis, double Flattening)
{
    /// <summary>Эллипсоид WGS 84 — тот, в котором работают спутниковые навигационные системы</summary>
    public static Ellipsoid Wgs84 => new("WGS 84", 6378137.0, 1.0 / 298.257223563);

    /// <summary>Эллипсоид Красовского, положенный в основу системы координат 1942 года</summary>
    public static Ellipsoid Krasovsky => new("Красовский 1940", 6378245.0, 1.0 / 298.3);

    /// <summary>Сфера того же объёма — для быстрых расчётов, где точность эллипсоида избыточна</summary>
    public static Ellipsoid Sphere => new("Сфера", 6371008.8, 0.0);

    /// <summary>Малая полуось, метры</summary>
    public double SemiMinorAxis => SemiMajorAxis * (1 - Flattening);

    /// <summary>Квадрат первого эксцентриситета</summary>
    public double EccentricitySquared => Flattening * (2 - Flattening);
}

/// <summary>
/// Точка на поверхности Земли в географических координатах
/// </summary>
/// <param name="Latitude">Широта в градусах, положительная к северу</param>
/// <param name="Longitude">Долгота в градусах, положительная к востоку</param>
/// <param name="Height">Высота над эллипсоидом, метры</param>
public readonly record struct GeoPoint(double Latitude, double Longitude, double Height = 0)
{
    /// <summary>Проверяет, что координаты лежат в допустимых пределах</summary>
    /// <exception cref="ArgumentOutOfRangeException">Широта вне отрезка от −90 до 90</exception>
    public GeoPoint Validated()
    {
        if (Latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(Latitude), "Широта лежит между −90 и 90 градусами");

        return this with { Longitude = Normalize(Longitude) };
    }

    /// <summary>Приводит долготу к отрезку от −180 до 180</summary>
    /// <param name="longitude">Долгота в градусах</param>
    public static double Normalize(double longitude)
    {
        double value = (longitude + 180) % 360;

        return (value < 0 ? value + 360 : value) - 180;
    }

    /// <summary>Широта как безразмерная величина в радианах</summary>
    public double LatitudeRadians => Latitude * Math.PI / 180;

    /// <summary>Долгота как безразмерная величина в радианах</summary>
    public double LongitudeRadians => Longitude * Math.PI / 180;

    /// <summary>Запись координат в градусах</summary>
    public override string ToString()
        => $"{Math.Abs(Latitude):F5}° {(Latitude >= 0 ? 'N' : 'S')}, {Math.Abs(Longitude):F5}° {(Longitude >= 0 ? 'E' : 'W')}";
}

/// <summary>
/// Расстояния, направления и преобразования координат на Земле.
/// </summary>
/// <remarks>
/// <para>
/// Расстояние по большому кругу (гаверсинус) считает Землю шаром и ошибается до половины
/// процента — на тысяче километров это пять километров. Метод Винсенти работает на эллипсоиде
/// и даёт доли миллиметра, но требует итераций и в редких случаях почти противоположных точек
/// сходится плохо. Выбор между ними — это выбор между скоростью и точностью, и делать его
/// должен вызывающий, а не библиотека молча.
/// </para>
/// </remarks>
public static class Geodesy
{
    private const double Degree = Math.PI / 180.0;

    /// <summary>
    /// Расстояние по большому кругу на сфере (формула гаверсинуса)
    /// </summary>
    /// <param name="from">Начальная точка</param>
    /// <param name="to">Конечная точка</param>
    /// <param name="radius">Радиус сферы, метры</param>
    public static Quantity GreatCircleDistance(GeoPoint from, GeoPoint to, double radius = 6371008.8)
    {
        double dLat = (to.Latitude - from.Latitude) * Degree;
        double dLon = (to.Longitude - from.Longitude) * Degree;

        double sinLat = Math.Sin(dLat / 2);
        double sinLon = Math.Sin(dLon / 2);

        double a = (sinLat * sinLat)
            + (Math.Cos(from.LatitudeRadians) * Math.Cos(to.LatitudeRadians) * sinLon * sinLon);

        return new Quantity(2 * radius * Math.Asin(Math.Min(1, Math.Sqrt(a))), Dimension.LengthDim);
    }

    /// <summary>
    /// Расстояние на эллипсоиде обратным методом Винсенти
    /// </summary>
    /// <param name="from">Начальная точка</param>
    /// <param name="to">Конечная точка</param>
    /// <param name="ellipsoid">Референц-эллипсоид</param>
    /// <param name="tolerance">Порог сходимости по разности долгот</param>
    /// <exception cref="InvalidOperationException">Итерации не сошлись: точки почти противоположны</exception>
    public static Quantity EllipsoidDistance(
        GeoPoint from, GeoPoint to, Ellipsoid ellipsoid = default, double tolerance = 1e-12)
    {
        Ellipsoid model = ellipsoid.SemiMajorAxis <= 0 ? Ellipsoid.Wgs84 : ellipsoid;

        double a = model.SemiMajorAxis;
        double f = model.Flattening;
        double b = model.SemiMinorAxis;

        double u1 = Math.Atan((1 - f) * Math.Tan(from.LatitudeRadians));
        double u2 = Math.Atan((1 - f) * Math.Tan(to.LatitudeRadians));
        double l = (to.Longitude - from.Longitude) * Degree;

        double sinU1 = Math.Sin(u1), cosU1 = Math.Cos(u1);
        double sinU2 = Math.Sin(u2), cosU2 = Math.Cos(u2);

        double lambda = l;
        double sinSigma = 0, cosSigma = 0, sigma = 0, cos2SigmaM = 0, cosSquaredAlpha = 0;

        for (int iteration = 0; iteration < 200; iteration++)
        {
            double sinLambda = Math.Sin(lambda), cosLambda = Math.Cos(lambda);

            sinSigma = Math.Sqrt(
                ((cosU2 * sinLambda) * (cosU2 * sinLambda))
                + (((cosU1 * sinU2) - (sinU1 * cosU2 * cosLambda)) * ((cosU1 * sinU2) - (sinU1 * cosU2 * cosLambda))));

            if (sinSigma == 0)
                return new Quantity(0, Dimension.LengthDim);

            cosSigma = (sinU1 * sinU2) + (cosU1 * cosU2 * cosLambda);
            sigma = Math.Atan2(sinSigma, cosSigma);

            double sinAlpha = cosU1 * cosU2 * sinLambda / sinSigma;
            cosSquaredAlpha = 1 - (sinAlpha * sinAlpha);
            cos2SigmaM = cosSquaredAlpha == 0 ? 0 : cosSigma - (2 * sinU1 * sinU2 / cosSquaredAlpha);

            double c = f / 16 * cosSquaredAlpha * (4 + (f * (4 - (3 * cosSquaredAlpha))));
            double previous = lambda;

            lambda = l + ((1 - c) * f * sinAlpha
                * (sigma + (c * sinSigma * (cos2SigmaM + (c * cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))))));

            if (Math.Abs(lambda - previous) < tolerance)
            {
                double uSquared = cosSquaredAlpha * ((a * a) - (b * b)) / (b * b);
                double aCoefficient = 1 + (uSquared / 16384 * (4096 + (uSquared * (-768 + (uSquared * (320 - (175 * uSquared)))))));
                double bCoefficient = uSquared / 1024 * (256 + (uSquared * (-128 + (uSquared * (74 - (47 * uSquared))))));

                double deltaSigma = bCoefficient * sinSigma
                    * (cos2SigmaM + (bCoefficient / 4
                        * ((cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))
                            - (bCoefficient / 6 * cos2SigmaM * (-3 + (4 * sinSigma * sinSigma)) * (-3 + (4 * cos2SigmaM * cos2SigmaM))))));

                return new Quantity(b * aCoefficient * (sigma - deltaSigma), Dimension.LengthDim);
            }
        }

        throw new InvalidOperationException(
            "Метод Винсенти не сошёлся: точки почти противоположны. Для таких пар пользуйтесь расстоянием по большому кругу.");
    }

    /// <summary>
    /// Начальный азимут пути по большому кругу, градусы от севера по часовой стрелке
    /// </summary>
    /// <param name="from">Начальная точка</param>
    /// <param name="to">Конечная точка</param>
    /// <remarks>
    /// Азимут вдоль большого круга непрерывно меняется: у прибытия он иной, чем на старте.
    /// Постоянный курс даёт локсодрома, которая длиннее.
    /// </remarks>
    public static double InitialBearing(GeoPoint from, GeoPoint to)
    {
        double dLon = (to.Longitude - from.Longitude) * Degree;

        double y = Math.Sin(dLon) * Math.Cos(to.LatitudeRadians);
        double x = (Math.Cos(from.LatitudeRadians) * Math.Sin(to.LatitudeRadians))
            - (Math.Sin(from.LatitudeRadians) * Math.Cos(to.LatitudeRadians) * Math.Cos(dLon));

        double bearing = Math.Atan2(y, x) / Degree;

        return (bearing + 360) % 360;
    }

    /// <summary>
    /// Точка, отстоящая на заданное расстояние в заданном направлении по большому кругу
    /// </summary>
    /// <param name="from">Начальная точка</param>
    /// <param name="bearingDegrees">Азимут в градусах</param>
    /// <param name="distance">Расстояние</param>
    /// <param name="radius">Радиус сферы, метры</param>
    public static GeoPoint Destination(GeoPoint from, double bearingDegrees, Quantity distance, double radius = 6371008.8)
    {
        double angular = distance.RequireSi(Dimension.LengthDim, nameof(distance)) / radius;
        double bearing = bearingDegrees * Degree;

        double latitude = Math.Asin((Math.Sin(from.LatitudeRadians) * Math.Cos(angular))
            + (Math.Cos(from.LatitudeRadians) * Math.Sin(angular) * Math.Cos(bearing)));

        double longitude = from.LongitudeRadians + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(from.LatitudeRadians),
            Math.Cos(angular) - (Math.Sin(from.LatitudeRadians) * Math.Sin(latitude)));

        return new GeoPoint(latitude / Degree, GeoPoint.Normalize(longitude / Degree), from.Height);
    }

    /// <summary>
    /// Перевод географических координат в геоцентрические прямоугольные
    /// </summary>
    /// <param name="point">Точка</param>
    /// <param name="ellipsoid">Референц-эллипсоид</param>
    public static Vector3 ToEarthCentred(GeoPoint point, Ellipsoid ellipsoid = default)
    {
        Ellipsoid model = ellipsoid.SemiMajorAxis <= 0 ? Ellipsoid.Wgs84 : ellipsoid;

        double sinLat = Math.Sin(point.LatitudeRadians);
        double cosLat = Math.Cos(point.LatitudeRadians);

        double curvature = model.SemiMajorAxis / Math.Sqrt(1 - (model.EccentricitySquared * sinLat * sinLat));

        double x = (curvature + point.Height) * cosLat * Math.Cos(point.LongitudeRadians);
        double y = (curvature + point.Height) * cosLat * Math.Sin(point.LongitudeRadians);
        double z = ((curvature * (1 - model.EccentricitySquared)) + point.Height) * sinLat;

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Перевод геоцентрических прямоугольных координат в географические
    /// </summary>
    /// <param name="position">Положение в геоцентрической системе</param>
    /// <param name="ellipsoid">Референц-эллипсоид</param>
    /// <remarks>
    /// Обратная задача решается итерациями по широте: замкнутого выражения у неё нет,
    /// а приближённые формулы теряют точность на больших высотах.
    /// </remarks>
    public static GeoPoint ToGeographic(Vector3 position, Ellipsoid ellipsoid = default)
    {
        Ellipsoid model = ellipsoid.SemiMajorAxis <= 0 ? Ellipsoid.Wgs84 : ellipsoid;

        double longitude = Math.Atan2(position.Y, position.X);
        double planar = Math.Sqrt((position.X * position.X) + (position.Y * position.Y));

        double latitude = Math.Atan2(position.Z, planar * (1 - model.EccentricitySquared));
        double height = 0;

        for (int iteration = 0; iteration < 100; iteration++)
        {
            double sinLat = Math.Sin(latitude);
            double curvature = model.SemiMajorAxis / Math.Sqrt(1 - (model.EccentricitySquared * sinLat * sinLat));

            height = (planar / Math.Cos(latitude)) - curvature;

            double updated = Math.Atan2(
                position.Z,
                planar * (1 - (model.EccentricitySquared * curvature / (curvature + height))));

            if (Math.Abs(updated - latitude) < 1e-14)
            {
                latitude = updated;
                break;
            }

            latitude = updated;
        }

        return new GeoPoint(latitude / Degree, GeoPoint.Normalize(longitude / Degree), height);
    }
}

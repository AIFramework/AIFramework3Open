using AI.Earth.Astronomy;
using AI.Earth.Geodesy;
using AI.Earth.Projections;
using AI.Earth.Spatial;
using AI.Geometry.Primitives;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Науки о Земле и астрономия проверяются известными расстояниями между городами,
/// обратимостью преобразований координат и справочными астрономическими датами.
/// </summary>
public class EarthTests
{
    private static readonly GeoPoint Moscow = new(55.7558, 37.6173);
    private static readonly GeoPoint SaintPetersburg = new(59.9311, 30.3609);
    private static readonly GeoPoint Novosibirsk = new(55.0084, 82.9357);

    #region Геодезия

    [Fact]
    public void Distance_MoscowToSaintPetersburg_MatchesKnownValue()
    {
        Quantity distance = Geodesy.GreatCircleDistance(Moscow, SaintPetersburg);

        // Справочное расстояние по прямой — около 635 км
        Assert.Equal(634, distance.In(UnitRegistry.Parse("km")), tolerance: 3);
    }

    [Fact]
    public void Distance_EllipsoidAndSphere_AgreeWithinHalfPercent()
    {
        Quantity sphere = Geodesy.GreatCircleDistance(Moscow, Novosibirsk);
        Quantity ellipsoid = Geodesy.EllipsoidDistance(Moscow, Novosibirsk);

        double difference = Math.Abs(sphere.SiValue - ellipsoid.SiValue) / ellipsoid.SiValue;

        // Сферическое приближение ошибается на доли процента — и это его известная цена
        Assert.True(difference < 0.005, $"Расхождение {difference:P2} больше ожидаемого");
        Assert.True(ellipsoid.In(UnitRegistry.Parse("km")) is > 2800 and < 2900);
    }

    [Fact]
    public void Distance_SamePoint_IsZero()
    {
        Assert.Equal(0.0, Geodesy.GreatCircleDistance(Moscow, Moscow).SiValue, tolerance: 1e-9);
        Assert.Equal(0.0, Geodesy.EllipsoidDistance(Moscow, Moscow).SiValue, tolerance: 1e-6);
    }

    [Fact]
    public void Distance_QuarterOfEquator_MatchesCircumference()
    {
        var origin = new GeoPoint(0, 0);
        var quarter = new GeoPoint(0, 90);

        Quantity distance = Geodesy.GreatCircleDistance(origin, quarter);

        // Четверть окружности Земли — около 10 007 км
        Assert.Equal(10007, distance.In(UnitRegistry.Parse("km")), tolerance: 5);
    }

    [Fact]
    public void Bearing_DueEast_IsNinetyDegrees()
    {
        var origin = new GeoPoint(0, 0);
        var east = new GeoPoint(0, 10);

        Assert.Equal(90.0, Geodesy.InitialBearing(origin, east), tolerance: 1e-9);
        Assert.Equal(0.0, Geodesy.InitialBearing(origin, new GeoPoint(10, 0)), tolerance: 1e-9);
    }

    [Fact]
    public void Destination_RoundTrips_WithDistanceAndBearing()
    {
        Quantity distance = Quantity.Of(500, "km");
        double bearing = 47.0;

        GeoPoint arrival = Geodesy.Destination(Moscow, bearing, distance);

        Assert.Equal(distance.SiValue, Geodesy.GreatCircleDistance(Moscow, arrival).SiValue, tolerance: 1);
        Assert.Equal(bearing, Geodesy.InitialBearing(Moscow, arrival), tolerance: 1e-6);
    }

    [Fact]
    public void EarthCentred_RoundTripsThroughGeographic()
    {
        var point = new GeoPoint(55.7558, 37.6173, 200);

        Vector3 cartesian = Geodesy.ToEarthCentred(point);
        GeoPoint restored = Geodesy.ToGeographic(cartesian);

        Assert.Equal(point.Latitude, restored.Latitude, tolerance: 1e-9);
        Assert.Equal(point.Longitude, restored.Longitude, tolerance: 1e-9);
        Assert.Equal(point.Height, restored.Height, tolerance: 1e-6);
    }

    [Fact]
    public void EarthCentred_NorthPole_LiesOnAxis()
    {
        Vector3 pole = Geodesy.ToEarthCentred(new GeoPoint(90, 0));

        Assert.Equal(0.0, pole.X, tolerance: 1e-6);
        Assert.Equal(0.0, pole.Y, tolerance: 1e-6);
        Assert.Equal(Ellipsoid.Wgs84.SemiMinorAxis, pole.Z, tolerance: 1e-6);
    }

    #endregion

    #region Проекции

    [Fact]
    public void WebMercator_RoundTrips()
    {
        PlanarPoint projected = WebMercator.Project(Moscow);
        GeoPoint restored = WebMercator.Unproject(projected);

        Assert.Equal(Moscow.Latitude, restored.Latitude, tolerance: 1e-9);
        Assert.Equal(Moscow.Longitude, restored.Longitude, tolerance: 1e-9);
    }

    [Fact]
    public void WebMercator_EquatorMapsToZero()
    {
        PlanarPoint origin = WebMercator.Project(new GeoPoint(0, 0));

        Assert.Equal(0.0, origin.Easting, tolerance: 1e-9);
        Assert.Equal(0.0, origin.Northing, tolerance: 1e-9);
    }

    [Fact]
    public void WebMercator_TileAtZoomZero_IsSingle()
    {
        TileIndex tile = WebMercator.Tile(Moscow, 0);

        Assert.Equal(0, tile.X);
        Assert.Equal(0, tile.Y);
    }

    [Fact]
    public void WebMercator_TileCorner_ContainsItsPoint()
    {
        TileIndex tile = WebMercator.Tile(Moscow, 10);
        GeoPoint corner = WebMercator.TileCorner(tile);

        // Северо-западный угол плитки лежит севернее и западнее самой точки
        Assert.True(corner.Latitude >= Moscow.Latitude);
        Assert.True(corner.Longitude <= Moscow.Longitude);
    }

    [Fact]
    public void WebMercator_GroundResolution_ShrinksTowardsPoles()
    {
        double equator = WebMercator.GroundResolution(0, 10);
        double moscow = WebMercator.GroundResolution(55.75, 10);

        // Косинус широты — та самая причина, по которой Гренландия кажется огромной
        Assert.True(moscow < equator);
        Assert.Equal(Math.Cos(55.75 * Math.PI / 180), moscow / equator, tolerance: 1e-12);
    }

    [Fact]
    public void Utm_ZoneNumbers_MatchReference()
    {
        Assert.Equal(37, Utm.ZoneFor(37.6173));    // Москва
        Assert.Equal(31, Utm.ZoneFor(2.3522));     // Париж
        Assert.Equal(1, Utm.ZoneFor(-177));
        Assert.Equal(60, Utm.ZoneFor(177));
    }

    [Fact]
    public void Utm_RoundTrips()
    {
        foreach (GeoPoint point in new[] { Moscow, SaintPetersburg, Novosibirsk, new GeoPoint(-33.8688, 151.2093) })
        {
            UtmPoint projected = Utm.Project(point);
            GeoPoint restored = Utm.Unproject(projected);

            Assert.Equal(point.Latitude, restored.Latitude, tolerance: 1e-7);
            Assert.Equal(point.Longitude, restored.Longitude, tolerance: 1e-7);
        }
    }

    [Fact]
    public void Utm_CentralMeridian_HasFalseEastingExactly()
    {
        var onMeridian = new GeoPoint(50, Utm.CentralMeridian(37));
        UtmPoint projected = Utm.Project(onMeridian);

        // На осевом меридиане восточная координата равна условному началу
        Assert.Equal(500000.0, projected.Easting, tolerance: 1e-6);
    }

    [Fact]
    public void Utm_SouthernHemisphere_UsesFalseNorthing()
    {
        UtmPoint sydney = Utm.Project(new GeoPoint(-33.8688, 151.2093));

        Assert.False(sydney.IsNorthern);
        Assert.True(sydney.Northing is > 6_000_000 and < 10_000_000);
    }

    #endregion

    #region Время

    [Fact]
    public void JulianDate_J2000_MatchesReference()
    {
        var epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(AstronomicalTime.J2000, AstronomicalTime.JulianDate(epoch), tolerance: 1e-9);
    }

    [Fact]
    public void JulianDate_KnownDates_MatchReference()
    {
        Assert.Equal(2440587.5,
            AstronomicalTime.JulianDate(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), tolerance: 1e-9);

        Assert.Equal(2451544.5,
            AstronomicalTime.JulianDate(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), tolerance: 1e-9);
    }

    [Fact]
    public void JulianDate_RoundTripsThroughDateTime()
    {
        var moment = new DateTime(2024, 3, 15, 18, 42, 30, DateTimeKind.Utc);
        double julian = AstronomicalTime.JulianDate(moment);
        DateTime restored = AstronomicalTime.ToDateTime(julian);

        Assert.True(Math.Abs((moment - restored).TotalSeconds) < 1.5);
    }

    [Fact]
    public void ModifiedJulianDate_DiffersByFixedOffset()
    {
        var moment = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            AstronomicalTime.JulianDate(moment) - 2400000.5,
            AstronomicalTime.ModifiedJulianDate(moment),
            tolerance: 1e-9);
    }

    [Fact]
    public void SiderealTime_AdvancesFasterThanSolar()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        double first = AstronomicalTime.GreenwichSiderealTime(start);
        double afterDay = AstronomicalTime.GreenwichSiderealTime(start.AddDays(1));

        // За солнечные сутки звёздное время убегает примерно на градус
        double advance = ((afterDay - first) % 360 + 360) % 360;

        Assert.Equal(0.9856, advance, tolerance: 0.01);
    }

    [Fact]
    public void LocalSiderealTime_ShiftsWithLongitude()
    {
        var moment = new DateTime(2024, 5, 20, 3, 0, 0, DateTimeKind.Utc);

        double greenwich = AstronomicalTime.LocalSiderealTime(moment, 0);
        double moscow = AstronomicalTime.LocalSiderealTime(moment, 37.6173);

        Assert.Equal(37.6173, ((moscow - greenwich) % 360 + 360) % 360, tolerance: 1e-9);
    }

    #endregion

    #region Солнце

    [Fact]
    public void SolarDeclination_AtSolstices_ReachesObliquity()
    {
        double june = SolarPosition.Declination(new DateTime(2024, 6, 20, 20, 51, 0, DateTimeKind.Utc));
        double december = SolarPosition.Declination(new DateTime(2024, 12, 21, 9, 21, 0, DateTimeKind.Utc));

        // В солнцестояние склонение достигает наклона земной оси
        Assert.Equal(23.44, june, tolerance: 0.05);
        Assert.Equal(-23.44, december, tolerance: 0.05);
    }

    [Fact]
    public void SolarDeclination_AtEquinox_IsNearZero()
    {
        double march = SolarPosition.Declination(new DateTime(2024, 3, 20, 3, 6, 0, DateTimeKind.Utc));

        Assert.Equal(0.0, march, tolerance: 0.05);
    }

    [Fact]
    public void EquationOfTime_HasKnownExtremes()
    {
        double november = SolarPosition.EquationOfTime(new DateTime(2024, 11, 3, 12, 0, 0, DateTimeKind.Utc));
        double february = SolarPosition.EquationOfTime(new DateTime(2024, 2, 11, 12, 0, 0, DateTimeKind.Utc));

        // Солнечные часы спешат на четверть часа в ноябре и отстают на столько же в феврале
        Assert.Equal(16.4, november, tolerance: 0.5);
        Assert.Equal(-14.2, february, tolerance: 0.5);
    }

    [Fact]
    public void SunTimes_SummerSolsticeInMoscow_GivesLongDay()
    {
        SunTimes times = SolarPosition.Times(new DateTime(2024, 6, 21), Moscow);

        // В Москве в день солнцестояния день длится около семнадцати с половиной часов
        Assert.Equal(17.5, times.DayLengthHours, tolerance: 0.3);
        Assert.NotNull(times.Sunrise);
        Assert.NotNull(times.Sunset);
    }

    [Fact]
    public void SunTimes_WinterSolsticeInMoscow_GivesShortDay()
    {
        SunTimes times = SolarPosition.Times(new DateTime(2024, 12, 21), Moscow);

        Assert.Equal(7.0, times.DayLengthHours, tolerance: 0.3);
    }

    [Fact]
    public void SunTimes_EquatorHasTwelveHourDayYearRound()
    {
        var equator = new GeoPoint(0, 0);

        foreach (DateTime date in new[]
        {
            new DateTime(2024, 3, 20), new DateTime(2024, 6, 21),
            new DateTime(2024, 9, 22), new DateTime(2024, 12, 21),
        })
        {
            Assert.Equal(12.1, SolarPosition.Times(date, equator).DayLengthHours, tolerance: 0.2);
        }
    }

    [Fact]
    public void SunTimes_PolarDayAndNight_AreDetected()
    {
        var murmansk = new GeoPoint(68.9585, 33.0827);

        SunTimes summer = SolarPosition.Times(new DateTime(2024, 6, 21), murmansk);
        SunTimes winter = SolarPosition.Times(new DateTime(2024, 12, 21), murmansk);

        Assert.True(summer.IsPolarDay);
        Assert.Null(summer.Sunrise);
        Assert.True(winter.IsPolarNight);
        Assert.Equal(0.0, winter.DayLengthHours, tolerance: 1e-9);
    }

    [Fact]
    public void SolarPosition_AtNoon_IsHighestAndSouthward()
    {
        SunTimes times = SolarPosition.Times(new DateTime(2024, 6, 21), Moscow);
        HorizontalPosition noon = SolarPosition.Position(times.SolarNoon, Moscow);

        // В истинный полдень в северном полушарии Солнце на юге и выше всего
        Assert.True(noon.Altitude > 55);
        Assert.Equal(180, noon.Azimuth, tolerance: 2);
        Assert.True(Math.Abs(noon.HourAngle) < 0.5);
    }

    [Fact]
    public void SolarPosition_Midnight_IsBelowHorizonInMoscow()
    {
        var midnight = new DateTime(2024, 12, 21, 21, 0, 0, DateTimeKind.Utc);

        Assert.False(SolarPosition.Position(midnight, Moscow).IsAboveHorizon);
    }

    [Fact]
    public void SunTimes_CivilTwilight_LastsLongerThanDaylight()
    {
        var date = new DateTime(2024, 9, 15);

        SunTimes day = SolarPosition.Times(date, Moscow);
        SunTimes twilight = SolarPosition.Times(date, Moscow, horizonAngle: 6);

        Assert.True(twilight.DayLengthHours > day.DayLengthHours);
    }

    #endregion

    #region Луна

    [Fact]
    public void Moon_ReferenceEpoch_IsNewMoon()
    {
        DateTime epoch = AstronomicalTime.ToDateTime(Moon.ReferenceNewMoon);
        MoonState state = Moon.State(epoch);

        Assert.Equal(MoonPhaseName.New, state.Name);
        Assert.True(state.Illumination < 0.01);
    }

    [Fact]
    public void Moon_HalfCycleLater_IsFull()
    {
        DateTime epoch = AstronomicalTime.ToDateTime(Moon.ReferenceNewMoon);
        MoonState state = Moon.State(epoch.AddDays(Moon.SynodicMonth / 2));

        Assert.Equal(MoonPhaseName.Full, state.Name);
        Assert.True(state.Illumination > 0.99);
    }

    [Fact]
    public void Moon_KnownFullMoon_IsRecognised()
    {
        // Полнолуние 25 марта 2024 года
        MoonState state = Moon.State(new DateTime(2024, 3, 25, 7, 0, 0, DateTimeKind.Utc));

        Assert.True(state.Illumination > 0.98);
        Assert.Equal(MoonPhaseName.Full, state.Name);
    }

    [Fact]
    public void Moon_NextNewMoon_ComesAfterGivenMoment()
    {
        var moment = new DateTime(2024, 3, 25, 0, 0, 0, DateTimeKind.Utc);
        DateTime next = Moon.NextNewMoon(moment);

        Assert.True(next > moment);
        Assert.True((next - moment).TotalDays < Moon.SynodicMonth);
        Assert.True(Moon.State(next).Illumination < 0.01);
    }

    #endregion

    #region Пространственный указатель

    [Fact]
    public void GeoIndex_FindsPointsWithinRadius()
    {
        var index = new GeoIndex<string>(cellSizeDegrees: 1.0);

        index.Add(Moscow, "Москва");
        index.Add(SaintPetersburg, "Санкт-Петербург");
        index.Add(Novosibirsk, "Новосибирск");
        index.Add(new GeoPoint(56.1366, 40.3966), "Владимир");

        IReadOnlyList<(GeoPoint Point, string Value, Quantity Distance)> found =
            index.WithinRadius(Moscow, Quantity.Of(300, "km"));

        Assert.Equal(4, index.Count);
        Assert.Equal(2, found.Count);
        Assert.Equal("Москва", found[0].Value);
        Assert.Equal("Владимир", found[1].Value);
    }

    [Fact]
    public void GeoIndex_Nearest_ReturnsClosestFirst()
    {
        var index = new GeoIndex<string>();

        index.Add(SaintPetersburg, "Санкт-Петербург");
        index.Add(Novosibirsk, "Новосибирск");

        IReadOnlyList<(GeoPoint Point, string Value, Quantity Distance)> nearest = index.Nearest(Moscow, 2);

        Assert.Equal("Санкт-Петербург", nearest[0].Value);
        Assert.Equal("Новосибирск", nearest[1].Value);
        Assert.True(nearest[0].Distance.SiValue < nearest[1].Distance.SiValue);
    }

    [Fact]
    public void GeoBounds_HandlesAntimeridian()
    {
        var bounds = new GeoBounds(60, 170, 70, -170);

        Assert.True(bounds.CrossesAntimeridian);
        Assert.True(bounds.Contains(new GeoPoint(65, 179)));
        Assert.True(bounds.Contains(new GeoPoint(65, -179)));
        Assert.False(bounds.Contains(new GeoPoint(65, 0)));
    }

    [Fact]
    public void GeoBounds_Around_ContainsCentre()
    {
        GeoBounds bounds = GeoBounds.Around(Moscow, Quantity.Of(50, "km"));

        Assert.True(bounds.Contains(Moscow));
        Assert.False(bounds.Contains(SaintPetersburg));
    }

    #endregion

    #region Объяснимость

    [Fact]
    public void Interpret_SunTimes_ExplainsNoonShift()
    {
        var interpretation = SolarPosition.Times(new DateTime(2024, 5, 1), Moscow).Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Долгота дня");
        Assert.Contains(interpretation.Findings, f => f.Contains("уравнения времени", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("рефракцию", StringComparison.Ordinal));
    }

    #endregion
}

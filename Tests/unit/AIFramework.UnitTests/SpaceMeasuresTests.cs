using AI.DataStructs.Algebraic;
using AI.Earth.Geodesy;
using AI.Geometry.Curves;
using AI.Geometry.MassProperties;
using AI.MathUtils.Integration;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Меры пространств против замкнутых ответов: квадратура Гаусса-Кронрода, длина, площадь и кривизна плоской кривой,
/// объем симплекса любой размерности, площадь и углы сферических фигур.
/// </summary>
public class SpaceMeasuresTests
{
    private const double Radius = 6371008.8;

    #region Квадратура

    [Fact]
    public void GaussKronrod_Integrate_PolynomialIsExact()
    {
        Assert.Equal(1.0 / 6, GaussKronrod.Integrate(x => Math.Pow(x, 5), 0, 1), 14);
    }

    [Fact]
    public void GaussKronrod_Integrate_SineOverHalfPeriodIsTwo()
    {
        Assert.Equal(2, GaussKronrod.Integrate(Math.Sin, 0, Math.PI), 12);
    }

    [Fact]
    public void GaussKronrod_Integrate_ReversedLimitsChangeSign()
    {
        Assert.Equal(-2, GaussKronrod.Integrate(Math.Sin, Math.PI, 0), 12);
    }

    [Fact]
    public void GaussKronrod_Integrate_EndpointSingularityConverges()
    {
        // ∫₀¹ dx/√x = 2: в нуле функция бесконечна, но узлы лежат внутри отрезков
        Assert.Equal(2, GaussKronrod.Integrate(x => 1 / Math.Sqrt(x), 0, 1), 6);
    }

    #endregion

    #region Кривые

    [Fact]
    public void PlaneCurveMeasures_ArcLength_ParabolaMatchesClosedForm()
    {
        // y = x² на [0, 1]: (2√5 + arsh 2) / 4
        double expected = ((2 * Math.Sqrt(5)) + Math.Asinh(2)) / 4;

        double length = PlaneCurveMeasures.ArcLength(_ => 1, x => 2 * x, 0, 1);

        Assert.Equal(expected, length, 1e-9 * expected);
    }

    [Fact]
    public void PlaneCurveMeasures_ArcLength_CircleIsCircumference()
    {
        const double r = 2;

        double length = PlaneCurveMeasures.ArcLength(t => -r * Math.Sin(t), t => r * Math.Cos(t), 0, 2 * Math.PI);

        Assert.Equal(2 * Math.PI * r, length, 1e-10);
    }

    [Fact]
    public void PlaneCurveMeasures_SignedArea_CircleSignFollowsDirection()
    {
        const double r = 2;
        Func<double, double> x = t => r * Math.Cos(t), y = t => r * Math.Sin(t);
        Func<double, double> dx = t => -r * Math.Sin(t), dy = t => r * Math.Cos(t);

        double counterclockwise = PlaneCurveMeasures.SignedArea(x, y, dx, dy, 0, 2 * Math.PI);
        double clockwise = PlaneCurveMeasures.SignedArea(x, y, dx, dy, 2 * Math.PI, 0);

        Assert.Equal(Math.PI * r * r, counterclockwise, 1e-10);
        Assert.Equal(-Math.PI * r * r, clockwise, 1e-10);
    }

    [Fact]
    public void PlaneCurveMeasures_Curvature_CircleIsInverseRadius()
    {
        const double r = 2, t = 0.7;

        double curvature = PlaneCurveMeasures.Curvature(-r * Math.Sin(t), r * Math.Cos(t), -r * Math.Cos(t), -r * Math.Sin(t));

        Assert.Equal(1 / r, curvature, 12);
    }

    [Fact]
    public void PlaneCurveMeasures_Curvature_StationaryPointIsNaN()
    {
        Assert.True(double.IsNaN(PlaneCurveMeasures.Curvature(0, 0, 1, 1)));
    }

    #endregion

    #region Симплекс

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.0 / 2)]
    [InlineData(3, 1.0 / 6)]
    [InlineData(4, 1.0 / 24)]
    public void SimplexVolume_Volume_UnitCornerSimplexIsOneOverFactorial(int dimension, double expected)
    {
        var vertices = new List<Vector> { new(dimension) };
        for (int i = 0; i < dimension; i++)
        {
            var corner = new Vector(dimension);
            corner[i] = 1;
            vertices.Add(corner);
        }

        Assert.Equal(expected, SimplexVolume.Volume(vertices), 12);
    }

    [Fact]
    public void SimplexVolume_Volume_TriangleInSpaceIsHalfCrossProduct()
    {
        // Ребра (1, 0, 0) и (0, 1, 1): площадь |e₁ × e₂| / 2 = √2 / 2
        var vertices = new List<Vector> { new(1.0, 2.0, 3.0), new(2.0, 2.0, 3.0), new(1.0, 3.0, 4.0) };

        Assert.Equal(Math.Sqrt(2) / 2, SimplexVolume.Volume(vertices), 12);
    }

    [Fact]
    public void SimplexVolume_Volume_CollinearPointsAreDegenerate()
    {
        var vertices = new List<Vector> { new(0.0, 0.0), new(1.0, 1.0), new(3.0, 3.0) };

        Assert.Equal(0, SimplexVolume.Volume(vertices), 9);
    }

    [Fact]
    public void SimplexVolume_Volume_MixedDimensionsThrow()
    {
        var vertices = new List<Vector> { new(0.0, 0.0), new(1.0, 1.0, 1.0) };

        Assert.Throws<ArgumentException>(() => SimplexVolume.Volume(vertices));
    }

    #endregion

    #region Сфера

    [Fact]
    public void SphericalGeometry_PolygonArea_OctantIsEighthOfSphere()
    {
        var octant = new[] { new GeoPoint(0, 0), new GeoPoint(0, 90), new GeoPoint(90, 0) };

        double area = SphericalGeometry.PolygonArea(octant, 2).SiValue;

        Assert.Equal(Math.PI * 4 / 2, area, 12);
    }

    [Fact]
    public void SphericalGeometry_PolygonArea_OrientationDoesNotMatter()
    {
        var polygon = new[] { new GeoPoint(10, 20), new GeoPoint(15, 40), new GeoPoint(40, 35), new GeoPoint(30, 25) };

        double forward = SphericalGeometry.PolygonArea(polygon).SiValue;
        double backward = SphericalGeometry.PolygonArea(polygon.Reverse().ToArray()).SiValue;

        Assert.Equal(forward, backward, 1e-9 * forward);
    }

    [Fact]
    public void SphericalGeometry_PolygonArea_EquatorBoundsHemisphere()
    {
        var equator = new[] { new GeoPoint(0, 0), new GeoPoint(0, 120), new GeoPoint(0, -120) };

        Assert.Equal(2 * Math.PI, SphericalGeometry.PolygonArea(equator, 1).SiValue, 12);
    }

    [Fact]
    public void SphericalGeometry_PolygonArea_SmallSquareMatchesPlane()
    {
        // Квадрат 0,001° у экватора почти плоский: площадь (R·δ)²
        const double side = 0.001;
        var square = new[] { new GeoPoint(0, 0), new GeoPoint(0, side), new GeoPoint(side, side), new GeoPoint(side, 0) };
        double expected = Math.Pow(Radius * side * Math.PI / 180, 2);

        Assert.Equal(expected, SphericalGeometry.PolygonArea(square).SiValue, 1e-6 * expected);
    }

    [Fact]
    public void SphericalGeometry_PolygonArea_NonConvexSubtractsNotch()
    {
        // Квадрат 2° × 2° с вырезанной четвертью почти плоский: три четверти площади квадрата
        var notched = new[]
        {
            new GeoPoint(0, 0), new GeoPoint(0, 2), new GeoPoint(1, 2), new GeoPoint(1, 1), new GeoPoint(2, 1), new GeoPoint(2, 0),
        };
        var square = new[] { new GeoPoint(0, 0), new GeoPoint(0, 2), new GeoPoint(2, 2), new GeoPoint(2, 0) };

        double ratio = SphericalGeometry.PolygonArea(notched).SiValue / SphericalGeometry.PolygonArea(square).SiValue;

        Assert.Equal(0.75, ratio, 3);
    }

    [Fact]
    public void SphericalGeometry_VertexAngle_OctantHasRightAngles()
    {
        GeoPoint a = new(0, 0), b = new(0, 90), c = new(90, 0);

        Assert.Equal(90, SphericalGeometry.VertexAngle(a, b, c), 10);
        Assert.Equal(90, SphericalGeometry.VertexAngle(b, c, a), 10);
        Assert.Equal(90, SphericalGeometry.VertexAngle(c, a, b), 10);
    }

    [Fact]
    public void SphericalGeometry_VertexAngle_AgreesWithBearingDifference()
    {
        GeoPoint a = new(48.8566, 2.3522), b = new(55.7558, 37.6173), c = new(59.9311, 30.3609);
        double difference = Math.Abs(Geodesy.InitialBearing(b, a) - Geodesy.InitialBearing(b, c));

        Assert.Equal(Math.Min(difference, 360 - difference), SphericalGeometry.VertexAngle(a, b, c), 9);
    }

    [Fact]
    public void SphericalGeometry_VertexAngle_CoincidentPointsAreNaN()
    {
        GeoPoint a = new(10, 10), c = new(20, 30);

        Assert.True(double.IsNaN(SphericalGeometry.VertexAngle(a, a, c)));
    }

    #endregion
}

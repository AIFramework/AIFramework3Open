using AI.DataStructs.Algebraic;
using AI.Geometry.Intersections;
using AI.Geometry.Polylines;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Геометрия карты мира: ломаные дороги, путь сквозь области и азимуты.
/// </summary>
public class SpatialGeometryTests
{
    [Fact]
    public void Polyline_LengthAndDistance_FollowLinks()
    {
        Vector[] road = [new(0.0, 0.0), new(3.0, 4.0), new(3.0, 10.0)];

        Assert.Equal(11.0, Polyline.Length(road), 12);

        // Ближе всего второе звено x = 3: до первого звена от (5, 7) дальше
        Assert.Equal(2.0, Polyline.Distance(new Vector(5.0, 7.0), road), 12);
        Vector closest = Polyline.ClosestPoint(new Vector(5.0, 7.0), road);
        Assert.Equal(3.0, closest[0], 12);
        Assert.Equal(7.0, closest[1], 12);
    }

    [Fact]
    public void Polyline_RepeatedVertex_DoesNotProduceNaN()
    {
        Vector[] road = [new(0.0, 0.0), new(0.0, 0.0), new(10.0, 0.0)];

        Assert.Equal(1.0, Polyline.Distance(new Vector(5.0, 1.0), road), 12);
    }

    [Fact]
    public void SegmentPolygonIntersection_ConvexForest_CountsOnlyPathInside()
    {
        Vector[] forest = [new(0.0, 0.0), new(10.0, 0.0), new(10.0, 10.0), new(0.0, 10.0)];

        Assert.Equal(10.0, SegmentPolygonIntersection.LengthInside(new Segment(new Vector(-5.0, 5.0), new Vector(15.0, 5.0)), forest), 9);
        Assert.Equal(0.0, SegmentPolygonIntersection.LengthInside(new Segment(new Vector(-5.0, 20.0), new Vector(15.0, 20.0)), forest), 12);

        // Один конец в лесу
        Assert.Equal(5.0, SegmentPolygonIntersection.LengthInside(new Segment(new Vector(5.0, 5.0), new Vector(15.0, 5.0)), forest), 9);
    }

    [Fact]
    public void SegmentPolygonIntersection_NonConvexForest_SplitsIntoPieces()
    {
        // Подкова: рукава леса по x ∈ [0; 2] и [8; 10], соединённые снизу
        Vector[] horseshoe =
        [
            new(0.0, 0.0), new(10.0, 0.0), new(10.0, 10.0), new(8.0, 10.0),
            new(8.0, 2.0), new(2.0, 2.0), new(2.0, 10.0), new(0.0, 10.0),
        ];
        var path = new Segment(new Vector(-1.0, 5.0), new Vector(11.0, 5.0));

        IReadOnlyList<Segment> pieces = SegmentPolygonIntersection.Inside(path, horseshoe);

        Assert.Equal(2, pieces.Count);
        Assert.Equal(4.0, pieces.Sum(piece => piece.Length), 9);
    }

    [Fact]
    public void Polar_Bearing_FollowsCompassConvention()
    {
        Vector north = Polar.FromBearing(1.0, 0.0);
        Vector east = Polar.FromBearing(1.0, Math.PI / 2);

        Assert.Equal(0.0, north[0], 12);
        Assert.Equal(1.0, north[1], 12);
        Assert.Equal(1.0, east[0], 12);
        Assert.Equal(0.0, east[1], 12);
        Assert.Equal(1.5 * Math.PI, Polar.Bearing(new Vector(0.0, 0.0), new Vector(-1.0, 0.0)), 12);
    }

    [Fact]
    public void Polar_CartesianRoundTrip_KeepsRadiusAndAngle()
    {
        (double radius, double angle) = Polar.FromCartesian(Polar.ToCartesian(2.0, 5.0));

        Assert.Equal(2.0, radius, 12);
        Assert.Equal(5.0, angle, 12);
    }
}

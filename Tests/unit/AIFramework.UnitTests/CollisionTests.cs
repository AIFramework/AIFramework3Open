using AI.Geometry.Collision;
using AI.Geometry.Primitives;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Поиск столкновений: частные решения, GJK и EPA, диспетчер и момент касания против аналитических ответов.
/// </summary>
public class CollisionTests
{
    private const int Digits = 9;

    private static readonly Vector3 Up = new(0, 0, 1);

    private static Plane Ground => Plane.FromGeneral(0, 0, 1, 0);

    [Fact]
    public void SphereContacts_SphereSphere_DepthIsAnalytic()
    {
        var manifold = SphereContacts.SphereSphere(new SphereShape(Vector3.Zero, 1), new SphereShape(new Vector3(1.5, 0, 0), 1));

        Assert.Single(manifold.Points);
        Assert.Equal(0.5, manifold.Depth, Digits);
        AssertClose(new Vector3(1, 0, 0), manifold.Normal);
        AssertClose(new Vector3(0.75, 0, 0), manifold.Points[0].Position);
    }

    [Fact]
    public void SphereContacts_SpherePlane_DepthIsAnalytic()
    {
        var manifold = CollisionDispatcher.Collide(new SphereShape(new Vector3(3, 4, 0.7), 1), Ground);

        Assert.Equal(0.3, manifold.Depth, Digits);
        AssertClose(-Up, manifold.Normal);
        AssertClose(new Vector3(3, 4, -0.15), manifold.Points[0].Position);
    }

    [Fact]
    public void BoxContacts_BoxPlane_RestingBoxGivesFourPoints()
    {
        var box = new BoxShape(Rotated(new Vector3(0, 0, 0.99), Up, 0.3), new Vector3(1, 2, 1));

        var manifold = CollisionDispatcher.Collide(box, Ground);

        Assert.Equal(4, manifold.Points.Count);
        Assert.All(manifold.Points, point => Assert.Equal(0.01, point.Depth, Digits));
        Assert.Equal(4, manifold.Points.Select(point => point.FeatureId).Distinct().Count());
    }

    [Fact]
    public void BoxContacts_BoxBox_FaceContactGivesFourPointsWithDepth()
    {
        // Одинаковые кубы, верхний повернут на 45°: обрезанная грань восьмиугольник, от него остаются четыре точки
        var lower = new BoxShape(Pose.Identity, new Vector3(1, 1, 1));
        var upper = new BoxShape(Rotated(new Vector3(0, 0, 1.95), Up, Math.PI / 4), new Vector3(1, 1, 1));

        var manifold = BoxContacts.BoxBox(lower, upper);

        Assert.Equal(4, manifold.Points.Count);
        Assert.Equal(0.05, manifold.Depth, Digits);
        AssertClose(Up, manifold.Normal);
        Assert.All(manifold.Points, point =>
        {
            Assert.Equal(0.05, point.Depth, Digits);
            Assert.Equal(0.975, point.Position.Z, Digits);
        });
    }

    [Fact]
    public void BoxContacts_BoxBox_SmallBoxOnLargeGivesItsFourCorners()
    {
        var lower = new BoxShape(Pose.Identity, new Vector3(2, 2, 1));
        var upper = new BoxShape(Rotated(new Vector3(0.3, -0.2, 1.4), Up, 0.5), new Vector3(0.5, 0.5, 0.5));

        var manifold = CollisionDispatcher.Collide(lower, upper);

        Assert.Equal(4, manifold.Points.Count);
        Assert.Equal(0.1, manifold.Depth, Digits);
        Assert.All(manifold.Points, point => Assert.Equal(0.1, point.Depth, Digits));
    }

    [Fact]
    public void BoxContacts_BoxBox_EdgeCrossingGivesOnePoint()
    {
        var (lower, upper) = CrossedEdges();

        var manifold = BoxContacts.BoxBox(lower, upper);

        Assert.Single(manifold.Points);
        Assert.Equal(0.1, manifold.Depth, Digits);
        AssertClose(Up, manifold.Normal);
        AssertClose(new Vector3(0, 0, Math.Sqrt(2) - 0.05), manifold.Points[0].Position);
    }

    [Fact]
    public void BoxContacts_BoxSphere_BothOrdersGiveOppositeNormals()
    {
        var box = new BoxShape(Pose.Identity, new Vector3(1, 1, 1));
        var sphere = new SphereShape(new Vector3(1.3, 0, 0), 0.5);

        var boxFirst = CollisionDispatcher.Collide(box, sphere);
        var sphereFirst = CollisionDispatcher.Collide(sphere, box);

        Assert.Equal(0.2, boxFirst.Depth, Digits);
        AssertClose(new Vector3(1, 0, 0), boxFirst.Normal);
        AssertClose(new Vector3(-1, 0, 0), sphereFirst.Normal);
        AssertClose(new Vector3(0.9, 0, 0), boxFirst.Points[0].Position);
    }

    [Fact]
    public void CapsuleContacts_CapsuleCapsule_ParallelGivesTwoPoints()
    {
        var alongX = Rotated(Vector3.Zero, new Vector3(0, 1, 0), Math.PI / 2);
        var a = new CapsuleShape(alongX, 1, 0.5);
        var b = new CapsuleShape(alongX with { Position = new Vector3(0.5, 0, 0.9) }, 1, 0.5);

        var manifold = CollisionDispatcher.Collide(a, b);

        Assert.Equal(2, manifold.Points.Count);
        Assert.Equal(0.1, manifold.Depth, Digits);
        AssertClose(Up, manifold.Normal);
        Assert.Equal(-0.5, manifold.Points.Min(point => point.Position.X), Digits);
        Assert.Equal(1.0, manifold.Points.Max(point => point.Position.X), Digits);
    }

    [Fact]
    public void CapsuleContacts_CapsuleSphere_DepthIsAnalytic()
    {
        var capsule = new CapsuleShape(Rotated(Vector3.Zero, new Vector3(0, 1, 0), Math.PI / 2), 1, 0.5);

        var manifold = CollisionDispatcher.Collide(new SphereShape(new Vector3(0.5, 0, 0.8), 0.5), capsule);

        Assert.Equal(0.2, manifold.Depth, Digits);
        AssertClose(-Up, manifold.Normal);
    }

    [Fact]
    public void CapsuleContacts_CapsuleBox_LyingCapsuleGivesTwoPoints()
    {
        var box = new BoxShape(Rotated(Vector3.Zero, Up, 0.2), new Vector3(1, 1, 1));
        var capsule = new CapsuleShape(Rotated(new Vector3(0.1, 0, 1.2), new Vector3(0, 1, 0), Math.PI / 2), 0.5, 0.25);

        var manifold = CollisionDispatcher.Collide(capsule, box);

        Assert.Equal(2, manifold.Points.Count);
        Assert.Equal(0.05, manifold.Depth, Digits);
        AssertClose(-Up, manifold.Normal);
        Assert.All(manifold.Points, point => Assert.Equal(0.05, point.Depth, Digits));
    }

    [Fact]
    public void Gjk_Distance_SpheresMatchAnalytic()
    {
        var (distance, onA, onB) = Gjk.Distance(new SphereShape(new Vector3(1, 2, 3), 1), new SphereShape(new Vector3(4, 6, 3), 1.5));

        Assert.Equal(2.5, distance, Digits);
        AssertClose(new Vector3(1.6, 2.8, 3), onA);
        AssertClose(new Vector3(3.1, 4.8, 3), onB);
    }

    [Fact]
    public void Gjk_Distance_BoxesMatchAnalytic()
    {
        var a = new BoxShape(Pose.Identity, new Vector3(1, 1, 1));

        Assert.Equal(1.0, Gjk.Distance(a, new BoxShape(Pose.At(new Vector3(3, 0.5, 0)), new Vector3(1, 1, 1))).Distance, Digits);
        Assert.Equal(3 - Math.Sqrt(2), Gjk.Distance(a, new BoxShape(Rotated(new Vector3(4, 0, 0), Up, Math.PI / 4), new Vector3(1, 1, 1))).Distance, Digits);
        Assert.Equal(Math.Sqrt(3), Gjk.Distance(a, new BoxShape(Pose.At(new Vector3(3, 3, 3)), new Vector3(1, 1, 1))).Distance, Digits);
        Assert.False(Gjk.Intersects(a, new BoxShape(Pose.At(new Vector3(3, 3, 3)), new Vector3(1, 1, 1))));
        Assert.True(Gjk.Intersects(a, new BoxShape(Pose.At(new Vector3(1.5, 1.5, 1.5)), new Vector3(1, 1, 1))));
    }

    [Fact]
    public void Gjk_Distance_PointSetAndMinkowskiSumMatchAnalytic()
    {
        var cube = new ConvexPointSet(Rotated(Vector3.Zero, Up, 0.4), Enumerable.Range(0, 8).Select(i =>
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
        var rounded = new MinkowskiShape(new BoxShape(Pose.Identity, new Vector3(1, 1, 1)), new SphereShape(Vector3.Zero, 0.5));

        Assert.Equal(2.0, Gjk.Distance(cube, new SphereShape(new Vector3(0, 0, 4), 1)).Distance, Digits);
        Assert.Equal(3 - 0.5 - 1, Gjk.Distance(rounded, new SphereShape(new Vector3(4, 0, 0), 1)).Distance, Digits);
    }

    [Fact]
    public void Epa_Penetration_BoxesAgreeWithSat()
    {
        var lower = new BoxShape(Pose.Identity, new Vector3(1, 1.5, 1));
        foreach (double angle in new[] { 0.0, 0.3, Math.PI / 4, 1.1 })
        {
            foreach (var offset in new[] { new Vector3(0, 0, 1.8), new Vector3(0.4, -0.3, 1.85), new Vector3(1.5, 0.2, 0.1) })
            {
                var upper = new BoxShape(Rotated(offset, Up, angle), new Vector3(0.6, 0.8, 0.9));
                var sat = BoxContacts.BoxBox(lower, upper);
                var epa = Epa.Penetration(lower, upper);

                Assert.Equal(sat.Depth, epa.Depth, Digits);
                AssertClose(sat.Normal, epa.Normal);
            }
        }

        var (a, b) = CrossedEdges();
        Assert.Equal(BoxContacts.BoxBox(a, b).Depth, Epa.Penetration(a, b).Depth, Digits);
    }

    [Fact]
    public void Epa_Penetration_CurvedShapesApproachAnalytic()
    {
        var spheres = Epa.Penetration(new SphereShape(Vector3.Zero, 1), new SphereShape(new Vector3(1, 1, 1), 1));
        var cylinder = new CylinderShape(Pose.At(new Vector3(0, 0, 1.4)), 0.5, 0.5);
        var standing = CollisionDispatcher.Collide(new BoxShape(Pose.Identity, new Vector3(1, 1, 1)), cylinder);

        Assert.Equal(2 - Math.Sqrt(3), spheres.Depth, 6);
        // У гладких форм нормаль грани многогранника точна примерно как корень из погрешности глубины
        AssertClose(new Vector3(1, 1, 1) / Math.Sqrt(3), spheres.Normal, 1e-4);
        Assert.Equal(0.1, standing.Depth, Digits);
        AssertClose(Up, standing.Normal);
    }

    [Fact]
    public void CollisionDispatcher_Collide_CylinderOnPlaneGivesFourPoints()
    {
        var cylinder = new CylinderShape(Rotated(new Vector3(2, 1, 0.98), Up, 0.7), 1, 0.5);

        var manifold = CollisionDispatcher.Collide(cylinder, Ground);

        Assert.Equal(4, manifold.Points.Count);
        Assert.All(manifold.Points, point => Assert.Equal(0.02, point.Depth, Digits));
        AssertClose(-Up, manifold.Normal);
    }

    [Fact]
    public void CollisionDispatcher_Collide_SeparatedShapesReportNoContact()
    {
        var box = new BoxShape(Rotated(Vector3.Zero, Up, 0.3), new Vector3(1, 1, 1));
        var alongX = Rotated(new Vector3(0, 0, 1.6), new Vector3(0, 1, 0), Math.PI / 2);
        IConvexShape[] others =
        [
            new SphereShape(new Vector3(0, 0, 2.1), 1),
            new BoxShape(Rotated(new Vector3(0, 0, 2.5), Up, 0.7), new Vector3(1, 1, 0.4)),
            new CapsuleShape(alongX, 0.5, 0.5),
            new CylinderShape(Pose.At(new Vector3(0, 0, 2.1)), 1, 1),
            new ConvexPointSet(Pose.At(new Vector3(3, 0, 0)), [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)]),
        ];

        foreach (var other in others)
        {
            Assert.False(CollisionDispatcher.Collide(box, other).HasContact);
            Assert.False(CollisionDispatcher.Collide(other, box).HasContact);
        }

        Assert.False(CollisionDispatcher.Collide(new CapsuleShape(alongX, 1, 0.5), new CapsuleShape(alongX with { Position = new Vector3(0, 0, 2.7) }, 1, 0.5)).HasContact);
        Assert.False(CollisionDispatcher.Collide(new SphereShape(new Vector3(0, 0, 1.01), 1), Ground).HasContact);
        Assert.False(CollisionDispatcher.Collide(new CylinderShape(Pose.At(new Vector3(0, 0, 1.01)), 1, 1), Ground).HasContact);
    }

    [Fact]
    public void TimeOfImpact_SweptSphere_MatchesAnalytic()
    {
        var sphere = new SphereShape(new Vector3(0, 0, 5), 1);
        var box = new BoxShape(Pose.Identity, new Vector3(1, 1, 1));

        Assert.Equal(0.4, TimeOfImpact.SpherePlane(sphere, new Vector3(0, 0, -10), Ground)!.Value, Digits);
        Assert.Null(TimeOfImpact.SpherePlane(sphere, new Vector3(0, 0, -3), Ground));
        Assert.Equal(0.3, TimeOfImpact.SphereSphere(new SphereShape(Vector3.Zero, 1), new Vector3(10, 0, 0), new SphereShape(new Vector3(5, 0, 0), 1))!.Value, Digits);
        Assert.Null(TimeOfImpact.SphereSphere(new SphereShape(Vector3.Zero, 1), new Vector3(10, 0, 0), new SphereShape(new Vector3(5, 3, 0), 1)));
        Assert.Equal(0.35, TimeOfImpact.SphereBox(new SphereShape(new Vector3(5, 0, 0), 0.5), new Vector3(-10, 0, 0), box)!.Value, 7);

        // По диагонали шар касается ребра x = y = 1: центр в точке (c, c) с √2·(c − 1) = r
        double touch = 1 + (0.5 / Math.Sqrt(2));
        Assert.Equal((5 - touch) / 10, TimeOfImpact.SphereBox(new SphereShape(new Vector3(5, 5, 0), 0.5), new Vector3(-10, -10, 0), box)!.Value, 7);
        Assert.Null(TimeOfImpact.SphereBox(new SphereShape(new Vector3(5, 5, 0), 0.5), new Vector3(-10, 0, 0), box));
    }

    [Fact]
    public void TimeOfImpact_Translational_MatchesAnalytic()
    {
        var box = new BoxShape(Pose.Identity, new Vector3(1, 1, 1));
        var moving = new BoxShape(Rotated(new Vector3(10, 0, 0), Up, Math.PI / 4), new Vector3(1, 1, 1));
        var cylinder = new CylinderShape(Pose.Identity, 1, 1);

        Assert.Equal((9 - Math.Sqrt(2)) / 10, TimeOfImpact.Translational(box, Vector3.Zero, moving, new Vector3(-10, 0, 0))!.Value, 7);
        Assert.Equal(0.85, TimeOfImpact.Translational(cylinder, Vector3.Zero, new SphereShape(new Vector3(0, 0, 10), 0.5), new Vector3(0, 0, -10))!.Value, 7);
        Assert.Equal(0.85, TimeOfImpact.Translational(cylinder, new Vector3(0, 0, 5), new SphereShape(new Vector3(0, 0, 10), 0.5), new Vector3(0, 0, -5))!.Value, 7);
        Assert.Null(TimeOfImpact.Translational(cylinder, new Vector3(0, 0, 2), new SphereShape(new Vector3(0, 0, 10), 0.5), new Vector3(0, 0, -2)));
        Assert.Null(TimeOfImpact.Translational(box, Vector3.Zero, moving, new Vector3(0, 10, 0)));
        Assert.Equal(0.0, TimeOfImpact.Translational(box, Vector3.Zero, box, new Vector3(1, 0, 0))!.Value);
    }

    /// <summary>
    /// Нижний куб повернут на 45° вокруг X, его верх это ребро вдоль X на высоте √2; верхний повернут на 45° вокруг Y,
    /// его низ это ребро вдоль Y, опущенное на 0,1 ниже верха нижнего
    /// </summary>
    private static (BoxShape Lower, BoxShape Upper) CrossedEdges()
    {
        var lower = new BoxShape(Rotated(Vector3.Zero, new Vector3(1, 0, 0), Math.PI / 4), new Vector3(1, 1, 1));
        var upper = new BoxShape(Rotated(new Vector3(0, 0, (2 * Math.Sqrt(2)) - 0.1), new Vector3(0, 1, 0), Math.PI / 4), new Vector3(1, 1, 1));
        return (lower, upper);
    }

    /// <summary>Положение с поворотом на angle вокруг единичной оси axis (формула Родрига)</summary>
    private static Pose Rotated(Vector3 position, Vector3 axis, double angle)
    {
        Vector3 Turn(Vector3 v) => (v * Math.Cos(angle)) + (axis.Cross(v) * Math.Sin(angle)) + (axis * (axis.Dot(v) * (1 - Math.Cos(angle))));
        return new Pose(position, Turn(new Vector3(1, 0, 0)), Turn(new Vector3(0, 1, 0)), Turn(new Vector3(0, 0, 1)));
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, double tolerance = 1e-9)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
        Assert.Equal(expected.Z, actual.Z, tolerance);
    }
}

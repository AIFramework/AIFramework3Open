using AI.Geometry.Distances;
using AI.Geometry.Hull;
using AI.Geometry.Intersections;
using AI.Geometry.MassProperties;
using AI.Geometry.Primitives;
using AI.Geometry.Spatial;
using AI.Geometry.Transforms;
using Xunit;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AIFramework.UnitTests;

/// <summary>
/// Трехмерная геометрия: кватернионы, расстояния, массовые характеристики, выпуклая оболочка и BVH.
/// </summary>
public class Geometry3DTests
{
    [Fact]
    public void Quaternion_ToRotationVector_InvertsFromRotationVector()
    {
        var rng = new Random(1);

        for (int i = 0; i < 200; i++)
        {
            Vector3 v = RandomDirection(rng) * (3.0 * rng.NextDouble());
            AssertClose(v, Quaternion.FromRotationVector(v).ToRotationVector(), 1e-12);
        }

        var tiny = new Vector3(1e-9, -2e-9, 3e-10);
        AssertClose(tiny, Quaternion.FromRotationVector(tiny).ToRotationVector(), 1e-22);
        AssertClose(Vector3.Zero, Quaternion.FromRotationVector(Vector3.Zero).ToRotationVector(), 0);
        Assert.Equal(Quaternion.Identity, Quaternion.FromRotationVector(Vector3.Zero));
    }

    [Fact]
    public void Quaternion_FromRotationVector_MatchesFromAxisAngle()
    {
        var axis = new AI.DataStructs.Algebraic.Vector(1.0, -2.0, 0.5);
        Quaternion expected = Quaternion.FromAxisAngle(axis, 2.3);
        Quaternion actual = Quaternion.FromRotationVector(Vector3.FromVector(axis).Normalized * 2.3);

        Assert.Equal(1.0, Quaternion.Dot(expected, actual), 14);
    }

    [Fact]
    public void Quaternion_ToRotationVector_TakesShortestArc()
    {
        Quaternion q = Quaternion.FromAxisAngle(new Vector3(0, 0, 1), 1.5 * Math.PI);
        var negated = new Quaternion(-q.W, -q.X, -q.Y, -q.Z);

        AssertClose(new Vector3(0, 0, -0.5 * Math.PI), q.ToRotationVector(), 1e-12);
        AssertClose(new Vector3(0, 0, -0.5 * Math.PI), negated.ToRotationVector(), 1e-12);
    }

    [Fact]
    public void Quaternion_Integrate_ConstantAngularVelocityMatchesAxisAngle()
    {
        var omega = new Vector3(0.3, -1.2, 2.0);
        Quaternion start = Quaternion.FromEuler(0.2, -0.4, 1.1);
        const double dt = 1e-3;
        const int steps = 5000;
        Quaternion q = start;

        for (int i = 0; i < steps; i++)
            q = q.Integrate(omega, dt);

        Quaternion expected = Quaternion.FromAxisAngle(omega, omega.Length * steps * dt) * start;
        var probe = new Vector3(0.7, 0.1, -0.4);

        Assert.Equal(1.0, Math.Abs(Quaternion.Dot(q, expected)), 10);
        Assert.Equal(1.0, q.Norm, 14);
        AssertClose(expected.Rotate(probe), q.Rotate(probe), 1e-9);
    }

    [Fact]
    public void Quaternion_Rotate_Vector3MatchesVectorOverload()
    {
        var rng = new Random(2);

        for (int i = 0; i < 50; i++)
        {
            // Ненормированный кватернион: обе перегрузки считают q·p·q*
            var q = new Quaternion(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
            Vector3 p = RandomDirection(rng) * 3;

            AssertClose(Vector3.FromVector(q.Rotate(p.ToVector())), q.Rotate(p), 1e-12);
        }
    }

    [Fact]
    public void Quaternion_RotationVectorBetween_RecoversStep()
    {
        Quaternion from = Quaternion.FromEuler(-0.7, 0.3, 2.0);
        var step = new Vector3(0.1, 0.2, -0.3);
        Quaternion to = Quaternion.FromRotationVector(step) * from;

        AssertClose(step, Quaternion.RotationVectorBetween(from, to), 1e-12);
        AssertClose(Vector3.Zero, Quaternion.RotationVectorBetween(from, from), 1e-12);
    }

    [Fact]
    public void SegmentSegment_ClosestPoints_SkewAndParallelSegments()
    {
        var (s, t, onA, onB) = SegmentSegment.ClosestPoints(
            new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(1, -1, 1), new Vector3(1, 1, 1));

        Assert.Equal(0.5, s, 12);
        Assert.Equal(0.5, t, 12);
        AssertClose(new Vector3(1, 0, 0), onA, 1e-12);
        AssertClose(new Vector3(1, 0, 1), onB, 1e-12);

        double parallel = SegmentSegment.Distance(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0.5, 1, 0), new Vector3(2, 1, 0));
        Assert.Equal(1.0, parallel, 12);

        double apart = SegmentSegment.Distance(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(3, 0, 0), new Vector3(5, 0, 0));
        Assert.Equal(2.0, apart, 12);
    }

    [Fact]
    public void SegmentSegment_Distance_MatchesSampling()
    {
        var rng = new Random(3);

        for (int pair = 0; pair < 20; pair++)
        {
            Vector3 a0 = RandomDirection(rng), a1 = RandomDirection(rng) * 2, b0 = RandomDirection(rng), b1 = RandomDirection(rng) * 2;
            double distance = SegmentSegment.Distance(a0, a1, b0, b1);
            double sampled = double.PositiveInfinity;

            for (int i = 0; i <= 200; i++)
            {
                for (int j = 0; j <= 200; j++)
                    sampled = Math.Min(sampled, Vector3.Lerp(a0, a1, i / 200.0).DistanceTo(Vector3.Lerp(b0, b1, j / 200.0)));
            }

            Assert.True(distance <= sampled + 1e-12);
            Assert.True(sampled - distance < 0.03);
        }
    }

    [Fact]
    public void PointObb_ClosestPoint_MatchesMatrixForm()
    {
        var rng = new Random(4);
        var center = new Vector3(1, -2, 0.5);
        var half = new Vector3(0.5, 1, 2);
        Quaternion q = Quaternion.FromEuler(0.4, -0.9, 1.3);
        var box = new Obb(center.ToVector(), half.ToVector(), q.ToRotationMatrix3());

        for (int i = 0; i < 100; i++)
        {
            Vector3 p = center + (RandomDirection(rng) * 4);
            AssertClose(Vector3.FromVector(PointObb.ClosestPoint(p.ToVector(), box)), PointObb.ClosestPoint(p, center, half, q), 1e-12);
        }

        Vector3 outside = center + q.Rotate(new Vector3(half.X + 2, 0, 0));
        Assert.Equal(2.0, PointObb.Distance(outside, center, half, q), 12);
        Assert.Equal(0.0, PointObb.Distance(center, center, half, q), 12);
        Assert.Equal(0.0, PointAabb.Distance(new Vector3(0.5, 0.5, 0.5), Vector3.Zero, new Vector3(1, 1, 1)), 12);
        Assert.Equal(5.0, PointAabb.Distance(new Vector3(4, 5, 0.5), Vector3.Zero, new Vector3(1, 1, 1)), 12);
    }

    [Fact]
    public void TriangleTriangle_Closest_SeparatedAndCrossing()
    {
        Vector3 a0 = new(0, 0, 0), a1 = new(2, 0, 0), a2 = new(0, 2, 0);
        var lift = new Vector3(0.1, 0.1, 2);

        Assert.Equal(2.0, TriangleTriangle.Closest(a0, a1, a2, a0 + lift, a1 + lift, a2 + lift).Distance, 12);

        var crossing = TriangleTriangle.Closest(a0, a1, a2, new Vector3(0.5, 0.5, -1), new Vector3(0.5, 0.5, 1), new Vector3(0.5, -2, 0));
        Assert.Equal(0.0, crossing.Distance, 12);
        AssertClose(new Vector3(0.5, 0.5, 0), crossing.OnA, 1e-12);
    }

    [Fact]
    public void TriangleTriangle_Closest_MatchesSampling()
    {
        var rng = new Random(5);
        const int n = 30;

        for (int pair = 0; pair < 10; pair++)
        {
            Vector3 a0 = RandomDirection(rng), a1 = RandomDirection(rng), a2 = RandomDirection(rng);
            Vector3 shift = RandomDirection(rng) * rng.NextDouble();
            Vector3 b0 = RandomDirection(rng) + shift, b1 = RandomDirection(rng) + shift, b2 = RandomDirection(rng) + shift;
            var (distance, onA, onB) = TriangleTriangle.Closest(a0, a1, a2, b0, b1, b2);

            Assert.Equal(distance, onA.DistanceTo(onB), 12);
            Assert.Equal(0.0, onA.DistanceTo(PointTriangle.ClosestPoint(onA, a0, a1, a2)), 12);
            Assert.Equal(0.0, onB.DistanceTo(PointTriangle.ClosestPoint(onB, b0, b1, b2)), 12);

            var samplesA = SampleTriangle(a0, a1, a2, n);
            var samplesB = SampleTriangle(b0, b1, b2, n);
            double sampled = double.PositiveInfinity;

            foreach (Vector3 p in samplesA)
            {
                foreach (Vector3 q in samplesB)
                    sampled = Math.Min(sampled, p.DistanceTo(q));
            }

            Assert.True(distance <= sampled + 1e-12);
            Assert.True(sampled - distance < 0.15);
        }
    }

    [Fact]
    public void MeshMass_Compute_BoxMatchesClosedForm()
    {
        var half = new Vector3(1, 2, 3);
        var center = new Vector3(5, -1, 2);
        var (vertices, triangles) = BoxMesh(center, half, Quaternion.Identity);
        SolidProperties solid = MeshMass.Compute(vertices, triangles);

        Assert.Equal(48.0, solid.Volume, 10);
        AssertClose(center, solid.Centroid, 1e-12);
        AssertClose(InertiaTensor.Box(48, half), solid.Inertia, 1e-9);
        AssertClose(InertiaTensor.Box(10, half), solid.InertiaForMass(10), 1e-10);

        // Обратный обход всех граней дает те же характеристики
        var reversed = triangles.Select(t => (t.A, t.C, t.B)).ToArray();
        SolidProperties flipped = MeshMass.Compute(vertices, reversed);
        Assert.Equal(48.0, flipped.Volume, 10);
        AssertClose(solid.Inertia, flipped.Inertia, 1e-9);
    }

    [Fact]
    public void MeshMass_Compute_RotatedBoxMatchesRotatedTensor()
    {
        var half = new Vector3(1, 2, 3);
        Quaternion q = Quaternion.FromEuler(0.3, 1.1, -0.6);
        var (vertices, triangles) = BoxMesh(new Vector3(-3, 4, 1), half, q);
        SolidProperties solid = MeshMass.Compute(vertices, triangles);

        AssertClose(InertiaTensor.Rotate(InertiaTensor.Box(48, half), q), solid.Inertia, 1e-9);

        var (moments, axes) = InertiaTensor.Principal(solid.Inertia);
        AssertClose(new Vector3(80, 160, 208), moments, 1e-9);
        AssertClose(solid.Inertia, InertiaTensor.Rotate(InertiaTensor.FromDiagonal(moments), axes), 1e-9);

        // Первая главная ось (наименьший момент) совпадает с длинной осью бруска с точностью до знака
        Vector3 longAxis = q.Rotate(new Vector3(0, 0, 1));
        Assert.Equal(1.0, Math.Abs(longAxis.Dot(new Vector3(axes[0, 0], axes[1, 0], axes[2, 0]))), 9);
    }

    [Fact]
    public void MeshMass_Compute_TetrahedronMatchesPrimitive()
    {
        var rng = new Random(6);

        for (int i = 0; i < 20; i++)
        {
            Vector3[] v = [RandomDirection(rng) * 3, RandomDirection(rng) * 3, RandomDirection(rng) * 3, RandomDirection(rng) * 3];

            if ((v[1] - v[0]).Cross(v[2] - v[0]).Dot(v[3] - v[0]) > 0)
                (v[1], v[2]) = (v[2], v[1]);

            (int, int, int)[] faces = [(0, 1, 2), (0, 3, 1), (1, 3, 2), (2, 3, 0)];
            SolidProperties solid = MeshMass.Compute(v, faces);
            var tetra = new Tetrahedron(v[0].ToVector(), v[1].ToVector(), v[2].ToVector(), v[3].ToVector());

            Assert.Equal(tetra.Volume(), solid.Volume, 10);
            AssertClose(Vector3.FromVector(tetra.Centroid), solid.Centroid, 1e-10);
        }
    }

    [Fact]
    public void InertiaTensor_ParallelAxis_ShiftsSphere()
    {
        Matrix shifted = InertiaTensor.ParallelAxis(InertiaTensor.Sphere(2, 0.5), 2, new Vector3(0, 0, 3));

        Assert.Equal(18.2, shifted[0, 0], 12);
        Assert.Equal(18.2, shifted[1, 1], 12);
        Assert.Equal(0.2, shifted[2, 2], 12);
        Assert.Equal(0.0, shifted[0, 2], 12);
    }

    [Fact]
    public void InertiaTensor_Capsule_ReducesToSphereAndCylinder()
    {
        AssertClose(InertiaTensor.Sphere(3, 0.7), InertiaTensor.Capsule(3, 0.7, 0), 1e-12);

        Matrix thin = InertiaTensor.Capsule(3, 1e-4, 2);
        Matrix rod = InertiaTensor.Cylinder(3, 1e-4, 2);
        Assert.Equal(rod[0, 0], thin[0, 0], 3);
        Assert.Equal(1.0, InertiaTensor.Cylinder(12, 0, 1)[0, 0], 12);
    }

    [Fact]
    public void ConvexHull3D_Build_RandomPointsContainedAndFacesOutward()
    {
        var rng = new Random(7);
        var points = Enumerable.Range(0, 400).Select(_ => RandomDirection(rng) * Math.Cbrt(rng.NextDouble())).ToArray();
        ConvexHull3D hull = ConvexHull3D.Build(points);

        Assert.All(points, p => Assert.True(hull.Contains(p)));
        Assert.Equal(2 * hull.Vertices.Count - 4, hull.Faces.Count);

        var edges = new HashSet<(int, int)>();

        foreach (HullFace face in hull.Faces)
        {
            Vector3 a = hull.Vertices[face.A], b = hull.Vertices[face.B], c = hull.Vertices[face.C];

            Assert.Equal(1.0, face.Normal.Length, 12);
            Assert.True((b - a).Cross(c - a).Dot(face.Normal) > 0);
            Assert.True(face.SignedDistance(Vector3.Zero) < 0);
            Assert.True(edges.Add((face.A, face.B)) && edges.Add((face.B, face.C)) && edges.Add((face.C, face.A)));
        }

        // Замкнутость: у каждого направленного ребра есть обратное
        Assert.All(edges, e => Assert.Contains((e.Item2, e.Item1), edges));
        Assert.True(MeshMass.Compute(hull.Vertices, hull.Triangles).Volume > 0);
    }

    [Fact]
    public void ConvexHull3D_Build_GridWithDuplicatesGivesCube()
    {
        var grid = new List<Vector3>();

        for (int i = 0; i <= 4; i++)
        {
            for (int j = 0; j <= 4; j++)
            {
                for (int k = 0; k <= 4; k++)
                {
                    var p = new Vector3(i / 2.0 - 1, j / 2.0 - 1, k / 2.0 - 1);
                    grid.Add(p);
                    grid.Add(p);
                }
            }
        }

        ConvexHull3D hull = ConvexHull3D.Build(grid);
        SolidProperties solid = MeshMass.Compute(hull.Vertices, hull.Triangles);

        Assert.Equal(8, hull.Vertices.Count);
        Assert.Equal(12, hull.Faces.Count);
        Assert.Equal(8.0, solid.Volume, 12);
        AssertClose(InertiaTensor.Box(8, new Vector3(1, 1, 1)), solid.Inertia, 1e-12);
    }

    [Fact]
    public void ConvexHull3D_Build_CoplanarPointsThrow()
    {
        var flat = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(0.5, 0.5, 0) };

        Assert.Throws<ArgumentException>(() => ConvexHull3D.Build(flat));
    }

    [Fact]
    public void DynamicAabbTree_Queries_MatchBruteForce()
    {
        var rng = new Random(8);
        var tree = new DynamicAabbTree<int>(0.2);
        var boxes = new Dictionary<int, (Vector3 Min, Vector3 Max)>();

        (Vector3, Vector3) RandomBox()
        {
            var c = new Vector3(rng.NextDouble() * 20, rng.NextDouble() * 20, rng.NextDouble() * 20);
            var h = new Vector3(0.1 + rng.NextDouble() * 1.4, 0.1 + rng.NextDouble() * 1.4, 0.1 + rng.NextDouble() * 1.4);
            return (c - h, c + h);
        }

        for (int i = 0; i < 300; i++)
        {
            var (min, max) = RandomBox();
            int id = tree.Insert(min, max, i);
            boxes[id] = (min, max);
            Assert.Equal(i, tree[id]);
        }

        for (int i = 0; i < 200; i++)
        {
            int id = boxes.Keys.ElementAt(rng.Next(boxes.Count));
            Vector3 shift = RandomDirection(rng) * (rng.Next(2) == 0 ? 0.05 : 3.0);
            var (min, max) = boxes[id];
            tree.Update(id, min + shift, max + shift);
            boxes[id] = (min + shift, max + shift);
        }

        for (int i = 0; i < 50; i++)
        {
            int id = boxes.Keys.ElementAt(rng.Next(boxes.Count));
            tree.Remove(id);
            boxes.Remove(id);
        }

        for (int i = 0; i < 30; i++)
        {
            var (min, max) = RandomBox();
            boxes[tree.Insert(min, max, -i)] = (min, max);
        }

        Assert.Equal(boxes.Count, tree.Count);
        Assert.True(tree.Height <= 20, $"Высота дерева {tree.Height}");

        for (int i = 0; i < 50; i++)
        {
            var (min, max) = RandomBox();
            var found = new List<int>();
            tree.Query(min, max, found);
            var expected = boxes.Where(b => Overlaps(b.Value.Min, b.Value.Max, min, max)).Select(b => b.Key);

            Assert.Equal(expected.OrderBy(x => x), found.OrderBy(x => x));
        }

        for (int i = 0; i < 50; i++)
        {
            var origin = new Vector3(rng.NextDouble() * 30 - 5, rng.NextDouble() * 30 - 5, rng.NextDouble() * 30 - 5);
            Vector3 direction = RandomDirection(rng);
            double maxT = i % 2 == 0 ? double.PositiveInfinity : 5 + rng.NextDouble() * 20;
            var found = new List<int>();
            tree.QueryRay(origin, direction, maxT, found);
            var ray = new Ray(origin.ToVector(), direction.ToVector());
            var expected = boxes.Where(b =>
            {
                var hit = RayAabbIntersection.Intersect(ray, new Aabb(b.Value.Min.ToVector(), b.Value.Max.ToVector()));
                return hit is { } h && h.tMax >= 0 && h.tMin <= maxT;
            }).Select(b => b.Key);

            Assert.Equal(expected.OrderBy(x => x), found.OrderBy(x => x));
        }

        var pairs = new List<(int A, int B)>();
        tree.QueryPairs(pairs);
        var ids = boxes.Keys.OrderBy(x => x).ToArray();
        var expectedPairs = new List<(int, int)>();

        for (int i = 0; i < ids.Length; i++)
        {
            for (int j = i + 1; j < ids.Length; j++)
            {
                if (Overlaps(boxes[ids[i]].Min, boxes[ids[i]].Max, boxes[ids[j]].Min, boxes[ids[j]].Max))
                    expectedPairs.Add((ids[i], ids[j]));
            }
        }

        Assert.Equal(expectedPairs.OrderBy(p => p), pairs.OrderBy(p => p));
    }

    [Fact]
    public void DynamicAabbTree_Update_ReinsertsOnlyOutsideMargin()
    {
        var tree = new DynamicAabbTree<string>(0.5);
        int id = tree.Insert(Vector3.Zero, new Vector3(1, 1, 1), "box");
        var small = new Vector3(0.3, 0, 0);
        var large = new Vector3(2, 0, 0);

        Assert.False(tree.Update(id, small, new Vector3(1, 1, 1) + small));
        Assert.Equal((small, new Vector3(1, 1, 1) + small), tree.GetBounds(id));
        Assert.True(tree.Update(id, large, new Vector3(1, 1, 1) + large));
        Assert.Equal((new Vector3(1.5, -0.5, -0.5), new Vector3(3.5, 1.5, 1.5)), tree.GetFatBounds(id));

        tree.Remove(id);
        Assert.Equal(0, tree.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[id]);
    }

    private static Vector3 RandomDirection(Random rng)
    {
        while (true)
        {
            var v = new Vector3(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
            double length = v.Length;

            if (length > 0.1 && length <= 1)
                return v / length;
        }
    }

    private static List<Vector3> SampleTriangle(Vector3 a, Vector3 b, Vector3 c, int n)
    {
        var samples = new List<Vector3>();

        for (int i = 0; i <= n; i++)
        {
            for (int j = 0; i + j <= n; j++)
                samples.Add(a + ((b - a) * (i / (double)n)) + ((c - a) * (j / (double)n)));
        }

        return samples;
    }

    private static (Vector3[] Vertices, (int A, int B, int C)[] Triangles) BoxMesh(Vector3 center, Vector3 half, Quaternion orientation)
    {
        var vertices = new Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            var local = new Vector3((i & 1) == 0 ? -half.X : half.X, (i & 2) == 0 ? -half.Y : half.Y, (i & 4) == 0 ? -half.Z : half.Z);
            vertices[i] = center + orientation.Rotate(local);
        }

        // Четырехугольные грани с обходом против часовой стрелки снаружи
        int[][] quads = [[0, 4, 6, 2], [1, 3, 7, 5], [0, 1, 5, 4], [2, 6, 7, 3], [0, 2, 3, 1], [4, 5, 7, 6]];
        var triangles = quads.SelectMany(q => new[] { (q[0], q[1], q[2]), (q[0], q[2], q[3]) }).ToArray();
        return (vertices, triangles);
    }

    private static bool Overlaps(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        => aMin.X <= bMax.X && aMax.X >= bMin.X && aMin.Y <= bMax.Y && aMax.Y >= bMin.Y && aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;

    private static void AssertClose(Vector3 expected, Vector3 actual, double tolerance)
        => Assert.True(expected.DistanceTo(actual) <= tolerance, $"Ожидалось {expected}, получено {actual}");

    private static void AssertClose(Matrix expected, Matrix actual, double tolerance)
    {
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                Assert.True(Math.Abs(expected[i, j] - actual[i, j]) <= tolerance, $"[{i},{j}]: ожидалось {expected[i, j]}, получено {actual[i, j]}");
        }
    }
}

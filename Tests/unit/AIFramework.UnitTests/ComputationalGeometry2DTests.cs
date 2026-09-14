using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Geometry.Intersections;
using AI.Geometry.Numerics;
using AI.Geometry.Polygons;
using AI.Geometry.Polylines;
using AI.Geometry.Primitives;
using AI.Geometry.Triangulation;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Вычислительная геометрия на плоскости: точные предикаты, оболочка, триангуляции, отсечение, булевы операции.
/// </summary>
public class ComputationalGeometry2DTests
{
    private static Vector P(double x, double y) => new(x, y);

    private static Vector[] Rect(double x0, double y0, double x1, double y1) => [P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1)];

    private static double Area(IEnumerable<Vector[]> rings) => rings.Sum(ShoelaceArea.SignedArea);

    private static Vector[] RandomPoints(Random random, int count, double size) =>
        Enumerable.Range(0, count).Select(_ => P(random.NextDouble() * size, random.NextDouble() * size)).ToArray();

    // ---------- Точные предикаты ----------

    [Fact]
    public void RobustPredicates_Orient2D_NearlyCollinearGrid_MatchesExactSign()
    {
        // a = (0.5 + i·u, 0.5 + j·u), b = (12, 12), c = (24, 24): точный определитель равен 12·(j − i)·u
        double u = Math.Pow(2, -53);
        int naiveWrong = 0;
        for (int i = 0; i < 64; i++)
        {
            for (int j = 0; j < 64; j++)
            {
                double ax = 0.5 + i * u, ay = 0.5 + j * u;
                int expected = Math.Sign(j - i);
                Assert.Equal(expected, RobustPredicates.Orient2D(ax, ay, 12, 12, 24, 24));

                double naive = (12 - ax) * (24 - ay) - (12 - ay) * (24 - ax);
                if (Math.Sign(naive) != expected)
                    naiveWrong++;
            }
        }

        // Случай действительно почти вырожденный: обычная формула ошибается в знаке
        Assert.True(naiveWrong > 0);
    }

    [Fact]
    public void RobustPredicates_Orient2D_TinyAndHugeCoordinates_ExactSign()
    {
        // Произведения уходят в ноль (денормализованные числа) или в бесконечность, знак все равно верен
        Assert.Equal(1, RobustPredicates.Orient2D(0, 0, 1e-310, 0, 0, 1e-310));
        Assert.Equal(-1, RobustPredicates.Orient2D(0, 0, 0, 1e-310, 1e-310, 0));
        Assert.Equal(1, RobustPredicates.Orient2D(-1e300, -1e300, 1e300, -1e300, 0, 1e300));
        Assert.Equal(0, RobustPredicates.Orient2D(1e300, 1e300, -1e300, -1e300, 0, 0));
        Assert.Throws<ArgumentException>(() => RobustPredicates.Orient2D(0, 0, 1, 1, double.NaN, 2));
    }

    [Fact]
    public void RobustPredicates_Orient2D_RandomPoints_AgreesWithOrientation2D()
    {
        var random = new Random(1);
        for (int k = 0; k < 1000; k++)
        {
            var pts = RandomPoints(random, 3, 10);
            Assert.Equal(Orientation2D.Orient(pts[0], pts[1], pts[2]), RobustPredicates.Orient2D(pts[0], pts[1], pts[2]));
        }
    }

    [Fact]
    public void RobustPredicates_InCircle_NearlyCocircularFarFromOrigin_ExactSign()
    {
        // Окружность радиуса 5 с центром (T, T); точка d сдвигается на k единиц последнего разряда
        double t = 1 << 30;
        double ulp = Math.Pow(2, -22);
        for (int k = -20; k <= 20; k++)
        {
            int result = RobustPredicates.InCircle(t + 5, t, t, t + 5, t - 5, t, t + 3, t - 4 + k * ulp);
            Assert.Equal(Math.Sign(k), result);
        }

        // Обход по часовой стрелке меняет знак
        Assert.Equal(-1, RobustPredicates.InCircle(P(0, 1), P(1, 0), P(-1, 0), P(0, 0)));
        Assert.Equal(1, RobustPredicates.InCircle(P(1, 0), P(0, 1), P(-1, 0), P(0, 0)));
        Assert.Equal(0, RobustPredicates.InCircle(P(1, 0), P(0, 1), P(-1, 0), P(0, -1)));
    }

    // ---------- Выпуклая оболочка ----------

    [Fact]
    public void ConvexHull_Compute_DegenerateInputs_HandlesCollinearAndDuplicates()
    {
        Assert.Empty(ConvexHull.Compute([]));

        var single = ConvexHull.Compute([P(1, 1), P(1, 1), P(1, 1)]);
        Assert.Single(single);

        var line = ConvexHull.Compute([P(2, 2), P(0, 0), P(1, 1), P(3, 3), P(1, 1)]);
        Assert.Equal(2, line.Length);
        Assert.Equal([0.0, 0.0], line[0].ToArray());
        Assert.Equal([3.0, 3.0], line[1].ToArray());

        // Точки на сторонах квадрата и внутри не входят в оболочку
        var square = ConvexHull.Compute([P(0, 0), P(1, 0), P(2, 0), P(2, 1), P(2, 2), P(1, 2), P(0, 2), P(0, 1), P(1, 1), P(0, 0)]);
        Assert.Equal(4, square.Length);
        Assert.Equal(4.0, ShoelaceArea.SignedArea(square), 12);
    }

    [Fact]
    public void ConvexHull_Compute_RandomPoints_ContainsAllPointsAndIsStrictlyConvex()
    {
        var random = new Random(2);
        var points = RandomPoints(random, 500, 100).Concat(Enumerable.Range(0, 50).Select(i => P(i, 100 - i))).ToArray();
        var hull = ConvexHull.Compute(points);
        int n = hull.Length;

        for (int i = 0; i < n; i++)
        {
            Assert.Equal(1, RobustPredicates.Orient2D(hull[i], hull[(i + 1) % n], hull[(i + 2) % n]));
            foreach (var p in points)
                Assert.True(RobustPredicates.Orient2D(hull[i], hull[(i + 1) % n], p) >= 0);
        }
    }

    // ---------- Отсечение ушей ----------

    private static void AssertTriangulation(IReadOnlyList<Triangle> triangles, double expectedArea, Vector[] outer, params Vector[][] holes)
    {
        double sum = 0;
        foreach (var t in triangles)
        {
            double signed = ShoelaceArea.SignedArea([t.A, t.B, t.C]);
            Assert.True(signed > 0, "Треугольник вырожден или обходится по часовой стрелке");
            sum += signed;

            var center = (t.A + t.B + t.C) * (1.0 / 3);
            Assert.True(PointInPolygon.Contains(center, outer));
            foreach (var hole in holes)
                Assert.False(PointInPolygon.Contains(center, hole));
        }

        Assert.Equal(expectedArea, sum, 9);
    }

    [Fact]
    public void EarClipping_Triangulate_SquareWithHole_AreaEqualsPolygonArea()
    {
        var outer = Rect(0, 0, 4, 4);
        var hole = Rect(1, 1, 3, 3); // обход против часовой стрелки: должен быть развернут
        var triangles = EarClipping.Triangulate(outer, hole);

        AssertTriangulation(triangles, 12, outer, hole);
        Assert.Equal(8, triangles.Count);
    }

    [Fact]
    public void EarClipping_Triangulate_ConcaveWithCollinearAndDuplicates_AreaEqualsPolygonArea()
    {
        // Гребенка по часовой стрелке, с повтором вершины и точками на сторонах
        Vector[] comb =
        [
            P(0, 0), P(0, 3), P(1, 3), P(1, 1), P(2, 1), P(2, 3), P(3, 3), P(3, 1), P(4, 1), P(4, 3), P(5, 3),
            P(5, 0), P(5, 0), P(4, 0), P(2.5, 0),
        ];
        var triangles = EarClipping.Triangulate(comb);
        AssertTriangulation(triangles, Math.Abs(ShoelaceArea.SignedArea(comb)), comb);
    }

    [Fact]
    public void EarClipping_Triangulate_SeveralHolesInConcavePolygon_AreaEqualsPolygonArea()
    {
        Vector[] outer = [P(0, 0), P(10, 0), P(10, 10), P(6, 10), P(6, 5), P(4, 5), P(4, 10), P(0, 10)];
        Vector[] hole1 = [P(1, 1), P(3, 1), P(3, 3), P(1, 3)];
        Vector[] hole2 = [P(7, 1), P(9, 1), P(8, 4)];
        Vector[] hole3 = [P(1, 6), P(3, 6), P(3, 8), P(2, 9), P(1, 8)];
        var triangles = EarClipping.Triangulate(outer, hole1, hole2, hole3);

        double expected = ShoelaceArea.Area(outer) - ShoelaceArea.Area(hole1) - ShoelaceArea.Area(hole2) - ShoelaceArea.Area(hole3);
        AssertTriangulation(triangles, expected, outer, hole1, hole2, hole3);
    }

    [Fact]
    public void EarClipping_Triangulate_HoleAlignedWithOuterVertex_AreaEqualsPolygonArea()
    {
        // Луч из крайней вершины дыры попадает точно в вершину внешнего контура
        Vector[] outer = [P(0, 0), P(4, 0), P(6, 2), P(4, 4), P(0, 4)];
        Vector[] hole = [P(1, 1), P(2, 2), P(1, 3)];
        var triangles = EarClipping.Triangulate(outer, hole);
        AssertTriangulation(triangles, ShoelaceArea.Area(outer) - ShoelaceArea.Area(hole), outer, hole);
    }

    // ---------- Делоне и Вороной ----------

    private static void AssertDelaunay(Vector[] points, IReadOnlyList<(int A, int B, int C)> triangles)
    {
        double area = 0;
        foreach (var (a, b, c) in triangles)
        {
            Assert.Equal(1, RobustPredicates.Orient2D(points[a], points[b], points[c]));
            area += ShoelaceArea.SignedArea([points[a], points[b], points[c]]);

            // Пустой описанный круг
            foreach (var p in points)
                Assert.True(RobustPredicates.InCircle(points[a], points[b], points[c], p) <= 0);
        }

        // Треугольники покрывают выпуклую оболочку без наложений
        Assert.Equal(ShoelaceArea.Area(ConvexHull.Compute(points)), area, 8);
    }

    [Fact]
    public void Delaunay_Triangulate_RandomPoints_EmptyCircumcircles()
    {
        var points = RandomPoints(new Random(3), 300, 1000);
        AssertDelaunay(points, Delaunay.Triangulate(points));
    }

    [Fact]
    public void Delaunay_Triangulate_GridWithCocircularPoints_ValidTriangulation()
    {
        var points = (from i in Enumerable.Range(0, 6) from j in Enumerable.Range(0, 6) select P(i, j)).ToArray();
        var triangles = Delaunay.Triangulate(points);

        AssertDelaunay(points, triangles);
        Assert.Equal(2 * 36 - 2 - 20, triangles.Count); // 20 точек на границе оболочки
    }

    [Fact]
    public void Delaunay_Triangulate_CollinearAndDuplicatePoints_HandlesDegenerateInput()
    {
        Assert.Empty(Delaunay.Triangulate([P(0, 0), P(1, 1), P(2, 2), P(3, 3)]));
        Assert.Empty(Delaunay.Triangulate([P(0, 0), P(0, 0), P(1, 0)]));

        // Сначала точки на одной прямой, затем выход из нее, с повторами
        Vector[] points = [P(0, 0), P(1, 0), P(2, 0), P(3, 0), P(1, 0), P(1.5, 2), P(1.5, -2), P(3, 0)];
        var triangles = Delaunay.Triangulate(points);
        AssertDelaunay(points, triangles);
        Assert.DoesNotContain(triangles, t => t.A == 4 || t.B == 4 || t.C == 4 || t.A == 7 || t.B == 7 || t.C == 7);
    }

    [Fact]
    public void Voronoi_Cells_RandomPoints_PartitionBoxByNearestSite()
    {
        var random = new Random(4);
        var points = RandomPoints(random, 60, 10);
        var cells = Voronoi.Cells(points, P(0, 0), P(10, 10));

        Assert.Equal(100.0, cells.Sum(ShoelaceArea.SignedArea), 8);
        for (int i = 0; i < points.Length; i++)
            Assert.True(PolygonDistance.Distance(points[i], cells[i]) <= 1e-9);

        for (int k = 0; k < 300; k++)
        {
            var q = P(random.NextDouble() * 10, random.NextDouble() * 10);
            int nearest = Enumerable.Range(0, points.Length).MinBy(i => Vector.Dot(points[i] - q, points[i] - q));
            Assert.True(PolygonDistance.Distance(q, cells[nearest]) <= 1e-9);
        }
    }

    [Fact]
    public void Voronoi_Cells_GridAndCollinearSites_EqualCells()
    {
        var grid = (from i in Enumerable.Range(0, 3) from j in Enumerable.Range(0, 3) select P(i + 0.5, j + 0.5)).ToArray();
        var cells = Voronoi.Cells(grid, P(0, 0), P(3, 3));
        foreach (var cell in cells)
            Assert.Equal(1.0, ShoelaceArea.SignedArea(cell), 12);

        // Точки на одной прямой и повтор: полосы
        var line = Voronoi.Cells([P(1, 1), P(3, 1), P(3, 1)], P(0, 0), P(4, 2));
        Assert.Equal(4.0, ShoelaceArea.SignedArea(line[0]), 12);
        Assert.Equal(4.0, ShoelaceArea.SignedArea(line[1]), 12);
        Assert.Equal(4.0, ShoelaceArea.SignedArea(line[2]), 12);
    }

    // ---------- Отсечение и сумма Минковского ----------

    [Fact]
    public void PolygonClipping_SutherlandHodgman_OverlapAndTouching_ExpectedArea()
    {
        Assert.Equal(1.0, ShoelaceArea.SignedArea(PolygonClipping.SutherlandHodgman(Rect(0, 0, 2, 2), Rect(1, 1, 3, 3))), 12);

        // Отсекатель по часовой стрелке: ромб |x| + |y| ≤ 1 внутри квадрата [−1, 1]²
        Vector[] diamond = [P(0, 1), P(1, 0), P(0, -1), P(-1, 0)];
        Assert.Equal(2.0, ShoelaceArea.Area(PolygonClipping.SutherlandHodgman(Rect(-1, -1, 1, 1), diamond)), 12);

        // Касание по стороне: площадь нулевая
        Assert.Equal(0.0, ShoelaceArea.Area(PolygonClipping.SutherlandHodgman(Rect(0, 0, 1, 1), Rect(1, 0, 2, 1))), 12);

        // Совпадающие многоугольники
        Assert.Equal(1.0, ShoelaceArea.Area(PolygonClipping.SutherlandHodgman(Rect(0, 0, 1, 1), Rect(0, 0, 1, 1))), 12);
    }

    [Fact]
    public void PolygonClipping_HalfPlane_KeepsLeftSide()
    {
        // Прямая x = 1 вверх: слева остается x ≤ 1
        var left = PolygonClipping.HalfPlane(Rect(0, 0, 4, 2), new Line2D(P(1, 0), P(0, 1)));
        Assert.Equal(2.0, ShoelaceArea.SignedArea(left), 12);

        var none = PolygonClipping.HalfPlane(Rect(0, 0, 4, 2), new Line2D(P(-1, 0), P(0, 1)));
        Assert.Empty(none);
    }

    [Fact]
    public void MinkowskiSum_Convex_MatchesHullOfPairwiseSums()
    {
        var sq = MinkowskiSum.Convex(Rect(0, 0, 1, 1), Rect(0, 0, 1, 1));
        Assert.Equal(4, sq.Length);
        Assert.Equal(4.0, ShoelaceArea.SignedArea(sq), 12);

        var random = new Random(5);
        for (int k = 0; k < 50; k++)
        {
            var a = ConvexHull.Compute(RandomPoints(random, 8, 10));
            var b = ConvexHull.Compute(RandomPoints(random, 8, 10));
            var sum = MinkowskiSum.Convex(a, b);
            var reference = ConvexHull.Compute(a.SelectMany(u => b.Select(v => u + v)).ToArray());

            Assert.Equal(reference.Length, sum.Length);
            for (int i = 0; i < sum.Length; i++)
            {
                Assert.Equal(reference[i][0], sum[i][0], 9);
                Assert.Equal(reference[i][1], sum[i][1], 9);
            }
        }

        // Точка сдвигает многоугольник, отрезок вытягивает его
        Assert.Equal(1.0, ShoelaceArea.SignedArea(MinkowskiSum.Convex(Rect(0, 0, 1, 1), [P(5, 5)])), 12);
        Assert.Equal(3.0, ShoelaceArea.SignedArea(MinkowskiSum.Convex(Rect(0, 0, 1, 1), [P(0, 0), P(2, 0)])), 12);
    }

    // ---------- Булевы операции ----------

    [Fact]
    public void PolygonBoolean_OverlappingSquares_ExpectedAreas()
    {
        var a = Rect(0, 0, 2, 2);
        var b = Rect(1, 1, 3, 3);
        Assert.Equal(1.0, Area(PolygonBoolean.Intersection(a, b)), 12);
        Assert.Equal(7.0, Area(PolygonBoolean.Union(a, b)), 12);
        Assert.Equal(3.0, Area(PolygonBoolean.Difference(a, b)), 12);
        Assert.Single(PolygonBoolean.Union(a, b));
    }

    [Fact]
    public void PolygonBoolean_IdenticalPolygons_SharedEdges()
    {
        var a = Rect(0, 0, 2, 2);
        Vector[] b = [P(0, 2), P(2, 2), P(2, 0), P(0, 0)]; // тот же квадрат по часовой стрелке

        var intersection = PolygonBoolean.Intersection(a, b);
        Assert.Single(intersection);
        Assert.Equal(4, intersection[0].Length);
        Assert.Equal(4.0, Area(intersection), 12);
        Assert.Equal(4.0, Area(PolygonBoolean.Union(a, b)), 12);
        Assert.Empty(PolygonBoolean.Difference(a, b));
    }

    [Fact]
    public void PolygonBoolean_TouchingAlongEdge_MergesWithoutSeam()
    {
        var a = Rect(0, 0, 1, 1);
        var b = Rect(1, 0, 2, 1);

        var union = PolygonBoolean.Union(a, b);
        Assert.Single(union);
        Assert.Equal(4, union[0].Length); // точки шва на прямой удалены
        Assert.Equal(2.0, Area(union), 12);
        Assert.Empty(PolygonBoolean.Intersection(a, b));
        Assert.Equal(1.0, Area(PolygonBoolean.Difference(a, b)), 12);
    }

    [Fact]
    public void PolygonBoolean_PartiallyOverlappingCollinearEdges_ExpectedAreas()
    {
        // Верхние и нижние стороны лежат на одних прямых и перекрываются частично; у b сторона с T-стыком
        var a = Rect(0, 0, 2, 1);
        Vector[] b = [P(1, 0), P(3, 0), P(3, 1), P(2, 1), P(1, 1)];

        var intersection = PolygonBoolean.Intersection(a, b);
        Assert.Single(intersection);
        Assert.Equal(4, intersection[0].Length);
        Assert.Equal(1.0, Area(intersection), 12);
        Assert.Equal(3.0, Area(PolygonBoolean.Union(a, b)), 12);
        Assert.Equal(1.0, Area(PolygonBoolean.Difference(a, b)), 12);
        Assert.Equal(1.0, Area(PolygonBoolean.Difference(b, a)), 12);
    }

    [Fact]
    public void PolygonBoolean_TouchingAtVertex_SeparateRings()
    {
        var a = Rect(0, 0, 1, 1);
        var b = Rect(1, 1, 2, 2);

        var union = PolygonBoolean.Union(a, b);
        Assert.Equal(2, union.Count);
        Assert.Equal(2.0, Area(union), 12);
        Assert.Empty(PolygonBoolean.Intersection(a, b));
        Assert.Equal(1.0, Area(PolygonBoolean.Difference(a, b)), 12);
    }

    [Fact]
    public void PolygonBoolean_ContainedPolygon_HoleAndNotch()
    {
        var outer = Rect(0, 0, 4, 4);

        // Строго внутри: разность дает дыру по часовой стрелке
        var inner = Rect(1, 1, 2, 2);
        var withHole = PolygonBoolean.Difference(outer, inner);
        Assert.Equal(2, withHole.Count);
        Assert.Contains(withHole, ring => ShoelaceArea.SignedArea(ring) < 0);
        Assert.Equal(15.0, Area(withHole), 12);
        Assert.Equal(1.0, Area(PolygonBoolean.Intersection(outer, inner)), 12);
        Assert.Equal(16.0, Area(PolygonBoolean.Union(outer, inner)), 12);

        // Касается стороной: разность дает выемку одним контуром
        var notch = Rect(0, 1, 2, 2);
        var notched = PolygonBoolean.Difference(outer, notch);
        Assert.Single(notched);
        Assert.Equal(14.0, Area(notched), 12);

        // Касается вершиной изнутри
        Vector[] corner = [P(0, 0), P(1, 2), P(2, 1)];
        Assert.Equal(16.0 - 1.5, Area(PolygonBoolean.Difference(outer, corner)), 12);
        Assert.Equal(1.5, Area(PolygonBoolean.Intersection(outer, corner)), 12);
    }

    [Fact]
    public void PolygonBoolean_SharedVerticesAndCrossings_ExpectedAreas()
    {
        // Треугольник и квадрат с общей вершиной и стороной через вершину квадрата
        var square = Rect(0, 0, 2, 2);
        Vector[] triangle = [P(0, 0), P(4, 0), P(0, 4)];
        Assert.Equal(4.0, Area(PolygonBoolean.Intersection(square, triangle)), 12);
        Assert.Equal(8.0, Area(PolygonBoolean.Union(square, triangle)), 12);
        Assert.Empty(PolygonBoolean.Difference(square, triangle));

        // Звезда Давида: два треугольника с собственными пересечениями
        Vector[] up = [P(0, 0), P(6, 0), P(3, 6)];
        Vector[] down = [P(0, 4), P(3, -2), P(6, 4)];
        double inter = Area(PolygonBoolean.Intersection(up, down));
        Assert.Equal(ShoelaceArea.Area(up) + ShoelaceArea.Area(down) - inter, Area(PolygonBoolean.Union(up, down)), 9);
        Assert.Equal(ShoelaceArea.Area(PolygonClipping.SutherlandHodgman(up, down)), inter, 9);
    }

    [Fact]
    public void PolygonBoolean_RandomGridPolygons_AreaIdentitiesHold()
    {
        // Целочисленная решетка дает много вырождений: общие вершины, стороны на одной прямой, касания
        var random = new Random(6);
        for (int k = 0; k < 300; k++)
        {
            var a = RandomGridPolygon(random);
            var b = RandomGridPolygon(random);
            double areaA = ShoelaceArea.Area(a), areaB = ShoelaceArea.Area(b);

            double inter = Area(PolygonBoolean.Intersection(a, b));
            double union = Area(PolygonBoolean.Union(a, b));
            double diffAB = Area(PolygonBoolean.Difference(a, b));
            double diffBA = Area(PolygonBoolean.Difference(b, a));

            Assert.True(inter >= -1e-9);
            Assert.Equal(areaA + areaB, union + inter, 9);
            Assert.Equal(areaA, diffAB + inter, 9);
            Assert.Equal(areaB, diffBA + inter, 9);

            if (k % 3 == 0)
            {
                // Выпуклые входы: независимая проверка пересечения
                var ha = ConvexHull.Compute(a);
                var hb = ConvexHull.Compute(b);
                Assert.Equal(
                    ShoelaceArea.Area(PolygonClipping.SutherlandHodgman(ha, hb)),
                    Area(PolygonBoolean.Intersection(ha, hb)),
                    9);
            }
        }
    }

    [Fact]
    public void PolygonBoolean_RandomStarPolygons_AreaIdentitiesHold()
    {
        // Звездные многоугольники с произвольными координатами: много собственных пересечений сторон
        var random = new Random(7);
        for (int k = 0; k < 200; k++)
        {
            var a = RandomStar(random, P(random.NextDouble() * 2, random.NextDouble() * 2), random.Next(3, 12));
            var b = RandomStar(random, P(random.NextDouble() * 2, random.NextDouble() * 2), random.Next(3, 12));
            double areaA = ShoelaceArea.Area(a), areaB = ShoelaceArea.Area(b);

            double inter = Area(PolygonBoolean.Intersection(a, b));
            Assert.Equal(areaA + areaB, Area(PolygonBoolean.Union(a, b)) + inter, 8);
            Assert.Equal(areaA, Area(PolygonBoolean.Difference(a, b)) + inter, 8);
            Assert.Equal(areaB, Area(PolygonBoolean.Difference(b, a)) + inter, 8);
        }
    }

    [Fact]
    public void EarClipping_Triangulate_RandomStarPolygonsWithHole_AreaEqualsPolygonArea()
    {
        var random = new Random(8);
        for (int k = 0; k < 100; k++)
        {
            // От шести вершин угловой зазор меньше 108°, и центр звезды остается внутри
            var outer = RandomStar(random, P(0, 0), random.Next(6, 40));
            Assert.Equal(ShoelaceArea.Area(outer), EarClipping.Triangulate(outer).Sum(t => ShoelaceArea.SignedArea([t.A, t.B, t.C])), 9);

            // Малая дыра у центра звезды лежит внутри при любых лучах (радиусы не меньше 1)
            var hole = Polygonization.FromCircle(new Circle(P(0.1, -0.05), 0.3), random.Next(3, 10));
            AssertTriangulation(EarClipping.Triangulate(outer, hole), ShoelaceArea.Area(outer) - ShoelaceArea.Area(hole), outer, hole);
        }
    }

    /// <summary>Звездный относительно центра многоугольник: углы по возрастанию, радиусы от 1 до 3.</summary>
    private static Vector[] RandomStar(Random random, Vector center, int count)
    {
        var angles = Enumerable.Range(0, count).Select(i => 2 * Math.PI * (i + 0.8 * random.NextDouble()) / count).ToArray();
        return angles.Select(angle =>
        {
            double radius = 1 + 2 * random.NextDouble();
            return P(center[0] + radius * Math.Cos(angle), center[1] + radius * Math.Sin(angle));
        }).ToArray();
    }

    /// <summary>Прямоугольник, L-образная фигура или треугольник на решетке 0..4.</summary>
    private static Vector[] RandomGridPolygon(Random random)
    {
        int x0 = random.Next(0, 3), y0 = random.Next(0, 3);
        int x1 = random.Next(x0 + 1, 5), y1 = random.Next(y0 + 1, 5);
        switch (random.Next(3))
        {
            case 0:
                return Rect(x0, y0, x1, y1);
            case 1:
                if (x1 - x0 < 2 || y1 - y0 < 2)
                    return Rect(x0, y0, x1, y1);
                return [P(x0, y0), P(x1, y0), P(x1, y0 + 1), P(x0 + 1, y0 + 1), P(x0 + 1, y1), P(x0, y1)];
            default:
                return random.Next(2) == 0 ? [P(x0, y0), P(x1, y0), P(x0, y1)] : [P(x1, y1), P(x0, y1), P(x1, y0)];
        }
    }

    // ---------- Ломаные, окружности, отрезки ----------

    [Fact]
    public void DouglasPeucker_Simplify_DropsSmallDeviationsKeepsCorners()
    {
        Vector[] noisy = [P(0, 0), P(1, 0.01), P(2, -0.01), P(3, 0.02), P(4, 0)];
        Assert.Equal(2, DouglasPeucker.Simplify(noisy, 0.05).Length);

        Vector[] corner = [P(0, 0), P(1, 0.01), P(2, 0), P(2, 1), P(2.01, 2), P(2, 3)];
        var simplified = DouglasPeucker.Simplify(corner, 0.05);
        Assert.Equal(3, simplified.Length);
        Assert.Equal([2.0, 0.0], simplified[1].ToArray());

        // Замкнутая ломаная: концы совпадают
        Vector[] closed = [P(0, 0), P(1, 0), P(1, 1), P(0, 1), P(0, 0)];
        Assert.Equal(5, DouglasPeucker.Simplify(closed, 0.1).Length);
    }

    [Fact]
    public void CircleCircleIntersection_Intersect_CrossingTangentAndSeparate()
    {
        var two = CircleCircleIntersection.Intersect(new Circle(P(0, 0), 5), new Circle(P(8, 0), 5));
        Assert.Equal(2, two.Length);
        Assert.Equal([4.0, 3.0], two[0].ToArray());
        Assert.Equal([4.0, -3.0], two[1].ToArray());

        var tangent = CircleCircleIntersection.Intersect(new Circle(P(0, 0), 1), new Circle(P(2, 0), 1));
        Assert.Single(tangent);
        Assert.Equal(1.0, tangent[0][0], 12);

        var inner = CircleCircleIntersection.Intersect(new Circle(P(0, 0), 2), new Circle(P(1, 0), 1));
        Assert.Single(inner);
        Assert.Equal(2.0, inner[0][0], 12);

        Assert.Empty(CircleCircleIntersection.Intersect(new Circle(P(0, 0), 1), new Circle(P(3, 0), 1)));
        Assert.Empty(CircleCircleIntersection.Intersect(new Circle(P(0, 0), 3), new Circle(P(0.5, 0), 1)));
        Assert.Empty(CircleCircleIntersection.Intersect(new Circle(P(0, 0), 1), new Circle(P(0, 0), 1)));
    }

    [Fact]
    public void CircleLineIntersection_Intersect_LineAndSegment()
    {
        var circle = new Circle(P(0, 0), 5);

        var line = CircleLineIntersection.Intersect(circle, new Line2D(P(-10, 3), P(1, 0)));
        Assert.Equal(2, line.Length);
        Assert.Equal(-4.0, line[0][0], 12);
        Assert.Equal(4.0, line[1][0], 12);

        Assert.Single(CircleLineIntersection.Intersect(circle, new Line2D(P(0, 5), P(3, 0))));
        Assert.Empty(CircleLineIntersection.Intersect(circle, new Line2D(P(0, 6), P(1, 0))));

        var segment = CircleLineIntersection.Intersect(circle, new Segment(P(0, 3), P(10, 3)));
        Assert.Single(segment);
        Assert.Equal(4.0, segment[0][0], 12);
        Assert.Empty(CircleLineIntersection.Intersect(circle, new Segment(P(0, 0), P(1, 1))));
        Assert.Single(CircleLineIntersection.Intersect(circle, new Segment(P(3, 4), P(3, 4))));
    }

    [Fact]
    public void SegmentSegmentIntersection_CommonPart_CollinearOverlapAndPoints()
    {
        var s1 = new Segment(P(0, 0), P(4, 0));
        var s2 = new Segment(P(6, 0), P(2, 0));

        // Прежний метод теряет перекрытие, новый возвращает его
        Assert.Null(SegmentSegmentIntersection.Intersect(s1, s2));
        var overlap = SegmentSegmentIntersection.CommonPart(s1, s2);
        Assert.NotNull(overlap);
        Assert.Equal([2.0, 0.0], overlap.A.ToArray());
        Assert.Equal([4.0, 0.0], overlap.B.ToArray());

        var contained = SegmentSegmentIntersection.CommonPart(new Segment(P(1, 1), P(2, 2)), new Segment(P(0, 0), P(3, 3)));
        Assert.Equal([1.0, 1.0], contained.A.ToArray());
        Assert.Equal([2.0, 2.0], contained.B.ToArray());

        var touch = SegmentSegmentIntersection.CommonPart(new Segment(P(0, 0), P(1, 0)), new Segment(P(1, 0), P(2, 0)));
        Assert.Equal([1.0, 0.0], touch.A.ToArray());
        Assert.Equal([1.0, 0.0], touch.B.ToArray());

        var cross = SegmentSegmentIntersection.CommonPart(new Segment(P(0, 0), P(2, 2)), new Segment(P(0, 2), P(2, 0)));
        Assert.Equal(1.0, cross.A[0], 12);
        Assert.Equal(1.0, cross.B[1], 12);

        Assert.Null(SegmentSegmentIntersection.CommonPart(new Segment(P(0, 0), P(1, 0)), new Segment(P(2, 0), P(3, 0))));
        Assert.Null(SegmentSegmentIntersection.CommonPart(new Segment(P(0, 0), P(1, 0)), new Segment(P(0, 1), P(1, 1))));
        Assert.Null(SegmentSegmentIntersection.CommonPart(new Segment(P(0, 0), P(1, 0)), new Segment(P(2, 1), P(2, -1))));
        Assert.NotNull(SegmentSegmentIntersection.CommonPart(new Segment(P(1, 0), P(1, 0)), s1));
    }

    [Fact]
    public void PolygonDistance_Distance_ZeroInsideBoundaryDistanceOutside()
    {
        var square = Rect(0, 0, 2, 2);
        Assert.Equal(0.0, PolygonDistance.Distance(P(1, 1), square));
        Assert.Equal(1.0, PolygonDistance.Distance(P(3, 1), square), 12);
        Assert.Equal(Math.Sqrt(2), PolygonDistance.Distance(P(3, 3), square), 12);

        // Невыпуклый: точка в выемке снаружи
        Vector[] u = [P(0, 0), P(3, 0), P(3, 3), P(2, 3), P(2, 1), P(1, 1), P(1, 3), P(0, 3)];
        Assert.Equal(0.5, PolygonDistance.Distance(P(1.5, 2), u), 12);
    }

    [Fact]
    public void Polygonization_FromCircleAndEllipse_VerticesOnCurve()
    {
        var circle = Polygonization.FromCircle(new Circle(P(1, 2), 3), 64);
        Assert.Equal(64, circle.Length);
        foreach (var v in circle)
            Assert.Equal(3.0, Math.Sqrt(Vector.Dot(v - P(1, 2), v - P(1, 2))), 12);
        Assert.Equal(32 * 9 * Math.Sin(2 * Math.PI / 64), ShoelaceArea.SignedArea(circle), 9);

        var ellipse = new Ellipse(P(0, 0), 4, 2, Math.PI / 6);
        var polygon = Polygonization.FromEllipse(ellipse, 36);
        foreach (var v in polygon)
        {
            double along = v[0] * Math.Cos(Math.PI / 6) + v[1] * Math.Sin(Math.PI / 6);
            double across = -v[0] * Math.Sin(Math.PI / 6) + v[1] * Math.Cos(Math.PI / 6);
            Assert.Equal(1.0, along * along / 16 + across * across / 4, 12);
        }

        Assert.Equal(18 * 8 * Math.Sin(2 * Math.PI / 36), ShoelaceArea.SignedArea(polygon), 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => Polygonization.FromCircle(new Circle(P(0, 0), 1), 2));
    }
}

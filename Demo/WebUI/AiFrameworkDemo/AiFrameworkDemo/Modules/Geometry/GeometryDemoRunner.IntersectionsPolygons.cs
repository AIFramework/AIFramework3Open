using AI.Charts;
using AI.Geometry.Conics;
using AI.Geometry.Curves;
using AI.Geometry.Distances;
using AI.Geometry.Fitting;
using AI.Geometry.Intersections;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;
using Matrix = AI.DataStructs.Algebraic.Matrix;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using Quaternion = AI.Geometry.Transforms.Quaternion;

namespace AiFrameworkDemo.Modules.Geometry;

public static partial class GeometryDemoRunner
{
    // -- Intersections -----------------------------------------------

    private static void DoPointToLine(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 15);
        var rng = new Random(I(p, "seed", 42));
        var line = Line2D.FromTwoPoints(new Vector(new[] { -2.0, -1.0 }), new Vector(new[] { 3.0, 2.0 }));
        var lx = new Vector(new[] { -3.0, 4.0 });
        var ly = new Vector(2);
        for (int i = 0; i < 2; i++)
        {
            double t = (lx[i] - line.Point[0]) / line.Direction[0];
            ly[i] = line.Point[1] + t * line.Direction[1];
        }
        cv.AddPlot(lx, ly, "Прямая", Pal[0], width: 2);
        var ptx = new Vector(n); var pty = new Vector(n);
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double x = rng.NextDouble() * 6 - 3, y = rng.NextDouble() * 6 - 3;
            ptx[i] = x; pty[i] = y;
            sum += PointLine.Distance2D(new Vector(new[] { x, y }), line);
        }
        cv.AddScatterMark3(ptx, pty, $"Точки (ср. {sum / n:F2})", Pal[1]);
        cv.ChartName = "Расстояние точка -> прямая 2D";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static void DoRayTriangle(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int nRays = I(p, "rays", 8);
        var rng = new Random(I(p, "seed", 42));
        var tri = new Triangle(
            new Vector(new[] { 1.0, 1, 0 }), new Vector(new[] { 4.0, 1, 0 }), new Vector(new[] { 2.5, 4, 0 }));
        cv.AddPlot(
            new Vector(new[] { tri.A[0], tri.B[0], tri.C[0], tri.A[0] }),
            new Vector(new[] { tri.A[1], tri.B[1], tri.C[1], tri.A[1] }),
            "Треугольник", Pal[0], width: 2);
        int hits = 0;
        var hitX = new Vector(nRays); var hitY = new Vector(nRays);
        var missX = new Vector(nRays); var missY = new Vector(nRays);
        int hc = 0, mc = 0;
        for (int i = 0; i < nRays; i++)
        {
            double ox = rng.NextDouble() * 5, oy = rng.NextDouble() * 5;
            var ray = new Ray(new Vector(new[] { ox, oy, -1.0 }), new Vector(new[] { 0.0, 0, 1.0 }));
            bool hit = RayTriangleIntersection.Intersect(ray, tri).HasValue;
            if (hit) { hitX[hc] = ox; hitY[hc] = oy; hc++; hits++; }
            else { missX[mc] = ox; missY[mc] = oy; mc++; }
        }
        if (hc > 0) cv.AddScatterMark3(hitX.CutAndZero(hc), hitY.CutAndZero(hc), $"Попадание ({hc})", Pal[2]);
        if (mc > 0) cv.AddScatterMark3(missX.CutAndZero(mc), missY.CutAndZero(mc), $"Промах ({mc})", Pal[1]);
        cv.ChartName = $"Möller–Trumbore: {hits}/{nRays} попаданий";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static string DoAabbObb(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 6);
        var rng = new Random(I(p, "seed", 42));
        var boxes = new Aabb[n];
        for (int i = 0; i < n; i++)
        {
            double cx = rng.NextDouble() * 8, cy = rng.NextDouble() * 8;
            double hw = 0.3 + rng.NextDouble() * 1.5, hh = 0.3 + rng.NextDouble() * 1.5;
            boxes[i] = new Aabb(new Vector(new[] { cx - hw, cy - hh }), new Vector(new[] { cx + hw, cy + hh }));
            var bx = new Vector(new[] { boxes[i].Min[0], boxes[i].Max[0], boxes[i].Max[0], boxes[i].Min[0], boxes[i].Min[0] });
            var by = new Vector(new[] { boxes[i].Min[1], boxes[i].Min[1], boxes[i].Max[1], boxes[i].Max[1], boxes[i].Min[1] });
            cv.AddPlot(bx, by, i == 0 ? "AABB" : "", Pal[i % Pal.Length], width: 2);
        }
        int coll = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (AabbAabbIntersection.Test(boxes[i], boxes[j])) coll++;
        cv.ChartName = $"AABB-тест: {coll} коллизий из {n} боксов";
        cv.LabelX = "x"; cv.LabelY = "y";
        return $"Боксов: {n}, коллизий: {coll}";
    }

    // -- Polygons ----------------------------------------------------

    private static string DoShoelace(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 6);
        var rng = new Random(I(p, "seed", 42));
        var angles = new double[n];
        for (int i = 0; i < n; i++) angles[i] = rng.NextDouble() * 2 * Math.PI;
        Array.Sort(angles);
        var poly = new Vector[n];
        double cx = 3, cy = 3;
        var pX = new Vector(n + 1); var pY = new Vector(n + 1);
        for (int i = 0; i < n; i++)
        {
            double r = 2 * (0.6 + 0.4 * rng.NextDouble());
            poly[i] = new Vector(new[] { cx + r * Math.Cos(angles[i]), cy + r * Math.Sin(angles[i]) });
            pX[i] = poly[i][0]; pY[i] = poly[i][1];
        }
        pX[n] = poly[0][0]; pY[n] = poly[0][1];
        double area = ShoelaceArea.Area(poly);
        cv.AddPlot(pX, pY, $"Полигон (S = {area:F3})", Pal[0], width: 2);
        var cen = PolygonCentroid.Centroid(poly);
        cv.AddScatterMark3(new Vector(new[] { cen[0] }), new Vector(new[] { cen[1] }), "Центроид", Pal[2]);
        cv.ChartName = $"Shoelace: S = {area:F3}";
        cv.LabelX = "x"; cv.LabelY = "y";
        return $"Площадь = {area:F6}\nЦентроид = ({cen[0]:F3}, {cen[1]:F3})";
    }

    private static void DoPointInPoly(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 80);
        var rng = new Random(I(p, "seed", 42));
        int sides = 5;
        var poly = new Vector[sides * 2];
        for (int i = 0; i < sides * 2; i++)
        {
            double angle = Math.PI * i / sides - Math.PI / 2;
            double r = i % 2 == 0 ? 2.5 : 1.0;
            poly[i] = new Vector(new[] { 3 + r * Math.Cos(angle), 3 + r * Math.Sin(angle) });
        }
        DrawClosedPoly(cv, poly, "Полигон", Pal[0]);
        var inX = new Vector(n); var inY = new Vector(n);
        var outX = new Vector(n); var outY = new Vector(n);
        int ic = 0, oc = 0;
        for (int i = 0; i < n; i++)
        {
            var pt = new Vector(new[] { rng.NextDouble() * 6, rng.NextDouble() * 6 });
            if (PointInPolygon.Contains(pt, poly)) { inX[ic] = pt[0]; inY[ic] = pt[1]; ic++; }
            else { outX[oc] = pt[0]; outY[oc] = pt[1]; oc++; }
        }
        if (ic > 0) cv.AddScatterMark3(inX.CutAndZero(ic), inY.CutAndZero(ic), $"Внутри ({ic})", Pal[2]);
        if (oc > 0) cv.AddScatterMark3(outX.CutAndZero(oc), outY.CutAndZero(oc), $"Снаружи ({oc})", Pal[1]);
        cv.ChartName = $"Точка в полигоне: {ic} внутри, {oc} снаружи";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static void DoClosestTri(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 25);
        var rng = new Random(I(p, "seed", 42));
        var A = new Vector(new[] { 1.0, 1.0 }); var B = new Vector(new[] { 5.0, 1.0 }); var C = new Vector(new[] { 3.0, 4.5 });
        cv.AddPlot(
            new Vector(new[] { A[0], B[0], C[0], A[0] }),
            new Vector(new[] { A[1], B[1], C[1], A[1] }),
            "Треугольник", Pal[0], width: 2);
        for (int i = 0; i < n; i++)
        {
            var pt = new Vector(new[] { rng.NextDouble() * 6, rng.NextDouble() * 6 });
            var cl = ClosestInTriangle.ClosestPoint(pt, A, B, C);
            cv.AddPlot(new Vector(new[] { pt[0], cl[0] }), new Vector(new[] { pt[1], cl[1] }),
                i == 0 ? "Проекция" : "", new SKColor(180, 180, 220, 100), width: 1);
            cv.AddScatterMark3(new Vector(new[] { pt[0] }), new Vector(new[] { pt[1] }), i == 0 ? "Точка" : "", Pal[1]);
            cv.AddScatterMark3(new Vector(new[] { cl[0] }), new Vector(new[] { cl[1] }), i == 0 ? "Ближайшая" : "", Pal[2]);
        }
        cv.ChartName = "Ближайшая точка в ^";
        cv.LabelX = "x"; cv.LabelY = "y";
    }
}

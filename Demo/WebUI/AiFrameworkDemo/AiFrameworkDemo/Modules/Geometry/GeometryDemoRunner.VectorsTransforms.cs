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
    // -- Vectors -----------------------------------------------------

    private static string DoVectorNorms(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int dim = I(p, "dim", 3);
        var rng = new Random(I(p, "seed", 42));
        var v = new Vector(dim);
        for (int i = 0; i < dim; i++) v[i] = rng.NextDouble() * 6 - 3;

        double l1 = v.NormL1(), l2 = v.NormL2(), linf = v.MaxAbs();
        var unit = v.GetUnitVector();

        var sb = new StringBuilder();
        sb.AppendLine($"v = [{Fmt(v)}]");
        sb.AppendLine($"‖v‖₁ = {l1:F4}");
        sb.AppendLine($"‖v‖₂ = {l2:F4}");
        sb.AppendLine($"‖v‖∞ = {linf:F4}");
        sb.AppendLine($"v̂   = [{Fmt(unit)}]");

        if (dim >= 2)
        {
            int steps = 100;
            var xC = new Vector(steps + 1); var yC = new Vector(steps + 1);
            var xD = new Vector(steps + 1); var yD = new Vector(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                double t = 2 * Math.PI * i / steps;
                double cs = Math.Cos(t), sn = Math.Sin(t);
                xC[i] = cs; yC[i] = sn;
                double den = Math.Abs(cs) + Math.Abs(sn);
                xD[i] = cs / den; yD[i] = sn / den;
            }
            cv.AddPlot(xC, yC, "L₂ = 1", Pal[0], width: 1);
            cv.AddPlot(xD, yD, "L₁ = 1", Pal[1], width: 1);
            cv.AddPlot(new Vector(new[] { 0.0, v[0] }), new Vector(new[] { 0.0, v[1] }), $"v ({l2:F2})", Pal[2], width: 3);
            cv.AddPlot(new Vector(new[] { 0.0, unit[0] }), new Vector(new[] { 0.0, unit[1] }), "v̂", Pal[3], width: 2);
            cv.ChartName = "Нормы и единичные «шары»";
            cv.LabelX = "x₁"; cv.LabelY = "x₂";
            double mx = Math.Max(Math.Abs(v[0]), Math.Abs(v[1])) + 0.5;
            cv.SetAxisRange(-mx, mx, -mx, mx);
        }
        return sb.ToString();
    }

    private static void DoLerpSlerp(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int steps = I(p, "steps", 12);
        var a = new Vector(new[] { 2.0, 0.0 });
        var b = new Vector(new[] { 0.0, 2.0 });
        var lx = new Vector(steps); var ly = new Vector(steps);
        var sx = new Vector(steps); var sy = new Vector(steps);
        for (int i = 0; i < steps; i++)
        {
            double t = (double)i / (steps - 1);
            var vl = Vector.Lerp(a, b, t); var vs = Vector.Slerp(a, b, t);
            lx[i] = vl[0]; ly[i] = vl[1]; sx[i] = vs[0]; sy[i] = vs[1];
        }
        cv.AddScatterMark3(lx, ly, "Lerp", Pal[1]);
        cv.AddScatterMark3(sx, sy, "Slerp", Pal[0]);
        int arcN = 60; var ax = new Vector(arcN); var ay = new Vector(arcN);
        double r = a.NormL2();
        for (int i = 0; i < arcN; i++)
        {
            double t = Math.PI / 2.0 * i / (arcN - 1);
            ax[i] = r * Math.Cos(t); ay[i] = r * Math.Sin(t);
        }
        cv.AddPlot(ax, ay, "Дуга", new SKColor(255, 255, 255, 60), width: 1);
        cv.ChartName = $"Lerp vs Slerp ({steps} шагов)";
        cv.LabelX = "x"; cv.LabelY = "y";
        cv.SetAxisRange(-0.5, 2.5, -0.5, 2.5);
    }

    private static void DoReflect(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        double rad = N(p, "angle", 45) * Math.PI / 180;
        var v = new Vector(new[] { Math.Cos(rad), Math.Sin(rad) });
        var normal = new Vector(new[] { 0.0, 1.0 });
        var refl = Vector.Reflect(v, normal);
        cv.AddPlot(Vec2(0, v[0]), Vec2(0, v[1]), "v (падающий)", Pal[0], width: 3);
        cv.AddPlot(Vec2(0, refl[0]), Vec2(0, refl[1]), "r (отражённый)", Pal[1], width: 3);
        cv.AddPlot(Vec2(0, 0), Vec2(0, 1.2), "n (нормаль)", Pal[2], width: 2);
        cv.AddPlot(Vec2(-1.5, 1.5), Vec2(0, 0), "Поверхность", new SKColor(100, 100, 100), width: 1);
        cv.ChartName = $"Отражение (угол {N(p, "angle", 45):F0}°)";
        cv.LabelX = "x"; cv.LabelY = "y";
        cv.SetAxisRange(-1.5, 1.5, -0.5, 1.5);
    }

    private static string DoTripleProduct(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        var rng = new Random(I(p, "seed", 7));
        var a = RndVec3(rng, 3); var b = RndVec3(rng, 3); var c = RndVec3(rng, 3);
        double tp = Vector.TripleProduct(a, b, c);
        cv.AddPlot(Vec2(0, a[0]), Vec2(0, a[1]), "a", Pal[0], width: 3);
        cv.AddPlot(Vec2(0, b[0]), Vec2(0, b[1]), "b", Pal[1], width: 3);
        cv.AddPlot(Vec2(0, c[0]), Vec2(0, c[1]), "c", Pal[2], width: 3);
        cv.ChartName = $"Тройное произведение |V| = {Math.Abs(tp):F2} (XY-проекция)";
        cv.LabelX = "x"; cv.LabelY = "y";
        return $"a = [{Fmt(a)}]\nb = [{Fmt(b)}]\nc = [{Fmt(c)}]\n(a,b,c) = {tp:F4}\n|V| = {Math.Abs(tp):F4}";
    }

    // -- Transforms --------------------------------------------------

    private static void DoQuaternion(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        double angle = N(p, "angle", 90) * Math.PI / 180;
        int steps = I(p, "steps", 12);
        var axis = new Vector(new[] { 0.0, 0.0, 1.0 });
        var q = Quaternion.FromAxisAngle(axis, angle);
        var qI = Quaternion.Identity;
        var pts = new[] {
            new Vector(new[]{1.0,0,0}), new Vector(new[]{0,1.0,0}),
            new Vector(new[]{-1.0,0,0}), new Vector(new[]{0,-1.0,0}), new Vector(new[]{1.0,0,0})
        };
        var oX = new Vector(pts.Length); var oY = new Vector(pts.Length);
        var rX = new Vector(pts.Length); var rY = new Vector(pts.Length);
        for (int i = 0; i < pts.Length; i++)
        {
            oX[i] = pts[i][0]; oY[i] = pts[i][1];
            var rp = q.Rotate(pts[i]); rX[i] = rp[0]; rY[i] = rp[1];
        }
        cv.AddPlot(oX, oY, "Исходная", Pal[0], width: 2);
        cv.AddPlot(rX, rY, $"Поворот {N(p, "angle", 90):F0}°", Pal[1], width: 3);
        var point = new Vector(new[] { 1.0, 0, 0 });
        var slX = new Vector(steps); var slY = new Vector(steps);
        for (int i = 0; i < steps; i++)
        {
            double t = (double)i / (steps - 1);
            var qi = Quaternion.Slerp(qI, q, t);
            var pi = qi.Rotate(point); slX[i] = pi[0]; slY[i] = pi[1];
        }
        cv.AddScatterMark3(slX, slY, "Slerp-траектория", Pal[2]);
        cv.ChartName = $"Кватернион: поворот на {N(p, "angle", 90):F0}° вокруг Z";
        cv.LabelX = "x"; cv.LabelY = "y";
        cv.SetAxisRange(-1.8, 1.8, -1.8, 1.8);
    }

    private static void DoAffine2D(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        double tx = N(p, "tx", 1), ty = N(p, "ty", 0.5);
        double ang = N(p, "angle", 30) * Math.PI / 180;
        double sc = N(p, "scale", 1.5);
        var xf = Affine2D.Rotation(ang).Compose(Affine2D.Scale(sc, sc)).Compose(Affine2D.Translation(tx, ty));
        var shape = new[] {
            new Vector(new[]{0.0,0}), new Vector(new[]{1.0,0}),
            new Vector(new[]{1.0,1}), new Vector(new[]{0,1.0}), new Vector(new[]{0.0,0})
        };
        var ox = new Vector(5); var oy = new Vector(5);
        var dx = new Vector(5); var dy = new Vector(5);
        for (int i = 0; i < 5; i++)
        {
            ox[i] = shape[i][0]; oy[i] = shape[i][1];
            var tp = xf.Apply(shape[i]); dx[i] = tp[0]; dy[i] = tp[1];
        }
        cv.AddPlot(ox, oy, "Исходный", Pal[0], width: 2);
        cv.AddPlot(dx, dy, "Преобразованный", Pal[1], width: 3);
        cv.ChartName = $"Affine2D: R({N(p, "angle", 30):F0}°)·S({sc:F1})·T({tx:F1},{ty:F1})";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static string DoHomography(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        var rng = new Random(I(p, "seed", 42));
        var src = new[] {
            new Vector(new[]{0.0,0}), new Vector(new[]{1.0,0}),
            new Vector(new[]{1.0,1}), new Vector(new[]{0,1.0})
        };
        var dst = new Vector[4];
        for (int i = 0; i < 4; i++)
            dst[i] = new Vector(new[] {
                src[i][0] + (rng.NextDouble() - 0.5) * 0.6,
                src[i][1] + (rng.NextDouble() - 0.5) * 0.6 + 0.3
            });
        var H = Homography.EstimateDLT(src, dst);

        var sb = new StringBuilder();
        sb.AppendLine("H (3×3):");
        for (int r = 0; r < 3; r++)
            sb.AppendLine($"  [{H.M[r, 0]:F4}  {H.M[r, 1]:F4}  {H.M[r, 2]:F4}]");

        DrawClosedPoly(cv, src, "Исходный", Pal[0]);
        DrawClosedPoly(cv, dst, "Целевой", Pal[1]);

        int g = 5;
        for (int gx = 0; gx <= g; gx++)
        {
            var lx = new Vector(g + 1); var ly = new Vector(g + 1);
            for (int gy = 0; gy <= g; gy++)
            {
                var pt = new Vector(new[] { (double)gx / g, (double)gy / g });
                var tp = H.Apply(pt); lx[gy] = tp[0]; ly[gy] = tp[1];
            }
            cv.AddPlot(lx, ly, gx == 0 ? "Сетка H" : "", new SKColor(160, 160, 255, 80), width: 1);
        }
        cv.ChartName = "Гомография (DLT)";
        cv.LabelX = "x"; cv.LabelY = "y";
        return sb.ToString();
    }
}

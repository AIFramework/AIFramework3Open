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
    private static readonly SKColor[] Pal =
    [
        new(0x60, 0xA5, 0xFA), new(0xF8, 0x71, 0x71), new(0x4A, 0xDE, 0x80),
        new(0xFB, 0xBF, 0x24), new(0xA7, 0x8B, 0xFA), new(0x38, 0xBD, 0xF8),
        new(0xFB, 0x92, 0x3C), new(0xF4, 0x72, 0xB6),
    ];

    public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        var cv = MakeView(s);
        string? txt = null;

        switch (key)
        {
            case "vector_norms":    txt = DoVectorNorms(p, cv);        break;
            case "lerp_slerp":      DoLerpSlerp(p, cv);               break;
            case "reflect":         DoReflect(p, cv);                  break;
            case "triple_product":  txt = DoTripleProduct(p, cv);      break;
            case "quaternion_demo": DoQuaternion(p, cv);               break;
            case "affine_2d_demo":  DoAffine2D(p, cv);                break;
            case "homography_demo": txt = DoHomography(p, cv);         break;
            case "point_to_line_demo": DoPointToLine(p, cv);           break;
            case "ray_triangle_demo":  DoRayTriangle(p, cv);           break;
            case "aabb_obb_demo":   txt = DoAabbObb(p, cv);           break;
            case "shoelace_demo":   txt = DoShoelace(p, cv);          break;
            case "point_in_polygon_demo": DoPointInPoly(p, cv);        break;
            case "closest_triangle_demo": DoClosestTri(p, cv);         break;
            case "line_fit_demo":   DoLineFit(p, cv);                  break;
            case "circle_fit_demo": DoCircleFit(p, cv);               break;
            case "bezier_demo":     DoBezier(p, cv);                   break;
            case "hermite_demo":    DoHermite(p, cv);                  break;
            case "catmull_rom_demo":DoCatmullRom(p, cv);               break;
            case "svd_demo":        txt = DoSvd(p, cv);               break;
            case "lu_demo":         txt = DoLu(p, cv);                break;
            case "cholesky_demo":   txt = DoCholesky(p, cv);          break;
            case "pseudoinverse_demo": txt = DoPseudoinverse(p, cv);  break;
            case "jacobi_eigen_demo":  txt = DoJacobiEigen(p, cv);    break;
            case "conic_demo":      txt = DoConic(p, cv);             break;
            default:
                return new DemoResult { Error = $"Неизвестный ключ «{key}»" };
        }

        return Png(cv, s, textOutput: txt);
    }

    // -- Helpers -----------------------------------------------------

    private static Vector Vec2(double a, double b) => new(new[] { a, b });
    private static Vector RndVec3(Random rng, double scale) =>
        new(new[] { rng.NextDouble() * scale, rng.NextDouble() * scale, rng.NextDouble() * scale });

    private static Matrix RndMatrix(Random rng, int rows, int cols, double range)
    {
        var m = new Matrix(rows, cols);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                m[r, c] = Math.Round(rng.NextDouble() * range - range / 2, 2);
        return m;
    }

    private static string Fmt(Vector v) =>
        string.Join(", ", Enumerable.Range(0, v.Count).Select(i => v[i].ToString("F3")));

    private static void FmtMat(StringBuilder sb, Matrix m)
    {
        for (int r = 0; r < m.Height; r++)
            sb.AppendLine("  [" + string.Join("  ",
                Enumerable.Range(0, m.Width).Select(c => m[r, c].ToString("F3").PadLeft(8))) + "]");
    }

    private static double FrobSq(Matrix a, Matrix b)
    {
        double s = 0;
        for (int r = 0; r < a.Height; r++)
            for (int c = 0; c < a.Width; c++)
            {
                double d = a[r, c] - b[r, c];
                s += d * d;
            }
        return s;
    }

    private static void DrawClosedPoly(ChartView cv, Vector[] pts, string label, SKColor color)
    {
        var x = new Vector(pts.Length + 1); var y = new Vector(pts.Length + 1);
        for (int i = 0; i < pts.Length; i++) { x[i] = pts[i][0]; y[i] = pts[i][1]; }
        x[pts.Length] = pts[0][0]; y[pts.Length] = pts[0][1];
        cv.AddPlot(x, y, label, color, width: 2);
    }

    private static void DrawCircle(ChartView cv, Circle c, string label, SKColor color, int width)
    {
        int steps = 80;
        var cx = new Vector(steps + 1); var cy = new Vector(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            double th = 2 * Math.PI * i / steps;
            cx[i] = c.Center[0] + c.Radius * Math.Cos(th);
            cy[i] = c.Center[1] + c.Radius * Math.Sin(th);
        }
        cv.AddPlot(cx, cy, label, color, width: width);
    }
}

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
    // -- Fitting -----------------------------------------------------

    private static void DoLineFit(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 60);
        double outlierPct = N(p, "outliers", 15) / 100.0;
        double noise = N(p, "noise", 0.3);
        var rng = new Random(I(p, "seed", 42));
        int nOut = (int)(n * outlierPct);
        var pts = new Vector[n]; var ptX = new Vector(n); var ptY = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            double x = rng.NextDouble() * 6 - 1;
            double y = i < nOut ? rng.NextDouble() * 10 - 3 : 1.5 * x + 0.5 + (rng.NextDouble() - 0.5) * 2 * noise;
            pts[i] = new Vector(new[] { x, y }); ptX[i] = x; ptY[i] = y;
        }
        cv.AddScatterMark3(ptX, ptY, "Данные", new SKColor(200, 200, 200, 160));
        var ols = LineFit.Ols(pts); var tls = LineFit.Tls(pts);
        var ransac = LineFit.Ransac(pts, 500, noise * 3, rng);
        var lx = new Vector(new[] { -1.5, 6.0 });
        cv.AddPlot(lx, new Vector(new[] { ols.slope * (-1.5) + ols.intercept, ols.slope * 6 + ols.intercept }),
            $"OLS (k={ols.slope:F2})", Pal[0], width: 2);
        if (Math.Abs(tls.direction[1]) > 1e-10)
        {
            double k = tls.direction[1] / tls.direction[0];
            double b = tls.point[1] - k * tls.point[0];
            cv.AddPlot(lx, new Vector(new[] { k * (-1.5) + b, k * 6 + b }), $"TLS (k={k:F2})", Pal[2], width: 2);
        }
        cv.AddPlot(lx, new Vector(new[] { ransac.slope * (-1.5) + ransac.intercept, ransac.slope * 6 + ransac.intercept }),
            $"RANSAC (k={ransac.slope:F2})", Pal[1], width: 3);
        cv.ChartName = $"Подгонка прямой: {n} точек, {nOut} выбросов";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static void DoCircleFit(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 50);
        double outlierPct = N(p, "outliers", 10) / 100.0;
        double noise = N(p, "noise", 0.15);
        var rng = new Random(I(p, "seed", 42));
        int nOut = (int)(n * outlierPct);
        var pts = new Vector[n]; var ptX = new Vector(n); var ptY = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            if (i < nOut)
                pts[i] = new Vector(new[] { rng.NextDouble() * 6, rng.NextDouble() * 6 });
            else
            {
                double th = rng.NextDouble() * 2 * Math.PI;
                double r = 2 + (rng.NextDouble() - 0.5) * 2 * noise;
                pts[i] = new Vector(new[] { 3 + r * Math.Cos(th), 3 + r * Math.Sin(th) });
            }
            ptX[i] = pts[i][0]; ptY[i] = pts[i][1];
        }
        cv.AddScatterMark3(ptX, ptY, "Данные", new SKColor(200, 200, 200, 160));
        var kasa = CircleFit.AlgebraicFit(pts);
        var ransac = CircleFit.Ransac(pts, 500, noise * 4, rng);
        DrawCircle(cv, kasa, $"Kåsa (r={kasa.Radius:F2})", Pal[0], 2);
        if (ransac.circle != null)
            DrawCircle(cv, ransac.circle, $"RANSAC (r={ransac.circle.Radius:F2})", Pal[1], 3);
        cv.ChartName = "Подгонка окружности: Kåsa vs RANSAC";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    // -- Curves ------------------------------------------------------

    private static void DoBezier(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int degree = I(p, "degree", 3);
        var rng = new Random(I(p, "seed", 42));
        var ctrl = new Vector[degree + 1];
        for (int i = 0; i < ctrl.Length; i++)
            ctrl[i] = new Vector(new[] { i * 5.0 / degree, rng.NextDouble() * 4 });
        var curve = new BezierCurve(ctrl);
        var samples = curve.Sample(80);
        var cpX = new Vector(ctrl.Length); var cpY = new Vector(ctrl.Length);
        for (int i = 0; i < ctrl.Length; i++) { cpX[i] = ctrl[i][0]; cpY[i] = ctrl[i][1]; }
        cv.AddPlot(cpX, cpY, "Контрольный полигон", new SKColor(150, 150, 200, 100), width: 1);
        cv.AddScatterMark3(cpX, cpY, "Контр. точки", Pal[2]);
        var cX = new Vector(samples.Length); var cY = new Vector(samples.Length);
        for (int i = 0; i < samples.Length; i++) { cX[i] = samples[i][0]; cY[i] = samples[i][1]; }
        cv.AddPlot(cX, cY, $"Безье (степ. {degree})", Pal[0], width: 3);
        cv.ChartName = $"Кривая Безье (степень {degree})";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static void DoHermite(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int nPts = I(p, "pts", 5);
        var rng = new Random(I(p, "seed", 42));
        var seg = new (Vector point, Vector tangent)[nPts];
        for (int i = 0; i < nPts; i++)
        {
            double x = i * 5.0 / (nPts - 1), y = rng.NextDouble() * 4;
            seg[i] = (new Vector(new[] { x, y }), new Vector(new[] { 1 + rng.NextDouble(), (rng.NextDouble() - 0.5) * 4 }));
        }
        var hc = new HermiteCurve(seg);
        var samples = hc.Sample(100);
        var kx = new Vector(nPts); var ky = new Vector(nPts);
        for (int i = 0; i < nPts; i++) { kx[i] = seg[i].point[0]; ky[i] = seg[i].point[1]; }
        cv.AddScatterMark3(kx, ky, "Узлы", Pal[2]);
        for (int i = 0; i < nPts; i++)
            cv.AddPlot(
                new Vector(new[] { seg[i].point[0], seg[i].point[0] + seg[i].tangent[0] * 0.3 }),
                new Vector(new[] { seg[i].point[1], seg[i].point[1] + seg[i].tangent[1] * 0.3 }),
                i == 0 ? "Касательные" : "", new SKColor(255, 200, 100, 150), width: 1);
        var sx = new Vector(samples.Length); var sy = new Vector(samples.Length);
        for (int i = 0; i < samples.Length; i++) { sx[i] = samples[i][0]; sy[i] = samples[i][1]; }
        cv.AddPlot(sx, sy, "Эрмит", Pal[0], width: 3);
        cv.ChartName = $"Сплайн Эрмита ({nPts} узлов)";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    private static void DoCatmullRom(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int nPts = I(p, "pts", 6); double alpha = N(p, "alpha", 0.5);
        var rng = new Random(I(p, "seed", 42));
        var points = new Vector[nPts];
        for (int i = 0; i < nPts; i++)
            points[i] = new Vector(new[] { i * 5.0 / (nPts - 1), rng.NextDouble() * 4 });
        var cr = new CatmullRomCurve(points, alpha);
        var samples = cr.Sample(100);
        var kx = new Vector(nPts); var ky = new Vector(nPts);
        for (int i = 0; i < nPts; i++) { kx[i] = points[i][0]; ky[i] = points[i][1]; }
        cv.AddScatterMark3(kx, ky, "Узлы", Pal[2]);
        cv.AddPlot(kx, ky, "Полилиния", new SKColor(150, 150, 200, 80), width: 1);
        var sx = new Vector(samples.Length); var sy = new Vector(samples.Length);
        for (int i = 0; i < samples.Length; i++) { sx[i] = samples[i][0]; sy[i] = samples[i][1]; }
        cv.AddPlot(sx, sy, $"Catmull–Rom (α={alpha:F1})", Pal[0], width: 3);
        cv.ChartName = $"Catmull–Rom ({nPts} точек, α = {alpha:F1})";
        cv.LabelX = "x"; cv.LabelY = "y";
    }

    // -- Linear Algebra ----------------------------------------------

    private static string DoSvd(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int rows = I(p, "rows", 3), cols = I(p, "cols", 3);
        var rng = new Random(I(p, "seed", 42));
        var A = RndMatrix(rng, rows, cols, 8);
        var (U, sigma, V) = AI.ClassicMath.MatrixUtils.Svd.Decompose(A);
        var sb = new StringBuilder();
        sb.AppendLine("A ="); FmtMat(sb, A);
        sb.AppendLine($"\nσ = [{string.Join(", ", sigma.Select(s => s.ToString("F4")))}]");
        int k = sigma.Length;
        var S = new Matrix(rows, cols);
        for (int i = 0; i < k; i++) S[i, i] = sigma[i];
        var Ah = U * S * V.Transpose();
        double err = FrobSq(A, Ah);
        sb.AppendLine($"‖A − UΣVᵀ‖²F = {err:E3}");
        if (sigma.Length >= 2)
        {
            for (int i = 0; i < sigma.Length; i++)
                cv.AddPlot(new Vector(new[] { (double)i + 1, i + 1.0 }), new Vector(new[] { 0.0, sigma[i] }),
                    i == 0 ? "σ" : "", Pal[i % Pal.Length], width: 4);
            cv.ChartName = $"SVD: σ = [{string.Join(", ", sigma.Select(s => s.ToString("F2")))}]";
            cv.LabelX = "Индекс"; cv.LabelY = "σ";
        }
        return sb.ToString();
    }

    private static string DoLu(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 3);
        var rng = new Random(I(p, "seed", 42));
        var A = RndMatrix(rng, n, n, 8);
        var (L, U, perm) = AI.ClassicMath.MatrixUtils.LU.Decompose(A);
        double det = AI.ClassicMath.MatrixUtils.LU.Determinant(A);
        var sb = new StringBuilder();
        sb.AppendLine("A ="); FmtMat(sb, A);
        sb.AppendLine("L ="); FmtMat(sb, L);
        sb.AppendLine("U ="); FmtMat(sb, U);
        sb.AppendLine($"perm = [{string.Join(", ", perm)}]");
        sb.AppendLine($"det(A) = {det:F4}");
        cv.ChartName = $"LU ({n}×{n}), det = {det:F2}";
        return sb.ToString();
    }

    private static string DoCholesky(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 3);
        var rng = new Random(I(p, "seed", 42));
        var R = RndMatrix(rng, n, n, 2);
        var A = R.Transpose() * R;
        for (int i = 0; i < n; i++) A[i, i] += n;
        var L = AI.ClassicMath.MatrixUtils.Cholesky.Decompose(A);
        var sb = new StringBuilder();
        sb.AppendLine("A (SPD) ="); FmtMat(sb, A);
        sb.AppendLine("L ="); FmtMat(sb, L);
        double err = FrobSq(A, L * L.Transpose());
        sb.AppendLine($"‖A − LLᵀ‖²F = {err:E3}");
        cv.ChartName = $"Холецкий ({n}×{n}): A = LLᵀ";
        return sb.ToString();
    }

    private static string DoPseudoinverse(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int rows = I(p, "rows", 4), cols = I(p, "cols", 3);
        var rng = new Random(I(p, "seed", 42));
        var A = RndMatrix(rng, rows, cols, 6);
        var Ap = AI.ClassicMath.MatrixUtils.Pseudoinverse.Compute(A);
        var sb = new StringBuilder();
        sb.AppendLine("A ="); FmtMat(sb, A);
        sb.AppendLine("A⁺ ="); FmtMat(sb, Ap);
        double err = FrobSq(A, A * Ap * A);
        sb.AppendLine($"‖A − A·A⁺·A‖²F = {err:E3}");
        cv.ChartName = $"Псевдообратная A⁺ ({rows}×{cols})";
        return sb.ToString();
    }

    private static string DoJacobiEigen(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        int n = I(p, "n", 3);
        var rng = new Random(I(p, "seed", 42));
        var A = new Matrix(n, n);
        for (int r = 0; r < n; r++)
            for (int c = r; c < n; c++)
            {
                double v = Math.Round(rng.NextDouble() * 6 - 3, 2);
                A[r, c] = v; A[c, r] = v;
            }
        var (eigs, evecs) = AI.ClassicMath.MatrixUtils.JacobiEigen.Compute(A);
        var sb = new StringBuilder();
        sb.AppendLine("A (симм.) ="); FmtMat(sb, A);
        sb.AppendLine($"λ = [{string.Join(", ", eigs.Select(e => e.ToString("F4")))}]");
        sb.AppendLine("V ="); FmtMat(sb, evecs);
        if (eigs.Length >= 2)
        {
            for (int i = 0; i < eigs.Length; i++)
                cv.AddPlot(new Vector(new[] { (double)i + 1, i + 1.0 }), new Vector(new[] { 0.0, eigs[i] }),
                    i == 0 ? "λ" : "", Pal[i % Pal.Length], width: 4);
            cv.ChartName = $"Якоби: λ = [{string.Join(", ", eigs.Select(e => e.ToString("F2")))}]";
            cv.LabelX = "Индекс"; cv.LabelY = "λ";
        }
        return sb.ToString();
    }

    // -- Conics ------------------------------------------------------

    private static string DoConic(IReadOnlyDictionary<string, double> p, ChartView cv)
    {
        double ca = N(p, "A", 1), cb = N(p, "B", 0), cc = N(p, "C", 1);
        double cd = N(p, "D", 0), ce = N(p, "E", 0), cf = N(p, "F", -4);
        var conic = new ConicSection(ca, cb, cc, cd, ce, cf);
        var type = conic.Classify();
        var sb = new StringBuilder();
        sb.AppendLine($"{ca:G3}x² + {cb:G3}xy + {cc:G3}y² + {cd:G3}x + {ce:G3}y + {cf:G3} = 0");
        sb.AppendLine($"Тип: {type}");
        sb.AppendLine($"B²−4AC = {cb * cb - 4 * ca * cc:F4}");

        int res = 300;
        double xMin = -5, xMax = 5, yMin = -5, yMax = 5;
        var cxList = new List<double>(); var cyList = new List<double>();
        for (int ix = 0; ix < res; ix++)
        {
            double x = xMin + (xMax - xMin) * ix / res;
            for (int iy = 0; iy < res; iy++)
            {
                double y = yMin + (yMax - yMin) * iy / res;
                double v1 = ca * x * x + cb * x * y + cc * y * y + cd * x + ce * y + cf;
                double y2 = yMin + (yMax - yMin) * (iy + 1) / res;
                double v2 = ca * x * x + cb * x * y2 + cc * y2 * y2 + cd * x + ce * y2 + cf;
                if (v1 * v2 <= 0 || Math.Abs(v1) < 0.05)
                {
                    cxList.Add(x); cyList.Add(y);
                }
            }
        }
        if (cxList.Count > 0)
            cv.AddScatterMark3(new Vector(cxList.ToArray()), new Vector(cyList.ToArray()), type.ToString(), Pal[0]);
        cv.ChartName = $"Коника: {type}";
        cv.LabelX = "x"; cv.LabelY = "y";
        cv.SetAxisRange(xMin, xMax, yMin, yMax);
        return sb.ToString();
    }
}

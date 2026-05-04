using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Fitting;

/// <summary>
/// Аппроксимация прямой по набору точек.
/// </summary>
public static class LineFit
{
    /// <summary>
    /// МНК (y = kx + b) — обычная регрессия.
    /// </summary>
    public static (double slope, double intercept) Ols(Vector[] points)
    {
        int n = points.Length;
        double sx = 0, sy = 0, sxy = 0, sxx = 0;

        for (int i = 0; i < n; i++)
        {
            double x = points[i][0], y = points[i][1];
            sx += x;
            sy += y;
            sxy += x * y;
            sxx += x * x;
        }

        double denom = n * sxx - sx * sx;
        double slope = (n * sxy - sx * sy) / denom;
        double intercept = (sy - slope * sx) / n;
        return (slope, intercept);
    }

    /// <summary>
    /// Полный МНК (Total Least Squares) — через собственное разложение ковариационной матрицы 2×2.
    /// </summary>
    public static (Vector direction, Vector point) Tls(Vector[] points)
    {
        int n = points.Length;
        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += points[i][0]; my += points[i][1]; }
        mx /= n; my /= n;

        double cxx = 0, cxy = 0, cyy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = points[i][0] - mx;
            double dy = points[i][1] - my;
            cxx += dx * dx;
            cxy += dx * dy;
            cyy += dy * dy;
        }

        // Собственный вектор для наибольшего собственного значения матрицы [[cxx,cxy],[cxy,cyy]]
        double trace = cxx + cyy;
        double det = cxx * cyy - cxy * cxy;
        double disc = Math.Sqrt(Math.Max(0, trace * trace / 4 - det));
        double lambda1 = trace / 2 + disc;

        double vx, vy;
        if (Math.Abs(cxy) > 1e-15)
        {
            vx = lambda1 - cyy;
            vy = cxy;
        }
        else
        {
            vx = cxx >= cyy ? 1 : 0;
            vy = cxx >= cyy ? 0 : 1;
        }

        double len = Math.Sqrt(vx * vx + vy * vy);
        var direction = new Vector(new[] { vx / len, vy / len });
        var center = new Vector(new[] { mx, my });
        return (direction, center);
    }

    /// <summary>
    /// RANSAC для аппроксимации прямой.
    /// </summary>
    public static (double slope, double intercept, bool[] inliers) Ransac(
        Vector[] points, int iterations = 500, double threshold = 1.0, Random rng = null)
    {
        rng ??= new Random(42);
        int n = points.Length;
        double bestSlope = 0, bestIntercept = 0;
        bool[] bestInliers = new bool[n];
        int bestCount = 0;

        for (int iter = 0; iter < iterations; iter++)
        {
            int i1 = rng.Next(n), i2 = rng.Next(n);
            while (i2 == i1) i2 = rng.Next(n);

            double x1 = points[i1][0], y1 = points[i1][1];
            double x2 = points[i2][0], y2 = points[i2][1];
            double dx = x2 - x1;

            if (Math.Abs(dx) < 1e-15) continue;

            double slope = (y2 - y1) / dx;
            double intercept = y1 - slope * x1;

            int count = 0;
            var inliers = new bool[n];
            double norm = Math.Sqrt(slope * slope + 1);

            for (int k = 0; k < n; k++)
            {
                double dist = Math.Abs(points[k][1] - slope * points[k][0] - intercept) / norm;
                if (dist <= threshold)
                {
                    inliers[k] = true;
                    count++;
                }
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestSlope = slope;
                bestIntercept = intercept;
                bestInliers = inliers;
            }
        }

        return (bestSlope, bestIntercept, bestInliers);
    }
}

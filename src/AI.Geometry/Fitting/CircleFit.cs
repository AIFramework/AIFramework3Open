using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Fitting;

/// <summary>
/// Аппроксимация окружности по набору точек.
/// </summary>
public static class CircleFit
{
    /// <summary>
    /// Алгебраический метод Kåsa (линейная система для x² + y² = ax + by + c).
    /// </summary>
    public static Circle AlgebraicFit(Vector[] points)
    {
        int n = points.Length;
        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
        double sxz = 0, syz = 0;

        for (int i = 0; i < n; i++)
        {
            double x = points[i][0], y = points[i][1];
            double z = x * x + y * y;
            sx += x; sy += y;
            sxx += x * x; syy += y * y; sxy += x * y;
            sxz += x * z; syz += y * z;
        }

        // Решаем систему 3×3: [sxx sxy sx; sxy syy sy; sx sy n] * [a;b;c] = [sxz;syz;sz]
        double sz = sxx + syy;
        double[,] A = {
            { sxx, sxy, sx },
            { sxy, syy, sy },
            { sx,  sy,  n  }
        };
        double[] rhs = { sxz, syz, sz };

        var abc = Solve3x3(A, rhs);
        double a = abc[0], b = abc[1], c = abc[2];
        double cx = a / 2.0;
        double cy = b / 2.0;
        double r = Math.Sqrt(c + cx * cx + cy * cy);

        return new Circle(new Vector(new[] { cx, cy }), r);
    }

    /// <summary>
    /// RANSAC для аппроксимации окружности.
    /// </summary>
    public static (Circle circle, bool[] inliers) Ransac(
        Vector[] points, int iterations = 500, double threshold = 1.0, Random rng = null)
    {
        rng ??= new Random(42);
        int n = points.Length;
        Circle bestCircle = null;
        bool[] bestInliers = new bool[n];
        int bestCount = 0;

        for (int iter = 0; iter < iterations; iter++)
        {
            int i0 = rng.Next(n);
            int i1 = rng.Next(n);
            int i2 = rng.Next(n);
            while (i1 == i0) i1 = rng.Next(n);
            while (i2 == i0 || i2 == i1) i2 = rng.Next(n);

            var sample = new[] { points[i0], points[i1], points[i2] };
            Circle c;
            try { c = AlgebraicFit(sample); }
            catch { continue; }

            if (double.IsNaN(c.Radius) || double.IsInfinity(c.Radius)) continue;

            int count = 0;
            var inliers = new bool[n];
            for (int k = 0; k < n; k++)
            {
                var diff = points[k] - c.Center;
                double dist = Math.Abs(Math.Sqrt(Vector.Dot(diff, diff)) - c.Radius);
                if (dist <= threshold) { inliers[k] = true; count++; }
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestCircle = c;
                bestInliers = inliers;
            }
        }

        return (bestCircle, bestInliers);
    }

    private static double[] Solve3x3(double[,] A, double[] b)
    {
        var aug = new double[3, 4];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++) aug[i, j] = A[i, j];
            aug[i, 3] = b[i];
        }

        for (int col = 0; col < 3; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < 3; row++)
                if (Math.Abs(aug[row, col]) > Math.Abs(aug[pivot, col]))
                    pivot = row;

            for (int j = 0; j < 4; j++)
                (aug[col, j], aug[pivot, j]) = (aug[pivot, j], aug[col, j]);

            double div = aug[col, col];
            for (int j = col; j < 4; j++) aug[col, j] /= div;

            for (int row = 0; row < 3; row++)
            {
                if (row == col) continue;
                double f = aug[row, col];
                for (int j = col; j < 4; j++) aug[row, j] -= f * aug[col, j];
            }
        }

        return new[] { aug[0, 3], aug[1, 3], aug[2, 3] };
    }
}

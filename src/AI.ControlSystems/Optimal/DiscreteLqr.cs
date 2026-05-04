using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Optimal;

/// <summary>
/// Дискретный регулятор LQR: минимизация ∑ (x' Q x + u' R u) при x[k+1] = A x[k] + B u[k].
/// Возвращает матрицу усиления K в законе u = −K x.
/// </summary>
public static class DiscreteLqr
{
    /// <summary>Вычисляет K методом итераций Риккати.</summary>
    public static Matrix Solve(Matrix a, Matrix b, Matrix q, Matrix r, double tolerance = 1e-10, int maxIterations = 500)
    {
        if (a == null || b == null || q == null || r == null)
            throw new ArgumentNullException();
        int n = a.Height;
        int m = b.Width;
        if (!a.IsSquared || b.Height != n || q.Height != n || q.Width != n || r.Height != m || r.Width != m)
            throw new ArgumentException("Несогласованные размеры A, B, Q, R.");

        Matrix p = q;
        Matrix at = a.Transpose();
        Matrix bt = b.Transpose();

        for (int iter = 0; iter < maxIterations; iter++)
        {
            Matrix btpb = bt * p * b + r;
            Matrix inv = btpb.GetInvertMatrix();
            Matrix mid = at * p * b * inv * bt * p * a;
            Matrix pNew = at * p * a - mid + q;
            pNew = ControlLinAlg.Symmetrize(pNew);
            if (MaxFrobDiff(p, pNew) < tolerance)
            {
                p = pNew;
                break;
            }
            p = pNew;
        }

        Matrix btpbF = bt * p * b + r;
        Matrix invF = btpbF.GetInvertMatrix();
        return invF * bt * p * a;
    }

    private static double MaxFrobDiff(Matrix a, Matrix b)
    {
        double s = 0;
        for (int i = 0; i < a.Data.Length; i++)
        {
            double d = a.Data[i] - b.Data[i];
            s += d * d;
        }
        return Math.Sqrt(s);
    }
}

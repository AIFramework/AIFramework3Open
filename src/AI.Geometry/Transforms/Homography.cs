using System;
using Vector = AI.DataStructs.Algebraic.Vector;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Transforms;

/// <summary>
/// Проективное преобразование (гомография) на плоскости, хранит матрицу 3×3.
/// </summary>
public class Homography
{
    /// <summary>
    /// Матрица гомографии 3×3.
    /// </summary>
    public Matrix M { get; }

    /// <summary>
    /// Создаёт гомографию из матрицы 3×3.
    /// </summary>
    public Homography(Matrix m) { M = m; }

    /// <summary>
    /// Применяет проективное преобразование к 2D-точке.
    /// </summary>
    public Vector Apply(Vector point2d)
    {
        double x = M[0, 0] * point2d[0] + M[0, 1] * point2d[1] + M[0, 2];
        double y = M[1, 0] * point2d[0] + M[1, 1] * point2d[1] + M[1, 2];
        double w = M[2, 0] * point2d[0] + M[2, 1] * point2d[1] + M[2, 2];
        return new Vector(new[] { x / w, y / w });
    }

    /// <summary>
    /// Оценивает гомографию по 4 парам точек-соответствий (DLT, решение системы 8×8 методом Гаусса).
    /// </summary>
    public static Homography EstimateDLT(Vector[] src, Vector[] dst)
    {
        if (src.Length < 4 || dst.Length < 4)
            throw new ArgumentException("Необходимо минимум 4 пары точек.");

        int n = Math.Min(src.Length, 4);
        var A = new double[2 * n, 9];

        for (int i = 0; i < n; i++)
        {
            double sx = src[i][0], sy = src[i][1];
            double dx = dst[i][0], dy = dst[i][1];

            int r1 = 2 * i, r2 = 2 * i + 1;
            A[r1, 0] = -sx; A[r1, 1] = -sy; A[r1, 2] = -1;
            A[r1, 6] = dx * sx; A[r1, 7] = dx * sy; A[r1, 8] = dx;

            A[r2, 3] = -sx; A[r2, 4] = -sy; A[r2, 5] = -1;
            A[r2, 6] = dy * sx; A[r2, 7] = dy * sy; A[r2, 8] = dy;
        }

        var h = SolveHomogeneous8(A, 2 * n, 9);

        var m = new Matrix(3, 3);
        m[0, 0] = h[0]; m[0, 1] = h[1]; m[0, 2] = h[2];
        m[1, 0] = h[3]; m[1, 1] = h[4]; m[1, 2] = h[5];
        m[2, 0] = h[6]; m[2, 1] = h[7]; m[2, 2] = h[8];
        return new Homography(m);
    }

    private static double[] SolveHomogeneous8(double[,] A, int rows, int cols)
    {
        var AtA = new double[cols, cols];
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < cols; j++)
            {
                double s = 0;
                for (int k = 0; k < rows; k++)
                    s += A[k, i] * A[k, j];
                AtA[i, j] = s;
            }

        // Фиксируем h[8] = 1 и решаем 8×8 систему
        var b = new double[8];
        var M8 = new double[8, 8];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
                M8[i, j] = AtA[i, j];
            b[i] = -AtA[i, 8];
        }

        var x = GaussSolve(M8, b, 8);
        var result = new double[9];
        for (int i = 0; i < 8; i++) result[i] = x[i];
        result[8] = 1;
        return result;
    }

    private static double[] GaussSolve(double[,] A, double[] b, int n)
    {
        var aug = new double[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                aug[i, j] = A[i, j];
            aug[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(aug[row, col]) > Math.Abs(aug[pivot, col]))
                    pivot = row;

            for (int j = 0; j <= n; j++)
                (aug[col, j], aug[pivot, j]) = (aug[pivot, j], aug[col, j]);

            double div = aug[col, col];
            if (Math.Abs(div) < 1e-15) continue;
            for (int j = col; j <= n; j++)
                aug[col, j] /= div;

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double f = aug[row, col];
                for (int j = col; j <= n; j++)
                    aug[row, j] -= f * aug[col, j];
            }
        }

        var x = new double[n];
        for (int i = 0; i < n; i++) x[i] = aug[i, n];
        return x;
    }
}

using System;
using Vector = AI.DataStructs.Algebraic.Vector;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Transforms;

/// <summary>
/// Аффинное преобразование в 3D-пространстве (хранит матрицу 4×4).
/// </summary>
public class Affine3D
{
    /// <summary>
    /// Внутренняя матрица преобразования 4×4.
    /// </summary>
    public Matrix M { get; }

    /// <summary>
    /// Создаёт преобразование из матрицы 4×4.
    /// </summary>
    public Affine3D(Matrix m) { M = m; }

    private static Matrix Eye4()
    {
        var m = new Matrix(4, 4);
        m[0, 0] = 1; m[1, 1] = 1; m[2, 2] = 1; m[3, 3] = 1;
        return m;
    }

    /// <summary>
    /// Единичное преобразование.
    /// </summary>
    public static Affine3D Identity() => new Affine3D(Eye4());

    /// <summary>
    /// Сдвиг на (dx, dy, dz).
    /// </summary>
    public static Affine3D Translation(double dx, double dy, double dz)
    {
        var m = Eye4();
        m[0, 3] = dx; m[1, 3] = dy; m[2, 3] = dz;
        return new Affine3D(m);
    }

    /// <summary>
    /// Масштабирование по осям.
    /// </summary>
    public static Affine3D Scale(double sx, double sy, double sz)
    {
        var m = new Matrix(4, 4);
        m[0, 0] = sx; m[1, 1] = sy; m[2, 2] = sz; m[3, 3] = 1;
        return new Affine3D(m);
    }

    /// <summary>
    /// Вращение вокруг оси X.
    /// </summary>
    public static Affine3D RotationX(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        var m = Eye4();
        m[1, 1] = c; m[1, 2] = -s;
        m[2, 1] = s; m[2, 2] = c;
        return new Affine3D(m);
    }

    /// <summary>
    /// Вращение вокруг оси Y.
    /// </summary>
    public static Affine3D RotationY(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        var m = Eye4();
        m[0, 0] = c; m[0, 2] = s;
        m[2, 0] = -s; m[2, 2] = c;
        return new Affine3D(m);
    }

    /// <summary>
    /// Вращение вокруг оси Z.
    /// </summary>
    public static Affine3D RotationZ(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        var m = Eye4();
        m[0, 0] = c; m[0, 1] = -s;
        m[1, 0] = s; m[1, 1] = c;
        return new Affine3D(m);
    }

    /// <summary>
    /// Создаёт аффинное преобразование из кватерниона.
    /// </summary>
    public static Affine3D FromQuaternion(Quaternion q)
    {
        var r = q.ToRotationMatrix3();
        var m = Eye4();
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                m[i, j] = r[i, j];
        return new Affine3D(m);
    }

    /// <summary>
    /// Применяет преобразование к 3D-точке.
    /// </summary>
    public Vector Apply(Vector point)
    {
        double x = M[0, 0] * point[0] + M[0, 1] * point[1] + M[0, 2] * point[2] + M[0, 3];
        double y = M[1, 0] * point[0] + M[1, 1] * point[1] + M[1, 2] * point[2] + M[1, 3];
        double z = M[2, 0] * point[0] + M[2, 1] * point[1] + M[2, 2] * point[2] + M[2, 3];
        return new Vector(new[] { x, y, z });
    }

    /// <summary>
    /// Композиция: this ∘ other.
    /// </summary>
    public Affine3D Compose(Affine3D other)
    {
        return new Affine3D(M * other.M);
    }

    /// <summary>
    /// Обратное преобразование (обращение матрицы 4×4 методом Гаусса).
    /// </summary>
    public Affine3D Inverse()
    {
        int n = 4;
        var aug = new double[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                aug[i, j] = M[i, j];
            aug[i, n + i] = 1;
        }

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(aug[row, col]) > Math.Abs(aug[pivot, col]))
                    pivot = row;

            if (Math.Abs(aug[pivot, col]) < 1e-15)
                throw new InvalidOperationException("Матрица вырождена.");

            for (int j = 0; j < 2 * n; j++)
                (aug[col, j], aug[pivot, j]) = (aug[pivot, j], aug[col, j]);

            double div = aug[col, col];
            for (int j = 0; j < 2 * n; j++)
                aug[col, j] /= div;

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double factor = aug[row, col];
                for (int j = 0; j < 2 * n; j++)
                    aug[row, j] -= factor * aug[col, j];
            }
        }

        var inv = new Matrix(n, n);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                inv[i, j] = aug[i, n + j];

        return new Affine3D(inv);
    }
}

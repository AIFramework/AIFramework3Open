using System;
using Vector = AI.DataStructs.Algebraic.Vector;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Transforms;

/// <summary>
/// Аффинное преобразование на плоскости (хранит матрицу 3×3).
/// </summary>
public class Affine2D
{
    /// <summary>
    /// Внутренняя матрица преобразования 3×3.
    /// </summary>
    public Matrix M { get; }

    /// <summary>
    /// Создаёт преобразование из матрицы 3×3.
    /// </summary>
    public Affine2D(Matrix m) { M = m; }

    /// <summary>
    /// Единичное преобразование.
    /// </summary>
    public static Affine2D Identity()
    {
        var m = new Matrix(3, 3);
        m[0, 0] = 1; m[1, 1] = 1; m[2, 2] = 1;
        return new Affine2D(m);
    }

    /// <summary>
    /// Сдвиг на (dx, dy).
    /// </summary>
    public static Affine2D Translation(double dx, double dy)
    {
        var m = new Matrix(3, 3);
        m[0, 0] = 1; m[1, 1] = 1; m[2, 2] = 1;
        m[0, 2] = dx; m[1, 2] = dy;
        return new Affine2D(m);
    }

    /// <summary>
    /// Масштабирование по осям.
    /// </summary>
    public static Affine2D Scale(double sx, double sy)
    {
        var m = new Matrix(3, 3);
        m[0, 0] = sx; m[1, 1] = sy; m[2, 2] = 1;
        return new Affine2D(m);
    }

    /// <summary>
    /// Поворот на угол (рад) вокруг начала координат.
    /// </summary>
    public static Affine2D Rotation(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        var m = new Matrix(3, 3);
        m[0, 0] = c; m[0, 1] = -s;
        m[1, 0] = s; m[1, 1] = c;
        m[2, 2] = 1;
        return new Affine2D(m);
    }

    /// <summary>
    /// Сдвиг (shear) по осям.
    /// </summary>
    public static Affine2D Shear(double shx, double shy)
    {
        var m = new Matrix(3, 3);
        m[0, 0] = 1; m[0, 1] = shx;
        m[1, 0] = shy; m[1, 1] = 1;
        m[2, 2] = 1;
        return new Affine2D(m);
    }

    /// <summary>
    /// Применяет преобразование к 2D-точке.
    /// </summary>
    public Vector Apply(Vector point)
    {
        double x = M[0, 0] * point[0] + M[0, 1] * point[1] + M[0, 2];
        double y = M[1, 0] * point[0] + M[1, 1] * point[1] + M[1, 2];
        return new Vector(new[] { x, y });
    }

    /// <summary>
    /// Композиция: this ∘ other (сначала other, потом this).
    /// </summary>
    public Affine2D Compose(Affine2D other)
    {
        return new Affine2D(M * other.M);
    }

    /// <summary>
    /// Обратное преобразование (аналитическое обращение 3×3).
    /// </summary>
    public Affine2D Inverse()
    {
        double a = M[0, 0], b = M[0, 1], c = M[0, 2];
        double d = M[1, 0], e = M[1, 1], f = M[1, 2];

        double det = a * e - b * d;
        if (Math.Abs(det) < 1e-15)
            throw new InvalidOperationException("Матрица вырождена, обратная не существует.");

        double invDet = 1.0 / det;
        var inv = new Matrix(3, 3);
        inv[0, 0] = e * invDet;
        inv[0, 1] = -b * invDet;
        inv[0, 2] = (b * f - c * e) * invDet;
        inv[1, 0] = -d * invDet;
        inv[1, 1] = a * invDet;
        inv[1, 2] = (c * d - a * f) * invDet;
        inv[2, 2] = 1;
        return new Affine2D(inv);
    }
}

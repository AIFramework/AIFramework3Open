using System;
using Vector = AI.DataStructs.Algebraic.Vector;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Transforms;

/// <summary>
/// Кватернион для представления вращений в 3D.
/// </summary>
public readonly struct Quaternion : IEquatable<Quaternion>
{
    /// <summary>Скалярная компонента.</summary>
    public readonly double W;
    /// <summary>Компонента i.</summary>
    public readonly double X;
    /// <summary>Компонента j.</summary>
    public readonly double Y;
    /// <summary>Компонента k.</summary>
    public readonly double Z;

    /// <summary>
    /// Создаёт кватернион из компонент.
    /// </summary>
    public Quaternion(double w, double x, double y, double z)
    {
        W = w; X = x; Y = y; Z = z;
    }

    /// <summary>
    /// Единичный кватернион (без вращения).
    /// </summary>
    public static Quaternion Identity => new Quaternion(1, 0, 0, 0);

    /// <summary>
    /// Создаёт кватернион вращения вокруг оси на заданный угол (рад).
    /// </summary>
    public static Quaternion FromAxisAngle(Vector axis, double angle)
    {
        double halfAngle = angle * 0.5;
        double s = Math.Sin(halfAngle);
        double len = Math.Sqrt(Vector.Dot(axis, axis));
        return new Quaternion(
            Math.Cos(halfAngle),
            axis[0] / len * s,
            axis[1] / len * s,
            axis[2] / len * s);
    }

    /// <summary>
    /// Создаёт кватернион из углов Эйлера (yaw, pitch, roll) в радианах.
    /// </summary>
    public static Quaternion FromEuler(double yaw, double pitch, double roll)
    {
        double cy = Math.Cos(yaw * 0.5);
        double sy = Math.Sin(yaw * 0.5);
        double cp = Math.Cos(pitch * 0.5);
        double sp = Math.Sin(pitch * 0.5);
        double cr = Math.Cos(roll * 0.5);
        double sr = Math.Sin(roll * 0.5);

        return new Quaternion(
            cr * cp * cy + sr * sp * sy,
            sr * cp * cy - cr * sp * sy,
            cr * sp * cy + sr * cp * sy,
            cr * cp * sy - sr * sp * cy);
    }

    /// <summary>
    /// Умножение (композиция вращений).
    /// </summary>
    public static Quaternion operator *(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);
    }

    /// <summary>
    /// Сопряжённый кватернион.
    /// </summary>
    public Quaternion Conjugate => new Quaternion(W, -X, -Y, -Z);

    /// <summary>
    /// Обратный кватернион.
    /// </summary>
    public Quaternion Inverse
    {
        get
        {
            double n2 = W * W + X * X + Y * Y + Z * Z;
            return new Quaternion(W / n2, -X / n2, -Y / n2, -Z / n2);
        }
    }

    /// <summary>
    /// Норма кватерниона.
    /// </summary>
    public double Norm => Math.Sqrt(W * W + X * X + Y * Y + Z * Z);

    /// <summary>
    /// Нормализованный кватернион.
    /// </summary>
    public Quaternion Normalize
    {
        get
        {
            double n = Norm;
            return new Quaternion(W / n, X / n, Y / n, Z / n);
        }
    }

    /// <summary>
    /// Преобразует кватернион в матрицу вращения 3×3.
    /// </summary>
    public Matrix ToRotationMatrix3()
    {
        var m = new Matrix(3, 3);
        double xx = X * X, yy = Y * Y, zz = Z * Z;
        double xy = X * Y, xz = X * Z, yz = Y * Z;
        double wx = W * X, wy = W * Y, wz = W * Z;

        m[0, 0] = 1 - 2 * (yy + zz);
        m[0, 1] = 2 * (xy - wz);
        m[0, 2] = 2 * (xz + wy);
        m[1, 0] = 2 * (xy + wz);
        m[1, 1] = 1 - 2 * (xx + zz);
        m[1, 2] = 2 * (yz - wx);
        m[2, 0] = 2 * (xz - wy);
        m[2, 1] = 2 * (yz + wx);
        m[2, 2] = 1 - 2 * (xx + yy);
        return m;
    }

    /// <summary>
    /// Создаёт кватернион из матрицы вращения 3×3.
    /// </summary>
    public static Quaternion FromRotationMatrix(Matrix m)
    {
        double trace = m[0, 0] + m[1, 1] + m[2, 2];
        double w, x, y, z;

        if (trace > 0)
        {
            double s = 0.5 / Math.Sqrt(trace + 1.0);
            w = 0.25 / s;
            x = (m[2, 1] - m[1, 2]) * s;
            y = (m[0, 2] - m[2, 0]) * s;
            z = (m[1, 0] - m[0, 1]) * s;
        }
        else if (m[0, 0] > m[1, 1] && m[0, 0] > m[2, 2])
        {
            double s = 2.0 * Math.Sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]);
            w = (m[2, 1] - m[1, 2]) / s;
            x = 0.25 * s;
            y = (m[0, 1] + m[1, 0]) / s;
            z = (m[0, 2] + m[2, 0]) / s;
        }
        else if (m[1, 1] > m[2, 2])
        {
            double s = 2.0 * Math.Sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]);
            w = (m[0, 2] - m[2, 0]) / s;
            x = (m[0, 1] + m[1, 0]) / s;
            y = 0.25 * s;
            z = (m[1, 2] + m[2, 1]) / s;
        }
        else
        {
            double s = 2.0 * Math.Sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]);
            w = (m[1, 0] - m[0, 1]) / s;
            x = (m[0, 2] + m[2, 0]) / s;
            y = (m[1, 2] + m[2, 1]) / s;
            z = 0.25 * s;
        }

        return new Quaternion(w, x, y, z);
    }

    /// <summary>
    /// Сферическая линейная интерполяция (SLERP).
    /// </summary>
    public static Quaternion Slerp(Quaternion a, Quaternion b, double t)
    {
        double dot = a.W * b.W + a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        if (dot < 0)
        {
            b = new Quaternion(-b.W, -b.X, -b.Y, -b.Z);
            dot = -dot;
        }

        if (dot > 0.9995)
        {
            return new Quaternion(
                a.W + t * (b.W - a.W),
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z)).Normalize;
        }

        double theta0 = Math.Acos(dot);
        double theta = theta0 * t;
        double sinTheta = Math.Sin(theta);
        double sinTheta0 = Math.Sin(theta0);

        double s0 = Math.Cos(theta) - dot * sinTheta / sinTheta0;
        double s1 = sinTheta / sinTheta0;

        return new Quaternion(
            s0 * a.W + s1 * b.W,
            s0 * a.X + s1 * b.X,
            s0 * a.Y + s1 * b.Y,
            s0 * a.Z + s1 * b.Z);
    }

    /// <summary>
    /// Вращает точку (3D-вектор) кватернионом.
    /// </summary>
    public Vector Rotate(Vector point)
    {
        var p = new Quaternion(0, point[0], point[1], point[2]);
        var result = this * p * Conjugate;
        return new Vector(new[] { result.X, result.Y, result.Z });
    }

    /// <inheritdoc/>
    public bool Equals(Quaternion other)
    {
        return W == other.W && X == other.X && Y == other.Y && Z == other.Z;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(W, X, Y, Z);
    }
}

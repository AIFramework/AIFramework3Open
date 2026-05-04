using System;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Transforms;

/// <summary>
/// Преобразования между углами Эйлера и матрицей вращения.
/// </summary>
public static class EulerAngles
{
    /// <summary>
    /// Строит матрицу вращения 3×3 из углов Эйлера (yaw, pitch, roll).
    /// Порядок: Rz(yaw) · Ry(pitch) · Rx(roll).
    /// </summary>
    public static Matrix ToMatrix(double yaw, double pitch, double roll)
    {
        double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
        double cp = Math.Cos(pitch), sp = Math.Sin(pitch);
        double cr = Math.Cos(roll), sr = Math.Sin(roll);

        var m = new Matrix(3, 3);
        m[0, 0] = cy * cp;
        m[0, 1] = cy * sp * sr - sy * cr;
        m[0, 2] = cy * sp * cr + sy * sr;
        m[1, 0] = sy * cp;
        m[1, 1] = sy * sp * sr + cy * cr;
        m[1, 2] = sy * sp * cr - cy * sr;
        m[2, 0] = -sp;
        m[2, 1] = cp * sr;
        m[2, 2] = cp * cr;
        return m;
    }

    /// <summary>
    /// Извлекает углы Эйлера (yaw, pitch, roll) из матрицы вращения 3×3.
    /// </summary>
    public static (double yaw, double pitch, double roll) FromMatrix(Matrix m)
    {
        double pitch = Math.Asin(-Clamp(m[2, 0], -1, 1));

        double yaw, roll;
        if (Math.Abs(m[2, 0]) < 0.99999)
        {
            yaw = Math.Atan2(m[1, 0], m[0, 0]);
            roll = Math.Atan2(m[2, 1], m[2, 2]);
        }
        else
        {
            yaw = Math.Atan2(-m[0, 1], m[1, 1]);
            roll = 0;
        }

        return (yaw, pitch, roll);
    }

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}

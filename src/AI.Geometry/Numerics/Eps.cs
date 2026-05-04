using System;

namespace AI.Geometry.Numerics;

/// <summary>
/// Утилиты для сравнения чисел с плавающей точкой с учётом погрешности.
/// </summary>
public static class Eps
{
    /// <summary>
    /// Допуск по умолчанию.
    /// </summary>
    public const double Default = 1e-10;

    /// <summary>
    /// Приблизительное равенство двух чисел с относительным и абсолютным допуском.
    /// </summary>
    public static bool ApproxEqual(double a, double b, double relTol = 1e-9, double absTol = 1e-12)
    {
        double diff = Math.Abs(a - b);
        return diff <= absTol || diff <= relTol * Math.Max(Math.Abs(a), Math.Abs(b));
    }

    /// <summary>
    /// Знак числа с учётом допуска: +1, -1 или 0.
    /// </summary>
    public static int Sign(double x, double eps = 1e-10) => x > eps ? 1 : x < -eps ? -1 : 0;

    /// <summary>
    /// Проверяет, является ли число нулём с учётом допуска.
    /// </summary>
    public static bool IsZero(double x, double eps = 1e-10) => Math.Abs(x) <= eps;
}

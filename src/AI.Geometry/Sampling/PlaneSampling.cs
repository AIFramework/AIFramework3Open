#nullable enable
using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Sampling;

/// <summary>
/// Равномерные случайные точки на плоских фигурах: в круге, на окружности, в треугольнике.
/// </summary>
/// <remarks>
/// Каждый метод есть в двух видах: с генератором <see cref="Random"/> и с готовыми равномерными числами
/// из [0; 1]. Второй вид нужен для детерминированной генерации и для квазислучайных точек
/// (последовательности Холтона и Соболя): отображения выбраны непрерывными и сохраняющими площадь,
/// поэтому равномерность исходных точек квадрата переходит на фигуру.
/// </remarks>
public static class PlaneSampling
{
    /// <summary>Равномерная точка в круге</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="disk">Круг</param>
    public static Vector InDisk(Random rng, Circle disk)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return InDisk(rng.NextDouble(), rng.NextDouble(), disk);
    }

    /// <summary>
    /// Точка в круге по двум равномерным числам: концентрическое отображение Ширли-Чиу квадрата на круг,
    /// сохраняющее площадь и мало искажающее соседство точек
    /// </summary>
    /// <param name="u1">Первое равномерное число из [0; 1]</param>
    /// <param name="u2">Второе равномерное число из [0; 1]</param>
    /// <param name="disk">Круг</param>
    public static Vector InDisk(double u1, double u2, Circle disk)
    {
        ArgumentNullException.ThrowIfNull(disk);
        CheckUnit(u1, nameof(u1));
        CheckUnit(u2, nameof(u2));

        double a = (2 * u1) - 1;
        double b = (2 * u2) - 1;

        if (a == 0 && b == 0)
            return new Vector(disk.Center[0], disk.Center[1]);

        double r, phi;

        if (Math.Abs(a) > Math.Abs(b))
        {
            r = a;
            phi = Math.PI / 4 * (b / a);
        }
        else
        {
            r = b;
            phi = (Math.PI / 2) - (Math.PI / 4 * (a / b));
        }

        r *= disk.Radius;

        return new Vector(disk.Center[0] + (r * Math.Cos(phi)), disk.Center[1] + (r * Math.Sin(phi)));
    }

    /// <summary>Равномерная точка на окружности</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="circle">Окружность</param>
    public static Vector OnCircle(Random rng, Circle circle)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return OnCircle(rng.NextDouble(), circle);
    }

    /// <summary>Точка на окружности по равномерному числу: угол 2π·u от оси X</summary>
    /// <param name="u">Равномерное число из [0; 1]</param>
    /// <param name="circle">Окружность</param>
    public static Vector OnCircle(double u, Circle circle)
    {
        ArgumentNullException.ThrowIfNull(circle);
        CheckUnit(u, nameof(u));

        double angle = 2 * Math.PI * u;

        return new Vector(
            circle.Center[0] + (circle.Radius * Math.Cos(angle)),
            circle.Center[1] + (circle.Radius * Math.Sin(angle)));
    }

    /// <summary>Равномерная точка в треугольнике любой размерности</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="triangle">Треугольник</param>
    public static Vector InTriangle(Random rng, Triangle triangle)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return InTriangle(rng.NextDouble(), rng.NextDouble(), triangle);
    }

    /// <summary>
    /// Точка в треугольнике по двум равномерным числам: <c>A·(1 − √u₁) + B·√u₁·(1 − u₂) + C·√u₁·u₂</c>
    /// </summary>
    /// <param name="u1">Первое равномерное число из [0; 1]</param>
    /// <param name="u2">Второе равномерное число из [0; 1]</param>
    /// <param name="triangle">Треугольник</param>
    public static Vector InTriangle(double u1, double u2, Triangle triangle)
    {
        ArgumentNullException.ThrowIfNull(triangle);
        CheckUnit(u1, nameof(u1));
        CheckUnit(u2, nameof(u2));

        double s = Math.Sqrt(u1);
        double wa = 1 - s;
        double wb = s * (1 - u2);
        double wc = s * u2;
        var point = new Vector(triangle.A.Count);

        for (int i = 0; i < point.Count; i++)
            point[i] = (wa * triangle.A[i]) + (wb * triangle.B[i]) + (wc * triangle.C[i]);

        return point;
    }

    /// <summary>Проверяет, что число лежит в [0; 1]</summary>
    internal static void CheckUnit(double u, string name)
    {
        if (!(u >= 0 && u <= 1))
            throw new ArgumentOutOfRangeException(name, "Равномерное число должно лежать в [0; 1]");
    }
}

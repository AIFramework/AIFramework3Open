#nullable enable
using System;
using AI.Geometry.Primitives;
using AI.Statistics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Sampling;

/// <summary>
/// Равномерные случайные точки в пространстве: направления, сфера, шар, прямоугольный параллелепипед,
/// а также распределение фон Мизеса-Фишера на сфере.
/// </summary>
/// <remarks>
/// Методы с генератором <see cref="Random"/> дополнены методами с готовыми равномерными числами из [0; 1]:
/// они подходят для детерминированной генерации и квазислучайных точек. Направления в трехмерном
/// пространстве берутся по теореме Архимеда о сфере: координата z вдоль оси равномерна на [−1; 1].
/// </remarks>
public static class SpaceSampling
{
    /// <summary>Равномерное направление: точка на единичной сфере</summary>
    /// <param name="rng">Генератор</param>
    public static Vector3 Direction(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return Direction(rng.NextDouble(), rng.NextDouble());
    }

    /// <summary>Направление по двум равномерным числам: <c>z = 1 − 2u₁</c>, долгота 2π·u₂</summary>
    /// <param name="u1">Первое равномерное число из [0; 1]</param>
    /// <param name="u2">Второе равномерное число из [0; 1]</param>
    public static Vector3 Direction(double u1, double u2)
    {
        PlaneSampling.CheckUnit(u1, nameof(u1));
        PlaneSampling.CheckUnit(u2, nameof(u2));

        double z = 1 - (2 * u1);
        double r = Math.Sqrt(Math.Max(0, 1 - (z * z)));
        double phi = 2 * Math.PI * u2;

        return new Vector3(r * Math.Cos(phi), r * Math.Sin(phi), z);
    }

    /// <summary>Равномерное направление в пространстве любой размерности: нормированный гауссов вектор</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="dimension">Размерность пространства, не меньше 2</param>
    public static Vector Direction(Random rng, int dimension)
    {
        ArgumentNullException.ThrowIfNull(rng);

        if (dimension < 2)
            throw new ArgumentOutOfRangeException(nameof(dimension), "Размерность должна быть не меньше 2");

        var direction = new Vector(dimension);
        double norm;

        do
        {
            norm = 0;

            for (int i = 0; i < dimension; i++)
            {
                direction[i] = RandomEngine.NextGaussian(rng);
                norm += direction[i] * direction[i];
            }
        }
        while (norm < 1e-300);

        norm = Math.Sqrt(norm);

        for (int i = 0; i < dimension; i++)
            direction[i] /= norm;

        return direction;
    }

    /// <summary>Равномерная точка на сфере</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус</param>
    public static Vector3 OnSphere(Random rng, Vector3 center, double radius)
        => center + (radius * Direction(rng));

    /// <summary>Точка на сфере по двум равномерным числам</summary>
    /// <param name="u1">Первое равномерное число из [0; 1]</param>
    /// <param name="u2">Второе равномерное число из [0; 1]</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус</param>
    public static Vector3 OnSphere(double u1, double u2, Vector3 center, double radius)
        => center + (radius * Direction(u1, u2));

    /// <summary>Равномерная точка в шаре</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус</param>
    public static Vector3 InBall(Random rng, Vector3 center, double radius)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return InBall(rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), center, radius);
    }

    /// <summary>Точка в шаре по трем равномерным числам: направление по первым двум, радиус R·∛u₃</summary>
    /// <param name="u1">Первое равномерное число из [0; 1]</param>
    /// <param name="u2">Второе равномерное число из [0; 1]</param>
    /// <param name="u3">Третье равномерное число из [0; 1]</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус</param>
    public static Vector3 InBall(double u1, double u2, double u3, Vector3 center, double radius)
    {
        PlaneSampling.CheckUnit(u3, nameof(u3));

        return center + (radius * Math.Cbrt(u3) * Direction(u1, u2));
    }

    /// <summary>Равномерная точка в прямоугольном параллелепипеде любой размерности</summary>
    /// <param name="rng">Генератор</param>
    /// <param name="box">Параллелепипед</param>
    public static Vector InBox(Random rng, Aabb box)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(box);

        var uniforms = new Vector(box.Min.Count);

        for (int i = 0; i < uniforms.Count; i++)
            uniforms[i] = rng.NextDouble();

        return InBox(uniforms, box);
    }

    /// <summary>Точка в параллелепипеде по равномерным числам, по одному на ось</summary>
    /// <param name="uniforms">Равномерные числа из [0; 1], например точка последовательности Холтона</param>
    /// <param name="box">Параллелепипед</param>
    public static Vector InBox(Vector uniforms, Aabb box)
    {
        ArgumentNullException.ThrowIfNull(uniforms);
        ArgumentNullException.ThrowIfNull(box);

        if (uniforms.Count != box.Min.Count || box.Max.Count != box.Min.Count)
            throw new ArgumentException("Число равномерных чисел должно совпадать с размерностью параллелепипеда", nameof(uniforms));

        var point = new Vector(uniforms.Count);

        for (int i = 0; i < point.Count; i++)
        {
            PlaneSampling.CheckUnit(uniforms[i], nameof(uniforms));
            point[i] = box.Min[i] + ((box.Max[i] - box.Min[i]) * uniforms[i]);
        }

        return point;
    }

    /// <summary>
    /// Направление по распределению фон Мизеса-Фишера на сфере: плотность пропорциональна exp(κ·μ·x)
    /// </summary>
    /// <param name="rng">Генератор</param>
    /// <param name="meanDirection">Среднее направление, нормируется внутри</param>
    /// <param name="kappa">Концентрация κ ≥ 0: при нуле направление равномерно, чем больше, тем уже разброс</param>
    public static Vector3 VonMisesFisher(Random rng, Vector3 meanDirection, double kappa)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return VonMisesFisher(rng.NextDouble(), rng.NextDouble(), meanDirection, kappa);
    }

    /// <summary>
    /// Направление фон Мизеса-Фишера на двумерной сфере по двум равномерным числам. В трехмерном
    /// пространстве шаг отбраковки алгоритма Вуда не нужен: косинус угла со средним направлением
    /// обращается точно, <c>w = 1 + ln(u₁ + (1 − u₁)·e^(−2κ))/κ</c>, а долгота равна 2π·u₂
    /// </summary>
    /// <param name="u1">Равномерное число для угла от среднего направления, из [0; 1]</param>
    /// <param name="u2">Равномерное число для долготы, из [0; 1]</param>
    /// <param name="meanDirection">Среднее направление, нормируется внутри</param>
    /// <param name="kappa">Концентрация κ ≥ 0</param>
    public static Vector3 VonMisesFisher(double u1, double u2, Vector3 meanDirection, double kappa)
    {
        PlaneSampling.CheckUnit(u1, nameof(u1));
        PlaneSampling.CheckUnit(u2, nameof(u2));
        CheckKappa(kappa);

        double length = meanDirection.Length;

        if (!(length > 0) || double.IsInfinity(length))
            throw new ArgumentException("Среднее направление должно быть ненулевым и конечным", nameof(meanDirection));

        var mean = meanDirection / length;
        double w = kappa < 1e-8
            ? (2 * u1) - 1
            : 1 + (Math.Log(u1 + ((1 - u1) * Math.Exp(-2 * kappa))) / kappa);
        w = Math.Clamp(w, -1, 1);

        var (e1, e2) = Basis(mean);
        double r = Math.Sqrt(Math.Max(0, 1 - (w * w)));
        double phi = 2 * Math.PI * u2;

        return (w * mean) + (r * Math.Cos(phi) * e1) + (r * Math.Sin(phi) * e2);
    }

    /// <summary>
    /// Направление фон Мизеса-Фишера на сфере в пространстве любой размерности p ≥ 2 по алгоритму Вуда (1994):
    /// косинус угла со средним направлением берется отбраковкой из бета-распределения, а поперечная часть
    /// равномерно по направлениям, перпендикулярным среднему
    /// </summary>
    /// <param name="rng">Генератор</param>
    /// <param name="meanDirection">Среднее направление, нормируется внутри</param>
    /// <param name="kappa">Концентрация κ ≥ 0</param>
    public static Vector VonMisesFisher(Random rng, Vector meanDirection, double kappa)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(meanDirection);
        CheckKappa(kappa);

        int p = meanDirection.Count;

        if (p < 2)
            throw new ArgumentException("Размерность должна быть не меньше 2", nameof(meanDirection));

        double length = Math.Sqrt(Vector.Dot(meanDirection, meanDirection));

        if (!(length > 0) || double.IsInfinity(length))
            throw new ArgumentException("Среднее направление должно быть ненулевым и конечным", nameof(meanDirection));

        if (kappa < 1e-8)
            return Direction(rng, p);

        var mean = (1.0 / length) * meanDirection;
        double m = p - 1;

        // b = (−2κ + √(4κ² + m²))/m, записано без вычитания близких чисел
        double b = m / ((2 * kappa) + Math.Sqrt((4 * kappa * kappa) + (m * m)));
        double x0 = (1 - b) / (1 + b);
        double c = (kappa * x0) + (m * Math.Log(1 - (x0 * x0)));
        double w;

        while (true)
        {
            double z = RandomEngine.NextBeta(rng, m / 2, m / 2);
            w = (1 - ((1 + b) * z)) / (1 - ((1 - b) * z));
            double u = rng.NextDouble();

            if (u > 0 && (kappa * w) + (m * Math.Log(1 - (x0 * w))) - c >= Math.Log(u))
                break;
        }

        // Поперечная часть: гауссов вектор без составляющей вдоль среднего направления
        Vector tangent;
        double norm;

        do
        {
            var g = Direction(rng, p);
            tangent = g - (Vector.Dot(g, mean) * mean);
            norm = Math.Sqrt(Vector.Dot(tangent, tangent));
        }
        while (norm < 1e-12);

        double side = Math.Sqrt(Math.Max(0, 1 - (w * w))) / norm;

        return (w * mean) + (side * tangent);
    }

    private static void CheckKappa(double kappa)
    {
        if (!(kappa >= 0) || double.IsInfinity(kappa))
            throw new ArgumentOutOfRangeException(nameof(kappa), "Концентрация должна быть конечным неотрицательным числом");
    }

    /// <summary>Два единичных вектора, перпендикулярных единичному n и друг другу (Дафф и соавторы, 2017)</summary>
    private static (Vector3 E1, Vector3 E2) Basis(Vector3 n)
    {
        double sign = n.Z >= 0 ? 1.0 : -1.0;
        double a = -1.0 / (sign + n.Z);
        double b = n.X * n.Y * a;

        return (
            new Vector3(1 + (sign * n.X * n.X * a), sign * b, -sign * n.X),
            new Vector3(b, sign + (n.Y * n.Y * a), -n.Y));
    }
}

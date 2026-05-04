using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Conics;

/// <summary>
/// Тип конического сечения.
/// </summary>
public enum ConicType
{
    /// <summary>Окружность.</summary>
    Circle,
    /// <summary>Эллипс.</summary>
    Ellipse,
    /// <summary>Парабола.</summary>
    Parabola,
    /// <summary>Гипербола.</summary>
    Hyperbola,
    /// <summary>Вырожденное сечение.</summary>
    Degenerate
}

/// <summary>
/// Коническое сечение: Ax² + Bxy + Cy² + Dx + Ey + F = 0.
/// </summary>
public class ConicSection
{
    /// <summary>Коэффициент при x².</summary>
    public double A { get; }
    /// <summary>Коэффициент при xy.</summary>
    public double B { get; }
    /// <summary>Коэффициент при y².</summary>
    public double C { get; }
    /// <summary>Коэффициент при x.</summary>
    public double D { get; }
    /// <summary>Коэффициент при y.</summary>
    public double E { get; }
    /// <summary>Свободный член.</summary>
    public double F { get; }

    /// <summary>
    /// Создаёт коническое сечение по шести коэффициентам.
    /// </summary>
    public ConicSection(double a, double b, double c, double d, double e, double f)
    {
        A = a; B = b; C = c; D = d; E = e; F = f;
    }

    /// <summary>
    /// Классифицирует коническое сечение по дискриминанту B²-4AC.
    /// </summary>
    public ConicType Classify()
    {
        double disc = B * B - 4 * A * C;

        // Определитель матрицы 3×3
        double det33 = A * (C * F - E * E / 4.0)
                     - B / 2.0 * (B / 2.0 * F - E * D / 4.0)
                     + D / 2.0 * (B / 2.0 * E / 2.0 - C * D / 2.0);

        // Упрощённая оценка: если определитель ~0 — вырождено
        if (Math.Abs(det33) < 1e-12)
            return ConicType.Degenerate;

        if (Math.Abs(disc) < 1e-12)
            return ConicType.Parabola;

        if (disc < 0)
        {
            if (Math.Abs(A - C) < 1e-12 && Math.Abs(B) < 1e-12)
                return ConicType.Circle;
            return ConicType.Ellipse;
        }

        return ConicType.Hyperbola;
    }

    /// <summary>
    /// Параметрическая выборка n точек для визуализации.
    /// </summary>
    public Vector[] Sample(int n)
    {
        var type = Classify();

        switch (type)
        {
            case ConicType.Circle:
            case ConicType.Ellipse:
                return SampleElliptic(n);
            case ConicType.Parabola:
                return SampleParametric(n, -10, 10);
            case ConicType.Hyperbola:
                return SampleParametric(n, -10, 10);
            default:
                return SampleParametric(n, -10, 10);
        }
    }

    /// <summary>
    /// Создаёт коническое сечение из эллипса.
    /// </summary>
    public static ConicSection FromEllipse(Ellipse e)
    {
        double cos = Math.Cos(e.Angle), sin = Math.Sin(e.Angle);
        double a2 = e.A * e.A, b2 = e.B * e.B;
        double cos2 = cos * cos, sin2 = sin * sin;

        double A = cos2 / a2 + sin2 / b2;
        double B = 2 * cos * sin * (1.0 / a2 - 1.0 / b2);
        double C = sin2 / a2 + cos2 / b2;
        double D = -2 * A * e.Center[0] - B * e.Center[1];
        double E = -B * e.Center[0] - 2 * C * e.Center[1];
        double F = A * e.Center[0] * e.Center[0] + B * e.Center[0] * e.Center[1]
                   + C * e.Center[1] * e.Center[1] - 1;

        return new ConicSection(A, B, C, D, E, F);
    }

    /// <summary>
    /// Создаёт коническое сечение из окружности.
    /// </summary>
    public static ConicSection FromCircle(Circle c)
    {
        double cx = c.Center[0], cy = c.Center[1], r = c.Radius;
        return new ConicSection(1, 0, 1, -2 * cx, -2 * cy, cx * cx + cy * cy - r * r);
    }

    private Vector[] SampleElliptic(int n)
    {
        // Переводим во внутренний эллипс
        double cx, cy, angle, semiA, semiB;
        ToCanonicalEllipse(out cx, out cy, out angle, out semiA, out semiB);

        var result = new Vector[n];
        for (int i = 0; i < n; i++)
        {
            double theta = 2 * Math.PI * i / n;
            double x = semiA * Math.Cos(theta);
            double y = semiB * Math.Sin(theta);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            result[i] = new Vector(new[] { cx + x * cos - y * sin, cy + x * sin + y * cos });
        }
        return result;
    }

    private void ToCanonicalEllipse(out double cx, out double cy, out double angle,
        out double semiA, out double semiB)
    {
        angle = 0.5 * Math.Atan2(B, A - C);
        double cos = Math.Cos(angle), sin = Math.Sin(angle);

        double A2 = A * cos * cos + B * cos * sin + C * sin * sin;
        double C2 = A * sin * sin - B * cos * sin + C * cos * cos;
        double D2 = D * cos + E * sin;
        double E2 = -D * sin + E * cos;
        double F2 = F;

        cx = -D2 / (2 * A2);
        cy = -E2 / (2 * C2);
        double rhs = -F2 + A2 * cx * cx + C2 * cy * cy;

        semiA = Math.Sqrt(Math.Abs(rhs / A2));
        semiB = Math.Sqrt(Math.Abs(rhs / C2));

        double cxR = cx * cos - cy * sin;
        double cyR = cx * sin + cy * cos;
        cx = cxR;
        cy = cyR;
    }

    private Vector[] SampleParametric(int n, double tMin, double tMax)
    {
        var result = new Vector[n];
        for (int i = 0; i < n; i++)
        {
            double t = tMin + (tMax - tMin) * i / (n - 1);
            // Решаем Ax² + Bxy + Cy² + Dx + Ey + F = 0 при x = t
            double qa = C;
            double qb = B * t + E;
            double qc = A * t * t + D * t + F;
            double disc = qb * qb - 4 * qa * qc;
            double y = disc >= 0 ? (-qb + Math.Sqrt(disc)) / (2 * qa) : 0;
            result[i] = new Vector(new[] { t, y });
        }
        return result;
    }
}

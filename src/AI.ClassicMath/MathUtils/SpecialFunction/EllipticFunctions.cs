using System;

namespace AI.MathUtils.SpecialFunction;

/// <summary>
/// Полный эллиптический интеграл первого рода и эллиптические функции Якоби (параметр m = k²).
/// Точность — до машинной: среднее арифметико-геометрическое и спуск Ландена (как в Cephes и scipy.special).
/// </summary>
public static class EllipticFunctions
{
    private const double MachineEpsilon = 1.11022302462515654042e-16;

    /// <summary>
    /// Полный эллиптический интеграл первого рода K(m) = π / (2·AGM(1, √(1 − m))), 0 ≤ m &lt; 1.
    /// </summary>
    /// <param name="m">Параметр m = k².</param>
    public static double CompleteK(double m)
    {
        if (m is < 0 or > 1 || double.IsNaN(m))
        {
            throw new ArgumentOutOfRangeException(nameof(m), "Параметр m должен лежать в [0; 1].");
        }

        return m == 1 ? double.PositiveInfinity : Math.PI / (2 * Agm(1, Math.Sqrt(1 - m)));
    }

    /// <summary>
    /// K(1 − p) без потери точности при малых p (аналог scipy.special.ellipkm1).
    /// </summary>
    /// <param name="p">Дополнительный параметр p = 1 − m.</param>
    public static double CompleteKm1(double p)
    {
        if (p is < 0 or > 1 || double.IsNaN(p))
        {
            throw new ArgumentOutOfRangeException(nameof(p), "Параметр p должен лежать в [0; 1].");
        }

        return p == 0 ? double.PositiveInfinity : Math.PI / (2 * Agm(1, Math.Sqrt(p)));
    }

    /// <summary>
    /// Эллиптические функции Якоби sn, cn, dn и амплитуда φ (алгоритм ellpj из Cephes).
    /// </summary>
    /// <param name="u">Аргумент.</param>
    /// <param name="m">Параметр m = k², 0 ≤ m ≤ 1.</param>
    /// <returns>(sn, cn, dn, φ).</returns>
    public static (double Sn, double Cn, double Dn, double Phi) Jacobi(double u, double m)
    {
        if (m is < 0 or > 1 || double.IsNaN(m))
        {
            throw new ArgumentOutOfRangeException(nameof(m), "Параметр m должен лежать в [0; 1].");
        }

        if (m < 1e-9)
        {
            double t = Math.Sin(u);
            double b = Math.Cos(u);
            double ai = 0.25 * m * (u - (t * b));
            return (t - (ai * b), b + (ai * t), 1.0 - (0.5 * m * t * t), u - ai);
        }

        if (m >= 0.9999999999)
        {
            double ai = 0.25 * (1.0 - m);
            double b = Math.Cosh(u);
            double t = Math.Tanh(u);
            double phi = 1.0 / b;
            double twon = b * Math.Sinh(u);
            double sn = t + (ai * (twon - u) / (b * b));
            double ph = (2.0 * Math.Atan(Math.Exp(u))) - (Math.PI / 2) + (ai * (twon - u) / b);
            ai *= t * phi;
            return (sn, phi - (ai * (twon - u)), phi + (ai * (twon + u)), ph);
        }

        var a = new double[9];
        var c = new double[9];
        a[0] = 1.0;
        double bb = Math.Sqrt(1.0 - m);
        c[0] = Math.Sqrt(m);
        double twoN = 1.0;
        int i = 0;

        while (Math.Abs(c[i] / a[i]) > MachineEpsilon)
        {
            if (i > 7)
            {
                break;
            }

            double ai = a[i];
            ++i;
            c[i] = (ai - bb) / 2.0;
            double tt = Math.Sqrt(ai * bb);
            a[i] = (ai + bb) / 2.0;
            bb = tt;
            twoN *= 2.0;
        }

        double phiN = twoN * a[i] * u;
        double prev;
        do
        {
            double t = c[i] * Math.Sin(phiN) / a[i];
            prev = phiN;
            phiN = (Math.Asin(t) + phiN) / 2.0;
        }
        while (--i != 0);

        double s = Math.Sin(phiN);
        double co = Math.Cos(phiN);
        return (s, co, co / Math.Cos(phiN - prev), phiN);
    }

    private static double Agm(double a, double b)
    {
        for (int i = 0; i < 64 && Math.Abs(a - b) > MachineEpsilon * a; i++)
        {
            double an = (a + b) / 2;
            b = Math.Sqrt(a * b);
            a = an;
        }

        return a;
    }
}

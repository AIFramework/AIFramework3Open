using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AI.MathUtils.SpecialFunction;

namespace AI.DSP.IIR;

/// <summary>
/// Аналоговые ФНЧ-прототипы с частотой среза 1 рад/с в виде нулей, полюсов и усиления
/// (те же формулы, что buttap, cheb1ap, cheb2ap, ellipap и besselap в scipy.signal).
/// </summary>
public static class AnalogPrototype
{
    /// <summary>Баттерворт порядка n: полюсы на единичной окружности в левой полуплоскости.</summary>
    public static ZeroPoleGain Butterworth(int order)
    {
        CheckOrder(order);
        Complex[] p = Odd(order).Select(m => -Complex.Exp(Complex.ImaginaryOne * Math.PI * m / (2.0 * order))).ToArray();
        return new ZeroPoleGain(Array.Empty<Complex>(), p, 1.0);
    }

    /// <summary>Чебышёв I рода: неравномерность rippleDb в полосе пропускания.</summary>
    public static ZeroPoleGain ChebyshevI(int order, double rippleDb)
    {
        CheckOrder(order);
        double eps = Math.Sqrt(Math.Pow(10, 0.1 * rippleDb) - 1.0);
        double mu = Math.Asinh(1.0 / eps) / order;
        Complex[] p = Odd(order).Select(m => -Complex.Sinh(new Complex(mu, Math.PI * m / (2.0 * order)))).ToArray();
        double k = Product(p.Select(x => -x)).Real;
        if (order % 2 == 0)
        {
            k /= Math.Sqrt(1 + (eps * eps));
        }

        return new ZeroPoleGain(Array.Empty<Complex>(), p, k);
    }

    /// <summary>Чебышёв II рода: затухание attenuationDb в полосе задерживания (частота 1 — её граница).</summary>
    public static ZeroPoleGain ChebyshevII(int order, double attenuationDb)
    {
        CheckOrder(order);
        double de = 1.0 / Math.Sqrt(Math.Pow(10, 0.1 * attenuationDb) - 1);
        double mu = Math.Asinh(1.0 / de) / order;
        IEnumerable<int> mz = order % 2 == 1
            ? Enumerable.Range(0, order).Select(i => -order + 1 + (2 * i)).Where(m => m != 0)
            : Odd(order);
        Complex[] z = mz.Select(m => -Complex.Conjugate(Complex.ImaginaryOne / Math.Sin(m * Math.PI / (2.0 * order)))).ToArray();
        Complex[] p = Odd(order).Select(m =>
        {
            Complex b = -Complex.Exp(Complex.ImaginaryOne * Math.PI * m / (2.0 * order));
            return 1.0 / new Complex(Math.Sinh(mu) * b.Real, Math.Cosh(mu) * b.Imaginary);
        }).ToArray();
        double k = (Product(p.Select(x => -x)) / Product(z.Select(x => -x))).Real;
        return new ZeroPoleGain(z, p, k);
    }

    /// <summary>Эллиптический: неравномерность rippleDb и затухание attenuationDb (эллиптические функции Якоби).</summary>
    public static ZeroPoleGain Elliptic(int order, double rippleDb, double attenuationDb)
    {
        CheckOrder(order);
        if (order == 1)
        {
            double p1 = -Math.Sqrt(1.0 / Pow10m1(0.1 * rippleDb));
            return new ZeroPoleGain(Array.Empty<Complex>(), new[] { new Complex(p1, 0) }, -p1);
        }

        double epsSq = Pow10m1(0.1 * rippleDb);
        double eps = Math.Sqrt(epsSq);
        double ck1Sq = epsSq / Pow10m1(0.1 * attenuationDb);
        if (ck1Sq == 0)
        {
            throw new ArgumentException("Затухание слишком велико для эллиптического фильтра при данной неравномерности.");
        }

        double k0 = EllipticFunctions.CompleteK(ck1Sq);
        double m = EllipticDegree(order, ck1Sq);
        double capK = EllipticFunctions.CompleteK(m);
        int[] j = Enumerable.Range(0, order).Select(i => (1 - (order % 2)) + (2 * i)).Where(v => v < order).ToArray();
        var s = new double[j.Length];
        var c = new double[j.Length];
        var d = new double[j.Length];
        for (int i = 0; i < j.Length; i++)
        {
            (s[i], c[i], d[i], _) = EllipticFunctions.Jacobi(j[i] * capK / order, m);
        }

        var zeros = new List<Complex>();
        foreach (double sv in s.Where(v => Math.Abs(v) > 2.220446049250313e-16))
        {
            zeros.Add(Complex.ImaginaryOne / (Math.Sqrt(m) * sv));
        }

        zeros.AddRange(zeros.Select(Complex.Conjugate).ToList());

        double r = ArcJacobiSc1(1.0 / eps, ck1Sq);
        double v0 = capK * r / (order * k0);
        (double svv, double cvv, double dvv, _) = EllipticFunctions.Jacobi(v0, 1 - m);
        var poles = new List<Complex>();
        for (int i = 0; i < j.Length; i++)
        {
            double den = 1 - Math.Pow(d[i] * svv, 2.0);
            poles.Add(new Complex(-(c[i] * d[i] * svv * cvv) / den, -(s[i] * dvv) / den));
        }

        if (order % 2 == 1)
        {
            double norm = Math.Sqrt(poles.Sum(p => (p * Complex.Conjugate(p)).Real));
            poles.AddRange(poles.Where(p => Math.Abs(p.Imaginary) > 2.220446049250313e-16 * norm).Select(Complex.Conjugate).ToList());
        }
        else
        {
            poles.AddRange(poles.Select(Complex.Conjugate).ToList());
        }

        double k = (Product(poles.Select(x => -x)) / Product(zeros.Select(x => -x))).Real;
        if (order % 2 == 0)
        {
            k /= Math.Sqrt(1 + epsSq);
        }

        return new ZeroPoleGain(zeros.ToArray(), poles.ToArray(), k);
    }

    /// <summary>Бессель порядка n с нормировкой частоты.</summary>
    public static ZeroPoleGain Bessel(int order, BesselNormalization normalization = BesselNormalization.Phase)
    {
        CheckOrder(order);
        Complex[] p = ReverseBesselRoots(order);
        double aLast = 1;
        for (int i = order + 1; i <= 2 * order; i++)
        {
            aLast *= i / 2.0;   // (2n)! / (n!·2^n)
        }

        if (normalization == BesselNormalization.Phase)
        {
            double scale = Math.Pow(10, -Math.Log10(aLast) / order);
            return new ZeroPoleGain(Array.Empty<Complex>(), p.Select(x => x * scale).ToArray(), 1.0);
        }

        if (normalization == BesselNormalization.Delay)
        {
            return new ZeroPoleGain(Array.Empty<Complex>(), p, aLast);
        }

        double w = MagnitudeNormFactor(p, aLast);
        return new ZeroPoleGain(Array.Empty<Complex>(), p.Select(x => x / w).ToArray(), Math.Pow(w, -order) * aLast);
    }

    // ---------------------------------------------------------------- вспомогательное

    private static void CheckOrder(int order)
    {
        if (order < 1 || order > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(order), $"Порядок фильтра должен быть от 1 до 256, дано {order}.");
        }
    }

    private static IEnumerable<int> Odd(int order) => Enumerable.Range(0, order).Select(i => -order + 1 + (2 * i));

    private static Complex Product(IEnumerable<Complex> values) => values.Aggregate(Complex.One, (a, b) => a * b);

    /// <summary>10^x − 1 без потери точности при малых x (как scipy _pow10m1 через expm1).</summary>
    private static double Pow10m1(double x)
    {
        // Точный expm1 по Кахану: (u − 1)·y / ln u
        double y = Math.Log(10) * x;
        double u = Math.Exp(y);
        if (u == 1.0)
        {
            return y;
        }

        double um1 = u - 1.0;
        return um1 == -1.0 ? -1.0 : um1 * y / Math.Log(u);
    }

    /// <summary>Модуль эллиптической функции по порядку и дополнительному модулю (scipy _ellipdeg).</summary>
    private static double EllipticDegree(int n, double m1)
    {
        double k1 = EllipticFunctions.CompleteK(m1);
        double k1p = EllipticFunctions.CompleteKm1(m1);
        double q1 = Math.Exp(-Math.PI * k1p / k1);
        double q = Math.Pow(q1, 1.0 / n);
        double num = 0;
        for (int i = 0; i <= 7; i++)
        {
            num += Math.Pow(q, i * (i + 1));
        }

        double den = 1;
        for (int i = 1; i <= 8; i++)
        {
            den += 2 * Math.Pow(q, i * i);
        }

        return 16 * q * Math.Pow(num / den, 4);
    }

    /// <summary>Обратная функция sc Якоби с действительным аргументом (scipy _arc_jac_sc1).</summary>
    private static double ArcJacobiSc1(double w, double m) => ArcJacobiSn(Complex.ImaginaryOne * w, m).Imaginary;

    /// <summary>Обратная функция sn Якоби комплексного аргумента спуском Ландена (scipy _arc_jac_sn).</summary>
    private static Complex ArcJacobiSn(Complex w, double m)
    {
        static Complex Complement(Complex kx) => Complex.Sqrt((1 - kx) * (1 + kx));

        double k = Math.Sqrt(m);
        if (k >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(m), "Модуль должен быть меньше 1.");
        }

        var ks = new List<double> { k };
        while (ks[^1] != 0)
        {
            double kp = Complement(ks[^1]).Real;
            ks.Add((1 - kp) / (1 + kp));
            if (ks.Count > 12)
            {
                break;
            }
        }

        double capK = Math.PI / 2;
        for (int i = 1; i < ks.Count; i++)
        {
            capK *= 1 + ks[i];
        }

        Complex wn = w;
        for (int i = 0; i + 1 < ks.Count; i++)
        {
            wn = 2 * wn / ((1 + ks[i + 1]) * (1 + Complement(ks[i] * wn)));
        }

        return capK * (2 / Math.PI) * Complex.Asin(wn);
    }

    /// <summary>
    /// Корни обратного полинома Бесселя θₙ(s) методом Аберта; значения полинома — по трёхчленной рекуррентности
    /// θₙ = (2n − 1)θₙ₋₁ + s²θₙ₋₂ (устойчивее, чем по коэффициентам до 10²³).
    /// </summary>
    private static Complex[] ReverseBesselRoots(int n)
    {
        double a0 = 1;
        for (int i = n + 1; i <= 2 * n; i++)
        {
            a0 *= i / 2.0;
        }

        double radius = Math.Pow(a0, 1.0 / n);
        var z = new Complex[n];
        for (int k = 0; k < n; k++)
        {
            double angle = (Math.PI / 2) + (Math.PI * ((2 * k) + 1) / (2.0 * n));
            z[k] = Complex.FromPolarCoordinates(radius * (1 + (0.01 * k / n)), angle);
        }

        for (int iter = 0; iter < 500; iter++)
        {
            double change = 0;
            for (int k = 0; k < n; k++)
            {
                (Complex v, Complex dv) = Theta(n, z[k]);
                Complex ratio = v / dv;
                Complex sum = Complex.Zero;
                for (int j = 0; j < n; j++)
                {
                    if (j != k)
                    {
                        sum += 1.0 / (z[k] - z[j]);
                    }
                }

                Complex step = ratio / (1 - (ratio * sum));
                z[k] -= step;
                change = Math.Max(change, step.Magnitude / Math.Max(1, z[k].Magnitude));
            }

            if (change < 1e-16)
            {
                break;
            }
        }

        // Уточнение Ньютоном в decimal (28 знаков): в double значение θₙ у корня теряется в сокращениях
        for (int k = 0; k < n; k++)
        {
            z[k] = Polish(n, z[k]);
        }

        // Пары сопряжённых корней и действительный корень при нечётном n — точно симметричными
        Complex[] upper = z.Where(r => r.Imaginary > 1e-9 * r.Magnitude).OrderBy(r => r.Real).ToArray();
        var roots = new List<Complex>();
        foreach (Complex r in upper)
        {
            roots.Add(r);
            roots.Add(Complex.Conjugate(r));
        }

        if (n % 2 == 1)
        {
            roots.Add(new Complex(z.OrderBy(r => Math.Abs(r.Imaginary)).First().Real, 0));
        }

        return [.. roots];
    }

    /// <summary>
    /// Шаги Ньютона для нормированного полинома φₙ = θₙ / (2n − 1)!! в арифметике decimal:
    /// φₖ = φₖ₋₁ + s²φₖ₋₂ / ((2k − 1)(2k − 3)), значения остаются умеренными при любом n.
    /// </summary>
    private static Complex Polish(int n, Complex root)
    {
        if (n > 40 || !(root.Magnitude < 1e6))
        {
            return root;
        }

        try
        {
            return PolishDecimal(n, root);
        }
        catch (OverflowException)
        {
            return root;
        }
    }

    private static Complex PolishDecimal(int n, Complex root)
    {
        var s = new DecimalComplex((decimal)root.Real, (decimal)root.Imaginary);
        for (int iter = 0; iter < 3; iter++)
        {
            DecimalComplex f0 = DecimalComplex.One, f1 = s + DecimalComplex.One;
            DecimalComplex d0 = DecimalComplex.Zero, d1 = DecimalComplex.One;
            DecimalComplex s2 = s * s;
            for (int k = 2; k <= n; k++)
            {
                decimal c = 1m / (((2 * k) - 1) * ((2 * k) - 3));
                DecimalComplex f2 = f1 + (s2 * f0 * c);
                DecimalComplex d2 = d1 + (((s * f0 * 2m) + (s2 * d0)) * c);
                (f0, f1, d0, d1) = (f1, f2, d1, d2);
            }

            if (d1.IsZero)
            {
                break;
            }

            s -= f1 / d1;
        }

        return new Complex((double)s.Re, (double)s.Im);
    }

    private static (Complex Value, Complex Derivative) Theta(int n, Complex s)
    {
        // θ₀ = 1, θ₁ = s + 1; производные — той же рекуррентностью
        Complex t0 = 1, t1 = s + 1, d0 = 0, d1 = 1;
        for (int k = 2; k <= n; k++)
        {
            Complex t2 = ((2 * k) - 1) * t1 + (s * s * t0);
            Complex d2 = ((2 * k) - 1) * d1 + (2 * s * t0) + (s * s * d0);
            (t0, t1, d0, d1) = (t1, t2, d1, d2);
        }

        return n == 0 ? (t0, d0) : (t1, d1);
    }

    private readonly record struct DecimalComplex(decimal Re, decimal Im)
    {
        public static DecimalComplex One => new(1m, 0m);

        public static DecimalComplex Zero => new(0m, 0m);

        public bool IsZero => Re == 0 && Im == 0;

        public static DecimalComplex operator +(DecimalComplex a, DecimalComplex b) => new(a.Re + b.Re, a.Im + b.Im);

        public static DecimalComplex operator -(DecimalComplex a, DecimalComplex b) => new(a.Re - b.Re, a.Im - b.Im);

        public static DecimalComplex operator *(DecimalComplex a, DecimalComplex b)
            => new((a.Re * b.Re) - (a.Im * b.Im), (a.Re * b.Im) + (a.Im * b.Re));

        public static DecimalComplex operator *(DecimalComplex a, decimal b) => new(a.Re * b, a.Im * b);

        public static DecimalComplex operator /(DecimalComplex a, DecimalComplex b)
        {
            decimal den = (b.Re * b.Re) + (b.Im * b.Im);
            return new(((a.Re * b.Re) + (a.Im * b.Im)) / den, ((a.Im * b.Re) - (a.Re * b.Im)) / den);
        }
    }

    /// <summary>Частота, на которой |H(jω)| = 1/√2 (нормировка Бесселя по АЧХ).</summary>
    private static double MagnitudeNormFactor(Complex[] p, double k)
    {
        double Gain(double w) => k / p.Aggregate(Complex.One, (acc, x) => acc * ((Complex.ImaginaryOne * w) - x)).Magnitude;

        double lo = 1e-3, hi = 1e3;
        for (int i = 0; i < 200; i++)
        {
            double mid = Math.Sqrt(lo * hi);
            if (Gain(mid) > 1 / Math.Sqrt(2))
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return Math.Sqrt(lo * hi);
    }
}

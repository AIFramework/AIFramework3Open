using System;
using System.Numerics;

namespace AI.HighLevelFunctions;

/// <summary>
/// Специальные функции, которых нет в стандартной библиотеке: функция Бесселя J₀ и интегралы Френеля.
/// </summary>
/// <remarks>
/// Нужны задачам распространения волн: J₀ — автокорреляция замираний по Кларку, интегралы Френеля —
/// дифракция на крае. Прежде интегралы Френеля жили закрытыми методами символьного решателя; теперь
/// они здесь, и решатель пользуется ими же.
/// </remarks>
public static class SpecialFunctions
{
    private const int MaxIterations = 300;
    private const double Tolerance = 1e-15;

    /// <summary>
    /// Функция Бесселя первого рода нулевого порядка J₀(x)
    /// </summary>
    /// <param name="x">Аргумент</param>
    /// <remarks>
    /// Считается по интегральному представлению <c>J₀(x) = (1/π)·∫₀^π cos(x·sin θ) dθ</c> формулой
    /// трапеций. Подынтегральная функция гладкая и периодическая, поэтому ошибка трапеций убывает
    /// быстрее любой степени числа узлов: при числе узлов больше |x| + 32 она на уровне машинной точности
    /// при любом x, без переключения между рядом и асимптотикой.
    /// </remarks>
    public static double BesselJ0(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x))
            return double.IsInfinity(x) ? 0 : double.NaN;

        int nodes = 32 + (int)Math.Ceiling(Math.Abs(x));
        double sum = 0;

        for (int k = 0; k < nodes; k++)
            sum += Math.Cos(x * Math.Sin(Math.PI * k / nodes));

        return sum / nodes;
    }

    /// <summary>Интеграл Френеля <c>C(x) = ∫₀ˣ cos(πt²/2) dt</c></summary>
    /// <param name="x">Аргумент</param>
    public static double FresnelC(double x) => Fresnel(x).C;

    /// <summary>Интеграл Френеля <c>S(x) = ∫₀ˣ sin(πt²/2) dt</c></summary>
    /// <param name="x">Аргумент</param>
    public static double FresnelS(double x) => Fresnel(x).S;

    /// <summary>
    /// Оба интеграла Френеля сразу
    /// </summary>
    /// <param name="x">Аргумент</param>
    /// <remarks>
    /// При |x| ≤ 1,5 — степенной ряд, дальше — цепная дробь для дополнительной функции ошибок
    /// комплексного аргумента (схема «Численных рецептов»). Ряд при больших x теряет точность
    /// на взаимном уничтожении слагаемых, цепная дробь при малых сходится медленно — поэтому граница.
    /// Обе функции нечётны и стремятся к 1/2 при x → ∞.
    /// </remarks>
    public static (double C, double S) Fresnel(double x)
    {
        if (double.IsNaN(x))
            return (double.NaN, double.NaN);

        if (double.IsInfinity(x))
            return x > 0 ? (0.5, 0.5) : (-0.5, -0.5);

        double ax = Math.Abs(x);
        double c, s;

        if (ax < 1e-150)
        {
            c = ax;
            s = 0;
        }
        else if (ax <= 1.5)
        {
            double sum = 0, sumS = 0, sumC = ax, sign = 1, term = ax;
            double factor = Math.PI / 2 * ax * ax;
            bool odd = true;
            int n = 3;

            // Слагаемые рядов S и C идут вперемежку: нечётные шаги пополняют S, чётные — C
            for (int k = 1; k <= MaxIterations; k++)
            {
                term *= factor / k;
                sum += sign * term / n;
                double test = Math.Abs(sum) * Tolerance;

                if (odd)
                {
                    sign = -sign;
                    sumS = sum;
                    sum = sumC;
                }
                else
                {
                    sumC = sum;
                    sum = sumS;
                }

                if (term < test)
                    break;

                odd = !odd;
                n += 2;
            }

            s = sumS;
            c = sumC;
        }
        else
        {
            double pix2 = Math.PI * ax * ax;
            var b = new Complex(1, -pix2);
            var cc = new Complex(1e300, 0);
            Complex d = 1 / b;
            Complex h = d;
            int n = -1;

            for (int k = 2; k <= MaxIterations; k++)
            {
                n += 2;
                double a = -n * (n + 1);
                b += 4;
                d = 1 / ((a * d) + b);
                cc = b + (a / cc);
                Complex delta = cc * d;
                h *= delta;

                if (Math.Abs(delta.Real - 1) + Math.Abs(delta.Imaginary) < Tolerance)
                    break;
            }

            h *= new Complex(ax, -ax);
            Complex cs = new Complex(0.5, 0.5) * (1 - (new Complex(Math.Cos(0.5 * pix2), Math.Sin(0.5 * pix2)) * h));
            c = cs.Real;
            s = cs.Imaginary;
        }

        return x < 0 ? (-c, -s) : (c, s);
    }
}

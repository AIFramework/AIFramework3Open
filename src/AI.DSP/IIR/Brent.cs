using System;

namespace AI.DSP.IIR;

/// <summary>
/// Минимум функции одной переменной на отрезке методом Брента — шаг в шаг как scipy.optimize.fminbound
/// (xatol = 1e-5): от этого зависят частоты среза режекторных фильтров, рассчитанных по ТЗ.
/// </summary>
internal static class Brent
{
    public static double Minimize(Func<double, double> f, double lower, double upper, double xatol = 1e-5, int maxIterations = 500)
    {
        double sqrtEps = Math.Sqrt(2.2e-16);
        double goldenMean = 0.5 * (3.0 - Math.Sqrt(5.0));
        double a = lower, b = upper;
        double fulc = a + (goldenMean * (b - a));
        double nfc = fulc, xf = fulc;
        double rat = 0.0, e = 0.0;
        double x = xf;
        double fx = f(x);
        int num = 1;
        double ffulc = fx, fnfc = fx;
        double xm = 0.5 * (a + b);
        double tol1 = (sqrtEps * Math.Abs(xf)) + (xatol / 3.0);
        double tol2 = 2.0 * tol1;

        while (Math.Abs(xf - xm) > (tol2 - (0.5 * (b - a))))
        {
            bool golden = true;
            if (Math.Abs(e) > tol1)
            {
                golden = false;
                double r = (xf - nfc) * (fx - ffulc);
                double q = (xf - fulc) * (fx - fnfc);
                double p = ((xf - fulc) * q) - ((xf - nfc) * r);
                q = 2.0 * (q - r);
                if (q > 0.0)
                {
                    p = -p;
                }

                q = Math.Abs(q);
                r = e;
                e = rat;

                if (Math.Abs(p) < Math.Abs(0.5 * q * r) && p > q * (a - xf) && p < q * (b - xf))
                {
                    rat = (p + 0.0) / q;
                    x = xf + rat;
                    if ((x - a) < tol2 || (b - x) < tol2)
                    {
                        double si0 = Math.Sign(xm - xf) + ((xm - xf) == 0 ? 1 : 0);
                        rat = tol1 * si0;
                    }
                }
                else
                {
                    golden = true;
                }
            }

            if (golden)
            {
                e = xf >= xm ? a - xf : b - xf;
                rat = goldenMean * e;
            }

            double si = Math.Sign(rat) + (rat == 0 ? 1 : 0);
            x = xf + (si * Math.Max(Math.Abs(rat), tol1));
            double fu = f(x);
            num++;

            if (fu <= fx)
            {
                if (x >= xf)
                {
                    a = xf;
                }
                else
                {
                    b = xf;
                }

                (fulc, ffulc) = (nfc, fnfc);
                (nfc, fnfc) = (xf, fx);
                (xf, fx) = (x, fu);
            }
            else
            {
                if (x < xf)
                {
                    a = x;
                }
                else
                {
                    b = x;
                }

                if (fu <= fnfc || nfc == xf)
                {
                    (fulc, ffulc) = (nfc, fnfc);
                    (nfc, fnfc) = (x, fu);
                }
                else if (fu <= ffulc || fulc == xf || fulc == nfc)
                {
                    (fulc, ffulc) = (x, fu);
                }
            }

            xm = 0.5 * (a + b);
            tol1 = (sqrtEps * Math.Abs(xf)) + (xatol / 3.0);
            tol2 = 2.0 * tol1;
            if (num >= maxIterations)
            {
                break;
            }
        }

        return xf;
    }
}

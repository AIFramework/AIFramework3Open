using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AI.DSP.IIR;

/// <summary>
/// Разбиение цифрового фильтра из нулей и полюсов на звенья второго порядка: алгоритм zpk2sos (pairing='nearest')
/// из scipy.signal. Полюсы, ближайшие к единичной окружности, попадают в последние звенья; каждому полюсу —
/// ближайший нуль; усиление — в первое звено.
/// </summary>
internal static class ZpkToSos
{
    public static double[,] Convert(Complex[] zeros, Complex[] poles, double gain)
    {
        if (zeros.Length == 0 && poles.Length == 0)
        {
            return new double[,] { { gain, 0, 0, 1, 0, 0 } };
        }

        var p = poles.Concat(Enumerable.Repeat(Complex.Zero, Math.Max(zeros.Length - poles.Length, 0))).ToList();
        var z = zeros.Concat(Enumerable.Repeat(Complex.Zero, Math.Max(poles.Length - zeros.Length, 0))).ToList();
        int sections = (Math.Max(p.Count, z.Count) + 1) / 2;
        if (p.Count % 2 == 1)
        {
            p.Add(Complex.Zero);
            z.Add(Complex.Zero);
        }

        z = CplxReal(z);
        p = CplxReal(p);
        var sos = new double[sections, 6];

        for (int si = sections - 1; si >= 0; si--)
        {
            int p1Index = Worst(p);
            Complex p1 = p[p1Index];
            p.RemoveAt(p1Index);
            double[] row;

            if (IsReal(p1) && p.Count(IsReal) == 0)
            {
                // Последний действительный полюс
                Complex z1 = Take(z, NearestIndex(z, p1, Which.Real));
                row = Section([z1, Complex.Zero], [p1, Complex.Zero]);
            }
            else if (p.Count + 1 == z.Count && !IsReal(p1) && p.Count(IsReal) == 1 && z.Count(IsReal) == 1)
            {
                // Остались один действительный полюс и один действительный нуль: комплексному полюсу — комплексный нуль
                Complex z1 = Take(z, NearestIndex(z, p1, Which.Complex));
                row = Section([z1, Complex.Conjugate(z1)], [p1, Complex.Conjugate(p1)]);
            }
            else
            {
                Complex p2;
                if (IsReal(p1))
                {
                    List<int> realIdx = Enumerable.Range(0, p.Count).Where(i => IsReal(p[i])).ToList();
                    int p2Index = realIdx[Worst(realIdx.Select(i => p[i]).ToList())];
                    p2 = Take(p, p2Index);
                }
                else
                {
                    p2 = Complex.Conjugate(p1);
                }

                if (z.Count > 0)
                {
                    Complex z1 = Take(z, NearestIndex(z, p1, Which.Any));
                    if (!IsReal(z1))
                    {
                        row = Section([z1, Complex.Conjugate(z1)], [p1, p2]);
                    }
                    else if (z.Count > 0)
                    {
                        Complex z2 = Take(z, NearestIndex(z, p1, Which.Real));
                        row = Section([z1, z2], [p1, p2]);
                    }
                    else
                    {
                        row = Section([z1], [p1, p2]);
                    }
                }
                else
                {
                    row = Section([], [p1, p2]);
                }
            }

            for (int j = 0; j < 6; j++)
            {
                sos[si, j] = row[j];
            }
        }

        for (int j = 0; j < 3; j++)
        {
            sos[0, j] *= gain;
        }

        return sos;
    }

    private enum Which
    {
        Real,
        Complex,
        Any,
    }

    private static bool IsReal(Complex c) => c.Imaginary == 0;

    private static Complex Take(List<Complex> list, int index)
    {
        Complex c = list[index];
        list.RemoveAt(index);
        return c;
    }

    // Ближайший к единичной окружности (первый при равенстве, как np.argmin)
    private static int Worst(List<Complex> p)
    {
        int best = 0;
        for (int i = 1; i < p.Count; i++)
        {
            if (Math.Abs(1 - p[i].Magnitude) < Math.Abs(1 - p[best].Magnitude))
            {
                best = i;
            }
        }

        return best;
    }

    private static int NearestIndex(List<Complex> from, Complex to, Which which)
    {
        // Устойчивая сортировка по расстоянию; при равенстве — меньший индекс
        IEnumerable<int> order = Enumerable.Range(0, from.Count).OrderBy(i => (from[i] - to).Magnitude);
        return which switch
        {
            Which.Any => order.First(),
            Which.Real => order.First(i => IsReal(from[i])),
            _ => order.First(i => !IsReal(from[i])),
        };
    }

    private static double[] Section(Complex[] z, Complex[] p)
    {
        double[] b = Poly(z);
        double[] a = Poly(p);
        var row = new double[6];
        Array.Copy(b, 0, row, 3 - b.Length, b.Length);
        Array.Copy(a, 0, row, 6 - a.Length, a.Length);
        return row;
    }

    // Коэффициенты Π(x − r) по убыванию степени, действительные части (корни — сопряжённые пары или действительные)
    private static double[] Poly(Complex[] roots)
    {
        var c = new Complex[roots.Length + 1];
        c[0] = Complex.One;
        for (int i = 0; i < roots.Length; i++)
        {
            for (int j = i + 1; j >= 1; j--)
            {
                c[j] -= roots[i] * c[j - 1];
            }
        }

        return c.Select(v => v.Real).ToArray();
    }

    /// <summary>
    /// Разделение на сопряжённые пары (представитель с Im &gt; 0) и действительные значения (scipy _cplxreal).
    /// </summary>
    private static List<Complex> CplxReal(List<Complex> values)
    {
        if (values.Count == 0)
        {
            return values;
        }

        const double tol = 100 * 2.220446049250313e-16;
        List<Complex> z = values.OrderBy(v => v.Real).ThenBy(v => Math.Abs(v.Imaginary)).ToList();
        List<Complex> reals = z.Where(v => Math.Abs(v.Imaginary) <= tol * v.Magnitude).Select(v => new Complex(v.Real, 0)).ToList();
        List<Complex> rest = z.Where(v => Math.Abs(v.Imaginary) > tol * v.Magnitude).ToList();
        List<Complex> zp = rest.Where(v => v.Imaginary > 0).ToList();
        List<Complex> zn = rest.Where(v => v.Imaginary < 0).ToList();
        if (zp.Count != zn.Count)
        {
            throw new ArgumentException("Комплексный нуль или полюс без сопряжённой пары.");
        }

        // Внутри серий с одинаковой действительной частью — по модулю мнимой
        int start = 0;
        while (start < zp.Count)
        {
            int stop = start;
            while (stop + 1 < zp.Count && zp[stop + 1].Real - zp[stop].Real <= tol * zp[stop].Magnitude)
            {
                stop++;
            }

            if (stop > start)
            {
                SortRun(zp, start, stop);
                SortRun(zn, start, stop);
            }

            start = stop + 1;
        }

        var result = new List<Complex>();
        for (int i = 0; i < zp.Count; i++)
        {
            if ((zp[i] - Complex.Conjugate(zn[i])).Magnitude > tol * zn[i].Magnitude)
            {
                throw new ArgumentException("Комплексный нуль или полюс без сопряжённой пары.");
            }

            result.Add((zp[i] + Complex.Conjugate(zn[i])) / 2);
        }

        result.AddRange(reals);
        return result;
    }

    private static void SortRun(List<Complex> list, int start, int stop)
    {
        List<Complex> run = list.GetRange(start, stop - start + 1).OrderBy(v => Math.Abs(v.Imaginary)).ToList();
        for (int i = 0; i < run.Count; i++)
        {
            list[start + i] = run[i];
        }
    }
}

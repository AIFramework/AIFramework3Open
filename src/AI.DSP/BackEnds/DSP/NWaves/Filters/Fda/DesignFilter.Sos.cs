using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AI.BackEnds.DSP.NWaves.Filters.Fda
{
    public static partial class DesignFilter
    {
        #region second order sections

        /// <summary>
        /// Second-order sections to zpk.
        /// </summary>
        /// <param name="sos"></param>
        /// <returns></returns>
        public static TransferFunction SosToTf(TransferFunction[] sos)
        {
            return sos.Aggregate((tf, s) => tf * s);
        }

        /// <summary>
        /// Zpk to second-order sections.
        /// </summary>
        /// <param name="tf">Передаточная функция</param>
        /// <returns>Array of SOS Передаточная функцияs</returns>
        public static TransferFunction[] TfToSos(TransferFunction tf)
        {
            List<Complex> zeros = tf.Zeros.ToComplexNumbers().ToList();
            List<Complex> poles = tf.Poles.ToComplexNumbers().ToList();

            if (zeros.Count != poles.Count)
            {
                if (zeros.Count > poles.Count)
                {
                    poles.AddRange(new Complex[zeros.Count - poles.Count]);
                }

                if (zeros.Count < poles.Count)
                {
                    zeros.AddRange(new Complex[poles.Count - zeros.Count]);
                }
            }

            int sosCount = (poles.Count + 1) / 2;

            if (poles.Count % 2 == 1)
            {
                zeros.Add(Complex.Zero);
                poles.Add(Complex.Zero);
            }

            RemoveConjugated(zeros);
            RemoveConjugated(poles);

            double[] gains = new double[sosCount];
            gains[0] = tf.Gain;
            for (int i = 1; i < gains.Length; i++)
            {
                gains[i] = 1;
            }

            TransferFunction[] sos = new TransferFunction[sosCount];

            // reverse order of sections

            for (int i = sosCount - 1; i >= 0; i--)
            {
                Complex z1, z2, p1, p2;

                // Select the next pole closest to unit circle

                int pos = ClosestToUnitCircle(poles, Any);
                p1 = poles[pos];
                poles.RemoveAt(pos);

                if (IsReal(p1) && poles.All(IsComplex))
                {
                    pos = ClosestToComplexValue(zeros, p1, IsReal);     // closest to pole p1
                    z1 = zeros[pos];
                    zeros.RemoveAt(pos);

                    p2 = Complex.Zero;
                    z2 = Complex.Zero;
                }
                else
                {
                    if (IsComplex(p1) && zeros.Count(IsReal) == 1)
                    {
                        pos = ClosestToComplexValue(zeros, p1, IsComplex);
                    }
                    else
                    {
                        pos = ClosestToComplexValue(zeros, p1, Any);
                    }

                    z1 = zeros[pos];
                    zeros.RemoveAt(pos);

                    if (IsComplex(p1))
                    {
                        p2 = Complex.Conjugate(p1);

                        if (IsComplex(z1))
                        {
                            z2 = Complex.Conjugate(z1);
                        }
                        else
                        {
                            pos = ClosestToComplexValue(zeros, p1, IsReal);
                            z2 = zeros[pos];
                            zeros.RemoveAt(pos);
                        }
                    }
                    else
                    {
                        if (IsComplex(z1))
                        {
                            z2 = Complex.Conjugate(z1);

                            pos = ClosestToComplexValue(poles, z1, IsReal);
                            p2 = poles[pos];
                            poles.RemoveAt(pos);
                        }
                        else
                        {
                            pos = ClosestToUnitCircle(poles, IsReal);
                            p2 = poles[pos];
                            poles.RemoveAt(pos);

                            pos = ClosestToComplexValue(zeros, p2, IsReal);
                            z2 = zeros[pos];
                            zeros.RemoveAt(pos);
                        }
                    }
                }

                ComplexDiscreteSignal zs = new ComplexDiscreteSignal(1, new[] { z1.Real, z2.Real }, new[] { z1.Imaginary, z2.Imaginary });
                ComplexDiscreteSignal ps = new ComplexDiscreteSignal(1, new[] { p1.Real, p2.Real }, new[] { p1.Imaginary, p2.Imaginary });

                sos[i] = new TransferFunction(zs, ps, gains[i]);
            }

            return sos;
        }

        private static readonly Func<Complex, bool> Any = c => true;
        private static readonly Func<Complex, bool> IsReal = c => Math.Abs(c.Imaginary) < 1e-10;
        private static readonly Func<Complex, bool> IsComplex = c => Math.Abs(c.Imaginary) > 1e-10;

        private static int ClosestToComplexValue(List<Complex> arr, Complex value, Func<Complex, bool> condition)
        {
            int pos = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < arr.Count; i++)
            {
                if (!condition(arr[i]))
                {
                    continue;
                }

                double distance = Complex.Abs(arr[i] - value);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    pos = i;
                }
            }

            return pos;
        }

        private static int ClosestToUnitCircle(List<Complex> arr, Func<Complex, bool> condition)
        {
            int pos = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < arr.Count; i++)
            {
                if (!condition(arr[i]))
                {
                    continue;
                }

                double distance = Complex.Abs(Complex.Abs(arr[i]) - 1.0);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    pos = i;
                }
            }

            return pos;
        }

        /// <summary>
        /// Leave only one of two conjugated numbers in the list of complex numbers
        /// </summary>
        /// <param name="c"></param>
        private static void RemoveConjugated(List<Complex> c)
        {
            for (int i = 0; i < c.Count; i++)
            {
                if (IsReal(c[i]))
                {
                    continue;
                }

                int j = i + 1;
                for (; j < c.Count; j++)
                {
                    if (Math.Abs(c[i].Real - c[j].Real) < 1e-10 &&
                        Math.Abs(c[i].Imaginary + c[j].Imaginary) < 1e-10)
                    {
                        break;
                    }
                }

                if (j == c.Count)
                {
                    throw new ArgumentException($"Complex array does not contain conjugated pair for {c[i]}");
                }

                c.RemoveAt(j);
            }
        }

        #endregion
    }
}

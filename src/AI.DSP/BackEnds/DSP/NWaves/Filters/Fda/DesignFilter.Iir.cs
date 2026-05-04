using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Signals;
using AI.BackEnds.DSP.NWaves.Utils;
using System;
using System.Linq;
using System.Numerics;

namespace AI.BackEnds.DSP.NWaves.Filters.Fda
{
    public static partial class DesignFilter
    {
        #region design Передаточная функцияs for IIR pole filters (Butterworth, Chebyshev, etc.)


        /// <summary>
        /// Design TF for Фильтр нижних частот pole filter
        /// </summary>
        /// <param name="freq">Cutoff frequency in range [0, 0.5]</param>
        /// <param name="poles">Analog prototype poles</param>
        /// <param name="zeros">Analog prototype zeros</param>
        /// <returns>Передаточная функция</returns>
        public static TransferFunction IirLpTf(double freq, Complex[] poles, Complex[] zeros = null)
        {
            double[] pre = new double[poles.Length];
            double[] pim = new double[poles.Length];

            double warpedFreq = Math.Tan(Math.PI * freq);

            // 1) poles of analog filter (scaled)

            for (int k = 0; k < poles.Length; k++)
            {
                Complex p = warpedFreq * poles[k];
                pre[k] = p.Real;
                pim[k] = p.Imaginary;
            }

            // 2) switch to z-domain

            MathUtilsDSP.BilinearTransform(pre, pim);


            // === if zeros are also specified do the same steps 1-2 with zeros ===

            double[] zre, zim;

            if (zeros != null)
            {
                zre = new double[zeros.Length];
                zim = new double[zeros.Length];

                for (int k = 0; k < zeros.Length; k++)
                {
                    Complex z = warpedFreq * zeros[k];
                    zre[k] = z.Real;
                    zim[k] = z.Imaginary;
                }

                MathUtilsDSP.BilinearTransform(zre, zim);
            }
            // otherwise create zeros (same amount as poles) and set them all to -1
            else
            {
                zre = Enumerable.Repeat(-1.0, poles.Length).ToArray();
                zim = new double[poles.Length];
            }

            // ===



            // 3) return TF with normalized coefficients

            TransferFunction tf = new TransferFunction(new ComplexDiscreteSignal(1, zre, zim),
                                          new ComplexDiscreteSignal(1, pre, pim));
            tf.NormalizeAt(0);

            return tf;
        }

        /// <summary>
        /// Design TF for фильтр нижних частот pole filter
        /// </summary>
        /// <param name="freq">Cutoff frequency in range [0, 0.5]</param>
        /// <param name="poles">Analog prototype poles</param>
        /// <param name="zeros">Analog prototype zeros</param>
        /// <returns>Передаточная функция</returns>
        public static TransferFunction IirHpTf(double freq, Complex[] poles, Complex[] zeros = null)
        {
            double[] pre = new double[poles.Length];
            double[] pim = new double[poles.Length];

            double warpedFreq = Math.Tan(Math.PI * freq);

            // 1) poles of analog filter (scaled)

            for (int k = 0; k < poles.Length; k++)
            {
                Complex p = warpedFreq / poles[k];
                pre[k] = p.Real;
                pim[k] = p.Imaginary;
            }

            // 2) switch to z-domain

            MathUtilsDSP.BilinearTransform(pre, pim);


            // === if zeros are also specified do the same steps 1-2 with zeros ===

            double[] zre, zim;

            if (zeros != null)
            {
                zre = new double[zeros.Length];
                zim = new double[zeros.Length];

                for (int k = 0; k < zeros.Length; k++)
                {
                    Complex z = warpedFreq / zeros[k];
                    zre[k] = z.Real;
                    zim[k] = z.Imaginary;
                }

                MathUtilsDSP.BilinearTransform(zre, zim);
            }
            // otherwise create zeros (same amount as poles) and set them all to -1
            else
            {
                zre = Enumerable.Repeat(1.0, poles.Length).ToArray();
                zim = new double[poles.Length];
            }

            // ===


            // 3) return TF with normalized coefficients

            TransferFunction tf = new TransferFunction(new ComplexDiscreteSignal(1, zre, zim),
                                          new ComplexDiscreteSignal(1, pre, pim));
            tf.NormalizeAt(Math.PI);

            return tf;
        }

        /// <summary>
        /// Design TF for Полосовой pole filter
        /// </summary>
        /// <param name="freq1">Left cutoff frequency in range [0, 0.5]</param>
        /// <param name="freq2">Right cutoff frequency in range [0, 0.5]</param>
        /// <param name="poles">Analog prototype poles</param>
        /// <param name="zeros">Analog prototype zeros</param>
        /// <returns>Передаточная функция</returns>
        public static TransferFunction IirBpTf(double freq1, double freq2, Complex[] poles, Complex[] zeros = null)
        {
            Guard.AgainstInvalidRange(freq1, freq2, "нижняя частота", "верхняя частота");

            double[] pre = new double[poles.Length * 2];
            double[] pim = new double[poles.Length * 2];

            double centerFreq = 2 * Math.PI * (freq1 + freq2) / 2;

            double warpedFreq1 = Math.Tan(Math.PI * freq1);
            double warpedFreq2 = Math.Tan(Math.PI * freq2);

            double f0 = Math.Sqrt(warpedFreq1 * warpedFreq2);
            double bw = warpedFreq2 - warpedFreq1;

            // 1) poles of analog filter (scaled)

            for (int k = 0; k < poles.Length; k++)
            {
                Complex alpha = bw / 2 * poles[k];
                Complex beta = Complex.Sqrt(1 - Complex.Pow(f0 / alpha, 2));

                Complex p1 = alpha * (1 + beta);
                pre[k] = p1.Real;
                pim[k] = p1.Imaginary;

                Complex p2 = alpha * (1 - beta);
                pre[poles.Length + k] = p2.Real;
                pim[poles.Length + k] = p2.Imaginary;
            }

            // 2) switch to z-domain

            MathUtilsDSP.BilinearTransform(pre, pim);


            // === if zeros are also specified do the same steps 1-2 with zeros ===

            double[] zre, zim;

            if (zeros != null)
            {
                zre = new double[zeros.Length * 2];
                zim = new double[zeros.Length * 2];

                for (int k = 0; k < zeros.Length; k++)
                {
                    Complex alpha = bw / 2 * zeros[k];
                    Complex beta = Complex.Sqrt(1 - Complex.Pow(f0 / alpha, 2));

                    Complex z1 = alpha * (1 + beta);
                    zre[k] = z1.Real;
                    zim[k] = z1.Imaginary;

                    Complex z2 = alpha * (1 - beta);
                    zre[zeros.Length + k] = z2.Real;
                    zim[zeros.Length + k] = z2.Imaginary;
                }

                MathUtilsDSP.BilinearTransform(zre, zim);
            }
            // otherwise create zeros (same amount as poles) and set them all to [-1, -1, -1, ..., 1, 1, 1]
            else
            {
                zre = Enumerable.Repeat(-1.0, poles.Length)
                                .Concat(Enumerable.Repeat(1.0, poles.Length))
                                .ToArray();
                zim = new double[poles.Length * 2];
            }

            // ===


            // 3) return TF with normalized coefficients

            TransferFunction tf = new TransferFunction(new ComplexDiscreteSignal(1, zre, zim),
                                          new ComplexDiscreteSignal(1, pre, pim));
            tf.NormalizeAt(centerFreq);

            return tf;
        }

        /// <summary>
        /// Design TF for band-reject pole filter
        /// </summary>
        /// <param name="freq1">Left cutoff frequency in range [0, 0.5]</param>
        /// <param name="freq2">Right cutoff frequency in range [0, 0.5]</param>
        /// <param name="poles">Analog prototype poles</param>
        /// <param name="zeros">Analog prototype zeros</param>
        /// <returns>Передаточная функция</returns>
        public static TransferFunction IirBsTf(double freq1, double freq2, Complex[] poles, Complex[] zeros = null)
        {
            Guard.AgainstInvalidRange(freq1, freq2, "нижняя частота", "верхняя частота");

            // Calculation of filter coefficients is based on Neil Robertson's post:
            // https://www.dsprelated.com/showarticle/1131.php

            double[] pre = new double[poles.Length * 2];
            double[] pim = new double[poles.Length * 2];

            double f1 = Math.Tan(Math.PI * freq1);
            double f2 = Math.Tan(Math.PI * freq2);
            double f0 = Math.Sqrt(f1 * f2);
            double bw = f2 - f1;

            double centerFreq = 2 * Math.Atan(f0);


            // 1) poles and zeros of analog filter (scaled)

            for (int k = 0; k < poles.Length; k++)
            {
                Complex alpha = bw / 2 / poles[k];
                Complex beta = Complex.Sqrt(1 - Complex.Pow(f0 / alpha, 2));

                Complex p1 = alpha * (1 + beta);
                pre[k] = p1.Real;
                pim[k] = p1.Imaginary;

                Complex p2 = alpha * (1 - beta);
                pre[poles.Length + k] = p2.Real;
                pim[poles.Length + k] = p2.Imaginary;
            }

            // 2) switch to z-domain

            MathUtilsDSP.BilinearTransform(pre, pim);


            // === if zeros are also specified do the same steps 1-2 with zeros ===

            double[] zre, zim;

            if (zeros != null)
            {
                zre = new double[zeros.Length * 2];
                zim = new double[zeros.Length * 2];

                for (int k = 0; k < zeros.Length; k++)
                {
                    Complex alpha = bw / 2 / zeros[k];
                    Complex beta = Complex.Sqrt(1 - Complex.Pow(f0 / alpha, 2));

                    Complex z1 = alpha * (1 + beta);
                    zre[k] = z1.Real;
                    zim[k] = z1.Imaginary;

                    Complex z2 = alpha * (1 - beta);
                    zre[zeros.Length + k] = z2.Real;
                    zim[zeros.Length + k] = z2.Imaginary;
                }

                MathUtilsDSP.BilinearTransform(zre, zim);
            }
            // otherwise create zeros (same amount as poles) and set the following values:
            else
            {
                zre = new double[poles.Length * 2];
                zim = new double[poles.Length * 2];

                for (int k = 0; k < poles.Length; k++)
                {
                    zre[k] = Math.Cos(centerFreq);
                    zim[k] = Math.Sin(centerFreq);
                    zre[poles.Length + k] = Math.Cos(-centerFreq);
                    zim[poles.Length + k] = Math.Sin(-centerFreq);
                }
            }

            // ===


            // 3) return TF with normalized coefficients

            TransferFunction tf = new TransferFunction(new ComplexDiscreteSignal(1, zre, zim),
                                          new ComplexDiscreteSignal(1, pre, pim));
            tf.NormalizeAt(0);

            return tf;
        }

        #endregion
    }
}

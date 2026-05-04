using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Signals;
using AI.BackEnds.DSP.NWaves.Transforms;
using AI.BackEnds.DSP.NWaves.Utils;
using AI.BackEnds.DSP.NWaves.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AI.BackEnds.DSP.NWaves.Filters.Fda
{
    /// <summary>
    /// Статический класс, предоставляющий основные методы для анализа и проектирования фильтров.
    /// </summary>
    [Serializable]

    public static partial class DesignFilter
    {
        /// <summary>
        /// Метод создания идеального КИХ-фильтра нижних частот с использованием метода весового окна с функцией sin(x)/x
        /// </summary>
        /// <param name="order"></param>
        /// <param name="freq"></param>
        /// <param name="window"></param>
        /// <returns></returns>
        public static double[] FirWinLp(int order, double freq, WindowTypes window = WindowTypes.Blackman)
        {
            Guard.AgainstEvenNumber(order, "Порядок фильтра");

            double[] kernel = new double[order];

            int middle = order / 2;
            double freq2Pi = 2 * Math.PI * freq;

            kernel[middle] = 2 * freq;
            for (int i = 1; i <= middle; i++)
            {
                kernel[middle - i] =
                kernel[middle + i] = Math.Sin(freq2Pi * i) / (Math.PI * i);
            }

            kernel.ApplyWindow(window);

            return kernel;
        }

        /// <summary>
        /// Метод создания идеального КИХ-фильтра верхних частот с использованием метода весового окна с функцией sin(x)/x
        /// </summary>
        /// <param name="order"></param>
        /// <param name="freq"></param>
        /// <param name="window"></param>
        /// <returns></returns>
        public static double[] FirWinHp(int order, double freq, WindowTypes window = WindowTypes.Blackman)
        {
            Guard.AgainstEvenNumber(order, "Порядок фильтра");

            double[] kernel = new double[order];

            int middle = order / 2;
            double freq2Pi = 2 * Math.PI * freq;

            kernel[middle] = 2 * (0.5 - freq);
            for (int i = 1; i <= middle; i++)
            {
                kernel[middle - i] =
                kernel[middle + i] = -Math.Sin(freq2Pi * i) / (Math.PI * i);
            }

            kernel.ApplyWindow(window);

            return kernel;
        }

        /// <summary>
        /// Метод создания идеального полосового КИХ-фильтра с использованием метода весового окна с функцией sin(x)/x
        /// </summary>
        /// <param name="order"></param>
        /// <param name="freq1"></param>
        /// <param name="freq2"></param>
        /// <param name="window"></param>
        /// <returns></returns>
        public static double[] FirWinBp(int order, double freq1, double freq2, WindowTypes window = WindowTypes.Blackman)
        {
            Guard.AgainstEvenNumber(order, "Порядок фильтра");
            Guard.AgainstInvalidRange(freq1, freq2, "нижняя частота", "верхняя частота");

            double[] kernel = new double[order];

            int middle = order / 2;
            double freq12Pi = 2 * Math.PI * freq1;
            double freq22Pi = 2 * Math.PI * freq2;

            kernel[middle] = 2 * (freq2 - freq1);
            for (int i = 1; i <= middle; i++)
            {
                kernel[middle - i] =
                kernel[middle + i] = (Math.Sin(freq22Pi * i) - Math.Sin(freq12Pi * i)) / (Math.PI * i);
            }

            kernel.ApplyWindow(window);

            return kernel;
        }

        /// <summary>
        /// Метод создания идеального режекторного КИХ-фильтра с использованием метода весового окна с функцией sin(x)/x
        /// </summary>
        /// <param name="order"></param>
        /// <param name="freq1"></param>
        /// <param name="freq2"></param>
        /// <param name="window"></param>
        /// <returns></returns>
        public static double[] FirWinBs(int order, double freq1, double freq2, WindowTypes window = WindowTypes.Blackman)
        {
            Guard.AgainstEvenNumber(order, "Порядок фильтра");
            Guard.AgainstInvalidRange(freq1, freq2, "нижняя частота", "верхняя частота");

            double[] kernel = new double[order];

            int middle = order / 2;
            double freq12Pi = 2 * Math.PI * freq1;
            double freq22Pi = 2 * Math.PI * freq2;

            kernel[middle] = 2 * (0.5 - freq2 + freq1);
            for (int i = 1; i <= middle; i++)
            {
                kernel[middle - i] =
                kernel[middle + i] = (Math.Sin(freq12Pi * i) - Math.Sin(freq22Pi * i)) / (Math.PI * i);
            }

            kernel.ApplyWindow(window);

            return kernel;
        }

        /// <summary>
        /// Design equiripple LP FIR filter using Remez (Parks-McClellan) algorithm
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="fp">Passband edge frequency</param>
        /// <param name="fa">Stopband edge frequency</param>
        /// <param name="wp">Passband weight</param>
        /// <param name="wa">Stopband weight</param>
        /// <returns>Filter kernel</returns>
        public static double[] FirEquirippleLp(int order, double fp, double fa, double wp, double wa)
        {
            return new Remez(order, new[] { 0, fp, fa, 0.5 }, new[] { 1, 0.0 }, new[] { wp, wa }).Design();
        }

        /// <summary>
        /// Design equiripple HP FIR filter using Remez (Parks-McClellan) algorithm
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="fa">Stopband edge frequency</param>
        /// <param name="fp">Passband edge frequency</param>
        /// <param name="wa">Stopband weight</param>
        /// <param name="wp">Passband weight</param>
        /// <returns>Filter kernel</returns>
        public static double[] FirEquirippleHp(int order, double fa, double fp, double wa, double wp)
        {
            return new Remez(order, new[] { 0, fa, fp, 0.5 }, new[] { 0, 1.0 }, new[] { wa, wp }).Design();
        }

        /// <summary>
        /// Design equiripple BP FIR filter using Remez (Parks-McClellan) algorithm
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="fa1">Left stopband edge frequency</param>
        /// <param name="fp1">Passband left edge frequency</param>
        /// <param name="fp2">Passband right edge frequency</param>
        /// <param name="fa2">Right stopband edge frequency</param>
        /// <param name="wa1">Left stopband weight</param>
        /// <param name="wp">Passband weight</param>
        /// <param name="wa2">Right stopband weight</param>
        /// <returns>Filter kernel</returns>
        public static double[] FirEquirippleBp(int order, double fa1, double fp1, double fp2, double fa2, double wa1, double wp, double wa2)
        {
            return new Remez(order, new[] { 0, fa1, fp1, fp2, fa2, 0.5 }, new[] { 0, 1.0, 0 }, new[] { wa1, wp, wa2 }).Design();
        }

        /// <summary>
        /// Design equiripple BS FIR filter using Remez (Parks-McClellan) algorithm
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="fp1">Left passband edge frequency</param>
        /// <param name="fa1">Stopband left edge frequency</param>
        /// <param name="fa2">Stopband right edge frequency</param>
        /// <param name="fp2">Right passband edge frequency</param>
        /// <param name="wp1">Left passband weight</param>
        /// <param name="wa">Stopband weight</param>
        /// <param name="wp2">Right passband weight</param>
        /// <returns>Filter kernel</returns>
        public static double[] FirEquirippleBs(int order, double fp1, double fa1, double fa2, double fp2, double wp1, double wa, double wp2)
        {
            return new Remez(order, new[] { 0, fp1, fa1, fa2, fp2, 0.5 }, new[] { 1, 0.0, 1 }, new[] { wp1, wa, wp2 }).Design();
        }

        /// <summary>
        /// FIR filter design using frequency sampling method
        /// </summary>
        /// <param name="order">Filter order</param>
        /// <param name="magnitudeResponse">Magnitude response</param>
        /// <param name="phaseResponse">Phase response</param>
        /// <param name="window">Окно</param>
        /// <returns>FIR filter kernel</returns>
        public static double[] Fir(int order,
                                   double[] magnitudeResponse,
                                   double[] phaseResponse = null,
                                   WindowTypes window = WindowTypes.Blackman)
        {
            Guard.AgainstEvenNumber(order, "Порядок фильтра");

            int fftSize = MathUtilsDSP.NextPowerOfTwo(magnitudeResponse.Length);

            double[] real = phaseResponse == null ?
                       magnitudeResponse.PadZeros(fftSize) :
                       magnitudeResponse.Zip(phaseResponse, (m, p) => m * Math.Cos(p)).ToArray();

            double[] imag = phaseResponse == null ?
                       new double[fftSize] :
                       magnitudeResponse.Zip(phaseResponse, (m, p) => m * Math.Sin(p)).ToArray();

            Fft64 fft = new Fft64(fftSize);
            fft.Inverse(real, imag);

            double[] kernel = new double[order];

            double compensation = 2.0 / fftSize;
            int middle = order / 2;
            for (int i = 0; i <= middle; i++)
            {
                kernel[i] = real[middle - i] * compensation;
                kernel[i + middle] = real[i] * compensation;
            }

            kernel.ApplyWindow(window);

            return kernel;
        }

        /// <summary>
        /// FIR filter design using frequency sampling method
        /// </summary>
        /// <param name="order">Filter order</param>
        /// <param name="frequencyResponse">Complex frequency response</param>
        /// <param name="window">Окно</param>
        /// <returns>FIR filter kernel</returns>
        public static double[] Fir(int order, ComplexDiscreteSignal frequencyResponse, WindowTypes window = WindowTypes.Blackman)
        {
            return Fir(order, frequencyResponse.Real, frequencyResponse.Imag, window);
        }

        /// <summary>
        /// FIR filter design using frequency sampling method (32-bit precision)
        /// </summary>
        /// <param name="order">Filter order</param>
        /// <param name="magnitudeResponse">Magnitude response</param>
        /// <param name="phaseResponse">Phase response</param>
        /// <param name="window">Окно</param>
        /// <returns>FIR filter kernel</returns>
        public static double[] Fir(int order,
                                   float[] magnitudeResponse,
                                   float[] phaseResponse = null,
                                   WindowTypes window = WindowTypes.Blackman)
        {
            Guard.AgainstEvenNumber(order, "Порядок фильтра");

            int fftSize = MathUtilsDSP.NextPowerOfTwo(magnitudeResponse.Length);

            float[] real = phaseResponse == null ?
                       magnitudeResponse.PadZeros(fftSize) :
                       magnitudeResponse.Zip(phaseResponse, (m, p) => (float)(m * Math.Cos(p))).ToArray();

            float[] imag = phaseResponse == null ?
                       new float[fftSize] :
                       magnitudeResponse.Zip(phaseResponse, (m, p) => (float)(m * Math.Sin(p))).ToArray();

            Fft fft = new Fft(fftSize);
            fft.Inverse(real, imag);

            double[] kernel = new double[order];

            double compensation = 2.0 / fftSize;
            int middle = order / 2;
            for (int i = 0; i <= middle; i++)
            {
                kernel[i] = real[middle - i] * compensation;
                kernel[i + middle] = real[i] * compensation;
            }

            kernel.ApplyWindow(window);

            return kernel;
        }


        #region Convert LowPass FIR filter kernel between band forms

        /// <summary>
        /// Method for making HP filter from the linear-phase LP filter
        /// </summary>
        /// <param name="kernel"></param>
        /// <returns></returns>
        public static double[] FirLpToHp(double[] kernel)
        {
            Guard.AgainstEvenNumber(kernel.Length, "Порядок фильтра");

            double[] kernelHp = kernel.Select(k => -k).ToArray();
            kernelHp[kernelHp.Length / 2] += 1.0;
            return kernelHp;
        }

        /// <summary>
        /// Method for making LP filter from the linear-phase HP filter
        /// (not different from FirLpToHp method)
        /// </summary>
        /// <param name="kernel"></param>
        /// <returns></returns>
        public static double[] FirHpToLp(double[] kernel)
        {
            return FirLpToHp(kernel);
        }

        /// <summary>
        /// Method for making BS filter from the linear-phase BP filter
        /// (not different from FirLpToHp method)
        /// </summary>
        /// <param name="kernel"></param>
        /// <returns></returns>
        public static double[] FirBpToBs(double[] kernel)
        {
            return FirLpToHp(kernel);
        }

        /// <summary>
        /// Method for making BP filter from the linear-phase BS filter
        /// (not different from FirLpToHp method)
        /// </summary>
        /// <param name="kernel"></param>
        /// <returns></returns>
        public static double[] FirBsToBp(double[] kernel)
        {
            return FirLpToHp(kernel);
        }

        #endregion
    }
}

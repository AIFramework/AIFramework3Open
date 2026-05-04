using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Signals;
using AI.BackEnds.DSP.NWaves.Transforms;
using AI.BackEnds.DSP.NWaves.Utils;
using System;
using System.Linq;
using System.Numerics;

namespace AI.BackEnds.DSP.NWaves.Filters.Fda
{
    public static partial class FilterBanks
    {
        /// <summary>
        /// Method creates overlapping triangular mel filters (as suggested by Malcolm Slaney).
        /// </summary>
        /// <param name="filterCount">Number of mel filters</param>
        /// <param name="fftSize">Assumed Размер блока БПФ</param>
        /// <param name="samplingRate">Assumed Частота дискретизации</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="normalizeGain">True if gain should be normalized; false if all filters should have same height 1.0</param>
        /// <param name="vtln">VTLN frequency warper</param>
        /// <returns>Array of mel filters</returns>
        public static float[][] MelBankSlaney(
            int filterCount, int fftSize, int samplingRate, double lowFreq = 0, double highFreq = 0, bool normalizeGain = true, VtlnWarper vtln = null)
        {
            if (lowFreq < 0)
            {
                lowFreq = 0;
            }
            if (highFreq <= lowFreq)
            {
                highFreq = samplingRate / 2.0;
            }

            Tuple<double, double, double>[] frequencies = UniformBands(Scale.HerzToMelSlaney, Scale.MelToHerzSlaney, filterCount, samplingRate, lowFreq, highFreq, true);

            float[][] filterBank = Triangular(fftSize, samplingRate, frequencies, vtln);

            if (normalizeGain)
            {
                Normalize(filterCount, frequencies, filterBank);
            }

            return filterBank;
        }

        /// <summary>
        /// Method creates overlapping trapezoidal bark filters (as suggested by Malcolm Slaney).
        /// </summary>
        /// <param name="filterCount"></param>
        /// <param name="fftSize"></param>
        /// <param name="samplingRate"></param>
        /// <param name="lowFreq"></param>
        /// <param name="highFreq"></param>
        /// <param name="width">Constant width of each band in Bark</param>
        /// <returns></returns>
        public static float[][] BarkBankSlaney(
            int filterCount, int fftSize, int samplingRate, double lowFreq = 0, double highFreq = 0, double width = 1)
        {
            if (lowFreq < 0)
            {
                lowFreq = 0;
            }
            if (highFreq <= lowFreq)
            {
                highFreq = samplingRate / 2.0;
            }

            double lowBark = Scale.HerzToBarkSlaney(lowFreq);
            double highBark = Scale.HerzToBarkSlaney(highFreq) - lowBark;

            double herzResolution = (double)samplingRate / fftSize;
            double step = highBark / (filterCount - 1);

            double[] binBarks = Enumerable.Range(0, (fftSize / 2) + 1)
                                     .Select(i => Scale.HerzToBarkSlaney(i * herzResolution))
                                     .ToArray();

            float[][] filterBank = new float[filterCount][];

            double midBark = lowBark;

            for (int i = 0; i < filterCount; i++, midBark += step)
            {
                filterBank[i] = new float[(fftSize / 2) + 1];

                for (int j = 0; j < filterBank[i].Length; j++)
                {
                    double lof = binBarks[j] - midBark - 0.5;
                    double hif = binBarks[j] - midBark + 0.5;

                    filterBank[i][j] = (float)Math.Pow(10, Math.Min(0, Math.Min(hif, -2.5 * lof) / width));
                }
            }

            return filterBank;
        }

        /// <summary>
        /// Method creates overlapping ERB filters (ported from Malcolm Slaney's MATLAB code).
        /// </summary>
        /// <param name="erbFilterCount">Number of ERB filters</param>
        /// <param name="fftSize">Assumed Размер блока БПФ</param>
        /// <param name="samplingRate">Assumed Частота дискретизации</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="normalizeGain">True if gain should be normalized; false if all filters should have same height 1.0</param>
        /// <returns>Array of ERB filters</returns>
        public static float[][] Erb(
            int erbFilterCount, int fftSize, int samplingRate, double lowFreq = 0, double highFreq = 0, bool normalizeGain = true)
        {
            if (lowFreq < 0)
            {
                lowFreq = 0;
            }
            if (highFreq <= lowFreq)
            {
                highFreq = samplingRate / 2.0;
            }

            const double earQ = 9.26449;
            const double minBw = 24.7;
            const double bw = earQ * minBw;
            const int order = 1;

            double t = 1.0 / samplingRate;

            double[] frequencies = new double[erbFilterCount];
            for (int i = 1; i <= erbFilterCount; i++)
            {
                frequencies[erbFilterCount - i] =
                    -bw + (Math.Exp(i * (-Math.Log(highFreq + bw) + Math.Log(lowFreq + bw)) / erbFilterCount) * (highFreq + bw));
            }

            Complex[] ucirc = new Complex[(fftSize / 2) + 1];
            for (int i = 0; i < ucirc.Length; i++)
            {
                ucirc[i] = Complex.Exp(2 * Complex.ImaginaryOne * i * Math.PI / fftSize);
            }

            double rootPos = Math.Sqrt(3 + Math.Pow(2, 1.5));
            double rootNeg = Math.Sqrt(3 - Math.Pow(2, 1.5));

            Fft fft = new Fft(fftSize);

            float[][] erbFilterBank = new float[erbFilterCount][];

            for (int i = 0; i < erbFilterCount; i++)
            {
                double cf = frequencies[i];
                double erb = Math.Pow(Math.Pow(cf / earQ, order) + Math.Pow(minBw, order), 1.0 / order);
                double b = 1.019 * 2 * Math.PI * erb;

                double theta = 2 * cf * Math.PI * t;
                Complex itheta = Complex.Exp(2 * Complex.ImaginaryOne * theta);

                double a0 = t;
                double a2 = 0.0;
                double b0 = 1.0;
                double b1 = -2 * Math.Cos(theta) / Math.Exp(b * t);
                double b2 = Math.Exp(-2 * b * t);

                double common = -t * Math.Exp(-b * t);

                double k1 = Math.Cos(theta) + (rootPos * Math.Sin(theta));
                double k2 = Math.Cos(theta) - (rootPos * Math.Sin(theta));
                double k3 = Math.Cos(theta) + (rootNeg * Math.Sin(theta));
                double k4 = Math.Cos(theta) - (rootNeg * Math.Sin(theta));

                double a11 = common * k1;
                double a12 = common * k2;
                double a13 = common * k3;
                double a14 = common * k4;

                Complex gainArg = Complex.Exp((Complex.ImaginaryOne * theta) - (b * t));

                float gain = (float)Complex.Abs(
                                    (itheta - (gainArg * k1)) *
                                    (itheta - (gainArg * k2)) *
                                    (itheta - (gainArg * k3)) *
                                    (itheta - (gainArg * k4)) *
                                    Complex.Pow(t * Math.Exp(b * t) / ((-1.0 / Math.Exp(b * t)) + 1 + (itheta * (1 - Math.Exp(b * t)))), 4.0));

                IirFilter filter1 = new IirFilter(new[] { a0, a11, a2 }, new[] { b0, b1, b2 });
                IirFilter filter2 = new IirFilter(new[] { a0, a12, a2 }, new[] { b0, b1, b2 });
                IirFilter filter3 = new IirFilter(new[] { a0, a13, a2 }, new[] { b0, b1, b2 });
                IirFilter filter4 = new IirFilter(new[] { a0, a14, a2 }, new[] { b0, b1, b2 });

                DiscreteSignal ir = new DiscreteSignal(1, fftSize);
                ir[0] = 1.0f;

                FilterChain chain = new FilterChain(new[] { filter1, filter2, filter3, filter4 });

                DiscreteSignal kernel = chain.ApplyTo(ir);
                kernel.Attenuate(gain);

                erbFilterBank[i] = fft.PowerSpectrum(kernel, false).Samples;
            }

            // normalize gain (by default)

            if (!normalizeGain)
            {
                return erbFilterBank;
            }

            foreach (float[] filter in erbFilterBank)
            {
                double sum = 0.0;
                for (int j = 0; j < filter.Length; j++)
                {
                    sum += Math.Abs(filter[j] * filter[j]);
                }

                double weight = Math.Sqrt(sum * samplingRate / fftSize);

                for (int j = 0; j < filter.Length; j++)
                {
                    filter[j] = (float)(filter[j] / weight);
                }
            }

            return erbFilterBank;
        }

        /// <summary>
        /// Normalize weights (so that energies in each band are approx. equal)
        /// </summary>
        /// <param name="filterCount"></param>
        /// <param name="frequencies"></param>
        /// <param name="filterBank"></param>
        public static void Normalize(int filterCount, Tuple<double, double, double>[] frequencies, float[][] filterBank)
        {
            for (int i = 0; i < filterCount; i++)
            {
                Tuple<double, double, double> tuple = frequencies[i];

                double left = tuple.Item1, right = tuple.Item2;

                for (int j = 0; j < filterBank[i].Length; j++)
                {
                    filterBank[i][j] *= 2 / (float)(right - left);
                }
            }
        }
    }
}

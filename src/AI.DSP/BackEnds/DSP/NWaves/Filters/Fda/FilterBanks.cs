using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Filters.BiQuad;
using AI.BackEnds.DSP.NWaves.Signals;
using AI.BackEnds.DSP.NWaves.Transforms;
using AI.BackEnds.DSP.NWaves.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AI.BackEnds.DSP.NWaves.Filters.Fda
{
    /// <summary>
    /// Static class with methods providing general shapes of filter banks:
    /// 
    ///     - triangular
    ///     - rectangular
    ///     - FIR bandpass (close to trapezoidal, slightly overlapping)
    ///     - BiQuad bandpass
    /// 
    /// ...and methods for obtaining the most widely used frequency bands:
    /// 
    ///     - Herz bands
    ///     - Mel bands (HTK and Slaney)
    ///     - Bark bands (uniform and Slaney)
    ///     - Critical bands
    ///     - ERB filterbank
    ///     - Octaves (from MPEG-7)
    /// 
    /// </summary>
    [Serializable]

    public static partial class FilterBanks
    {
        /// <summary>
        /// Method returns universal triangular filterbank weights based on given frequencies.
        /// </summary>
        /// <param name="fftSize">Assumed Размер блока БПФ</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="frequencies">Array of frequency tuples (left, center, right) for each filter</param>
        /// <param name="vtln">VTLN frequency warper</param>
        /// <param name="mapper">Frequency scale mapper (e.g. herz-to-mel) used here only for proper weighting</param>
        /// <returns>Array of triangular filters</returns>
        public static float[][] Triangular(int fftSize,
                                           int samplingRate,
                                           Tuple<double, double, double>[] frequencies,
                                           VtlnWarper vtln = null,
                                           Func<double, double> mapper = null)
        {
            if (mapper == null)
            {
                mapper = x => x;
            }

            Func<double, double> warp = vtln == null ? mapper : x => mapper(vtln.Warp(x));

            double herzResolution = (double)samplingRate / fftSize;

            double[] herzFrequencies = Enumerable.Range(0, (fftSize / 2) + 1)
                                            .Select(f => f * herzResolution)
                                            .ToArray();

            int filterCount = frequencies.Length;
            float[][] filterBank = new float[filterCount][];

            for (int i = 0; i < filterCount; i++)
            {
                filterBank[i] = new float[(fftSize / 2) + 1];

                Tuple<double, double, double> tuple = frequencies[i];

                double left = tuple.Item1, center = tuple.Item2, right = tuple.Item3;

                left = warp(left);
                center = warp(center);
                right = warp(right);

                int j = 0;
                for (; mapper(herzFrequencies[j]) <= left; j++)
                {
                    ;
                }

                for (; mapper(herzFrequencies[j]) <= center; j++)
                {
                    filterBank[i][j] = (float)((mapper(herzFrequencies[j]) - left) / (center - left));
                }
                for (; j < herzFrequencies.Length && mapper(herzFrequencies[j]) < right; j++)
                {
                    filterBank[i][j] = (float)((right - mapper(herzFrequencies[j])) / (right - center));
                }
            }

            return filterBank;
        }

        /// <summary>
        /// Method returns universal rectangular filterbank weights based on given frequencies.
        /// </summary>
        /// <param name="fftSize">Assumed Размер блока БПФ</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="frequencies">Array of frequency tuples (left, center, right) for each filter</param>
        /// <param name="vtln">VTLN frequency warper</param>
        /// <param name="mapper">Frequency scale mapper (e.g. herz-to-mel)</param>
        /// <returns>Array of rectangular filters</returns>
        public static float[][] Rectangular(int fftSize,
                                           int samplingRate,
                                           Tuple<double, double, double>[] frequencies,
                                           VtlnWarper vtln = null,
                                           Func<double, double> mapper = null)
        {
            if (mapper == null)
            {
                mapper = x => x;
            }

            Func<double, double> warp = vtln == null ? mapper : x => mapper(vtln.Warp(x));

            double herzResolution = (double)samplingRate / fftSize;

            double[] herzFrequencies = Enumerable.Range(0, (fftSize / 2) + 1)
                                            .Select(f => f * herzResolution)
                                            .ToArray();

            int filterCount = frequencies.Length;
            float[][] filterBank = new float[filterCount][];

            for (int i = 0; i < filterCount; i++)
            {
                filterBank[i] = new float[(fftSize / 2) + 1];

                Tuple<double, double, double> tuple = frequencies[i];

                double left = tuple.Item1, center = tuple.Item2, right = tuple.Item3;


                left = warp(left);
                center = warp(center);
                right = warp(right);

                int j = 0;
                for (; mapper(herzFrequencies[j]) <= left; j++)
                {
                    ;
                }

                for (; j < herzFrequencies.Length && mapper(herzFrequencies[j]) < right; j++)
                {
                    filterBank[i][j] = 1;
                }
            }

            return filterBank;
        }

        /// <summary>
        /// Method returns FIR bandpass (close to trapezoidal) filterbank based on given frequencies.
        /// </summary>
        /// <param name="fftSize">Assumed Размер блока БПФ</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="frequencies">Array of frequency tuples (left, center, right) for each filter</param>
        /// <param name="vtln">VTLN frequency warper</param>
        /// <param name="mapper">Frequency scale mapper (e.g. herz-to-mel)</param>
        /// <returns>Array of rectangular filters</returns>
        public static float[][] Trapezoidal(int fftSize,
                                           int samplingRate,
                                           Tuple<double, double, double>[] frequencies,
                                           VtlnWarper vtln = null,
                                           Func<double, double> mapper = null)
        {
            float[][] filterBank = Rectangular(fftSize, samplingRate, frequencies, vtln, mapper);

            for (int i = 0; i < filterBank.Length; i++)
            {
                TransferFunction filterTf = new TransferFunction(DesignFilter.Fir((fftSize / 4) + 1, filterBank[i]));

                filterBank[i] = filterTf.FrequencyResponse(fftSize).Magnitude.ToFloats();

                // normalize gain to 1.0

                float maxAmp = 0.0f;
                for (int j = 0; j < filterBank[i].Length; j++)
                {
                    if (filterBank[i][j] > maxAmp)
                    {
                        maxAmp = filterBank[i][j];
                    }
                }
                for (int j = 0; j < filterBank[i].Length; j++)
                {
                    filterBank[i][j] /= maxAmp;
                }
            }

            return filterBank;
        }

        /// <summary>
        /// Method returns BiQuad bandpass overlapping filters based on given frequencies.
        /// </summary>
        /// <param name="fftSize">Assumed Размер блока БПФ</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="frequencies">Array of frequency tuples (left, center, right) for each filter</param>
        /// <returns>Array of BiQuad bandpass filters</returns>
        public static float[][] BiQuad(int fftSize, int samplingRate, Tuple<double, double, double>[] frequencies)
        {
            double[] center = frequencies.Select(f => f.Item2).ToArray();

            int filterCount = frequencies.Length;
            float[][] filterBank = new float[filterCount][];

            for (int i = 0; i < filterCount; i++)
            {
                double freq = center[i] / samplingRate;
                BandPassFilter filter = new BandPassFilter(freq, 2.0);

                filterBank[i] = filter.Tf.FrequencyResponse(fftSize).Magnitude.ToFloats();
            }

            return filterBank;
        }

        /// <summary>
        /// This general method returns frequency tuples for uniformly spaced frequency bands on any scale.
        /// </summary>
        /// <param name="scaleMapper">The function that converts Hz to other frequency scale</param>
        /// <param name="inverseMapper">The function that converts frequency from alternative scale back to Hz</param>
        /// <param name="filterCount">Число фильтров</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each filter</returns>
        private static Tuple<double, double, double>[] UniformBands(
                                                     Func<double, double> scaleMapper,
                                                     Func<double, double> inverseMapper,
                                                     int filterCount,
                                                     int samplingRate,
                                                     double lowFreq = 0,
                                                     double highFreq = 0,
                                                     bool overlap = true)
        {
            if (lowFreq < 0)
            {
                lowFreq = 0;
            }
            if (highFreq <= lowFreq)
            {
                highFreq = samplingRate / 2.0;
            }

            double startingFrequency = scaleMapper(lowFreq);

            Tuple<double, double, double>[] frequencyTuples = new Tuple<double, double, double>[filterCount];

            if (overlap)
            {
                double newResolution = (scaleMapper(highFreq) - scaleMapper(lowFreq)) / (filterCount + 1);

                double[] frequencies = Enumerable.Range(0, filterCount + 2)
                                            .Select(i => inverseMapper(startingFrequency + (i * newResolution)))
                                            .ToArray();

                for (int i = 0; i < filterCount; i++)
                {
                    frequencyTuples[i] = new Tuple<double, double, double>(frequencies[i], frequencies[i + 1], frequencies[i + 2]);
                }
            }
            else
            {
                double newResolution = (scaleMapper(highFreq) - scaleMapper(lowFreq)) / filterCount;

                double[] frequencies = Enumerable.Range(0, filterCount + 1)
                                            .Select(i => inverseMapper(startingFrequency + (i * newResolution)))
                                            .ToArray();

                for (int i = 0; i < filterCount; i++)
                {
                    frequencyTuples[i] = new Tuple<double, double, double>(frequencies[i], frequencies[i + 1], frequencies[i + 2]);
                }
            }

            return frequencyTuples;
        }

        /// <summary>
        /// Method returns frequency tuples for uniformly spaced frequency bands on Herz scale.
        /// </summary>
        /// <param name="combFilterCount">Число фильтров</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each Herz filter</returns>
        public static Tuple<double, double, double>[] HerzBands(
            int combFilterCount, int samplingRate, double lowFreq = 0, double highFreq = 0, bool overlap = false)
        {
            // "x => x" means map frequency 1-to-1 (in Hz as it is)
            return UniformBands(x => x, x => x, combFilterCount, samplingRate, lowFreq, highFreq, overlap);
        }

        /// <summary>
        /// Method returns frequency tuples for uniformly spaced frequency bands on Mel scale.
        /// </summary>
        /// <param name="melFilterCount">Number of mel filters to create</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each Mel filter</returns>
        public static Tuple<double, double, double>[] MelBands(
            int melFilterCount, int samplingRate, double lowFreq = 0, double highFreq = 0, bool overlap = true)
        {
            return UniformBands(Scale.HerzToMel, Scale.MelToHerz, melFilterCount, samplingRate, lowFreq, highFreq, overlap);
        }

        /// <summary>
        /// Method returns frequency tuples for uniformly spaced frequency bands on Mel scale
        /// (according to M.Slaney's formula).
        /// </summary>
        /// <param name="melFilterCount">Number of mel filters to create</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each Mel filter</returns>
        public static Tuple<double, double, double>[] MelBandsSlaney(
            int melFilterCount, int samplingRate, double lowFreq = 0, double highFreq = 0, bool overlap = true)
        {
            return UniformBands(Scale.HerzToMelSlaney, Scale.MelToHerzSlaney, melFilterCount, samplingRate, lowFreq, highFreq, overlap);
        }

        /// <summary>
        /// Method returns frequency tuples for uniformly spaced frequency bands on Bark scale (Traunmueller, 1990).
        /// </summary>
        /// <param name="barkFilterCount">Number of bark filters to create</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each Bark filter</returns>
        public static Tuple<double, double, double>[] BarkBands(
            int barkFilterCount, int samplingRate, double lowFreq = 0, double highFreq = 0, bool overlap = true)
        {
            return UniformBands(Scale.HerzToBark, Scale.BarkToHerz, barkFilterCount, samplingRate, lowFreq, highFreq, overlap);
        }

        /// <summary>
        /// Method returns frequency tuples for uniformly spaced frequency bands on Bark scale (Wang, 1992).
        /// </summary>
        /// <param name="barkFilterCount">Number of bark filters to create</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each Bark filter</returns>
        public static Tuple<double, double, double>[] BarkBandsSlaney(
            int barkFilterCount, int samplingRate, double lowFreq = 0, double highFreq = 0, bool overlap = true)
        {
            return UniformBands(Scale.HerzToBarkSlaney, Scale.BarkToHerzSlaney, barkFilterCount, samplingRate, lowFreq, highFreq, overlap);
        }

        /// <summary>
        /// Method returns frequency tuples for critical bands.
        /// </summary>
        /// <param name="filterCount">Число фильтров</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <returns>Array of frequency tuples for each Critical Band filter</returns>
        public static Tuple<double, double, double>[] CriticalBands(
            int filterCount, int samplingRate, double lowFreq = 0, double highFreq = 0)
        {
            if (lowFreq < 0)
            {
                lowFreq = 0;
            }
            if (highFreq <= lowFreq)
            {
                highFreq = samplingRate / 2.0;
            }

            double[] edgeFrequencies = { 20,   100,  200,  300,  400,  510,  630,  770,  920,  1080, 1270,  1480,  1720,
                                         2000, 2320, 2700, 3150, 3700, 4400, 5300, 6400, 7700, 9500, 12000, 15500, 20500 };

            double[] centerFrequencies = { 50,   150,  250,  350,  450,  570,  700,  840,  1000, 1170, 1370,  1600,
                                           1850, 2150, 2500, 2900, 3400, 4000, 4800, 5800, 7000, 8500, 10500, 13500, 17500 };

            int startIndex = 0;
            for (int i = 0; i < centerFrequencies.Length; i++)
            {
                if (centerFrequencies[i] < lowFreq)
                {
                    continue;
                }

                startIndex = i;
                break;
            }

            int endIndex = 0;
            for (int i = centerFrequencies.Length - 1; i >= 0; i--)
            {
                if (centerFrequencies[i] > highFreq)
                {
                    continue;
                }

                endIndex = i;
                break;
            }

            filterCount = Math.Min(endIndex - startIndex + 1, filterCount);

            double[] edges = edgeFrequencies.Skip(startIndex)
                                       .Take(filterCount + 1)
                                       .ToArray();

            double[] centers = centerFrequencies.Skip(startIndex)
                                           .Take(filterCount)
                                           .ToArray();

            Tuple<double, double, double>[] frequencyTuples = new Tuple<double, double, double>[filterCount];

            for (int i = 0; i < filterCount; i++)
            {
                frequencyTuples[i] = new Tuple<double, double, double>(edges[i], centers[i], edges[i + 1]);
            }

            return frequencyTuples;
        }

        /// <summary>
        /// Method returns frequency tuples for octave bands.
        /// </summary>
        /// <param name="octaveCount">Number of octave filters to create</param>
        /// <param name="samplingRate">Assumed Частота дискретизации of a signal</param>
        /// <param name="lowFreq">Lower bound of the frequency range</param>
        /// <param name="highFreq">Upper bound of the frequency range</param>
        /// <param name="overlap">Flag indicating that bands should overlap</param>
        /// <returns>Array of frequency tuples for each octave filter</returns>
        public static Tuple<double, double, double>[] OctaveBands(
            int octaveCount, int samplingRate, double lowFreq = 0, double highFreq = 0, bool overlap = false)
        {
            if (lowFreq < 1e-10)
            {
                lowFreq = 62.5;//Hz
            }

            if (highFreq <= lowFreq)
            {
                highFreq = samplingRate / 2.0;
            }

            double f1 = lowFreq;
            double f2 = lowFreq * 2;

            List<Tuple<double, double, double>> frequencyTuples = new List<Tuple<double, double, double>>();

            if (overlap)
            {
                double f3 = f2 * 2;

                for (int i = 0; i < octaveCount && f3 < highFreq; i++)
                {
                    frequencyTuples.Add(new Tuple<double, double, double>(f1, f2, f3));
                    f1 = f2;
                    f2 = f3;
                    f3 *= 2;
                }
            }
            else
            {
                for (int i = 0; i < octaveCount && f2 < highFreq; i++)
                {
                    frequencyTuples.Add(new Tuple<double, double, double>(f1, (f1 + f2) / 2, f2));
                    f1 *= 2;
                    f2 *= 2;
                }
            }

            return frequencyTuples.ToArray();
        }

    }
}

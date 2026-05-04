using AI.BackEnds.DSP.NWaves.Utils;
using System;
using System.Collections.Generic;

namespace AI.BackEnds.DSP.NWaves.Filters.Fda
{
    public static partial class FilterBanks
    {
        /// <summary>
        /// Method applies filters to spectrum and fills resulting filtered spectrum.
        /// </summary>
        /// <param name="filterbank"></param>
        /// <param name="spectrum"></param>
        /// <param name="filtered"></param>
        public static void Apply(float[][] filterbank, float[] spectrum, float[] filtered)
        {
            for (int i = 0; i < filterbank.Length; i++)
            {
                float en = 0.0f;

                for (int j = 0; j < spectrum.Length; j++)
                {
                    en += filterbank[i][j] * spectrum[j];
                }

                filtered[i] = en;
            }
        }

        /// <summary>
        /// Method applies filters to sequence of spectra
        /// </summary>
        /// <param name="filterbank"></param>
        /// <param name="spectrogram"></param>
        public static float[][] Apply(float[][] filterbank, IList<float[]> spectrogram)
        {
            float[][] filtered = new float[spectrogram.Count][];

            for (int k = 0; k < filtered.Length; k++)
            {
                filtered[k] = new float[filterbank.Length];
            }

            for (int i = 0; i < filterbank.Length; i++)
            {
                for (int k = 0; k < filtered.Length; k++)
                {
                    float en = 0.0f;

                    for (int j = 0; j < spectrogram[i].Length; j++)
                    {
                        en += filterbank[i][j] * spectrogram[k][j];
                    }

                    filtered[k][i] = en;
                }
            }

            return filtered;
        }

        /// <summary>
        /// Method applies filters to spectrum and then does Ln() on resulting spectrum.
        /// </summary>
        /// <param name="filterbank"></param>
        /// <param name="spectrum"></param>
        /// <param name="filtered"></param>
        /// <param name="floor">log floor</param>
        public static void ApplyAndLog(float[][] filterbank, float[] spectrum, float[] filtered, float floor = float.Epsilon)
        {
            for (int i = 0; i < filterbank.Length; i++)
            {
                float en = 0.0f;

                for (int j = 0; j < spectrum.Length; j++)
                {
                    en += filterbank[i][j] * spectrum[j];
                }

                filtered[i] = (float)Math.Log(Math.Max(en, floor));
            }
        }

        /// <summary>
        /// Method applies filters to spectrum and then does Log10() on resulting spectrum.
        /// </summary>
        /// <param name="filterbank"></param>
        /// <param name="spectrum"></param>
        /// <param name="filtered"></param>
        /// <param name="floor">log floor</param>
        public static void ApplyAndLog10(float[][] filterbank, float[] spectrum, float[] filtered, float floor = float.Epsilon)
        {
            for (int i = 0; i < filterbank.Length; i++)
            {
                float en = 0.0f;

                for (int j = 0; j < spectrum.Length; j++)
                {
                    en += filterbank[i][j] * spectrum[j];
                }

                filtered[i] = (float)Math.Log10(Math.Max(en, floor));
            }
        }

        /// <summary>
        /// Method applies filters to spectrum and then does 10*Log10() on resulting spectrum
        /// (added to compare MFCC coefficients with librosa results)
        /// </summary>
        /// <param name="filterbank"></param>
        /// <param name="spectrum"></param>
        /// <param name="filtered"></param>
        /// <param name="minLevel"></param>
        public static void ApplyAndToDecibel(float[][] filterbank, float[] spectrum, float[] filtered, float minLevel = 1e-10f)
        {
            for (int i = 0; i < filterbank.Length; i++)
            {
                float en = 0.0f;

                for (int j = 0; j < spectrum.Length; j++)
                {
                    en += filterbank[i][j] * spectrum[j];
                }

                filtered[i] = (float)Scale.ToDecibelPower(Math.Max(en, minLevel));
            }
        }

        /// <summary>
        /// Method applies filters to spectrum and then does Pow(x, power) on resulting spectrum.
        /// In PLP: power=1/3 (cubic root).
        /// </summary>
        /// <param name="filterbank"></param>
        /// <param name="spectrum"></param>
        /// <param name="filtered"></param>
        /// <param name="power"></param>
        public static void ApplyAndPow(float[][] filterbank, float[] spectrum, float[] filtered, double power = 1.0 / 3)
        {
            for (int i = 0; i < filterbank.Length; i++)
            {
                float en = 0.0f;

                for (int j = 0; j < spectrum.Length; j++)
                {
                    en += filterbank[i][j] * spectrum[j];
                }

                filtered[i] = (float)Math.Pow(en, power);
            }
        }
    }
}

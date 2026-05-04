using System;
using System.Linq;

namespace AI.BackEnds.DSP.NWaves.Transforms.Wavelets
{
    /// <summary>
    /// Wavelet
    /// </summary>
    [Serializable]
    public partial class Wavelet
    {
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The length of the mother wavelet
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// LP coefficients for decomposition
        /// </summary>
        public float[] LoD { get; set; }

        /// <summary>
        /// HP coefficients for decomposition
        /// </summary>
        public float[] HiD { get; set; }

        /// <summary>
        /// LP coefficients for reconstruction
        /// </summary>
        public float[] LoR { get; set; }

        /// <summary>
        /// HP coefficients for reconstruction
        /// </summary>
        public float[] HiR { get; set; }

        /// <summary>
        /// Конструктор from wavelet family and number of taps
        /// </summary>
        /// <param name="waveletFamily"></param>
        /// <param name="taps">Set for all wavelets</param>
        public Wavelet(WaveletFamily waveletFamily, int taps = 1)
        {
            MakeWavelet(waveletFamily, taps);
        }

        /// <summary>
        /// Конструктор from name
        /// </summary>
        /// <param name="name"></param>
        public Wavelet(string name)
        {
            WaveletFamily waveletFamily;
            int taps = 1;

            name = name.ToLower();

            if (name == "haar")
            {
                waveletFamily = WaveletFamily.Haar;
            }
            else
            {
                int digitPos = -1;
                for (int i = 0; i < name.Length; i++)
                {
                    if (char.IsDigit(name[i]))
                    {
                        digitPos = i;
                        break;
                    }
                }

                string wname = name;

                if (digitPos < 0)
                {
                    taps = 1;
                }
                else
                {
                    wname = name.Substring(0, digitPos);
                    taps = int.Parse(name.Substring(digitPos));
                }

                switch (wname)
                {
                    case "db":
                        waveletFamily = WaveletFamily.Daubechies;
                        break;
                    case "sym":
                        waveletFamily = WaveletFamily.Symlet;
                        break;
                    case "coif":
                        waveletFamily = WaveletFamily.Coiflet;
                        break;
                    default:
                        throw new ArgumentException($"Unrecognized wavelet name: {name}");
                }
            }

            MakeWavelet(waveletFamily, taps);
        }

        /// <summary>
        /// Fill wavelet fields: name, length and coefficients
        /// </summary>
        /// <param name="waveletFamily"></param>
        /// <param name="taps"></param>
        private void MakeWavelet(WaveletFamily waveletFamily, int taps)
        {
            switch (waveletFamily)
            {
                case WaveletFamily.Daubechies:
                    MakeDaubechiesWavelet(taps);
                    break;
                case WaveletFamily.Symlet:
                    MakeSymletWavelet(taps);
                    break;
                case WaveletFamily.Coiflet:
                    MakeCoifletWavelet(taps);
                    break;
                default:
                    MakeHaarWavelet();
                    break;
            }

            ComputeOrthonormalCoeffs();
        }

        /// <summary>
        /// Compute orthonormal coefficients from LoD coefficients only
        /// </summary>
        public void ComputeOrthonormalCoeffs()
        {
            HiD = LoD.Reverse().ToArray();

            for (int i = 0; i < HiD.Length; i += 2)
            {
                HiD[i] = -HiD[i];
            }

            LoR = LoD.Reverse().ToArray();
            HiR = HiD.Reverse().ToArray();
        }


        #region wavelet coefficients

        /// <summary>
        /// Haar wavelet
        /// </summary>
        public void MakeHaarWavelet()
        {
            Name = "haar";
            Length = 2;

            float sqrt2 = (float)Math.Sqrt(2);

            LoD = new[] { 1 / sqrt2, 1 / sqrt2 };
        }

        #endregion
    }
}

using System;

namespace AI.BackEnds.DSP.NWaves.Filters.BiQuad
{
    /// <summary>
    /// ������������ ������� ������ �����������
    /// 
    /// The coef "A"re calculated automatically according to 
    /// audio-eq-cookbook by R.Bristow-Johnson and WebAudio API.
    /// </summary>
    [Serializable]
    public class PeakFilter : BiQuadFilter
    {
        /// <summary>
        /// �������
        /// </summary>
        public double Freq { get; protected set; }

        /// <summary>
        /// �����������
        /// </summary>
        public double Q { get; protected set; }

        /// <summary>
        /// ��������
        /// </summary>
        public double Gain { get; protected set; }

        /// <summary>
        /// �����������
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="q"></param>
        /// <param name="gain"></param>
        public PeakFilter(double freq, double q = 1, double gain = 1.0)
        {
            SetCoefficients(freq, q, gain);
        }

        /// <summary>
        /// ���������� ������������ �������
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="q"></param>
        /// <param name="gain"></param>
        private void SetCoefficients(double freq, double q, double gain)
        {
            Freq = freq;
            Q = q;
            Gain = gain;

            double ga = Math.Pow(10, gain / 40);
            double omega = 2 * Math.PI * freq;
            double alpha = Math.Sin(omega) / (2 * q);
            double cosw = Math.Cos(omega);

            _b[0] = 1 + (alpha * ga);
            _b[1] = -2 * cosw;
            _b[2] = 1 - (alpha * ga);

            _a[0] = 1 + (alpha / ga);
            _a[1] = -2 * cosw;
            _a[2] = 1 - (alpha / ga);

            Normalize();
        }

        /// <summary>
        /// �������� ��������� ������� (� ����������� ��� ���������)
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="q"></param>
        /// <param name="gain"></param>
        public void Change(double freq, double q = 1, double gain = 1.0)
        {
            SetCoefficients(freq, q, gain);
        }
    }
}

using System;

namespace AI.BackEnds.DSP.NWaves.Filters.OnePole
{
    /// <summary>
    /// ����� ��� �������-�������� ������� ������� ������
    /// </summary>
    [Serializable]

    public class HighPassFilter : OnePoleFilter
    {
        /// <summary>
        /// �������
        /// </summary>
        public double Freq { get; protected set; }

        /// <summary>
        /// �����������
        /// </summary>
        /// <param name="freq"></param>
        public HighPassFilter(double freq)
        {
            SetCoefficients(freq);
        }

        /// <summary>
        /// ���������� ������������ �������
        /// </summary>
        /// <param name="freq"></param>
        private void SetCoefficients(double freq)
        {
            _a[0] = _a[_denominatorSize] = 1;
            _a[1] = _a[_denominatorSize + 1] = (float)Math.Exp(-2 * Math.PI * (0.5 - freq));

            _b[0] = _b[_numeratorSize] = 1 - _a[1];
        }

        /// <summary>
        /// �������� ��������� ������� (� ����������� ��� ���������)
        /// </summary>
        /// <param name="freq"></param>
        public void Change(double freq)
        {
            SetCoefficients(freq);
        }
    }
}

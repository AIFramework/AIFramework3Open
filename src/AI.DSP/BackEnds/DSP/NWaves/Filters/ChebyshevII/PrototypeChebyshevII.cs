using AI.BackEnds.DSP.NWaves.Utils;
using System;
using System.Numerics;

namespace AI.BackEnds.DSP.NWaves.Filters.ChebyshevII
{
    /// <summary>
    /// Прототип фильтра Чебышёва 2го рода
    /// </summary>
    public static class PrototypeChebyshevII
    {
        /// <summary>
        /// Полюсы передаточной функции
        /// </summary>
        /// <param name="order">Порядок</param>
        /// <param name="ripple">Коэф. пульсаций</param>
        /// <returns></returns>
        public static Complex[] Poles(int order, double ripple = 0.1)
        {
            double eps = Math.Sqrt(Math.Pow(10, ripple / 10) - 1);
            double s = MathUtilsDSP.Asinh(1 / eps) / order;
            double sinh = Math.Sinh(s);
            double cosh = Math.Cosh(s);

            Complex[] poles = new Complex[order];

            for (int k = 0; k < order; k++)
            {
                double theta = Math.PI * ((2 * k) + 1) / (2 * order);
                double re = -sinh * Math.Sin(theta);
                double im = cosh * Math.Cos(theta);
                poles[k] = 1 / new Complex(re, im);
            }

            return poles;
        }

        /// <summary>
        /// Нули передаточной функции.
        /// Для нечётного порядка центральный ноль уходит на бесконечность
        /// (так же поступает scipy.signal.cheby2), поэтому возвращается на один
        /// ноль меньше, чем полюсов.
        /// </summary>
        /// <param name="order">Порядок фильтра</param>
        /// <returns></returns>
        public static Complex[] Zeros(int order)
        {
            bool odd = (order & 1) == 1;
            int skip = odd ? (order - 1) / 2 : -1;
            int count = odd ? order - 1 : order;

            Complex[] zeros = new Complex[count];

            int idx = 0;
            for (int k = 0; k < order; k++)
            {
                if (k == skip)
                {
                    continue;
                }

                double theta = Math.PI * ((2 * k) + 1) / (2 * order);
                zeros[idx++] = new Complex(0, -1 / Math.Cos(theta));
            }

            return zeros;
        }
    }
}

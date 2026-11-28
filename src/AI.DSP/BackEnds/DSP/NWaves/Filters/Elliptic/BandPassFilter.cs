using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Filters.Fda;
using System;

namespace AI.BackEnds.DSP.NWaves.Filters.Elliptic
{

    /// <summary>
    /// Полосно пропускающий фильтр (полосовой)
    /// </summary>
    [Serializable]

    [System.Obsolete("Используйте AI.DSP.IIR.IirDesign (IirFamily.Elliptic); здесь при чётном порядке АЧХ поднята на неравномерность над 0 дБ.")]
    public class BandPassFilter : IirFilter
    {
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="freq1"></param>
        /// <param name="freq2"></param>
        /// <param name="order"></param>
        /// <param name="ripplePass"></param>
        /// <param name="rippleStop"></param>
        public BandPassFilter(double freq1, double freq2, int order, double ripplePass = 1, double rippleStop = 20) :
            base(MakeTf(freq1, freq2, order, ripplePass, rippleStop))
        {
        }

        /// <summary>
        /// Создание передаточной функции
        /// </summary>
        /// <param name="freq1"></param>
        /// <param name="freq2"></param>
        /// <param name="order"></param>
        /// <param name="ripplePass"></param>
        /// <param name="rippleStop"></param>
        /// <returns></returns>
        private static TransferFunction MakeTf(double freq1, double freq2, int order, double ripplePass = 1, double rippleStop = 20)
        {
            return DesignFilter.IirBpTf(freq1, freq2,
                                        PrototypeElliptic.Poles(order, ripplePass, rippleStop),
                                        PrototypeElliptic.Zeros(order, ripplePass, rippleStop));
        }
    }
}

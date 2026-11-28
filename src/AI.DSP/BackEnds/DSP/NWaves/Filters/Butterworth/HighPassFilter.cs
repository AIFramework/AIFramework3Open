using AI.BackEnds.DSP.NWaves.Filters.Base;
using AI.BackEnds.DSP.NWaves.Filters.Base64;
using AI.BackEnds.DSP.NWaves.Filters.Fda;
using System;

namespace AI.BackEnds.DSP.NWaves.Filters.Butterworth
{
    /// <summary>
    /// Фильтр Баттервордта верхних частот
    /// </summary>
    [Serializable]
    [System.Obsolete("Используйте AI.DSP.IIR.IirDesign (IirFamily.Butterworth): дизайн по ТЗ, звенья второго порядка.")]
    public class HighPassFilter : IirFilter64
    {
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="order"></param>
        public HighPassFilter(double freq, int order) : base(MakeTf(freq, order))
        {
        }

        /// <summary>
        /// Создание передаточной функции
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        private static TransferFunction MakeTf(double freq, int order)
        {
            return DesignFilter.IirHpTf(freq, PrototypeButterworth.Poles(order));
        }
    }
}

using System;

namespace AI.BackEnds.DSP.NWaves.Filters.Adaptive
{
    /// <summary>
    /// Адаптивный фильтр (Нормализованный Least-Mean-Fourth алгоритм + смещение)
    /// </summary>
    [Serializable]
    public class NlmfFilter : AdaptiveFilter
    {
        /// <summary>
        /// Mu
        /// </summary>
        private readonly float _mu;

        /// <summary>
        /// Смещение
        /// </summary>
        private readonly float _eps;

        /// <summary>
        /// Утечка
        /// </summary>
        private readonly float _leakage;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="order"></param>
        /// <param name="mu"></param>
        /// <param name="eps"></param>
        /// <param name="leakage"></param>
        public NlmfFilter(int order, float mu = 0.75f, float eps = 1, float leakage = 0) : base(order)
        {
            _mu = mu;
            _eps = eps;
            _leakage = leakage;
        }

        /// <summary>
        /// Входные данные процесса и целевые данные
        /// </summary>
        /// <param name="input"></param>
        /// <param name="desired"></param>
        /// <returns></returns>
        public override float Process(float input, float desired)
        {
            int offset = _delayLineOffset;

            _delayLine[offset + _kernelSize] = input;   // duplicate it for better loop performance


            float y = Process(input);

            float e = desired - y;

            // Норма только по актуальному окну длины _kernelSize (буфер продублирован).
            float norm = _eps;
            for (int i = 0, pos = offset; i < _kernelSize; i++, pos++)
            {
                float s = _delayLine[pos];
                norm += s * s;
            }

            float step = 4 * _mu * e * e * e / norm;
            float retain = 1 - (_leakage * _mu);

            for (int i = 0; i < _kernelSize; i++, offset++)
            {
                _b[i] = _b[_kernelSize + i] = (retain * _b[i]) + (step * _delayLine[offset]);
            }

            return y;
        }
    }
}

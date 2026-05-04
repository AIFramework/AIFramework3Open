using System;

namespace AI.BackEnds.DSP.NWaves.Filters.Adaptive
{
    /// <summary>
    /// Адаптивный фильтр (Recursive-Least-Squares алгоритм)
    /// </summary>
    [Serializable]
    public class RlsFilter : AdaptiveFilter
    {
        /// <summary>
        /// Lambda
        /// </summary>
        private readonly float _lambda;

        /// <summary>
        /// Обратная корреляционная матрица
        /// </summary>
        private readonly float[,] _p;

        /// <summary>
        /// Матрица коэффициентов усиления
        /// </summary>
        private readonly float[] _gains;

        /// <summary>
        /// Нeнормированное произведение P*x (сохраняется между шагами,
        /// чтобы не пересчитывать x^T * P заново при обновлении P)
        /// </summary>
        private readonly float[] _px;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="order"></param>
        /// <param name="lambda"></param>
        /// <param name="initCoeff"></param>
        public RlsFilter(int order, float lambda = 0.99f, float initCoeff = 1e2f) : base(order)
        {
            _lambda = lambda;

            _p = new float[_kernelSize, _kernelSize];
            for (int i = 0; i < _kernelSize; i++)
            {
                _p[i, i] = initCoeff;
            }

            _gains = new float[_kernelSize];
            _px = new float[_kernelSize];
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


            // ======================================================================
            // ============= lot of calculations before updating weights ============
            // ======================================================================

            // =========== calculate gain coefficients ===========
            // ===========   p*x / (lambda + xT*p*x)   ===========

            // 1) px = P * x
            for (int i = 0; i < _kernelSize; i++)
            {
                float s = 0f;
                for (int k = 0, pos = offset; k < _kernelSize; k++, pos++)
                {
                    s += _p[i, k] * _delayLine[pos];
                }
                _px[i] = s;
            }

            // 2) g = lambda + xT * P * x
            float g = _lambda;
            for (int k = 0, pos = offset; k < _kernelSize; k++, pos++)
            {
                g += _px[k] * _delayLine[pos];
            }

            // 3) gains = px / g
            float invG = 1f / g;
            for (int i = 0; i < _kernelSize; i++)
            {
                _gains[i] = _px[i] * invG;
            }

            // ============ update inv corr matrix ================
            // ========== (P - gain * (xT * P)) / lambda ==========
            //
            // Используем симметрию P: xT * P = (P * x)T = _px
            // Поэтому (gain * xT * P)[i, j] = gains[i] * _px[j],
            // что снижает сложность шага с O(N^3) до O(N^2).

            float invLambda = 1f / _lambda;
            for (int i = 0; i < _kernelSize; i++)
            {
                float gi = _gains[i];
                for (int j = 0; j < _kernelSize; j++)
                {
                    _p[i, j] = (_p[i, j] - (gi * _px[j])) * invLambda;
                }
            }

            // ======================================================================
            // ===================== finally, update weights: =======================
            // ======================================================================

            for (int i = 0; i < _kernelSize; i++)
            {
                _b[i] = _b[_kernelSize + i] = _b[i] + (_gains[i] * e);
            }

            return y;
        }
    }
}

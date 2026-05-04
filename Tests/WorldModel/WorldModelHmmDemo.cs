using AI.DataStructs.Algebraic;
using AI.ML.SequenceAnalysis.HMM;
using AI.ML.Utils.NeuralSymbolic;
using System;

namespace WorldModel
{
    /// <summary>
    /// Сборка демонстрационной дискретной цепи Маркова (512 состояний) и столбцов матрицы переходов.
    /// </summary>
    internal static class WorldModelHmmDemo
    {
        public const int StateCount = 512;

        public static (HMM Hmm, Vector[] Columns) Build()
        {
            Matrix matrixState = new Matrix(StateCount, StateCount);
            int[] states = new int[StateCount];

            for (int i = 0; i < StateCount; i++)
            {
                for (int j = 0; j < StateCount; j++)
                {
                    double p = 1;
                    if (i != j)
                    {
                        p = 0.9;
                    }

                    matrixState[i, j] = p * Math.Max(0, Similarity.CorrelationIntInt(i, j));
                    if (Similarity.Bools2Vect(i.DecimalToGrayBits(9)).Sum() != Similarity.Bools2Vect(j.DecimalToGrayBits(9)).Sum())
                    {
                        matrixState[i, j] *= 0.0002;
                    }
                }

                states[i] = i;
            }

            var hmm = new HMM
            {
                stateMatrix = matrixState,
                stateAlter = 1 - matrixState - 0.000001,
                states = states
            };

            Vector[] columns = Matrix.GetColumns(matrixState);
            return (hmm, columns);
        }
    }
}

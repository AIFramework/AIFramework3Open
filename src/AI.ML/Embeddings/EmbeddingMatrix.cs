using AI.DataStructs.Algebraic;
using System;

namespace AI.ML.Embeddings;

/// <summary>
/// Матрица векторов встраивания
/// </summary>
[Serializable]
public class EmbeddingMatrix
{
    /// <summary>
    /// Векторы встраивания, строки
    /// </summary>
    public Vector[] Rows { get; private set; }

    /// <summary>
    /// Матрица векторов встраивания
    /// </summary>
    public EmbeddingMatrix(Vector[] data)
    {
        Rows = new Vector[data.Length];
        for (int i = 0; i < data.Length; i++)
            Rows[i] = data[i].Clone();
    }

    /// <summary>
    /// Матрица векторов встраивания
    /// </summary>
    public EmbeddingMatrix(int countVectors = 5, int embedingDimention = 5)
    {
        Rows = new Vector[countVectors];
        double pikToPik = 2.0 / Math.Sqrt(embedingDimention);

        for (int i = 0; i < Rows.Length; i++)
        {
            Rows[i] = pikToPik * (Statistics.Statistic.UniformDistribution(embedingDimention) - 0.5);
        }
    }
}

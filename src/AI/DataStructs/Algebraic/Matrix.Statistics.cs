using AI.DataStructs.Shapes;
using AI.Extensions;
using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI.DataStructs.Algebraic;

public partial class Matrix
{
    #region Статические методы — Статистика
    /// <summary>
    /// ╨Ь╨╡╤В╨╛╨┤ ╤Б╨╛╨╖╨┤╨░╨╡╤В ╨╝╨░╤В╤А╨╕╤Ж╤Г ╤Б ╨║╨╛╤Н╤Д╤Д╨╕╤Ж╨╕╨╡╨╜╤В╨░╨╝╨╕ ╨┐╨╛╨┐╨░╤А╨╜╨╛╨╣ ╨║╨╛╤А╤А╨╡╨╗╤П╤Ж╨╕╨╕ ╨▓╨╡╨║╤В╨╛╤А╨╛╨▓
    /// </summary>
    /// <param name="vectors">╨Т╨╡╨║╤В╨╛╤А╨░</param>
    /// <returns>╨Ъ╨╛╤А╤А╨╡╨╗╤П╤Ж╨╕╨╛╨╜╨╜╨░╤П ╨╝╨░╤В╤А╨╕╤Ж╨░</returns>
    public static Matrix GetCorrelationMatrixNorm(Vector[] vectors)
    {
        Matrix corelationMatrix = new Matrix(vectors.Length, vectors.Length);
        for (int i = 0; i < vectors.Length; i++)
            for (int j = i; j < vectors.Length; j++)
                if (i == j) corelationMatrix[i, j] = 1;
                else
                {
                    corelationMatrix[i, j] = Statistic.CorrelationCoefficient(vectors[i], vectors[j]);
                    corelationMatrix[j, i] = corelationMatrix[i, j];
                }

        return corelationMatrix;
    }
    /// <summary>
    /// ╨Ь╨╡╤В╨╛╨┤ ╤Б╨╛╨╖╨┤╨░╨╡╤В ╨╝╨░╤В╤А╨╕╤Ж╤Г ╤Б ╨║╨╛╤Н╤Д╤Д╨╕╤Ж╨╕╨╡╨╜╤В╨░╨╝╨╕ ╨┐╨╛╨┐╨░╤А╨╜╨╛╨╣ ╨║╨╛╨▓╨░╤А╨╕╤Ж╨╕╨╕ ╨▓╨╡╨║╤В╨╛╤А╨╛╨▓
    /// </summary>
    /// <param name="vectors">╨Т╨╡╨║╤В╨╛╤А╨░</param>
    /// <returns>╨Ъ╨╛╨▓╨░╤А╨╕╨░╤Ж╨╕╨╛╨╜╨╜╨░╨╣ ╨╝╨░╤В╤А╨╕╤Ж╨░</returns>
    public static Matrix GetCovMatrix(Vector[] vectors)
    {
        Matrix covMatrix = new Matrix(vectors.Length, vectors.Length);
        for (int i = 0; i < vectors.Length; i++)
            for (int j = i; j < vectors.Length; j++)
            {
                covMatrix[i, j] = Statistic.Cov(vectors[i], vectors[j]);
                covMatrix[j, i] = covMatrix[i, j];
            }

        return covMatrix;
    }

    /// <summary>
    /// ╨Ь╨╡╤В╨╛╨┤ ╤Б╨╛╨╖╨┤╨░╨╡╤В ╨╝╨░╤В╤А╨╕╤Ж╤Г ╤Б ╨║╨╛╤Н╤Д╤Д╨╕╤Ж╨╕╨╡╨╜╤В╨░╨╝╨╕ ╨┐╨╛╨┐╨░╤А╨╜╨╛╨╣ ╨║╨╛╨▓╨░╤А╨╕╤Ж╨╕╨╕ ╨▓╨╡╨║╤В╨╛╤А╨╛╨▓
    /// </summary>
    /// <param name="matrix">╨Ь╨░╤В╤А╨╕╤Ж╨░</param>
    /// <returns>╨Ъ╨╛╨▓╨░╤А╨╕╨░╤Ж╨╕╨╛╨╜╨╜╨░╨╣ ╨╝╨░╤В╤А╨╕╤Ж╨░</returns>
    public static Matrix GetCovMatrixFromColumns(Matrix matrix)
    {
        Vector[] vectors = GetColumns(matrix);
        return GetCovMatrix(vectors);
    }
    /// <summary>
    /// ╨Ь╨░╤В╤А╨╕╤Ж╨░ ╤Б╤А╨╡╨┤╨╜╨╕╤Е 
    /// </summary>
    public static Matrix MeanMatrix(Matrix[] matrices)
    {
        if (matrices == null)
        {
            throw new ArgumentNullException(nameof(matrices));
        }

        if (matrices.Length == 0)
        {
            throw new ArgumentException("Given array is empty", nameof(matrices));
        }

        Matrix m = new Matrix(matrices[0].Height, matrices[0].Width);

        for (int i = 0; i < matrices.Length; i++)
        {
            m += matrices[i];
        }

        return m / matrices.Length;
    }
    /// <summary>
    /// ╨Ь╨░╤В╤А╨╕╤Ж╨░ ╨┤╨╕╤Б╨┐╨╡╤А╤Б╨╕╨╣ 
    /// </summary>
    public static Matrix DispersionMatrix(Matrix[] matrices)
    {
        if (matrices == null)
        {
            throw new ArgumentNullException(nameof(matrices));
        }

        if (matrices.Length == 0)
        {
            throw new ArgumentException("Given array is empty", nameof(matrices));
        }

        if (matrices.Length == 1)
        {
            throw new ArgumentException("At least two matrices are required to compute dispersion", nameof(matrices));
        }

        Matrix m = new Matrix(matrices[0].Height, matrices[0].Width), sq, matrixM = MeanMatrix(matrices);

        for (int i = 0; i < matrices.Length; i++)
        {
            sq = matrices[i] - matrixM;
            m += sq * sq;
        }

        return m / (matrices.Length - 1);
    }
    /// <summary>
    /// ╨Ь╨░╤В╤А╨╕╤Ж╨░ ╤Б╤А╨╡╨┤╨╜╨╡╨║╨▓╨░╨┤╤А╨░╤В╨╕╤З╨╜╤Л╤Е ╨╛╤В╨║╨╗╨╛╨╜╨╡╨╜╨╕╨╣
    /// </summary>
    /// <param name="matrices">╨Ь╨░╤Б╤Б╨╕╨▓ ╨╝╨░╤В╤А╨╕╤Ж</param>
    public static Matrix StdMatrix(Matrix[] matrices)
    {
        return DispersionMatrix(matrices).Transform(Math.Sqrt);
    }
    #endregion
}

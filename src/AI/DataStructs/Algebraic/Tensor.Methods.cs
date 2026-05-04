using AI.DataStructs.Shapes;
using AI.Extensions;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI.DataStructs.Algebraic;

public partial class Tensor
{
    /// <summary>
    /// Копирование
    /// </summary>
    public Tensor Copy()
    {
        Tensor tensor3 = new Tensor(Height, Width, Depth);
        Buffer.BlockCopy(Data, 0, tensor3.Data, 0, 8 * Shape.Count);
        return tensor3;
    }
    /// <summary>
    /// Поэлементное преобразование тензора
    /// </summary>
    /// <param name="transform">Функция преобразования</param>
    public Tensor Transform(Func<double, double> transform)
    {
        Tensor newTensor = new Tensor(Height, Width, Depth);
        int len = Shape.Count;
        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = transform(Data[i]);
        }

        return newTensor;
    }

    /// <summary>
    /// Конвертация тензор в массив матриц
    /// </summary>
    public Matrix[] ToMatrices()
    {
        Matrix[] matrix = new Matrix[Depth];

        for (int i = 0; i < matrix.Length; i++)
        {
            matrix[i] = new Matrix(Height, Width);
        }

        for (int k = 0; k < Depth; k++)
        {
            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    matrix[k][i, j] = this[i, j, k];
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// Конвертация массива матриц в тензор
    /// </summary>
    public static Tensor FromMatrices(Matrix[] matrices)
    {
        Tensor tensor = new Tensor(matrices[0].Height, matrices[0].Width, matrices.Length);


        for (int k = 0; k < matrices.Length; k++)
        {
            for (int i = 0; i < matrices[0].Height; i++)
            {
                for (int j = 0; j < matrices[0].Width; j++)
                {
                    tensor[i, j, k] = matrices[k][i, j];
                }
            }
        }

        return tensor;
    }
    /// <summary>
    /// Конвертация вектора в тензор
    /// </summary>
    public static Tensor VectorToTensor(Vector data, int h, int w)
    {
        int d = data.Count / (h * w);

        Tensor tensor = new Tensor(h, w, d);

        for (int i = 0, l = 0; i < h; i++)
        {
            for (int j = 0; j < w; j++)
            {
                for (int k = 0; k < d; k++)
                {
                    tensor[i, j, k] = data[l++];
                }
            }
        }

        return tensor;
    }
    /// <summary>
    /// Поэлементное вычитание из тензора по глубине TNew[i,j,k] = T[i,j,k] - V[k]
    /// </summary>
    /// <returns></returns>
    public Tensor SubtractingDepth(Vector minus)
    {
        Tensor tensor = new Tensor(Height, Width, Depth);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                for (int k = 0; k < Depth; k++)
                {
                    tensor[i, j, k] = this[i, j, k] - minus[k];
                }
            }
        }

        return tensor;
    }
    /// <summary>
    /// Поэлементное прибавление к тензору по глубине TNew[i,j,k] = T[i,j,k] + V[k]
    /// </summary>
    public Tensor PlusD(Vector ps)
    {
        Tensor tensor = new Tensor(Height, Width, Depth);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                for (int k = 0; k < Depth; k++)
                {
                    tensor[i, j, k] = this[i, j, k] + ps[k];
                }
            }
        }

        return tensor;
    }

    #region Статистика

    /// <summary>
    /// Сумма всех элементов тензора
    /// </summary>
    public double Sum()
    {
        double summ = 0;
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            summ += Data[i];
        }

        return summ;
    }

    /// <summary>
    /// Среднее всех элементов тензора
    /// </summary>
    public double Mean()
    {
        return Sum() / Shape.Count;
    }

    /// <summary>
    /// Дисперсия
    /// </summary>
    /// <param name="mean">Рассчитанное среднее</param>
    public double Dispersion(double mean)
    {
        double summ = 0;
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            summ += Math.Pow(Data[i] - mean, 2);
        }

        return summ / (Shape.Count - 1);
    }

    /// <summary>
    /// Дисперсия
    /// </summary>
    public double Dispersion()
    {
        double mean = Mean();
        double summ = 0;
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            summ += Math.Pow(Data[i] - mean, 2);
        }

        return summ / (Shape.Count - 1);
    }

    /// <summary>
    /// Среднеквадратичное отклонение
    /// </summary>
    /// <param name="mean">Рассчитанное среднее</param>
    public double Std(double mean)
    {
        return Math.Sqrt(Dispersion(mean));
    }

    /// <summary>
    /// Среднеквадратичное отклонение
    /// </summary>
    public double Std()
    {
        return Math.Sqrt(Dispersion());
    }

    #endregion

    #region Технические методы

    /// <summary>
    /// Перевод тензора в строку
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return ToString(AISettings.GetProvider());
    }

    /// <summary>
    /// Перевод тензора в строку
    /// </summary>
    public string ToString(NumberFormatInfo provider)
    {
        StringBuilder sb = new StringBuilder();

        for (int k = 0; k < Depth; k++)
        {
            _ = sb.Append("Deep #");
            _ = sb.Append(k + 1);
            _ = sb.AppendLine(":");

            for (int i = 0; i < Height; i++)
            {
                _ = sb.Append("[");
                for (int j = 0; j < Width; j++)
                {
                    _ = sb.Append(this[i, j, k].ToString(provider));
                    _ = sb.Append(" ");
                }
                sb.Length--;
                _ = sb.AppendLine("]");
            }
        }

        sb.Length -= Environment.NewLine.Length;
        return sb.ToString();
    }

    /// <summary>
    /// Сравнение с объектом
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj is Tensor tensor)
        {
            return tensor == this;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Сравнение с другим тензором
    /// </summary>
    public bool Equals(Tensor other)
    {
        return this == other;
    }

    /// <summary>
    /// Вернуть хэш-код
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ((Vector)Data).GetHashCode();
            hash = (hash * 13) + Height;
            hash = (hash * 13) + Width;
            hash = (hash * 13) + Depth;
            return hash;
        }
    }


    #endregion
}

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
    #region Операторы
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static Tensor operator +(Tensor A, double b)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] + b;
        }

        return newTensor;
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="B"></param>
    /// <returns></returns>
    public static Tensor operator +(Tensor A, Tensor B)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);

        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] + B.Data[i];
        }

        return newTensor;
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static Tensor operator +(double b, Tensor A)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] + b;
        }

        return newTensor;
    }
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="K"></param>
    /// <returns></returns>
    public static Tensor operator *(Tensor A, double K)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] * K;
        }

        return newTensor;
    }
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="K"></param>
    /// <returns></returns>
    public static Tensor operator *(double K, Tensor A)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] * K;
        }

        return newTensor;
    }
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="B"></param>
    /// <returns></returns>
    public static Tensor operator *(Tensor A, Tensor B)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] * B.Data[i];
        }

        return newTensor;
    }
    /// <summary>
	/// Деление
	/// </summary>
	public static Tensor operator /(Tensor A, double b)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] / b;
        }

        return newTensor;
    }
    /// <summary>
    /// Деление
    /// </summary>
    public static Tensor operator /(double b, Tensor A)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = b / A.Data[i];
        }

        return newTensor;
    }
    /// <summary>
    /// Деление
    /// </summary>
    public static Tensor operator /(Tensor A, Tensor B)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] / B.Data[i];
        }

        return newTensor;
    }
    /// <summary>
    /// Вычитание
    /// </summary>
    public static Tensor operator -(Tensor A, double b)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = A.Data[i] - b;
        }

        return newTensor;
    }
    /// <summary>
    /// Вычитание
    /// </summary>
    public static Tensor operator -(double b, Tensor A)
    {
        Tensor newTensor = new Tensor(A.Width, A.Height, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = b - A.Data[i];
        }

        return newTensor;
    }
    /// <summary>
    /// Вычитание
    /// </summary>
    public static Tensor operator -(Tensor B, Tensor A)
    {
        Tensor newTensor = new Tensor(A.Height, A.Width, A.Depth);
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            newTensor.Data[i] = B.Data[i] - A.Data[i];
        }

        return newTensor;
    }
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator ==(Tensor left, Tensor right)
    {
        return left.Shape == right.Shape && left.Data.ElementWiseEqual(right.Data);
    }

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator !=(Tensor left, Tensor right)
    {
        return left.Shape != right.Shape || !left.Data.ElementWiseEqual(right.Data);
    }
    #endregion
}

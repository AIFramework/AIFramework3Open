using AI.DataStructs.Shapes;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DataStructs.WithComplexElements;

public partial class ComplexVector
{
    #region Операторы
    /// <summary>
    /// Поэлементное сложение
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator +(ComplexVector left, ComplexVector right)
    {
        if (left.Count != right.Count)
        {
            throw new InvalidOperationException("Lengths of given vectors mismatch");
        }

        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
        {
            C[i] = left[i] + right[i];
        }

        return C;
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator +(Complex k, ComplexVector vector)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
            C[i] = k + vector[i];

        return C;
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator +(ComplexVector vector, Complex k)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = vector[i] + k;
        }

        return C;
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator +(double k, ComplexVector vector)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = k + vector[i];
        }

        return C;
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator +(ComplexVector vector, double k)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
            C[i] = vector[i] - k;

        return C;
    }
    /// <summary>
    /// Отрицание
    /// </summary>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator -(ComplexVector vector)
    {
        return 0.0 - vector;
    }
    /// <summary>
    /// Поэлементное вычитание
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator -(ComplexVector left, ComplexVector right)
    {
        if (left.Count != right.Count)
        {
            throw new InvalidOperationException("Lengths of given vectors mismatch");
        }

        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
        {
            C[i] = left[i] - right[i];
        }

        return C;
    }


    /// <summary>
    /// Поэлементное умножить
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator *(ComplexVector left, ComplexVector right)
    {
        if (left.Count != right.Count)
            throw new InvalidOperationException("Lengths of given vectors mismatch");


        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
            C[i] = left[i] * right[i];

        return C;
    }

    /// <summary>
    /// Деление с остатком
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator %(ComplexVector left, ComplexVector right)
    {
        if (left.Count != right.Count)
            throw new InvalidOperationException("Lengths of given vectors mismatch");


        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
            C[i] = new Complex(left[i].Real % right[i].Real, left[i].Imaginary % right[i].Imaginary);

        return C;
    }

    /// <summary>
    /// Деление с остатком
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator %(ComplexVector left, Complex right)
    {

        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
            C[i] = new Complex(left[i].Real % right.Real, left[i].Imaginary % right.Imaginary);

        return C;
    }

    /// <summary>
    /// Деление с остатком
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator %(Complex left, ComplexVector right)
    {
        ComplexVector C = new ComplexVector(right.Count);

        for (int i = 0; i < right.Count; i++)
            C[i] = new Complex(left.Real % right[i].Real, left.Imaginary % right[i].Imaginary);

        return C;
    }

    /// <summary>
    /// Вычитание из числа
    /// </summary>
    /// <param name="k">комплексное число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator -(Complex k, ComplexVector vector)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = k - vector[i];
        }

        return C;
    }
    /// <summary>
    /// Вычитание числа
    /// </summary>
    /// <param name="k">комплексное число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator -(ComplexVector vector, Complex k)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = vector[i] - k;
        }

        return C;
    }
    /// <summary>
    /// Вычитание из числа
    /// </summary>
    /// <param name="k">реальное число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator -(double k, ComplexVector vector)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = k - vector[i];
        }

        return C;
    }
    /// <summary>
    /// Вычитание числа
    /// </summary>
    /// <param name="k"> число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator -(ComplexVector vector, double k)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = vector[i] - k;
        }

        return C;
    }
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator *(ComplexVector vector, Complex k)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = k * vector[i];
        }

        return C;
    }
    /// <summary>
    /// Поэлементное умножение на реальный вектор
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator *(ComplexVector left, Vector right)
    {
        if (left.Count != right.Count)
        {
            throw new InvalidOperationException("Lengths of given vectors mismatch");
        }

        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
        {
            C[i] = left[i] * right[i];
        }

        return C;
    }
    /// <summary>
    /// Поэлементное умножение на реальный вектор
    /// </summary>
    /// <param name="right">Первый вектор</param>
    /// <param name="left">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator *(Vector left, ComplexVector right)
    {
        if (right.Count != left.Count)
        {
            throw new InvalidOperationException("Lengths of given vectors mismatch");
        }

        ComplexVector C = new ComplexVector(right.Count);

        for (int i = 0; i < right.Count; i++)
        {
            C[i] = right[i] * left[i];
        }

        return C;
    }
    /// <summary>
    /// Умножение на число
    /// </summary>
    /// <param name="k">комплексное число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator *(Complex k, ComplexVector vector)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = k * vector[i];
        }

        return C;
    }
    // Поэлементное умножение (отключено — конфликтует со скалярным произведением)
    //public static ComplexVector operator *(ComplexVector left, ComplexVector right)
    //{
    //    if (left.Count != right.Count)
    //    {
    //        throw new InvalidOperationException("Lengths of given vectors mismatch");
    //    }
    //    ComplexVector C = new ComplexVector(left.Count);
    //    for (int i = 0; i < left.Count; i++)
    //    {
    //        C[i] = left[i] * right[i];
    //    }
    //    return C;
    //}

    /// <summary>
    /// Деление
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator /(Complex k, ComplexVector vector)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = k / vector[i];
        }

        return C;
    }
    /// <summary>
    /// Деление
    /// </summary>
    /// <param name="k">Число</param>
    /// <param name="vector">Комплексный вектор</param>
    /// <returns>Комплексный вектор</returns>
    public static ComplexVector operator /(ComplexVector vector, Complex k)
    {
        ComplexVector C = new ComplexVector(vector.Count);

        for (int i = 0; i < vector.Count; i++)
        {
            C[i] = vector[i] / k;
        }

        return C;
    }
    /// <summary>
    /// Поэлементное деление
    /// </summary>
    /// <param name="left">Первый вектор</param>
    /// <param name="right">Второй</param>
    /// <returns>Результат</returns>
    public static ComplexVector operator /(ComplexVector left, ComplexVector right)
    {
        if (left.Count != right.Count)
        {
            throw new InvalidOperationException("Lengths of given vectors mismatch");
        }

        ComplexVector C = new ComplexVector(left.Count);

        for (int i = 0; i < left.Count; i++)
        {
            C[i] = left[i] / right[i];
        }

        return C;
    }
    /// <summary>
    /// Implicit cast ComplexVector -> Complex[]
    /// </summary>
    /// <param name="vect">Вектор</param>
    public static implicit operator Complex[](ComplexVector vect)
    {
        return vect.ToArray();
    }
    /// <summary>
    /// Implicit cast Complex[] -> ComplexVector
    /// </summary>
    /// <param name="dbs">Complex array</param>
    public static implicit operator ComplexVector(Complex[] dbs)
    {
        return new ComplexVector(dbs);
    }
    /// <summary>
    /// Implicit cast double[] -> ComplexVector
    /// </summary>
    /// <param name="dbs">Double array</param>
    public static implicit operator ComplexVector(double[] dbs)
    {
        return new ComplexVector(dbs);
    }
    /// <summary>
    /// Implicit cast Vector -> ComplexVector
    /// </summary>
    /// <param name="dbs"></param>
    public static implicit operator ComplexVector(Vector dbs)
    {
        return new ComplexVector(dbs);
    }
    #endregion
}

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

/// <summary>
/// Класс, представляющий матрицы и операции с ними
/// </summary>
[Serializable]
[DebuggerDisplay("Height = {Height}, Width = {Width}")]
public partial class Matrix : IAlgebraicStructure<double>, IEquatable<Matrix>, ISavable, ITextSavable, IByteConvertable
{
    #region Поля и свойства
    /// <summary>
    /// Данные(компоненты) матрицы
    /// </summary>
    public double[] Data { get; set; }
    /// <summary>
    /// Тип матрицы
    /// </summary>
    public MatrixType DataType { get; set; }
    /// <summary>
    /// Высота
    /// </summary>
    public int Height => Shape[1];
    /// <summary>
    /// Ширина
    /// </summary>
    public int Width => Shape[0];
    /// <summary>
    /// Форма матрицы
    /// </summary>
    public Shape Shape { get; } = new Shape2D(3, 3);
    /// <summary>
    /// Выдает элемент по индексу
    /// </summary>
    /// <param name="i">Индекс высоты</param>
    /// <param name="j">Индекс ширины</param>
    public double this[int i, int j]
    {
        get => Get(i, j);
        set => Set(i, j, value);
    }
    /// <summary>
    /// Выдает элемент по индексу
    /// </summary>
    /// <param name="i">Индекс</param>
    public double this[int i]
    {
        get => Data[i];
        set => Data[i] = value;
    }
    /// <summary>
    /// Определитель матрицы
    /// </summary>
    /// <returns></returns>
    public double Determinant
    {
        get
        {
            if (!IsSquared)
            {
                throw new InvalidOperationException("Матрица не является квадратной");
            }

            double result = 1.0;

            if (IsZero)
            {
                return 0;
            }

            if (IsTriangle || IsDiagonal)
            {
                for (int i = 0; i < Height; i++)
                {
                    result *= this[i, i];
                }

                return result;
            }

            Matrix matrix = ToTriangularMatr();

            for (int i = 0; i < Height; i++)
            {
                result *= matrix[i, i];
            }

            return result;
        }
    }
    /// <summary>
    /// Все ли элементы матрицы равны нулю
    /// </summary>
    public bool IsZero => Data.All(el => el == 0);
    /// <summary>
    /// Квадратная ли матрица
    /// </summary>
    public bool IsSquared => Height == Width;
    /// <summary>
    /// Является ли матрица диагональной
    /// </summary>
    public bool IsDiagonal
    {
        get
        {
            if (!IsSquared)
            {
                return false;
            }

            double eU = 0, eD = 0, all = 0;

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    all += Math.Abs(this[i, j]);

                    if (i == j)
                    {
                        eD += Math.Abs(this[i, j]);
                    }
                }
            }

            for (int i = 0; i < Height; i++)
            {
                for (int j = i; j < Width; j++)
                {
                    eU += Math.Abs(this[i, j]);
                }
            }

            double allND = all - eD;

            return eD > (allND * 1000);
        }
    }
    /// <summary>
    /// Является ли матрица треугольной
    /// </summary>
    public bool IsTriangle
    {
        get
        {
            if (!IsSquared)
            {
                return false;
            }

            double eU = 0, eD = 0, all = 0;

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    all += Math.Abs(this[i, j]);

                    if (i == j)
                    {
                        eD += Math.Abs(this[i, j]);
                    }
                }
            }

            for (int i = 0; i < Height; i++)
            {
                for (int j = i; j < Width; j++)
                {
                    eU += Math.Abs(this[i, j]);
                }
            }

            double allND = all - eD, deL = all - eU, deU = eU - eD;

            return Math.Abs(deU - deL) > 0.999 * allND;
        }
    }
    #endregion

    #region Конструкторы
    /// <summary>
    /// Создает матрицу со всеми нулями размерности 3х3
    /// </summary>
    public Matrix()
    {
        DataType = MatrixType.MatStruct;
        Data = new double[Shape.Count];
    }
    /// <summary>
    /// Создает матрицу на основе двумерного массива
    /// </summary>
    public Matrix(double[,] matr)
    {
        DataType = MatrixType.MatStruct;
        Shape = new Shape2D(matr.GetLength(0), matr.GetLength(1));
        Data = new double[Shape.Count];

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                Data[GetIndex(i, j)] = matr[i, j];
            }
        }
    }
    /// <summary>
    /// Создает матрицу заданной формы инициализированную нулями 
    /// </summary>
    /// <param name="shape"></param>
    public Matrix(Shape shape)
    {
        if (shape.Rank > 2)
        {
            throw new ArgumentException("Rank of the given shape if greater than 2", nameof(shape));
        }

        DataType = MatrixType.MatStruct;

        switch (shape.Rank)
        {
            case 1:
                Shape = new Shape2D(1, shape[0]);
                break;
            case 2:
                Shape = new Shape2D(shape[1], shape[0]);
                break;
        }

        Data = new double[Shape.Count];
    }
    /// <summary>
    /// Создает матрицу со всеми нулями размерности MxN
    /// </summary>
    public Matrix(int height, int width) : this(new Shape2D(height, width)) { }
    #endregion

    #region Операторы 
    /// <summary>
    /// Поэлементная сумма
    /// </summary>
    /// <param name="A"></param>
    /// <param name="B"></param>
    /// <returns></returns>
    public static Matrix operator +(Matrix A, Matrix B)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        if (A.Shape != B.Shape)
        {
            throw new InvalidOperationException("Matrices dimensions don't match");
        }

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] + B.Data[i];
        }

        return C;
    }
    /// <summary>
    /// Поэлементная разность
    /// </summary>
    /// <param name="A"></param>
    /// <param name="B"></param>
    /// <returns></returns>
    public static Matrix operator -(Matrix A, Matrix B)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        if (A.Shape != B.Shape)
        {
            throw new InvalidOperationException("Matrices dimensions don't match");
        }

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] - B.Data[i];
        }

        return C;
    }
    /// <summary>
    /// Addition 
    /// </summary>
    /// <param name="A"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static Matrix operator +(Matrix A, double k)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] + k;
        }

        return C;
    }
    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="k"></param>
    /// <param name="A"></param>
    /// <returns></returns>
    public static Matrix operator +(double k, Matrix A)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] + k;
        }

        return C;
    }
    /// <summary>
    /// вычитание
    /// </summary>
    /// <param name="A"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static Matrix operator -(Matrix A, double k)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] - k;
        }

        return C;
    }
    /// <summary>
    /// Вычитание
    /// </summary>
    /// <param name="k"></param>
    /// <param name="A"></param>
    /// <returns></returns>
    public static Matrix operator -(double k, Matrix A)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = k - A.Data[i];
        }

        return C;
    }

    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="A"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static Matrix operator *(Matrix A, double k)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] * k;
        }

        return C;
    }
    /// <summary>
    /// Деление
    /// </summary>
    public static Matrix operator /(Matrix A, double k)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = A.Data[i] / k;
        }

        return C;
    }

    /// <summary>
    /// Деление
    /// </summary>
    public static Matrix operator /(double k, Matrix A)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = k / A.Data[i];
        }

        return C;
    }
    /// <summary>
    /// Умножение
    /// </summary>
    public static Matrix operator *(double k, Matrix A)
    {
        Matrix C = new Matrix(A.Height, A.Width);
        int len = C.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            C.Data[i] = k * A.Data[i];
        }

        return C;
    }
    /// <summary>
    /// Умножение вектора на матрицу
    /// </summary>
    /// <param name="A"></param>
    /// <param name="B"></param>
    /// <returns></returns>
    public static Matrix operator *(Matrix A, Vector B)
    {
        return A * B.ToMatrix();
    }
    /// <summary>
    /// Умножение вектора на матрицу
    /// </summary>
    /// <param name="B"></param>
    /// <param name="A"></param>
    /// <returns></returns>
    public static Vector operator *(Vector B, Matrix A)
    {
        return (B.ToMatrix() * A).LikeVector();
    }

    /// <summary>
    /// Матричное умножение
    /// </summary>
    public static Matrix operator *(Matrix A, Matrix B)
    {
        Matrix C = new Matrix(A.Height, B.Width);

        int n = A.Width;

        if (!(A.Width == B.Height))
        {
            throw new InvalidOperationException("Can't multiply given matrices");
        }

        for (int i = 0; i < A.Height; i++)
        {
            for (int j = 0; j < B.Width; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    C[i, j] += A[i, k] * B[k, j];
                }
            }
        }

        return C;
    }

    /// <summary>
    /// Проверка равенства
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(Matrix left, Matrix right)
    {
        bool lNull = Equals(left, null);
        bool rNull = Equals(right, null);

        if (lNull && rNull)
        {
            return true;
        }
        else if ((lNull && !rNull) || (!lNull && rNull))
        {
            return false;
        }

        return left!.Shape == right!.Shape && left.Data.ElementWiseEqual(right.Data);
    }

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator !=(Matrix left, Matrix right)
    {
        bool lNull = Equals(left, null);
        bool rNull = Equals(right, null);

        if (lNull && rNull)
        {
            return false;
        }
        else if ((lNull && !rNull) || (!lNull && rNull))
        {
            return true;
        }

        return left!.Shape != right!.Shape || !left.Data.ElementWiseEqual(right.Data);
    }
    #endregion

}

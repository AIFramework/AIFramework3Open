using AI.DataStructs.Algebraic;
using AI.DataStructs.Shapes;
using AI.DSP.DSPCore;
using AI.Extensions;
using System;
using System.IO;
using Complex = System.Numerics.Complex;
using System.Runtime.CompilerServices;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DataStructs.WithComplexElements;

/// <summary>
/// Матрица с комплексными числами
/// </summary>
[Serializable]
public partial class ComplexMatrix : IComplexStructure, ISavable, IByteConvertable
{
    #region Поля и свойства
    /// <summary>
    /// Matrix data
    /// </summary>
    public Complex[] Data { get; }
    /// <summary>
    /// Matrix height
    /// </summary>
    public int Height => Shape[1];
    /// <summary>
    /// Matrix width
    /// </summary>
    public int Width => Shape[0];
    /// <summary>
    /// Matrix shape
    /// </summary>
    public Shape Shape { get; } = new Shape2D(3, 3);
    /// <summary>
    /// Get element by indexes
    /// </summary>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public Complex this[int i, int j]
    {
        get => Get(i, j);
        set => Set(i, j, value);
    }
    /// <summary>
    /// Real parts of all matrix components represented as algebraic matrix
    /// </summary>
    public Matrix RealMatrix
    {
        get
        {
            Matrix ret = new Matrix(Height, Width);

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    ret[i, j] = this[i, j].Real;
                }
            }

            return ret;
        }
    }
    /// <summary>
    /// Imaginary parts of all matrix components represented as algebraic matrix
    /// </summary>
    public Matrix ImaginaryMatrix
    {
        get
        {
            Matrix ret = new Matrix(Height, Width);

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    ret[i, j] = this[i, j].Imaginary;
                }
            }

            return ret;
        }
    }
    /// <summary>
    /// Magnitude parts of all matrix components represented as algebraic matrix
    /// </summary>
    public Matrix MagnitudeMatrix
    {
        get
        {
            Matrix ret = new Matrix(Height, Width);

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    ret[i, j] = this[i, j].Magnitude;
                }
            }

            return ret;
        }
    }
    /// <summary>
    /// Phase parts of all matrix components represented as algebraic matrix
    /// </summary>
    public Matrix PhaseMatrix
    {
        get
        {
            Matrix ret = new Matrix(Height, Width);

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    ret[i, j] = this[i, j].Phase;
                }
            }

            return ret;
        }
    }
    #endregion

    #region Конструкторы
    /// <summary>
    /// Creates matrix of 3x3 size
    /// </summary>
    public ComplexMatrix()
    {
        Data = new Complex[Shape.Count];
    }
    /// <summary>
    /// Creates matrix of the given size
    /// </summary>
    /// <param name="height">Matrix Высота</param>
    /// <param name="width">Matrix Ширина</param>
    public ComplexMatrix(int height, int width)
    {
        Shape = new Shape2D(height, width);
        Data = new Complex[Shape.Count];
    }
    /// <summary>
    /// Creates matrix with real and imaginary element's parts represented as algebraic matrices
    /// </summary>
    /// <param name="real">Real element's part</param>
    /// <param name="imaginary">Imaginary element's part</param>
    public ComplexMatrix(Matrix real, Matrix imaginary)
    {
        if (real == null)
        {
            throw new ArgumentNullException(nameof(real));
        }

        if (imaginary == null)
        {
            throw new ArgumentNullException(nameof(imaginary));
        }

        if (real.Shape != imaginary.Shape)
        {
            throw new InvalidOperationException("Matrices dimensions don't match");
        }

        Shape = new Shape2D(real.Height, real.Width);
        Data = new Complex[Shape.Count];

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                this[i, j] = new Complex(real[i, j], imaginary[i, j]);
            }
        }
    }
    /// <summary>
    /// Creates matrix with real element's parts represented as algebraic matrix and zero imaginary parts
    /// </summary>
    /// <param name="real">Реальная часть</param>
    public ComplexMatrix(Matrix real)
    {
        if (real == null)
        {
            throw new ArgumentNullException(nameof(real));
        }

        Shape = new Shape2D(real.Height, real.Width);
        Data = new Complex[Shape.Count];

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                this[i, j] = new Complex(real[i, j], 0);
            }
        }
    }
    #endregion

    #region Операторы
    /// <summary>
    /// Перемножение матриц
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static ComplexMatrix operator *(ComplexMatrix left, ComplexMatrix right)
    {
        ComplexMatrix ret = new ComplexMatrix(left.Height, right.Width);

        if (left.Width != right.Height)
        {
            throw new InvalidOperationException("Can't multiply given matrices");
        }

        for (int i = 0; i < left.Height; i++)
        {
            for (int j = 0; j < right.Width; j++)
            {
                for (int k = 0; k < left.Width; k++)
                {
                    ret[i, j] += left[i, k] * right[k, j];
                }
            }
        }

        return ret;
    }
    #endregion

    #region Приватные методы
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(int i, int j)
    {
        return (Width * i) + j;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Complex Get(int i, int j)
    {
        return Data[GetIndex(i, j)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Set(int i, int j, Complex value)
    {
        Data[GetIndex(i, j)] = value;
    }
    #endregion
}

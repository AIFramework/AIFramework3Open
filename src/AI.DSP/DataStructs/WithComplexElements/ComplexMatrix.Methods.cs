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

public partial class ComplexMatrix
{
    #region Методы
    /// <summary>
    /// Поэлементное преобразование матриц
    /// </summary>
    /// <param name="func">Функция преобразования</param>
    /// <returns></returns>
    public ComplexMatrix Transform(Func<Complex, Complex> func)
    {
        ComplexMatrix matrix = new ComplexMatrix(Height, Width);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                matrix[i, j] = func(this[i, j]);
            }
        }

        return matrix;
    }
    /// <summary>
    /// Матрица изменяет саму себя
    /// </summary>
    /// <param name="func">Функция преобразования</param>
    public void TransformSelf(Func<Complex, Complex> func)
    {
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                this[i, j] = func(this[i, j]);
            }
        }
    }
    /// <summary>
    /// Двумерное преобразование Фурье
    /// </summary>
    /// <param name="input">Вход</param>
    public static ComplexMatrix MatrixFFT(Matrix input)
    {
        ComplexMatrix matrix;
        Vector[] vs = Matrix.GetColumns(input);
        ComplexVector[] complexVector = new ComplexVector[vs.Length];
        FFT furie = new FFT(vs[0].Count);

        for (int i = 0; i < vs.Length; i++)
        {
            complexVector[i] = furie.CalcFFT(vs[i]);
        }

        matrix = new ComplexMatrix(complexVector.Length, complexVector[0].Count);

        for (int i = 0; i < vs.Length; i++)
        {
            for (int j = 0; j < vs[0].Count; j++)
            {
                matrix[i, j] = complexVector[i][j];
            }
        }

        complexVector = new ComplexVector[matrix.Width];

        for (int i = 0; i < matrix.Width; i++)
        {
            complexVector[i] = new ComplexVector(matrix.Height);

            for (int j = 0; j < matrix.Height; j++)
            {
                complexVector[i][j] = matrix[j, i];
            }
        }

        for (int i = 0; i < complexVector.Length; i++)
        {
            complexVector[i] = FFT.CalcFFT(complexVector[i]);
        }

        for (int i = 0; i < matrix.Width; i++)
        {
            for (int j = 0; j < matrix.Height; j++)
            {
                matrix[j, i] = complexVector[i][j];
            }
        }

        return matrix;
    }
    /// <summary>
    /// Обратное двумерное преобразование Фурье
    /// </summary>
    /// <param name="input">Входная матрица</param>
    /// <returns></returns>
    public static ComplexMatrix MatrixIFFT(ComplexMatrix input)
    {
        ComplexMatrix matrix;
        ComplexVector[] vs = GetColumns(input.ConjugateMatr());
        ComplexVector[] complexVector = new ComplexVector[vs.Length];

        for (int i = 0; i < vs.Length; i++)
        {
            complexVector[i] = FFT.CalcFFT(vs[i]);
        }

        matrix = new ComplexMatrix(complexVector.Length, complexVector[0].Count);

        for (int i = 0; i < vs.Length; i++)
        {
            for (int j = 0; j < vs[0].Count; j++)
            {
                matrix[i, j] = complexVector[i][j];
            }
        }

        complexVector = new ComplexVector[matrix.Width];

        for (int i = 0; i < matrix.Width; i++)
        {
            complexVector[i] = new ComplexVector(matrix.Height);

            for (int j = 0; j < matrix.Height; j++)
            {
                complexVector[i][j] = matrix[j, i];
            }
        }

        for (int i = 0; i < complexVector.Length; i++)
        {
            complexVector[i] = FFT.CalcFFT(complexVector[i]);
        }

        for (int i = 0; i < matrix.Width; i++)
        {
            for (int j = 0; j < matrix.Height; j++)
            {
                matrix[j, i] = complexVector[i][j];
            }
        }

        int totalSize = matrix.Height * matrix.Width;
        for (int i = 0; i < matrix.Height; i++)
            for (int j = 0; j < matrix.Width; j++)
                matrix[i, j] /= totalSize;

        return matrix;
    }
    /// <summary>
    /// Двумерное преобразование Фурье
    /// </summary>
    /// <param name="input">Вход</param>
    public static ComplexMatrix MatrixFFT(ComplexMatrix input)
    {
        ComplexMatrix matrix;
        ComplexVector[] vs = ComplexMatrix.GetColumns(input);
        ComplexVector[] complexVector = new ComplexVector[vs.Length];

        for (int i = 0; i < vs.Length; i++)
        {
            complexVector[i] = FFT.CalcFFT(vs[i]);
        }

        matrix = new ComplexMatrix(complexVector.Length, complexVector[0].Count);

        for (int i = 0; i < vs.Length; i++)
        {
            for (int j = 0; j < vs[0].Count; j++)
            {
                matrix[i, j] = complexVector[i][j];
            }
        }

        complexVector = new ComplexVector[matrix.Width];

        for (int i = 0; i < matrix.Width; i++)
        {
            complexVector[i] = new ComplexVector(matrix.Height);

            for (int j = 0; j < matrix.Height; j++)
            {
                complexVector[i][j] = matrix[j, i];
            }
        }

        for (int i = 0; i < complexVector.Length; i++)
        {
            complexVector[i] = FFT.CalcFFT(complexVector[i]);
        }

        for (int i = 0; i < matrix.Width; i++)
        {
            for (int j = 0; j < matrix.Height; j++)
            {
                matrix[j, i] = complexVector[i][j];
            }
        }

        return matrix;
    }
    /// <summary>
    /// Разложение матрицы на столбцы
    /// </summary>
    /// <param name="matr">Матрица</param>
    /// <returns>Массив векторов</returns>
    public static ComplexVector[] GetColumns(ComplexMatrix matr)
    {
        ComplexVector[] columns = new ComplexVector[matr.Width];

        for (int i = 0; i < columns.Length; i++)
        {
            columns[i] = new ComplexVector(matr.Height);
            for (int j = 0; j < matr.Height; j++)
            {
                columns[i][j] = matr[j, i];
            }
        }

        return columns;
    }
    /// <summary>
    /// Сопряженная матрица
    /// </summary>
    /// <returns></returns>
    public ComplexMatrix ConjugateMatr()
    {
        ComplexMatrix cm = new ComplexMatrix(Height, Width);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                cm[i, j] = Complex.Conjugate(this[i, j]);
            }
        }

        return cm;
    }
    /// <summary>
    /// Адамарово произведение матриц (поэлементное)
    /// </summary>
    /// <param name="complexMatrix">Матрица на которую происходит умножение</param>
    public ComplexMatrix AdamarProduct(ComplexMatrix complexMatrix)
    {
        ComplexMatrix cm = new ComplexMatrix(Height, Width);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                cm[i, j] = this[i, j] * complexMatrix[i, j];
            }
        }

        return cm;
    }
    /// <summary>
    /// Адамарово произведение матриц (поэлементное)
    /// </summary>
    /// <param name="matrix">Матрица на которую происходит умножение</param>
    public ComplexMatrix AdamarProduct(Matrix matrix)
    {
        ComplexMatrix cm = new ComplexMatrix(Height, Width);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                cm[i, j] = this[i, j] * matrix[i, j];
            }
        }

        return cm;
    }
    #endregion
}

using AI.DataStructs.Algebraic;
using System;

namespace AI.HighLevelFunctions;

public static partial class FunctionsForEachElements
{
    #region Экспонента / Логарифм / Гиперболические (Vector)
    /// <summary>
    /// Дсятичный логарифм
    /// </summary>
    /// <param name="Inp">Подлогарифмическое число</param>
    public static Vector Log10(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Log10(Inp[i]);

        return A;
    }
    /// <summary>
    /// Логарифм по основанию "e"
    /// </summary>
    /// <param name="Inp">Подлогарифмическое число</param>
    public static Vector Ln(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Log(Inp[i]);

        return A;
    }
    /// <summary>
    /// Экспонента e^x
    /// </summary>
    /// <param name="Inp">показатели степени</param>
    /// <returns>e^Inp - поэлементно</returns>
    public static Vector Exp(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Exp(Inp[i]);

        return A;
    }
    /// <summary>
    /// Гиперболический тангенс
    /// </summary>
    /// <param name="Inp">углы</param>
    public static Vector Tanh(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
        {
            A[i] = Math.Tanh(Inp[i]);
        }

        return A;
    }
    /// <summary>
    /// Квадратный корень
    /// </summary>
    /// <param name="Inp">числа</param>		
    public static Vector Sqrt(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Sqrt(Inp[i]);

        return A;
    }
    #endregion

    #region Экспонента / Логарифм / Гиперболические (Matrix)
    /// <summary>
    /// e^x
    /// </summary>
    /// <param name="Inp">Матрица значений для преобразования</param>	
    public static Matrix Exp(Matrix Inp)
    {
        Matrix A = new Matrix(Inp.Height, Inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Exp(Inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Гиперболический тангенс
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Tanh(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Tanh(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Квадратный корень
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Sqrt(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Sqrt(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Десятичный логарифм
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Log10(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Log10(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Логарифм по основанию E
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Ln(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Log(inp.Data[i]);

        return A;
    }
    #endregion
}

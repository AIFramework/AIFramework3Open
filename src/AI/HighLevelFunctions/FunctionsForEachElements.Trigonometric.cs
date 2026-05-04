using AI.DataStructs.Algebraic;
using System;

namespace AI.HighLevelFunctions;

public static partial class FunctionsForEachElements
{
    #region Тригонометрия (Vector)
    /// <summary>
    /// Вычисление синусов
    /// </summary>
    /// <param name="Inp">Вектор углов (в радианах)</param>
    /// <returns>Вектор синусов</returns>
    public static Vector Sin(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Sin(Inp[i]);

        return A;
    }
    /// <summary>
    /// Вычисление косинусов
    /// </summary>
    /// <param name="Inp">Вектор углов (в радианах)</param>
    /// <returns>Вектор косинусов</returns>
    public static Vector Cos(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Cos(Inp[i]);

        return A;
    }
    /// <summary>
    /// Calculating tangents
    /// </summary>
    /// <param name="Inp">Вектор углов (в радианах)</param>
    /// <returns>Вектор тангенсов</returns>
    public static Vector Tan(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Tan(Inp[i]);

        return A;
    }
    /// <summary>
    /// Вычисление котангенсов
    /// </summary>
    /// <param name="Inp">Вектор углов (в радианах)</param>
    /// <returns>Вектор котангенсов</returns>
    public static Vector ctg(Vector Inp)
    {
        return 1.0 / Tan(Inp);
    }
    /// <summary>
    /// Вычисление арксинусов
    /// </summary>
    /// <param name="Inp">Вектор синусов</param>
    /// <returns>Вектор углов (в радианах)</returns>
    public static Vector Asin(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Asin(Inp[i]);

        return A;
    }
    /// <summary>
    /// Вычисление арккосинусов
    /// </summary>
    /// <param name="Inp">Вектор косинусов</param>
    /// <returns>Вектор углов (в радианах)</returns>
    public static Vector Acos(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Acos(Inp[i]);

        return A;
    }
    /// <summary>
    /// Вычисление арктангенсов
    /// </summary>
    /// <param name="Inp">Вектор тангенсов</param>
    /// <returns>Вектор углов (в радианах)</returns>
    public static Vector Atan(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Atan(Inp[i]);

        return A;
    }
    /// <summary>
    /// Секанс угла
    /// </summary>
    /// <param name="Inp">углы</param>
    public static Vector Sec(Vector Inp)
    {
        return 1 / Cos(Inp);
    }
    /// <summary>
    /// Косеканс угла
    /// </summary>
    /// <param name="Inp">углы</param>
    public static Vector Cosec(Vector Inp)
    {
        return 1 / Sin(Inp);
    }
    #endregion

    #region Тригонометрия (Matrix)
    /// <summary>
    /// Вычисление синуса
    /// </summary>
    /// <param name="Inp">Матрица значений для преобразования</param>	
    public static Matrix Sin(Matrix Inp)
    {
        Matrix A = new Matrix(Inp.Height, Inp.Width);
        for (int i = 0; i < Inp.Height; i++)
            for (int j = 0; j < Inp.Width; j++)
                A[i, j] = Math.Sin(Inp[i, j]);

        return A;
    }
    /// <summary>
    /// Косинус
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Cos(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Cos(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Тангенс
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Tan(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Tan(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Котангенс
    /// </summary>
    /// <param name="Inp">Матрица значений для преобразования</param>	
    public static Matrix Ctan(Matrix Inp)
    {
        return 1.0 / Tan(Inp);
    }
    /// <summary>
    /// Арксинус
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Asin(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Asin(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Арккосинус
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Acos(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Acos(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Арктангенс
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Atan(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Atan(inp.Data[i]);

        return A;
    }
    /// <summary>
    /// Секонс
    /// </summary>
    /// <param name="Inp">Матрица значений для преобразования</param>	
    public static Matrix Sec(Matrix Inp)
    {
        return 1.0 / Cos(Inp);
    }
    /// <summary>
    /// Косеконс
    /// </summary>
    /// <param name="Inp">Матрица значений для преобразования</param>	
    public static Matrix Cosec(Matrix Inp)
    {
        return 1.0 / Sin(Inp);
    }
    #endregion
}

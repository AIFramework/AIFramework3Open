using AI.DataStructs.Algebraic;
using System;

namespace AI.HighLevelFunctions;

public static partial class FunctionsForEachElements
{
    #region Утилиты (Vector)
    /// <summary>
    /// Окугление
    /// </summary>
    /// <param name="Inp">Вектор входных данных</param>
    /// <param name="digits">до какого знака</param>
    /// <returns>Вектор выхода</returns>
    public static Vector Round(Vector Inp, int digits)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Round(Inp[i], digits);

        return A;
    }
    /// <summary>
    /// Определение знака
    /// </summary>
    /// <param name="Inp">Входной вектор</param>
    /// <returns></returns>
    public static Vector Sign(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Sign(Inp[i]);

        return A;
    }
    /// <summary>
    /// Модуль
    /// </summary>
    /// <param name="Inp">Комплексный вектор значений для преобразования</param>
    public static Vector Abs(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = Math.Abs(Inp[i]);

        return A;
    }
    #endregion

    #region Утилиты (Matrix)
    /// <summary>
    /// Модуль
    /// </summary>
    /// <param name="inp">Матрица значений для преобразования</param>	
    public static Matrix Abs(Matrix inp)
    {
        Matrix A = new Matrix(inp.Height, inp.Width);
        int len = A.Shape.Count;
        for (int i = 0; i < len; i++)
            A.Data[i] = Math.Abs(inp.Data[i]);

        return A;
    }
    #endregion
}

using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Разложение Холецкого: прежнее имя, считает <see cref="AI.HighLevelFunctions.CholeskyDecomposition"/>.
/// </summary>
[Serializable]
[Obsolete("Используйте AI.HighLevelFunctions.CholeskyDecomposition — единственное разложение Холецкого.")]
public static class Cholesky
{
    /// <summary>Нижнетреугольный множитель L, такой что A = L·Lᵀ.</summary>
    /// <param name="A">Симметричная положительно определённая матрица</param>
    /// <exception cref="InvalidOperationException">Если матрица не является положительно определённой</exception>
    public static Matrix Decompose(Matrix A) => AI.HighLevelFunctions.CholeskyDecomposition.Decompose(A);

    /// <summary>Решает систему Ax = b через разложение Холецкого.</summary>
    public static Vector Solve(Matrix A, Vector b) => AI.HighLevelFunctions.CholeskyDecomposition.Solve(A, b);
}

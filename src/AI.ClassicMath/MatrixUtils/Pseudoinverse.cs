using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Псевдообратная матрица Мура–Пенроуза через SVD.
/// </summary>
[Serializable]
public static class Pseudoinverse
{
    /// <summary>
    /// Вычисляет псевдообратную матрицу A⁺ = V·Σ⁺·Uᵀ,
    /// где Σ⁺ содержит 1/σᵢ для σᵢ > tolerance, иначе 0.
    /// </summary>
    /// <param name="A">Матрица произвольного размера</param>
    /// <param name="tolerance">Порог отсечки малых сингулярных значений</param>
    public static Matrix Compute(Matrix A, double tolerance = 1e-10)
    {
        var (U, sigma, V) = Svd.Decompose(A);

        int n = A.Width;

        // Разложение усечённое: U имеет размер m×n, V - n×n, сингулярных чисел n.
        // Поэтому Σ⁺ здесь квадратная n×n, а размер m×n даёт уже произведение с Uᵀ:
        // A⁺ = V·Σ⁺·Uᵀ = (n×n)·(n×n)·(n×m).
        Matrix sigmaPlus = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            if (sigma[i] > tolerance)
                sigmaPlus[i, i] = 1.0 / sigma[i];
        }

        return V * sigmaPlus * U.Transpose();
    }
}

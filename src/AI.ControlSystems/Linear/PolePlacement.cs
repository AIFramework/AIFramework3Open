using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Размещение полюсов для SISO: u = −K x по формуле Аккермана (достижимая пара A, B).
/// Монический полином: λ^n + c_{n−1} λ^{n−1} + … + c₀;
/// коэффициенты задаются как [c₀, c₁, …, c_{n−1}] (от младшего к старшему при степенях λ).
/// </summary>
public static class PolePlacement
{
    /// <summary>Матрица управляемости [B | AB | … | A^{n−1} B].</summary>
    public static Matrix ControllabilityMatrix(Matrix a, Matrix b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException();
        int n = a.Height;
        if (!a.IsSquared || b.Height != n || b.Width != 1)
            throw new ArgumentException("Ожидается SISO: B — столбец n×1.");

        Matrix w = new Matrix(n, n);
        Matrix ap = ControlLinAlg.Eye(n);
        for (int i = 0; i < n; i++)
        {
            Vector col = ControlLinAlg.MatVec(ap, b);
            for (int r = 0; r < n; r++)
                w[r, i] = col[r];
            ap = ap * a;
        }
        return w;
    }

    /// <summary>Строка усилений K размером 1×n (u = −K x).</summary>
    public static Matrix AckermannGain(Matrix a, Matrix b, Vector monicPolynomialCoeffsLowToHigh)
    {
        if (monicPolynomialCoeffsLowToHigh == null)
            throw new ArgumentNullException(nameof(monicPolynomialCoeffsLowToHigh));
        int n = a.Height;
        if (monicPolynomialCoeffsLowToHigh.Count != n)
            throw new ArgumentException("Ожидается " + n + " коэффициентов полинома (c0 … c_{n-1}).");

        Matrix w = ControllabilityMatrix(a, b);
        Matrix wInv = w.GetInvertMatrix();

        // φ(A) = A^n + c_{n-1} A^{n-1} + … + c₀ I
        Matrix phi = MatrixPow(a, n);
        Matrix aPow = ControlLinAlg.Eye(n);
        for (int i = 0; i < n; i++)
        {
            phi = phi + aPow * monicPolynomialCoeffsLowToHigh[i];
            aPow = aPow * a;
        }

        Matrix t = wInv * phi;
        Matrix k = new Matrix(1, n);
        for (int j = 0; j < n; j++)
            k[0, j] = t[n - 1, j];
        return k;
    }

    private static Matrix MatrixPow(Matrix a, int p)
    {
        Matrix r = ControlLinAlg.Eye(a.Height);
        for (int i = 0; i < p; i++)
            r = r * a;
        return r;
    }
}

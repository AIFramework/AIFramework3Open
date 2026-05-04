using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Identification;

/// <summary>
/// Рекурсивные МНК для скалярного выхода y ≈ φᵀ θ. Забывание: λ ∈ (0, 1].
/// </summary>
[Serializable]
public sealed class RecursiveLeastSquares
{
    private Matrix _p;

    /// <summary>Оценка параметров θ.</summary>
    public Vector Theta { get; private set; }

    /// <summary>Коэффициент забывания λ (1 — без забывания).</summary>
    public double ForgettingFactor { get; set; } = 1.0;

    public RecursiveLeastSquares(Vector initialTheta, Matrix initialCovariance)
    {
        if (initialTheta == null || initialCovariance == null)
            throw new ArgumentNullException();
        int n = initialTheta.Count;
        if (!initialCovariance.IsSquared || initialCovariance.Height != n)
            throw new ArgumentException("P₀ должна быть n×n.");
        Theta = initialTheta;
        _p = initialCovariance;
    }

    /// <summary>Один шаг по паре (y, φ).</summary>
    public void Update(Vector phi, double y)
    {
        if (phi == null)
            throw new ArgumentNullException(nameof(phi));
        int n = Theta.Count;
        if (phi.Count != n)
            throw new ArgumentException("Размерность φ должна совпадать с θ.");

        double lam = ForgettingFactor;
        if (lam <= 0 || lam > 1)
            throw new InvalidOperationException("ForgettingFactor должен быть в (0, 1].");

        double yPred = 0;
        for (int i = 0; i < n; i++)
            yPred += phi[i] * Theta[i];

        double err = y - yPred;

        Vector pPhi = ControlLinAlg.MatVec(_p, phi);
        double phiPphi = 0;
        for (int i = 0; i < n; i++)
            phiPphi += phi[i] * pPhi[i];

        double denom = lam + phiPphi;
        if (Math.Abs(denom) < 1e-30)
            return;

        Vector k = pPhi * (1.0 / denom);
        for (int i = 0; i < n; i++)
            Theta[i] += k[i] * err;

        // P := (P - K φᵀ P) / λ, где K = k (столбец)
        Vector phiTP = new Vector(n);
        for (int j = 0; j < n; j++)
        {
            double s = 0;
            for (int m = 0; m < n; m++)
                s += phi[m] * _p[m, j];
            phiTP[j] = s;
        }

        Matrix pNew = new Matrix(n, n);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                pNew[i, j] = (_p[i, j] - k[i] * phiTP[j]) / lam;
        }

        _p = pNew;
    }
}

using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Переход от непрерывной модели ẋ = Ac x + Bc u к дискретной ZOH с шагом dt:
/// x[k+1] = Ad x[k] + Bd u[k], u кусочно-постоянен на [k dt, (k+1)dt).
/// </summary>
public static class Discretization
{
    /// <summary>
    /// Ad = exp(Ac·dt), Bd = ∫₀^dt exp(Ac·τ) dτ · Bc.
    /// </summary>
    public static void ZeroOrderHold(Matrix ac, Matrix bc, double dt, out Matrix ad, out Matrix bd)
    {
        if (ac == null || bc == null)
            throw new ArgumentNullException();
        if (!ac.IsSquared)
            throw new ArgumentException("Ac должна быть квадратной.");
        int n = ac.Height;
        if (bc.Height != n)
            throw new ArgumentException("Число строк Bc должно совпадать с порядком Ac.");
        if (dt <= 0)
            throw new ArgumentOutOfRangeException(nameof(dt));

        Matrix acDt = ac * dt;
        ad = ControlLinAlg.MatrixExp(acDt);
        Matrix integ = ControlLinAlg.IntegrateExpDt(ac, dt);
        bd = integ * bc;
    }

    /// <summary>Упаковка в один вызов: создаёт <see cref="DiscreteLtiModel"/> с D = 0.</summary>
    public static DiscreteLtiModel ZeroOrderHoldModel(Matrix ac, Matrix bc, Matrix cc, double dt, Vector x0)
    {
        ZeroOrderHold(ac, bc, dt, out Matrix ad, out Matrix bd);
        return new DiscreteLtiModel(ad, bd, cc, x0);
    }
}

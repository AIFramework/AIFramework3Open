using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Optimal;

/// <summary>
/// Линейный квадратичный MPC без ограничений: горизонт N, стадийные Q, R, терминальный Qf.
/// Оптимальное первое управление u₀ = −K₀ x, где K₀ вычисляется из обратного прохода Риккати
/// (S_N = Qf, затем N шагов к S₀).
/// </summary>
public static class LinearQuadraticMpc
{
    /// <summary>Первое усиление горизонта: u = −K x.</summary>
    public static Matrix ComputeFirstGain(Matrix a, Matrix b, Matrix q, Matrix r, Matrix qf, int horizon)
    {
        if (horizon < 1)
            throw new ArgumentOutOfRangeException(nameof(horizon));
        int n = a.Height;
        int m = b.Width;
        if (!a.IsSquared || b.Height != n || q.Height != n || q.Width != n || r.Height != m || r.Width != m
            || qf.Height != n || qf.Width != n)
            throw new ArgumentException("Несогласованные размеры.");

        Matrix s = qf;
        Matrix at = a.Transpose();
        Matrix bt = b.Transpose();
        Matrix k0 = null;

        for (int t = 0; t < horizon; t++)
        {
            Matrix btsb = bt * s * b + r;
            Matrix inv = btsb.GetInvertMatrix();
            Matrix k = inv * bt * s * a;
            if (t == horizon - 1)
                k0 = k;

            Matrix sNew = at * s * a - at * s * b * inv * bt * s * a + q;
            s = ControlLinAlg.Symmetrize(sNew);
        }

        return k0;
    }
}

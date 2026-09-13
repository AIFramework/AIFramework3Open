using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Переход от непрерывной модели ẋ = Ac x + Bc u к дискретной ZOH с шагом dt:
/// x[k+1] = Ad x[k] + Bd u[k], u кусочно-постоянен на [k dt, (k+1)dt).
/// </summary>
/// <remarks>
/// <para>
/// Ad и Bd получаются одной матричной экспонентой расширенной матрицы
/// <c>exp([[Ac, Bc], [0, 0]]·dt) = [[Ad, Bd], [0, I]]</c>. Прежде интеграл ∫₀^dt exp(Ac·τ)dτ считался
/// рядом Тейлора без масштабирования: при ‖Ac·dt‖ порядка десятков — жёсткая система или крупный
/// шаг — слагаемые ряда доходили до 10²⁰, взаимно сокращались, и Bd получалась неверной, а
/// восьмидесяти членов не хватало даже для сходимости.
/// </para>
/// <para>
/// Шум процесса дискретизируется методом Ван Лоана, тоже одной экспонентой: без этого фильтр
/// Калмана по непрерывной модели получает Q·dt, что верно лишь для очень малого шага.
/// </para>
/// </remarks>
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
        if (!(dt > 0) || double.IsInfinity(dt))
            throw new ArgumentOutOfRangeException(nameof(dt));

        int m = bc.Width;
        var augmented = new Matrix(n + m, n + m);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                augmented[i, j] = ac[i, j] * dt;
            for (int j = 0; j < m; j++)
                augmented[i, n + j] = bc[i, j] * dt;
        }

        Matrix exponential = ControlLinAlg.MatrixExp(augmented);
        ad = ControlLinAlg.Block(exponential, 0, 0, n, n);
        bd = ControlLinAlg.Block(exponential, 0, n, n, m);
    }

    /// <summary>Упаковка в один вызов: создаёт <see cref="DiscreteLtiModel"/> с D = 0.</summary>
    public static DiscreteLtiModel ZeroOrderHoldModel(Matrix ac, Matrix bc, Matrix cc, double dt, Vector x0)
    {
        ZeroOrderHold(ac, bc, dt, out Matrix ad, out Matrix bd);
        return new DiscreteLtiModel(ad, bd, cc, x0);
    }

    /// <summary>
    /// Ковариация дискретного шума процесса Qd = ∫₀^dt exp(Ac·τ)·Qc·exp(Acᵀ·τ) dτ методом Ван Лоана
    /// </summary>
    /// <param name="ac">Матрица непрерывной модели</param>
    /// <param name="qc">Спектральная плотность непрерывного шума (n×n)</param>
    /// <param name="dt">Шаг</param>
    /// <remarks>
    /// <c>exp([[−Ac, Qc], [0, Acᵀ]]·dt) = [[·, F₁₂], [0, F₂₂]]</c>, откуда Ad = F₂₂ᵀ и Qd = F₂₂ᵀ·F₁₂.
    /// </remarks>
    public static Matrix DiscretizeProcessNoise(Matrix ac, Matrix qc, double dt)
    {
        if (ac == null || qc == null)
            throw new ArgumentNullException();
        int n = ac.Height;
        if (!ac.IsSquared || qc.Height != n || qc.Width != n)
            throw new ArgumentException("Ac и Qc должны быть n×n.");
        if (!(dt > 0) || double.IsInfinity(dt))
            throw new ArgumentOutOfRangeException(nameof(dt));

        var augmented = new Matrix(2 * n, 2 * n);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                augmented[i, j] = -ac[i, j] * dt;
                augmented[i, n + j] = qc[i, j] * dt;
                augmented[n + i, n + j] = ac[j, i] * dt;
            }
        }

        Matrix exponential = ControlLinAlg.MatrixExp(augmented);
        Matrix f12 = ControlLinAlg.Block(exponential, 0, n, n, n);
        Matrix f22 = ControlLinAlg.Block(exponential, n, n, n, n);

        return ControlLinAlg.Symmetrize(f22.Transpose() * f12);
    }
}

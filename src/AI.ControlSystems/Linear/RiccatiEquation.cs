using System;
using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Дискретное алгебраическое уравнение Риккати (DARE):
/// X = AᵀXA − AᵀXB(R + BᵀXB)⁻¹BᵀXA + Q.
/// </summary>
/// <remarks>
/// <para>
/// Решается структурно-сохраняющим алгоритмом удвоения (Чу, Фань, Линь, 2005): каждый шаг
/// удваивает горизонт, и сходимость квадратичная — десяток итераций вместо сотен итераций
/// Риккати, которые к тому же сходятся медленно, когда замкнутая система близка к границе
/// устойчивости. Прежний LQR делал до 500 таких итераций и возвращал результат, даже если не сошёлся.
/// </para>
/// <para>
/// Решение проверяется дважды: невязка уравнения должна быть мала, а замкнутая система
/// <c>A − BK</c> — устойчива. Если пара (A, B) не стабилизируема или неустойчивая мода не видна
/// через Q, стабилизирующего решения нет, и вместо негодного усиления бросается исключение.
/// </para>
/// <para>
/// То же уравнение с (Aᵀ, Cᵀ) даёт установившуюся ковариацию фильтра Калмана.
/// </para>
/// </remarks>
public static class RiccatiEquation
{
    private const int MaxDoublings = 100;

    /// <summary>Стабилизирующее решение DARE</summary>
    /// <param name="a">A (n×n)</param>
    /// <param name="b">B (n×m)</param>
    /// <param name="q">Q (n×n), симметричная неотрицательно определённая</param>
    /// <param name="r">R (m×m), симметричная положительно определённая</param>
    /// <param name="tolerance">Допустимая относительная невязка</param>
    /// <exception cref="InvalidOperationException">Стабилизирующего решения нет</exception>
    public static Matrix SolveDiscrete(Matrix a, Matrix b, Matrix q, Matrix r, double tolerance = 1e-9)
    {
        if (a == null || b == null || q == null || r == null)
            throw new ArgumentNullException();

        int n = a.Height;
        int m = b.Width;

        if (!a.IsSquared || b.Height != n || q.Height != n || q.Width != n || r.Height != m || r.Width != m)
            throw new ArgumentException("Несогласованные размеры A, B, Q, R.");

        try
        {
            _ = Cholesky.Decompose(ControlLinAlg.Symmetrize(r));
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException("R должна быть положительно определённой: иначе управление ничего не стоит и задача вырождена.", nameof(r));
        }

        Matrix eye = ControlLinAlg.Eye(n);
        Matrix ak = a.Copy();
        Matrix gk = ControlLinAlg.Symmetrize(b * ControlLinAlg.Inverse(r) * b.Transpose());
        Matrix hk = ControlLinAlg.Symmetrize(q);
        bool converged = false;

        for (int iteration = 0; iteration < MaxDoublings; iteration++)
        {
            Matrix w = eye + (gk * hk);
            Matrix wInverse = ControlLinAlg.Inverse(w);
            Matrix aw = ak * wInverse;

            Matrix aNext = aw * ak;
            Matrix gNext = ControlLinAlg.Symmetrize(gk + (aw * gk * ak.Transpose()));
            Matrix hNext = ControlLinAlg.Symmetrize(hk + (ak.Transpose() * hk * wInverse * ak));

            if (!ControlLinAlg.IsFinite(hNext) || !ControlLinAlg.IsFinite(aNext))
                throw new InvalidOperationException("Уравнение Риккати не имеет конечного решения: пара (A, B) не стабилизируема.");

            double change = ControlLinAlg.Frobenius(hNext - hk);
            double scale = Math.Max(1, ControlLinAlg.Frobenius(hNext));

            ak = aNext;
            gk = gNext;
            hk = hNext;

            if (change <= 1e-14 * scale || ControlLinAlg.MaxAbs(ak) <= 1e-300)
            {
                converged = true;
                break;
            }
        }

        Matrix x = hk;
        double residual = ControlLinAlg.Frobenius(DiscreteResidual(a, b, q, r, x));

        if (!converged && residual > tolerance * Math.Max(1, ControlLinAlg.Frobenius(x)))
            throw new InvalidOperationException("Метод удвоения не сошёлся: уравнение Риккати не решено.");

        if (residual > tolerance * Math.Max(1, ControlLinAlg.Frobenius(x)))
            throw new InvalidOperationException($"Невязка уравнения Риккати {residual:G3} слишком велика: решение неточно.");

        Matrix gain = DiscreteGain(a, b, r, x);

        if (Eigen.SpectralRadius(a - (b * gain)) >= 1 - 1e-12)
            throw new InvalidOperationException(
                "Решение Риккати не стабилизирует систему: неустойчивая мода неуправляема или не видна через Q.");

        return x;
    }

    /// <summary>Невязка DARE: AᵀXA − AᵀXB(R + BᵀXB)⁻¹BᵀXA + Q − X</summary>
    public static Matrix DiscreteResidual(Matrix a, Matrix b, Matrix q, Matrix r, Matrix x)
    {
        Matrix at = a.Transpose();
        Matrix bt = b.Transpose();
        Matrix inner = ControlLinAlg.Inverse(r + (bt * x * b));

        return (at * x * a) - (at * x * b * inner * bt * x * a) + q - x;
    }

    /// <summary>Усиление K = (R + BᵀXB)⁻¹BᵀXA для закона u = −Kx</summary>
    public static Matrix DiscreteGain(Matrix a, Matrix b, Matrix r, Matrix x)
    {
        Matrix bt = b.Transpose();

        return ControlLinAlg.Inverse(r + (bt * x * b)) * bt * x * a;
    }
}

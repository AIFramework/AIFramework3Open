using System;
using System.Linq;
using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;
using Complex = System.Numerics.Complex;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Структурные свойства линейной системы: управляемость, наблюдаемость, полюса, устойчивость.
/// </summary>
/// <remarks>
/// Ранги считаются по сингулярным числам, а не по определителю: у почти неуправляемой системы
/// определитель матрицы управляемости может быть и 10⁻²⁰, и 10⁵ в зависимости от масштаба, а
/// отношение наименьшего сингулярного числа к наибольшему честно показывает близость к вырождению.
/// Полюса — собственные значения из <see cref="Eigen.General"/>.
/// </remarks>
public static class SystemAnalysis
{
    /// <summary>Матрица управляемости [B | AB | … | Aⁿ⁻¹B] (n × n·m)</summary>
    public static Matrix Controllability(Matrix a, Matrix b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException();
        int n = a.Height;
        if (!a.IsSquared || b.Height != n)
            throw new ArgumentException("A должна быть n×n, B — n×m.");

        int m = b.Width;
        var result = new Matrix(n, n * m);
        Matrix block = b.Copy();

        for (int k = 0; k < n; k++)
        {
            ControlLinAlg.SetBlock(result, 0, k * m, block);
            block = a * block;
        }

        return result;
    }

    /// <summary>Матрица наблюдаемости [C; CA; …; CAⁿ⁻¹] (n·p × n)</summary>
    public static Matrix Observability(Matrix a, Matrix c)
    {
        if (a == null || c == null)
            throw new ArgumentNullException();
        int n = a.Height;
        if (!a.IsSquared || c.Width != n)
            throw new ArgumentException("A должна быть n×n, C — p×n.");

        int p = c.Height;
        var result = new Matrix(n * p, n);
        Matrix block = c.Copy();

        for (int k = 0; k < n; k++)
        {
            ControlLinAlg.SetBlock(result, k * p, 0, block);
            block = block * a;
        }

        return result;
    }

    /// <summary>Численный ранг: число сингулярных чисел больше tolerance·σmax</summary>
    /// <param name="matrix">Матрица</param>
    /// <param name="tolerance">Относительный порог</param>
    public static int Rank(Matrix matrix, double tolerance = 1e-9)
    {
        if (matrix == null)
            throw new ArgumentNullException(nameof(matrix));

        // Одностороннее вращение Якоби ортогонализует столбцы: широкую матрицу удобнее транспонировать
        Matrix tall = matrix.Height >= matrix.Width ? matrix : matrix.Transpose();
        (_, double[] sigma, _) = Svd.Decompose(tall);
        double largest = sigma.Length == 0 ? 0 : sigma.Max();

        return largest == 0 ? 0 : sigma.Count(s => s > tolerance * largest);
    }

    /// <summary>Управляема ли пара (A, B)</summary>
    public static bool IsControllable(Matrix a, Matrix b, double tolerance = 1e-9)
        => Rank(Controllability(a, b), tolerance) == a.Height;

    /// <summary>Наблюдаема ли пара (A, C)</summary>
    public static bool IsObservable(Matrix a, Matrix c, double tolerance = 1e-9)
        => Rank(Observability(a, c), tolerance) == a.Height;

    /// <summary>Полюса — собственные значения A</summary>
    public static Complex[] Poles(Matrix a) => Eigen.General(a);

    /// <summary>Спектральный радиус A</summary>
    public static double SpectralRadius(Matrix a) => Eigen.SpectralRadius(a);

    /// <summary>Устойчива ли дискретная система: все полюса строго внутри единичного круга</summary>
    public static bool IsStableDiscrete(Matrix a) => SpectralRadius(a) < 1;

    /// <summary>Устойчива ли непрерывная система: все полюса строго в левой полуплоскости</summary>
    public static bool IsStableContinuous(Matrix a) => Poles(a).All(p => p.Real < 0);
}

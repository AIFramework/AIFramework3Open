using System;
using System.Collections.Generic;
using System.Linq;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;
using Complex = System.Numerics.Complex;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Размещение полюсов для SISO: u = −K x по формуле Аккермана (достижимая пара A, B).
/// Монический полином: λ^n + c_{n−1} λ^{n−1} + … + c₀;
/// коэффициенты задаются как [c₀, c₁, …, c_{n−1}] (от младшего к старшему при степенях λ).
/// </summary>
/// <remarks>
/// Формула Аккермана обращает матрицу управляемости. Если пара неуправляема, этой матрицы нет,
/// и прежде вместо ошибки получалось усиление из численного мусора; теперь управляемость
/// проверяется по сингулярным числам. Для систем со многими входами полюса размещают через
/// LQR или методы с дополнительными степенями свободы — здесь их нет.
/// </remarks>
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

        return SystemAnalysis.Controllability(a, b);
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

        if (SystemAnalysis.Rank(w) < n)
            throw new InvalidOperationException("Пара (A, B) неуправляема: полюса нельзя разместить произвольно.");

        Matrix wInv = ControlLinAlg.Inverse(w);

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

    /// <summary>Строка усилений K, размещающая полюса замкнутой системы A − BK в заданных точках</summary>
    /// <param name="a">A (n×n)</param>
    /// <param name="b">B (n×1)</param>
    /// <param name="poles">n полюсов: вещественные или сопряжённые пары</param>
    public static Matrix AckermannGain(Matrix a, Matrix b, IEnumerable<Complex> poles)
        => AckermannGain(a, b, PolynomialFromPoles(poles));

    /// <summary>
    /// Коэффициенты монического полинома с заданными корнями: [c₀, …, c_{n−1}] для λⁿ + c_{n−1}λⁿ⁻¹ + … + c₀
    /// </summary>
    /// <param name="poles">Корни: вещественные или сопряжённые пары</param>
    /// <exception cref="ArgumentException">Комплексный корень без сопряжённого: коэффициенты не вещественны</exception>
    public static Vector PolynomialFromPoles(IEnumerable<Complex> poles)
    {
        if (poles == null)
            throw new ArgumentNullException(nameof(poles));

        Complex[] roots = poles.ToArray();
        var coefficients = new Complex[roots.Length + 1];
        coefficients[0] = Complex.One;

        // Умножение на (λ − p) по одному корню; coefficients[k] — при λ^k
        int degree = 0;
        foreach (Complex root in roots)
        {
            degree++;
            for (int k = degree; k >= 0; k--)
            {
                Complex shifted = k > 0 ? coefficients[k - 1] : Complex.Zero;
                coefficients[k] = shifted - (root * coefficients[k]);
            }
        }

        double scale = coefficients.Max(c => Complex.Abs(c));
        var result = new Vector(roots.Length);

        for (int k = 0; k < roots.Length; k++)
        {
            if (Math.Abs(coefficients[k].Imaginary) > 1e-9 * Math.Max(1, scale))
                throw new ArgumentException("Комплексные полюса должны идти сопряжёнными парами.", nameof(poles));

            result[k] = coefficients[k].Real;
        }

        return result;
    }

    private static Matrix MatrixPow(Matrix a, int p)
    {
        Matrix r = ControlLinAlg.Eye(a.Height);
        for (int i = 0; i < p; i++)
            r = r * a;
        return r;
    }
}

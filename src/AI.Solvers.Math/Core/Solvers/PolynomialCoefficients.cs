using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>
/// Извлечение коэффициентов многочлена одной переменной из AST.
/// <para>
/// Нужен взамен разбора уравнения регулярными выражениями: шаблон вида
/// «x^2 ± bx ± c = 0» отвечает на написание, а не на смысл, поэтому 2x²+3x+1
/// терял старший коэффициент, а x³-2x+1 попадал в шаблон линейного уравнения.
/// </para>
/// </summary>
internal static class PolynomialCoefficients
{
    /// <summary>
    /// Пытается представить выражение как многочлен от <paramref name="variable"/>.
    /// </summary>
    /// <param name="coefficients">coefficients[k] — коэффициент при x^k (индекс = степень).</param>
    /// <returns>false, если выражение не многочлен или его степень выше maxDegree.</returns>
    public static bool TryExtract(Expression expr, string variable, int maxDegree, out double[] coefficients)
    {
        var collected = Collect(expr, variable, maxDegree);
        coefficients = collected ?? [];
        return collected != null;
    }

    /// <summary>Степень многочлена по коэффициентам (старшие нули отбрасываются).</summary>
    public static int Degree(double[] coefficients)
    {
        for (int k = coefficients.Length - 1; k >= 0; k--)
            if (System.Math.Abs(coefficients[k]) > 1e-12) return k;
        return 0;
    }

    private static double[]? Collect(Expression expr, string variable, int maxDegree)
    {
        switch (expr)
        {
            case Constant c:
                return [c.Value];

            case Variable v:
                return v.Name == variable ? [0.0, 1.0] : null;

            case Add add:
                return Sum(Collect(add.Left, variable, maxDegree), Collect(add.Right, variable, maxDegree));

            case Multiply mult:
                return Product(Collect(mult.Left, variable, maxDegree),
                               Collect(mult.Right, variable, maxDegree), maxDegree);

            case Divide div:
                return Quotient(Collect(div.Numerator, variable, maxDegree),
                                Collect(div.Denominator, variable, maxDegree));

            case Power pow:
                return Exponentiate(pow, variable, maxDegree);

            // sin, exp, ln и прочее многочленом не являются
            default:
                return null;
        }
    }

    private static double[]? Sum(double[]? left, double[]? right)
    {
        if (left is null || right is null) return null;

        var result = new double[System.Math.Max(left.Length, right.Length)];
        for (int k = 0; k < left.Length; k++) result[k] += left[k];
        for (int k = 0; k < right.Length; k++) result[k] += right[k];
        return result;
    }

    private static double[]? Product(double[]? left, double[]? right, int maxDegree)
    {
        if (left is null || right is null) return null;
        if (left.Length + right.Length - 2 > maxDegree) return null;

        var result = new double[left.Length + right.Length - 1];
        for (int i = 0; i < left.Length; i++)
            for (int j = 0; j < right.Length; j++)
                result[i + j] += left[i] * right[j];
        return result;
    }

    /// <summary>Делить можно только на константу — иначе это рациональная функция, а не многочлен.</summary>
    private static double[]? Quotient(double[]? numerator, double[]? denominator)
    {
        if (numerator is null || denominator is null) return null;
        if (denominator.Length != 1 || System.Math.Abs(denominator[0]) < 1e-12) return null;

        var result = new double[numerator.Length];
        for (int k = 0; k < numerator.Length; k++) result[k] = numerator[k] / denominator[0];
        return result;
    }

    private static double[]? Exponentiate(Power pow, string variable, int maxDegree)
    {
        if (pow.Exponent is not Constant ce) return null;

        double exponent = ce.Value;
        if (exponent < 0 || System.Math.Abs(exponent - System.Math.Round(exponent)) > 1e-10) return null;

        int n = (int)System.Math.Round(exponent);
        if (n > maxDegree) return null;

        var baseCoeffs = Collect(pow.Base, variable, maxDegree);
        if (baseCoeffs is null) return null;

        double[]? result = [1.0];
        for (int i = 0; i < n; i++)
        {
            result = Product(result, baseCoeffs, maxDegree);
            if (result is null) return null;
        }
        return result;
    }
}

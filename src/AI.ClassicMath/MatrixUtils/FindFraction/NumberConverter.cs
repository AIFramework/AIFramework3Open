using AI.ClassicMath.Calculator.Libs.Algebra;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace AI.ClassicMath.MatrixUtils.FindFraction;

public static partial class NumberConverter
{
    public static ConversionResult Analyze(double number)
    {
        double tolerance = 1e-10;

        // 1. Check Integers
        if (Math.Abs(number % 1) < double.Epsilon)
        {
            return new ConversionResult
            {
                Type = ConversionType.Integer,
                Fraction = ((BigInteger)number).ToString(),
                Description = "Целое число (погрешность: 0)",
                Numerator = (BigInteger)number,
                Denominator = 1
            };
        }

        // Трансцендентные числа (π, e) - ПРИОРИТЕТ!
        var transcendental = TranscendentalNumbers.CheckKnownConstant(number, tolerance);
        if (transcendental != null) return transcendental;

        // Произведение рационального на трансцендентное (2π, π/2)
        var rationalMultiple = TranscendentalNumbers.CheckRationalMultiple(number, tolerance);
        if (rationalMultiple != null) return rationalMultiple;

        // Рациональные числа (1/2, 2/3, и т.д.)
        var rational = RationalAnalyzer.Analyze(number, tolerance);
        if (rational != null && rational.Type != ConversionType.Irrational) return rational;

        // Алгебраические константы (√2, √3, √2/2) - только для иррациональных!
        var symbolicForm = Calculator.KnownConstants.TryGetSymbolicForm(number, tolerance);
        if (symbolicForm != null)
        {
            return new ConversionResult
            {
                Type = ConversionType.Algebraic,
                Fraction = symbolicForm,
                Description = $"Известная константа: {symbolicForm}",
                Numerator = 0,
                Denominator = 0
            };
        }

        // Проверка на высокие корни
        var nthRoot = CheckNthRoot(number);
        if (nthRoot != null) return nthRoot;

        var algebraic = TryAlgebraicQuadratic(number, tolerance);
        if (algebraic != null) return algebraic;

        var cfResult = TryContinuedFraction(number);
        if (cfResult != null) return cfResult;

        return new ConversionResult
        {
            Type = ConversionType.Irrational,
            Fraction = null,
            Description = "Иррациональное число или сложный период"
        };
    }

    private static ConversionResult TryAlgebraicQuadratic(double x, double tolerance)
    {
        return TryAlgebraicDegree(x, 2, tolerance);
    }

    private static ConversionResult TryAlgebraicDegree(double x, int degree, double tolerance)
    {
        var coeffs = FindPolynomialCoefficients(x, degree, tolerance);
        if (coeffs != null)
        {
            var result = SolveAndFormat(coeffs, x, tolerance);
            if (result != null) return result;
        }

        return null;
    }

    private static int[] FindPolynomialCoefficients(double x, int degree, double tolerance) => IntegerRelationFinder.FindPolynomial(x, degree, tolerance);


    private static double EvaluatePolynomial(int[] coeffs, double x)
    {
        double result = 0;
        double xPow = 1;
        for (int i = 0; i < coeffs.Length; i++)
        {
            result += coeffs[i] * xPow;
            xPow *= x;
        }
        return Math.Abs(result);
    }

    private static ConversionResult SolveAndFormat(int[] coeffs, double targetX, double tolerance)
    {
        int degree = coeffs.Length - 1;

        if (degree == 2)
            return SolveQuadratic(coeffs, targetX, tolerance);
        else if (degree == 3)
            return SolveCubic(coeffs, targetX, tolerance);

        else if (degree == 4)
            return SolveQuartic(coeffs, targetX, tolerance);

        else if (degree >= 5)
            return SolveHighDegree(coeffs, targetX, tolerance, degree);


        return null;
    }
}

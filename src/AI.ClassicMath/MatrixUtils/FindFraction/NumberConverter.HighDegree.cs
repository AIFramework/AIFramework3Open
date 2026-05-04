using AI.ClassicMath.Calculator.Libs.Algebra;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace AI.ClassicMath.MatrixUtils.FindFraction;

public static partial class NumberConverter
{
    private static ConversionResult SolveHighDegree(int[] coeffs, double targetX, double tolerance, int degree)
    {
        double error = Math.Abs(EvaluatePolynomial(coeffs, targetX));
        if (error > tolerance)
            return null;

        if (degree == 6)
        {
            var decomposed = TryDecomposeDegree6(coeffs, targetX, tolerance);
            if (decomposed != null) return decomposed;
        }

        string polyStr = FormatPolynomial(coeffs);

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = $"Корень уравнения {polyStr}=0",
            Description = $"Алгебраическое число {degree}-й степени (≈ {targetX:F10})",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static ConversionResult TryDecomposeDegree6(int[] coeffs, double targetX, double tolerance)
    {
        for (int cubeNum = -20; cubeNum <= 20; cubeNum++)
        {
            if (cubeNum == 0) continue;

            for (int cubeDen = 1; cubeDen <= 20; cubeDen++)
            {
                double cubeRootPart = Math.Pow((double)cubeNum / cubeDen, 1.0 / 3.0);
                double remainder = targetX - cubeRootPart;

                double remainderSquared = remainder * remainder;

                for (int sqNum = 1; sqNum <= 50; sqNum++)
                {
                    for (int sqDen = 1; sqDen <= 20; sqDen++)
                    {
                        double sqrtVal = Math.Sqrt((double)sqNum / sqDen);

                        if (Math.Abs(remainder - sqrtVal) < tolerance)
                            return FormatMixedRadical(cubeNum, cubeDen, sqNum, sqDen, true);

                        if (Math.Abs(remainder + sqrtVal) < tolerance)
                            return FormatMixedRadical(cubeNum, cubeDen, sqNum, sqDen, false);
                    }
                }
            }
        }

        return null;
    }

    private static ConversionResult FormatMixedRadical(int cubeNum, int cubeDen, int sqNum, int sqDen, bool plusSign)
    {

        string cubeRootStr;
        if (cubeDen == 1)
            cubeRootStr = RadicalHelper.SimplifyNthRoot(cubeNum, 3);
        else
        {
            int gcd = GCD(Math.Abs(cubeNum), cubeDen);
            int simpleCubeNum = cubeNum / gcd;
            int simpleCubeDen = cubeDen / gcd;
            cubeRootStr = simpleCubeDen == 1
                ? RadicalHelper.SimplifyNthRoot(simpleCubeNum, 3)
                : $"∛({simpleCubeNum}/{simpleCubeDen})";
        }

        string sqrtStr;
        if (sqDen == 1)
        {
            RadicalHelper.SimplifySqrt(sqNum, out int outPart, out int inPart);
            if (inPart == 1)
                sqrtStr = outPart.ToString();
            else if (outPart == 1)
                sqrtStr = $"√{inPart}";
            else
                sqrtStr = $"{outPart}√{inPart}";
        }
        else
        {
            int gcd = GCD(sqNum, sqDen);
            int simpleSqNum = sqNum / gcd;
            int simpleSqDen = sqDen / gcd;

            RadicalHelper.SimplifySqrt(simpleSqNum, out int numOut, out int numIn);
            RadicalHelper.SimplifySqrt(simpleSqDen, out int denOut, out int denIn);

            if (numIn == 1 && denIn == 1)
            {
                sqrtStr = denOut == 1 ? numOut.ToString() : $"{numOut}/{denOut}";
            }
            else if (denIn == 1)
            {
                string numStr = numOut == 1 ? $"√{numIn}" : $"{numOut}√{numIn}";
                sqrtStr = denOut == 1 ? numStr : $"{numStr}/{denOut}";
            }
            else
            {
                sqrtStr = $"√({simpleSqNum}/{simpleSqDen})";
            }
        }

        string sign = plusSign ? "+" : "-";
        string result = $"{cubeRootStr}{sign}{sqrtStr}";

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = result,
            Description = $"Алгебраическое число 6-й степени (смешанные радикалы)",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static string FormatPolynomial(int[] coeffs)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = coeffs.Length - 1; i >= 0; i--)
        {
            int c = coeffs[i];
            if (c == 0) continue;

            if (sb.Length > 0 && c > 0) sb.Append("+");

            if (i == 0)
            {
                sb.Append(c);
            }
            else if (i == 1)
            {
                if (c == 1) sb.Append("x");
                else if (c == -1) sb.Append("-x");
                else sb.Append($"{c}x");
            }
            else
            {
                if (c == 1) sb.Append($"x^{i}");
                else if (c == -1) sb.Append($"-x^{i}");
                else sb.Append($"{c}x^{i}");
            }
        }
        return sb.ToString();
    }

    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}

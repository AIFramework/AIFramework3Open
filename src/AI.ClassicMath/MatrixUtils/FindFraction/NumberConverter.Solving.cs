using AI.ClassicMath.Calculator.Libs.Algebra;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace AI.ClassicMath.MatrixUtils.FindFraction;

public static partial class NumberConverter
{
    private static ConversionResult SolveQuadratic(int[] coeffs, double targetX, double tolerance)
    {
        int c = coeffs[0], b = coeffs[1], a = coeffs[2];

        int D = b * b - 4 * a * c;

        if (D < 0) return null;

        int sqrtD = (int)Math.Round(Math.Sqrt(D));
        bool isPerfectSquare = sqrtD * sqrtD == D;

        string sqrtDStr;
        if (isPerfectSquare)
            sqrtDStr = sqrtD.ToString();

        else
        {
            RadicalHelper.SimplifySqrt(D, out int outPart, out int inPart);
            if (outPart == 1)
                sqrtDStr = $"√{inPart}";
            else
                sqrtDStr = $"{outPart}√{inPart}";
        }

        double x1 = (-b + Math.Sqrt(D)) / (2.0 * a);
        double x2 = (-b - Math.Sqrt(D)) / (2.0 * a);

        // Проверка на знаки
        string rootFormula;
        if (Math.Abs(x1 - targetX) < tolerance && Math.Sign(targetX) == Math.Sign(x1))
            rootFormula = FormatQuadraticRoot(-b, sqrtDStr, 2 * a, true);
        else if (Math.Abs(x2 - targetX) < tolerance && Math.Sign(targetX) == Math.Sign(x2))
            rootFormula = FormatQuadraticRoot(-b, sqrtDStr, 2 * a, false);
        else
            return null;

        double calculatedValue = Math.Abs(x1 - targetX) < tolerance ? x1 : x2;
        double error = Math.Abs(targetX - calculatedValue);

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = rootFormula,
            Description = $"Корень уравнения {FormatPolynomial(coeffs)}=0 (погрешность: {error:E3})",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static string FormatQuadraticRoot(int numeratorConst, string sqrtPart, int denominator, bool plusSign)
    {
        string sign = plusSign ? "+" : "-";

        int sqrtCoeff = 1;
        string sqrtInner = sqrtPart;

        if (sqrtPart.Contains("√"))
        {
            int sqrtIdx = sqrtPart.IndexOf('√');
            if (sqrtIdx > 0)
            {
                string coeffStr = sqrtPart.Substring(0, sqrtIdx);
                if (int.TryParse(coeffStr, out int parsedCoeff))
                {
                    sqrtCoeff = parsedCoeff;
                    sqrtInner = "√" + sqrtPart.Substring(sqrtIdx + 1);
                }
            }
        }

        int gcd = GCD(GCD(Math.Abs(numeratorConst), sqrtCoeff), Math.Abs(denominator));

        if (gcd > 1)
        {
            numeratorConst /= gcd;
            sqrtCoeff /= gcd;
            denominator /= gcd;
        }

        if (sqrtCoeff == 1)
            sqrtPart = sqrtInner;
        else if (sqrtCoeff == 0)
            sqrtPart = "";
        else
            sqrtPart = sqrtCoeff + sqrtInner;

        if (denominator == 1)
        {
            if (numeratorConst == 0)
            {
                if (string.IsNullOrEmpty(sqrtPart))
                    return "0";
                return plusSign ? sqrtPart : $"-{sqrtPart}";
            }
            else if (string.IsNullOrEmpty(sqrtPart))
                return numeratorConst.ToString();
            else
                return $"{numeratorConst}{sign}{sqrtPart}";
        }
        else
        {
            if (numeratorConst == 0)
            {
                if (string.IsNullOrEmpty(sqrtPart))
                    return "0";
                return plusSign ? $"{sqrtPart}/{denominator}" : $"-{sqrtPart}/{denominator}";
            }
            else if (string.IsNullOrEmpty(sqrtPart))
                return $"{numeratorConst}/{denominator}";
            else
                return $"({numeratorConst}{sign}{sqrtPart})/{denominator}";
        }
    }

    private static ConversionResult SolveCubic(int[] coeffs, double targetX, double tolerance)
    {
        double d = coeffs[0], c = coeffs[1], b = coeffs[2], a = coeffs[3];

        double p = (3 * a * c - b * b) / (3 * a * a);
        double q = (2 * b * b * b - 9 * a * b * c + 27 * a * a * d) / (27 * a * a * a);

        double discriminant = -(4 * p * p * p + 27 * q * q);

        if (discriminant > 0)
        {
            double m = 2 * Math.Sqrt(-p / 3);
            double theta = Math.Acos(3 * q / (p * m)) / 3;

            double t1 = m * Math.Cos(theta);
            double t2 = m * Math.Cos(theta - 2 * Math.PI / 3);
            double t3 = m * Math.Cos(theta - 4 * Math.PI / 3);

            double shift = -b / (3 * a);
            double x1 = t1 + shift;
            double x2 = t2 + shift;
            double x3 = t3 + shift;

            if (Math.Abs(x1 - targetX) < tolerance)
                return FormatCubicRoot(coeffs, x1, "x₁");
            if (Math.Abs(x2 - targetX) < tolerance)
                return FormatCubicRoot(coeffs, x2, "x₂");
            if (Math.Abs(x3 - targetX) < tolerance)
                return FormatCubicRoot(coeffs, x3, "x₃");
        }
        else
        {
            double delta = q * q / 4 + p * p * p / 27;
            double u = Math.Pow(-q / 2 + Math.Sqrt(delta), 1.0 / 3.0);
            double v = Math.Pow(-q / 2 - Math.Sqrt(delta), 1.0 / 3.0);
            double t = u + v;
            double x = t - b / (3 * a);

            if (Math.Abs(x - targetX) < tolerance)
                return FormatCubicRoot(coeffs, x, "x");
        }

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = $"Корень полинома {FormatPolynomial(coeffs)}",
            Description = $"Алгебраическое число 3-й степени",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static ConversionResult FormatCubicRoot(int[] coeffs, double rootValue, string rootLabel)
    {
        double d = coeffs[0], c = coeffs[1], b = coeffs[2], a = coeffs[3];

        if (Math.Abs(b) < 1e-10 && Math.Abs(c) < 1e-10 && a == 1)
        {
            int cubeRoot = (int)Math.Round(Math.Pow(-d, 1.0 / 3.0));
            if (Math.Abs(Math.Pow(cubeRoot, 3) + d) < 1e-10)
            {
                return new ConversionResult
                {
                    Type = ConversionType.Algebraic,
                    Fraction = cubeRoot.ToString(),
                    Description = $"Корень кубический из {-d}",
                    Numerator = 0,
                    Denominator = 0
                };
            }

            SimplifyCubeRoot((int)-d, out int outPart, out int inPart);
            string cubeRootStr = outPart == 1 ? $"∛{inPart}" : $"{outPart}∛{inPart}";

            return new ConversionResult
            {
                Type = ConversionType.Algebraic,
                Fraction = cubeRootStr,
                Description = $"Корень кубический",
                Numerator = 0,
                Denominator = 0
            };
        }

        string polyStr = FormatPolynomial(coeffs);

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = $"{rootLabel} уравнения {polyStr}=0",
            Description = $"Алгебраическое число 3-й степени (корень ≈ {rootValue:F10})",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static void SimplifyCubeRoot(int n, out int outPart, out int inPart)
    {
        outPart = 1;
        inPart = Math.Abs(n);

        for (int i = 2; i * i * i <= Math.Abs(n); i++)
        {
            int cube = i * i * i;
            while (inPart % cube == 0)
            {
                inPart /= cube;
                outPart *= i;
            }
        }

        if (n < 0)
        {
            outPart = -outPart;
        }
    }
}

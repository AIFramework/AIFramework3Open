using AI.ClassicMath.Calculator.Libs.Algebra;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace AI.ClassicMath.MatrixUtils.FindFraction;

public static partial class NumberConverter
{
    private static ConversionResult SolveQuartic(int[] coeffs, double targetX, double tolerance)
    {
        int e = coeffs[0], d = coeffs[1], c = coeffs[2], b = coeffs[3], a = coeffs[4];

        if (b == 0 && d == 0)
            return SolveBiquadratic(a, c, e, targetX, tolerance);

        return SolveQuarticFerrari(coeffs, targetX, tolerance);
    }

    private static ConversionResult SolveBiquadratic(int a, int c, int e, double targetX, double tolerance)
    {
        int D = c * c - 4 * a * e;
        if (D < 0)
        {
            return new ConversionResult
            {
                Type = ConversionType.Algebraic,
                Fraction = $"Корень уравнения {a}x⁴{(c >= 0 ? "+" : "")}{c}x²{(e >= 0 ? "+" : "")}{e}=0",
                Description = "Комплексные корни",
                Numerator = 0,
                Denominator = 0
            };
        }

        double y1 = (-c + Math.Sqrt(D)) / (2.0 * a);
        double y2 = (-c - Math.Sqrt(D)) / (2.0 * a);

        double[] possibleRoots = new double[4];
        int rootCount = 0;

        if (y1 >= 0)
        {
            possibleRoots[rootCount++] = Math.Sqrt(y1);
            possibleRoots[rootCount++] = -Math.Sqrt(y1);
        }
        if (y2 >= 0 && Math.Abs(y2 - y1) > 1e-10)
        {
            possibleRoots[rootCount++] = Math.Sqrt(y2);
            possibleRoots[rootCount++] = -Math.Sqrt(y2);
        }

        for (int i = 0; i < rootCount; i++)
        {
            if (Math.Abs(possibleRoots[i] - targetX) < tolerance && Math.Sign(targetX) == Math.Sign(possibleRoots[i]))
            {
                return FormatBiquadraticRoot(a, c, e, D, possibleRoots[i], targetX, tolerance);
            }
        }

        return null;
    }

    private static ConversionResult FormatBiquadraticRoot(int a, int c, int e, int D, double root, double targetX, double tolerance)
    {
        double y = root * root;

        double y1 = (-c + Math.Sqrt(D)) / (2.0 * a);
        double y2 = (-c - Math.Sqrt(D)) / (2.0 * a);

        bool useY1 = Math.Abs(y - y1) < Math.Abs(y - y2);
        bool positive = root > 0;
        RadicalHelper.SimplifySqrt(D, out int sqrtDOut, out int sqrtDIn);

        if (a == 1)
        {
            int constPart = -c;
            int sqrtCoeff = useY1 ? sqrtDOut : -sqrtDOut;
            int den = 2 * a;

            var decomp = TryDecomposeNestedSqrtAdvanced(constPart, sqrtCoeff, sqrtDIn, den, targetX);
            if (decomp != null)
            {
                double error = Math.Abs(targetX - root);
                return new ConversionResult
                {
                    Type = ConversionType.Algebraic,
                    Fraction = decomp,
                    Description = $"Биквадратное уравнение {a}x⁴{(c >= 0 ? "+" : "")}{c}x²{(e >= 0 ? "+" : "")}{e}=0 (погрешность: {error:E3})",
                    Numerator = 0,
                    Denominator = 0
                };
            }
        }

        string sign1 = positive ? "" : "-";
        string sign2 = useY1 ? "+" : "-";

        string sqrtDStr = sqrtDIn == 1 ? sqrtDOut.ToString() :
                         sqrtDOut == 1 ? $"√{sqrtDIn}" : $"{sqrtDOut}√{sqrtDIn}";

        string innerExpr;
        int yDen = 2 * a;
        if (a == 1 && yDen == 2)
        {
            innerExpr = $"√(({-c}{sign2}{sqrtDStr})/2)";
        }
        else
        {
            innerExpr = $"√(({-c}{sign2}{sqrtDStr})/{2 * a})";
        }

        string formula = sign1 + innerExpr;
        double error2 = Math.Abs(targetX - root);

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = formula,
            Description = $"Биквадратное уравнение {a}x⁴{(c >= 0 ? "+" : "")}{c}x²{(e >= 0 ? "+" : "")}{e}=0 (погрешность: {error2:E3})",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static string TryDecomposeNestedSqrtAdvanced(int A, int B, int C, int D, double targetX)
    {
        if (B > 0)
        {
            for (int m = 1; m <= 20; m++)
            {
                for (int n = 1; n <= 20; n++)
                {
                    if (m >= n) continue;

                    if (A != D * (m + n)) continue;

                    if (B * B * C != 4 * m * n * D * D) continue;

                    double x1 = Math.Sqrt(m) + Math.Sqrt(n);
                    if (Math.Sign(targetX) == Math.Sign(x1))
                        return $"√{m}+√{n}";
                    else return $"-√{m}-√{n}";
                }
            }
        }

        if (B < 0)
        {
            for (int m = 1; m <= 20; m++)
            {
                for (int n = 1; n <= 20; n++)
                {
                    if (m <= n) continue;
                    if (A != D * (m + n)) continue;
                    if (B * B * C != 4 * m * n * D * D) continue;

                    double x1 = Math.Sqrt(m) - Math.Sqrt(n);
                    if (Math.Sign(targetX) == Math.Sign(x1))
                        return $"√{m}-√{n}";
                    else return $"√{n}-√{m}";

                }
            }

            for (int m = 1; m <= 20; m++)
            {
                for (int n = 1; n <= 20; n++)
                {
                    if (n >= m) continue;
                    if (A != D * (m + n)) continue;
                    if (B * B * C != 4 * m * n * D * D) continue;

                    double x1 = Math.Sqrt(n) - Math.Sqrt(m);
                    if (Math.Sign(targetX) == Math.Sign(x1))
                        return $"√{n}-√{m}";
                    else return $"√{m}-√{n}";

                }
            }
        }

        return null;
    }


    private static ConversionResult SolveQuarticFerrari(int[] coeffs, double targetX, double tolerance)
    {
        double e = coeffs[0], d = coeffs[1], c = coeffs[2], b = coeffs[3], a = coeffs[4];

        double p = (8 * a * c - 3 * b * b) / (8 * a * a);
        double q = (b * b * b - 4 * a * b * c + 8 * a * a * d) / (8 * a * a * a);
        double r = (-3 * b * b * b * b + 256 * a * a * a * e - 64 * a * a * b * d + 16 * a * b * b * c) / (256 * a * a * a * a);

        double[] resolventCoeffs = new double[4];
        resolventCoeffs[0] = -q * q;
        resolventCoeffs[1] = p * p - 4 * r;
        resolventCoeffs[2] = 2 * p;
        resolventCoeffs[3] = 1;

        double y = SolveResolventCubic(resolventCoeffs);

        if (double.IsNaN(y))
        {
            double error = Math.Abs(EvaluatePolynomial(coeffs, targetX));
            return new ConversionResult
            {
                Type = ConversionType.Algebraic,
                Fraction = $"Корень полинома {FormatPolynomial(coeffs)}",
                Description = $"Алгебраическое число 4-й степени (погрешность: {error:E3})",
                Numerator = 0,
                Denominator = 0
            };
        }

        double sqrtTerm = Math.Sqrt(2 * y - p);

        double a1 = 1;
        double b1 = sqrtTerm;
        double c1 = y - q / (2 * sqrtTerm);

        double a2 = 1;
        double b2 = -sqrtTerm;
        double c2 = y + q / (2 * sqrtTerm);

        double[] roots = new double[4];
        int rootCount = 0;

        double disc1 = b1 * b1 - 4 * a1 * c1;
        if (disc1 >= 0)
        {
            roots[rootCount++] = (-b1 + Math.Sqrt(disc1)) / (2 * a1) - b / (4 * a);
            roots[rootCount++] = (-b1 - Math.Sqrt(disc1)) / (2 * a1) - b / (4 * a);
        }

        double disc2 = b2 * b2 - 4 * a2 * c2;
        if (disc2 >= 0)
        {
            roots[rootCount++] = (-b2 + Math.Sqrt(disc2)) / (2 * a2) - b / (4 * a);
            roots[rootCount++] = (-b2 - Math.Sqrt(disc2)) / (2 * a2) - b / (4 * a);
        }

        for (int i = 0; i < rootCount; i++)
        {
            if (Math.Abs(roots[i] - targetX) < tolerance)
            {
                double error = Math.Abs(targetX - roots[i]);
                return FormatQuarticRoot(coeffs, roots[i], $"x_{i + 1}", error);
            }
        }

        double minError = double.MaxValue;
        for (int i = 0; i < rootCount; i++)
        {
            double err = Math.Abs(roots[i] - targetX);
            if (err < minError) minError = err;
        }

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = $"Корень полинома {FormatPolynomial(coeffs)}",
            Description = $"Алгебраическое число 4-й степени (погрешность: {minError:E3})",
            Numerator = 0,
            Denominator = 0
        };
    }

    private static double SolveResolventCubic(double[] coeffs)
    {
        double x = 1.0;
        for (int i = 0; i < 100; i++)
        {
            double f = coeffs[3] * x * x * x + coeffs[2] * x * x + coeffs[1] * x + coeffs[0];
            double df = 3 * coeffs[3] * x * x + 2 * coeffs[2] * x + coeffs[1];

            if (Math.Abs(df) < 1e-15) break;

            double xNew = x - f / df;
            if (Math.Abs(xNew - x) < 1e-12) return xNew;
            x = xNew;
        }
        return x;
    }

    private static ConversionResult FormatQuarticRoot(int[] coeffs, double rootValue, string rootLabel, double error)
    {
        string polyStr = FormatPolynomial(coeffs);

        return new ConversionResult
        {
            Type = ConversionType.Algebraic,
            Fraction = $"{rootLabel} уравнения {polyStr}=0",
            Description = $"Алгебраическое число 4-й степени (корень ≈ {rootValue:F10}, погрешность: {error:E3})",
            Numerator = 0,
            Denominator = 0
        };
    }
}

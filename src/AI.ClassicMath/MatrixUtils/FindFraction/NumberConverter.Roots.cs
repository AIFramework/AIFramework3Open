using AI.ClassicMath.Calculator.Libs.Algebra;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace AI.ClassicMath.MatrixUtils.FindFraction;

public static partial class NumberConverter
{
    private static ConversionResult CheckNthRoot(double number)
    {
        double tolerance = 1e-10;


        if (RadicalHelper.IsNthRoot(number, 2, tolerance, out int qRadicand))
        {
            string simplified = RadicalHelper.SimplifyNthRoot(qRadicand, 2);
            return new ConversionResult
            {
                Type = ConversionType.Root,
                Fraction = simplified,
                Description = $"Квадратный корень из {qRadicand}",
                Numerator = 0,
                Denominator = 0
            };
        }

        if (RadicalHelper.IsNthRoot(number, 3, tolerance, out int cubeRadicand))
        {
            string simplified = RadicalHelper.SimplifyNthRoot(cubeRadicand, 3);
            return new ConversionResult
            {
                Type = ConversionType.Root,
                Fraction = simplified,
                Description = $"Кубический корень из {cubeRadicand}",
                Numerator = 0,
                Denominator = 0
            };
        }

        if (RadicalHelper.IsNthRoot(number, 4, tolerance, out int fourthRadicand))
        {
            int sqrtRadicand = (int)Math.Round(Math.Sqrt(fourthRadicand));
            if (sqrtRadicand * sqrtRadicand == fourthRadicand)
            {
                // Это квадратный корень
            }
            else
            {
                string simplified = RadicalHelper.SimplifyNthRoot(fourthRadicand, 4);
                return new ConversionResult
                {
                    Type = ConversionType.Root,
                    Fraction = simplified,
                    Description = $"Корень 4-й степени из {fourthRadicand}",
                    Numerator = 0,
                    Denominator = 0
                };
            }
        }

        if (RadicalHelper.IsNthRoot(number, 5, tolerance, out int fifthRadicand))
        {
            string simplified = RadicalHelper.SimplifyNthRoot(fifthRadicand, 5);
            return new ConversionResult
            {
                Type = ConversionType.Root,
                Fraction = simplified,
                Description = $"Корень 5-й степени из {fifthRadicand}",
                Numerator = 0,
                Denominator = 0
            };
        }

        return null;
    }

    private static ConversionResult CheckRoot(double number)
    {
        double square = number * number;
        double tolerance = 1e-10;

        double roundedSquare = Math.Round(square);
        if (Math.Abs(square - roundedSquare) < tolerance)
        {
            BigInteger val = (BigInteger)roundedSquare;
            return CreateRootResult(val, 1, number < 0);
        }

        var ratResult = DetectRational(square);

        // Убираю огромные дроби
        if(ratResult != null)
        if ((double)ratResult.Denominator > 1e+5)
            return null;

        if (ratResult != null && ratResult.Type != ConversionType.Irrational)
        {
            return CreateRootResult(ratResult.Numerator, ratResult.Denominator, number < 0);
        }

        var cfResult = TryContinuedFraction(square);

        // Убираю огромные дроби
        if (cfResult != null)
            if ((double)cfResult.Denominator > 1e+5)
                return null;

        if (cfResult != null && cfResult.Type != ConversionType.Irrational)
        {
            return CreateRootResult(cfResult.Numerator, cfResult.Denominator, number < 0);
        }

        return null;
    }

    private static ConversionResult CreateRootResult(BigInteger num, BigInteger den, bool isNegative)
    {
        Simplify(ref num, ref den);

        ExtractSquare(num, out BigInteger numOut, out BigInteger numIn);
        ExtractSquare(den, out BigInteger denOut, out BigInteger denIn);

        string sign = isNegative ? "-" : "";
        string prefix = "";

        if (numOut != 1 || denOut != 1)
        {
            if (denOut == 1) prefix = $"{numOut}";
            else prefix = $"{numOut}/{denOut}";
        }

        string rootPart;
        if (denIn == 1)
        {
            if (numIn == 1) rootPart = "";
            else rootPart = $"√{numIn}";
        }
        else
        {
            rootPart = $"√({numIn}/{denIn})";
        }

        string finalStr;
        if (string.IsNullOrEmpty(prefix)) finalStr = string.IsNullOrEmpty(rootPart) ? "1" : rootPart;
        else finalStr = string.IsNullOrEmpty(rootPart) ? prefix : prefix == "1" ? rootPart : $"{prefix}*{rootPart}";

        if (isNegative) finalStr = "-" + finalStr;

        return new ConversionResult
        {
            Type = ConversionType.Root,
            Fraction = finalStr,
            Description = $"Корень из числа {num}/{den}",
            Numerator = numIn,
            Denominator = denIn
        };
    }

    private static void ExtractSquare(BigInteger val, out BigInteger outPart, out BigInteger inPart)
    {
        outPart = 1;
        inPart = val;

        for (long i = 2; i < 100; i++)
        {
            BigInteger sq = i * i;
            while (inPart % sq == 0)
            {
                inPart /= sq;
                outPart *= i;
            }
        }
    }
}

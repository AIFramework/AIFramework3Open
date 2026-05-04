using System;

namespace MCMCTest
{
    /// <summary>
    /// Параметры и функции для демонстрации MCMC и интегрирования Монте-Карло.
    /// </summary>
    internal static class McmcDemoMath
    {
        public const int McmcBurnInSteps = 3000;
        public const int McmcGenerateCount = 15000;

        public const double HistogramXMin = -3;
        public const double HistogramXMax = 3;
        public const double HistogramStep = 0.1;
        public const int HistogramBins = 70;

        public const double IntegralXMin = -5;
        public const double IntegralXMax = 20;
        public const double IntegralStep = 0.1;

        /// <summary>Логарифм ненормированной плотности ∝ exp(-(x⁴ - 2x²)/2).</summary>
        public static double TargetLogDensity(double x)
        {
            return -(x * x * x * x - 2 * x * x) / 2;
        }

        public static double Integrand(double x)
        {
            return 4 * Math.Sin(x) + 5;
        }

        public static double Antiderivative(double x)
        {
            return -4 * Math.Cos(x) + 5 * x;
        }
    }
}

using AI.ClassicMath.AlgorithmAnalysis;
using AI.DataStructs.Algebraic;
using AI.ML.Regression;

namespace AI.Solvers.Chem.Processors;

/// <summary>
/// Линейная аппроксимация y = a·x + b на регрессии фреймворка (AI.ML),
/// чтобы не дублировать МНК в каждом калькуляторе
/// </summary>
internal static class LeastSquares
{
    /// <summary>
    /// Наклон, свободный член и коэффициент детерминации
    /// </summary>
    public static (double Slope, double Intercept, double R2) Fit(double[] x, double[] y)
    {
        if (x.Length != y.Length)
            throw new ArgumentException("X and Y must have the same length");

        if (x.Length < 2)
            throw new ArgumentException("At least two points are required for a linear fit");

        var vectorX = new Vector(x);
        var vectorY = new Vector(y);

        var model = new LinearRegression(vectorX, vectorY);
        double r2 = MetricsForRegression.R2(vectorY, model.Predict(vectorX));

        return (model.Lrm.Slope, model.Lrm.Intercept, r2);
    }
}

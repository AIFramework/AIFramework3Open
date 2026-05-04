using System.Globalization;
using System.Text;
using AI.ClassicMath.AlgorithmAnalysis;
using AI.ClassicMath.Calculator;
using AI.ClassicMath.Calculator.ProcessorLogic;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.MathUtils.Algebra;
using AI.MathUtils.Combinatorics;
using AI.MathUtils.ODE;
using AI.MathUtils.SpecialFunction;

namespace AiFrameworkDemo.Modules.ClassicMath;

public static class MathDemoRunner
{
    public static string Run(string key, IReadOnlyDictionary<string, double> numP, IReadOnlyDictionary<string, string> textP)
    {
        string T(string k) => textP.TryGetValue(k, out var v) ? v : "";
        double N(string k, double def = 0) => numP.TryGetValue(k, out var v) ? v : def;

        return key switch
        {
            "gauss" => RunGauss(T("_matrix"), T("_vector")),
            "kramer" => RunKramer(T("_matrix"), T("_vector")),
            "placing" => $"A({(int)N("K", 3)},{(int)N("N", 10)}) = {CombinatoricsBaseFunction.PlacingWithoutRepetition((int)N("K", 3), (int)N("N", 10)).ToString("G14", CultureInfo.InvariantCulture)}",
            "num_combos" => $"C({(int)N("K", 3)},{(int)N("N", 10)}) (double) = {CombinatoricsBaseFunction.NumberOfCombinations((int)N("K", 3), (int)N("N", 10)).ToString("G14", CultureInfo.InvariantCulture)}",
            "combinations_long" => $"C({(int)N("N", 10)},{(int)N("K", 3)}) = {CombinatoricsBaseFunction.Combinations((int)N("N", 10), (int)N("K", 3))}",
            "calc_eval" => RunCalculator(T("_expression")),
            "mae" => Metric(T("_vector"), T("_vector2"), MetricsForRegression.MAE),
            "mse" => Metric(T("_vector"), T("_vector2"), MetricsForRegression.MSE),
            "rmse" => Metric(T("_vector"), T("_vector2"), MetricsForRegression.RMSE),
            "r2" => Metric(T("_vector"), T("_vector2"), MetricsForRegression.R2),
            "qr_q" => RunQrQ(T("_matrix")),
            "qr_r" => RunQrR(T("_matrix")),
            "eigen_val" => RunEigenVal(T("_matrix"), (int)N("eigenIter", 80)),
            "rk4" => RunRk4(N("odeK", 0.7), N("odeX0", 0), N("odeY0", 1), N("odeXf", 4), N("odeStep", 0.05)),
            "elliptic" => RunElliptic(N("ellK", 0.5)),
            _ => "Неизвестный ключ."
        };
    }

    private static string RunGauss(string matrixText, string vectorText)
    {
        var a0 = MathParseHelper.ParseMatrix(matrixText);
        var b0 = MathParseHelper.ParseVector(vectorText);
        if (b0.Count != a0.Height)
            throw new InvalidOperationException("Размер вектора b должен совпадать с числом строк A.");

        var a = a0.Copy();
        var b = b0.Clone();
        var x = Gauss.SolvingEquations(a, b);
        var sb = new StringBuilder();
        sb.AppendLine(MathParseHelper.FormatVector(x, "Решение x:"));
        return sb.ToString().Trim();
    }

    private static string RunKramer(string matrixText, string vectorText)
    {
        var a = MathParseHelper.ParseMatrix(matrixText);
        var b = MathParseHelper.ParseVector(vectorText);
        if (b.Count != a.Height || a.Height != a.Width)
            throw new InvalidOperationException("Крамер: нужна квадратная A и b размера n.");

        var k = new Kramer();
        var x = k.SolvingEquations(a.Copy(), b.Clone());
        return MathParseHelper.FormatVector(x, "Решение x:");
    }

    private static string RunCalculator(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            script = DefaultScript;

        var processor = new Processor();
        var lines = processor.Run(script);
        return string.Join("\n", lines);
    }

    private const string DefaultScript =
        """
        // Примеры: однострочные выражения
        sin(pi/2) + sqrt(16)
        2^10

        // Переменные и циклы
        n = 10
        s = 0
        for i = 1 to n:
            s += i
        s          // сумма 1..10
        """;


    private static (Vector target, Vector output) ParsePair(string vectorText, string vector2Text)
    {
        var t = MathParseHelper.ParseVector(vectorText);
        var o = MathParseHelper.ParseVector(vector2Text);
        if (t.Count != o.Count)
            throw new InvalidOperationException("Векторы target и output должны быть одинаковой длины.");
        return (t, o);
    }

    private static string Metric(string vectorText, string vector2Text,
        Func<IAlgebraicStructure<double>, IAlgebraicStructure<double>, double> fn)
    {
        var (t, o) = ParsePair(vectorText, vector2Text);
        var v = fn(t, o);
        return v.ToString("G12", CultureInfo.InvariantCulture);
    }

    private static string RunQrQ(string matrixText)
    {
        var a = MathParseHelper.ParseMatrix(matrixText);
        var q = QR.GetQ(a);
        return MathParseHelper.FormatMatrix(q, "Q:");
    }

    private static string RunQrR(string matrixText)
    {
        var a = MathParseHelper.ParseMatrix(matrixText);
        var q = QR.GetQ(a);
        var r = QR.GetR(a, q);
        return MathParseHelper.FormatMatrix(r, "R:");
    }

    private static string RunEigenVal(string matrixText, int eigenIterations)
    {
        var a = MathParseHelper.ParseMatrix(matrixText);
        if (a.Height != a.Width)
            throw new InvalidOperationException("Собственные значения считаются для квадратной матрицы.");

        var ev = new EigenValuesVectors(a.Copy(), eigenIterations, eps: 1e-2);
        var sb = new StringBuilder();
        sb.AppendLine("Сходимость: " + ev.IsConvergence + ", eps ≈ " + ev.Eps.ToString("G6", CultureInfo.InvariantCulture));
        sb.AppendLine(MathParseHelper.FormatVector(ev.Eigenvalues, "λ (диагональ предела QR):"));
        return sb.ToString().Trim();
    }

    private static string RunRk4(double odeK, double odeX0, double odeY0, double odeXf, double odeStep)
    {
        if (odeStep <= 0 || odeXf <= odeX0)
            throw new InvalidOperationException("Нужно step > 0 и x_final > x0.");

        var sol = RungeKutta.RungeKutta4((x, y) => -odeK * y, odeX0, odeY0, odeXf, odeStep);
        var sb = new StringBuilder();
        sb.AppendLine($"dy/dx = -{odeK.ToString(CultureInfo.InvariantCulture)}·y, y({odeX0.ToString(CultureInfo.InvariantCulture)}) = {odeY0.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Шаг {odeStep.ToString(CultureInfo.InvariantCulture)}, узлов: {sol.X.Count}");
        int n = sol.X.Count;
        if (n > 0)
        {
            sb.AppendLine("Первые 5 точек:");
            for (int i = 0; i < Math.Min(5, n); i++)
                sb.AppendLine($"  x={sol.X[i].ToString("G8", CultureInfo.InvariantCulture)}\t y={sol.Y[i].ToString("G8", CultureInfo.InvariantCulture)}");
            sb.AppendLine("Последняя точка:");
            sb.AppendLine($"  x={sol.X[n - 1].ToString("G8", CultureInfo.InvariantCulture)}\t y={sol.Y[n - 1].ToString("G8", CultureInfo.InvariantCulture)}");
        }
        return sb.ToString().Trim();
    }

    private static string RunElliptic(double ellipticK)
    {
        if (ellipticK < 0 || ellipticK >= 1)
            throw new InvalidOperationException("Параметр k должен быть в [0, 1).");
        var v = EllipticIntegral.CompleteEllipticIntegral_I(ellipticK);
        return "K(" + ellipticK.ToString("G10", CultureInfo.InvariantCulture) + ") = " + v.ToString("G14", CultureInfo.InvariantCulture);
    }
}

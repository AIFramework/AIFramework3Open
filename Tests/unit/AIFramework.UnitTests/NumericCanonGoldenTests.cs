using System.Text.Json;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using AI.MathUtils.Nonlinear;
using AI.MathUtils.ODE;
using AI.Solvers.Chem.Kinetics;
using AI.Statistics.Distributions;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Численные методы против scipy: корни, ОДУ, матричная экспонента, Холецкий, прогонка, МНК.
/// </summary>
/// <remarks>
/// Эталоны выгружает Tools/MathGolden/export_numeric_golden.py в Golden/numeric.json. Здесь же случаи,
/// на которых прежние копии ошибались: корень функции малого масштаба, лишний шаг Рунге — Кутты,
/// «разложение» матрицы, не являющейся положительно определённой.
/// </remarks>
public class NumericCanonGoldenTests
{
    private static readonly Lazy<JsonElement> s_data = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "numeric.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    });

    private static readonly Dictionary<string, Func<double, double>> s_rootFunctions = new(StringComparer.Ordinal)
    {
        ["cubic"] = x => (x * x * x) - (2 * x) - 5,
        ["cos_minus_x"] = x => Math.Cos(x) - x,
        ["tiny_scale"] = x => 1e-12 * (x - 0.3),
        ["exp_minus_million"] = x => Math.Exp(x) - 1e6,
        ["flat_near_root"] = x => Math.Pow(x - 1.5, 3),
        ["root_far_from_one"] = x => x - (1e9 / 3),
    };

    // --- корни ---

    [Fact]
    public void Brent_MatchesScipyBrentq()
    {
        foreach (JsonElement item in s_data.Value.GetProperty("roots").EnumerateArray())
        {
            string name = Name(item);
            double expected = item.GetProperty("root").GetDouble();
            RootResult result = RootFinder.Brent(s_rootFunctions[name], item.GetProperty("a").GetDouble(), item.GetProperty("b").GetDouble());

            Assert.True(result.Converged, name);

            // У тройного корня функция неотличима от нуля в полосе шириной ∛ε вокруг него
            double tolerance = name == "flat_near_root" ? 1e-5 : 1e-11;

            AssertClose(expected, result.Root, tolerance, name);
        }
    }

    [Fact]
    public void Brent_OnTinyScaleFunction_FindsRootNotFirstPoint()
    {
        // Прежний критерий «|f(x)| меньше точности» выполнялся в первой же точке: 1e-12·(0.5 − 0.3) < 1e-6
        Func<double, double> function = x => 1e-12 * (x - 0.3);

        Assert.Equal(0.3, RootFinder.Solve(function, 0, 1), 11);
        Assert.True(RootFinder.TrySolve(function, 0, 1, out double root));
        Assert.Equal(0.3, root, 11);
    }

    [Fact]
    public void Brent_WithoutSignChange_RefusesAndTrySolveReturnsFalse()
    {
        Assert.Throws<ArgumentException>(() => RootFinder.Brent(x => (x * x) + 1, -1, 1));
        Assert.False(RootFinder.TrySolve(x => (x * x) + 1, -1, 1, out _));
        Assert.Throws<ArgumentException>(() => RootFinder.Brent(_ => double.NaN, -1, 1));
    }

    [Fact]
    public void FindAll_ReturnsEveryRootOnInterval()
    {
        IReadOnlyList<double> roots = RootFinder.FindAll(Math.Sin, 0.5, 10);

        Assert.Equal(3, roots.Count);
        Assert.Equal(Math.PI, roots[0], 10);
        Assert.Equal(2 * Math.PI, roots[1], 10);
        Assert.Equal(3 * Math.PI, roots[2], 10);
    }

    [Fact]
    public void Boundary_FindsEdgeOfStepPredicate()
    {
        // Ступенька: непрерывности нет, корня в обычном смысле нет — есть порог
        Assert.Equal(0.37, RootFinder.Boundary(x => x < 0.37, 0, 1), 11);

        (double low, double high) = RootFinder.BoundaryBracket(x => x < 0.37, 0, 1, xTolerance: 0, relativeTolerance: 0);

        Assert.True(low < 0.37 && high >= 0.37);
        Assert.Equal(Math.BitIncrement(low), high);
    }

    [Fact]
    public void Boundary_OnConstantPredicate_ConvergesToMatchingEnd()
    {
        Assert.Equal(5, RootFinder.Boundary(_ => true, 2, 5), 10);
        Assert.Equal(2, RootFinder.Boundary(_ => false, 2, 5), 10);
    }

    // --- ОДУ ---

    [Fact]
    public void RungeKutta4_OnGrowthEquation_EndsAtEulerNumber()
    {
        // y' = y на [0; 1] с шагом 0.1: прежде накопление x += h давало лишний шаг и e^1.1 = 3.004…
        RungeKutta solution = RungeKutta.RungeKutta4((_, y) => y, 0, 1, 1, 0.1);

        Assert.Equal(11, solution.X.Count);
        Assert.Equal(solution.X.Count, solution.Y.Count);
        Assert.Equal(0.0, solution.X[0]);
        Assert.Equal(1.0, solution.Y[0]);
        Assert.Equal(1.0, solution.X[^1]);
        Assert.Equal(Math.E, solution.Y[^1], 5);

        for (int i = 0; i < solution.X.Count; i++)
            Assert.Equal(Math.Exp(solution.X[i]), solution.Y[i], 5);
    }

    [Fact]
    public void RungeKutta4_WhenStepDoesNotDivideInterval_LandsExactlyOnEnd()
    {
        RungeKutta solution = RungeKutta.RungeKutta4((_, y) => y, 0, 1, 1, 0.3);

        Assert.Equal(1.0, solution.X[^1]);
        Assert.Equal(5, solution.X.Count);
        Assert.Equal(Math.E, solution.Y[^1], 3);
    }

    [Fact]
    public void RungeKutta4_HalfStepGrid_MatchesFullStepGrid()
    {
        RungeKutta coarse = RungeKutta.RungeKutta4((x, y) => Math.Sin(x) - y, 0, 0.5, 3, 0.1);
        RungeKutta fine = RungeKutta.RungeKutta4((x, y) => Math.Sin(x) - y, 0, 0.5, 3, 0.05, isHalfStep: true);

        Assert.Equal(coarse.X.Count, fine.X.Count);

        for (int i = 0; i < coarse.X.Count; i++)
        {
            Assert.Equal(coarse.X[i], fine.X[i], 12);
            Assert.True(Math.Abs(coarse.Y[i] - fine.Y[i]) < 1e-6);
        }
    }

    [Fact]
    public void RungeKutta4_MatchesScipySolveIvp()
    {
        var scalar = new Dictionary<string, Func<double, double, double>>(StringComparer.Ordinal)
        {
            ["growth"] = (_, y) => y,
            ["gaussian"] = (x, y) => -2 * x * y,
            ["forced"] = (x, y) => Math.Sin(x) - y,
        };

        foreach (JsonElement item in s_data.Value.GetProperty("odes").EnumerateArray())
        {
            string name = Name(item);
            double x0 = item.GetProperty("x0").GetDouble(), x1 = item.GetProperty("x1").GetDouble();
            double[] y0 = Doubles(item, "y0"), expected = Doubles(item, "y1");

            if (scalar.TryGetValue(name, out Func<double, double, double>? function))
            {
                RungeKutta solution = RungeKutta.RungeKutta4(function, x0, y0[0], x1, 0.01);

                Assert.Equal(x1, solution.X[^1]);
                AssertClose(expected[0], solution.Y[^1], 1e-8, name);
            }
            else
            {
                Vector[] solution = RungeKutta.SolveSystem(
                    (_, y) => new Vector(y[1], -y[0]), x0, new Vector(y0), [x1], stepsPerInterval: 500);

                AssertClose(expected[0], solution[0][0], 1e-8, name);
                AssertClose(expected[1], solution[0][1], 1e-8, name);
            }
        }
    }

    [Fact]
    public void RungeKuttaStep_ScalarAndVector_Agree()
    {
        double scalar = RungeKutta.Step((x, y) => (x * y) + 1, 0.3, 2, 0.05);
        Vector vector = RungeKutta.Step((x, y) => new Vector((x * y[0]) + 1), 0.3, new Vector(2.0), 0.05);

        Assert.Equal(scalar, vector[0], 14);
    }

    // --- матричная экспонента ---

    [Fact]
    public void MatrixExponential_MatchesScipyExpm()
    {
        foreach (JsonElement item in s_data.Value.GetProperty("expm").EnumerateArray())
        {
            Matrix expected = ToMatrix(item.GetProperty("exp"));
            Matrix actual = MatrixExponential.Compute(ToMatrix(item.GetProperty("matrix")));
            double scale = 0;

            for (int i = 0; i < expected.Data.Length; i++) scale = Math.Max(scale, Math.Abs(expected.Data[i]));

            for (int i = 0; i < expected.Height; i++)
            {
                for (int j = 0; j < expected.Width; j++)
                {
                    Assert.True(
                        Math.Abs(actual[i, j] - expected[i, j]) <= 1e-12 * Math.Max(1, scale),
                        $"{Name(item)}[{i},{j}]: ожидалось {expected[i, j]:R}, получено {actual[i, j]:R}");
                }
            }
        }
    }

    [Fact]
    public void MatrixExponential_RejectsNonSquareAndNotNumbers()
    {
        Assert.Throws<ArgumentException>(() => MatrixExponential.Compute(new Matrix(2, 3)));
        Assert.Throws<ArgumentException>(() => MatrixExponential.Compute(new Matrix(new[,] { { double.NaN, 0 }, { 0, 1.0 } })));
    }

    // --- Холецкий ---

    [Fact]
    public void Cholesky_MatchesScipy()
    {
        JsonElement item = s_data.Value.GetProperty("cholesky");
        Matrix matrix = ToMatrix(item.GetProperty("matrix"));
        Matrix lower = CholeskyDecomposition.Decompose(matrix);

        AssertMatrix(ToMatrix(item.GetProperty("lower")), lower, 1e-12, "L");
        AssertMatrix(ToMatrix(item.GetProperty("inverse")), CholeskyDecomposition.InverseFromFactor(lower), 1e-11, "A⁻¹");
        AssertClose(item.GetProperty("logdet").GetDouble(), CholeskyDecomposition.LogDeterminantFromFactor(lower), 1e-12, "ln|A|");

        double[] expected = Doubles(item, "solution");
        Vector solution = CholeskyDecomposition.Solve(matrix, new Vector(Doubles(item, "rhs")));

        for (int i = 0; i < expected.Length; i++) AssertClose(expected[i], solution[i], 1e-11, $"x[{i}]");

        RegularizedCholesky regular = CholeskyDecomposition.DecomposeRegularized(matrix);

        Assert.False(regular.Regularized);
        Assert.Equal(0.0, regular.Shift);
        AssertMatrix(lower, regular.Lower, 0, "L без сдвига");
    }

    [Fact]
    public void Cholesky_OnNotPositiveDefinite_RefusesInsteadOfReturningFactor()
    {
        JsonElement item = s_data.Value.GetProperty("cholesky");
        Matrix indefinite = ToMatrix(item.GetProperty("indefinite"));
        Matrix singular = ToMatrix(item.GetProperty("singular"));

        Assert.Throws<InvalidOperationException>(() => CholeskyDecomposition.Decompose(indefinite));
        Assert.False(CholeskyDecomposition.TryDecompose(indefinite, out _));
        Assert.Throws<InvalidOperationException>(() => CholeskyDecomposition.Decompose(singular));
        Assert.Throws<ArgumentException>(() => CholeskyDecomposition.Decompose(new Matrix(2, 3)));

#pragma warning disable CS0618 // Старое имя проверяется намеренно: оно обязано вести себя как канон
        Assert.Throws<InvalidOperationException>(() => Cholesky.Decompose(indefinite));
#pragma warning restore CS0618
    }

    [Fact]
    public void CholeskyRegularized_ReportsShiftAndFactorsShiftedMatrix()
    {
        Matrix singular = ToMatrix(s_data.Value.GetProperty("cholesky").GetProperty("singular"));
        RegularizedCholesky result = CholeskyDecomposition.DecomposeRegularized(singular);

        Assert.True(result.Regularized);
        Assert.True(result.Shift > 0 && result.Shift < 1e-6);

        Matrix product = result.Lower * result.Lower.Transpose();

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                Assert.Equal(singular[i, j] + (i == j ? result.Shift : 0), product[i, j], 12);
        }

        // Матрица с отрицательным собственным числом порядка единицы малым сдвигом не лечится — это отказ
        Assert.Throws<InvalidOperationException>(
            () => CholeskyDecomposition.DecomposeRegularized(new Matrix(new[,] { { 1.0, 2.0 }, { 2.0, 1.0 } })));
    }

    [Fact]
    public void GaussianFullCovariance_UsesCovarianceAsIs()
    {
        // Прежде к диагонали всегда добавлялось 1e-6, и плотность стандартной гауссианы в нуле
        // выходила −1.83787807 вместо −ln 2π = −1.83787707
        var standard = new GaussianDistFullCov(new Vector(0.0, 0.0), new[,] { { 1.0, 0.0 }, { 0.0, 1.0 } });

        Assert.Equal(0.0, standard.CovarianceShift);
        Assert.Equal(-Math.Log(2 * Math.PI), standard.CulcLogProb(new Vector(0.0, 0.0)), 14);

        var correlated = new GaussianDistFullCov(new Vector(1.0, -1.0), new[,] { { 2.0, 0.6 }, { 0.6, 0.5 } });

        // Плотность двумерной гауссианы в точке (2, −1.5), посчитанная по определению
        double determinant = (2.0 * 0.5) - (0.6 * 0.6);
        double quadratic = ((0.5 * 1 * 1) - (2 * 0.6 * 1 * -0.5) + (2.0 * 0.25)) / determinant;
        double expected = (-0.5 * quadratic) - (0.5 * Math.Log(determinant)) - Math.Log(2 * Math.PI);

        Assert.Equal(expected, correlated.CulcLogProb(new Vector(2.0, -1.5)), 12);

        var degenerate = new GaussianDistFullCov(new Vector(0.0, 0.0), new[,] { { 1.0, 1.0 }, { 1.0, 1.0 } });

        Assert.True(degenerate.CovarianceShift > 0);
    }

    // --- прогонка и сплайн ---

    [Fact]
    public void Tridiagonal_MatchesScipySolveBanded()
    {
        JsonElement item = s_data.Value.GetProperty("tridiagonal");
        double[] expected = Doubles(item, "solution");
        double[] lower = Doubles(item, "lower"), diagonal = Doubles(item, "diagonal"), upper = Doubles(item, "upper"), rhs = Doubles(item, "rhs");

        Vector solution = Tridiagonal.Solve(new Vector(lower), new Vector(diagonal), new Vector(upper), new Vector(rhs));

#pragma warning disable CS0618 // Старое имя проверяется намеренно
        Vector adapter = AI.Solvers.Pde.Numerics.IterativeSolvers.SolveTridiagonal(
            new Vector(lower), new Vector(diagonal), new Vector(upper), new Vector(rhs));
#pragma warning restore CS0618

        for (int i = 0; i < expected.Length; i++)
        {
            AssertClose(expected[i], solution[i], 1e-13, $"x[{i}]");
            Assert.Equal(solution[i], adapter[i]);
        }
    }

    [Fact]
    public void Tridiagonal_RefusesSingularAndMismatchedInput()
    {
        Assert.Throws<ArgumentException>(() => Tridiagonal.Solve([0.0, 1.0], [0.0, 1.0], [1.0, 0.0], [1.0, 1.0]));
        Assert.Throws<ArgumentException>(() => Tridiagonal.Solve([0.0, 1.0], [1.0, 1.0], [1.0, 0.0], [1.0, 1.0]));
        Assert.Throws<ArgumentException>(() => Tridiagonal.Solve([0.0], [1.0, 1.0], [1.0, 0.0], [1.0, 1.0]));
    }

    [Fact]
    public void CubicSpline_MatchesScipyNaturalSpline()
    {
        JsonElement item = s_data.Value.GetProperty("spline");
        var spline = new CubicSplineInterpolator(Doubles(item, "x"), Doubles(item, "y"));
        double[] probes = Doubles(item, "probes"), expected = Doubles(item, "values");

        for (int i = 0; i < probes.Length; i++)
            AssertClose(expected[i], spline.Interpolate(probes[i]), 1e-12, $"s({probes[i]})");

        // Правый узел: прежде обращение к нему выходило за границы массива коэффициентов
        Assert.Equal(Doubles(item, "y")[^1], spline.Interpolate(Doubles(item, "x")[^1]), 12);
    }

    // --- дифференцирование ---

    [Fact]
    public void Derivative_CentralAndForward_MatchAnalytic()
    {
        Assert.Equal(Math.Cos(1), NumericalDifferentiation.Derivative(Math.Sin, 1), 9);
        Assert.True(Math.Abs(NumericalDifferentiation.Derivative(Math.Sin, 1, 1e-4) - Math.Cos(1)) < 2e-9);
        Assert.Equal(Math.Cos(1), NumericalDifferentiation.ForwardDerivative(Math.Sin, 1), 6);

        // Масштаб точки учитывается: относительная ошибка та же при x = 1e6
        double large = NumericalDifferentiation.Derivative(x => x * x, 1e6);

        Assert.Equal(2e6, large, 2);

        // Справа от излома односторонняя разность даёт наклон правой ветви, центральная — среднее двух
        Assert.Equal(3.0, NumericalDifferentiation.ForwardDerivative(x => x > 0 ? 3 * x : 0, 0), 9);
        Assert.Equal(1.5, NumericalDifferentiation.Derivative(x => x > 0 ? 3 * x : 0, 0), 9);
    }

    [Fact]
    public void JacobianAndGradient_MatchAnalytic()
    {
        double[] point = [1.5, -2.0, 0.5];
        double[,] jacobian = NumericalDifferentiation.Jacobian(
            x => [x[0] * x[0], x[0] * x[1], Math.Exp(x[2]) + x[1]], point, 3);
        double[,] expected =
        {
            { 3.0, 0, 0 },
            { -2.0, 1.5, 0 },
            { 0, 1, Math.Exp(0.5) },
        };

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.Equal(expected[i, j], jacobian[i, j], 9);

        double[] gradient = NumericalDifferentiation.Gradient(x => (x[0] * x[0] * x[1]) + Math.Sin(x[2]), point);

        Assert.Equal(2 * 1.5 * -2.0, gradient[0], 9);
        Assert.Equal(1.5 * 1.5, gradient[1], 9);
        Assert.Equal(Math.Cos(0.5), gradient[2], 9);
        Assert.Equal([1.5, -2.0, 0.5], point);

        Assert.Throws<InvalidOperationException>(() => NumericalDifferentiation.Jacobian(x => [x[0]], point, 2));
    }

    [Fact]
    public void Hessian_MatchesAnalytic_IncludingDiagonal()
    {
        // f = x²·y + exp(2y) + 3xy
        double[] point = [1.2, 0.4];
        double[,] hessian = NumericalDifferentiation.Hessian(
            x => (x[0] * x[0] * x[1]) + Math.Exp(2 * x[1]) + (3 * x[0] * x[1]), point);

        double xx = 2 * 0.4, xy = (2 * 1.2) + 3, yy = 4 * Math.Exp(0.8);

        Assert.Equal(xx, hessian[0, 0], 6);
        Assert.Equal(xy, hessian[0, 1], 6);
        Assert.Equal(xy, hessian[1, 0], 6);

        // Диагональ по трём точкам с шагом h; четырёхточечная формула дала бы шаг 2h и вчетверо большую ошибку
        Assert.True(Math.Abs(hessian[1, 1] - yy) < 2e-7 * yy, $"∂²f/∂y²: ожидалось {yy:R}, получено {hessian[1, 1]:R}");
        Assert.Equal([1.2, 0.4], point);
    }

    // --- нелинейные наименьшие квадраты ---

    [Fact]
    public void LevenbergMarquardt_AndChemNonlinearFit_MatchScipyLeastSquares()
    {
        JsonElement item = s_data.Value.GetProperty("least_squares");
        double[] t = Doubles(item, "t"), y = Doubles(item, "y"), start = Doubles(item, "start"), expected = Doubles(item, "parameters");
        double cost = item.GetProperty("cost").GetDouble();

        double[] Residuals(double[] p)
        {
            var result = new double[t.Length];

            for (int i = 0; i < t.Length; i++) result[i] = (p[0] * Math.Exp(-p[1] * t[i])) + p[2] - y[i];

            return result;
        }

        LeastSquaresResult canon = LevenbergMarquardt.Solve(p => new Vector(Residuals(p.ToArray())), new Vector(start));

        Assert.True(canon.Converged);
        AssertClose(cost, canon.Cost, 1e-9, "сумма квадратов");

        for (int i = 0; i < expected.Length; i++) AssertClose(expected[i], canon.Solution[i], 1e-6, $"p[{i}]");

        // Подгонка в химии — тот же спуск: без отжига она обязана прийти в ту же точку
        NonlinearFitResult chem = NonlinearFit.Fit(Residuals, start, options: new NonlinearFitOptions { AnnealingIterations = 0 });

        Assert.True(chem.Converged);
        AssertClose(cost, chem.ResidualSumOfSquares, 1e-9, "сумма квадратов (химия)");

        for (int i = 0; i < expected.Length; i++)
        {
            AssertClose(expected[i], chem.Parameters[i], 1e-6, $"p[{i}] (химия)");
            Assert.True(chem.StandardErrors[i] > 0 && chem.StandardErrors[i] < 0.1);
        }
    }

    // --- служебное ---

    private static string Name(JsonElement item) => item.GetProperty("name").GetString()!;

    private static double[] Doubles(JsonElement item, string property) =>
        [.. item.GetProperty(property).EnumerateArray().Select(value => value.GetDouble())];

    private static Matrix ToMatrix(JsonElement rows)
    {
        double[][] data = [.. rows.EnumerateArray().Select(row => row.EnumerateArray().Select(value => value.GetDouble()).ToArray())];
        var matrix = new Matrix(data.Length, data[0].Length);

        for (int i = 0; i < data.Length; i++)
            for (int j = 0; j < data[i].Length; j++)
                matrix[i, j] = data[i][j];

        return matrix;
    }

    private static void AssertMatrix(Matrix expected, Matrix actual, double tolerance, string what)
    {
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Width, actual.Width);

        for (int i = 0; i < expected.Height; i++)
            for (int j = 0; j < expected.Width; j++)
                AssertClose(expected[i, j], actual[i, j], tolerance, $"{what}[{i},{j}]");
    }

    private static void AssertClose(double expected, double actual, double tolerance, string what) =>
        Assert.True(
            Math.Abs(actual - expected) <= tolerance * Math.Max(1.0, Math.Abs(expected)),
            $"{what}: ожидалось {expected:R}, получено {actual:R}");
}

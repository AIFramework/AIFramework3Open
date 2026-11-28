using System.Text;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>
/// Численные методы решения уравнений
/// </summary>
public static class NumericalEquationSolver
{
    /// <summary>
    /// Метод Ньютона (касательных) для поиска корня уравнения f(x) = 0
    /// </summary>
    public static (bool success, double root, int iterations) Newton(
        Func<double, double> f,
        Func<double, double> df,
        double x0,
        double tolerance = 1e-6,
        int maxIterations = 100)
    {
        double x = x0;

        for (int i = 0; i < maxIterations; i++)
        {
            double fx = f(x);

            if (System.Math.Abs(fx) < tolerance)
                return (true, x, i + 1);

            double dfx = df(x);

            if (System.Math.Abs(dfx) < 1e-12)
                return (false, x, i + 1);

            // Итерация метода Ньютона
            double xNew = x - fx / dfx;

            if (System.Math.Abs(xNew - x) < tolerance)
                return (true, xNew, i + 1);

            x = xNew;

            if (System.Math.Abs(x) > 1e10)
                return (false, x, i + 1);
        }

        return (false, x, maxIterations);
    }

    /// <summary>Корень на отрезке со сменой знака.</summary>
    /// <remarks>
    /// Считает <see cref="AI.HighLevelFunctions.RootFinder.Brent"/>; <paramref name="tolerance"/> — точность
    /// по аргументу. Прежний критерий «|f(x)| меньше точности» убран: у функции малого масштаба он
    /// выполнялся в первой же точке.
    /// </remarks>
    [Obsolete("Используйте AI.HighLevelFunctions.RootFinder.Brent / Solve / TrySolve — единственный решатель корней.")]
    public static (bool success, double root, int iterations) Bisection(
        Func<double, double> f,
        double a,
        double b,
        double tolerance = 1e-6,
        int maxIterations = 100)
    {
        double fa = f(a), fb = f(b);

        if (double.IsNaN(fa) || double.IsNaN(fb) || fa * fb > 0)
            return (false, (a + b) / 2, 0);

        AI.HighLevelFunctions.RootResult result = AI.HighLevelFunctions.RootFinder.Brent(f, a, b, tolerance, maxIterations: maxIterations);

        return (result.Converged, result.Root, result.Iterations);
    }

    /// <summary>
    /// Метод секущих для поиска корня
    /// </summary>
    public static (bool success, double root, int iterations) Secant(
        Func<double, double> f,
        double x0,
        double x1,
        double tolerance = 1e-6,
        int maxIterations = 100)
    {
        double fx0 = f(x0);
        double fx1 = f(x1);

        for (int i = 0; i < maxIterations; i++)
        {
            if (System.Math.Abs(fx1) < tolerance)
                return (true, x1, i + 1);

            if (System.Math.Abs(fx1 - fx0) < 1e-12)
                return (false, x1, i + 1);

            double x2 = x1 - fx1 * (x1 - x0) / (fx1 - fx0);

            if (System.Math.Abs(x2 - x1) < tolerance)
                return (true, x2, i + 1);

            x0 = x1;
            fx0 = fx1;
            x1 = x2;
            fx1 = f(x2);

            if (System.Math.Abs(x1) > 1e10)
                return (false, x1, i + 1);
        }

        return (false, x1, maxIterations);
    }

    /// <summary>Все корни на отрезке по сетке проб.</summary>
    [Obsolete("Используйте AI.HighLevelFunctions.RootFinder.FindAll — единственный решатель корней.")]
    public static List<double> FindAllRoots(
        Func<double, double> f,
        double a,
        double b,
        int intervals = 100,
        double tolerance = 1e-6)
        => [.. AI.HighLevelFunctions.RootFinder.FindAll(f, a, b, intervals, tolerance)];

    /// <summary>Численная производная центральной разностью.</summary>
    /// <param name="h">Шаг; при h ≤ 0 подбирается автоматически по масштабу точки.</param>
    [Obsolete("Используйте AI.HighLevelFunctions.NumericalDifferentiation.Derivative — единственную разностную производную.")]
    public static Func<double, double> NumericalDerivative(Func<double, double> f, double h = 0)
        => x => AI.HighLevelFunctions.NumericalDifferentiation.Derivative(f, x, h);

    /// <summary>
    /// Решение уравнения с автоматическим выбором метода и начального приближения
    /// </summary>
    public static string SolveNumerically(Expression leftExpr, Expression rightExpr, string variable)
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ УРАВНЕНИЯ ===");
            result.AppendLine();
            result.AppendLine($"Уравнение: {leftExpr} = {rightExpr}");
            result.AppendLine($"Переменная: {variable}");
            result.AppendLine();

            // Создаем функцию f(x) = left - right
            var diffExpr = new Add(leftExpr, new Multiply(new Constant(-1), rightExpr)).Simplify();
            result.AppendLine($"Эквивалентно: {diffExpr} = 0");
            result.AppendLine();

            Func<double, double> f = x =>
            {
                var vars = new Dictionary<string, double> { { variable, x } };
                try
                {
                    return EvaluateExpression(diffExpr, vars);
                }
                catch
                {
                    // f(x) не определена в этой точке (область определения,
                    // деление на 0 и т.д.) — возвращаем NaN, чтобы поиск
                    // корней корректно пропустил точку.
                    return double.NaN;
                }
            };

            // Создаем численную производную
            Func<double, double> df = x => AI.HighLevelFunctions.NumericalDifferentiation.Derivative(f, x);

            // Ищем корни на нескольких интервалах
            var searchIntervals = new List<(double a, double b)>
            {
                (-10, 10),    // Основной интервал
                (-100, -10),  // Отрицательные значения
                (10, 100)     // Большие положительные значения
            };

            var allRoots = new List<double>();

            foreach (var (a, b) in searchIntervals)
            {
                var roots = AI.HighLevelFunctions.RootFinder.FindAll(f, a, b, samples: 200, xTolerance: 1e-7);
                allRoots.AddRange(roots);
            }

            // Удаляем дубликаты
            allRoots = allRoots
                .OrderBy(r => r)
                .Distinct()
                .Where(r => !double.IsNaN(r) && !double.IsInfinity(r))
                .ToList();

            if (allRoots.Count > 0)
            {
                result.AppendLine($"Метод: Бисекция + Ньютон");
                result.AppendLine($"Диапазон поиска: [-100, 100]");
                result.AppendLine($"Точность: ε = 1e-7");
                result.AppendLine();
                result.AppendLine($"НАЙДЕНО КОРНЕЙ: {allRoots.Count}");
                result.AppendLine();

                for (int i = 0; i < System.Math.Min(allRoots.Count, 10); i++)
                {
                    double root = allRoots[i];

                    // Уточнение принимаем, только если Ньютон остался у того же корня
                    // и стал ближе к нулю: иначе он уводит на соседний корень,
                    // и локализованный бисекцией результат подменяется чужим.
                    var (success, refinedRoot, iterations) = Newton(f, df, root, tolerance: 1e-12);
                    if (success &&
                        System.Math.Abs(refinedRoot - root) < 1e-3 &&
                        System.Math.Abs(f(refinedRoot)) <= System.Math.Abs(f(root)))
                        root = refinedRoot;

                    double fRoot = f(root);

                    result.AppendLine($"  {variable}_{i + 1} = {root:G10}");
                    result.AppendLine($"      Проверка: f({root:G10}) = {fRoot:E3}");
                    result.AppendLine();
                }

                if (allRoots.Count > 10)
                {
                    result.AppendLine($"... и ещё {allRoots.Count - 10} корней");
                    result.AppendLine();
                }
            }
            else
            {
                result.AppendLine("Корни не найдены в диапазоне [-100, 100]");
                result.AppendLine();
                result.AppendLine("Попытка метода Ньютона с различными начальными приближениями:");

                var startPoints = new[] { 0.0, 1.0, -1.0, 5.0, -5.0, 10.0, -10.0 };
                bool foundAny = false;

                foreach (var x0 in startPoints)
                {
                    var (success, root, iterations) = Newton(f, df, x0);
                    if (success && System.Math.Abs(f(root)) < 1e-6)
                    {
                        // Проверяем, что не дубликат
                        bool isDuplicate = allRoots.Any(r => System.Math.Abs(r - root) < 1e-6);
                        if (!isDuplicate)
                        {
                            allRoots.Add(root);
                            result.AppendLine($"  {variable} ≈ {root:G10}  (x₀ = {x0}, {iterations} итераций)");
                            foundAny = true;
                        }
                    }
                }

                if (!foundAny)
                {
                    result.AppendLine("  Корни не найдены");
                    result.AppendLine();
                    result.AppendLine("Возможные причины:");
                    result.AppendLine("  * Уравнение не имеет действительных корней");
                    result.AppendLine("  * Корни находятся вне диапазона поиска");
                    result.AppendLine("  * Уравнение имеет только комплексные корни");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Ошибка численного решения: {ex.Message}";
        }
    }

    private static double EvaluateExpression(Expression expr, Dictionary<string, double> variables)
        => ExpressionEvaluator.Evaluate(expr, variables);
}


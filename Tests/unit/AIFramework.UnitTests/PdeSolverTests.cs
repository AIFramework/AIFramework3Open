using AI.DataStructs.Algebraic;
using AI.Solvers.Pde;
using AI.Solvers.Pde.FiniteDifference;
using AI.Solvers.Pde.FiniteElement;
using AI.Solvers.Pde.Numerics;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Решатели уравнений в частных производных проверяются точными решениями и порядком
/// сходимости: измельчение сетки вдвое обязано уменьшать ошибку вчетверо у схем
/// второго порядка. Совпадение с точным решением на одной сетке ничего не доказывает —
/// оно бывает и при неверной схеме с удачно подобранным шагом.
/// </summary>
public class PdeSolverTests
{
    private static double MaxError(Vector values, Grid1D grid, Func<double, double> exact)
    {
        double worst = 0;

        for (int i = 0; i < grid.Count; i++)
            worst = Math.Max(worst, Math.Abs(values[i] - exact(grid.Node(i))));

        return worst;
    }

    #region Линейная алгебра

    [Fact]
    public void ConjugateGradient_SolvesSmallSystem()
    {
        var matrix = new SparseMatrix(3, 3);
        matrix.Add(0, 0, 4); matrix.Add(0, 1, 1);
        matrix.Add(1, 0, 1); matrix.Add(1, 1, 3); matrix.Add(1, 2, 1);
        matrix.Add(2, 1, 1); matrix.Add(2, 2, 2);

        var right = new Vector(1.0, 2.0, 3.0);
        IterativeResult result = IterativeSolvers.ConjugateGradient(matrix, right);

        Assert.True(result.Converged);

        Vector check = matrix.Multiply(result.Solution);

        for (int i = 0; i < 3; i++)
            Assert.Equal(right[i], check[i], tolerance: 1e-9);
    }

    [Fact]
    public void EliminateKnown_KeepsMatrixSymmetric()
    {
        var matrix = new SparseMatrix(3, 3);
        matrix.Add(0, 0, 2); matrix.Add(0, 1, -1);
        matrix.Add(1, 0, -1); matrix.Add(1, 1, 2); matrix.Add(1, 2, -1);
        matrix.Add(2, 1, -1); matrix.Add(2, 2, 2);

        var right = new Vector(0.0, 1.0, 0.0);
        matrix.EliminateKnown(0, 5.0, right);

        // Столбец исключённого неизвестного обнуляется вместе со строкой
        Assert.Equal(0.0, matrix[1, 0]);
        Assert.Equal(0.0, matrix[0, 1]);
        Assert.Equal(1.0, matrix[0, 0]);
        Assert.Equal(5.0, right[0]);
        Assert.Equal(6.0, right[1], tolerance: 1e-12);   // 1 - (-1)·5
    }

    [Fact]
    public void Tridiagonal_SolvesKnownSystem()
    {
        var lower = new Vector(0.0, 1.0, 1.0);
        var diagonal = new Vector(2.0, 2.0, 2.0);
        var upper = new Vector(1.0, 1.0, 0.0);
        var right = new Vector(4.0, 8.0, 8.0);

        Vector solution = IterativeSolvers.SolveTridiagonal(lower, diagonal, upper, right);

        Assert.Equal(1.0, solution[0], tolerance: 1e-12);
        Assert.Equal(2.0, solution[1], tolerance: 1e-12);
        Assert.Equal(3.0, solution[2], tolerance: 1e-12);
    }

    #endregion

    #region Теплопроводность

    [Fact]
    public void Heat_CrankNicolson_MatchesAnalyticSolution()
    {
        // u_t = α·u_xx, u(x,0) = sin(πx), u(0,t) = u(1,t) = 0
        // Точное решение: u = exp(−απ²t)·sin(πx)
        const double Alpha = 0.5;
        const double Time = 0.1;

        var grid = new Grid1D(0, 1, 201);

        HeatSolution solution = HeatEquation1D.Solve(
            grid, Alpha,
            x => Math.Sin(Math.PI * x),
            _ => 0, _ => 0,
            Time, steps: 200);

        double decay = Math.Exp(-Alpha * Math.PI * Math.PI * Time);

        Assert.True(solution.IsStable);
        Assert.True(MaxError(solution.Values, grid, x => decay * Math.Sin(Math.PI * x)) < 1e-4);
    }

    [Fact]
    public void Heat_CrankNicolson_IsSecondOrderAccurate()
    {
        const double Alpha = 0.5;
        const double Time = 0.1;

        double Error(int nodes, int steps)
        {
            var grid = new Grid1D(0, 1, nodes);

            HeatSolution solution = HeatEquation1D.Solve(
                grid, Alpha, x => Math.Sin(Math.PI * x), _ => 0, _ => 0, Time, steps);

            double decay = Math.Exp(-Alpha * Math.PI * Math.PI * Time);

            return MaxError(solution.Values, grid, x => decay * Math.Sin(Math.PI * x));
        }

        double coarse = Error(21, 20);
        double fine = Error(41, 40);

        // Второй порядок: измельчение вдвое уменьшает ошибку примерно вчетверо
        Assert.True(coarse / fine > 3.5, $"Отношение ошибок {coarse / fine:F2} ниже ожидаемого для второго порядка");
    }

    [Fact]
    public void Heat_ExplicitScheme_ReportsInstability()
    {
        var grid = new Grid1D(0, 1, 51);

        HeatSolution stable = HeatEquation1D.Solve(
            grid, 1.0, x => Math.Sin(Math.PI * x), _ => 0, _ => 0,
            finalTime: 0.01, steps: 2000, TimeScheme.Explicit);

        HeatSolution unstable = HeatEquation1D.Solve(
            grid, 1.0, x => Math.Sin(Math.PI * x), _ => 0, _ => 0,
            finalTime: 0.01, steps: 10, TimeScheme.Explicit);

        Assert.True(stable.IsStable);
        Assert.True(stable.Courant <= 0.5);
        Assert.False(unstable.IsStable);
    }

    [Fact]
    public void Heat_SteadySource_ApproachesSteadyState()
    {
        // При постоянном источнике и нулевых границах решение сходится к −f·x(x−1)/(2α)
        const double Alpha = 1.0;
        const double Source = 2.0;

        var grid = new Grid1D(0, 1, 101);

        HeatSolution solution = HeatEquation1D.Solve(
            grid, Alpha, _ => 0, _ => 0, _ => 0,
            finalTime: 5.0, steps: 500, TimeScheme.CrankNicolson,
            source: (_, _) => Source);

        Assert.True(MaxError(solution.Values, grid, x => Source * x * (1 - x) / (2 * Alpha)) < 1e-3);
    }

    #endregion

    #region Волновое уравнение

    [Fact]
    public void Wave_StandingWave_MatchesAnalyticSolution()
    {
        // u(x,0) = sin(πx), u_t(x,0) = 0, c = 1 → u = cos(πt)·sin(πx)
        const double Time = 0.5;

        var grid = new Grid1D(0, 1, 201);

        WaveSolution solution = WaveEquation1D.Solve(
            grid, speed: 1.0, x => Math.Sin(Math.PI * x), null, Time, steps: 400);

        double phase = Math.Cos(Math.PI * Time);

        Assert.True(solution.IsStable);
        Assert.True(MaxError(solution.Values, grid, x => phase * Math.Sin(Math.PI * x)) < 1e-3);
    }

    [Fact]
    public void Wave_HalfPeriod_InvertsInitialShape()
    {
        var grid = new Grid1D(0, 1, 201);

        WaveSolution solution = WaveEquation1D.Solve(
            grid, speed: 1.0, x => Math.Sin(Math.PI * x), null, finalTime: 1.0, steps: 400);

        // Через полпериода стоячая волна переворачивается
        Assert.True(MaxError(solution.Values, grid, x => -Math.Sin(Math.PI * x)) < 5e-3);
    }

    [Fact]
    public void Wave_CourantViolation_IsRefused()
    {
        var grid = new Grid1D(0, 1, 101);

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => WaveEquation1D.Solve(grid, speed: 10.0, x => Math.Sin(Math.PI * x), null, 1.0, steps: 10));

        Assert.Contains("Куранта", error.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Уравнение Пуассона

    [Fact]
    public void Poisson_ManufacturedSolution_IsAccurate()
    {
        // u = sin(πx)·sin(πy) удовлетворяет −Δu = 2π²·sin(πx)·sin(πy)
        var grid = new Grid2D(0, 1, 0, 1, 41, 41);

        PoissonSolution solution = Poisson2D.Solve(
            grid,
            (x, y) => 2 * Math.PI * Math.PI * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * y),
            static (_, _) => 0.0);

        Assert.True(solution.Converged);
        Assert.True(PoissonError(solution, grid) < 2e-3);
    }

    [Fact]
    public void Poisson_IsSecondOrderAccurate()
    {
        double Error(int count)
        {
            var grid = new Grid2D(0, 1, 0, 1, count, count);

            PoissonSolution solution = Poisson2D.Solve(
                grid,
                (x, y) => 2 * Math.PI * Math.PI * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * y),
                static (_, _) => 0.0);

            return PoissonError(solution, grid);
        }

        double coarse = Error(11);
        double fine = Error(21);

        Assert.True(coarse / fine > 3.5, $"Отношение ошибок {coarse / fine:F2} ниже ожидаемого для второго порядка");
    }

    [Fact]
    public void Laplace_LinearBoundary_ReproducesLinearField()
    {
        // Линейная функция гармонична: решение обязано совпасть с граничными данными точно
        var grid = new Grid2D(0, 1, 0, 2, 21, 31);

        PoissonSolution solution = Poisson2D.SolveLaplace(grid, (x, y) => (2 * x) + (3 * y));

        for (int j = 0; j < grid.CountY; j++)
            for (int i = 0; i < grid.CountX; i++)
                Assert.Equal((2 * grid.X(i)) + (3 * grid.Y(j)), solution[i, j], tolerance: 1e-6);
    }

    private static double PoissonError(PoissonSolution solution, Grid2D grid)
    {
        double worst = 0;

        for (int j = 0; j < grid.CountY; j++)
        {
            for (int i = 0; i < grid.CountX; i++)
            {
                double exact = Math.Sin(Math.PI * grid.X(i)) * Math.Sin(Math.PI * grid.Y(j));
                worst = Math.Max(worst, Math.Abs(solution[i, j] - exact));
            }
        }

        return worst;
    }

    #endregion

    #region Конечные элементы

    [Fact]
    public void Fem1D_PoissonProblem_IsNodallyExact()
    {
        // −u'' = 2 при u(0) = u(1) = 0 даёт u = x(1−x); линейные элементы попадают в узлы точно
        var mesh = new Grid1D(0, 1, 11);

        Fem1DSolution solution = Fem1D.Solve(
            mesh,
            static _ => 1.0, static _ => 0.0, static _ => 2.0,
            BoundaryCondition.Fixed(0), BoundaryCondition.Fixed(0));

        Assert.True(solution.Converged);
        Assert.True(MaxError(solution.Values, mesh, x => x * (1 - x)) < 1e-9);
    }

    [Fact]
    public void Fem1D_NeumannCondition_IsRespected()
    {
        // −u'' = 0, u(0) = 0, u'(1) = 1 → u = x
        var mesh = new Grid1D(0, 1, 21);

        Fem1DSolution solution = Fem1D.Solve(
            mesh,
            static _ => 1.0, static _ => 0.0, static _ => 0.0,
            BoundaryCondition.Fixed(0), BoundaryCondition.Flux(1.0));

        Assert.True(MaxError(solution.Values, mesh, x => x) < 1e-9);
    }

    [Fact]
    public void Fem1D_ReactionTerm_MatchesAnalyticSolution()
    {
        // −u'' + u = f при u = sin(πx): f = (π² + 1)·sin(πx)
        var mesh = new Grid1D(0, 1, 201);

        Fem1DSolution solution = Fem1D.Solve(
            mesh,
            static _ => 1.0, static _ => 1.0,
            x => ((Math.PI * Math.PI) + 1) * Math.Sin(Math.PI * x),
            BoundaryCondition.Fixed(0), BoundaryCondition.Fixed(0));

        Assert.True(MaxError(solution.Values, mesh, x => Math.Sin(Math.PI * x)) < 1e-4);
    }

    [Fact]
    public void Fem1D_Interpolation_IsContinuous()
    {
        var mesh = new Grid1D(0, 1, 11);

        Fem1DSolution solution = Fem1D.Solve(
            mesh, static _ => 1.0, static _ => 0.0, static _ => 2.0,
            BoundaryCondition.Fixed(0), BoundaryCondition.Fixed(0));

        // В середине элемента линейная интерполяция чуть ниже параболы — это и есть
        // погрешность метода, а не ошибка: решение ищется среди ломаных
        double middle = solution.Evaluate(0.05);
        double exact = 0.05 * 0.95;

        Assert.True(middle < exact);
        Assert.True(Math.Abs(middle - exact) < 1e-2);
    }

    [Fact]
    public void Fem2D_ManufacturedSolution_IsAccurate()
    {
        var grid = new Grid2D(0, 1, 0, 1, 31, 31);
        TriangularMesh mesh = TriangularMesh.Rectangle(grid);

        Fem2DSolution solution = Fem2D.SolvePoisson(
            mesh,
            (x, y) => 2 * Math.PI * Math.PI * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * y),
            static (_, _) => 0.0);

        Assert.True(solution.Converged);
        Assert.Equal(2 * 30 * 30, mesh.TriangleCount);
        Assert.True(Fem2DError(solution, mesh) < 5e-3);
    }

    [Fact]
    public void Fem2D_IsSecondOrderAccurate()
    {
        double Error(int count)
        {
            TriangularMesh mesh = TriangularMesh.Rectangle(new Grid2D(0, 1, 0, 1, count, count));

            Fem2DSolution solution = Fem2D.SolvePoisson(
                mesh,
                (x, y) => 2 * Math.PI * Math.PI * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * y),
                static (_, _) => 0.0);

            return Fem2DError(solution, mesh);
        }

        double coarse = Error(11);
        double fine = Error(21);

        Assert.True(coarse / fine > 3.0, $"Отношение ошибок {coarse / fine:F2} ниже ожидаемого для линейных элементов");
    }

    [Fact]
    public void Fem2D_HarmonicBoundary_ReproducesLinearField()
    {
        var grid = new Grid2D(0, 1, 0, 1, 21, 21);
        TriangularMesh mesh = TriangularMesh.Rectangle(grid);

        Fem2DSolution solution = Fem2D.SolvePoisson(mesh, static (_, _) => 0.0, (x, y) => (2 * x) - y);

        for (int node = 0; node < mesh.NodeCount; node++)
            Assert.Equal((2 * mesh.X(node)) - mesh.Y(node), solution.Values[node], tolerance: 1e-6);
    }

    private static double Fem2DError(Fem2DSolution solution, TriangularMesh mesh)
    {
        double worst = 0;

        for (int node = 0; node < mesh.NodeCount; node++)
        {
            double exact = Math.Sin(Math.PI * mesh.X(node)) * Math.Sin(Math.PI * mesh.Y(node));
            worst = Math.Max(worst, Math.Abs(solution.Values[node] - exact));
        }

        return worst;
    }

    #endregion
}

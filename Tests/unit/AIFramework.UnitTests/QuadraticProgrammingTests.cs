using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Квадратичное программирование сверяется с перебором всех активных множеств: для каждого
/// подмножества неравенств решается задача только с равенствами, и оптимум — лучшая из
/// допустимых таких точек. Плюс ответы в замкнутой форме и условия Каруша — Куна — Таккера.
/// </summary>
public class QuadraticProgrammingTests
{
    [Fact]
    public void Unconstrained_IsNewtonStep()
    {
        var qp = new QuadraticProgram(new Matrix(new double[,] { { 4, 1 }, { 1, 3 } }), new Vector(new[] { 1.0, 2.0 }));
        QpSolution solution = QpSolver.Solve(qp);

        // H x = −g: [4 1; 1 3] x = [−1; −2] → x = [−1/11, −7/11]
        Assert.True(solution.IsOptimal);
        Assert.Equal(-1.0 / 11, solution[0], 12);
        Assert.Equal(-7.0 / 11, solution[1], 12);
    }

    [Fact]
    public void OneDimensionalBound_ClipsAndReportsMultiplier()
    {
        // min (x − 3)² ⇔ ½·2x² − 6x при x ≤ 1: оптимум на границе, множитель = −f'(1) = 4
        var qp = new QuadraticProgram(new Matrix(new double[,] { { 2 } }), new Vector(new[] { -6.0 }));
        qp.AddBounds(0, double.NegativeInfinity, 1);
        QpSolution solution = QpSolver.Solve(qp);

        Assert.Equal(1, solution[0], 12);
        Assert.Equal(4, solution.Multipliers[0], 10);
        Assert.Single(solution.ActiveConstraints);
    }

    [Fact]
    public void RandomProblems_MatchExhaustiveActiveSetSearch()
    {
        var rng = new Random(3);

        for (int trial = 0; trial < 150; trial++)
        {
            int n = 2 + rng.Next(2);
            int inequalities = 1 + rng.Next(5);
            bool equality = rng.Next(3) == 0;

            Matrix h = RandomPositiveDefinite(rng, n);
            Vector g = RandomVector(rng, n, 3);
            var qp = new QuadraticProgram(h, g);
            var rows = new List<(double[] Row, double Rhs, bool Equality)>();

            if (equality)
            {
                Vector a = RandomVector(rng, n, 1);
                qp.AddEquality(a, rng.NextDouble() - 0.5);
                rows.Add((a.ToArray(), qp.Constraints[^1].RightHandSide, true));
            }

            for (int k = 0; k < inequalities; k++)
            {
                Vector a = RandomVector(rng, n, 1);
                double b = rng.NextDouble();
                qp.AddInequality(a, b);
                rows.Add((a.ToArray(), b, false));
            }

            double? best = BruteForce(h, g, rows, out double[] bestPoint);
            QpSolution solution = QpSolver.Solve(qp);

            if (best is null)
            {
                Assert.Equal(SolverStatus.Infeasible, solution.Status);
                continue;
            }

            Assert.True(solution.IsOptimal, $"попытка {trial}: {solution.Status}");
            Assert.Equal(best.Value, solution.Objective, 7);

            for (int j = 0; j < n; j++)
                Assert.Equal(bestPoint[j], solution[j], 6);

            Assert.True(solution.MaxViolation < 1e-8);
            AssertKkt(h, g, rows, solution);
        }
    }

    [Fact]
    public void ContradictoryConstraints_AreInfeasible()
    {
        var qp = new QuadraticProgram(new Matrix(new double[,] { { 1 } }), new Vector(new[] { 0.0 }));
        qp.AddInequality(new Vector(new[] { 1.0 }), 0);
        qp.AddInequality(new Vector(new[] { -1.0 }), -1);

        Assert.Equal(SolverStatus.Infeasible, QpSolver.Solve(qp).Status);
    }

    [Fact]
    public void MinimumVariancePortfolio_MatchesClosedForm()
    {
        // Когда запрет коротких позиций не активен, w = Σ⁻¹1 / (1ᵀΣ⁻¹1)
        var covariance = new Matrix(new double[,] { { 0.04, 0.006, 0.002 }, { 0.006, 0.09, 0.009 }, { 0.002, 0.009, 0.0625 } });
        var qp = new QuadraticProgram(covariance * 2, new Vector(3));
        qp.AddEquality(new Vector(new[] { 1.0, 1.0, 1.0 }), 1, "бюджет");

        for (int i = 0; i < 3; i++)
            qp.AddBounds(i, 0, double.PositiveInfinity);

        QpSolution solution = QpSolver.Solve(qp);
        Vector raw = LU.Solve(covariance, new Vector(new[] { 1.0, 1.0, 1.0 }));
        double total = raw.ToArray().Sum();

        for (int i = 0; i < 3; i++)
            Assert.Equal(raw[i] / total, solution[i], 9);
    }

    [Fact]
    public void InfeasibleStart_IsRepaired_AndNonConvexProblemIsRejected()
    {
        var qp = new QuadraticProgram(new Matrix(new double[,] { { 1, 0 }, { 0, 1 } }), new Vector(new[] { -4.0, -4.0 }));
        qp.AddInequality(new Vector(new[] { 1.0, 1.0 }), 2);
        QpSolution solution = QpSolver.Solve(qp, start: new Vector(new[] { 10.0, 10.0 }));

        Assert.Equal(1, solution[0], 10);
        Assert.Equal(1, solution[1], 10);

        _ = Assert.Throws<ArgumentException>(() => QpSolver.Solve(
            new QuadraticProgram(new Matrix(new double[,] { { 1, 0 }, { 0, -1 } }), new Vector(2))));
    }

    private static double? BruteForce(Matrix h, Vector g, List<(double[] Row, double Rhs, bool Equality)> rows, out double[] point)
    {
        int n = g.Count;
        int[] inequalities = Enumerable.Range(0, rows.Count).Where(i => !rows[i].Equality).ToArray();
        int[] equalities = Enumerable.Range(0, rows.Count).Where(i => rows[i].Equality).ToArray();
        double? best = null;
        point = [];

        for (int mask = 0; mask < 1 << inequalities.Length; mask++)
        {
            int[] active = equalities.Concat(inequalities.Where((_, k) => ((mask >> k) & 1) == 1)).ToArray();

            if (active.Length > n)
                continue;

            int size = n + active.Length;
            var kkt = new Matrix(size, size);
            var rhs = new Vector(size);

            for (int i = 0; i < n; i++)
            {
                rhs[i] = -g[i];
                for (int j = 0; j < n; j++)
                    kkt[i, j] = h[i, j];
            }

            for (int k = 0; k < active.Length; k++)
            {
                rhs[n + k] = rows[active[k]].Rhs;
                for (int j = 0; j < n; j++)
                {
                    kkt[n + k, j] = rows[active[k]].Row[j];
                    kkt[j, n + k] = rows[active[k]].Row[j];
                }
            }

            double[] x;

            try
            {
                x = LU.Solve(kkt, rhs).ToArray()[..n];
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            bool feasible = rows.All(r =>
            {
                double lhs = r.Row.Select((c, j) => c * x[j]).Sum();
                return r.Equality ? Math.Abs(lhs - r.Rhs) < 1e-9 : lhs <= r.Rhs + 1e-9;
            });

            if (!feasible)
                continue;

            double value = 0;
            for (int i = 0; i < n; i++)
            {
                value += g[i] * x[i];
                for (int j = 0; j < n; j++)
                    value += 0.5 * x[i] * h[i, j] * x[j];
            }

            if (best is null || value < best.Value - 1e-12)
            {
                best = value;
                point = x;
            }
        }

        return best;
    }

    private static void AssertKkt(Matrix h, Vector g, List<(double[] Row, double Rhs, bool Equality)> rows, QpSolution solution)
    {
        int n = g.Count;

        for (int i = 0; i < n; i++)
        {
            double stationarity = g[i];
            for (int j = 0; j < n; j++)
                stationarity += h[i, j] * solution[j];
            for (int k = 0; k < rows.Count; k++)
                stationarity += solution.Multipliers[k] * rows[k].Row[i];

            Assert.True(Math.Abs(stationarity) < 1e-7, $"стационарность нарушена: {stationarity}");
        }

        for (int k = 0; k < rows.Count; k++)
        {
            if (rows[k].Equality)
                continue;

            double slack = rows[k].Rhs - rows[k].Row.Select((c, j) => c * solution[j]).Sum();
            Assert.True(solution.Multipliers[k] >= -1e-9);
            Assert.True(Math.Abs(solution.Multipliers[k] * slack) < 1e-7, "дополняющая нежёсткость нарушена");
        }
    }

    private static Matrix RandomPositiveDefinite(Random rng, int n)
    {
        var m = new Matrix(n, n);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                m[i, j] = (rng.NextDouble() * 2) - 1;

        Matrix h = m.Transpose() * m;
        for (int i = 0; i < n; i++)
            h[i, i] += 0.5;

        return (h + h.Transpose()) * 0.5;
    }

    private static Vector RandomVector(Random rng, int n, double scale)
    {
        var v = new Vector(n);
        for (int i = 0; i < n; i++)
            v[i] = ((rng.NextDouble() * 2) - 1) * scale;
        return v;
    }
}

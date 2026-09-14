using AI.DataStructs.Algebraic;
using AI.MathUtils.Nonlinear;
using AI.MathUtils.ODE;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Нелинейные решатели и интеграторы из <c>AI.ClassicMath</c>: Левенберг-Марквардт, Ньютон,
/// Дорман-Принс с событиями и скоростной метод Верле.
/// </summary>
public class NonlinearNumericsTests
{
    private const double CircleCenterX = 2.0;
    private const double CircleCenterY = -1.0;
    private const double CircleRadius = 3.0;

    private static (double[] X, double[] Y) CirclePoints()
    {
        const int count = 12;
        var x = new double[count];
        var y = new double[count];

        for (int k = 0; k < count; k++)
        {
            double angle = 2 * Math.PI * k / count;
            x[k] = CircleCenterX + (CircleRadius * Math.Cos(angle));
            y[k] = CircleCenterY + (CircleRadius * Math.Sin(angle));
        }

        return (x, y);
    }

    /// <summary>Функция Розенброка как задача наименьших квадратов: r = (10(x₁ - x₀²), 1 - x₀), минимум в (1, 1).</summary>
    [Fact]
    public void LevenbergMarquardt_Solve_RosenbrockReachesMinimum()
    {
        var result = LevenbergMarquardt.Solve(
            x => new Vector(10 * (x[1] - (x[0] * x[0])), 1 - x[0]),
            new Vector(-1.2, 1.0));

        Assert.True(result.Converged);
        Assert.Equal(1.0, result.Solution[0], 1e-8);
        Assert.Equal(1.0, result.Solution[1], 1e-8);
        Assert.True(result.Cost < 1e-20, $"Cost = {result.Cost}");
        Assert.Equal(2, result.JacobianRank());
    }

    /// <summary>Подгонка окружности по точкам: невязки - расстояния до центра минус радиус.</summary>
    [Fact]
    public void LevenbergMarquardt_Solve_CircleFitRecoversCenterAndRadius()
    {
        var (px, py) = CirclePoints();

        Vector Residuals(Vector p)
        {
            var r = new Vector(px.Length);

            for (int i = 0; i < px.Length; i++)
                r[i] = Math.Sqrt(((px[i] - p[0]) * (px[i] - p[0])) + ((py[i] - p[1]) * (py[i] - p[1]))) - p[2];

            return r;
        }

        Matrix Jacobian(Vector p)
        {
            var j = new Matrix(px.Length, 3);

            for (int i = 0; i < px.Length; i++)
            {
                double distance = Math.Sqrt(((px[i] - p[0]) * (px[i] - p[0])) + ((py[i] - p[1]) * (py[i] - p[1])));
                j[i, 0] = -(px[i] - p[0]) / distance;
                j[i, 1] = -(py[i] - p[1]) / distance;
                j[i, 2] = -1;
            }

            return j;
        }

        var start = new Vector(0.5, 0.5, 1.0);
        var analytic = LevenbergMarquardt.Solve(Residuals, start, Jacobian);
        var numeric = LevenbergMarquardt.Solve(Residuals, start);

        foreach (var result in new[] { analytic, numeric })
        {
            Assert.True(result.Converged);
            Assert.Equal(CircleCenterX, result.Solution[0], 1e-8);
            Assert.Equal(CircleCenterY, result.Solution[1], 1e-8);
            Assert.Equal(CircleRadius, result.Solution[2], 1e-8);
            Assert.Equal(px.Length, result.Jacobian.Height);
            Assert.Equal(3, result.Jacobian.Width);
            Assert.Equal(3, result.JacobianRank());
        }
    }

    /// <summary>Невязки зависят только от суммы параметров: решение находится, а ранг якобиана показывает недоопределенность.</summary>
    [Fact]
    public void LevenbergMarquardt_Solve_UnderdeterminedProblemHasDeficientJacobianRank()
    {
        var result = LevenbergMarquardt.Solve(
            x => new Vector(x[0] + x[1] - 3, 2 * (x[0] + x[1] - 3), x[0] + x[1] - 3),
            new Vector(0.0, 0.0));

        Assert.True(result.Converged);
        Assert.True(result.Cost < 1e-20, $"Cost = {result.Cost}");
        Assert.Equal(3.0, result.Solution[0] + result.Solution[1], 1e-10);
        Assert.Equal(1, result.JacobianRank());
    }

    /// <summary>Пересечение окружности x² + y² = 4 с прямой x = y: корень (√2, √2).</summary>
    [Fact]
    public void NewtonSystemSolver_Solve_CircleLineIntersectionFound()
    {
        static Vector F(Vector v) => new(((v[0] * v[0]) + (v[1] * v[1])) - 4, v[0] - v[1]);

        static Matrix J(Vector v) => new(new[,] { { 2 * v[0], 2 * v[1] }, { 1.0, -1.0 } });

        var numeric = NewtonSystemSolver.Solve(F, new Vector(1.0, 0.5));
        var analytic = NewtonSystemSolver.Solve(F, new Vector(1.0, 0.5), J);
        var broyden = NewtonSystemSolver.Solve(F, new Vector(1.0, 0.5), options: new NewtonSystemOptions { UseBroyden = true });

        foreach (var result in new[] { numeric, analytic, broyden })
        {
            Assert.True(result.Converged);
            Assert.Equal(Math.Sqrt(2), result.Solution[0], 1e-10);
            Assert.Equal(Math.Sqrt(2), result.Solution[1], 1e-10);
        }

        Assert.True(analytic.Iterations <= 10, $"Iterations = {analytic.Iterations}");
    }

    /// <summary>Система x² + y² = 4, eˣ + y = 1 из далекого начального приближения: дробление шага удерживает сходимость.</summary>
    [Fact]
    public void NewtonSystemSolver_Solve_LineSearchConvergesFromFarStart()
    {
        static Vector F(Vector v) => new(((v[0] * v[0]) + (v[1] * v[1])) - 4, Math.Exp(v[0]) + v[1] - 1);

        foreach (bool broyden in new[] { false, true })
        {
            var result = NewtonSystemSolver.Solve(F, new Vector(3.0, 3.0), options: new NewtonSystemOptions { UseBroyden = broyden });

            Assert.True(result.Converged, $"Broyden = {broyden}, residual = {result.ResidualNorm}");
            Assert.True(NewtonResidual(F(result.Solution)) < 1e-10);
        }
    }

    private static double NewtonResidual(Vector value) => Math.Max(Math.Abs(value[0]), Math.Abs(value[1]));

    /// <summary>y' = -y: узлы траектории и плотная выдача между узлами совпадают с e⁻ᵗ.</summary>
    [Fact]
    public void DormandPrince_Integrate_ExponentialDecayMatchesAnalytic()
    {
        var solution = DormandPrince.Integrate(
            (t, y) => new Vector(-y[0]),
            0,
            new Vector(1.0),
            5,
            new DormandPrinceOptions { RelativeTolerance = 1e-10, AbsoluteTolerance = 1e-12 });

        Assert.True(solution.Success);
        Assert.False(solution.StoppedByEvent);
        Assert.Equal(5.0, solution.FinalTime);
        Assert.True(solution.AcceptedSteps > 5);

        for (int i = 0; i < solution.Times.Count; i++)
            Assert.Equal(Math.Exp(-solution.Times[i]), solution.States[i][0], 1e-9);

        for (int i = 0; i < solution.Times.Count - 1; i++)
        {
            double middle = 0.5 * (solution.Times[i] + solution.Times[i + 1]);
            Assert.Equal(Math.Exp(-middle), solution.Interpolate(middle)[0], 1e-9);
        }
    }

    /// <summary>Гармонический осциллятор x'' = -x на 20 единицах времени: x = cos t, v = -sin t.</summary>
    [Fact]
    public void DormandPrince_Integrate_HarmonicOscillatorMatchesAnalytic()
    {
        var solution = DormandPrince.Integrate(
            (t, y) => new Vector(y[1], -y[0]),
            0,
            new Vector(1.0, 0.0),
            20,
            new DormandPrinceOptions { RelativeTolerance = 1e-10, AbsoluteTolerance = 1e-12 });

        Assert.True(solution.Success);

        for (int i = 0; i < solution.Times.Count; i++)
        {
            double t = solution.Times[i];
            Assert.Equal(Math.Cos(t), solution.States[i][0], 1e-7);
            Assert.Equal(-Math.Sin(t), solution.States[i][1], 1e-7);
        }
    }

    /// <summary>Падение с высоты h: останавливающее событие «высота = 0» наступает при t = √(2h/g).</summary>
    [Fact]
    public void DormandPrince_Integrate_BallDropStopsAtAnalyticImpactTime()
    {
        const double height = 10.0;
        const double gravity = 9.81;

        var ground = new OdeEvent((t, y) => y[0], isTerminal: true, direction: -1);
        var solution = DormandPrince.Integrate(
            (t, y) => new Vector(y[1], -gravity),
            0,
            new Vector(height, 0.0),
            100,
            events: new[] { ground });

        double expected = Math.Sqrt(2 * height / gravity);

        Assert.True(solution.Success);
        Assert.True(solution.StoppedByEvent);
        Assert.Single(solution.Events);
        Assert.Equal(expected, solution.Events[0].Time, 1e-10);
        Assert.Equal(expected, solution.FinalTime, 1e-10);
        Assert.Equal(0.0, solution.FinalState[0], 1e-9);
        Assert.Equal(-gravity * expected, solution.FinalState[1], 1e-8);
    }

    /// <summary>Нули x = cos t на [0, 10]: без фильтра три события, при фильтре «спад» два (π/2 и 5π/2).</summary>
    [Fact]
    public void DormandPrince_Integrate_RecordsNonTerminalEventsWithDirection()
    {
        var anyCrossing = new OdeEvent((t, y) => y[0]);
        var falling = new OdeEvent((t, y) => y[0], direction: -1);

        var solution = DormandPrince.Integrate(
            (t, y) => new Vector(y[1], -y[0]),
            0,
            new Vector(1.0, 0.0),
            10,
            new DormandPrinceOptions { RelativeTolerance = 1e-10, AbsoluteTolerance = 1e-12 },
            new[] { anyCrossing, falling });

        var any = solution.Events.Where(e => e.EventIndex == 0).Select(e => e.Time).ToArray();
        var down = solution.Events.Where(e => e.EventIndex == 1).Select(e => e.Time).ToArray();

        Assert.Equal(10.0, solution.FinalTime);
        Assert.Equal(3, any.Length);
        Assert.Equal(2, down.Length);

        for (int k = 0; k < 3; k++)
            Assert.Equal((Math.PI / 2) + (k * Math.PI), any[k], 1e-7);

        Assert.Equal(Math.PI / 2, down[0], 1e-7);
        Assert.Equal(5 * Math.PI / 2, down[1], 1e-7);
    }

    /// <summary>Осциллятор x'' = -ω²x на 1000 периодах: энергия колеблется в узких пределах и не дрейфует.</summary>
    [Fact]
    public void VelocityVerlet_Integrate_HarmonicOscillatorConservesEnergy()
    {
        const double omega = 2.0;
        const int stepsPerPeriod = 200;
        const int periods = 1000;
        double period = 2 * Math.PI / omega;

        var trajectory = VelocityVerlet.Integrate(
            (x, v, t) => new Vector(-omega * omega * x[0]),
            new Vector(1.0),
            new Vector(0.0),
            0,
            period / stepsPerPeriod,
            stepsPerPeriod * periods,
            recordEvery: stepsPerPeriod / 10);

        double Energy(int i) => (0.5 * trajectory.Velocities[i][0] * trajectory.Velocities[i][0])
            + (0.5 * omega * omega * trajectory.Positions[i][0] * trajectory.Positions[i][0]);

        double initial = Energy(0);
        double worst = 0;

        for (int i = 0; i < trajectory.Times.Count; i++)
            worst = Math.Max(worst, Math.Abs(Energy(i) - initial) / initial);

        Assert.Equal(periods * 10 + 1, trajectory.Times.Count);
        Assert.Equal(periods * period, trajectory.Times[trajectory.Times.Count - 1], 1e-9);
        Assert.True(worst < 1e-3, $"Максимальное относительное отклонение энергии {worst}");
    }
}

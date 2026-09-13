using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Identification;
using AI.ControlSystems.Linear;
using AI.ControlSystems.Observers;
using AI.ControlSystems.Optimal;
using AI.ControlSystems.Pid;
using AI.DataStructs.Algebraic;
using Xunit;
using Complex = System.Numerics.Complex;

namespace AIFramework.UnitTests;

/// <summary>
/// Линейная часть систем управления проверяется замкнутыми формулами, долгими итерациями как
/// независимым решением уравнения Риккати, характеристическим многочленом по Фаддееву — Леверье
/// и моделированием с шумом.
/// </summary>
public class ControlSystemsLinearTests
{
    #region Дискретизация и уравнения

    [Fact]
    public void Discretization_MatchesAnalyticSolutions()
    {
        // Первый порядок: ẋ = −2x + 3u
        Discretization.ZeroOrderHold(M(new double[,] { { -2 } }), M(new double[,] { { 3 } }), 0.1, out Matrix ad, out Matrix bd);
        Assert.Equal(Math.Exp(-0.2), ad[0, 0], 14);
        Assert.Equal(3 * (1 - Math.Exp(-0.2)) / 2, bd[0, 0], 14);

        // Двойной интегратор
        Discretization.ZeroOrderHold(M(new double[,] { { 0, 1 }, { 0, 0 } }), M(new double[,] { { 0 }, { 1 } }), 0.3, out ad, out bd);
        Assert.Equal(0.3, ad[0, 1], 14);
        Assert.Equal(0.045, bd[0, 0], 14);
        Assert.Equal(0.3, bd[1, 0], 14);

        // Осциллятор ẍ = −ω²x + u
        double w = 2, dt = 0.4;
        Discretization.ZeroOrderHold(M(new double[,] { { 0, 1 }, { -w * w, 0 } }), M(new double[,] { { 0 }, { 1 } }), dt, out ad, out bd);
        Assert.Equal(Math.Cos(w * dt), ad[0, 0], 13);
        Assert.Equal(Math.Sin(w * dt) / w, ad[0, 1], 13);
        Assert.Equal(-w * Math.Sin(w * dt), ad[1, 0], 13);
        Assert.Equal((1 - Math.Cos(w * dt)) / (w * w), bd[0, 0], 13);
        Assert.Equal(Math.Sin(w * dt) / w, bd[1, 0], 13);

        // Шум по Ван Лоану: ẋ = ax + w → Qd = q·(e^(2a·dt) − 1)/(2a)
        Matrix qd = Discretization.DiscretizeProcessNoise(M(new double[,] { { -1.5 } }), M(new double[,] { { 2 } }), 0.3);
        Assert.Equal(2 * (Math.Exp(-0.9) - 1) / -3.0, qd[0, 0], 13);

        // Двойной интегратор с шумом ускорения q: Qd = q·[dt³/3, dt²/2; dt²/2, dt]
        Matrix qd2 = Discretization.DiscretizeProcessNoise(M(new double[,] { { 0, 1 }, { 0, 0 } }), M(new double[,] { { 0, 0 }, { 0, 0.5 } }), 0.3);
        Assert.Equal(0.5 * 0.027 / 3, qd2[0, 0], 14);
        Assert.Equal(0.5 * 0.09 / 2, qd2[0, 1], 14);
        Assert.Equal(0.5 * 0.09 / 2, qd2[1, 0], 14);
        Assert.Equal(0.5 * 0.3, qd2[1, 1], 14);
    }

    [Fact]
    public void Lyapunov_MatchesScalarFormulas_AndHasSmallResidual()
    {
        Assert.Equal(-3 / (2 * -1.5), LyapunovEquation.SolveContinuous(M(new double[,] { { -1.5 } }), M(new double[,] { { 3 } }))[0, 0], 13);
        Assert.Equal(2 / (1 - 0.81), LyapunovEquation.SolveDiscrete(M(new double[,] { { 0.9 } }), M(new double[,] { { 2 } }))[0, 0], 12);

        var a = M(new double[,] { { -1, 2, 0 }, { -2, -1, 1 }, { 0, 0.5, -3 } });
        var q = M(new double[,] { { 2, 0.3, 0 }, { 0.3, 1, 0.1 }, { 0, 0.1, 1.5 } });
        Matrix x = LyapunovEquation.SolveContinuous(a, q);
        AssertSmall(a * x + x * a.Transpose() + q, 1e-11);

        var ad = a * 0.2;
        Matrix xd = LyapunovEquation.SolveDiscrete(ad, q);
        AssertSmall(ad * xd * ad.Transpose() - xd + q, 1e-11);
    }

    [Fact]
    public void Riccati_ScalarClosedForm()
    {
        double a = 1.2, b = 0.5, q = 2, r = 0.3;
        double linear = (r * (1 - (a * a))) - (q * b * b);
        double expected = (-linear + Math.Sqrt((linear * linear) + (4 * b * b * q * r))) / (2 * b * b);

        LqrDesign design = DiscreteLqr.Design(M(new double[,] { { a } }), M(new double[,] { { b } }), M(new double[,] { { q } }), M(new double[,] { { r } }));

        Assert.Equal(expected, design.CostToGo[0, 0], 10);
        Assert.Equal(b * expected * a / (r + (b * b * expected)), design.Gain[0, 0], 10);
        Assert.True(Complex.Abs(design.ClosedLoopPoles[0]) < 1);
    }

    [Fact]
    public void Riccati_AgreesWithLongValueIteration_OnRandomSystems()
    {
        var rng = new Random(4);

        for (int trial = 0; trial < 10; trial++)
        {
            Matrix a = RandomMatrix(rng, 4, 4, 1);
            Matrix b = RandomMatrix(rng, 4, 2, 1);
            Matrix q = Eye(4);
            Matrix r = Eye(2);

            Matrix p = RiccatiEquation.SolveDiscrete(a, b, q, r);
            AssertSmall(RiccatiEquation.DiscreteResidual(a, b, q, r, p), 1e-9 * Math.Max(1, Max(p)));

            // Независимо: итерации Риккати до неподвижной точки
            Matrix iterate = q;
            for (int k = 0; k < 20_000; k++)
            {
                Matrix inner = (r + b.Transpose() * iterate * b).GetInvertMatrix();
                iterate = a.Transpose() * iterate * a - a.Transpose() * iterate * b * inner * b.Transpose() * iterate * a + q;
                iterate = (iterate + iterate.Transpose()) * 0.5;
            }

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    Assert.Equal(iterate[i, j], p[i, j], 6);

            Matrix k0 = DiscreteLqr.Solve(a, b, q, r);
            Assert.True(Eigen.SpectralRadius(a - b * k0) < 1);
        }
    }

    [Fact]
    public void Lqr_CostOfClosedLoopEqualsQuadraticForm()
    {
        double dt = 0.1;
        var a = M(new double[,] { { 1, dt }, { 0, 1 } });
        var b = M(new double[,] { { 0.5 * dt * dt }, { dt } });
        var q = M(new double[,] { { 1, 0 }, { 0, 0.2 } });
        var r = M(new double[,] { { 0.05 } });
        LqrDesign design = DiscreteLqr.Design(a, b, q, r);

        var x = new Vector(new[] { 3.0, -1.0 });
        double expected = Quadratic(design.CostToGo, x);
        double cost = 0;

        for (int k = 0; k < 5000; k++)
        {
            Vector u = Mul(design.Gain, x) * -1;
            cost += Quadratic(q, x) + Quadratic(r, u);
            x = Mul(a, x) + Mul(b, u);
        }

        Assert.Equal(expected, cost, 8);
    }

    #endregion

    #region Структура и полюса

    [Fact]
    public void Controllability_And_Observability()
    {
        var a = M(new double[,] { { 1, 0.1 }, { 0, 1 } });

        Assert.True(SystemAnalysis.IsControllable(a, M(new double[,] { { 0 }, { 1 } })));
        Assert.False(SystemAnalysis.IsControllable(a, M(new double[,] { { 1 }, { 0 } })));
        Assert.True(SystemAnalysis.IsObservable(a, M(new double[,] { { 1, 0 } })));
        Assert.False(SystemAnalysis.IsObservable(a, M(new double[,] { { 0, 1 } })));
        Assert.True(SystemAnalysis.IsStableDiscrete(a * 0.5));
        Assert.False(SystemAnalysis.IsStableContinuous(M(new double[,] { { 0, 1 }, { -1, 0 } })));
    }

    [Fact]
    public void PolePlacement_PlacesRequestedPoles()
    {
        var a = M(new double[,] { { 1, 0.1, 0 }, { 0, 1, 0.1 }, { 0, 0, 0.9 } });
        var b = M(new double[,] { { 0 }, { 0 }, { 0.1 } });
        Complex[] poles = [0.5, new Complex(0.6, 0.2), new Complex(0.6, -0.2)];

        Matrix k = PolePlacement.AckermannGain(a, b, poles);
        Matrix closed = a - b * k;

        // Характеристический многочлен по Фаддееву — Леверье, без собственных значений
        double[] characteristic = FaddeevLeverrier(closed);
        Vector desired = PolePlacement.PolynomialFromPoles(poles);

        for (int i = 0; i < 3; i++)
            Assert.Equal(desired[i], characteristic[i], 10);

        foreach (Complex pole in poles)
            Assert.Contains(Eigen.General(closed), z => Complex.Abs(z - pole) < 1e-8);

        Assert.Equal(new[] { -2.0, 1.0 }, PolePlacement.PolynomialFromPoles([1, -2]).ToArray());
        _ = Assert.Throws<ArgumentException>(() => PolePlacement.PolynomialFromPoles([new Complex(0.5, 0.1)]));

        // Неуправляемая пара: полюс 1 при B = [1; 0] не сдвигается никаким K
        _ = Assert.Throws<InvalidOperationException>(() => PolePlacement.AckermannGain(
            M(new double[,] { { 1, 0.1 }, { 0, 1 } }), M(new double[,] { { 1 }, { 0 } }), new Complex[] { 0.25, -1 }));
    }

    #endregion

    #region Оценивание

    [Fact]
    public void SteadyKalman_RandomWalk_MatchesClosedForm_AndRunningFilterConverges()
    {
        double q = 0.2, r = 1.5;
        double prior = (q + Math.Sqrt((q * q) + (4 * q * r))) / 2;
        SteadyStateKalman steady = KalmanFilter.SteadyState(M(new double[,] { { 1 } }), M(new double[,] { { 1 } }), M(new double[,] { { q } }), M(new double[,] { { r } }));

        Assert.Equal(prior, steady.PriorCovariance[0, 0], 12);
        Assert.Equal(prior / (prior + r), steady.Gain[0, 0], 12);
        Assert.Equal(prior * r / (prior + r), steady.PosteriorCovariance[0, 0], 12);

        var filter = new KalmanFilter(M(new double[,] { { 1 } }), M(new double[,] { { 0 } }), M(new double[,] { { 1 } }),
            M(new double[,] { { 0 } }), M(new double[,] { { q } }), M(new double[,] { { r } }));
        var zero = new Vector(new[] { 0.0 });

        for (int k = 0; k < 200; k++)
        {
            filter.Predict(zero);
            filter.Update(new Vector(new[] { 0.3 }), zero);
        }

        Assert.Equal(steady.PosteriorCovariance[0, 0], filter.Covariance[0, 0], 10);
    }

    [Fact]
    public void Kalman_EmpiricalErrorCovarianceMatchesSteadyState()
    {
        var a = M(new double[,] { { 1, 0.1 }, { 0, 1 } });
        var b = M(new double[,] { { 0.005 }, { 0.1 } });
        var c = M(new double[,] { { 1, 0 } });
        var w = M(new double[,] { { 1e-3, 0 }, { 0, 1e-2 } });
        var v = M(new double[,] { { 0.05 } });
        SteadyStateKalman steady = KalmanFilter.SteadyState(a, c, w, v);
        var filter = new KalmanFilter(a, b, c, M(new double[,] { { 0 } }), w, v, new Vector(2), steady.PosteriorCovariance);

        var rng = new Random(5);
        var x = new Vector(2);
        var u = new Vector(new[] { 0.0 });
        double s00 = 0, s11 = 0;
        int count = 0;

        for (int k = 0; k < 40_000; k++)
        {
            x = Mul(a, x) + new Vector(new[] { Gauss(rng) * Math.Sqrt(1e-3), Gauss(rng) * Math.Sqrt(1e-2) });
            var y = new Vector(new[] { x[0] + (Gauss(rng) * Math.Sqrt(0.05)) });
            filter.Predict(u);
            filter.Update(y, u);

            if (k < 500)
                continue;

            double e0 = x[0] - filter.State[0], e1 = x[1] - filter.State[1];
            s00 += e0 * e0;
            s11 += e1 * e1;
            count++;
        }

        Assert.Equal(steady.PosteriorCovariance[0, 0], s00 / count, steady.PosteriorCovariance[0, 0] * 0.1);
        Assert.Equal(steady.PosteriorCovariance[1, 1], s11 / count, steady.PosteriorCovariance[1, 1] * 0.1);
    }

    [Fact]
    public void ExtendedKalman_OnLinearModel_EqualsKalman()
    {
        var a = M(new double[,] { { 0.9, 0.2 }, { -0.1, 0.8 } });
        var b = M(new double[,] { { 0 }, { 1 } });
        var c = M(new double[,] { { 1, 0.5 } });
        var q = M(new double[,] { { 0.01, 0 }, { 0, 0.02 } });
        var r = M(new double[,] { { 0.1 } });
        var p0 = M(new double[,] { { 1, 0 }, { 0, 1 } });

        var kf = new KalmanFilter(a, b, c, M(new double[,] { { 0 } }), q, r, new Vector(2), p0);
        var ekf = new ExtendedKalmanFilter(new Vector(2), p0);
        var rng = new Random(6);

        for (int k = 0; k < 50; k++)
        {
            var u = new Vector(new[] { Math.Sin(k * 0.3) });
            var y = new Vector(new[] { rng.NextDouble() });

            kf.Predict(u);
            kf.Update(y, u);

            ekf.Predict(Mul(a, ekf.State) + Mul(b, u), a, q);
            ekf.Update(y, Mul(c, ekf.State), c, r);

            for (int i = 0; i < 2; i++)
                Assert.Equal(kf.State[i], ekf.State[i], 12);
        }
    }

    [Fact]
    public void Luenberger_ErrorFollowsObserverDynamics()
    {
        var a = M(new double[,] { { 1, 0.1 }, { 0, 1 } });
        var b = M(new double[,] { { 0.005 }, { 0.1 } });
        var c = M(new double[,] { { 1, 0 } });

        // Наблюдатель — размещение полюсов для двойственной пары (Aᵀ, Cᵀ)
        Matrix l = PolePlacement.AckermannGain(a.Transpose(), c.Transpose(), [0.3, 0.4]).Transpose();
        var observer = new LuenbergerObserver(a, b, c, l, new Vector(2));
        var x = new Vector(new[] { 1.0, -2.0 });
        Matrix errorDynamics = a - l * c;
        Vector expectedError = x;

        for (int k = 0; k < 20; k++)
        {
            var u = new Vector(new[] { Math.Cos(k) });
            observer.Step(u, Mul(c, x));
            x = Mul(a, x) + Mul(b, u);
            expectedError = Mul(errorDynamics, expectedError);

            for (int i = 0; i < 2; i++)
                Assert.Equal(expectedError[i], x[i] - observer.State[i], 10);
        }
    }

    [Fact]
    public void Rls_MatchesBatchLeastSquares_AndTracksWithForgetting()
    {
        var rng = new Random(7);
        var rls = new RecursiveLeastSquares(new Vector(3), Eye(3) * 1e8);
        var rows = new List<double[]>();
        var targets = new List<double>();

        for (int k = 0; k < 300; k++)
        {
            double[] phi = [Gauss(rng), Gauss(rng), 1];
            double y = (2 * phi[0]) - (0.5 * phi[1]) + 0.3 + (0.1 * Gauss(rng));
            rls.Update(new Vector(phi), y);
            rows.Add(phi);
            targets.Add(y);
        }

        var normal = new Matrix(3, 3);
        var right = new Vector(3);
        for (int k = 0; k < rows.Count; k++)
            for (int i = 0; i < 3; i++)
            {
                right[i] += rows[k][i] * targets[k];
                for (int j = 0; j < 3; j++)
                    normal[i, j] += rows[k][i] * rows[k][j];
            }

        Vector batch = LU.Solve(normal, right);
        for (int i = 0; i < 3; i++)
            Assert.Equal(batch[i], rls.Theta[i], 5);

        // Скачок параметра: с забыванием 0.95 оценка догоняет новое значение
        rls.ForgettingFactor = 0.95;
        for (int k = 0; k < 300; k++)
        {
            double[] phi = [Gauss(rng), Gauss(rng), 1];
            rls.Update(new Vector(phi), (-1 * phi[0]) - (0.5 * phi[1]) + 0.3 + (0.01 * Gauss(rng)));
        }

        Assert.Equal(-1, rls.Theta[0], 0.05);
    }

    #endregion

    #region PID

    [Fact]
    public void ImcPi_OnFirstOrderPlant_GivesFirstOrderClosedLoop()
    {
        double gain = 2, tau = 5, lambda = 1, dt = 0.001;
        ImcPidTuning.FirstOrderPi(gain, tau, lambda, out double kp, out double ki);
        var pid = new PidController(kp, ki, 0);
        double decay = Math.Exp(-dt / tau), y = 0, worst = 0;

        for (int k = 0; k < 5000; k++)
        {
            double t = k * dt;
            worst = Math.Max(worst, Math.Abs(y - (1 - Math.Exp(-t / lambda))));
            double u = pid.Compute(1, y, dt);
            y = (decay * y) + (gain * (1 - decay) * u);
        }

        // IMC-ПИ сокращает полюс объекта: замкнутый контур — первый порядок с постоянной λ
        Assert.True(worst < 0.01, $"отклонение {worst}");
    }

    [Fact]
    public void AntiWindupTracking_ReducesOvershootUnderSaturation()
    {
        double Overshoot(bool tracking)
        {
            var pid = new PidController(2.5, 0.5, 0) { OutputMin = -1, OutputMax = 1, UseAntiWindupTracking = tracking };
            double decay = Math.Exp(-0.01 / 5), y = 0, peak = 0;

            for (int k = 0; k < 6000; k++)
            {
                double u = pid.Compute(1.8, y, 0.01);
                y = (decay * y) + (2 * (1 - decay) * u);
                peak = Math.Max(peak, y);
            }

            return peak - 1.8;
        }

        Assert.True(Overshoot(false) > Overshoot(true) + 0.01);
    }

    #endregion

    #region Инструменты

    private static Matrix M(double[,] values) => new(values);

    private static Matrix Eye(int n)
    {
        var m = new Matrix(n, n);
        for (int i = 0; i < n; i++)
            m[i, i] = 1;
        return m;
    }

    private static Vector Mul(Matrix a, Vector x)
    {
        var r = new Vector(a.Height);
        for (int i = 0; i < a.Height; i++)
            for (int j = 0; j < a.Width; j++)
                r[i] += a[i, j] * x[j];
        return r;
    }

    private static double Quadratic(Matrix w, Vector x)
    {
        Vector wx = Mul(w, x);
        double s = 0;
        for (int i = 0; i < x.Count; i++)
            s += x[i] * wx[i];
        return s;
    }

    private static double Max(Matrix m) => m.Data.Max(Math.Abs);

    private static void AssertSmall(Matrix m, double tolerance)
        => Assert.True(Max(m) <= tolerance, $"невязка {Max(m)}");

    private static Matrix RandomMatrix(Random rng, int rows, int cols, double scale)
    {
        var m = new Matrix(rows, cols);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                m[i, j] = ((rng.NextDouble() * 2) - 1) * scale;
        return m;
    }

    private static double Gauss(Random rng)
        => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());

    /// <summary>Коэффициенты det(λI − A) от младшего к старшему без старшей единицы</summary>
    private static double[] FaddeevLeverrier(Matrix a)
    {
        int n = a.Height;
        var coefficients = new double[n + 1];
        coefficients[n] = 1;
        var m = new Matrix(n, n);

        for (int k = 1; k <= n; k++)
        {
            m = a * m;
            for (int i = 0; i < n; i++)
                m[i, i] += coefficients[n - k + 1];

            Matrix am = a * m;
            double trace = 0;
            for (int i = 0; i < n; i++)
                trace += am[i, i];

            coefficients[n - k] = -trace / k;
        }

        return coefficients[..n];
    }

    #endregion
}

using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Adaptive;
using AI.ControlSystems.Nonlinear;
using AI.ControlSystems.Optimal;
using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;
using Xunit;
using Complex = System.Numerics.Complex;

namespace AIFramework.UnitTests;

/// <summary>
/// MPC сверяется с LQR и с рекурсией Риккати там, где ограничения не активны, и проверяется на
/// соблюдение ограничений там, где активны. LQG — принципом разделения на явно собранной
/// замкнутой системе и методом Монте-Карло по средней стоимости. Адаптивные и скользящие
/// регуляторы — на объектах с неизвестными параметрами и возмущениями.
/// </summary>
public class ControlSystemsAdvancedTests
{
    private const double Dt = 0.1;
    private static readonly Matrix A = new(new double[,] { { 1, Dt }, { 0, 1 } });
    private static readonly Matrix B = new(new double[,] { { 0.5 * Dt * Dt }, { Dt } });

    #region MPC

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public void Mpc_WithoutConstraints_EqualsLqr(int horizon)
    {
        var q = M(new double[,] { { 1, 0 }, { 0, 0.1 } });
        var r = M(new double[,] { { 0.1 } });
        Matrix k = DiscreteLqr.Solve(A, B, q, r);
        var mpc = new ModelPredictiveController(A, B, q, r, horizon);
        var rng = new Random(horizon);

        for (int trial = 0; trial < 10; trial++)
        {
            var x = new Vector(new[] { (rng.NextDouble() * 10) - 5, (rng.NextDouble() * 4) - 2 });
            double expected = -((k[0, 0] * x[0]) + (k[0, 1] * x[1]));

            Assert.Equal(expected, mpc.Compute(x)[0], 7);
        }
    }

    [Fact]
    public void Mpc_WithoutConstraints_EqualsFiniteHorizonRiccati()
    {
        var q = M(new double[,] { { 2, 0 }, { 0, 0.5 } });
        var r = M(new double[,] { { 0.3 } });
        var qf = M(new double[,] { { 5, 1 }, { 1, 1 } });
        Matrix k0 = LinearQuadraticMpc.ComputeFirstGain(A, B, q, r, qf, 7);
        var mpc = new ModelPredictiveController(A, B, q, r, 7, qf);
        var x = new Vector(new[] { 1.5, -0.7 });

        Assert.Equal(-((k0[0, 0] * x[0]) + (k0[0, 1] * x[1])), mpc.Compute(x)[0], 8);
    }

    [Fact]
    public void Mpc_OneStepWithBox_IsClippedOptimum()
    {
        var q = M(new double[,] { { 1, 0 }, { 0, 1 } });
        var r = M(new double[,] { { 0.01 } });
        var qf = M(new double[,] { { 3, 0 }, { 0, 2 } });
        Matrix k = LinearQuadraticMpc.ComputeFirstGain(A, B, q, r, qf, 1);

        foreach (double position in new[] { -20.0, -0.3, 0.1, 4.0, 30.0 })
        {
            var mpc = new ModelPredictiveController(A, B, q, r, 1, qf)
            {
                InputLower = new Vector(new[] { -1.0 }),
                InputUpper = new Vector(new[] { 1.0 })
            };
            var x = new Vector(new[] { position, 0.5 });
            double unconstrained = -((k[0, 0] * x[0]) + (k[0, 1] * x[1]));

            // Одномерная выпуклая задача: оптимум на отрезке — обрезанный безусловный
            Assert.Equal(Math.Clamp(unconstrained, -1, 1), mpc.Compute(x)[0], 8);
        }
    }

    [Fact]
    public void Mpc_RespectsInputBounds_WhereLqrWouldSaturate()
    {
        var q = M(new double[,] { { 1, 0 }, { 0, 0.1 } });
        var r = M(new double[,] { { 0.01 } });
        var x = new Vector(new[] { 10.0, 0.0 });
        Matrix k = DiscreteLqr.Solve(A, B, q, r);

        Assert.True(Math.Abs((k[0, 0] * x[0]) + (k[0, 1] * x[1])) > 1, "LQR должен требовать больше предела");

        var mpc = new ModelPredictiveController(A, B, q, r, 20)
        {
            InputLower = new Vector(new[] { -1.0 }),
            InputUpper = new Vector(new[] { 1.0 })
        };

        for (int step = 0; step < 400; step++)
        {
            MpcStep result = mpc.Step(x);

            Assert.Equal(SolverStatus.Optimal, result.Status);
            Assert.InRange(result.Input[0], -1 - 1e-9, 1 + 1e-9);
            x = Mul(A, x) + Mul(B, result.Input);
        }

        Assert.True(Math.Abs(x[0]) < 0.05 && Math.Abs(x[1]) < 0.05, $"x = [{x[0]}, {x[1]}]");
    }

    [Fact]
    public void Mpc_RespectsRateLimits()
    {
        var mpc = new ModelPredictiveController(A, B, M(new double[,] { { 1, 0 }, { 0, 0.1 } }), M(new double[,] { { 0.01 } }), 15)
        {
            InputLower = new Vector(new[] { -1.0 }),
            InputUpper = new Vector(new[] { 1.0 }),
            RateLower = new Vector(new[] { -0.2 }),
            RateUpper = new Vector(new[] { 0.2 })
        };
        var x = new Vector(new[] { 5.0, 0.0 });
        double previous = 0;

        for (int step = 0; step < 200; step++)
        {
            double u = mpc.Compute(x)[0];

            Assert.True(Math.Abs(u - previous) <= 0.2 + 1e-9, $"шаг {step}: Δu = {u - previous}");
            previous = u;
            x = Mul(A, x) + Mul(B, new Vector(new[] { u }));
        }
    }

    [Fact]
    public void Mpc_SoftStateLimit_CapsVelocity_WhileTrackingReference()
    {
        var q = M(new double[,] { { 1, 0 }, { 0, 0.01 } });
        var r = M(new double[,] { { 0.01 } });

        double PeakVelocity(bool limited, out double finalPosition)
        {
            var mpc = new ModelPredictiveController(A, B, q, r, 25)
            {
                InputLower = new Vector(new[] { -2.0 }),
                InputUpper = new Vector(new[] { 2.0 }),
                StateUpper = limited ? new Vector(new[] { double.PositiveInfinity, 1.0 }) : null,
                StateReference = new Vector(new[] { 10.0, 0.0 })
            };

            var x = new Vector(2);
            double peak = 0;

            for (int step = 0; step < 300; step++)
            {
                x = Mul(A, x) + Mul(B, mpc.Compute(x));
                peak = Math.Max(peak, x[1]);
            }

            finalPosition = x[0];
            return peak;
        }

        Assert.True(PeakVelocity(false, out _) > 1.2);
        Assert.True(PeakVelocity(true, out double position) <= 1.0 + 1e-3);
        Assert.Equal(10, position, 0.05);
    }

    [Fact]
    public void Mpc_InfeasibleHardStateLimit_FallsBack_SoftLimitDegradesGracefully()
    {
        var q = M(new double[,] { { 1, 0 }, { 0, 0.1 } });
        var r = M(new double[,] { { 0.01 } });
        var lower = new Vector(new[] { -1.0 });
        var upper = new Vector(new[] { 1.0 });
        var velocityLimit = new Vector(new[] { double.PositiveInfinity, 1.0 });

        var hard = new ModelPredictiveController(A, B, q, r, 10)
        {
            InputLower = lower, InputUpper = upper, StateUpper = velocityLimit, SoftStateConstraints = false
        };
        MpcStep first = hard.Step(new Vector(new[] { 1.0, 0.0 }));
        Assert.False(first.UsedFallback);

        // Скорость 5 при пределе 1: за шаг 0.1 при |u| ≤ 1 её не погасить, задача несовместна
        MpcStep second = hard.Step(new Vector(new[] { 1.0, 5.0 }));
        Assert.True(second.UsedFallback);
        Assert.Equal(SolverStatus.Infeasible, second.Status);
        Assert.Equal(first.PredictedInputs[1][0], second.Input[0], 12);

        // Мягкое ограничение: задача решается, нарушение минимально — тормозить изо всех сил
        var soft = new ModelPredictiveController(A, B, q, r, 10) { InputLower = lower, InputUpper = upper, StateUpper = velocityLimit };
        MpcStep relaxed = soft.Step(new Vector(new[] { 1.0, 5.0 }));
        Assert.Equal(SolverStatus.Optimal, relaxed.Status);
        Assert.Equal(3.9, relaxed.StateViolation, 6);
        Assert.Equal(-1, relaxed.Input[0], 9);
    }

    #endregion

    #region LQG

    [Fact]
    public void Lqg_ClosedLoopPolesFollowSeparationPrinciple()
    {
        var c = M(new double[,] { { 1, 0 } });
        LqgRegulator lqg = LqgRegulator.Design(A, B, c, M(new double[,] { { 1, 0 }, { 0, 0.1 } }), M(new double[,] { { 0.1 } }),
            M(new double[,] { { 1e-4, 0 }, { 0, 1e-3 } }), M(new double[,] { { 0.01 } }));

        // Замкнутая система в координатах (x, x̂): x' = Ax − BKx̂, x̂' = LCAx + (A − BK − LCA)x̂
        Matrix k = lqg.StateFeedbackGain;
        Matrix l = lqg.EstimatorGain;
        Matrix lca = l * c * A;
        Matrix lower = A - (B * k) - lca;
        Matrix upperRight = (B * k) * -1;
        var closed = new Matrix(4, 4);

        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
            {
                closed[i, j] = A[i, j];
                closed[i, j + 2] = upperRight[i, j];
                closed[i + 2, j] = lca[i, j];
                closed[i + 2, j + 2] = lower[i, j];
            }

        foreach (Complex pole in Eigen.General(closed))
            Assert.Contains(lqg.ClosedLoopPoles, p => Complex.Abs(p - pole) < 1e-8);

        Assert.Equal(4, lqg.ClosedLoopPoles.Length);
    }

    [Fact]
    public void Lqg_MonteCarloCostMatchesPrediction()
    {
        var c = M(new double[,] { { 1, 0 } });
        var q = M(new double[,] { { 1, 0 }, { 0, 0.1 } });
        var r = M(new double[,] { { 0.1 } });
        double w0 = 1e-4, w1 = 1e-3, v = 0.01;
        LqgRegulator lqg = LqgRegulator.Design(A, B, c, q, r, M(new double[,] { { w0, 0 }, { 0, w1 } }), M(new double[,] { { v } }));

        var rng = new Random(8);
        var x = new Vector(2);
        var uPrevious = new Vector(new[] { 0.0 });
        double cost = 0;
        int count = 0;

        for (int k = 0; k < 100_000; k++)
        {
            var y = new Vector(new[] { x[0] + (Gauss(rng) * Math.Sqrt(v)) });
            Vector u = lqg.Step(uPrevious, y);

            if (k >= 1000)
            {
                cost += Quadratic(q, x) + Quadratic(r, u);
                count++;
            }

            x = Mul(A, x) + Mul(B, u) + new Vector(new[] { Gauss(rng) * Math.Sqrt(w0), Gauss(rng) * Math.Sqrt(w1) });
            uPrevious = u;
        }

        Assert.Equal(lqg.ExpectedCost, cost / count, lqg.ExpectedCost * 0.05);
    }

    #endregion

    #region Адаптивное управление

    [Fact]
    public void FirstOrderMrac_ConvergesToIdealGains_WithNegativePlantGain()
    {
        // Объект ẏ = 0.5y − 2u: неустойчив, усиление отрицательно. θ_r* = a_m/b, θ_y* = −(a + a_m)/b
        double a = 0.5, b = -2, am = 2;
        var mrac = new ModelReferenceAdaptiveController { AdaptationGain = 4, ReferencePole = am, Theta = 0, PlantGainSign = -1 };
        double y = 0, dt = 0.001;

        for (int k = 0; k < 150_000; k++)
        {
            double r = Math.Floor(k * dt / 4) % 2 == 0 ? 1 : -0.5;
            y += ((a * y) + (b * mrac.Compute(r, y, dt))) * dt;
        }

        Assert.Equal(am / b, mrac.Theta, 0.05);
        Assert.Equal(-(a + am) / b, mrac.FeedbackGain, 0.05);
        Assert.True(Math.Abs(mrac.TrackingError) < 0.01);
    }

    [Fact]
    public void StateMrac_MakesUnstablePlantBehaveLikeReferenceModel()
    {
        // ẋ = Ax + BΛu с неизвестными A и Λ; эталон с ω = 2, ζ = 0.7
        var a = M(new double[,] { { 0, 1 }, { 2, -1 } });
        var b = M(new double[,] { { 0 }, { 1 } });
        double lambda = 1.5;
        var am = M(new double[,] { { 0, 1 }, { -4, -2.8 } });
        var bm = M(new double[,] { { 0 }, { 4 } });

        var mrac = new StateModelReferenceAdaptiveController(am, bm, b) { StateAdaptationGain = 20, ReferenceAdaptationGain = 20 };
        var x = new Vector(2);
        double dt = 0.001, worst = 0;

        for (int k = 0; k < 120_000; k++)
        {
            double t = k * dt;
            var r = new Vector(new[] { Math.Floor(t / 5) % 2 == 0 ? 1.0 : -1.0 });
            Vector u = mrac.Compute(x, r, dt);
            Vector derivative = Mul(a, x) + (Mul(b, u) * lambda);
            x = x + (derivative * dt);

            if (t > 110)
                worst = Math.Max(worst, Math.Abs(mrac.TrackingError[0]));
        }

        Assert.True(worst < 0.02, $"ошибка слежения {worst}");

        // Условия согласования: A + BΛK_x* = A_m, BΛK_r* = B_m
        Matrix kx = mrac.StateGain;
        Assert.Equal(-6 / lambda, kx[0, 0], 0.3);
        Assert.Equal(-1.8 / lambda, kx[0, 1], 0.3);
        Assert.Equal(4 / lambda, mrac.ReferenceGain[0, 0], 0.3);
    }

    #endregion

    #region Скользящий режим

    [Fact]
    public void StateSlidingMode_ReachesSurfaceAndIgnoresMatchedDisturbance()
    {
        // ẍ = u + d, |d| ≤ 0.4; поверхность s = 2x + ẋ, на ней ẋ = −2x
        var ac = M(new double[,] { { 0, 1 }, { 0, 0 } });
        var bc = M(new double[,] { { 0 }, { 1 } });
        var surface = M(new double[,] { { 2, 1 } });

        Vector Run(bool disturbed, out double reachTime)
        {
            var smc = new StateSlidingModeController(ac, bc, surface) { SwitchingGain = new Vector(new[] { 1.0 }), BoundaryLayer = 0.005 };
            var x = new Vector(new[] { 1.0, 0.0 });
            double dt = 1e-4;
            reachTime = double.NaN;

            for (int k = 0; k < 80_000; k++)
            {
                double t = k * dt;
                double d = disturbed ? 0.4 * Math.Sin(2 * t) : 0;
                double u = smc.Compute(x)[0];
                x = new Vector(new[] { x[0] + (x[1] * dt), x[1] + ((u + d) * dt) });

                if (double.IsNaN(reachTime) && Math.Abs((2 * x[0]) + x[1]) < 0.01)
                    reachTime = t;
            }

            return x;
        }

        Vector clean = Run(false, out double cleanReach);
        Vector noisy = Run(true, out double noisyReach);

        // Время достижения не больше |s₀|/(K − |d|) = 2/0.6
        Assert.True(noisyReach < 2 / 0.6, $"поверхность достигнута к {noisyReach}");
        Assert.True(cleanReach < 2.0);
        Assert.True(Math.Abs(noisy[0]) < 0.01 && Math.Abs(clean[0]) < 0.01);
        Assert.Equal(clean[0], noisy[0], 0.005);
    }

    [Fact]
    public void ScalarSlidingMode_SecondOrderPlantWithDisturbance_ReachesSetpoint()
    {
        // Пример из SlidingMode.md: ÿ = −0.5ẏ + u + 0.3·sin t, s = e + 3ė
        var smc = new SlidingModeController { Lambda = 3, Gain = 15, SmoothingBoundary = 0.05 };
        double position = 0, speed = 0, dt = 0.001;

        for (int k = 0; k < 20_000; k++)
        {
            double u = smc.Compute(1.0, position, dt);
            speed += (-0.5 * speed + u + (0.3 * Math.Sin(k * dt))) * dt;
            position += speed * dt;
        }

        Assert.Equal(1, position, 0.02);
    }

    [Fact]
    public void SuperTwisting_ConvergesInFiniteTime_WithContinuousControl()
    {
        var controller = SuperTwistingController.ForDisturbanceRate(0.5);
        double s = 1, dt = 1e-3, maxJump = 0, previous = double.NaN, worstLate = 0;

        for (int k = 0; k < 10_000; k++)
        {
            double t = k * dt;
            double u = controller.Compute(s, dt);

            if (!double.IsNaN(previous) && t > 1)
                maxJump = Math.Max(maxJump, Math.Abs(u - previous));

            previous = u;
            s += (u + (0.5 * Math.Sin(t))) * dt;

            if (t > 5)
                worstLate = Math.Max(worstLate, Math.Abs(s));
        }

        Assert.True(worstLate < 1e-3, $"|s| = {worstLate}");
        Assert.True(maxJump < 0.05, $"скачок управления {maxJump}");

        // Интегратор оценивает возмущение: v ≈ −d(t)
        Assert.Equal(-0.5 * Math.Sin(10), controller.Integral, 0.05);
    }

    #endregion

    #region Инструменты

    private static Matrix M(double[,] values) => new(values);

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

    private static double Gauss(Random rng)
        => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());

    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using AI.Charts;
using AI.ControlSystems.Adaptive;
using AI.ControlSystems.Identification;
using AI.ControlSystems.Linear;
using AI.ControlSystems.Nonlinear;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using AI.ControlSystems.Observers;
using AI.ControlSystems.Optimal;
using AI.ControlSystems.Pid;
using AI.DataStructs.Algebraic;
using AI.Charts.JS;
using AiFrameworkDemo.Core;
using SkiaSharp;
using Matrix = AI.DataStructs.Algebraic.Matrix;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.ControlSystems;

public record ControlDemoResult(string Key, string Title, string Subtitle, string PngDataUrl, string CardClass);

public record ControlPreview(string Key, string Title, string Subtitle, bool DefaultOn, string CategoryId, string CardClass);

public static partial class ControlDemoRunner
{
    public sealed class Settings
    {
        public int Width     { get; set; } = 680;
        public int Height    { get; set; } = 360;
        public bool DarkTheme { get; set; } = true;
    }

    private static readonly string[] Cards = ["emerald", "indigo", "violet", "sky", "amber", "pink", "cyan"];
    private static string C(int i) => Cards[i % Cards.Length];

    public static IReadOnlyList<ControlPreview> OrderedKeys { get; } = BuildKeys();

    private static ControlPreview[] BuildKeys()
    {
        int n = 0;
        return
        [
            new("pid_ms_damper",  "ПИД: масса-пружина",       "PidController, 2-й порядок",              true,  "pid",            C(n++)),
            new("pid_imc",        "ПИД: IMC настройка",        "ImcPidTuning.FirstOrderPi",               true,  "pid",            C(n++)),
            new("pid_slew",       "Ограничитель нарастания",   "SlewRateLimiter",                         true,  "pid",            C(n++)),
            new("pid_vector2",    "Векторный ПИД (2 канала)",  "VectorPidController",                     false, "pid",            C(n++)),

            new("lqr_dbl_int",    "LQR: двойной интегратор",  "DiscreteLqr.Solve + Q/R",                 true,  "optimal",        C(n++)),
            new("mpc_horizon",    "MPC: N=3 vs N=20",         "LinearQuadraticMpc.ComputeFirstGain",     true,  "optimal",        C(n++)),
            new("lqg_demo",       "LQG = LQR + Калман",       "LqgRegulator + шумные измерения",         true,  "optimal",        C(n++)),

            new("obs_kalman",     "Фильтр Калмана",            "KalmanFilter.Predict / Update",           true,  "observers",      C(n++)),
            new("obs_luenberger", "Наблюдатель Люенбергера",  "LuenbergerObserver.Step",                 true,  "observers",      C(n++)),
            new("obs_ekf",        "EKF: нелинейный маятник",  "ExtendedKalmanFilter + якобиан",          false, "observers",      C(n++)),

            new("lin_poles",      "Метод полюсов (Аккерман)", "PolePlacement.AckermannGain",             true,  "linear",         C(n++)),
            new("lin_zoh",        "ZOH дискретизация",        "Discretization.ZeroOrderHold",            true,  "linear",         C(n++)),

            new("nl_smc",         "Скользящий режим (SMC)",   "SlidingModeController, λ=2, G=8",         true,  "nonlinear",      C(n++)),
            new("nl_mrac",        "MRAC",                     "ModelReferenceAdaptiveController",        true,  "nonlinear",      C(n++)),

            new("id_rls",         "RLS-идентификация",        "RecursiveLeastSquares.Update",            true,  "identification", C(n++)),
        ];
    }

    // -------------------------------------------------------------------------
    /// <summary>Render a simulation with user-supplied parameter overrides.</summary>
    public static (string png, string? plotlyJson, ChartView cv) RenderWithParams(
        string key,
        IReadOnlyDictionary<string, double> p,
        Settings cfg)
    {
        double G(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;
        var cv = MakeView(cfg);

        switch (key)
        {
            // ============================== PID ==============================

            case "pid_ms_damper":
            {
                double Kp = G("Kp", 8), Ki = G("Ki", 4), Kd = G("Kd", 1.2);
                double omega = G("omega", 2), zeta = G("zeta", 0.3);
                double r1 = G("r1", 1.0), r2 = G("r2", 1.5);
                const double dt = 0.05;
                double k = omega * omega, c = 2 * zeta * omega;
                Discretization.ZeroOrderHold(M4(0,1,-k,-c), Col(0,1), dt, out var ad, out var bd);
                var plant = new DiscreteLtiModel(ad, bd, Row(1,0), Zero1x1());
                var pid = new PidController { Kp=Kp, Ki=Ki, Kd=Kd, OutputMin=-30, OutputMax=30, DerivativeOnMeasurement=true };
                const int N = 280;
                double[] ta = new double[N], ya = new double[N], ra = new double[N];
                double u = 0;
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt;
                    double r = i < N / 2 ? r1 : r2;
                    ra[i] = r; ya[i] = plant.Step(Vs(u))[0];
                    u = pid.Compute(r, ya[i], dt);
                }
                cv.ChartName = $"ПИД масса-пружина  ω={omega:F1} рад/с, ζ={zeta:F2}  Kp={Kp}, Ki={Ki}, Kd={Kd}";
                cv.LabelX = "t, с"; cv.LabelY = "x";
                cv.AddPlot(Tv(ta), Tv(ra), "r(t) — уставка",  HexC("#64748b"), 1);
                cv.AddPlot(Tv(ta), Tv(ya), "y(t) — выход",    HexC("#38bdf8"), 2);
                break;
            }

            case "pid_imc":
            {
                double K = G("K", 2), tau = G("tau", 3), lambda = G("lambda", 1.5);
                const double dt = 0.1;
                ImcPidTuning.FirstOrderPi(K, tau, lambda, out double kp, out double ki);
                var pid = new PidController { Kp=kp, Ki=ki, Kd=0, OutputMin=-20, OutputMax=20 };
                Discretization.ZeroOrderHold(M1(-1.0/tau), M1(K/tau), dt, out var ad, out var bd);
                var plant = new DiscreteLtiModel(ad, bd, M1(1.0), M1(0.0));
                const int N = 200;
                double[] ta = new double[N], ya = new double[N];
                double u = 0;
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; ya[i] = plant.Step(Vs(u))[0];
                    u = pid.Compute(1.0, ya[i], dt);
                }
                double[] rLine = Enumerable.Repeat(1.0, N).ToArray();
                cv.ChartName = $"IMC-PI: G(s)={K:F2}/({tau:F1}s+1)  λ={lambda:F2}  ->  Kp={kp:F3}, Ki={ki:F3}";
                cv.LabelX = "t, с"; cv.LabelY = "y";
                cv.AddPlot(Tv(ta), Tv(rLine), "r=1",  HexC("#64748b"), 1);
                cv.AddPlot(Tv(ta), Tv(ya),    "y(t)", HexC("#34d399"), 2);
                break;
            }

            case "pid_slew":
            {
                double maxD = Math.Max(0.005, G("maxDelta", 0.12));
                var slew = new SlewRateLimiter(-maxD, maxD);
                const int N = 120; const double dt = 0.05;
                double[] ta = new double[N], desired = new double[N], limited = new double[N];
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt;
                    double dv = i < 20 ? 0.0 : i < 70 ? 1.0 : i < 100 ? 0.4 : 0.0;
                    desired[i] = dv; limited[i] = slew.Limit(dv);
                }
                cv.ChartName = $"SlewRateLimiter: maxΔ=±{maxD:F3}/шаг";
                cv.LabelX = "t, с"; cv.LabelY = "значение";
                cv.AddPlot(Tv(ta), Tv(desired), "Желаемый",     HexC("#f472b6"), 1);
                cv.AddPlot(Tv(ta), Tv(limited), "Ограниченный", HexC("#fbbf24"), 2);
                break;
            }

            case "pid_vector2":
            {
                double kp1 = G("Kp1", 4), ki1 = G("Ki1", 1.5);
                double kp2 = G("Kp2", 6), ki2 = G("Ki2", 2.5);
                double rv1 = G("r1", 1.0), rv2 = G("r2", -0.6);
                const double dt = 0.1;
                var vpc = new VectorPidController(2);
                vpc[0].Kp=kp1; vpc[0].Ki=ki1; vpc[0].OutputMin=-12; vpc[0].OutputMax=12;
                vpc[1].Kp=kp2; vpc[1].Ki=ki2; vpc[1].OutputMin=-12; vpc[1].OutputMax=12;
                Discretization.ZeroOrderHold(M1(-0.5), M1(1.0), dt, out var ad1, out var bd1);
                Discretization.ZeroOrderHold(M1(-0.3), M1(0.8), dt, out var ad2, out var bd2);
                var p1 = new DiscreteLtiModel(ad1, bd1, M1(1.0), M1(0.0));
                var p2 = new DiscreteLtiModel(ad2, bd2, M1(1.0), M1(0.0));
                const int N = 200;
                double[] ta = new double[N], y1a = new double[N], y2a = new double[N],
                    r1a = new double[N], r2a = new double[N];
                var uv = new Vector(new double[] { 0.0, 0.0 });
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; r1a[i] = rv1; r2a[i] = rv2;
                    y1a[i] = p1.Step(Vs(uv[0]))[0]; y2a[i] = p2.Step(Vs(uv[1]))[0];
                    uv = vpc.Compute(new Vector(new double[]{rv1,rv2}), new Vector(new double[]{y1a[i],y2a[i]}), dt);
                }
                cv.ChartName = $"Векторный ПИД: r₁={rv1:F1}, r₂={rv2:F1}";
                cv.LabelX = "t, с"; cv.LabelY = "y";
                cv.AddPlot(Tv(ta), Tv(r1a), "r₁",      HexC("#64748b"), 1);
                cv.AddPlot(Tv(ta), Tv(y1a), "y₁ кан.1",HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(r2a), "r₂",      HexC("#94a3b8"), 1);
                cv.AddPlot(Tv(ta), Tv(y2a), "y₂ кан.2",HexC("#f472b6"), 2);
                break;
            }

            // ============================== OPTIMAL ==============================

            case "lqr_dbl_int":
            {
                double q11 = G("Q11", 10), q22 = G("Q22", 1), r = G("R", 1);
                double x0v = G("x01", 2);
                const double dt = 0.05;
                Discretization.ZeroOrderHold(M4(0,1,0,0), Col(0,1), dt, out var ad, out var bd);
                var Qm = Diag(q11, q22); var Rm = Diag(r);
                var K = DiscreteLqr.Solve(ad, bd, Qm, Rm);
                var x = new Vector(new double[] { x0v, 0.0 });
                const int N = 200;
                double[] ta = new double[N], x1 = new double[N], x2 = new double[N], ua = new double[N];
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; x1[i] = x[0]; x2[i] = x[1];
                    double us = -KxScalar(K, x); ua[i] = us;
                    x = MV(ad, x) + MV(bd, Vs(us));
                }
                cv.ChartName = $"LQR двойной интегратор  Q=diag({q11:F0},{q22:F1}), R={r:F1}, x₀={x0v:F1}";
                cv.LabelX = "t, с"; cv.LabelY = "";
                cv.AddPlot(Tv(ta), Tv(x1), "x₁ позиция",  HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(x2), "x₂ скорость", HexC("#34d399"), 2);
                cv.AddPlot(Tv(ta), Tv(ua.Select(v=>v*0.2).ToArray()), "u×0.2", HexC("#fbbf24"), 1);
                break;
            }

            case "mpc_horizon":
            {
                double q11 = G("Q11", 10), q22 = G("Q22", 1), r = G("R", 1);
                int n1 = (int)Math.Max(1, G("N1", 3)), n2 = (int)Math.Max(2, G("N2", 20));
                double x0v = G("x01", 2);
                const double dt = 0.05;
                Discretization.ZeroOrderHold(M4(0,1,0,0), Col(0,1), dt, out var ad, out var bd);
                var Qm = Diag(q11, q22); var Rm = Diag(r); var Qf = Qm;
                var K1 = LinearQuadraticMpc.ComputeFirstGain(ad, bd, Qm, Rm, Qf, n1);
                var K2 = LinearQuadraticMpc.ComputeFirstGain(ad, bd, Qm, Rm, Qf, n2);
                const int N = 200;
                double[] ta = new double[N], xa = new double[N], xb = new double[N];
                var xA = new Vector(new double[] { x0v, 0.0 });
                var xB = new Vector(new double[] { x0v, 0.0 });
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; xa[i] = xA[0]; xb[i] = xB[0];
                    xA = MV(ad, xA) + MV(bd, Vs(-KxScalar(K1, xA)));
                    xB = MV(ad, xB) + MV(bd, Vs(-KxScalar(K2, xB)));
                }
                cv.ChartName = $"MPC горизонт N={n1} vs N={n2}  (x₁ — позиция)";
                cv.LabelX = "t, с"; cv.LabelY = "x₁";
                cv.AddPlot(Tv(ta), Tv(xa), $"N={n1}",  HexC("#f472b6"), 2);
                cv.AddPlot(Tv(ta), Tv(xb), $"N={n2}",  HexC("#34d399"), 2);
                break;
            }

            case "lqg_demo":
            {
                double q11 = G("Q11", 10), r = G("R", 1), sigV = G("sigmaV", 0.7);
                const double dt = 0.05;
                var cc = Row(1, 0); var dc = Zero1x1();
                Discretization.ZeroOrderHold(M4(0,1,0,0), Col(0,1), dt, out var ad, out var bd);
                var K = DiscreteLqr.Solve(ad, bd, Diag(q11, 1), Diag(r));
                var kf = new KalmanFilter(ad, bd, cc, dc, Diag(0.01, 0.01), Diag(0.5));
                var lqg = new LqgRegulator(kf, K);
                var plant = new DiscreteLtiModel(ad, bd, cc, dc, new Vector(new double[] { 2.0, 0.0 }));
                var rng = new Random(17);
                const int N = 200;
                double[] ta = new double[N], truePos = new double[N], estPos = new double[N], noisy = new double[N];
                var u = new Vector(new double[] { 0.0 });
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt;
                    var y = plant.Step(u);
                    truePos[i] = plant.State[0];
                    noisy[i] = y[0] + rng.NextGaussian() * sigV;
                    u = lqg.Step(u, Vs(noisy[i]));
                    estPos[i] = lqg.Filter.State[0];
                }
                cv.ChartName = $"LQG двойной интегратор  σ_v={sigV:F2}  Q₁₁={q11:F0}, R={r:F1}";
                cv.LabelX = "t, с"; cv.LabelY = "x₁";
                cv.AddPlot(Tv(ta), Tv(noisy),   "y шумное",    HexC("#334155"), 1);
                cv.AddPlot(Tv(ta), Tv(truePos), "x₁ истинная", HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(estPos),  "x̂₁ Калман",  HexC("#f472b6"), 2);
                break;
            }

            // ============================== OBSERVERS ========================

            case "obs_kalman":
            {
                double qk = G("Qk", 0.05), rk = G("Rk", 1.0), uc = G("uConst", 0.8), sm = G("sigmaM", 1.0);
                const double dt = 0.1;
                Discretization.ZeroOrderHold(M1(-0.5), M1(1.0), dt, out var ad, out var bd);
                var kf = new KalmanFilter(ad, bd, M1(1.0), M1(0.0), Diag(qk), Diag(rk));
                var plant = new DiscreteLtiModel(ad, bd, M1(1.0), M1(0.0));
                var rng = new Random(42);
                const int N = 160;
                double[] ta = new double[N], trueY = new double[N], nY = new double[N], kfE = new double[N];
                var uVec = Vs(uc);
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt;
                    double y = plant.Step(uVec)[0];
                    trueY[i] = y; nY[i] = y + rng.NextGaussian() * sm;
                    kf.Predict(uVec); kf.Update(Vs(nY[i]), uVec);
                    kfE[i] = kf.State[0];
                }
                cv.ChartName = $"Фильтр Калмана  Q={qk:F3}, R={rk:F2}, u={uc:F2}, σ={sm:F2}";
                cv.LabelX = "t, с"; cv.LabelY = "y";
                cv.AddPlot(Tv(ta), Tv(nY),    "y шумное",   HexC("#334155"), 1);
                cv.AddPlot(Tv(ta), Tv(trueY), "y истинное", HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(kfE),   "x̂ Калмана", HexC("#f472b6"), 2);
                break;
            }

            case "obs_luenberger":
            {
                double l1 = G("L1", 0.45), l2 = G("L2", 1.0), x0v = G("x01", 1.5);
                const double dt = 0.05;
                Discretization.ZeroOrderHold(M4(0,1,0,0), Col(0,1), dt, out var ad, out var bd);
                var cc = Row(1, 0);
                var L = new Matrix(new double[,] { { l1 }, { l2 } });
                var obs = new LuenbergerObserver(ad, bd, cc, Zero1x1(), L);
                var plant = new DiscreteLtiModel(ad, bd, cc, Zero1x1(), new Vector(new double[] { x0v, 0.0 }));
                const int N = 200;
                double[] ta = new double[N], tx1 = new double[N], tx2 = new double[N],
                    ex1 = new double[N], ex2 = new double[N];
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt;
                    double us = -0.8 * plant.State[0] - 0.5 * plant.State[1];
                    var y = plant.Step(Vs(us));
                    tx1[i] = plant.State[0]; tx2[i] = plant.State[1];
                    obs.Step(Vs(us), y);
                    ex1[i] = obs.State[0]; ex2[i] = obs.State[1];
                }
                cv.ChartName = $"Наблюдатель Люенбергера  L=[{l1:F2}; {l2:F2}]";
                cv.LabelX = "t, с"; cv.LabelY = "";
                cv.AddPlot(Tv(ta), Tv(tx1), "x₁ истинная",  HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(ex1), "x̂₁ оценка",   HexC("#38bdf8"), 1);
                cv.AddPlot(Tv(ta), Tv(tx2), "x₂ истинная",  HexC("#34d399"), 2);
                cv.AddPlot(Tv(ta), Tv(ex2), "x̂₂ оценка",   HexC("#34d399"), 1);
                break;
            }

            case "obs_ekf":
            {
                double theta0Deg = G("theta0Deg", 30), errDeg = G("errDeg", 11.5);
                double sigmaM = G("sigmaM", 0.05), bDamp = G("bDamp", 0.3);
                const double dt = 0.02, g = 9.81, Lpend = 1.0;
                double theta0 = theta0Deg * Math.PI / 180.0;
                double errRad = errDeg * Math.PI / 180.0;
                var Qe = Diag(1e-4, 1e-3); var Re = Diag(sigmaM * sigmaM);
                var ekf = new ExtendedKalmanFilter(
                    x0: new Vector(new double[] { theta0 + errRad, 0.0 }),
                    p0: Diag(0.1, 0.1));
                var rng = new Random(99);
                const int N = 300;
                double[] ta = new double[N], tTrue = new double[N], tEkf = new double[N];
                double thetaT = theta0, omegaT = 0.0;
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; tTrue[i] = thetaT;
                    double th = ekf.State[0], om = ekf.State[1];
                    var xNext = new Vector(new double[] {
                        th + om * dt,
                        om + (-g / Lpend * Math.Sin(th) - bDamp * om) * dt });
                    var F = new Matrix(new double[,] {
                        { 1.0, dt },
                        { -g / Lpend * Math.Cos(th) * dt, 1.0 - bDamp * dt } });
                    ekf.Predict(xNext, F, Qe);
                    var H = new Matrix(new double[,] { { 1.0, 0.0 } });
                    ekf.Update(Vs(thetaT + rng.NextGaussian() * sigmaM), Vs(ekf.State[0]), H, Re);
                    tEkf[i] = ekf.State[0];
                    omegaT += (-g / Lpend * Math.Sin(thetaT) - bDamp * omegaT) * dt;
                    thetaT += omegaT * dt;
                }
                cv.ChartName = $"EKF маятник  θ₀={theta0Deg:F0}°, δ={errDeg:F0}°, σ={sigmaM:F3}, b={bDamp:F2}";
                cv.LabelX = "t, с"; cv.LabelY = "θ, рад";
                cv.AddPlot(Tv(ta), Tv(tTrue), "θ истинная", HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(tEkf),  "θ̂ EKF",     HexC("#f472b6"), 2);
                break;
            }

            // ============================== LINEAR ===========================

            case "lin_poles":
            {
                double pv1 = G("p1", 0.75), pv2 = G("p2", 0.70), x0v = G("x01", 1.0);
                const double dt = 0.05;
                Discretization.ZeroOrderHold(M4(0,1,-1,-0.5), Col(0,1), dt, out var ad, out var bd);
                double c0 = pv1 * pv2, c1 = -(pv1 + pv2);
                var K = PolePlacement.AckermannGain(ad, bd, new Vector(new double[] { c0, c1 }));
                var x = new Vector(new double[] { x0v, 0.0 });
                const int N = 200;
                double[] ta = new double[N], x1 = new double[N];
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; x1[i] = x[0];
                    x = MV(ad, x) + MV(bd, Vs(-KxScalar(K, x)));
                }
                cv.ChartName = $"Метод полюсов: z₁={pv1:F2}, z₂={pv2:F2}  x₀={x0v:F2}";
                cv.LabelX = "t, с"; cv.LabelY = "x₁";
                cv.AddPlot(Tv(ta), Tv(x1), "x₁(t)", HexC("#a78bfa"), 2);
                break;
            }

            case "lin_zoh":
            {
                double omega = G("omega", 2), zeta = G("zeta", 0.3);
                double dt1v = G("dt1", 0.05), dt2v = G("dt2", 0.20);
                double k = omega * omega, c = 2 * zeta * omega;
                var ac = M4(0,1,-k,-c); var bc = Col(0,1);
                Discretization.ZeroOrderHold(ac, bc, dt1v, out var ad1, out var bd1);
                Discretization.ZeroOrderHold(ac, bc, dt2v, out var ad2, out var bd2);
                var pp1 = new DiscreteLtiModel(ad1, bd1, Row(1,0), Zero1x1());
                var pp2 = new DiscreteLtiModel(ad2, bd2, Row(1,0), Zero1x1());
                int N1 = (int)(6.0 / dt1v), N2 = (int)(6.0 / dt2v);
                double[] ta1 = new double[N1], y1a = new double[N1];
                double[] ta2 = new double[N2], y2a = new double[N2];
                for (int i = 0; i < N1; i++) { ta1[i] = i*dt1v; y1a[i] = pp1.Step(Vs(1.0))[0]; }
                for (int i = 0; i < N2; i++) { ta2[i] = i*dt2v; y2a[i] = pp2.Step(Vs(1.0))[0]; }
                cv.ChartName = $"ZOH ω={omega:F1} рад/с ζ={zeta:F2}: dt₁={dt1v:F3}с vs dt₂={dt2v:F3}с";
                cv.LabelX = "t, с"; cv.LabelY = "y";
                cv.AddPlot(Tv(ta1), Tv(y1a), $"dt={dt1v:F3} (точный)", HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta2), Tv(y2a), $"dt={dt2v:F3} (крупный)",HexC("#f472b6"), 2);
                break;
            }

            // ============================== NONLINEAR ========================

            case "nl_smc":
            {
                double lambda = G("lambda", 2.0), gain = G("gain", 8.0), phi = G("phi", 0.25), x0v = G("x0", 2.0);
                const double dt = 0.03;
                var smc = new SlidingModeController { Lambda=lambda, Gain=gain, SmoothingBoundary=phi };
                const int N = 300;
                double[] ta = new double[N], xa = new double[N], ua = new double[N];
                double x = x0v, xd = 0.0;
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; xa[i] = x;
                    double us = smc.Compute(0.0, x, dt); ua[i] = us;
                    xd -= us * dt; x += xd * dt;
                }
                cv.ChartName = $"SMC двойной интегратор  λ={lambda:F1}, K={gain:F0}, Φ={phi:F2}";
                cv.LabelX = "t, с"; cv.LabelY = "";
                cv.AddPlot(Tv(ta), Tv(xa), "x позиция",   HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(ua.Select(v=>v*0.15).ToArray()), "u×0.15", HexC("#fbbf24"), 1);
                break;
            }

            case "nl_mrac":
            {
                double adaptGain = G("adaptGain", 8.0), refPole = G("refPole", 4.0);
                const double dt = 0.05;
                var mrac = new ModelReferenceAdaptiveController { AdaptationGain=adaptGain, ReferencePole=refPole };
                const int N = 300;
                double[] ta = new double[N], ya = new double[N], yma = new double[N];
                double x = 0.0;
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt; ya[i] = x; yma[i] = mrac.ReferenceOutput;
                    double u = mrac.Compute(1.0, x, dt);
                    x += (-2.0 * x + 3.0 * u) * dt;
                }
                cv.ChartName = $"MRAC ẋ=−2x+3u  γ={adaptGain:F1}, λ_m={refPole:F1}";
                cv.LabelX = "t, с"; cv.LabelY = "x";
                cv.AddPlot(Tv(ta), Tv(yma), "x_m эталон",  HexC("#64748b"), 1);
                cv.AddPlot(Tv(ta), Tv(ya),  "x(t) объект", HexC("#38bdf8"), 2);
                break;
            }

            // ============================== IDENTIFICATION ==================

            case "id_rls":
            {
                double th1t = G("th1", 0.7), th2t = G("th2", 0.5);
                double sigV = G("sigmaV", 0.05), lam = G("forgetting", 1.0);
                var rls = new RecursiveLeastSquares(
                    initialTheta: new Vector(new double[] { 0.0, 0.0 }),
                    initialCovariance: 100.0 * Diag(1, 1));
                rls.ForgettingFactor = lam;
                var rng = new Random(7);
                const int N = 200; const double dt = 0.05;
                double[] ta = new double[N], th1a = new double[N], th2a = new double[N],
                    tr1 = new double[N], tr2 = new double[N];
                double yPrev = 0, uPrev = 0;
                for (int i = 0; i < N; i++)
                {
                    ta[i] = i * dt;
                    double u = 0.3 * Math.Sin(2 * Math.PI * 0.5 * i * dt);
                    double y = th1t * yPrev + th2t * uPrev + rng.NextGaussian() * sigV;
                    rls.Update(new Vector(new double[] { yPrev, uPrev }), y);
                    th1a[i] = rls.Theta[0]; th2a[i] = rls.Theta[1];
                    tr1[i] = th1t; tr2[i] = th2t;
                    yPrev = y; uPrev = u;
                }
                cv.ChartName = $"RLS: θ₁={th1t:F2}, θ₂={th2t:F2}, σ={sigV:F3}, λ={lam:F2}";
                cv.LabelX = "шаг k"; cv.LabelY = "θ̂";
                cv.AddPlot(Tv(ta), Tv(tr1),  $"θ₁={th1t:F2} истинное", HexC("#64748b"), 1);
                cv.AddPlot(Tv(ta), Tv(th1a), "θ̂₁ RLS",                 HexC("#38bdf8"), 2);
                cv.AddPlot(Tv(ta), Tv(tr2),  $"θ₂={th2t:F2} истинное", HexC("#94a3b8"), 1);
                cv.AddPlot(Tv(ta), Tv(th2a), "θ̂₂ RLS",                 HexC("#34d399"), 2);
                break;
            }

            default:
                cv.ChartName = key;
                break;
        }

        return (ToPngDataUrl(cv, cfg), PlotlyChartRenderer.ToPlotlyJson(cv), cv);
    }
}

file static class RngExt
{
    public static double NextGaussian(this Random rng)
    {
        double u1 = 1 - rng.NextDouble();
        double u2 = 1 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}

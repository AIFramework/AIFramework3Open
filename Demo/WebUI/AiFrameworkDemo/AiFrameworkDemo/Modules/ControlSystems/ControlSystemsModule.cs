using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.ControlSystems;

public sealed class ControlSystemsModule : LibraryModuleBase
{
    public override string Id => "control-systems";
    public override string Name => "AI.ControlSystems";
    public override string Description => "ПИД, LQR/LQG, MPC, фильтр Калмана, скользящий режим, MRAC, RLS-идентификация";
    public override string Color => "indigo";
    public override string TutorialFolder => "AutoControl";
    public override string IconSvg => """<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg>""";

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("pid", "ПИД-регуляторы",
            "PidController · VectorPidController · ImcPidTuning · SlewRateLimiter",
            [
                new("pid_ms_damper", "ПИД: масса-пружина", "PidController, 2-й порядок", "PidController", "PID.md",
                    [
                        new("Kp","K<sub>p</sub>",0.5,20,8,0.5,Hint:"Пропорциональный коэффициент — реакция на текущую ошибку"),
                        new("Ki","K<sub>i</sub>",0,15,4,0.5,Hint:"Интегральный коэффициент — устраняет статическую ошибку"),
                        new("Kd","K<sub>d</sub>",0,5,1.2,0.1,Hint:"Дифференциальный коэффициент — реакция на скорость изменения ошибки"),
                        new("omega","ω",0.5,6,2,0.5,"рад/с",Hint:"Собственная частота колебательного объекта"),
                        new("zeta","ζ",0.05,1,0.3,0.05,Hint:"Коэффициент демпфирования (0 — нет затухания, 1 — критическое)"),
                        new("r1","r₁",0.1,3,1,0.1,Hint:"Уставка на первой половине симуляции"),
                        new("r2","r₂",0.1,3,1.5,0.1,Hint:"Уставка на второй половине симуляции"),
                    ]),
                new("pid_imc", "ПИД: IMC настройка", "ImcPidTuning.FirstOrderPi", "ImcPidTuning", "PID.md",
                    [
                        new("K","K",0.5,5,2,0.25,Hint:"Статический коэффициент усиления объекта"),
                        new("tau","τ",0.5,10,3,0.5,"с",Hint:"Постоянная времени объекта первого порядка"),
                        new("lambda","λ",0.1,5,1.5,0.1,"с",Hint:"Желаемая постоянная времени замкнутой системы (IMC λ)"),
                    ]),
                new("pid_slew", "Ограничитель нарастания", "SlewRateLimiter", "SlewRateLimiter", "PID.md",
                    [new("maxDelta","maxΔ",0.01,0.5,0.12,0.01,"/шаг",Hint:"Максимальное изменение выходного сигнала за один шаг")]),
                new("pid_vector2", "Векторный ПИД (2 канала)", "VectorPidController", "VectorPidController", "PID.md",
                    [
                        new("Kp1","K<sub>p1</sub>",1,12,4,0.5,Hint:"Пропорциональный коэффициент 1-го канала"),
                        new("Ki1","K<sub>i1</sub>",0,8,1.5,0.25,Hint:"Интегральный коэффициент 1-го канала"),
                        new("r1","r₁",-2,2,1,0.1,Hint:"Уставка 1-го канала"),
                        new("Kp2","K<sub>p2</sub>",1,12,6,0.5,Hint:"Пропорциональный коэффициент 2-го канала"),
                        new("Ki2","K<sub>i2</sub>",0,8,2.5,0.25,Hint:"Интегральный коэффициент 2-го канала"),
                        new("r2","r₂",-2,2,-0.6,0.1,Hint:"Уставка 2-го канала"),
                    ]),
            ]),
        new("optimal", "Оптимальное управление",
            "LQR · MPC · LQG",
            [
                new("lqr_dbl_int", "LQR: двойной интегратор", "DiscreteLqr.Solve + Q/R", "DiscreteLqr", "LQR.md",
                    [
                        new("Q11","Q₁₁",1,100,10,1,Hint:"Штраф за отклонение x₁ (положение) в матрице весов Q"),
                        new("Q22","Q₂₂",0.1,20,1,0.1,Hint:"Штраф за отклонение x₂ (скорость) в матрице весов Q"),
                        new("R","R",0.1,10,1,0.1,Hint:"Штраф за управляющее воздействие u"),
                        new("x01","x₀",0.5,5,2,0.25,Hint:"Начальное состояние x₀ (положение)"),
                    ]),
                new("mpc_horizon", "MPC: N=3 vs N=20", "LinearQuadraticMpc", "LinearQuadraticMpc", "MPC.md",
                    [
                        new("Q11","Q₁₁",1,100,10,1,Hint:"Штраф за отклонение x₁ в матрице весов Q"),
                        new("Q22","Q₂₂",0.1,20,1,0.1,Hint:"Штраф за отклонение x₂ в матрице весов Q"),
                        new("R","R",0.1,10,1,0.1,Hint:"Штраф за управляющее воздействие u"),
                        new("N1","N₁",1,15,3,1,Hint:"Короткий горизонт предсказания (сравнение)"),
                        new("N2","N₂",5,50,20,1,Hint:"Длинный горизонт предсказания (сравнение)"),
                        new("x01","x₀",0.5,5,2,0.25,Hint:"Начальное состояние x₀"),
                    ]),
                new("lqg_demo", "LQG = LQR + Калман", "LqgRegulator", "LqgRegulator", "LQR.md",
                    [
                        new("Q11","Q₁₁",1,100,10,1,Hint:"Штраф за отклонение состояния в матрице Q"),
                        new("R","R",0.1,10,1,0.1,Hint:"Штраф за управляющее воздействие u"),
                        new("sigmaV","σ<sub>v</sub>",0.1,3,0.7,0.1,Hint:"Среднеквадратичное отклонение шума процесса"),
                    ]),
            ]),
        new("observers", "Наблюдатели состояния",
            "KalmanFilter · LuenbergerObserver · ExtendedKalmanFilter",
            [
                new("obs_kalman", "Фильтр Калмана", "KalmanFilter.Predict/Update", "KalmanFilter", "KalmanFilter.md",
                    [
                        new("Qk","Q",0.001,1,0.05,0.005,Hint:"Дисперсия шума процесса (ковариация Q)"),
                        new("Rk","R",0.1,5,1,0.1,Hint:"Дисперсия шума измерений (ковариация R)"),
                        new("uConst","u",0.1,2,0.8,0.1,Hint:"Постоянный управляющий сигнал"),
                        new("sigmaM","σ<sub>изм</sub>",0.1,3,1,0.1,Hint:"Σ добавляемого шума измерений в симуляции"),
                    ]),
                new("obs_luenberger", "Наблюдатель Люенбергера", "LuenbergerObserver.Step", "LuenbergerObserver", "LuenbergerObserver.md",
                    [
                        new("L1","L₁",0.05,2,0.45,0.05,Hint:"Первый коэффициент матрицы усиления наблюдателя L"),
                        new("L2","L₂",0.05,3,1,0.05,Hint:"Второй коэффициент матрицы усиления наблюдателя L"),
                        new("x01","x₁₀",0.5,4,1.5,0.25,Hint:"Начальное истинное состояние x₁ (наблюдатель стартует с 0)"),
                    ]),
                new("obs_ekf", "EKF: нелинейный маятник", "ExtendedKalmanFilter", "ExtendedKalmanFilter", "KalmanFilter.md",
                    [
                        new("theta0Deg","θ₀",5,80,30,5,"°",Hint:"Начальный угол отклонения маятника"),
                        new("errDeg","δθ",0,30,11.5,1,"°",Hint:"Ошибка начального угла в оценке фильтра"),
                        new("sigmaM","σ",0.01,0.2,0.05,0.005,"рад",Hint:"Среднеквадратичное отклонение шума измерений угла"),
                        new("bDamp","b",0,1,0.3,0.05,Hint:"Коэффициент вязкого демпфирования маятника"),
                    ]),
            ]),
        new("linear", "Линейные системы",
            "Discretization · PolePlacement",
            [
                new("lin_poles", "Метод полюсов (Аккерман)", "PolePlacement.AckermannGain", "PolePlacement", "PolePlacement.md",
                    [
                        new("p1","p₁",0.3,0.97,0.75,0.01,Hint:"Первый желаемый полюс замкнутой системы (|p|<1 -> устойчиво)"),
                        new("p2","p₂",0.3,0.97,0.70,0.01,Hint:"Второй желаемый полюс замкнутой системы (|p|<1 -> устойчиво)"),
                        new("x01","x₀",0.5,4,1,0.25,Hint:"Начальное состояние x₀"),
                    ]),
                new("lin_zoh", "ZOH дискретизация", "Discretization.ZeroOrderHold", "Discretization", "StateSpace.md",
                    [
                        new("omega","ω",0.5,6,2,0.5,"рад/с",Hint:"Собственная частота непрерывного объекта"),
                        new("zeta","ζ",0.05,1,0.3,0.05,Hint:"Коэффициент демпфирования объекта"),
                        new("dt1","dt₁",0.01,0.15,0.05,0.01,"с",Hint:"Период дискретизации для первой кривой"),
                        new("dt2","dt₂",0.1,0.6,0.20,0.05,"с",Hint:"Период дискретизации для второй кривой"),
                    ]),
            ]),
        new("nonlinear", "Нелинейное управление",
            "SlidingModeController · MRAC",
            [
                new("nl_smc", "Скользящий режим (SMC)", "SlidingModeController", "SlidingModeController", "SlidingMode.md",
                    [
                        new("lambda","λ",0.5,8,2,0.5,Hint:"Наклон скользящей поверхности s = ẋ + λ·x"),
                        new("gain","K",1,30,8,1,Hint:"Усиление переключающего управления (чем больше — быстрее, но больше чаттеринг)"),
                        new("phi","Φ",0.02,1,0.25,0.02,Hint:"Ширина граничного слоя для сглаживания знаковой функции"),
                        new("x0","x₀",0.5,5,2,0.5,Hint:"Начальное положение объекта"),
                    ]),
                new("nl_mrac", "MRAC", "ModelReferenceAdaptiveController", "ModelReferenceAdaptiveController", "MRAC.md",
                    [
                        new("adaptGain","γ",0.5,30,8,0.5,Hint:"Скорость адаптации коэффициента (чем больше — быстрее, но может стать нестабильным)"),
                        new("refPole","λ<sub>m</sub>",0.5,12,4,0.5,"1/с",Hint:"Полюс эталонной модели (определяет желаемую скорость затухания)"),
                    ]),
            ]),
        new("identification", "Идентификация систем",
            "RecursiveLeastSquares",
            [
                new("id_rls", "RLS-идентификация", "RecursiveLeastSquares.Update", "RecursiveLeastSquares", "RLS.md",
                    [
                        new("th1","θ₁",0.1,0.95,0.70,0.05,Hint:"Истинный первый параметр системы (оценщик должен сойтись к этому значению)"),
                        new("th2","θ₂",0.05,0.95,0.50,0.05,Hint:"Истинный второй параметр системы"),
                        new("sigmaV","σ_v",0.01,0.3,0.05,0.01,Hint:"Среднеквадратичное отклонение шума наблюдения"),
                        new("forgetting","λ",0.9,1,1,0.01,Hint:"Коэффициент забывания (1 = без забывания, <1 = более чувствителен к изменениям)"),
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
    {
        var cfg = new ControlDemoRunner.Settings { Width = settings.Width, Height = settings.Height, DarkTheme = settings.DarkTheme };
        var (png, plotlyJson, cv) = ControlDemoRunner.RenderWithParams(algoKey, numericParams, cfg);
        return new DemoResult { PngDataUrl = png, PlotlyJson = plotlyJson, SourceChart = cv };
    }
}

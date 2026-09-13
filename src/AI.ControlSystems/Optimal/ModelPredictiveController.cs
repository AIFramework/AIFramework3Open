using System;
using System.Collections.Generic;
using System.Linq;
using AI.ControlSystems.Internal;
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Solvers.Optimization;

namespace AI.ControlSystems.Optimal;

/// <summary>Результат одного такта MPC</summary>
public sealed class MpcStep : IInterpretable
{
    internal MpcStep(
        Vector input, IReadOnlyList<Vector> inputs, IReadOnlyList<Vector> states, double cost,
        SolverStatus status, int iterations, double violation, bool fallback, int activeConstraints)
    {
        Input = input;
        PredictedInputs = inputs;
        PredictedStates = states;
        PredictedCost = cost;
        Status = status;
        Iterations = iterations;
        StateViolation = violation;
        UsedFallback = fallback;
        ActiveConstraints = activeConstraints;
    }

    /// <summary>Управление, которое нужно подать сейчас — первое из оптимального плана</summary>
    public Vector Input { get; }

    /// <summary>Оптимальный план управлений u₀ … u_{N−1}</summary>
    public IReadOnlyList<Vector> PredictedInputs { get; }

    /// <summary>Прогноз состояний x₁ … x_N при этом плане</summary>
    public IReadOnlyList<Vector> PredictedStates { get; }

    /// <summary>Стоимость плана на горизонте, включая терминальную</summary>
    public double PredictedCost { get; }

    /// <summary>Исход задачи квадратичного программирования</summary>
    public SolverStatus Status { get; }

    /// <summary>Итераций решателя</summary>
    public int Iterations { get; }

    /// <summary>Наибольшее нарушение ограничений на состояние в прогнозе</summary>
    public double StateViolation { get; }

    /// <summary>Задача не решена, и управление взято из прошлого плана с обрезкой по ограничениям</summary>
    public bool UsedFallback { get; }

    /// <summary>Число активных ограничений в оптимуме</summary>
    public int ActiveConstraints { get; }

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Такт MPC")
            .Summary(UsedFallback
                ? $"Задача оптимизации не решена ({Status}); подано управление из прошлого плана, обрезанное по ограничениям."
                : $"План на {PredictedInputs.Count} шагов найден, активных ограничений {ActiveConstraints}; "
                  + $"прогнозная стоимость {Fmt.Num(PredictedCost, 4)}.")
            .Metric("Исход", Status.ToString(), null, null, UsedFallback ? MetricQuality.Critical : MetricQuality.Good)
            .Metric("Горизонт", PredictedInputs.Count, null, "шагов прогноза", MetricQuality.Unknown, 0)
            .Metric("Активных ограничений", ActiveConstraints, null, "выполнены как равенства", MetricQuality.Unknown, 0)
            .Metric("Нарушение по состоянию", StateViolation, null, "мягкие ограничения; 0 — выполнены",
                StateViolation > 1e-6 ? MetricQuality.Warning : MetricQuality.Good, 6)
            .FindingIf(ActiveConstraints > 0,
                "Ограничения активны: план отличается от LQR, и первое управление упирается в предел или в скорость его изменения.")
            .WarningIf(StateViolation > 1e-6,
                "Мягкое ограничение на состояние нарушено в прогнозе: соблюсти его при допустимых управлениях нельзя, "
                + "и регулятор нарушает его как можно меньше.")
            .Warning("Без терминального множества устойчивость и выполнимость на следующих тактах не гарантированы: "
                + "горизонт должен быть достаточно длинным, а терминальный вес — решением Риккати.")
            .Build();
}

/// <summary>
/// Управление с прогнозирующей моделью (MPC) для линейного объекта x[k+1] = A x[k] + B u[k]
/// с ограничениями на управление, скорость его изменения и состояние.
/// </summary>
/// <remarks>
/// <para>
/// На каждом такте решается задача на горизонте N шагов:
/// <c>min Σ (x_k − r)ᵀQ(x_k − r) + (u_k − u_r)ᵀR(u_k − u_r) + Δu_kᵀSΔu_k + (x_N − r)ᵀQ_f(x_N − r)</c>
/// при <c>u_min ≤ u_k ≤ u_max</c>, <c>Δu_min ≤ u_k − u_{k−1} ≤ Δu_max</c>, <c>x_min ≤ x_k ≤ x_max</c>,
/// и подаётся только первое управление. Прогноз подставляется в стоимость (плотная форма), и
/// остаётся квадратичная задача от управлений, которую решает <see cref="QpSolver"/>.
/// </para>
/// <para>
/// Ради ограничений MPC и существует. Без них оптимальный закон — линейная обратная связь,
/// и с терминальным весом, равным решению Риккати, MPC на любом горизонте в точности совпадает
/// с LQR (<see cref="LinearQuadraticMpc"/> даёт это усиление напрямую). С ограничениями закон
/// становится кусочно-линейным: регулятор заранее притормаживает, чтобы не упереться в предел.
/// </para>
/// <para>
/// Ограничения на состояние по умолчанию мягкие: каждое ослабляется неотрицательной переменной
/// с большим штрафом. Иначе возмущение, выводящее прогноз за предел, делает задачу невыполнимой,
/// и регулятору нечего подать. Если задача всё же не решена, подаётся следующий шаг прошлого
/// плана, обрезанный по пределам управления, и это отмечено в результате.
/// </para>
/// <para>
/// Решение прошлого такта, сдвинутое на шаг, служит стартом решателя: план меняется мало, и
/// метод активного множества возвращается к оптимуму за несколько итераций.
/// </para>
/// </remarks>
public sealed class ModelPredictiveController
{
    private readonly Matrix _a;
    private readonly Matrix _b;
    private readonly Matrix _q;
    private readonly Matrix _r;
    private readonly Matrix _qf;
    private readonly int _n;
    private readonly int _m;
    private readonly int _horizon;
    private readonly Matrix _phi;
    private readonly Matrix _gamma;
    private Matrix _gammaTransposeWeighted;
    private Matrix _baseHessian;
    private double[] _warm;

    /// <summary>Создаёт регулятор</summary>
    /// <param name="a">A (n×n)</param>
    /// <param name="b">B (n×m)</param>
    /// <param name="q">Вес состояния Q ≥ 0</param>
    /// <param name="r">Вес управления R &gt; 0</param>
    /// <param name="horizon">Горизонт N ≥ 1</param>
    /// <param name="terminalWeight">Терминальный вес Q_f; по умолчанию — решение уравнения Риккати</param>
    public ModelPredictiveController(Matrix a, Matrix b, Matrix q, Matrix r, int horizon, Matrix terminalWeight = null)
    {
        if (a == null || b == null || q == null || r == null)
            throw new ArgumentNullException();
        if (horizon < 1)
            throw new ArgumentOutOfRangeException(nameof(horizon), "Горизонт должен быть не меньше одного шага.");

        _n = a.Height;
        _m = b.Width;

        if (!a.IsSquared || b.Height != _n || q.Height != _n || q.Width != _n || r.Height != _m || r.Width != _m)
            throw new ArgumentException("Несогласованные размеры A, B, Q, R.");

        _a = a;
        _b = b;
        _q = q;
        _r = r;
        _horizon = horizon;
        _qf = terminalWeight ?? RiccatiEquation.SolveDiscrete(a, b, q, r);

        if (_qf.Height != _n || _qf.Width != _n)
            throw new ArgumentException("Терминальный вес должен быть n×n.", nameof(terminalWeight));

        // Прогноз x_k = A^k x₀ + Σ_{j<k} A^{k−1−j} B u_j, k = 1..N
        var powers = new Matrix[horizon + 1];
        powers[0] = ControlLinAlg.Eye(_n);
        for (int k = 1; k <= horizon; k++)
            powers[k] = powers[k - 1] * a;

        _phi = new Matrix(horizon * _n, _n);
        _gamma = new Matrix(horizon * _n, horizon * _m);

        for (int k = 1; k <= horizon; k++)
        {
            ControlLinAlg.SetBlock(_phi, (k - 1) * _n, 0, powers[k]);

            for (int j = 0; j < k; j++)
                ControlLinAlg.SetBlock(_gamma, (k - 1) * _n, j * _m, powers[k - 1 - j] * b);
        }

        PreviousInput = new Vector(_m);
    }

    /// <summary>Горизонт прогноза</summary>
    public int Horizon => _horizon;

    /// <summary>Порядок объекта</summary>
    public int StateDimension => _n;

    /// <summary>Число управлений</summary>
    public int InputDimension => _m;

    /// <summary>Терминальный вес</summary>
    public Matrix TerminalWeight => _qf.Copy();

    /// <summary>Нижние пределы управления; null — нет</summary>
    public Vector InputLower { get; init; }

    /// <summary>Верхние пределы управления; null — нет</summary>
    public Vector InputUpper { get; init; }

    /// <summary>Наименьшее приращение управления за такт; null — нет</summary>
    public Vector RateLower { get; init; }

    /// <summary>Наибольшее приращение управления за такт; null — нет</summary>
    public Vector RateUpper { get; init; }

    /// <summary>Нижние пределы состояния; null или бесконечность — нет</summary>
    public Vector StateLower { get; init; }

    /// <summary>Верхние пределы состояния; null или бесконечность — нет</summary>
    public Vector StateUpper { get; init; }

    /// <summary>Вес приращения управления S (m×m); null — не штрафуется</summary>
    public Matrix RateWeight { get; init; }

    /// <summary>Мягкие ли ограничения на состояние</summary>
    public bool SoftStateConstraints { get; init; } = true;

    /// <summary>Штраф за нарушение мягкого ограничения</summary>
    public double ViolationWeight { get; init; } = 1e5;

    /// <summary>Уставка по состоянию; null — начало координат</summary>
    public Vector StateReference { get; set; }

    /// <summary>Установившееся управление при уставке; null — нуль</summary>
    public Vector InputReference { get; set; }

    /// <summary>Управление, поданное на прошлом такте, — от него отсчитывается ограничение скорости</summary>
    public Vector PreviousInput { get; private set; }

    /// <summary>Сбрасывает память регулятора</summary>
    /// <param name="previousInput">Управление, действующее сейчас</param>
    public void Reset(Vector previousInput = null)
    {
        PreviousInput = previousInput == null ? new Vector(_m) : Copy(previousInput);
        _warm = null;
    }

    /// <summary>Управление для текущего состояния</summary>
    /// <param name="state">Измеренное или оценённое состояние</param>
    public Vector Compute(Vector state) => Step(state).Input;

    /// <summary>Один такт: план на горизонте, первое управление и прогноз</summary>
    /// <param name="state">Измеренное или оценённое состояние</param>
    public MpcStep Step(Vector state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.Count != _n)
            throw new ArgumentException($"Состояние должно иметь размерность {_n}.", nameof(state));

        EnsureStructure();

        int inputs = _horizon * _m;
        int[] constrained = Enumerable.Range(0, _n)
            .Where(i => IsFinite(StateLower, i, false) || IsFinite(StateUpper, i, true))
            .ToArray();
        int slacks = SoftStateConstraints ? constrained.Length : 0;
        int variables = inputs + slacks;

        Vector reference = StateReference ?? new Vector(_n);
        Vector inputReference = InputReference ?? new Vector(_m);
        Vector free = ControlLinAlg.MatVec(_phi, state);
        var offset = new Vector(_horizon * _n);

        for (int k = 0; k < _horizon; k++)
            for (int i = 0; i < _n; i++)
                offset[(k * _n) + i] = free[(k * _n) + i] - reference[i];

        // ½UᵀHU + gᵀU, H = 2(ΓᵀQ̄Γ + R̄ + DᵀS̄D), g = 2(ΓᵀQ̄f − R̄U_r − DᵀS̄d₀)
        var hessian = new Matrix(variables, variables);
        var linear = new Vector(variables);
        Vector projected = ControlLinAlg.MatVec(_gammaTransposeWeighted, offset);
        Vector weightedReference = ControlLinAlg.MatVec(_r, inputReference);

        for (int i = 0; i < inputs; i++)
        {
            for (int j = 0; j < inputs; j++)
                hessian[i, j] = 2 * _baseHessian[i, j];

            linear[i] = 2 * (projected[i] - weightedReference[i % _m]);
        }

        if (RateWeight != null)
        {
            Vector weightedPrevious = ControlLinAlg.MatVec(RateWeight, PreviousInput);
            for (int i = 0; i < _m; i++)
                linear[i] -= 2 * weightedPrevious[i];
        }

        for (int s = 0; s < slacks; s++)
        {
            hessian[inputs + s, inputs + s] = 2 * ViolationWeight;
            linear[inputs + s] = ViolationWeight;
        }

        var program = new QuadraticProgram(hessian, linear, "MPC");
        AddInputConstraints(program);
        AddStateConstraints(program, free, constrained, inputs, slacks);

        Vector start = _warm != null && _warm.Length == variables ? new Vector((double[])_warm.Clone()) : null;
        QpSolution solution = QpSolver.Solve(program, null, start);

        double[] plan;
        bool fallback = !solution.IsOptimal;

        if (solution.IsOptimal)
        {
            plan = solution.Values.ToArray();
        }
        else
        {
            plan = _warm != null && _warm.Length == variables ? (double[])_warm.Clone() : new double[variables];

            for (int k = 0; k < _horizon; k++)
                for (int i = 0; i < _m; i++)
                    plan[(k * _m) + i] = Clip(plan[(k * _m) + i], i);
        }

        var inputsPlan = new List<Vector>(_horizon);
        var control = new Vector(inputs);

        for (int k = 0; k < _horizon; k++)
        {
            var u = new Vector(_m);
            for (int i = 0; i < _m; i++)
            {
                u[i] = plan[(k * _m) + i];
                control[(k * _m) + i] = u[i];
            }
            inputsPlan.Add(u);
        }

        Vector predicted = free + ControlLinAlg.MatVec(_gamma, control);
        var statesPlan = new List<Vector>(_horizon);
        double violation = 0;

        for (int k = 0; k < _horizon; k++)
        {
            var x = new Vector(_n);
            for (int i = 0; i < _n; i++)
            {
                x[i] = predicted[(k * _n) + i];

                if (IsFinite(StateUpper, i, true))
                    violation = Math.Max(violation, x[i] - StateUpper[i]);
                if (IsFinite(StateLower, i, false))
                    violation = Math.Max(violation, StateLower[i] - x[i]);
            }
            statesPlan.Add(x);
        }

        double cost = PlanCost(statesPlan, inputsPlan, reference, inputReference);

        // Сдвинутый план — старт следующего такта
        _warm = new double[variables];
        for (int k = 0; k < _horizon; k++)
            for (int i = 0; i < _m; i++)
                _warm[(k * _m) + i] = plan[(Math.Min(k + 1, _horizon - 1) * _m) + i];
        for (int s = 0; s < slacks; s++)
            _warm[inputs + s] = plan[inputs + s];

        Vector applied = Copy(inputsPlan[0]);
        PreviousInput = Copy(applied);

        return new MpcStep(applied, inputsPlan, statesPlan, cost, solution.Status, solution.Iterations,
            Math.Max(0, violation), fallback, solution.ActiveConstraints.Count);
    }

    private void AddInputConstraints(QuadraticProgram program)
    {
        for (int k = 0; k < _horizon; k++)
        {
            for (int i = 0; i < _m; i++)
            {
                double lower = InputLower != null ? InputLower[i] : double.NegativeInfinity;
                double upper = InputUpper != null ? InputUpper[i] : double.PositiveInfinity;

                if (!double.IsInfinity(lower) || !double.IsInfinity(upper))
                    program.AddBounds((k * _m) + i, lower, upper);
            }
        }

        if (RateLower == null && RateUpper == null)
            return;

        int variables = program.VariableCount;

        for (int k = 0; k < _horizon; k++)
        {
            for (int i = 0; i < _m; i++)
            {
                var row = new Vector(variables);
                row[(k * _m) + i] = 1;
                double shift = 0;

                if (k == 0)
                    shift = PreviousInput[i];
                else
                    row[((k - 1) * _m) + i] = -1;

                if (RateUpper != null && !double.IsPositiveInfinity(RateUpper[i]))
                    _ = program.AddInequality(row, RateUpper[i] + shift, $"Δu{i}[{k}] ≤ {RateUpper[i]}");

                if (RateLower != null && !double.IsNegativeInfinity(RateLower[i]))
                {
                    var negative = new Vector(variables);
                    for (int j = 0; j < variables; j++)
                        negative[j] = -row[j];
                    _ = program.AddInequality(negative, -(RateLower[i] + shift), $"Δu{i}[{k}] ≥ {RateLower[i]}");
                }
            }
        }
    }

    private void AddStateConstraints(QuadraticProgram program, Vector free, int[] constrained, int inputs, int slacks)
    {
        int variables = program.VariableCount;

        for (int c = 0; c < constrained.Length; c++)
        {
            int i = constrained[c];

            for (int k = 0; k < _horizon; k++)
            {
                int row = (k * _n) + i;

                if (IsFinite(StateUpper, i, true))
                {
                    var upper = new Vector(variables);
                    for (int j = 0; j < inputs; j++)
                        upper[j] = _gamma[row, j];
                    if (slacks > 0)
                        upper[inputs + c] = -1;
                    _ = program.AddInequality(upper, StateUpper[i] - free[row], $"x{i}[{k + 1}] ≤ {StateUpper[i]}");
                }

                if (IsFinite(StateLower, i, false))
                {
                    var lower = new Vector(variables);
                    for (int j = 0; j < inputs; j++)
                        lower[j] = -_gamma[row, j];
                    if (slacks > 0)
                        lower[inputs + c] = -1;
                    _ = program.AddInequality(lower, free[row] - StateLower[i], $"x{i}[{k + 1}] ≥ {StateLower[i]}");
                }
            }
        }

        for (int s = 0; s < slacks; s++)
            program.AddBounds(inputs + s, 0, double.PositiveInfinity);
    }

    // Постоянная часть задачи зависит только от весов и горизонта и считается один раз
    private void EnsureStructure()
    {
        if (_baseHessian != null)
            return;

        int states = _horizon * _n;
        int inputs = _horizon * _m;
        var stateWeight = new Matrix(states, states);

        for (int k = 0; k < _horizon; k++)
            ControlLinAlg.SetBlock(stateWeight, k * _n, k * _n, k == _horizon - 1 ? _qf : _q);

        _gammaTransposeWeighted = _gamma.Transpose() * stateWeight;
        Matrix hessian = _gammaTransposeWeighted * _gamma;

        for (int k = 0; k < _horizon; k++)
        {
            for (int i = 0; i < _m; i++)
                for (int j = 0; j < _m; j++)
                    hessian[(k * _m) + i, (k * _m) + j] += _r[i, j];
        }

        if (RateWeight != null)
        {
            if (RateWeight.Height != _m || RateWeight.Width != _m)
                throw new ArgumentException("Вес приращения управления должен быть m×m.");

            // DᵀS̄D: на диагонали 2S (последний блок — S), рядом с диагональю −S
            for (int k = 0; k < _horizon; k++)
            {
                double diagonal = k == _horizon - 1 ? 1 : 2;

                for (int i = 0; i < _m; i++)
                {
                    for (int j = 0; j < _m; j++)
                    {
                        hessian[(k * _m) + i, (k * _m) + j] += diagonal * RateWeight[i, j];

                        if (k + 1 < _horizon)
                        {
                            hessian[(k * _m) + i, ((k + 1) * _m) + j] -= RateWeight[i, j];
                            hessian[((k + 1) * _m) + i, (k * _m) + j] -= RateWeight[i, j];
                        }
                    }
                }
            }
        }

        _baseHessian = ControlLinAlg.Symmetrize(hessian);

        if (inputs != _baseHessian.Height)
            throw new InvalidOperationException("Внутренняя ошибка размеров MPC.");
    }

    private double PlanCost(List<Vector> states, List<Vector> inputs, Vector reference, Vector inputReference)
    {
        double cost = 0;
        Vector previous = PreviousInput;

        for (int k = 0; k < _horizon; k++)
        {
            Vector dx = states[k] - reference;
            Vector du = inputs[k] - inputReference;
            cost += Quadratic(k == _horizon - 1 ? _qf : _q, dx) + Quadratic(_r, du);

            if (RateWeight != null)
                cost += Quadratic(RateWeight, inputs[k] - previous);

            previous = inputs[k];
        }

        return cost;
    }

    private double Clip(double value, int channel)
    {
        if (InputLower != null && value < InputLower[channel])
            value = InputLower[channel];
        if (InputUpper != null && value > InputUpper[channel])
            value = InputUpper[channel];
        return value;
    }

    private static bool IsFinite(Vector bounds, int index, bool upper)
        => bounds != null && (upper ? !double.IsPositiveInfinity(bounds[index]) : !double.IsNegativeInfinity(bounds[index]));

    private static double Quadratic(Matrix weight, Vector v)
    {
        Vector wv = ControlLinAlg.MatVec(weight, v);
        double s = 0;
        for (int i = 0; i < v.Count; i++)
            s += v[i] * wv[i];
        return s;
    }

    private static Vector Copy(Vector v)
    {
        var r = new Vector(v.Count);
        for (int i = 0; i < v.Count; i++)
            r[i] = v[i];
        return r;
    }
}

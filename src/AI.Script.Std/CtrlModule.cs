using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Linear;
using AI.ControlSystems.Observers;
using AI.ControlSystems.Optimal;
using AI.ControlSystems.Pid;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Модель объекта, опознанная по записи входа и выхода (ARX).
/// </summary>
/// <remarks>
/// Разностное уравнение <c>y[k] = -a₁y[k-1] - … + b₁u[k-1] + …</c>. Хранит коэффициенты и
/// умеет прогонять по ним вход: этого хватает и для проверки качества опознания, и для
/// замкнутой симуляции.
/// </remarks>
public sealed class ArxModel
{
    /// <summary>Коэффициенты при прошлых выходах.</summary>
    public Vector A { get; }

    /// <summary>Коэффициенты при прошлых входах.</summary>
    public Vector B { get; }

    /// <summary>Порядок модели.</summary>
    public int Order => A.Count;

    /// <summary>Создаёт модель.</summary>
    public ArxModel(Vector a, Vector b)
    {
        A = a;
        B = b;
    }

    /// <summary>Статический коэффициент передачи.</summary>
    public double Gain
    {
        get
        {
            double numerator = 0, denominator = 1;

            for (int i = 0; i < B.Count; i++) numerator += B[i];
            for (int i = 0; i < A.Count; i++) denominator += A[i];

            return Math.Abs(denominator) < 1e-12 ? double.NaN : numerator / denominator;
        }
    }

    /// <summary>Прогоняет вход через модель с нулевыми начальными условиями.</summary>
    public Vector Simulate(Vector input)
    {
        var output = new Vector(input.Count);

        for (int k = 0; k < input.Count; k++)
        {
            double value = 0;

            for (int i = 0; i < A.Count; i++)
            {
                int index = k - i - 1;
                if (index >= 0) value -= A[i] * output[index];
            }

            for (int i = 0; i < B.Count; i++)
            {
                int index = k - i - 1;
                if (index >= 0) value += B[i] * input[index];
            }

            output[k] = value;
        }

        return output;
    }

    /// <summary>Один шаг: следующий выход по предыстории.</summary>
    public double Next(IReadOnlyList<double> pastOutputs, IReadOnlyList<double> pastInputs)
    {
        double value = 0;

        for (int i = 0; i < A.Count && i < pastOutputs.Count; i++) value -= A[i] * pastOutputs[i];
        for (int i = 0; i < B.Count && i < pastInputs.Count; i++) value += B[i] * pastInputs[i];

        return value;
    }

    /// <inheritdoc/>
    public override string ToString() => $"ARX порядка {Order}, K = {ScriptFormatter.Number(Gain)}";
}

/// <summary>
/// Пространство <c>ctrl</c>: идентификация, регуляторы, наблюдатели, замкнутая симуляция.
/// </summary>
[ScriptModule("ctrl", "Системы управления: идентификация, ПИД, LQR, фильтр Калмана, симуляция", Version = "0.1")]
public static class CtrlModule
{
    /// <summary>Тип-тег дескриптора модели объекта.</summary>
    public const string PlantHandle = "ctrl.plant";

    /// <summary>Тип-тег дескриптора регулятора.</summary>
    public const string PidHandle = "ctrl.pid";

    /// <summary>Тип-тег дескриптора фильтра Калмана.</summary>
    public const string KalmanHandle = "ctrl.kalman";

    /// <summary>Тип-тег дескриптора линейной модели в пространстве состояний.</summary>
    public const string LtiHandle = "ctrl.lti";

    // --- идентификация ---

    /// <summary>
    /// Опознаёт объект по записи входа и выхода методом наименьших квадратов.
    /// </summary>
    /// <remarks>
    /// Задача сводится к переопределённой линейной системе и решается через нормальные
    /// уравнения с псевдообратной матрицей: она справляется и с почти вырожденным случаем,
    /// который для реальных логов скорее правило, чем исключение.
    /// </remarks>
    [ScriptFn("identify", "Опознаёт ARX-модель объекта по логу входа и выхода", Returns = PlantHandle,
        Example = "let plant = ctrl.identify(u: log[\"u\"], y: log[\"y\"], order: 2)")]
    public static ScriptHandle Identify(
        [ScriptParam("вход объекта")] Vector u,
        [ScriptParam("выход объекта")] Vector y,
        [ScriptParam("порядок модели")] int order = 2)
    {
        if (u.Count != y.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"ctrl.identify: {u.Count} отсчётов входа и {y.Count} выхода");
        }

        if (order < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "ctrl.identify: порядок должен быть не меньше 1");

        int rows = y.Count - order;

        if (rows < 2 * order)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"ctrl.identify: для порядка {order} нужно хотя бы {(3 * order) + 1} отсчётов, а их {y.Count}",
                "уменьшите порядок либо возьмите более длинную запись");
        }

        var regressors = new Matrix(rows, 2 * order);
        var target = new Vector(rows);

        for (int k = 0; k < rows; k++)
        {
            int t = k + order;

            for (int i = 0; i < order; i++)
            {
                regressors[k, i] = -y[t - i - 1];
                regressors[k, order + i] = u[t - i - 1];
            }

            target[k] = y[t];
        }

        Vector theta = LeastSquares(regressors, target, "ctrl.identify");

        var a = new Vector(order);
        var b = new Vector(order);

        for (int i = 0; i < order; i++)
        {
            a[i] = theta[i];
            b[i] = theta[order + i];
        }

        var model = new ArxModel(a, b);

        return new ScriptHandle(PlantHandle, model, model.ToString());
    }

    [ScriptFn("arx", "Собирает ARX-модель из готовых коэффициентов", Returns = PlantHandle,
        Example = "ctrl.arx(a: <-0.8>, b: <0.2>)")]
    public static ScriptHandle Arx(
        [ScriptParam("коэффициенты при прошлых выходах")] Vector a,
        [ScriptParam("коэффициенты при прошлых входах")] Vector b)
    {
        var model = new ArxModel(a, b);

        return new ScriptHandle(PlantHandle, model, model.ToString());
    }

    [ScriptFn("describe", "Характеристики модели: порядок, коэффициент передачи, коэффициенты",
        Example = "plant.describe()")]
    [ScriptMethod(PlantHandle)]
    public static ScriptRecord Describe([ScriptParam("модель объекта")] ScriptHandle plant)
    {
        var model = (ArxModel)plant.Target;

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("order", ScriptValue.Num(model.Order)),
            new KeyValuePair<string, ScriptValue>("gain", ScriptValue.Num(model.Gain)),
            new KeyValuePair<string, ScriptValue>("a", ScriptValue.Vec(model.A)),
            new KeyValuePair<string, ScriptValue>("b", ScriptValue.Vec(model.B)),
        ]);
    }

    [ScriptFn("response", "Отклик модели на заданный вход", Example = "plant.response(u)")]
    [ScriptMethod(PlantHandle)]
    public static Vector Response(
        [ScriptParam("модель объекта")] ScriptHandle plant,
        [ScriptParam("вход")] Vector u)
        => ((ArxModel)plant.Target).Simulate(u);

    [ScriptFn("step_response", "Переходная характеристика: отклик на единичный скачок",
        Example = "plant.step_response(200)")]
    [ScriptMethod(PlantHandle)]
    public static Vector StepResponse(
        [ScriptParam("модель объекта")] ScriptHandle plant,
        [ScriptParam("число шагов")] int steps)
    {
        if (steps < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "ctrl.step_response: нужен хотя бы один шаг");

        var input = new Vector(steps);

        for (int i = 0; i < steps; i++) input[i] = 1;

        return ((ArxModel)plant.Target).Simulate(input);
    }

    // --- регуляторы ---

    [ScriptFn("pid", "ПИД-регулятор", Returns = PidHandle,
        Example = "let c = ctrl.pid(kp: 1.2, ki: 0.4, kd: 0.05)")]
    public static ScriptHandle Pid(
        [ScriptParam("пропорциональный коэффициент")] double kp,
        [ScriptParam("интегральный коэффициент")] double ki = 0,
        [ScriptParam("дифференциальный коэффициент")] double kd = 0,
        [ScriptParam("нижний предел управления; nan — без предела")] double low = double.NaN,
        [ScriptParam("верхний предел управления; nan — без предела")] double high = double.NaN)
    {
        var controller = new PidController(kp, ki, kd);

        if (!double.IsNaN(low)) controller.OutputMin = low;
        if (!double.IsNaN(high)) controller.OutputMax = high;

        return new ScriptHandle(PidHandle, controller,
            $"ПИД: Kp={ScriptFormatter.Number(kp)}, Ki={ScriptFormatter.Number(ki)}, Kd={ScriptFormatter.Number(kd)}");
    }

    /// <summary>
    /// Настройка ПИ по методу внутренней модели.
    /// </summary>
    /// <remarks>
    /// Объект приводится к первому порядку: коэффициент передачи берётся статический, а
    /// постоянная времени — из доминирующего полюса разностного уравнения. Это приближение, и
    /// оно названо приближением: для объекта высокого порядка настройку нужно проверять
    /// симуляцией, а не принимать на веру.
    /// </remarks>
    [ScriptFn("pid_tune", "Настраивает ПИ по методу внутренней модели (IMC)", Returns = PidHandle,
        Example = "let c = ctrl.pid_tune(plant, softness: 5)")]
    public static ScriptHandle PidTune(
        [ScriptParam("модель объекта")] ScriptHandle plant,
        [ScriptParam("мягкость замкнутого контура: больше — спокойнее и медленнее")] double softness = 5,
        [ScriptParam("шаг дискретизации")] double dt = 1)
    {
        var model = (ArxModel)plant.Target;
        double gain = model.Gain;

        if (double.IsNaN(gain) || Math.Abs(gain) < 1e-9)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "ctrl.pid_tune: коэффициент передачи объекта близок к нулю",
                "по такой модели настроить регулятор нельзя: проверьте идентификацию");
        }

        if (softness <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, "ctrl.pid_tune: мягкость должна быть больше нуля");

        double pole = DominantPole(model.A);
        double tau = pole is > 0 and < 1 ? -dt / Math.Log(pole) : dt;

        ImcPidTuning.FirstOrderPi(gain, Math.Max(tau, dt), softness * dt, out double kp, out double ki);

        var controller = new PidController(kp, ki, 0);

        return new ScriptHandle(PidHandle, controller,
            $"ПИ по IMC: Kp={ScriptFormatter.Number(kp)}, Ki={ScriptFormatter.Number(ki)}");
    }

    [ScriptFn("gains", "Коэффициенты регулятора", Example = "c.gains().kp")]
    [ScriptMethod(PidHandle)]
    public static ScriptRecord Gains([ScriptParam("регулятор")] ScriptHandle controller)
    {
        var pid = (PidController)controller.Target;

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("kp", ScriptValue.Num(pid.Kp)),
            new KeyValuePair<string, ScriptValue>("ki", ScriptValue.Num(pid.Ki)),
            new KeyValuePair<string, ScriptValue>("kd", ScriptValue.Num(pid.Kd)),
        ]);
    }

    [ScriptFn("compute", "Один шаг регулятора", Example = "c.compute(setpoint: 1, measured: y, dt: 0.01)")]
    [ScriptMethod(PidHandle)]
    public static double Compute(
        [ScriptParam("регулятор")] ScriptHandle controller,
        [ScriptParam("задание")] double setpoint,
        [ScriptParam("измеренное значение")] double measured,
        [ScriptParam("шаг времени")] double dt)
        => ((PidController)controller.Target).Compute(setpoint, measured, dt);

    [ScriptFn("reset", "Сбрасывает внутреннее состояние регулятора", Example = "c.reset()")]
    [ScriptMethod(PidHandle)]
    public static ScriptValue ResetPid([ScriptParam("регулятор")] ScriptHandle controller)
    {
        ((PidController)controller.Target).Reset();

        return ScriptValue.None;
    }

    /// <summary>
    /// Замкнутая симуляция: объект под управлением регулятора отрабатывает задание.
    /// </summary>
    /// <remarks>
    /// Регулятор сбрасывается перед прогоном: иначе повторная симуляция дала бы другой
    /// результат из-за накопленного интеграла, а сравнивать настройки стало бы нельзя.
    /// </remarks>
    [ScriptFn("simulate", "Замкнутая симуляция объекта с регулятором",
        Example = "let sim = ctrl.simulate(plant, controller: c, setpoint: 1, steps: 200)")]
    public static ScriptRecord Simulate(
        IScriptContext context,
        [ScriptParam("модель объекта")] ScriptHandle plant,
        [ScriptParam("регулятор")] ScriptHandle controller,
        [ScriptParam("задание")] double setpoint = 1,
        [ScriptParam("число шагов")] int steps = 200,
        [ScriptParam("шаг времени")] double dt = 1)
    {
        var model = (ArxModel)plant.Target;
        var pid = (PidController)controller.Target;

        if (steps < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "ctrl.simulate: нужен хотя бы один шаг");

        context.CountAllocation(steps * 3L);
        pid.Reset();

        var time = new Vector(steps);
        var output = new Vector(steps);
        var control = new Vector(steps);

        var pastOutputs = new List<double>(model.Order);
        var pastInputs = new List<double>(model.Order);

        for (int k = 0; k < steps; k++)
        {
            context.Cancellation.ThrowIfCancellationRequested();

            double measured = k == 0 ? 0 : output[k - 1];
            double u = pid.Compute(setpoint, measured, dt);

            // Списки хранят ТОЛЬКО прошлое: выход на шаге k зависит от u[k-1], поэтому
            // свежее управление добавляется после вычисления выхода, а не до него.
            double y = model.Next(pastOutputs, pastInputs);

            pastInputs.Insert(0, u);
            if (pastInputs.Count > model.B.Count) pastInputs.RemoveAt(pastInputs.Count - 1);

            pastOutputs.Insert(0, y);
            if (pastOutputs.Count > model.A.Count) pastOutputs.RemoveAt(pastOutputs.Count - 1);

            time[k] = k * dt;
            output[k] = y;
            control[k] = u;
        }

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("t", ScriptValue.Vec(time)),
            new KeyValuePair<string, ScriptValue>("y", ScriptValue.Vec(output)),
            new KeyValuePair<string, ScriptValue>("u", ScriptValue.Vec(control)),
        ]);
    }

    // --- пространство состояний ---

    [ScriptFn("lti", "Дискретная линейная модель в пространстве состояний", Returns = LtiHandle,
        Example = "ctrl.lti(a, b, c)")]
    public static ScriptHandle Lti(
        [ScriptParam("матрица состояния A")] Matrix a,
        [ScriptParam("матрица входа B")] Matrix b,
        [ScriptParam("матрица выхода C")] Matrix c)
    {
        try
        {
            var model = new DiscreteLtiModel(a, b, c);

            return new ScriptHandle(LtiHandle, model, $"LTI: состояний {model.StateDimension}, входов {model.InputDimension}");
        }
        catch (ArgumentException exception)
        {
            throw new ScriptError(DiagnosticCodes.SizeMismatch, $"ctrl.lti: {exception.Message}");
        }
    }

    [ScriptFn("step", "Один шаг модели в пространстве состояний", Example = "m.step(<1>)")]
    [ScriptMethod(LtiHandle)]
    public static Vector Step(
        [ScriptParam("модель")] ScriptHandle model,
        [ScriptParam("вход")] Vector u)
        => ((DiscreteLtiModel)model.Target).Step(u);

    [ScriptFn("reset", "Обнуляет состояние модели", Example = "m.reset()")]
    [ScriptMethod(LtiHandle)]
    public static ScriptValue ResetLti([ScriptParam("модель")] ScriptHandle model)
    {
        ((DiscreteLtiModel)model.Target).Reset();

        return ScriptValue.None;
    }

    [ScriptFn("lqr", "Оптимальный регулятор: матрица обратной связи K", Example = "ctrl.lqr(a, b, q, r)")]
    public static Matrix Lqr(
        [ScriptParam("матрица состояния A")] Matrix a,
        [ScriptParam("матрица входа B")] Matrix b,
        [ScriptParam("вес состояния Q")] Matrix q,
        [ScriptParam("вес управления R")] Matrix r)
    {
        try
        {
            return DiscreteLqr.Solve(a, b, q, r);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new ScriptError(DiagnosticCodes.FunctionFailed, $"ctrl.lqr: {exception.Message}");
        }
    }

    [ScriptFn("kalman", "Фильтр Калмана", Returns = KalmanHandle,
        Example = "let f = ctrl.kalman(a, b, c, q: q, r: r)")]
    public static ScriptHandle Kalman(
        [ScriptParam("матрица состояния A")] Matrix a,
        [ScriptParam("матрица входа B")] Matrix b,
        [ScriptParam("матрица выхода C")] Matrix c,
        [ScriptParam("ковариация шума процесса Q")] Matrix q,
        [ScriptParam("ковариация шума измерения R")] Matrix r)
    {
        try
        {
            var filter = new KalmanFilter(a, b, c, new Matrix(c.Height, b.Width), q, r);

            return new ScriptHandle(KalmanHandle, filter, $"фильтр Калмана: состояний {filter.StateDimension}");
        }
        catch (ArgumentException exception)
        {
            throw new ScriptError(DiagnosticCodes.SizeMismatch, $"ctrl.kalman: {exception.Message}");
        }
    }

    [ScriptFn("update", "Шаг фильтра: прогноз и коррекция по измерению", Example = "f.update(y: <1.2>, u: <0>)")]
    [ScriptMethod(KalmanHandle)]
    public static Vector Update(
        [ScriptParam("фильтр")] ScriptHandle filter,
        [ScriptParam("измерение")] Vector y,
        [ScriptParam("управление")] Vector u)
    {
        var kalman = (KalmanFilter)filter.Target;

        kalman.Predict(u);
        kalman.Update(y, u);

        return kalman.State;
    }

    [ScriptFn("state", "Текущая оценка состояния", Example = "f.state()")]
    [ScriptMethod(KalmanHandle)]
    public static Vector State([ScriptParam("фильтр")] ScriptHandle filter) =>
        ((KalmanFilter)filter.Target).State;

    /// <summary>
    /// Доминирующий полюс по коэффициентам разностного уравнения.
    /// </summary>
    /// <remarks>
    /// Для первого порядка это ровно <c>-a₁</c>; для более высоких берётся та же оценка как
    /// приближение — постоянная времени всё равно уходит в настройку регулятора, которую
    /// полагается проверить симуляцией.
    /// </remarks>
    private static double DominantPole(Vector a) => a.Count == 0 ? 0 : -a[0];

    /// <summary>
    /// Решение переопределённой системы методом наименьших квадратов.
    /// </summary>
    /// <remarks>
    /// Через нормальные уравнения <c>XᵀX θ = Xᵀy</c>: матрица <c>XᵀX</c> мала (порядок модели
    /// умножить на два) и решается напрямую, а псевдообратная от высокой матрицы регрессоров
    /// стоила бы разложения там, где хватает системы из четырёх уравнений.
    /// <para>
    /// К диагонали добавляется малая величина: логи реальных объектов часто почти вырождены,
    /// и без этого система оказывается неразрешимой на данных, по которым модель всё же
    /// восстанавливается.
    /// </para>
    /// </remarks>
    private static Vector LeastSquares(Matrix design, Vector target, string what)
    {
        int columns = design.Width;
        int rows = design.Height;

        var normal = new Matrix(columns, columns);
        var right = new Vector(columns);

        for (int i = 0; i < columns; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                double sum = 0;

                for (int k = 0; k < rows; k++) sum += design[k, i] * design[k, j];

                normal[i, j] = sum;
            }

            normal[i, i] += 1e-9;

            double product = 0;

            for (int k = 0; k < rows; k++) product += design[k, i] * target[k];

            right[i] = product;
        }

        try
        {
            return LU.Solve(normal, right);
        }
        catch (Exception exception) when (exception is not ScriptError)
        {
            throw new ScriptError(
                DiagnosticCodes.FunctionFailed,
                $"{what}: система нормальных уравнений неразрешима — {exception.Message}",
                "вход слишком беден: подайте на объект более разнообразный сигнал");
        }
    }
}

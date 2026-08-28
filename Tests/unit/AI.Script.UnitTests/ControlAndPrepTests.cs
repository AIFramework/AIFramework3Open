using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Идентификация, регуляторы, замкнутая симуляция и подготовка признаков.</summary>
public sealed class ControlAndPrepTests
{
    /// <summary>Лог объекта первого порядка y[k] = 0.7·y[k-1] + 0.3·u[k-1] со случайным входом.</summary>
    private const string PlantLog = """
        options { seed: 4 }

        let n = 300
        let u = vec.zeros(n)
        let y = vec.zeros(n)

        for k in 1..n {
            set u[k] = math.random(low: -1, high: 1)
            set y[k] = 0.7 * y[k - 1] + 0.3 * u[k - 1]
        }
        """;

    [Fact]
    public void Ctrl_Identify_RecoversCoefficients()
    {
        // Коэффициенты закладывались явно, поэтому проверяется не «что-то посчиталось»,
        // а именно возврат к исходным числам.
        RunResult result = Script.RunOk($$"""
            {{PlantLog}}
            let plant = ctrl.identify(u, y, order: 1)
            let info = plant.describe()
            emit a = core.round(info.a[0], digits: 3)
            emit b = core.round(info.b[0], digits: 3)
            emit gain = core.round(info.gain, digits: 3)
            emit order = info.order
            """);

        Assert.Equal(-0.7, (double)result.Emitted["a"]!, 2);
        Assert.Equal(0.3, (double)result.Emitted["b"]!, 2);
        Assert.Equal(1.0, (double)result.Emitted["gain"]!, 2);
        Assert.Equal(1.0, result.Emitted["order"]);
    }

    [Fact]
    public void Ctrl_Identify_ReproducesTheLog()
    {
        RunResult result = Script.RunOk($$"""
            {{PlantLog}}
            let plant = ctrl.identify(u, y, order: 1)
            emit error = stat.rmse(y, plant.response(u)) < 0.01
            """);

        Assert.Equal(true, result.Emitted["error"]);
    }

    [Fact]
    public void Ctrl_Identify_RejectsShortLog()
    {
        Diagnostic error = Script.FailsWith("emit r = ctrl.identify(<1, 2, 3>, <1, 2, 3>, order: 3)");

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("порядок", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_Identify_RejectsMismatchedLog()
    {
        Assert.Equal(
            DiagnosticCodes.SizeMismatch,
            Script.FailsWith("emit r = ctrl.identify(<1, 2>, <1, 2, 3>)").Code);
    }

    [Fact]
    public void Ctrl_StepResponse_SettlesAtGain()
    {
        RunResult result = Script.RunOk("""
            let plant = ctrl.arx(a: <-0.7>, b: <0.3>)
            let response = plant.step_response(200)
            emit final = core.round(response[199], digits: 3)
            """);

        Assert.Equal(1.0, (double)result.Emitted["final"]!, 2);
    }

    [Fact]
    public void Ctrl_ClosedLoop_ReachesSetpoint()
    {
        RunResult result = Script.RunOk("""
            let plant = ctrl.arx(a: <-0.7>, b: <0.3>)
            let controller = ctrl.pid(kp: 0.5, ki: 0.2)
            let sim = ctrl.simulate(plant, controller: controller, setpoint: 1, steps: 300)
            emit final = core.round(sim.y[299], digits: 2)
            emit points = len(sim.t) == len(sim.y)
            """);

        Assert.Equal(1.0, (double)result.Emitted["final"]!, 1);
        Assert.Equal(true, result.Emitted["points"]);
    }

    [Fact]
    public void Ctrl_Simulate_IsRepeatable()
    {
        // Регулятор сбрасывается перед прогоном; иначе накопленный интеграл сделал бы вторую
        // симуляцию другой, и сравнивать настройки стало бы нельзя.
        const string source = """
            let plant = ctrl.arx(a: <-0.7>, b: <0.3>)
            let controller = ctrl.pid(kp: 0.5, ki: 0.2)
            let first = ctrl.simulate(plant, controller: controller, setpoint: 1, steps: 50)
            let second = ctrl.simulate(plant, controller: controller, setpoint: 1, steps: 50)
            emit same = core.to_str(first.y) == core.to_str(second.y)
            """;

        Assert.Equal(true, Script.RunOk(source).Emitted["same"]);
    }

    [Fact]
    public void Ctrl_PidTune_ProducesWorkingController()
    {
        RunResult result = Script.RunOk($$"""
            {{PlantLog}}
            let plant = ctrl.identify(u, y, order: 1)
            let controller = ctrl.pid_tune(plant, softness: 3)
            let gains = controller.gains()
            let sim = ctrl.simulate(plant, controller: controller, setpoint: 1, steps: 400)
            emit kp = gains.kp > 0
            emit settled = math.abs(sim.y[399] - 1) < 0.05
            """);

        Assert.Equal(true, result.Emitted["kp"]);
        Assert.Equal(true, result.Emitted["settled"]);
    }

    [Fact]
    public void Ctrl_PidTune_RejectsDegenerateModel()
    {
        Diagnostic error = Script.FailsWith("""
            let plant = ctrl.arx(a: <0>, b: <0>)
            emit r = ctrl.pid_tune(plant)
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("идентификацию", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_PidLimitsOutput()
    {
        RunResult result = Script.RunOk("""
            let c = ctrl.pid(kp: 100, low: -1, high: 1)
            emit u = c.compute(setpoint: 10, measured: 0, dt: 1)
            """);

        Assert.Equal(1.0, result.Emitted["u"]);
    }

    [Fact]
    public void Ctrl_Lti_StepsState()
    {
        RunResult result = Script.RunOk("""
            let m = ctrl.lti(mat.of([<0.5>]), mat.of([<1>]), mat.of([<1>]))
            let first = m.step(<1>)
            let second = m.step(<1>)
            emit first = first[0]
            emit second = second[0]
            """);

        // Модель фреймворка обновляет состояние и лишь затем считает выход, поэтому первый
        // же шаг уже даёт единицу, а не ноль.
        Assert.Equal(1.0, result.Emitted["first"]);
        Assert.Equal(1.5, result.Emitted["second"]);
    }

    [Fact]
    public void Ctrl_Lqr_ProducesGainMatrix()
    {
        RunResult result = Script.RunOk("""
            let k = ctrl.lqr(mat.of([<1>]), mat.of([<1>]), mat.eye(1), mat.eye(1))
            emit rows = mat.rows(k)
            emit positive = k[0, 0] > 0
            """);

        Assert.Equal(1.0, result.Emitted["rows"]);
        Assert.Equal(true, result.Emitted["positive"]);
    }

    [Fact]
    public void Ctrl_Kalman_TracksConstant()
    {
        RunResult result = Script.RunOk("""
            options { seed: 2 }
            let f = ctrl.kalman(mat.eye(1), mat.of([<0>]), mat.eye(1), q: mat.eye(1) * 0.001, r: mat.eye(1) * 0.1)
            let last = 0
            for i in 0..100 {
                set last = f.update(y: <5 + math.gauss(std: 0.1)>, u: <0>)[0]
            }
            emit estimate = math.abs(last - 5) < 0.3
            """);

        Assert.Equal(true, result.Emitted["estimate"]);
    }

    [Fact]
    public void Ctrl_Mst_RejectsDirectedGraph()
    {
        // Проверка направленности живёт в дескрипторе графа, а не в алгоритме.
        Diagnostic error = Script.FailsWith("""
            let edges = table.of({ from: <0, 1>, to: <1, 2> })
            let g = graph.of(edges, directed: true)
            emit r = g.mst()
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    // --- prep ---

    [Fact]
    public void Prep_Scaler_UsesTrainingParametersOnTest()
    {
        // Главное свойство: параметры считаются по обучающей выборке. Если бы нормировка
        // пересчитывалась по тесту, среднее теста стало бы нулём — и метрика бы соврала.
        RunResult result = Script.RunOk("""
            let train = mat.of([<0>, <10>])
            let test = mat.of([<20>, <30>])
            let scaler = prep.zscore(train)
            let z = scaler.apply(test)
            emit trainMean = core.round(mat.mean(scaler.apply(train))[0], digits: 9)
            emit testMean = core.round(mat.mean(z)[0], digits: 3)
            """);

        Assert.Equal(0.0, result.Emitted["trainMean"]);
        Assert.NotEqual(0.0, result.Emitted["testMean"]);
    }

    [Fact]
    public void Prep_Scaler_RoundTrips()
    {
        RunResult result = Script.RunOk("""
            let x = mat.of([<1, 100>, <3, 300>, <5, 500>])
            let scaler = prep.minmax(x)
            let back = scaler.undo(scaler.apply(x))
            emit same = math.approx(back[2, 1], 500, eps: 1e-9)
            """);

        Assert.Equal(true, result.Emitted["same"]);
    }

    [Fact]
    public void Prep_Scaler_RejectsWrongFeatureCount()
    {
        Diagnostic error = Script.FailsWith("""
            let scaler = prep.zscore(mat.of([<1, 2>, <3, 4>]))
            emit r = scaler.apply(mat.of([<1, 2, 3>]))
            """);

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("порядку", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Prep_Params_AreReadable()
    {
        RunResult result = Script.RunOk("""
            let scaler = prep.minmax(mat.of([<0>, <10>]))
            let p = scaler.params()
            emit center = p.center[0]
            emit scale = p.scale[0]
            """);

        Assert.Equal(0.0, result.Emitted["center"]);
        Assert.Equal(10.0, result.Emitted["scale"]);
    }

    [Fact]
    public void Prep_Augment_GrowsSampleAndIsReproducible()
    {
        const string source = """
            options { seed: 9 }
            let x = mat.of([<1, 2>, <3, 4>])
            let y = <0, 1>
            let a = prep.augment(x, y, times: 2, sigma: 0.1)
            emit rows = mat.rows(a.x)
            emit labels = len(a.y)
            emit firstNoisy = core.round(a.x[2, 0], digits: 6)
            """;

        RunResult first = Script.RunOk(source);
        RunResult second = Script.RunOk(source);

        Assert.Equal(6.0, first.Emitted["rows"]);
        Assert.Equal(6.0, first.Emitted["labels"]);
        Assert.Equal(first.Emitted["firstNoisy"], second.Emitted["firstNoisy"]);
    }

    [Fact]
    public void Prep_Polynomial_AddsPowers()
    {
        RunResult result = Script.RunOk("""
            let p = prep.polynomial(mat.of([<2, 3>]), degree: 2)
            emit cols = mat.cols(p)
            emit squared = p[0, 2]
            """);

        Assert.Equal(4.0, result.Emitted["cols"]);
        Assert.Equal(4.0, result.Emitted["squared"]);
    }
}

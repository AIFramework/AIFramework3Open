using AI.DataStructs.Algebraic;
using AI.MathUtils.ODE;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;

namespace AI.Script.Science;

/// <summary>
/// Пространство <c>ode</c>: численное решение систем обыкновенных дифференциальных уравнений.
/// </summary>
/// <remarks>
/// Система записывается так же, как в учебнике: правая часть — функция <c>(t, s) =&gt; { ... }</c>,
/// где <c>s</c> — запись текущего состояния по именам, а результат — запись производных с теми же
/// именами. Интегрирует адаптивный метод Дормана — Принса 5(4) из <c>AI.ClassicMath</c>: шаг
/// подбирается под заданную точность, а не задаётся вручную.
/// <para>
/// Чем <c>ode.solve</c> отличается от <c>sim.stock_flow</c>: системная динамика считает запасы и
/// потоки с границами (запас не уходит ниже нуля) и годится для моделей «склад, население,
/// деньги», где важна наглядность, а не точность. <c>ode.solve</c> — для физики и химической
/// кинетики: управляемая точность, сохранение энергии на долгих прогонах и события — момент,
/// когда функция состояния проходит через ноль (тело упало, маятник прошёл низ). Символьное
/// решение одного уравнения даёт <c>solve.ode</c>.
/// </para>
/// </remarks>
[ScriptModule("ode", "ОДУ: задача Коши с точностью по допуску и событиями остановки", Version = "0.1", Group = "решатели")]
public static class OdeModule
{
    /// <summary>
    /// Задача Коши для системы ОДУ.
    /// </summary>
    /// <remarks>
    /// Ответ — запись, а не голая таблица: кроме траектории <c>table</c> (колонка <c>time</c> и по
    /// колонке на переменную, <c>points</c> равноотстоящих строк от <c>from</c> до конца) в ней
    /// <c>final</c> — состояние в последний момент, <c>t_end</c> — этот момент, <c>stopped_by</c> —
    /// имя сработавшего события либо пустая строка, <c>t_event</c> — его момент либо <c>nan</c>,
    /// <c>ok</c> — дошёл ли расчёт до конца или до события. Событие — запись
    /// <c>{ имя: (t, s) =&gt; g }</c>: расчёт останавливается, когда <c>g</c> меняет знак, и таблица
    /// кончается ровно в этот момент. Ноль в самый начальный момент событием не считается —
    /// тело, брошенное с земли, не «падает» в момент броска.
    /// </remarks>
    [ScriptFn("solve", "Задача Коши: система ОДУ методом Дормана — Принса, с событиями остановки",
        Example = "let r = ode.solve((t, s) => { y: -0.5 * s.y }, { y: 1 }, until: 10)")]
    public static ScriptRecord Solve(
        IScriptContext context,
        [ScriptParam("правая часть: (t, s) => запись производных с теми же именами, что в y0")] ScriptCallable rhs,
        [ScriptParam("начальное состояние: запись «имя: значение»")] ScriptRecord y0,
        [ScriptParam("конечный момент")] double until,
        [ScriptParam("начальный момент")] double from = 0,
        [ScriptParam("сколько строк в таблице траектории")] int points = 200,
        [ScriptParam("события остановки: запись «имя: (t, s) => число», стоп при смене знака")] ScriptRecord? events = null,
        [ScriptParam("относительный допуск шага")] double rtol = 1e-6,
        [ScriptParam("абсолютный допуск шага")] double atol = 1e-9,
        [ScriptParam("наибольший шаг; 0 — без ограничения")] double max_step = 0)
    {
        const string function = "ode.solve";

        ScriptData.Require(y0.Count > 0, $"{function}: в начальном состоянии нет ни одной переменной");
        ScriptData.Require(until != from, $"{function}: конечный момент совпадает с начальным");
        ScriptData.Require(points >= 2, $"{function}: строк в траектории — хотя бы две");
        ScriptData.Require(rtol > 0 && atol > 0, $"{function}: допуски положительны");
        ScriptData.Require(max_step >= 0, $"{function}: наибольший шаг не может быть отрицательным");

        IReadOnlyList<string> names = y0.Keys;
        var start = new Vector(names.Count);

        for (int j = 0; j < names.Count; j++) start[j] = y0.Values[j].AsNumber($"{function}: начальное значение '{names[j]}'");

        Vector Derivatives(double time, Vector state) => Read(
            ScriptCallbacks.Invoke(context, rhs, ScriptValue.Num(time), ScriptValue.Record(ScriptData.Numbers(names, state))),
            names,
            function);

        IReadOnlyList<string> eventNames = events?.Keys ?? [];
        var stops = new List<OdeEvent>(eventNames.Count);

        for (int e = 0; e < eventNames.Count; e++)
        {
            ScriptCallable callable = events!.Values[e].AsCallable($"{function}: событие '{eventNames[e]}'");
            string what = $"{function}: событие '{eventNames[e]}'";

            stops.Add(new OdeEvent(
                (time, state) => ScriptCallbacks.Invoke(
                        context, callable, ScriptValue.Num(time), ScriptValue.Record(ScriptData.Numbers(names, state)))
                    .AsNumber(what),
                isTerminal: true));
        }

        var options = new DormandPrinceOptions
        {
            RelativeTolerance = rtol,
            AbsoluteTolerance = atol,
            MaxStep = max_step > 0 ? max_step : double.PositiveInfinity,
        };

        context.CountAllocation((long)points * (names.Count + 1));

        OdeSolution solution = DormandPrince.Integrate(Derivatives, from, start, until, options, stops);
        OdeEventHit? hit = solution.StoppedByEvent ? solution.Events[^1] : null;
        double end = solution.FinalTime;

        return ScriptData.Plain(
            ("table", Trajectory(solution, names, from, end, points)),
            ("final", ScriptData.Numbers(names, solution.FinalState)),
            ("t_end", end),
            ("stopped_by", hit == null ? string.Empty : eventNames[hit.EventIndex]),
            ("t_event", hit?.Time ?? double.NaN),
            ("ok", solution.Success),
            ("steps", solution.AcceptedSteps),
            ("evaluations", solution.FunctionEvaluations),
            ("summary", Summary(solution, hit, eventNames, from, until)),
            ("warnings", Warnings(solution, until)));
    }

    // --- внутреннее ---

    /// <summary>Производные из записи, которую вернула правая часть: те же имена, что у состояния.</summary>
    private static Vector Read(ScriptValue value, IReadOnlyList<string> names, string function)
    {
        ScriptRecord record = value.AsRecord($"{function}: результат правой части");

        foreach (string key in record.Keys)
        {
            if (names.Contains(key)) continue;

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: правая часть вернула производную '{key}' переменной, которой нет в y0",
                $"переменные: {string.Join(", ", names)}");
        }

        var derivatives = new Vector(names.Count);

        for (int j = 0; j < names.Count; j++)
        {
            if (!record.TryGet(names[j], out ScriptValue derivative))
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"{function}: правая часть не вернула производную '{names[j]}'",
                    $"постоянная переменная — производная ноль: {names[j]}: 0");
            }

            derivatives[j] = derivative.AsNumber($"{function}: производная '{names[j]}'");
        }

        return derivatives;
    }

    /// <summary>Равноотстоящие строки траектории по плотной выдаче интегратора.</summary>
    private static ScriptTable Trajectory(OdeSolution solution, IReadOnlyList<string> names, double from, double end, int points)
    {
        var time = new Vector(points);
        var columns = new Vector[names.Count];

        for (int j = 0; j < names.Count; j++) columns[j] = new Vector(points);

        for (int i = 0; i < points; i++)
        {
            // Последняя строка — ровно конец расчёта, без накопленной ошибки сложения шагов
            double t = i == points - 1 ? end : from + ((end - from) * i / (points - 1));
            Vector state = solution.Interpolate(t);

            time[i] = t;

            for (int j = 0; j < names.Count; j++) columns[j][i] = state[j];
        }

        var table = new List<ScriptColumn>(names.Count + 1) { ScriptColumn.FromVector("time", time) };

        for (int j = 0; j < names.Count; j++) table.Add(ScriptColumn.FromVector(names[j], columns[j]));

        return ScriptTable.Create(table);
    }

    private static string Summary(OdeSolution solution, OdeEventHit? hit, IReadOnlyList<string> eventNames, double from, double until)
    {
        string course = $"от t = {ScriptFormatter.Number(from)}";

        if (hit != null)
            return $"Расчёт {course} остановлен событием «{eventNames[hit.EventIndex]}» при t = {ScriptFormatter.Number(hit.Time)}; "
                + $"принятых шагов {solution.AcceptedSteps}.";

        return solution.Success
            ? $"Расчёт {course} до t = {ScriptFormatter.Number(until)} завершён: принятых шагов {solution.AcceptedSteps}, "
                + $"вычислений правой части {solution.FunctionEvaluations}."
            : $"Расчёт {course} прерван при t = {ScriptFormatter.Number(solution.FinalTime)}, не дойдя до "
                + $"t = {ScriptFormatter.Number(until)}.";
    }

    private static IReadOnlyList<string> Warnings(OdeSolution solution, double until)
    {
        var warnings = new List<string>();

        if (!solution.Success)
        {
            warnings.Add($"Интегрирование не дошло до t = {ScriptFormatter.Number(until)}: исчерпан лимит шагов либо шаг "
                + "стал исчезающе мал. Так ведут себя жёсткие системы и решения, уходящие в бесконечность за конечное "
                + "время; таблица кончается там, где расчёт остановился.");
        }

        if (solution.RejectedSteps > solution.AcceptedSteps)
        {
            warnings.Add("Отвергнутых шагов больше, чем принятых: система, вероятно, жёсткая либо правая часть "
                + "разрывна. Ответ верен с заданной точностью, но расчёт дорог.");
        }

        return warnings;
    }
}

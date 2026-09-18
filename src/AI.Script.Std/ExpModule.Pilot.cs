using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пробный прогон опыта: первое испытание плана и оценка на весь план.
/// </summary>
public static partial class ExpModule
{
    /// <summary>
    /// Считает первую точку плана один раз, записывает оценку и останавливает прогон.
    /// </summary>
    /// <remarks>
    /// Испытание в журнал не пишется: пробный прогон не опыт, и его строка в журнале выглядела бы
    /// испытанием с одним повтором. Кэш стадий при этом работает как обычно, поэтому настоящий
    /// прогон первую точку второй раз не оплачивает, если испытание оформлено стадией.
    /// </remarks>
    private static async Task PilotAsync(
        IScriptContext context, ExperimentPilot pilot, ScriptTable plan, ScriptCallable trial, int repeat)
    {
        ExternalUsage before = context.Usage;

        _ = await context
            .CallAsync(ScriptValue.Fn(trial), ScriptValue.Record(plan.Row(0)))
            .ConfigureAwait(false);

        ExternalUsage after = context.Usage;
        var one = new ExternalUsage(after.Calls - before.Calls, after.Tokens - before.Tokens, after.Cost - before.Cost);

        int trials = checked(plan.RowCount * repeat);

        pilot.Record(new ExperimentEstimate(trials, before, one));

        throw new ScriptError(
            DiagnosticCodes.PilotStopped,
            $"exp.run: пробный прогон, посчитано одно испытание из {trials}",
            "оценка расхода записана; настоящий прогон считается без пилота");
    }
}

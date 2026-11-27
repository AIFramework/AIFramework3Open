using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using Serilog;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Движок контракта (<see cref="IEngine"/>) над старым <c>ReActEngine</c>, ключ <see cref="EngineKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// Движок собирается на каждый прогон через <c>ReActAgentBuilder</c>: модель — порт <c>EngineInput.Model</c> под
/// <c>ILLMClient</c> (роль <c>decide</c>), инструменты — <c>EngineInput.Tools</c> через гейт эффектов
/// (<c>EffectGate</c>) и <c>AsToolSource</c>. Набор, который хост уже обернул гейтом, второй раз не оборачивается:
/// так хост читает журнал эффектов для <c>RunState</c>.
/// </para>
/// <para>Границы до шагов Ф5/Ф6 (подробно — README сборки):</para>
/// <list type="bullet">
/// <item><c>EngineInput.State</c> или ответ человека на входе — итог <see cref="EngineStatus.Failed"/> с кодом
/// <see cref="EngineInteropCodes.ContinuationNotSupported"/>.</item>
/// <item>Бюджет: срок переходит в предел времени движка до старта; шаг спрашивается у бюджета в начале каждого
/// шага цикла, расход модели сообщается бюджету после каждого вызова. Внутри шага (несколько инструментов,
/// синтез ответа после цикла) токены и деньги не проверяются.</item>
/// </list>
/// </remarks>
public sealed class ReActEngineAdapter : IEngine
{
    /// <summary>Ключ движка.</summary>
    public const string EngineKey = "react";

    private readonly ILogger? _log;

    /// <summary>Создает адаптер.</summary>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    public ReActEngineAdapter(ILogger? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public string Key => EngineKey;

    /// <inheritdoc />
    public IAsyncEnumerable<EngineEvent> RunAsync(EngineInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return RunCoreAsync(input, cancellationToken);
    }

    private async IAsyncEnumerable<EngineEvent> RunCoreAsync(
        EngineInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (ReActRun.Refusal(input, out EngineOptions options, planSteps: false) is { } refusal)
        {
            yield return new EngineEvent.Finished { Result = refusal };
            yield break;
        }

        var recorder = new CallRecorder(ReActRun.Gated(input.Tools, _log));
        var tag = new ToolContext { RunId = input.RunId, Path = new StepPath { Tier = 1 } };
        var outcome = new ReActOutcome();

        await foreach (EngineEvent evt in ReActRun.StreamAsync(
                           ReActRun.Build(input, recorder, options, _log),
                           ReActRun.Query(ReActRun.TaskText(input.Task), input.Context, tag, _log),
                           recorder,
                           input.Budget,
                           step => new StepPath { Tier = step },
                           outcome,
                           _log,
                           cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }

        EngineResult result = outcome.BudgetStop is StopCode stop
            ? ReActRun.BudgetStopped(stop, outcome.Usage)
            : ReActRun.ToEngineResult(outcome.Result!, outcome.Usage, _log);

        yield return new EngineEvent.Finished { Result = result };
    }
}

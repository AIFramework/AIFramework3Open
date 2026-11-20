using AI.Agents.Contracts.Engines;
using AI.LLM.Agents.ReAct;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Как адаптеры движков переводят прогон <c>ReActEngine</c> в контракт: причина остановки → статус и код,
/// событие → событие. Храповик перебирает все значения <see cref="ReActStopReason"/> и всех наследников
/// <see cref="ReActEvent"/>: новое значение без записи роняет тест.
/// </summary>
public static class ReActEngineTranslationTable
{
    /// <summary>Причина остановки → статус итога и код. Исчерпание предела — <see cref="EngineStatus.Incomplete"/>, а не <see cref="EngineStatus.Done"/>.</summary>
    public static IReadOnlyDictionary<ReActStopReason, (EngineStatus Status, StopCode Code)> StopReasons { get; } =
        new Dictionary<ReActStopReason, (EngineStatus, StopCode)>
        {
            [ReActStopReason.FinalAnswer] = (EngineStatus.Done, StopCode.FinalAnswer),
            [ReActStopReason.TerminalTool] = (EngineStatus.Done, StopCode.TerminalTool),
            [ReActStopReason.IterationLimit] = (EngineStatus.Incomplete, StopCode.StepLimit),
            [ReActStopReason.TimeLimit] = (EngineStatus.Incomplete, StopCode.TimeLimit),
            [ReActStopReason.NoProgress] = (EngineStatus.Incomplete, StopCode.NoProgress),
            [ReActStopReason.NoTools] = (EngineStatus.Incomplete, StopCode.NoTools),
            [ReActStopReason.PolicyFailure] = (EngineStatus.Failed, StopCode.PolicyFailure),
            [ReActStopReason.Cancelled] = (EngineStatus.Cancelled, StopCode.Cancelled),
            [ReActStopReason.EngineFailure] = (EngineStatus.Failed, StopCode.EngineFailure),
        };

    /// <summary>Событие старого движка (имя вложенного типа <see cref="ReActEvent"/>) → что получается в контракте.</summary>
    public static IReadOnlyDictionary<string, string> Events { get; } = new Dictionary<string, string>
    {
        [nameof(ReActEvent.Phase)] =
            "фаза шага (react.step) → EngineEvent.Step с номером шага; бюджет спрашивается здесь (TryStep). " +
            "Фаза синтеза и прочие фазы не переводятся: в контракте нет события фазы",
        [nameof(ReActEvent.Thought)] = "EngineEvent.Step того же шага с Thought",
        [nameof(ReActEvent.ToolStarted)] =
            "EngineEvent.ToolStarted; аргументы — ModelTranslation.ParseArguments, EffectKey = null: ключ назначает гейт при вызове",
        [nameof(ReActEvent.ToolProgress)] =
            "EngineEvent.ToolProgress; Data — JSON полезной нагрузки, если она сериализуется, иначе null с записью в журнал",
        [nameof(ReActEvent.Observed)] =
            "EngineEvent.ToolFinished; итог и вызов с ключом — из набора, если вызов дошел до него, иначе перевод наблюдения",
        [nameof(ReActEvent.Note)] = "не переводится: служебной пометки движка в контракте нет; пишется в журнал",
        [nameof(ReActEvent.AnswerChunk)] = "EngineEvent.Text",
        [nameof(ReActEvent.ReasoningChunk)] = "EngineEvent.Reasoning",
        [nameof(ReActEvent.Spent)] = "EngineEvent.Spent и IRunBudget.Spend; нулевой расход без стоимости не отдается",
        [nameof(ReActEvent.Completed)] = "EngineEvent.Finished по StopReasons",
    };
}

using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Набор инструментов контракта под старым интерфейсом <see cref="IReActToolSource"/>.
/// </summary>
/// <remarks>
/// Набор отдает только спецификации и вызов по имени, поэтому каждый инструмент — это спецификация из
/// <see cref="IToolSet.Available"/> и вызов через <see cref="IToolSet.CallAsync"/>. Набор не классифицирует
/// вызов, поэтому вид действия для журнала — <see cref="ToolSpec.Action"/>.
/// </remarks>
internal sealed class ToolSetAsReActSource : IReActToolSource
{
    private readonly IToolSet _set;
    private readonly ILogger? _log;

    /// <summary>Оборачивает набор инструментов контракта.</summary>
    public ToolSetAsReActSource(IToolSet set, ILogger? log)
    {
        _set = set ?? throw new ArgumentNullException(nameof(set));
        _log = log;
    }

    /// <inheritdoc />
    /// <remarks>Контекст прогона набору не нужен: он отдает то, что доступно сейчас.</remarks>
    public IEnumerable<IReActTool> GetTools(ReActRunContext context) =>
        [.. (_set.Available ?? []).Select(spec => new ToolAsReActTool(new Member(_set, spec), _log))];

    /// <summary>Инструмент набора: спецификация и вызов через набор.</summary>
    private sealed class Member(IToolSet set, ToolSpec spec) : ITool
    {
        /// <inheritdoc />
        public ToolSpec Spec { get; } = spec;

        /// <inheritdoc />
        public IAsyncEnumerable<ToolUpdate> CallAsync(
            ToolCall call, ToolContext context, CancellationToken cancellationToken = default) =>
            set.CallAsync(call, context, cancellationToken);
    }
}

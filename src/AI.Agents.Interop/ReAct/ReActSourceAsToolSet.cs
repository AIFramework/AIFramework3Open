using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Источник старых инструментов под контрактом <see cref="IToolSet"/>.
/// </summary>
/// <remarks>
/// Источник отдает инструменты для контекста прогона, поэтому набор держит базовый контекст: из него
/// берутся запрос и история (в <see cref="ToolContext"/> их нет). Каждый вызов спрашивает источник заново,
/// как и старый движок на каждом шаге, а предупреждение о неизвестном виде действия пишется один раз на имя
/// инструмента: источник вправе отдавать каждый раз новые экземпляры.
/// </remarks>
internal sealed class ReActSourceAsToolSet : IToolSet
{
    private readonly IReActToolSource _source;
    private readonly ReActRunContext _baseRun;
    private readonly IReadOnlyDictionary<string, ToolAction>? _actions;
    private readonly IReadOnlyDictionary<string, ToolEffect>? _effects;
    private readonly ILogger? _log;
    private readonly HashSet<string> _warned = new(ToolSpec.NameComparer);

    /// <summary>Оборачивает источник старых инструментов.</summary>
    public ReActSourceAsToolSet(
        IReActToolSource source,
        ReActRunContext? baseRun,
        IReadOnlyDictionary<string, ToolAction>? actions,
        ILogger? log)
        : this(source, baseRun, actions, effects: null, log)
    {
    }

    /// <summary>
    /// Оборачивает источник старых инструментов с эффектами из описаний: вид действия и «повтор безопасен» одной
    /// записью. Инструмент без записи получает <see cref="ToolAction.Unknown"/> и <see cref="ToolIdempotency.Unknown"/>
    /// и предупреждение (одно на имя).
    /// </summary>
    public ReActSourceAsToolSet(
        IReActToolSource source,
        ReActRunContext? baseRun,
        IReadOnlyDictionary<string, ToolEffect> effects,
        ILogger? log)
        : this(source, baseRun, actions: null, effects ?? throw new ArgumentNullException(nameof(effects)), log)
    {
    }

    private ReActSourceAsToolSet(
        IReActToolSource source,
        ReActRunContext? baseRun,
        IReadOnlyDictionary<string, ToolAction>? actions,
        IReadOnlyDictionary<string, ToolEffect>? effects,
        ILogger? log)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        if (baseRun?.Tag != null)
        {
            throw new ArgumentException(
                "Метка базового контекста занята: при вызове через набор в ReActRunContext.Tag едет ToolContext " +
                "(идентификатор запуска и путь шага). Передайте контекст без метки.",
                nameof(baseRun));
        }

        _baseRun = baseRun ?? new ReActRunContext(string.Empty);
        _actions = actions;
        _effects = effects;
        _log = log;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Available => [.. Adapt(_baseRun).Select(tool => tool.Spec)];

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolUpdate> CallAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        ReActRunContext run = ReActTranslation.ToRunContext(context, _baseRun);
        ReActToolAsTool? tool = Adapt(run).FirstOrDefault(t => ToolSpec.NameComparer.Equals(t.Spec.Name, call.Name));

        if (tool == null)
        {
            // Текст и код те же, что у движка ReAct; текст составил сам адаптер, поэтому происхождение доверенное.
            yield return new ToolUpdate.Final
            {
                Result = new ToolResult
                {
                    Status = ToolStatus.Failed,
                    Text = "инструмент не найден",
                    Origin = new Origin { Kind = OriginKind.Trusted },
                    ErrorCode = ReActInteropCodes.UnknownTool,
                },
            };
            yield break;
        }

        await foreach (ToolUpdate update in tool.CallAsync(call, context, cancellationToken).ConfigureAwait(false))
            yield return update;
    }

    /// <summary>Инструменты источника для контекста, обернутые в контракт.</summary>
    private List<ReActToolAsTool> Adapt(ReActRunContext run)
    {
        var tools = new List<ReActToolAsTool>();
        foreach (IReActTool tool in _source.GetTools(run) ?? [])
        {
            if (tool == null)
                continue;

            ToolAction? action = _actions != null && _actions.TryGetValue(tool.Name, out ToolAction known) ? known : null;
            ToolIdempotency? idempotency = null;
            bool idempotencyMissing = false;
            if (_effects != null)
            {
                idempotencyMissing = !_effects.TryGetValue(tool.Name, out ToolEffect effect);
                action = idempotencyMissing ? null : effect.Action;
                idempotency = idempotencyMissing ? ToolIdempotency.Unknown : effect.Idempotency;
            }

            var adapter = new ReActToolAsTool(tool, action, idempotency, _baseRun, _log);

            bool first;
            lock (_warned)
                first = _warned.Add(adapter.Spec.Name);

            if (first && idempotencyMissing)
                adapter.WarnUnknownEffect();
            else if (first && action == null)
                adapter.WarnUnknownAction();

            tools.Add(adapter);
        }

        return tools;
    }
}

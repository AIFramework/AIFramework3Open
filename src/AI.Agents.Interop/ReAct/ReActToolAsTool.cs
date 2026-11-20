using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Старый инструмент ReAct под контрактом <see cref="ITool"/>.
/// </summary>
/// <remarks>
/// Вид действия старый инструмент не знает, поэтому без явного вида он <see cref="ToolAction.Unknown"/>
/// (действует как <see cref="ToolAction.Irreversible"/>), и об этом одно предупреждение в журнал на инструмент.
/// Вид <see cref="ToolAction.Read"/> сам не появляется никогда.
/// </remarks>
internal sealed class ReActToolAsTool : ITool
{
    private readonly IReActTool _tool;
    private readonly ReActRunContext? _baseRun;
    private readonly ILogger? _log;

    /// <summary>Оборачивает старый инструмент.</summary>
    /// <param name="tool">Старый инструмент.</param>
    /// <param name="action">Вид действия; <see langword="null"/>: <see cref="ToolAction.Unknown"/>.</param>
    /// <param name="baseRun">Запрос и история прогона для контекста вызова; <see langword="null"/>: пустые.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/> на момент записи.</param>
    public ReActToolAsTool(IReActTool tool, ToolAction? action, ReActRunContext? baseRun, ILogger? log)
        : this(tool, action, idempotency: null, baseRun, log)
    {
    }

    /// <summary>Оборачивает старый инструмент с известной осью «повтор безопасен».</summary>
    /// <param name="tool">Старый инструмент.</param>
    /// <param name="action">Вид действия; <see langword="null"/>: <see cref="ToolAction.Unknown"/>.</param>
    /// <param name="idempotency">«Повтор безопасен»; <see langword="null"/>: не объявлено (<see cref="ToolSpec.Idempotency"/> = <see langword="null"/>).</param>
    /// <param name="baseRun">Запрос и история прогона для контекста вызова; <see langword="null"/>: пустые.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/> на момент записи.</param>
    public ReActToolAsTool(IReActTool tool, ToolAction? action, ToolIdempotency? idempotency, ReActRunContext? baseRun, ILogger? log)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _baseRun = baseRun;
        _log = log;

        string schema = string.IsNullOrWhiteSpace(tool.ParametersJsonSchema)
            ? NativeToolCallPolicy.BuildPlainSchema(tool.Description)
            : tool.ParametersJsonSchema;

        using JsonDocument document = JsonDocument.Parse(schema);
        Spec = new ToolSpec
        {
            Name = tool.Name,
            Description = tool.Description ?? string.Empty,
            Parameters = document.RootElement.Clone(),
            Action = action ?? ToolAction.Unknown,
            Idempotency = idempotency,
            Group = null,
            About = null,
        };

        if (tool.Tags is { Count: > 0 } tags)
        {
            Logger.Information(
                "ReAct → контракт: метки инструмента {Tool} ({Tags}) не переводятся: в ToolSpec.Group их не превращаем",
                Spec.Name, string.Join(", ", tags));
        }
    }

    /// <inheritdoc />
    public ToolSpec Spec { get; }

    /// <summary>Старый инструмент, который обернут.</summary>
    public IReActTool Inner => _tool;

    private ILogger Logger => _log ?? Log.Logger;

    /// <summary>Пишет предупреждение о неизвестном виде действия.</summary>
    public void WarnUnknownAction() =>
        Logger.Warning(
            "ReAct → контракт: инструмент {Tool} не знает вида действия; вид Unknown, действует как Irreversible. " +
            "Если вид известен, передайте его явно: AsTool(action: ...)",
            Spec.Name);

    /// <summary>Пишет одно предупреждение о незаданном атрибуте эффекта: вид и «повтор безопасен» неизвестны.</summary>
    public void WarnUnknownEffect() =>
        Logger.Warning(
            "[AgentTool] → контракт: у инструмента {Tool} нет [ToolEffect]; вид Unknown (действует как Irreversible), " +
            "повтор Unknown (действует как NotIdempotent). Объявите их атрибутом [ToolEffect(...)] на методе",
            Spec.Name);

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolUpdate> CallAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        string raw = call.Arguments.ValueKind == JsonValueKind.Undefined ? string.Empty : call.Arguments.GetRawText();

        // У инструмента без схемы модель видит {"input": "…"}, а сам он ждет строку — разворачиваем так же,
        // как NativeToolCallPolicy, иначе один инструмент получал бы разный аргумент в двух движках.
        string arguments = NativeToolCallPolicy.Unwrap(_tool.Name, raw, [_tool]);

        var invocation = new ReActToolInvocation(
            new ReActAction(_tool.Name, arguments, call.Id),
            ReActTranslation.ToRunContext(context, _baseRun));

        await foreach (ReActToolEvent toolEvent in _tool.ExecuteAsync(invocation, cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ToolUpdate update = ReActTranslation.ToUpdate(toolEvent, Spec.Name, Logger);
            yield return update;

            // Окончателен первый итог: дальше поток не читается.
            if (update is ToolUpdate.Final)
                yield break;
        }
    }
}

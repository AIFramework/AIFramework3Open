using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Инструмент контракта под старым интерфейсом <see cref="IReActTool"/>.
/// </summary>
/// <remarks>
/// Перед вызовом адаптер спрашивает вид действия (<see cref="ITool.Classify(ToolCall)"/>) и пишет его в журнал.
/// Гейта в старом движке нет, поэтому вид ни на что не влияет: поведение движка не меняется.
/// </remarks>
internal sealed class ToolAsReActTool : IReActTool
{
    private readonly ITool _tool;
    private readonly ILogger? _log;

    /// <summary>Оборачивает инструмент контракта.</summary>
    /// <param name="tool">Инструмент контракта.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/> на момент записи.</param>
    public ToolAsReActTool(ITool tool, ILogger? log)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _log = log;

        ToolSpec spec = tool.Spec ?? throw new ArgumentException("У инструмента нет спецификации (Spec = null).", nameof(tool));
        Name = spec.Name;
        Description = spec.Description;
        ParametersJsonSchema = spec.Parameters.GetRawText();
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string ParametersJsonSchema { get; }

    /// <inheritdoc />
    /// <remarks><see cref="ToolSpec.Group"/> в метки не превращается: меток нет.</remarks>
    public IReadOnlyCollection<string> Tags => [];

    /// <summary>Инструмент контракта, который обернут.</summary>
    public ITool Inner => _tool;

    private ILogger Logger => _log ?? Log.Logger;

    /// <inheritdoc />
    public async IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
        ReActToolInvocation invocation, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var call = new ToolCall
        {
            Id = invocation.ActionId,
            Name = Name,
            Arguments = ToArguments(invocation.Arguments),
        };

        ToolAction action = _tool.Classify(call);
        Logger.Information(
            "Контракт → ReAct: вызов {Tool} ({CallId}) вида {Action}, действует как {Effective}; гейта в старом движке нет",
            Name, call.Id, action, action.Effective());

        await foreach (ToolUpdate update in _tool
                           .CallAsync(call, ReActTranslation.ToToolContext(invocation.Run), cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ReActToolEvent toolEvent = ReActTranslation.ToEvent(update, Name, Logger);
            yield return toolEvent;

            // Окончателен первый итог: дальше поток не читается.
            if (toolEvent is ReActToolEvent.Result)
                yield break;
        }
    }

    /// <summary>
    /// Аргумент старого движка (строка) → аргументы контракта (JSON). Пусто — пустой объект; JSON — как есть;
    /// простая строка — в единственное свойство схемы, а если оно не одно, то строкой JSON: аргумент не
    /// подгоняется под схему, которой не соответствует.
    /// </summary>
    private JsonElement ToArguments(string arguments)
    {
        string text = (arguments ?? string.Empty).Trim();
        if (text.Length == 0)
            return JsonSerializer.SerializeToElement(new Dictionary<string, string>());

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Не JSON — простая строка.
        }

        return SoleProperty(_tool.Spec.Parameters) is { } name
            ? JsonSerializer.SerializeToElement(new Dictionary<string, string> { [name] = arguments! })
            : JsonSerializer.SerializeToElement(arguments);
    }

    /// <summary>Имя единственного свойства схемы; <see langword="null"/>: свойств нет или их больше одного.</summary>
    private static string? SoleProperty(JsonElement schema)
    {
        if (!schema.TryGetProperty("properties", out JsonElement properties) || properties.ValueKind != JsonValueKind.Object)
            return null;

        string? sole = null;
        foreach (JsonProperty property in properties.EnumerateObject())
        {
            if (sole != null)
                return null;
            sole = property.Name;
        }

        return sole;
    }
}

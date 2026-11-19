using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Точки входа адаптеров между старыми инструментами ReAct и контрактами: <c>tool.AsTool()</c>,
/// <c>tool.AsReActTool()</c>, <c>source.AsToolSet()</c>, <c>set.AsToolSource()</c>.
/// </summary>
/// <remarks>
/// Как переводится каждое поле — <see cref="ReActTranslationTable"/>; сами значения переводит
/// <see cref="ReActTranslation"/>. Журнал — Serilog, как в AI.LLM (<c>ToolRegistry</c>); параметр
/// <c>log</c> = <see langword="null"/> означает <see cref="Log.Logger"/> на момент записи.
/// </remarks>
public static class ReActToolExtensions
{
    /// <summary>Инструменты, о неизвестном виде которых уже предупредили: одно предупреждение на экземпляр.</summary>
    private static readonly ConditionalWeakTable<IReActTool, object> Warned = new();

    /// <summary>Значение в <see cref="Warned"/>: важен только ключ.</summary>
    private static readonly object Mark = new();

    /// <summary>Старый инструмент под контрактом <see cref="ITool"/>.</summary>
    /// <param name="tool">Старый инструмент.</param>
    /// <param name="action">
    /// Вид действия, если он известен вызывающему; <see langword="null"/>: <see cref="ToolAction.Unknown"/>
    /// (действует как <see cref="ToolAction.Irreversible"/>) и одно предупреждение в журнал на инструмент.
    /// </param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Инструмент контракта. Схема параметров при <c>ParametersJsonSchema = null</c> — та же простая
    /// схема <c>{"input": string}</c>, что строит <c>NativeToolCallPolicy</c>.</returns>
    /// <exception cref="ArgumentException">Имя инструмента не подходит под <see cref="ToolSpec.NamePattern"/>.</exception>
    /// <exception cref="System.Text.Json.JsonException">Схема параметров инструмента — не JSON.</exception>
    public static ITool AsTool(this IReActTool tool, ToolAction? action = null, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var adapter = new ReActToolAsTool(tool, action, baseRun: null, log);
        if (action == null && Warned.TryAdd(tool, Mark))
            adapter.WarnUnknownAction();

        return adapter;
    }

    /// <summary>Инструмент контракта под старым интерфейсом <see cref="IReActTool"/>.</summary>
    /// <param name="tool">Инструмент контракта.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Старый инструмент. Перед каждым вызовом вид действия (<see cref="ITool.Classify(ToolCall)"/>) пишется
    /// в журнал; гейта в старом движке нет.</returns>
    public static IReActTool AsReActTool(this ITool tool, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return new ToolAsReActTool(tool, log);
    }

    /// <summary>Источник старых инструментов под контрактом <see cref="IToolSet"/>.</summary>
    /// <param name="source">Источник старых инструментов.</param>
    /// <param name="run">
    /// Контекст прогона, для которого источник отдает инструменты и из которого вызов берет запрос и историю;
    /// <see langword="null"/>: пустой запрос и история. Метка (<see cref="ReActRunContext.Tag"/>) должна быть
    /// пустой: при вызове в ней едет <see cref="ToolContext"/>.
    /// </param>
    /// <param name="actions">
    /// Известные виды действия по имени инструмента (с учетом регистра); <see langword="null"/> или имени нет:
    /// <see cref="ToolAction.Unknown"/> и одно предупреждение на имя.
    /// </param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Набор контракта. Неизвестное имя дает итог <see cref="ToolStatus.Failed"/> с кодом
    /// <see cref="ReActInteropCodes.UnknownTool"/>, а не исключение.</returns>
    /// <exception cref="ArgumentException">У <paramref name="run"/> занята метка.</exception>
    public static IToolSet AsToolSet(
        this IReActToolSource source,
        ReActRunContext? run = null,
        IReadOnlyDictionary<string, ToolAction>? actions = null,
        ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReActSourceAsToolSet(source, run, actions, log);
    }

    /// <summary>Набор инструментов контракта под старым интерфейсом <see cref="IReActToolSource"/>.</summary>
    /// <param name="set">Набор контракта.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Источник старых инструментов: по инструменту на спецификацию из <see cref="IToolSet.Available"/>.</returns>
    public static IReActToolSource AsToolSource(this IToolSet set, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new ToolSetAsReActSource(set, log);
    }
}

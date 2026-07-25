namespace AI.LLM.Agents.ReAct.Tools;

/// <summary>
/// Инструмент цикла ReAct.
/// <para>
/// В отличие от инструментов на атрибутах (<see cref="AI.LLM.Agents.Tools.AgentToolAttribute"/>),
/// которые находятся рефлексией по методам объекта, такой инструмент собирается в рантайме —
/// вокруг сессии, пользователя, подключённой интеграции. Оба стиля работают вместе:
/// <see cref="Interop.ToolRegistryToolSource"/> приводит атрибутные инструменты к этому интерфейсу.
/// </para>
/// </summary>
public interface IReActTool
{
    /// <summary>Имя инструмента — то, что модель называет в поле действия.</summary>
    string Name { get; }

    /// <summary>Описание для модели: когда вызывать и что передавать в аргументе.</summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema аргументов. <c>null</c> — инструмент принимает один строковый аргумент
    /// и потому доступен и моделям без нативного function calling.
    /// </summary>
    string ParametersJsonSchema => null;

    /// <summary>
    /// Метки инструмента («web», «documents», «integration»). Позволяют вызывающей стороне
    /// принимать решения о наборе инструментов, не перечисляя их имена: набор меняется —
    /// условие остаётся. Никогда не <c>null</c>.
    /// </summary>
    IReadOnlyCollection<string> Tags => [];

    /// <summary>Исполняет инструмент, отдавая прогресс по ходу дела.</summary>
    /// <param name="invocation">Вызов: аргумент и контекст прогона.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>
    /// Поток событий, последним из которых должен быть <see cref="ReActToolEvent.Result"/>.
    /// Если результата нет, движок считает вызов неудачным.
    /// </returns>
    IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
        ReActToolInvocation invocation, CancellationToken cancellationToken = default);
}

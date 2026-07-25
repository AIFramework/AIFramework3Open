using System.Runtime.CompilerServices;

namespace AI.LLM.Agents.ReAct.Tools;

/// <summary>
/// Инструмент, собранный из делегата. Обычный случай: инструмент замыкается на состояние
/// прогона (сессия, владелец, подключение) и живёт ровно один ход, поэтому объявлять под него
/// класс с атрибутами незачем.
/// </summary>
/// <remarks>
/// Интерфейс инструмента потоковый, но большинству инструментов поток не нужен. Фабрики
/// <see cref="FromText"/> и <see cref="FromOutcome"/> закрывают простые случаи одной строкой,
/// оставляя движку ровно один интерфейс — без проверок типа во время исполнения.
/// </remarks>
public sealed class DelegateReActTool : IReActTool
{
    private readonly Func<ReActToolInvocation, CancellationToken, IAsyncEnumerable<ReActToolEvent>> _run;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string ParametersJsonSchema { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags { get; }

    private DelegateReActTool(
        string name,
        string description,
        Func<ReActToolInvocation, CancellationToken, IAsyncEnumerable<ReActToolEvent>> run,
        string parametersJsonSchema,
        IReadOnlyCollection<string> tags)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя инструмента не может быть пустым.", nameof(name));

        Name = name.Trim();
        Description = description ?? string.Empty;
        ParametersJsonSchema = parametersJsonSchema;
        Tags = tags ?? [];
        _run = run ?? throw new ArgumentNullException(nameof(run));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
        ReActToolInvocation invocation, CancellationToken cancellationToken = default) =>
        _run(invocation, cancellationToken);

    /// <summary>Инструмент из потоковой функции — может отдавать прогресс по ходу работы.</summary>
    /// <param name="name">Имя инструмента.</param>
    /// <param name="description">Описание для модели.</param>
    /// <param name="run">Исполнитель, отдающий события.</param>
    /// <param name="parametersJsonSchema">Схема аргументов; необязательна.</param>
    /// <param name="tags">Метки инструмента; необязательны.</param>
    public static DelegateReActTool FromStream(
        string name,
        string description,
        Func<ReActToolInvocation, CancellationToken, IAsyncEnumerable<ReActToolEvent>> run,
        string parametersJsonSchema = null,
        IReadOnlyCollection<string> tags = null) =>
        new(name, description, run, parametersJsonSchema, tags);

    /// <summary>Инструмент из функции, возвращающей результат целиком.</summary>
    /// <param name="name">Имя инструмента.</param>
    /// <param name="description">Описание для модели.</param>
    /// <param name="run">Исполнитель.</param>
    /// <param name="parametersJsonSchema">Схема аргументов; необязательна.</param>
    /// <param name="tags">Метки инструмента; необязательны.</param>
    public static DelegateReActTool FromOutcome(
        string name,
        string description,
        Func<ReActToolInvocation, CancellationToken, Task<ReActToolOutcome>> run,
        string parametersJsonSchema = null,
        IReadOnlyCollection<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new DelegateReActTool(
            name, description, (invocation, ct) => OnceAsync(run, invocation, ct), parametersJsonSchema, tags);
    }

    /// <summary>Инструмент из функции, возвращающей текст наблюдения.</summary>
    /// <param name="name">Имя инструмента.</param>
    /// <param name="description">Описание для модели.</param>
    /// <param name="run">Исполнитель; получает сырой аргумент.</param>
    /// <param name="parametersJsonSchema">Схема аргументов; необязательна.</param>
    /// <param name="tags">Метки инструмента; необязательны.</param>
    public static DelegateReActTool FromText(
        string name,
        string description,
        Func<string, CancellationToken, Task<string>> run,
        string parametersJsonSchema = null,
        IReadOnlyCollection<string> tags = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        return FromOutcome(
            name,
            description,
            async (invocation, ct) =>
            {
                string text = await run(invocation.Arguments, ct).ConfigureAwait(false);
                return ReActToolOutcome.Success(text);
            },
            parametersJsonSchema,
            tags);
    }

    private static async IAsyncEnumerable<ReActToolEvent> OnceAsync(
        Func<ReActToolInvocation, CancellationToken, Task<ReActToolOutcome>> run,
        ReActToolInvocation invocation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ReActToolOutcome outcome = await run(invocation, cancellationToken).ConfigureAwait(false);
        yield return new ReActToolEvent.Result(outcome ?? ReActToolOutcome.Failure("инструмент не вернул результат"));
    }
}

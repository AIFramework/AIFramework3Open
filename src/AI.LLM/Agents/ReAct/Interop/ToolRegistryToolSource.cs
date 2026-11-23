using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.LLM.Agents.ReAct.Interop;

/// <summary>
/// Мост к инструментам, объявленным атрибутами: <see cref="ToolRegistry"/> становится обычным
/// источником инструментов цикла. Существующие инструменты продолжают работать без правок.
/// </summary>
public sealed class ToolRegistryToolSource : IReActToolSource
{
    private const string ObsoleteText =
        "Строка инструмента здесь молча считается удачей. Выберите правило явно: перегрузка с ToolTextResult " +
        "(Success — как прежде, Refused — только итог с признаком успеха).";

    private readonly ToolRegistry _registry;
    private readonly ToolTextResult _textResults;
    private readonly Func<Type, bool> _alsoTyped;
    private readonly Func<object, ReActToolOutcome> _interpret;

    /// <summary>Создаёт источник поверх реестра; строка инструмента считается удачей.</summary>
    /// <param name="registry">Реестр инструментов.</param>
    [Obsolete(ObsoleteText)]
    public ToolRegistryToolSource(ToolRegistry registry)
        : this(registry, ToolTextResult.Success)
    {
    }

    /// <summary>Создаёт источник поверх реестра с явным правилом для строкового результата.</summary>
    /// <param name="registry">Реестр инструментов.</param>
    /// <param name="textResults">Как понимать строку, которую вернул метод-инструмент.</param>
    /// <exception cref="ArgumentOutOfRangeException">Правило не выбрано (<see cref="ToolTextResult.Unspecified"/>).</exception>
    /// <exception cref="ArgumentException">При <see cref="ToolTextResult.Refused"/> в реестре есть инструмент, который
    /// возвращает не итог с признаком успеха.</exception>
    public ToolRegistryToolSource(ToolRegistry registry, ToolTextResult textResults)
        : this(registry, textResults, alsoTyped: null, interpret: null)
    {
    }

    /// <summary>
    /// Источник для адаптера со своим типом итога: <paramref name="alsoTyped"/> признает тип итогом с признаком
    /// успеха, <paramref name="interpret"/> переводит такое значение в итог цикла (<c>null</c> — значение не его,
    /// действует обычное правило).
    /// </summary>
    internal ToolRegistryToolSource(
        ToolRegistry registry, ToolTextResult textResults, Func<Type, bool> alsoTyped, Func<object, ReActToolOutcome> interpret)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        if (textResults is not (ToolTextResult.Success or ToolTextResult.Refused))
            throw new ArgumentOutOfRangeException(nameof(textResults), textResults, "Правило для строкового результата не выбрано.");

        _textResults = textResults;
        _alsoTyped = alsoTyped;
        _interpret = interpret;
        EnsureTyped(nameof(registry));
    }

    /// <summary>Создаёт источник из объектов с методами-инструментами; строка инструмента считается удачей.</summary>
    /// <param name="toolInstances">Объекты с методами <see cref="AgentToolAttribute"/>.</param>
    [Obsolete(ObsoleteText)]
    public static ToolRegistryToolSource FromObjects(params object[] toolInstances) =>
        new(ToolRegistry.FromObjects(toolInstances), ToolTextResult.Success);

    /// <summary>Создаёт источник из объектов с методами-инструментами с явным правилом для строкового результата.</summary>
    /// <param name="textResults">Как понимать строку, которую вернул метод-инструмент.</param>
    /// <param name="toolInstances">Объекты с методами <see cref="AgentToolAttribute"/>.</param>
    /// <returns>Источник.</returns>
    public static ToolRegistryToolSource FromObjects(ToolTextResult textResults, params object[] toolInstances) =>
        new(ToolRegistry.FromObjects(toolInstances), textResults);

    /// <summary>
    /// При <see cref="ToolTextResult.Refused"/> инструмент со строковым или непонятным результатом — ошибка
    /// конфигурации: удача по строке выглядела бы так же, как отказ.
    /// </summary>
    private void EnsureTyped(string parameter)
    {
        if (_textResults != ToolTextResult.Refused)
            return;

        IReadOnlyList<string> untyped = _registry.UntypedResultTools(_alsoTyped);
        if (untyped.Count > 0)
        {
            throw new ArgumentException(
                $"Правило ToolTextResult.Refused: инструменты {string.Join(", ", untyped)} возвращают не итог с " +
                "признаком успеха (строку, object или другой тип). Верните ToolResult (Success/Failure) или ничего.",
                parameter);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Набор читается из реестра на КАЖДОМ прогоне, а не снимается один раз при сборке. Реестр
    /// потокобезопасен и допускает регистрацию во время работы (имя инструмента может приходить
    /// из данных), а исполнение и без того резолвит инструмент по имени в момент вызова: снимок
    /// означал бы, что зарегистрированный позже инструмент исполнить можно, а увидеть его в
    /// списке модель не может.
    /// </remarks>
    public IEnumerable<IReActTool> GetTools(ReActRunContext context)
    {
        // Реестр мог пополниться после создания источника: правило проверяется и здесь.
        EnsureTyped(nameof(context));

        var tools = new List<IReActTool>(_registry.Count);
        foreach (ToolDefinition definition in _registry.GetDefinitions())
        {
            if (definition?.Function?.Name is not { Length: > 0 } name)
                continue;

            tools.Add(new RegistryTool(_registry, definition, name, _interpret));
        }

        return tools;
    }

    /// <summary>Один инструмент реестра, приведённый к контракту цикла.</summary>
    private sealed class RegistryTool : IReActTool
    {
        private readonly ToolRegistry _registry;
        private readonly string _soleParameterName;
        private readonly Func<object, ReActToolOutcome> _interpret;

        public RegistryTool(ToolRegistry registry, ToolDefinition definition, string name, Func<object, ReActToolOutcome> interpret)
        {
            _registry = registry;
            _interpret = interpret;
            Name = name;
            Description = definition.Function.Description ?? string.Empty;
            ParametersJsonSchema = definition.Function.Parameters?.GetRawText();
            _soleParameterName = FindSoleParameter(definition.Function.Parameters);
        }

        public string Name { get; }

        public string Description { get; }

        public string ParametersJsonSchema { get; }

        public IReadOnlyCollection<string> Tags => [];

        public async IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
            ReActToolInvocation invocation, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var call = new ToolCall
            {
                Id = invocation.ActionId,
                Function = new FunctionCall
                {
                    Name = Name,
                    Arguments = NormalizeArguments(invocation.Arguments),
                },
            };

            ToolExecutionResult result = await _registry
                .ExecuteAsync(call, cancellationToken)
                .ConfigureAwait(false);

            ReActToolOutcome own = result.Value is { } value ? _interpret?.Invoke(value) : null;
            yield return new ReActToolEvent.Result(own ?? (result.IsSuccess
                ? new ReActToolOutcome { Ok = true, Observation = result.Content, Images = result.Images ?? [] }
                : ReActToolOutcome.Failure(result.Content)));
        }

        /// <summary>
        /// Реестр ждёт аргументы JSON-объектом, а текстовый протокол решений присылает простую
        /// строку. Если у инструмента ровно один параметр, строку заворачиваем в него — иначе
        /// один и тот же инструмент работал бы только с одним способом принятия решений.
        /// </summary>
        private string NormalizeArguments(string arguments)
        {
            string text = (arguments ?? string.Empty).Trim();
            if (text.Length == 0)
                return "{}";

            if (text.StartsWith('{'))
                return text;

            if (_soleParameterName == null)
                return "{}";

            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString(_soleParameterName, text);
                writer.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>Имя единственного параметра схемы; <c>null</c>, если параметров не один.</summary>
        private static string FindSoleParameter(JsonElement? parameters)
        {
            if (parameters is not { } schema
                || schema.ValueKind != JsonValueKind.Object
                || !schema.TryGetProperty("properties", out JsonElement properties)
                || properties.ValueKind != JsonValueKind.Object)
                return null;

            string sole = null;
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                if (sole != null)
                    return null;

                sole = property.Name;
            }

            return sole;
        }
    }
}

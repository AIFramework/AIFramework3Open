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
    private readonly List<IReActTool> _tools;

    /// <summary>Создаёт источник поверх реестра.</summary>
    /// <param name="registry">Реестр инструментов.</param>
    public ToolRegistryToolSource(ToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _tools = [];
        foreach (ToolDefinition definition in registry.GetDefinitions())
        {
            if (definition?.Function?.Name is not { Length: > 0 } name)
                continue;

            _tools.Add(new RegistryTool(registry, definition, name));
        }
    }

    /// <summary>Создаёт источник из объектов с методами-инструментами.</summary>
    /// <param name="toolInstances">Объекты с методами <see cref="AgentToolAttribute"/>.</param>
    public static ToolRegistryToolSource FromObjects(params object[] toolInstances) =>
        new(ToolRegistry.FromObjects(toolInstances));

    /// <inheritdoc />
    public IEnumerable<IReActTool> GetTools(ReActRunContext context) => _tools;

    /// <summary>Один инструмент реестра, приведённый к контракту цикла.</summary>
    private sealed class RegistryTool : IReActTool
    {
        private readonly ToolRegistry _registry;
        private readonly string _soleParameterName;

        public RegistryTool(ToolRegistry registry, ToolDefinition definition, string name)
        {
            _registry = registry;
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

            yield return new ReActToolEvent.Result(result.IsSuccess
                ? new ReActToolOutcome { Ok = true, Observation = result.Content, Images = result.Images ?? [] }
                : ReActToolOutcome.Failure(result.Content));
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

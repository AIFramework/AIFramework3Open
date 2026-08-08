using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AI.LLM.Agents.Multimodal;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.ToolCalling;
using Microsoft.SemanticKernel;
using Serilog;

namespace AI.LLM.Agents.Tools;

/// <summary>
/// Потокобезопасный реестр инструментов агента.
/// Сканирует методы с <see cref="AgentToolAttribute"/>, строит JSON Schema
/// и предоставляет выполнение по <see cref="ToolCall"/>.
/// Единый источник истины для агента, MCP-сервера и SK-интеграции.
/// </summary>
public sealed class ToolRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Количество зарегистрированных инструментов.</summary>
    public int Count => _tools.Count;

    /// <summary>Имена всех зарегистрированных инструментов.</summary>
    public IReadOnlyCollection<string> ToolNames => [.. _tools.Keys];

    /// <summary>Создаёт реестр из экземпляров с методами <see cref="AgentToolAttribute"/>.</summary>
    public static ToolRegistry FromObjects(params object[] toolInstances)
    {
        var registry = new ToolRegistry();
        foreach (var instance in toolInstances)
            registry.Register(instance);
        return registry;
    }

    /// <summary>Регистрирует все методы с <see cref="AgentToolAttribute"/> из экземпляра.</summary>
    public void Register(object toolInstance)
    {
        ArgumentNullException.ThrowIfNull(toolInstance);

        foreach (var (name, tool) in ScanMethods(toolInstance))
            _tools[name] = tool;
    }

    /// <summary>
    /// Регистрирует инструмент под именем, известным только в рантайме.
    /// </summary>
    /// <param name="name">Имя инструмента (function calling). Повторное имя перезаписывает прежнее.</param>
    /// <param name="description">Описание для LLM.</param>
    /// <param name="handler">Делегат-исполнитель; его сигнатура задаёт схему параметров.</param>
    /// <param name="parametersJson">Явная JSON Schema параметров. <c>null</c> — вывести из сигнатуры.</param>
    /// <remarks>
    /// <see cref="AgentToolAttribute"/> — статическая метаданная МЕТОДА: имя одно на весь тип и на всех
    /// его наследников, а <see cref="Register(object)"/> перезаписывает по имени. Поэтому список из N
    /// однотипных носителей через атрибутный путь схлопывается в ОДИН инструмент. Этот путь нужен там,
    /// где имя приходит из данных (агент каталога, пользовательская сборка), а не из кода.
    /// </remarks>
    public void Register(string name, string description, Delegate handler, string parametersJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        // Делегат несёт ровно то, из чего состоит запись реестра: MethodInfo и получателя
        // (для замыкания — экземпляр display-класса). Поэтому исполнение и разбор аргументов
        // работают дальше без единой правки.
        var definition = parametersJson is null
            ? BuildToolDefinition(name, description ?? "", handler.Method)
            : ToolDefinition.Create(name, description ?? "", parametersJson);

        _tools[name] = new RegisteredTool(name, definition, handler.Method, handler.Target);
    }

    /// <summary>
    /// Объявляет ли экземпляр хотя бы один метод с <see cref="AgentToolAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Регистрация принимает <see cref="object"/> — контракта у инструментов нет, только атрибут.
    /// Поэтому чужой экземпляр (опечатка, забытый атрибут, не тот объект) молча даёт пустой реестр,
    /// а агент остаётся вовсе без инструментов. Проверка позволяет поймать это на входе, а не
    /// по факту тихой деградации.
    /// </remarks>
    public static bool DeclaresTools(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return ScanMethods(instance).Any();
    }

    /// <summary>Возвращает <see cref="ToolDefinition"/> для передачи в LLM (function calling).</summary>
    public List<ToolDefinition> GetDefinitions()
        => _tools.Values.Select(t => t.Definition).ToList();

    /// <summary>
    /// Конвертирует зарегистрированные инструменты в SK <see cref="KernelPlugin"/>
    /// для использования в Semantic Kernel (Auto Function Invocation, Planners).
    /// </summary>
    public KernelPlugin ToKernelPlugin(string pluginName = "Tools")
    {
        var functions = new List<KernelFunction>();

        foreach (var tool in _tools.Values)
        {
            var func = tool.Method.IsStatic
                ? KernelFunctionFactory.CreateFromMethod(tool.Method, functionName: tool.Name, description: tool.Definition.Function.Description)
                : KernelFunctionFactory.CreateFromMethod(tool.Method, tool.Target, functionName: tool.Name, description: tool.Definition.Function.Description);

            functions.Add(func);
        }

        return KernelPluginFactory.CreateFromFunctions(pluginName, functions: functions);
    }

    #region Выполнение

    /// <summary>Выполняет один инструмент. CancellationToken пробрасывается в метод.</summary>
    public async Task<ToolExecutionResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        var funcName = toolCall.Function?.Name;
        if (string.IsNullOrEmpty(funcName) || !_tools.TryGetValue(funcName, out var registered))
        {
            return new ToolExecutionResult(
                toolCall.Id, funcName ?? "unknown",
                $"Инструмент '{funcName}' не найден в реестре.",
                isSuccess: false, TimeSpan.Zero);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var args = DeserializeArguments(registered.Method, toolCall.Function.Arguments, cancellationToken);
            var rawResult = registered.Method.Invoke(
                registered.Method.IsStatic ? null : registered.Target, args);

            var (content, images, success) = await UnwrapResultAsync(rawResult).ConfigureAwait(false);

            sw.Stop();
            // Инструмент мог сообщить об отказе без исключения — тогда это не успех.
            return new ToolExecutionResult(toolCall.Id, funcName, content, success, sw.Elapsed, images);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
            Log.Error(inner, "ToolRegistry: ошибка выполнения {ToolName}", funcName);
            return new ToolExecutionResult(toolCall.Id, funcName, $"Ошибка: {inner.Message}", false, sw.Elapsed);
        }
    }

    /// <summary>Параллельное выполнение нескольких инструментов.</summary>
    public async Task<List<ToolExecutionResult>> ExecuteParallelAsync(
        IEnumerable<ToolCall> toolCalls, CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(
            toolCalls.Select(tc => ExecuteAsync(tc, cancellationToken))
        ).ConfigureAwait(false);
        return [.. results];
    }

    /// <summary>
    /// Преобразует результаты в сообщения для LLM.
    /// Текстовые результаты -> <c>role=tool</c>. Если есть изображения —
    /// дополнительное <c>role=user</c> сообщение с <see cref="MessageContent"/> (OpenAI API
    /// не поддерживает изображения в role=tool).
    /// </summary>
    public static List<LLMMessage> ToToolMessages(IEnumerable<ToolExecutionResult> results)
    {
        var messages = new List<LLMMessage>();
        foreach (var r in results)
        {
            messages.Add(LLMMessage.CreateToolResult(r.ToolCallId, r.Content));

            if (!r.HasImages) continue;

            var mc = new MessageContent($"[Результат инструмента \"{r.ToolName}\" содержит изображения]");
            foreach (var img in r.Images)
                mc.AddImage(img.Data);
            messages.Add(new LLMMessage(LLMMessage.UserRole, mc));
        }
        return messages;
    }

    /// <summary>
    /// Преобразует результаты в сообщения для моделей БЕЗ нативного function calling: один
    /// <c>role=user</c> с текстом результатов и приложенными изображениями.
    /// </summary>
    /// <remarks>
    /// Ответ <c>role=tool</c> обязан ссылаться на <c>tool_call_id</c> из ответа модели. Когда
    /// вызовы разобраны из текста, у провайдера таких идентификаторов нет — он их не выдавал, —
    /// и переписка с ответом на несуществующий вызов отвергается целиком. Поэтому результат
    /// возвращается тем же способом, каким был запрошен: текстом.
    /// </remarks>
    public static List<LLMMessage> ToPromptResultMessages(IEnumerable<ToolExecutionResult> results)
    {
        var sb = new System.Text.StringBuilder();
        var images = new List<AgentImage>();

        foreach (var r in results)
        {
            sb.Append("### Результат инструмента \"").Append(r.ToolName).Append('"');
            if (!r.IsSuccess) sb.Append(" (ошибка)");
            sb.AppendLine();
            sb.AppendLine(r.Content);
            sb.AppendLine();

            if (r.HasImages)
                images.AddRange(r.Images);
        }

        if (sb.Length == 0)
            return [];

        var text = sb.ToString().TrimEnd();

        if (images.Count == 0)
            return [LLMMessage.CreateMessage(Roles.User, text)];

        var content = new MessageContent(text);
        foreach (var img in images)
            content.AddImage(img.Data);

        return [new LLMMessage(LLMMessage.UserRole, content)];
    }

    #endregion

    #region Сканирование атрибутов (единая точка для Agent, MCP, SK)

    /// <summary>
    /// Сканирует экземпляр и возвращает пары (имя -> RegisteredTool).
    /// Используется внутри реестра и в <see cref="MCP.McpToolBridge"/>.
    /// </summary>
    internal static IEnumerable<(string Name, RegisteredTool Tool)> ScanMethods(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<AgentToolAttribute>();
            if (attr == null) continue;

            var name = attr.Name ?? CamelToSnakeCase(method.Name);
            var definition = BuildToolDefinition(name, attr.Description, method);

            yield return (name, new RegisteredTool(name, definition, method, instance));
        }
    }

    #endregion

    #region JSON Schema

    private static ToolDefinition BuildToolDefinition(string name, string description, MethodInfo method)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in method.GetParameters())
        {
            if (param.ParameterType == typeof(CancellationToken)) continue;

            var paramAttr = param.GetCustomAttribute<ToolParameterAttribute>();
            var prop = new Dictionary<string, object>
            {
                ["type"] = MapClrType(param.ParameterType)
            };

            if (paramAttr is { Description.Length: > 0 })
                prop["description"] = paramAttr.Description;

            if (param.HasDefaultValue && param.DefaultValue != null)
                prop["default"] = param.DefaultValue;

            properties[param.Name!] = prop;

            if (paramAttr?.RequiredExplicit ?? !param.HasDefaultValue)
                required.Add(param.Name!);
        }

        var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0) schema["required"] = required;

        var schemaJson = JsonSerializer.Serialize(schema);
        return ToolDefinition.Create(name, description, schemaJson);
    }

    private static string MapClrType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return "string";
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return "integer";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
        if (t == typeof(bool)) return "boolean";
        if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))) return "array";
        return "string";
    }

    #endregion

    #region Десериализация аргументов

    private static object[] DeserializeArguments(MethodInfo method, string json, CancellationToken ct)
    {
        var parameters = method.GetParameters();
        var args = new object[parameters.Length];

        Dictionary<string, JsonElement> parsed = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json); }
            catch (Exception ex)
            {
                var preview = json.Length > 200 ? json[..200] + "…" : json;
                throw new ArgumentException(
                    $"Невалидный JSON в аргументах инструмента '{method.Name}': {preview}", ex);
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = ct;
                continue;
            }

            if (parsed != null && parsed.TryGetValue(param.Name!, out var element))
                args[i] = DeserializeElement(element, param.ParameterType);
            else if (param.HasDefaultValue)
                args[i] = param.DefaultValue;
            else
                args[i] = param.ParameterType.IsValueType ? Activator.CreateInstance(param.ParameterType) : null;
        }

        return args;
    }

    private static object DeserializeElement(JsonElement element, Type target)
    {
        // Инструмент принимает LLMMessage, а модель по схеме шлёт строку: прямая десериализация
        // строки в объект падает, и аргумент молча становится null — инструмент получал бы
        // пустое сообщение вместо задачи. Для сообщения «аргумент» это и есть его текст.
        if (target == typeof(LLMMessage) && element.ValueKind == JsonValueKind.String)
            return new LLMMessage(LLMMessage.UserRole, element.GetString());

        try { return JsonSerializer.Deserialize(element.GetRawText(), target); }
        catch
        {
            if (target == typeof(string)) return element.ToString();
            return target.IsValueType ? Activator.CreateInstance(target) : null;
        }
    }

    #endregion

    #region Утилиты

    private static string CamelToSnakeCase(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Разворачивает результат метода инструмента: поддерживает string, ToolResult,
    /// Task&lt;string&gt;, Task&lt;ToolResult&gt; и любой Task&lt;T&gt;.
    /// </summary>
    private static async Task<(string Content, IReadOnlyList<AgentImage> Images, bool Success)> UnwrapResultAsync(
        object rawResult)
    {
        switch (rawResult)
        {
            case ToolResult tr:
                return (tr.Text, tr.Images, tr.IsSuccess);

            case Task<ToolResult> taskTr:
                var tr2 = await taskTr.ConfigureAwait(false);
                return (tr2.Text, tr2.Images, tr2.IsSuccess);

            case Task<string> taskStr:
                return (await taskStr.ConfigureAwait(false), [], true);

            case Task task:
            {
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                var innerResult = resultProp?.GetValue(task);
                if (innerResult is ToolResult trInner)
                    return (trInner.Text, trInner.Images, trInner.IsSuccess);
                return (innerResult?.ToString() ?? "OK", [], true);
            }

            case string s:
                return (s, [], true);

            default:
                return (rawResult?.ToString() ?? "OK", [], true);
        }
    }

    #endregion

    internal sealed record RegisteredTool(string Name, ToolDefinition Definition, MethodInfo Method, object Target);
}

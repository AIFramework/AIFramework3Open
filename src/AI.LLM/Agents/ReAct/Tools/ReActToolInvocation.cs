using System.Text.Json;

namespace AI.LLM.Agents.ReAct.Tools;

/// <summary>
/// Вызов инструмента: аргумент вместе с контекстом прогона и терпимыми к формату способами
/// его прочитать.
/// <para>
/// Аргумент приходит либо JSON-объектом (нативный function calling, схема параметров), либо
/// простой строкой (текстовый протокол решений). Инструмент не должен знать, каким образом
/// получено решение, поэтому разбор живёт здесь, а не в каждом инструменте по-своему.
/// </para>
/// </summary>
public sealed class ReActToolInvocation
{
    private readonly JsonElement _root;
    private readonly bool _isJson;

    /// <summary>Идентификатор вызова.</summary>
    public string ActionId { get; }

    /// <summary>Имя инструмента.</summary>
    public string ToolName { get; }

    /// <summary>Сырой аргумент. Никогда не <c>null</c>; может быть пустым.</summary>
    public string Arguments { get; }

    /// <summary>Контекст прогона.</summary>
    public ReActRunContext Run { get; }

    /// <summary>Аргумент является JSON-объектом.</summary>
    public bool IsJson => _isJson;

    /// <summary>Создаёт вызов.</summary>
    /// <param name="action">Действие, породившее вызов.</param>
    /// <param name="run">Контекст прогона.</param>
    public ReActToolInvocation(ReActAction action, ReActRunContext run)
    {
        ArgumentNullException.ThrowIfNull(action);

        ActionId = action.Id;
        ToolName = action.ToolName;
        Arguments = action.Arguments ?? string.Empty;
        Run = run ?? new ReActRunContext(string.Empty);

        string trimmed = Arguments.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(Arguments);
                _root = document.RootElement.Clone();
                _isJson = _root.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                _isJson = false;
            }
        }
    }

    /// <summary>
    /// Читает строковое поле аргумента. Если аргумент не JSON, возвращает его целиком —
    /// инструменту с единственным осмысленным параметром это ровно то, что нужно.
    /// </summary>
    /// <param name="propertyName">Имя поля.</param>
    /// <param name="fallback">Что вернуть, если поля нет; по умолчанию — весь аргумент.</param>
    public string GetString(string propertyName, string fallback = null)
    {
        if (!_isJson)
            return Arguments;

        if (_root.TryGetProperty(propertyName, out JsonElement value))
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString();
                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    return value.GetRawText();
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.ToString();
            }
        }

        return fallback ?? Arguments;
    }

    /// <summary>
    /// Читает первое непустое строковое поле из перечисленных. Нужно из-за того, что модели
    /// называют один и тот же параметр по-разному (<c>query</c>, <c>input</c>, <c>text</c>).
    /// </summary>
    /// <param name="propertyNames">Имена полей в порядке предпочтения.</param>
    public string GetFirstString(params string[] propertyNames)
    {
        if (!_isJson || propertyNames == null)
            return Arguments;

        foreach (string name in propertyNames)
        {
            if (_root.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
        }

        return Arguments;
    }

    /// <summary>Разбирает аргумент целиком в объект указанного типа.</summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <returns>Разобранный объект либо значение по умолчанию, если аргумент не JSON.</returns>
    public T Deserialize<T>()
    {
        if (!_isJson)
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(Arguments);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

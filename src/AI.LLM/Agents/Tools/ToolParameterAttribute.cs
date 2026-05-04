namespace AI.LLM.Agents.Tools;

/// <summary>
/// Описывает параметр метода-инструмента для генерации JSON Schema.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ToolParameterAttribute : Attribute
{
    /// <summary>
    /// Описание параметра для LLM.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Обязательный ли параметр. По умолчанию true для параметров без default-значения.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Описывает параметр метода-инструмента для генерации JSON Schema.
    /// </summary>
    /// <param name="description">Описание параметра для LLM.</param>
    public ToolParameterAttribute(string description)
    {
        Description = description;
    }
}

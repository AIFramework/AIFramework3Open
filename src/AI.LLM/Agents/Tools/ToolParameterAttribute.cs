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

    private bool? _required;

    /// <summary>
    /// Обязательный ли параметр. Если не задан явно, обязательность определяется
    /// наличием default-значения у параметра: без default — обязательный, с default — опциональный.
    /// </summary>
    public bool Required { get => _required ?? true; set => _required = value; }

    /// <summary>
    /// Явно ли задано <see cref="Required"/>. null — пользователь не указывал значение.
    /// </summary>
    internal bool? RequiredExplicit => _required;

    /// <summary>
    /// Описывает параметр метода-инструмента для генерации JSON Schema.
    /// </summary>
    /// <param name="description">Описание параметра для LLM.</param>
    public ToolParameterAttribute(string description)
    {
        Description = description;
    }
}

namespace AI.LLM.Agents.Tools;

/// <summary>
/// Помечает метод как инструмент, доступный агенту и MCP-серверу.
/// При сканировании <see cref="ToolRegistry"/> метод автоматически превращается
/// в <see cref="AI.LLM.Core.Models.Common.ToolCalling.ToolDefinition"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentToolAttribute : Attribute
{
    /// <summary>
    /// Имя инструмента (используется в function calling). Если null — берётся имя метода.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Описание инструмента для LLM.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Помечает метод как инструмент агента.
    /// </summary>
    /// <param name="name">Имя инструмента (snake_case). Если null — имя метода.</param>
    /// <param name="description">Описание для LLM.</param>
    public AgentToolAttribute(string name = null, string description = null)
    {
        Name = name;
        Description = description ?? string.Empty;
    }
}

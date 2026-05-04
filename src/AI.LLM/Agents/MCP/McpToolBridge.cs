using AI.LLM.Agents.Tools;
using ModelContextProtocol.Server;

namespace AI.LLM.Agents.MCP;

/// <summary>
/// Мост между <see cref="ToolRegistry"/> (атрибуты <see cref="AgentToolAttribute"/>)
/// и <see cref="McpServerTool"/> из ModelContextProtocol SDK.
/// Делегирует сканирование атрибутов в <see cref="ToolRegistry.ScanMethods"/> —
/// единая точка рефлексии для Agent, MCP и SK.
/// </summary>
public static class McpToolBridge
{
    /// <summary>
    /// Создаёт список <see cref="McpServerTool"/> из экземпляров с <see cref="AgentToolAttribute"/>.
    /// </summary>
    public static IReadOnlyList<McpServerTool> CreateMcpTools(params object[] toolInstances)
    {
        ArgumentNullException.ThrowIfNull(toolInstances);

        var tools = new List<McpServerTool>();

        foreach (var instance in toolInstances)
        {
            foreach (var (name, registered) in ToolRegistry.ScanMethods(instance))
            {
                var options = new McpServerToolCreateOptions
                {
                    Name = name,
                    Description = registered.Definition.Function.Description
                };

                var mcpTool = registered.Method.IsStatic
                    ? McpServerTool.Create(registered.Method, options: options)
                    : McpServerTool.Create(registered.Method, registered.Target, options);

                tools.Add(mcpTool);
            }
        }

        return tools;
    }
}

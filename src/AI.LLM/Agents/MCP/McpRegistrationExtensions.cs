using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace AI.LLM.Agents.MCP;

/// <summary>
/// Расширения для регистрации инструментов AIFramework в MCP-сервере.
/// </summary>
public static class McpRegistrationExtensions
{
    /// <summary>
    /// Регистрирует инструменты AIFramework (помеченные <see cref="Tools.AgentToolAttribute"/>)
    /// в MCP-сервере как <see cref="McpServerTool"/>.
    /// </summary>
    /// <param name="builder">Построитель MCP-сервера.</param>
    /// <param name="toolInstances">Экземпляры классов с <see cref="Tools.AgentToolAttribute"/>.</param>
    /// <returns>Построитель MCP-сервера для цепочки вызовов.</returns>
    public static IMcpServerBuilder AddAIFrameworkTools(
        this IMcpServerBuilder builder, params object[] toolInstances)
    {
        var mcpTools = McpToolBridge.CreateMcpTools(toolInstances);
        return builder.WithTools(mcpTools);
    }
}

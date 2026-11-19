using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Источник старых инструментов, который на каждый запрос создает новые экземпляры (как ToolRegistryToolSource).
/// </summary>
internal sealed class FreshSource(params string[] names) : IReActToolSource
{
    /// <summary>Контексты, с которыми спрашивали инструменты.</summary>
    public List<ReActRunContext> Contexts { get; } = [];

    /// <summary>Вызовы инструментов источника.</summary>
    public List<ReActToolInvocation> Invocations { get; } = [];

    /// <inheritdoc />
    public IEnumerable<IReActTool> GetTools(ReActRunContext context)
    {
        Contexts.Add(context);
        return [.. names.Select(name => DelegateReActTool.FromOutcome(
            name,
            "инструмент " + name,
            (invocation, _) =>
            {
                Invocations.Add(invocation);
                return Task.FromResult(ReActToolOutcome.Success(name + ":" + invocation.Arguments));
            }))];
    }
}

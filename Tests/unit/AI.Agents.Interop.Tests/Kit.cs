using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>Общие помощники тестов.</summary>
internal static class Kit
{
    /// <summary>Схема с одним строковым свойством <c>q</c>.</summary>
    public const string QuerySchema =
        "{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\"}},\"required\":[\"q\"]}";

    /// <summary>Контекст вызова контракта.</summary>
    public static ToolContext Context(int tier = 3) =>
        new() { RunId = "run-42", Path = new StepPath { Tier = tier, Branch = "b1" } };

    /// <summary>JSON из текста.</summary>
    public static JsonElement Json(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Спецификация инструмента контракта.</summary>
    public static ToolSpec Spec(string name, ToolAction action = ToolAction.Compute, string schema = QuerySchema) =>
        new() { Name = name, Description = "описание " + name, Parameters = Json(schema), Action = action };

    /// <summary>Читает поток до конца.</summary>
    public static async Task<List<T>> ReadAll<T>(IAsyncEnumerable<T> stream)
    {
        var items = new List<T>();
        await foreach (T item in stream)
            items.Add(item);
        return items;
    }

    /// <summary>Вызывает старый инструмент и возвращает кадры.</summary>
    public static Task<List<ReActToolEvent>> Execute(IReActTool tool, string arguments, ReActRunContext? run = null) =>
        ReadAll(tool.ExecuteAsync(new ReActToolInvocation(new ReActAction(tool.Name, arguments, "call-1"), run)));

    /// <summary>Вызывает инструмент контракта и возвращает кадры.</summary>
    public static Task<List<ToolUpdate>> Call(ITool tool, string arguments, ToolContext? context = null) =>
        ReadAll(tool.CallAsync(
            new ToolCall { Id = "call-1", Name = tool.Spec.Name, Arguments = Json(arguments) },
            context ?? Context()));

    /// <summary>Итог из кадров старого инструмента.</summary>
    public static ReActToolOutcome Outcome(IEnumerable<ReActToolEvent> events) =>
        Assert.Single(events.OfType<ReActToolEvent.Result>()).Value;

    /// <summary>Итог из кадров инструмента контракта.</summary>
    public static ToolResult Result(IEnumerable<ToolUpdate> updates) =>
        Assert.Single(updates.OfType<ToolUpdate.Final>()).Result;
}

using System.Text.Json;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Связь с закрытой логикой NativeToolCallPolicy через отражение: простая схема и разворачивание аргумента.
/// Падение здесь значит, что метод в AI.LLM переименовали или изменили, — поправьте NativePolicyBridge.
/// </summary>
public sealed class NativePolicyBridgeTests
{
    [Fact]
    public void ReActTool_WithoutSchema_GetsPlainInputSchemaOfNativePolicy()
    {
        IReActTool legacy = DelegateReActTool.FromText("plain", "строка запроса", (s, _) => Task.FromResult(s));

        ToolSpec spec = legacy.AsTool(ToolAction.Compute).Spec;

        JsonElement input = spec.Parameters.GetProperty("properties").GetProperty("input");
        Assert.Equal("string", input.GetProperty("type").GetString());
        Assert.Equal("строка запроса", input.GetProperty("description").GetString());
        Assert.Equal("input", Assert.Single(spec.Parameters.GetProperty("required").EnumerateArray()).GetString());
        Assert.Equal("plain", spec.Name);
        Assert.Equal("строка запроса", spec.Description);
        Assert.Null(spec.Group);
        Assert.Null(spec.About);
    }

    [Fact]
    public async Task ReActTool_WithoutSchema_GetsUnwrappedString_WithSchema_GetsJson()
    {
        string? plain = null;
        string? json = null;
        IReActTool withoutSchema = DelegateReActTool.FromText("plain", "строка", (s, _) => Task.FromResult(plain = s));
        IReActTool withSchema = DelegateReActTool.FromText("typed", "объект", (s, _) => Task.FromResult(json = s), Kit.QuerySchema);

        await Kit.Call(withoutSchema.AsTool(ToolAction.Compute), "{\"input\":\"кот\"}");
        await Kit.Call(withSchema.AsTool(ToolAction.Compute), "{\"q\":\"кот\"}");

        Assert.Equal("кот", plain);
        Assert.Equal("{\"q\":\"кот\"}", json);
    }
}

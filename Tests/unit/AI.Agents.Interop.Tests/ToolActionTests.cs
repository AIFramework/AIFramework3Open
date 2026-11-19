using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Вид действия старого инструмента: Unknown и одно предупреждение без явного вида, явный вид — как есть,
/// Read сам не появляется.
/// </summary>
public sealed class ToolActionTests
{
    private const string UnknownActionWarning = "не знает вида действия";

    [Fact]
    public void AsTool_WithoutAction_IsUnknownWithOneWarningPerTool()
    {
        var log = new CapturingLog();
        IReActTool first = Legacy("first_tool");
        IReActTool second = Legacy("second_tool");

        ITool a = first.AsTool(log: log.Logger);
        ITool b = first.AsTool(log: log.Logger);
        ITool c = second.AsTool(log: log.Logger);

        Assert.Equal(ToolAction.Unknown, a.Spec.Action);
        Assert.Equal(ToolAction.Unknown, b.Spec.Action);
        Assert.Equal(ToolAction.Unknown, c.Spec.Action);
        Assert.Equal(ToolAction.Irreversible, a.Spec.Action.Effective());
        Assert.Equal(1, log.Warnings.Count(w => w.Contains(UnknownActionWarning) && w.Contains("first_tool")));
        Assert.Equal(1, log.Warnings.Count(w => w.Contains(UnknownActionWarning) && w.Contains("second_tool")));
    }

    [Theory]
    [InlineData(ToolAction.Read)]
    [InlineData(ToolAction.Compute)]
    [InlineData(ToolAction.Reversible)]
    [InlineData(ToolAction.Irreversible)]
    public void AsTool_WithAction_UsesItWithoutWarning(ToolAction action)
    {
        var log = new CapturingLog();

        ITool tool = Legacy("explicit_tool").AsTool(action, log.Logger);

        Assert.Equal(action, tool.Spec.Action);
        Assert.Equal(action, tool.Classify(new ToolCall { Id = "1", Name = "explicit_tool", Arguments = Kit.Json("{}") }));
        Assert.DoesNotContain(log.Warnings, w => w.Contains(UnknownActionWarning));
    }

    [Fact]
    public void ReadNeverAppearsByItself()
    {
        IReActTool[] tools =
        [
            Legacy("no_schema"),
            DelegateReActTool.FromText("with_schema", "читает", (s, _) => Task.FromResult(s), Kit.QuerySchema, ["read", "web"]),
            DelegateReActTool.FromText("search", "поиск в сети, только чтение", (s, _) => Task.FromResult(s)),
        ];

        foreach (IReActTool tool in tools)
            Assert.NotEqual(ToolAction.Read, tool.AsTool(log: new CapturingLog().Logger).Spec.Action);

        IToolSet set = new FreshSource("read_file", "get_page").AsToolSet(log: new CapturingLog().Logger);
        Assert.All(set.Available, spec => Assert.Equal(ToolAction.Unknown, spec.Action));
    }

    [Fact]
    public void AsToolSet_FreshInstancesEachTime_WarnsOncePerName()
    {
        var log = new CapturingLog();
        IToolSet set = new FreshSource("alpha", "beta").AsToolSet(log: log.Logger);

        for (int i = 0; i < 3; i++)
            Assert.Equal(2, set.Available.Count);

        Assert.Equal(1, log.Warnings.Count(w => w.Contains(UnknownActionWarning) && w.Contains("alpha")));
        Assert.Equal(1, log.Warnings.Count(w => w.Contains(UnknownActionWarning) && w.Contains("beta")));
    }

    [Fact]
    public void AsToolSet_WithActions_UsesThemAndWarnsOnlyForTheRest()
    {
        var log = new CapturingLog();
        var actions = new Dictionary<string, ToolAction> { ["alpha"] = ToolAction.Read };

        IToolSet set = new FreshSource("alpha", "beta").AsToolSet(actions: actions, log: log.Logger);

        Assert.Equal(ToolAction.Read, set.Available.Single(s => s.Name == "alpha").Action);
        Assert.Equal(ToolAction.Unknown, set.Available.Single(s => s.Name == "beta").Action);
        Assert.DoesNotContain(log.Warnings, w => w.Contains(UnknownActionWarning) && w.Contains("alpha"));
        Assert.Equal(1, log.Warnings.Count(w => w.Contains(UnknownActionWarning) && w.Contains("beta")));
    }

    [Fact]
    public async Task AsReActTool_ClassifiesBeforeCallAndLogsAction()
    {
        var log = new CapturingLog();
        var tool = new FakeTool(Kit.Spec("click", ToolAction.Reversible), new ToolUpdate.Final
        {
            Result = new ToolResult { Status = ToolStatus.Ok, Text = "ok", Origin = new Origin { Kind = OriginKind.Trusted } },
        })
        {
            Classified = ToolAction.Irreversible,
        };

        await Kit.Execute(tool.AsReActTool(log.Logger), "{\"q\":\"submit\"}");

        Assert.Contains(log.All, line => line.Contains("click") && line.Contains("Irreversible"));
    }

    private static IReActTool Legacy(string name) =>
        DelegateReActTool.FromText(name, "инструмент " + name, (s, _) => Task.FromResult(s));
}

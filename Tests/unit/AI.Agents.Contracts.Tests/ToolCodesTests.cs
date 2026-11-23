using System.Reflection;
using System.Text.RegularExpressions;
using AI.Agents.Contracts.Tools;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Один словарь кодов исхода инструмента (сессия С2, механизм М3): все коды в одном месте, без повторов литералом,
/// и правило «отказ до действия» задано данными словаря, а не ветками у потребителей.
/// </summary>
public sealed class ToolCodesTests
{
    /// <summary>Коды отказа до действия: мир не изменился, гейт снимает запись. Смена списка видна в ревью.</summary>
    private static readonly string[] ExpectedNotPerformed =
    [
        "transient", "not_configured", "needs_reconnect", "needs_input", "too_long", "bad_markup", "no_rights", "not_found",
        "unknown_tool",
    ];

    private static IEnumerable<string> Constants() =>
        typeof(ToolCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void EveryConstant_IsInAll_AndAllHasNoExtras()
    {
        Assert.Equal(Constants().Order(), ToolCodes.All.Order());
        Assert.Equal(ToolCodes.All.Count, ToolCodes.All.Distinct().Count());
    }

    [Fact]
    public void Codes_AreSnakeCase()
    {
        Assert.All(ToolCodes.All, code => Assert.Matches("^[a-z][a-z0-9_]*$", code));
    }

    [Fact]
    public void BlockBCodes_AndWrapperCodes_AreAllThere()
    {
        string[] required =
        [
            "transient", "unknown_result", "partial", "not_configured", "needs_input",
            "needs_reconnect", "pending", "too_long", "bad_markup", "no_rights", "not_found",
        ];

        Assert.All(required, code => Assert.Contains(code, ToolCodes.All));
    }

    [Fact]
    public void NotPerformed_IsExactlyTheDeclaredList()
    {
        Assert.Equal(ExpectedNotPerformed.Order(), ToolCodes.All.Where(c => ToolCodes.NotPerformed(c)).Order());
        Assert.False(ToolCodes.NotPerformed(null));
        Assert.False(ToolCodes.NotPerformed("my_tool_code"));
    }

    /// <summary>
    /// Код словаря не пишется литералом вне <c>ToolCodes.cs</c> в контрактах и адаптерах: второе место с тем же словом
    /// разойдется с первым. Потребитель ссылается на константу (<c>ToolCodes.Transient</c>).
    /// </summary>
    [Fact]
    public void Codes_AreNotRepeatedAsLiterals_InAgentAssemblies()
    {
        var literal = new Regex("\"(" + string.Join("|", ToolCodes.All.Select(Regex.Escape)) + ")\"");
        string[] offenders = [.. new[] { "AI.Agents.Contracts", "AI.Agents.Interop" }
            .Select(project => Path.Combine(Repository.Root, "src", project))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Select(Repository.Relative)
            .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light"))
            .Where(path => !path.EndsWith("/Tools/ToolCodes.cs", StringComparison.Ordinal))
            .SelectMany(path => literal.Matches(File.ReadAllText(Path.Combine(Repository.Root, path)))
                .Select(m => path + ": " + m.Value))];

        Assert.True(offenders.Length == 0,
            "Код словаря ToolCodes записан литералом — сошлитесь на константу ToolCodes: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Pending_TravelsInTheSameResult_WithHandle()
    {
        var result = new ToolResult
        {
            Status = ToolStatus.Ok,
            Text = "рендер начат",
            Origin = new Origin { Kind = OriginKind.Trusted },
            ErrorCode = ToolCodes.Pending,
            Handle = "job-1",
        };

        Assert.Equal("job-1", result.Handle);
        Assert.False(ToolCodes.NotPerformed(result.ErrorCode));
    }
}

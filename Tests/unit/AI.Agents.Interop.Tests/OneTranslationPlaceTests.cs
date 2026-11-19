using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Храповик одного места: вне src/AI.Agents.Interop никто не собирает ReActToolOutcome из ToolResult контрактов
/// и наоборот.
/// </summary>
/// <remarks>
/// Признак — файл, который знает и <c>ReActToolOutcome</c>, и пространство контрактов <c>AI.Agents.Contracts</c>:
/// без обоих перевести одно в другое нельзя. Ищется по исходникам <c>src</c> и <c>Tools</c>.
/// </remarks>
public sealed class OneTranslationPlaceTests
{
    [Fact]
    public void OnlyInteropKnowsBothForms()
    {
        string[] offenders = [.. new[] { "src", "Tools" }
            .Select(dir => Path.Combine(Repository.Root, dir))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Select(Repository.Relative)
            .Where(path => !IsBuildOutput(path) && !path.StartsWith("src/AI.Agents.Interop/", StringComparison.Ordinal))
            .Where(path =>
            {
                string text = File.ReadAllText(Path.Combine(Repository.Root, path));
                return text.Contains("ReActToolOutcome", StringComparison.Ordinal)
                       && text.Contains("AI.Agents.Contracts", StringComparison.Ordinal);
            })];

        Assert.True(offenders.Length == 0,
            "Перевод ReActToolOutcome ↔ ToolResult вне src/AI.Agents.Interop — используйте ReActTranslation: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void Interop_IsTheOnePlace()
    {
        string translation = Path.Combine(Repository.Root, "src", "AI.Agents.Interop", "ReAct", "ReActTranslation.cs");
        string text = File.ReadAllText(translation);

        // Иначе храповик выше проверял бы пустоту.
        Assert.Contains("ReActToolOutcome", text);
        Assert.Contains("AI.Agents.Contracts", text);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light");
}

using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Храповик: исходники src/AI.Agents.Interop не вызывают члены других сборок через отражение.
/// </summary>
/// <remarks>
/// Закрытую логику AI.LLM адаптер вызывает напрямую через <c>InternalsVisibleTo</c>, тогда переименование
/// ловит компилятор. Отражение прятало бы такую поломку до первого вызова.
/// </remarks>
public sealed class NoReflectionTests
{
    private static readonly string[] Markers =
        ["System.Reflection", "GetMethod(", "GetField(", "BindingFlags", ".Invoke(", "CreateDelegate"];

    [Fact]
    public void InteropSources_DoNotUseReflection()
    {
        string source = Path.Combine(Repository.Root, "src", "AI.Agents.Interop");

        string[] files = [.. Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Select(Repository.Relative)
            .Where(path => !IsBuildOutput(path))];

        // Иначе храповик проверял бы пустоту.
        Assert.Contains("src/AI.Agents.Interop/ReAct/ReActToolAsTool.cs", files);

        string[] offenders = [.. files
            .SelectMany(path =>
            {
                string text = File.ReadAllText(Path.Combine(Repository.Root, path));
                return Markers.Where(marker => text.Contains(marker, StringComparison.Ordinal))
                    .Select(marker => $"{path}: {marker}");
            })];

        Assert.True(offenders.Length == 0,
            "Отражение в src/AI.Agents.Interop — вызывайте члены AI.LLM напрямую (InternalsVisibleTo): " +
            string.Join(", ", offenders));
    }

    private static bool IsBuildOutput(string path) =>
        path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light");
}

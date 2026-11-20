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

    /// <summary>
    /// Файлы, которым разрешена часть маркеров, с причиной. Исключение ровно одно: чтение пользовательского
    /// атрибута [ToolEffect] с MethodInfo метода-инструмента (решение владельца 20.11). Вызов членов AI.LLM через
    /// отражение и там запрещен: для этого файла снимаются только маркеры чтения метаданных.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string[] Allowed, string Reason)> Exceptions =
        new Dictionary<string, (string[], string)>
        {
            ["src/AI.Agents.Interop/Effects/AttributedTools.cs"] = (
                ["System.Reflection", "BindingFlags"],
                "чтение [ToolEffect] с метода пользователя и поиск [ToolEffect] без [AgentTool]"),
        };

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
                string[] allowed = Exceptions.TryGetValue(path, out var exception) ? exception.Allowed : [];
                return Markers.Where(marker => !allowed.Contains(marker) && text.Contains(marker, StringComparison.Ordinal))
                    .Select(marker => $"{path}: {marker}");
            })];

        Assert.True(offenders.Length == 0,
            "Отражение в src/AI.Agents.Interop — вызывайте члены AI.LLM напрямую (InternalsVisibleTo): " +
            string.Join(", ", offenders));
    }

    /// <summary>Исключение одно и указывает на существующий файл: иначе оно разрешало бы отражение где угодно.</summary>
    [Fact]
    public void Exceptions_AreExactlyOneExistingFile()
    {
        Assert.Single(Exceptions);
        foreach (string path in Exceptions.Keys)
            Assert.True(File.Exists(Path.Combine(Repository.Root, path)), path);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light");
}

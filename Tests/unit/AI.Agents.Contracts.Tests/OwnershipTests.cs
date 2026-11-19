using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Каждый тип контрактов объявлен один раз, а сама сборка зависит только от BCL.
/// </summary>
public sealed partial class OwnershipTests
{
    /// <summary>
    /// Долг: старые типы src и Tools, чьи имена совпадают с именами контрактов. Файл, который видит оба
    /// пространства имен, получает ошибку CS0104, поэтому адаптеры берут псевдонимы using. Переименовать
    /// старые публичные типы можно только в мажорной версии 5.0. Список только сокращается: новое совпадение
    /// роняет тест, погашенная строка требует вычеркивания.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NameDebt = new Dictionary<string, string>
    {
        // AI.LLM.Agents.Multimodal.ToolResult — результат мультимодального цикла (текст и картинки)
        ["ToolResult"] = "src/AI.LLM/Agents/Multimodal/ToolResult.cs",

        // AI.LLM.Core.Models.Common.ToolCalling.ToolCall — вызов инструмента в формате OpenAI Chat Completions
        ["ToolCall"] = "src/AI.LLM/Core/Models/Common/ToolCalling/ToolCall.cs",

        // AiFramework.Tools.Atlas.LlmAnswer — ответ отбора Атласа (утилита, не пакет)
        ["LlmAnswer"] = "Tools/Atlas/LlmSelector.cs",
    };

    private static readonly string[] SkippedDirectories = ["bin", "obj", "bin-light", "obj-light"];

    /// <summary>
    /// Имена верхнеуровневых публичных типов контрактов не объявлены ни в одной другой сборке src и Tools,
    /// кроме записанного долга. Вложенные типы не считаются: к неоднозначности CS0104 они не ведут.
    /// </summary>
    [Fact]
    public void ContractTypeNames_AreNotDeclaredElsewhere_ExceptDebt()
    {
        HashSet<string> names = ContractTopLevelNames().ToHashSet(StringComparer.Ordinal);
        var found = new List<(string Name, string File)>();

        foreach (string file in SourceFiles())
        {
            foreach (string name in TopLevelTypeNames(File.ReadAllText(file)).Where(names.Contains))
                found.Add((name, Repository.Relative(file)));
        }

        var problems = new List<string>();

        foreach ((string name, string file) in found)
        {
            if (!NameDebt.TryGetValue(name, out string? debtFile) || debtFile != file)
                problems.Add($"новое совпадение имени: {name} объявлен в {file}");
        }

        foreach ((string name, string file) in NameDebt)
        {
            if (!found.Contains((name, file)))
                problems.Add($"долг погашен: {name} больше не объявлен в {file} — вычеркните строку из NameDebt");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// Имена контрактов не совпадают с типами src и Tools и без учета регистра: «LLMAnswer» рядом с «LlmAnswer»
    /// путал бы читателя так же, как точный двойник. Похожие, но другие имена допустимы: например,
    /// <c>AI.LLM.LLMStreamChunk</c> (кадр потока клиента AI.LLM) и <c>LlmChunk</c> контрактов — разные слова.
    /// </summary>
    [Fact]
    public void ContractTypeNames_DoNotCollideIgnoringCase()
    {
        var names = ContractTopLevelNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();

        foreach (string file in SourceFiles())
        {
            foreach (string name in TopLevelTypeNames(File.ReadAllText(file)))
            {
                // Точное совпадение разбирает тест выше вместе с долгом
                if (names.TryGetValue(name, out string? contract) && contract != name)
                    problems.Add($"{name} в {Repository.Relative(file)} отличается от контракта {contract} только регистром");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Внутри самой сборки контрактов каждое имя верхнего уровня встречается один раз.</summary>
    [Fact]
    public void ContractTypeNames_AreUniqueAcrossNamespaces()
    {
        string[] duplicates = ContractTopLevelNames()
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }

    /// <summary>Проект контрактов не подключает ни пакетов, ни проектов, ни дополнительных фреймворков.</summary>
    [Fact]
    public void ContractsProject_HasNoPackageOrProjectReferences()
    {
        string project = Path.Combine(Repository.Root, "src", "AI.Agents.Contracts", "AI.Agents.Contracts.csproj");
        XDocument document = XDocument.Load(project);

        string[] references = document.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference" or "FrameworkReference" or "Reference")
            .Select(element => $"{element.Name.LocalName} {element.Attribute("Include")?.Value}")
            .ToArray();

        Assert.Empty(references);
    }

    /// <summary>Скомпилированная сборка ссылается только на сборки BCL.</summary>
    [Fact]
    public void ContractsAssembly_ReferencesOnlyBcl()
    {
        string[] foreign = Repository.Contracts.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "netstandard" && name != "mscorlib" && name != "System"
                           && !name.StartsWith("System.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(foreign);
    }

    private static IEnumerable<string> ContractTopLevelNames() =>
        Repository.Contracts.GetExportedTypes()
            .Where(type => !type.IsNested)
            .Select(type => type.Name.Split('`')[0]);

    private static IEnumerable<string> SourceFiles()
    {
        string contracts = Path.Combine(Repository.Root, "src", "AI.Agents.Contracts") + Path.DirectorySeparatorChar;

        foreach (string root in new[] { "src", "Tools" })
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(Repository.Root, root), "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(contracts, StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = Repository.Relative(file).Split('/');

                if (parts.Any(part => SkippedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase)))
                    continue;

                yield return file;
            }
        }
    }

    /// <summary>
    /// Имена типов верхнего уровня в файле. Верхний уровень определяется отступом: в файле с file-scoped
    /// namespace (и без namespace) — без отступа, в файле с блочным namespace — один уровень (4 пробела или
    /// табуляция). Код репозитория отформатирован единообразно, поэтому отступа достаточно.
    /// </summary>
    private static IEnumerable<string> TopLevelTypeNames(string source)
    {
        bool blockNamespace = !FileScopedNamespace().IsMatch(source) && BlockNamespace().IsMatch(source);

        foreach (Match match in TypeDeclaration().Matches(source))
        {
            string indent = match.Groups["indent"].Value;
            bool topLevel = blockNamespace ? indent is "    " or "\t" : indent.Length == 0;

            if (topLevel)
                yield return match.Groups["name"].Value;
        }
    }

    [GeneratedRegex(@"^\s*namespace\s+[\w.]+\s*;", RegexOptions.Multiline)]
    private static partial Regex FileScopedNamespace();

    [GeneratedRegex(@"^\s*namespace\s+[\w.]+\s*(\{|$)", RegexOptions.Multiline)]
    private static partial Regex BlockNamespace();

    [GeneratedRegex(
        @"^(?<indent>[ \t]*)(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|unsafe|new|file)\s+)*" +
        @"(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.Multiline)]
    private static partial Regex TypeDeclaration();
}

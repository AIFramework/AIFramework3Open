using System.Text.RegularExpressions;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Храповик долга: вызовы <c>ILLMClient.Send*</c> в агентном слое AI.LLM, в реранкере по модели и в AI.Script.Llm
/// записаны в <c>Models/ModelCallDebt.txt</c> и могут только убывать.
/// </summary>
/// <remarks>
/// Новый код агентного слоя ходит к модели через <c>IModelPort</c>; старые места переводятся по одному, и каждое
/// переведенное вычеркивается из файла — иначе храповик не даст долгу вырасти обратно незаметно.
/// </remarks>
public sealed class ModelCallDebtTests
{
    /// <summary>Где ищутся вызовы: агентный слой, реранкер по модели, модуль скрипта.</summary>
    private static readonly string[] Scopes =
    [
        "src/AI.LLM/Agents",
        "src/AI.LLM/Services/Reranking/Llm",
        "src/AI.Script.Llm",
    ];

    /// <summary>Вызов <c>.SendAsync(</c> или <c>.SendFullAsync(</c>, в том числе с точкой на новой строке.</summary>
    private static readonly Regex Call = new(@"\.\s*Send(Full)?Async\s*\(", RegexOptions.Compiled);

    [Fact]
    public void CallsBypassingAdapters_OnlyShrink()
    {
        Dictionary<string, int> recorded = Recorded();
        Dictionary<string, int> actual = Actual();

        string[] grown = [.. actual
            .Where(pair => pair.Value > recorded.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value} (в долге {recorded.GetValueOrDefault(pair.Key)})")];
        string[] shrunk = [.. recorded
            .Where(pair => actual.GetValueOrDefault(pair.Key) < pair.Value)
            .Select(pair => $"{pair.Key}: {actual.GetValueOrDefault(pair.Key)} (в долге {pair.Value})")];

        Assert.True(grown.Length == 0,
            "Новые вызовы ILLMClient.Send* мимо адаптеров — идите к модели через IModelPort: " + string.Join("; ", grown));
        Assert.True(shrunk.Length == 0,
            "Долг уменьшился — вычеркните исправленное в Models/ModelCallDebt.txt: " + string.Join("; ", shrunk));
    }

    [Fact]
    public void DebtFile_ListsExistingFiles() =>
        Assert.All(Recorded().Keys, path => Assert.True(File.Exists(Path.Combine(Repository.Root, path)), path));

    private static Dictionary<string, int> Recorded()
    {
        string file = Path.Combine(Repository.Root, "Tests", "unit", "AI.Agents.Interop.Tests", "Models", "ModelCallDebt.txt");

        return File.ReadAllLines(file)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(':'))
            .ToDictionary(parts => parts[0].Trim(), parts => int.Parse(parts[1].Trim()), StringComparer.Ordinal);
    }

    private static Dictionary<string, int> Actual() =>
        Scopes
            .Select(scope => Path.Combine(Repository.Root, scope))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Select(Repository.Relative)
            .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light"))
            .Select(path => (path, count: Call.Matches(File.ReadAllText(Path.Combine(Repository.Root, path))).Count))
            .Where(entry => entry.count > 0)
            .ToDictionary(entry => entry.path, entry => entry.count, StringComparer.Ordinal);
}

using System.Text.RegularExpressions;
using AI.Agents.Interop.Engines;
using AI.LLM.Agents.ReAct;
using Xunit;

namespace AI.Agents.Interop.Tests.Engines;

/// <summary>
/// Храповики адаптеров движков и ключа эффекта: таблица перевода полна, ключ создается в одном месте,
/// канонизаций аргументов ровно две (эвристика повтора и ключ эффекта).
/// </summary>
public sealed class EngineRatchetTests
{
    [Fact]
    public void EveryStopReason_IsTranslated()
    {
        ReActStopReason[] missing = [.. Enum.GetValues<ReActStopReason>().Where(r => !ReActEngineTranslationTable.StopReasons.ContainsKey(r))];
        Assert.True(missing.Length == 0, "ReActStopReason без записи в ReActEngineTranslationTable.StopReasons: " + string.Join(", ", missing));
    }

    [Fact]
    public void IterationLimit_IsIncomplete()
    {
        Assert.Equal(Contracts.Engines.EngineStatus.Incomplete, ReActEngineTranslationTable.StopReasons[ReActStopReason.IterationLimit].Status);
        Assert.Equal(Contracts.Engines.StopCode.StepLimit, ReActEngineTranslationTable.StopReasons[ReActStopReason.IterationLimit].Code);
    }

    [Fact]
    public void EveryReActEvent_IsInTable()
    {
        string[] events = [.. typeof(ReActEvent).GetNestedTypes().Where(t => t.IsSubclassOf(typeof(ReActEvent))).Select(t => t.Name)];
        string[] missing = [.. events.Where(name => !ReActEngineTranslationTable.Events.ContainsKey(name))];
        string[] stale = [.. ReActEngineTranslationTable.Events.Keys.Where(name => !events.Contains(name))];

        Assert.True(missing.Length == 0, "ReActEvent без записи в таблице: " + string.Join(", ", missing));
        Assert.True(stale.Length == 0, "Запись таблицы без события: " + string.Join(", ", stale));
    }

    /// <summary>
    /// Ключ эффекта строится в одном месте (EffectKeys): только там считается SHA-256, только через EffectKeys.Create
    /// в src назначается ToolCall.EffectKey, и ключ эффекта не опирается на эвристику повтора ReActActionKey.
    /// </summary>
    [Fact]
    public void EffectKey_HasOnePlace()
    {
        const string keys = "src/AI.Agents.Interop/Effects/EffectKeys.cs";

        // Хеш ключа считается в одном месте.
        string[] hashing = [.. SourceFiles().Where(f => f.StartsWith("src/AI.Agents.", StringComparison.Ordinal)
                                                       && File.ReadAllText(Abs(f)).Contains("SHA256", StringComparison.Ordinal))];
        Assert.Equal([keys], hashing);

        // Каждое назначение EffectKey в src берет значение из EffectKeys.Create или из уже назначенного ключа.
        // EffectGate: key = EffectKeys.Create(...) строкой выше; null — текст таблицы перевода.
        var assignment = new Regex(@"EffectKey\s*=\s*(?<value>\S+)", RegexOptions.CultureInvariant);
        string[] foreign = [.. SourceFiles()
            .Where(f => f.StartsWith("src/AI.Agents.", StringComparison.Ordinal))
            .SelectMany(f => assignment.Matches(File.ReadAllText(Abs(f))).Select(m => (File: f, Value: m.Groups["value"].Value)))
            .Where(m => !m.Value.StartsWith("EffectKeys.Create(", StringComparison.Ordinal)
                        && m.Value.TrimEnd(';', ',', ':', '}', ')') is not ("key" or "null"))
            .Select(m => m.File + ": " + m.Value)];
        Assert.True(foreign.Length == 0, "EffectKey назначается не через EffectKeys.Create: " + string.Join("; ", foreign));

        // Эвристика повтора модели остается в движке ReAct и в ключ эффекта не входит.
        string[] users = [.. SourceFiles().Where(f => File.ReadAllText(Abs(f)).Contains("ReActActionKey.Create(", StringComparison.Ordinal))];
        Assert.Equal(
            ["src/AI.LLM/Agents/ReAct/ReActEngine.Loop.cs", "src/AI.LLM/Agents/ReAct/ReActEngine.Tools.cs"],
            users.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Канонизаций аргументов в src ровно две, и это разные понятия: эвристика повтора действия модели
    /// (ReActActionKey, AI.LLM) и точная идентичность эффекта (EffectKeys, AI.Agents.Interop). Признак —
    /// обход свойств объекта (EnumerateObject) и сортировка имен в том же файле.
    /// </summary>
    [Fact]
    public void Canonicalizations_AreExactlyReActActionKeyAndEffectKeys()
    {
        var sortsNames = new Regex(@"\.Sort\(\s*StringComparer|OrderBy\(\s*\w+\s*=>\s*\w+\.Name\W", RegexOptions.CultureInvariant);

        string[] found = [.. SourceFiles()
            .Where(f =>
            {
                string text = File.ReadAllText(Abs(f));
                return text.Contains("EnumerateObject", StringComparison.Ordinal) && sortsNames.IsMatch(text);
            })
            .Order(StringComparer.Ordinal)];

        Assert.True(
            found.SequenceEqual(["src/AI.Agents.Interop/Effects/EffectKeys.cs", "src/AI.LLM/Agents/ReAct/Tools/ReActActionKey.cs"]),
            "Канонизация аргументов JSON (обход свойств и сортировка имен) найдена в: " + string.Join(", ", found) +
            ". Ключ эффекта строится только EffectKeys, эвристика повтора — только ReActActionKey.");
    }

    /// <summary>
    /// Пока Interop опирается на внутренние члены AI.LLM, пакет зависит от AI.LLM точной версией [X]: цель
    /// PinAiLlmExactVersion в csproj переписывает диапазон, который NuGet по умолчанию пишет как «&gt;= X».
    /// </summary>
    [Fact]
    public void Package_PinsAiLlmToExactVersion()
    {
        string project = File.ReadAllText(Abs("src/AI.Agents.Interop/AI.Agents.Interop.csproj"));

        Assert.Contains("<Target Name=\"PinAiLlmExactVersion\" AfterTargets=\"_GetProjectReferenceVersions\">", project, StringComparison.Ordinal);
        Assert.Contains("Condition=\"'%(Filename)' == 'AI.LLM'\"", project, StringComparison.Ordinal);
        Assert.Contains("<ProjectVersion>[%(ProjectVersion)]</ProjectVersion>", project, StringComparison.Ordinal);
    }

    private static string Abs(string relative) => Path.Combine(Repository.Root, relative);

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(Repository.Relative)
            .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light"));
}

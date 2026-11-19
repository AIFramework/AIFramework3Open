using System.Reflection;
using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;
using AgentImage = AI.LLM.Agents.Multimodal.AgentImage;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Храповик перевода: каждое публичное свойство переводимых типов записано в <see cref="ReActTranslationTable"/>
/// ровно в одну таблицу, и в таблицах нет записей о свойствах, которых уже нет.
/// </summary>
public sealed class TranslationRatchetTests
{
    /// <summary>Типы, которые переводят адаптеры, включая вложенные значения и кадры потока.</summary>
    private static readonly Type[] Translated =
    [
        typeof(ReActToolOutcome),
        typeof(ToolResult),
        typeof(ReActCitation),
        typeof(Citation),
        typeof(ToolSpec),
        typeof(IReActTool),
        typeof(ToolFile),
        typeof(AgentImage),
        typeof(ReActToolEvent.Progress),
        typeof(ReActToolEvent.Result),
        typeof(ToolUpdate.Progress),
        typeof(ToolUpdate.Final),
    ];

    [Fact]
    public void EveryPublicProperty_IsInExactlyOneTable()
    {
        string[] missing = [.. Keys().Where(key =>
            !ReActTranslationTable.Translated.ContainsKey(key) && !ReActTranslationTable.NotTranslated.ContainsKey(key))];
        string[] both = [.. ReActTranslationTable.Translated.Keys.Intersect(ReActTranslationTable.NotTranslated.Keys)];

        Assert.True(missing.Length == 0,
            "Свойства без записи в ReActTranslationTable (впишите в Translated — куда, или в NotTranslated — почему): " +
            string.Join(", ", missing));
        Assert.True(both.Length == 0, "Свойства сразу в обеих таблицах: " + string.Join(", ", both));
    }

    [Fact]
    public void Tables_HaveNoStaleEntries()
    {
        var keys = new HashSet<string>(Keys(), StringComparer.Ordinal);

        string[] stale = [.. ReActTranslationTable.Translated.Keys
            .Concat(ReActTranslationTable.NotTranslated.Keys)
            .Where(key => !keys.Contains(key))];

        Assert.True(stale.Length == 0, "Записи о свойствах, которых нет: " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryEntry_HasReason()
    {
        Assert.All(ReActTranslationTable.Translated.Concat(ReActTranslationTable.NotTranslated),
            entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), entry.Key));
    }

    private static IEnumerable<string> Keys() =>
        Translated.SelectMany(type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => Name(type) + "." + property.Name));

    private static string Name(Type type) => type.DeclaringType is { } outer ? Name(outer) + "." + type.Name : type.Name;
}

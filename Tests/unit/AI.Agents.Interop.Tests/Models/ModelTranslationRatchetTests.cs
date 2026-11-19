using System.Reflection;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Models;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using Xunit;
using ContractToolCall = AI.Agents.Contracts.Tools.ToolCall;
using LlmToolCall = AI.LLM.Core.Models.Common.ToolCalling.ToolCall;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Храповик перевода модели: каждое публичное свойство переводимых типов записано в <see cref="ModelTranslationTable"/>
/// ровно в одну таблицу, и в таблицах нет записей о свойствах, которых уже нет.
/// </summary>
public sealed class ModelTranslationRatchetTests
{
    /// <summary>Переводимые типы и их префиксы ключей; у исключения — только свои свойства.</summary>
    private static readonly (Type Type, string Prefix, bool DeclaredOnly)[] Translated =
    [
        (typeof(ModelAsk), "ModelAsk", false),
        (typeof(ModelMessage), "ModelMessage", false),
        (typeof(MessagePart.Text), "MessagePart.Text", false),
        (typeof(MessagePart.Image), "MessagePart.Image", false),
        (typeof(MessagePart.Audio), "MessagePart.Audio", false),
        (typeof(LlmAnswer), "LlmAnswer", false),
        (typeof(ModelUsage), "ModelUsage", false),
        (typeof(LlmChunk.Text), "LlmChunk.Text", false),
        (typeof(LlmChunk.Reasoning), "LlmChunk.Reasoning", false),
        (typeof(LlmChunk.Final), "LlmChunk.Final", false),
        (typeof(ContractToolCall), "ToolCall", false),
        (typeof(ToolSpec), "ToolSpec", false),

        (typeof(LLMMessage), "LLMMessage", false),
        (typeof(GenerateSettings), "GenerateSettings", false),
        (typeof(ChatCompletionsResponse), "ChatCompletionsResponse", false),
        (typeof(Choice), "Choice", false),
        (typeof(Usage), "Usage", false),
        (typeof(LLMStreamChunk), "LLMStreamChunk", false),
        (typeof(LlmToolCall), "LLM.ToolCall", false),
        (typeof(FunctionCall), "FunctionCall", false),
        (typeof(ToolDefinition), "ToolDefinition", false),
        (typeof(FunctionDefinition), "FunctionDefinition", false),
        (typeof(ToolChoice), "ToolChoice", false),
        (typeof(ResponseFormat), "ResponseFormat", false),
        (typeof(JsonSchemaDefinition), "JsonSchemaDefinition", false),
        (typeof(TextContentItem), "TextContentItem", false),
        (typeof(ImageContent), "ImageContent", false),
        (typeof(AudioContent), "AudioContent", false),
        (typeof(InputAudio), "InputAudio", false),
        (typeof(LlmRequestException), "LlmRequestException", true),
    ];

    [Fact]
    public void EveryPublicProperty_IsInExactlyOneTable()
    {
        string[] missing = [.. Keys().Where(key =>
            !ModelTranslationTable.Translated.ContainsKey(key) && !ModelTranslationTable.NotTranslated.ContainsKey(key))];
        string[] both = [.. ModelTranslationTable.Translated.Keys.Intersect(ModelTranslationTable.NotTranslated.Keys)];

        Assert.True(missing.Length == 0,
            "Свойства без записи в ModelTranslationTable (впишите в Translated — куда, или в NotTranslated — почему): " +
            string.Join(", ", missing));
        Assert.True(both.Length == 0, "Свойства сразу в обеих таблицах: " + string.Join(", ", both));
    }

    [Fact]
    public void Tables_HaveNoStaleEntries()
    {
        var keys = new HashSet<string>(Keys(), StringComparer.Ordinal);

        string[] stale = [.. ModelTranslationTable.Translated.Keys
            .Concat(ModelTranslationTable.NotTranslated.Keys)
            .Where(key => !keys.Contains(key))];

        Assert.True(stale.Length == 0, "Записи о свойствах, которых нет: " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryEntry_HasReason() =>
        Assert.All(ModelTranslationTable.Translated.Concat(ModelTranslationTable.NotTranslated),
            entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), entry.Key));

    /// <summary>Ключи потерь, которые методы перевода кладут в список lost, есть в таблице.</summary>
    [Fact]
    public void LostKeys_AreTableKeys()
    {
        string source = File.ReadAllText(Path.Combine(Repository.Root, "src", "AI.Agents.Interop", "Models", "ModelTranslation.cs"));

        string[] keys = [.. System.Text.RegularExpressions.Regex
            .Matches(source, "lost\\??\\.Add\\(\"([^\"]+)\"\\)")
            .Select(m => m.Groups[1].Value)
            .Distinct()];

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.True(
            ModelTranslationTable.Translated.ContainsKey(key) || ModelTranslationTable.NotTranslated.ContainsKey(key), key));
    }

    private static IEnumerable<string> Keys() =>
        Translated.SelectMany(entry => entry.Type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | (entry.DeclaredOnly ? BindingFlags.DeclaredOnly : 0))
            .Select(property => entry.Prefix + "." + property.Name));
}

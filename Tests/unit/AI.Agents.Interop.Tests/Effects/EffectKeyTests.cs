using System.Text.RegularExpressions;
using AI.Agents.Interop.Effects;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Ключ эффекта: детерминирован, не зависит от порядка полей JSON и пробелов между токенами, различает запуск,
/// инструмент и любое отличие в значениях аргументов (строгая каноническая форма, не ReActActionKey).
/// </summary>
public sealed class EffectKeyTests
{
    [Fact]
    public void SameRunToolAndArguments_GiveSameKey_RegardlessOfFieldOrder()
    {
        string a = EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"to":"a@example.org","body":"привет"}"""));
        string b = EffectKeys.Create("run-1", "send_mail", Kit.Json("""{ "body" : "привет",  "to":"a@example.org" }"""));

        Assert.Equal(a, b);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), a);
    }

    [Fact]
    public void DifferentRun_Tool_OrArguments_GiveDifferentKeys()
    {
        string baseKey = EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"to":"a@example.org"}"""));

        Assert.NotEqual(baseKey, EffectKeys.Create("run-2", "send_mail", Kit.Json("""{"to":"a@example.org"}""")));
        Assert.NotEqual(baseKey, EffectKeys.Create("run-1", "send_sms", Kit.Json("""{"to":"a@example.org"}""")));
        Assert.NotEqual(baseKey, EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"to":"b@example.org"}""")));
        Assert.NotEqual(baseKey, EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"to":"a@example.org","cc":"x"}""")));
    }

    [Fact]
    public void RunIdAndAction_DoNotFlowIntoEachOther()
    {
        // Без длины в начале "run-1" + "x…" и "run-1x" + "…" могли бы совпасть.
        Assert.NotEqual(
            EffectKeys.Create("ab", "c", Kit.Json("{}")),
            EffectKeys.Create("a", "bc", Kit.Json("{}")));
    }

    /// <summary>
    /// Ключ — точная идентичность эффекта: регистр и пробелы внутри строкового значения различаются (это не
    /// эвристика повтора ReActActionKey, которая их схлопывает).
    /// </summary>
    [Fact]
    public void CaseOrSpacesInsideStringValue_GiveDifferentKeys()
    {
        string key = EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"body":"Курс евро"}"""));

        Assert.NotEqual(key, EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"body":"курс евро"}""")));
        Assert.NotEqual(key, EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"body":"Курс   евро"}""")));
        Assert.NotEqual(key, EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"body":" Курс евро"}""")));
    }

    [Fact]
    public void ToolName_IsCaseSensitive()
    {
        Assert.NotEqual(
            EffectKeys.Create("run-1", "Search", Kit.Json("{}")),
            EffectKeys.Create("run-1", "search", Kit.Json("{}")));
    }

    [Fact]
    public void NestedFieldOrderAndWhitespaceBetweenTokens_DoNotChangeKey()
    {
        Assert.Equal(
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"order":{"id":7,"lines":[{"sku":"A","n":1},{"sku":"B","n":2}]},"to":"x"}""")),
            EffectKeys.Create("run-1", "pay", Kit.Json("""
                { "to" : "x",
                  "order" : { "lines" : [ { "n" : 1, "sku" : "A" }, { "n" : 2, "sku" : "B" } ], "id" : 7 } }
                """)));
    }

    [Fact]
    public void ArrayOrder_AndNumberText_AreKept()
    {
        Assert.NotEqual(
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"ids":[1,2]}""")),
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"ids":[2,1]}""")));
        Assert.NotEqual(
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"amount":1}""")),
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"amount":1.0}""")));
    }

    [Fact]
    public void EscapedAndPlainString_GiveSameKey()
    {
        // Экранированная последовательность JSON для «é»: обратная косая черта (char 92), затем u00e9.
        string escaped = """{"to":"caf""" + (char)92 + """u00e9"}""";

        Assert.Equal(
            EffectKeys.Create("run-1", "send_mail", Kit.Json(escaped)),
            EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"to":"café"}""")));
    }
}

using System.Text.RegularExpressions;
using AI.Agents.Interop.Effects;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Ключ эффекта: детерминирован, не зависит от порядка полей JSON, пробелов между токенами и записи числа,
/// различает запуск, инструмент и любое отличие в значениях аргументов (строгая каноническая форма, не
/// ReActActionKey).
/// </summary>
public sealed class EffectKeyTests
{
    [Fact]
    public void SameRunToolAndArguments_GiveSameKey_RegardlessOfFieldOrder()
    {
        string a = EffectKeys.Create("run-1", "send_mail", Kit.Json("""{"to":"a@example.org","body":"привет"}"""));
        string b = EffectKeys.Create("run-1", "send_mail", Kit.Json("""{ "body" : "привет",  "to":"a@example.org" }"""));

        Assert.Equal(a, b);
        Assert.Matches(new Regex("^ek1:[0-9a-f]{64}$"), a);
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
    public void ArrayOrder_IsKept_AndNumberNotation_GivesSameKey()
    {
        Assert.NotEqual(
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"ids":[1,2]}""")),
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"ids":[2,1]}""")));
        Assert.Equal(
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"amount":1}""")),
            EffectKeys.Create("run-1", "pay", Kit.Json("""{"amount":1.0}""")));
    }

    /// <summary>Одно значение в разной записи — один ключ.</summary>
    [Theory]
    [InlineData("100", "100.0")]
    [InlineData("100", "1e2")]
    [InlineData("100", "1E+2")]
    [InlineData("100", "0.001e5")]
    [InlineData("100", "10000e-2")]
    [InlineData("0", "0.0")]
    [InlineData("0", "-0")]
    [InlineData("0", "-0.0e-7")]
    [InlineData("0.1", "0.10")]
    [InlineData("0.1", "1e-1")]
    [InlineData("-2.5", "-25E-1")]
    [InlineData("1e007", "1e7")]
    public void SameNumberValue_InDifferentNotation_GivesSameKey(string left, string right)
    {
        Assert.Equal(Amount(left), Amount(right));
    }

    /// <summary>
    /// Разные значения никогда не дают один ключ: ни соседние целые, неразличимые в double, ни числа длиннее
    /// точности decimal, ни знак, ни порядок.
    /// </summary>
    [Theory]
    [InlineData("9007199254740993", "9007199254740992")]
    [InlineData("0.12345678901234567890123456789012345", "0.12345678901234567890123456789012346")]
    [InlineData("123456789012345678901234567890123456789", "123456789012345678901234567890123456788")]
    [InlineData("1e400", "1e401")]
    [InlineData("1", "-1")]
    [InlineData("10", "1")]
    [InlineData("0.1", "0.01")]
    [InlineData("1.5", "15")]
    public void DifferentNumberValues_GiveDifferentKeys(string left, string right)
    {
        Assert.NotEqual(Amount(left), Amount(right));
    }

    /// <summary>
    /// Порядок больше 18 цифр не приводится: ключ строится по исходной записи и не сворачивается с соседним
    /// значением (лучше лишний вызов, чем пропущенное действие).
    /// </summary>
    [Fact]
    public void HugeExponent_KeepsNotation_AndDoesNotCollide()
    {
        string huge = Amount("1e1000000000000000000000");

        Assert.Matches(new Regex("^ek1:[0-9a-f]{64}$"), huge);
        Assert.Equal(huge, Amount("1e1000000000000000000000"));
        Assert.NotEqual(huge, Amount("1e1000000000000000000001"));
        Assert.NotEqual(huge, Amount("2e1000000000000000000000"));
    }

    /// <summary>Регистр получателя и идентификатора, пробел внутри строки, порядок массива — разные ключи.</summary>
    [Fact]
    public void Collisions_AreNotProduced_ByCaseSpacesOrArrayOrder()
    {
        string key = Mail("run-1", "Ann@Example.org", "A-1", "[\"x\",\"y\"]", "добрый день");

        Assert.Equal(key, Mail("run-1", "Ann@Example.org", "A-1", "[\"x\",\"y\"]", "добрый день"));
        Assert.NotEqual(key, Mail("run-1", "ann@example.org", "A-1", "[\"x\",\"y\"]", "добрый день"));
        Assert.NotEqual(key, Mail("run-1", "Ann@Example.org", "a-1", "[\"x\",\"y\"]", "добрый день"));
        Assert.NotEqual(key, Mail("run-1", "Ann@Example.org", "A-1", "[\"x\",\"y\"]", "добрый  день"));
        Assert.NotEqual(key, Mail("run-1", "Ann@Example.org", "A-1", "[\"y\",\"x\"]", "добрый день"));
        Assert.NotEqual(key, Mail("Run-1", "Ann@Example.org", "A-1", "[\"x\",\"y\"]", "добрый день"));

        static string Mail(string run, string to, string id, string tags, string body) =>
            EffectKeys.Create(run, "send_mail", Kit.Json(
                "{\"to\":\"" + to + "\",\"id\":\"" + id + "\",\"tags\":" + tags + ",\"body\":\"" + body + "\"}"));
    }

    /// <summary>
    /// Эталоны формулы ek1: фиксированный вход — конкретная строка ключа. Менять эталон можно только решением
    /// владельца и только со сменой версии формулы (ek2:): иначе ключи журналов прежних запусков перестанут
    /// совпадать с новыми, и гейт повторит уже исполненный эффект.
    /// </summary>
    [Theory]
    [MemberData(nameof(Golden))]
    public void Golden_Ek1(string runId, string tool, string arguments, string expected)
    {
        Assert.Equal(expected, EffectKeys.Create(runId, tool, arguments.Length == 0 ? default : Kit.Json(arguments)));
    }

    /// <summary>Эталонные входы и ключи формулы ek1:.</summary>
    public static TheoryData<string, string, string, string> Golden => new()
    {
        { "run-1", "noop", "",
            "ek1:ee9a1b061fa9e4335416678ee5f45385b37c1b1ae088cfde3a73d4d2b6020eec" },
        { "run-1", "send_mail", """{"to":"a@example.org","body":"привет"}""",
            "ek1:18f986ad5efd0437c55f719361e66921a5f7232c0d2625e19002a40d0a7e65a3" },
        { "run-1", "pay", """{"order":{"id":7,"lines":[{"sku":"A","n":1},{"sku":"B","n":2}]},"amount":100}""",
            "ek1:0eca017c877531d874bf2e58dfe6c9bab7e1dcf9982fedfab66703b9e58c73ac" },
        { "run-1", "pay", """{"amount":1E+2,"order":{"lines":[{"n":1.0,"sku":"A"},{"n":2e0,"sku":"B"}],"id":7.00}}""",
            "ek1:0eca017c877531d874bf2e58dfe6c9bab7e1dcf9982fedfab66703b9e58c73ac" },
        { "run-2", "measure", """{"values":[0,-0.0,0.10,9007199254740993,-2.5e-3,1e400]}""",
            "ek1:ddee05f7f3e5f89a068e72b32d04244d0598151cd26ccba78d2d03e024d5b26a" },
        { "запуск-α", "перевод", """{"текст":"Ёжик в тумане 🦔","язык":"ru"}""",
            "ek1:a8120cce9e86687ab7a64e04f765df317303e366c455f108b47ea17b61fc3058" },
        { "run-3", "write_file", """{"path":"C:\\temp\\a \"b\".txt","text":"line1\nline2\t\u00e9\u0000"}""",
            "ek1:41a17e87f79002b1cd7ed584eea5b86e189d2d1d2f4fe2473bcd9623bf3f16c7" },
    };

    private static string Amount(string number) =>
        EffectKeys.Create("run-1", "pay", Kit.Json("{\"amount\":" + number + "}"));

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

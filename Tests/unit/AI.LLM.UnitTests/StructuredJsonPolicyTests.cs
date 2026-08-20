using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Policies;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Разбор ответа модели в решение шага. Ключевая проверка — неразобранный ответ НЕ означает
/// «модель закончила»: именно эта подмена приводила к молчаливому завершению хода с пустыми
/// руками.
/// </summary>
public class StructuredJsonPolicyTests
{
    [Fact]
    public void StructuredJsonPolicy_Parse_ReadsPlainDecision()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse(
            """{"thought":"надо поискать","action":"web_search","action_input":"погода"}""");

        Assert.False(decision.IsFinal);
        Assert.False(decision.IsMalformed);
        Assert.Equal("надо поискать", decision.Thought);
        ReActAction action = Assert.Single(decision.Actions);
        Assert.Equal("web_search", action.ToolName);
        Assert.Equal("погода", action.Arguments);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_StripsCodeFences()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse(
            "```json\n{\"action\":\"web_search\",\"action_input\":\"погода\"}\n```");

        Assert.Single(decision.Actions);
        Assert.Equal("web_search", decision.Actions[0].ToolName);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_FindsJsonSurroundedByProse()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse(
            "Конечно! Вот мой ответ:\n{\"action\":\"web_search\",\"action_input\":\"погода\"}\nНадеюсь, помог.");

        Assert.Single(decision.Actions);
    }

    [Theory]
    [InlineData("action_input")]
    [InlineData("input")]
    [InlineData("query")]
    [InlineData("arg")]
    public void StructuredJsonPolicy_Parse_AcceptsArgumentAliases(string field)
    {
        ReActDecision decision = StructuredJsonPolicy.Parse(
            "{\"action\":\"web_search\",\"" + field + "\":\"погода\"}");

        Assert.Equal("погода", Assert.Single(decision.Actions).Arguments);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_TreatsFinalFieldAsCompletion()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse("""{"thought":"хватит","final":"готовый ответ"}""");

        Assert.True(decision.IsFinal);
        Assert.Equal("готовый ответ", decision.FinalText);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_TreatsFinalActionAsCompletion()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse("""{"action":"final","answer":"готово"}""");

        Assert.True(decision.IsFinal);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_TreatsNonStringFinalAsSignalWithoutText()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse("""{"thought":"хватит","final":true}""");

        // {"final":true} означает «данных достаточно», а не ответ «True»:
        // иначе слово-заглушка уехало бы в итоговый текст.
        Assert.True(decision.IsFinal);
        Assert.Null(decision.FinalText);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_DoesNotTreatFalseFinalAsCompletion()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse(
            """{"final":false,"action":"web_search","action_input":"погода"}""");

        Assert.False(decision.IsFinal);
        Assert.Single(decision.Actions);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_RepairsJsonTruncatedInsideStringValue()
    {
        // Обрезка по лимиту токенов: объект не закрыт, значение оборвано на середине.
        // Уцелевшие поля должны дать действие, а не «не удалось разобрать».
        ReActDecision decision = StructuredJsonPolicy.Parse(
            """{"thought":"ищу конкурентов","action":"web_search","arg":"МойСклад позициони""");

        Assert.False(decision.IsMalformed);
        ReActAction action = Assert.Single(decision.Actions);
        Assert.Equal("web_search", action.ToolName);
        Assert.StartsWith("МойСклад", action.Arguments);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_RepairsJsonTruncatedAfterComma()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse(
            """{"thought":"данных достаточно","action":"final",""");

        Assert.False(decision.IsMalformed);
        Assert.True(decision.IsFinal);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_RepairsJsonTruncatedOnEscape()
    {
        // Обрыв ровно на символе экрана: висячий '\' не должен ломать достроенную строку.
        ReActDecision decision = StructuredJsonPolicy.Parse(
            "{\"action\":\"web_search\",\"arg\":\"текст \\");

        Assert.False(decision.IsMalformed);
        Assert.Equal("web_search", Assert.Single(decision.Actions).ToolName);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_ReturnsMalformedForUnparseableText()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse("Извините, я не понял вопрос.");

        Assert.True(decision.IsMalformed);
        Assert.False(decision.IsFinal);
        Assert.Empty(decision.Actions);
    }

    [Fact]
    public void StructuredJsonPolicy_Parse_ReturnsMalformedWhenActionMissing()
    {
        ReActDecision decision = StructuredJsonPolicy.Parse("""{"thought":"думаю"}""");

        Assert.True(decision.IsMalformed);
        Assert.False(decision.IsFinal);
    }
}

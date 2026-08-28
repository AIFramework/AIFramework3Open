using AI.Script.Hosting;
using AI.Script.Semantics;
using AI.Script.Syntax;

namespace AI.Script.UnitTests;

/// <summary>
/// Форма диагностики: позиция, фрагмент исходника, конкретика, что делать.
/// </summary>
/// <remarks>
/// Сообщение без последней части считается недоделанным (DESIGN.md §10): она и есть то, ради
/// чего проверка вообще существует.
/// </remarks>
public sealed class DiagnosticRenderingTests
{
    [Fact]
    public void Render_ContainsPositionFragmentAndHint()
    {
        Diagnostic error = Script.CheckFailsWith("let x = 1\nemit r = math.clamp(x, lo: 0)");
        string rendered = error.Render();

        Assert.Contains("script.ais:2:", rendered, StringComparison.Ordinal);
        Assert.Contains("emit r = math.clamp(x, lo: 0)", rendered, StringComparison.Ordinal);
        Assert.Contains("^^", rendered, StringComparison.Ordinal);
        Assert.Contains("= возможно, имелось в виду: low", rendered, StringComparison.Ordinal);
        Assert.Contains("сигнатура: math.clamp(", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UsesLanguageNamesNotEnumNames()
    {
        // «Ожидалось Greater» — имя перечисления, а не языка; читателю от него никакого проку.
        Diagnostic error = Script.CheckFailsWith("emit r = <1, 2");

        Assert.DoesNotContain("Greater", error.Message, StringComparison.Ordinal);
        Assert.Contains("'>'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TokenKind.Pipe, "'|>'")]
    [InlineData(TokenKind.FatArrow, "'=>'")]
    [InlineData(TokenKind.EndOfFile, "конец файла")]
    [InlineData(TokenKind.Newline, "перевод строки")]
    [InlineData(TokenKind.Identifier, "имя")]
    public void Describe_IsReadable(TokenKind kind, string expected) =>
        Assert.Equal(expected, Token.Describe(kind));

    [Fact]
    public void Render_ErrorsAreSortedByPosition()
    {
        CheckResult result = Script.Check("emit a = nope1\nemit b = nope2");

        Assert.False(result.Success);
        Assert.True(result.Diagnostics[0].Span.Start <= result.Diagnostics[^1].Span.Start);
    }

    [Fact]
    public void RunResult_SeparatesFailureFromTranscript()
    {
        // Пока причина отказа лежала ВНУТРИ транскрипта, вызывающий отличал сорвавшийся
        // скрипт от удачного только разбором текста.
        Hosting.RunResult result = Script.Run("print(\"работа\")\nemit r = [1][7]");

        Assert.False(result.Success);
        Assert.Contains("работа", result.Transcript);
        Assert.DoesNotContain(result.Transcript, line => line.Contains("AIS3101", StringComparison.Ordinal));
        Assert.Equal(DiagnosticCodes.IndexOutOfRange, result.Error!.Code);
    }
}

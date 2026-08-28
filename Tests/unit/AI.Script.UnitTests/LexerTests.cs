using AI.Script.Semantics;
using AI.Script.Syntax;

namespace AI.Script.UnitTests;

/// <summary>Лексический разбор: литералы, переносы строк, комментарии.</summary>
public sealed class LexerTests
{
    private static List<Token> Lex(string source, out DiagnosticBag diagnostics)
    {
        var text = new SourceText(source);
        diagnostics = new DiagnosticBag(text);

        return new Lexer(text, diagnostics).Lex();
    }

    private static List<Token> Lex(string source) => Lex(source, out _);

    [Theory]
    [InlineData("42", 42.0)]
    [InlineData("3.14", 3.14)]
    [InlineData("1e-3", 0.001)]
    [InlineData("1E3", 1000.0)]
    [InlineData("1_000_000", 1000000.0)]
    [InlineData("0xFF", 255.0)]
    [InlineData("0x1_0", 16.0)]
    public void Lexer_Number_ParsedWithInvariantCulture(string source, double expected)
    {
        List<Token> tokens = Lex(source);

        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(expected, (double)tokens[0].Value!, 12);
    }

    [Theory]
    [InlineData("250ms", 250)]
    [InlineData("30s", 30000)]
    [InlineData("5m", 300000)]
    [InlineData("2h", 7200000)]
    [InlineData("1d", 86400000)]
    public void Lexer_Duration_HasUnitSuffix(string source, double milliseconds)
    {
        List<Token> tokens = Lex(source);

        Assert.Equal(TokenKind.Duration, tokens[0].Kind);
        Assert.Equal(milliseconds, ((TimeSpan)tokens[0].Value!).TotalMilliseconds, 6);
    }

    [Fact]
    public void Lexer_NumberFollowedByIdentifier_IsNotDuration()
    {
        List<Token> tokens = Lex("5 max");

        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
    }

    [Fact]
    public void Lexer_Date_ParsesDateAndTime()
    {
        List<Token> tokens = Lex("@2026-08-28T14:30");

        Assert.Equal(TokenKind.Date, tokens[0].Kind);
        Assert.Equal(new DateTime(2026, 8, 28, 14, 30, 0), (DateTime)tokens[0].Value!);
    }

    [Fact]
    public void Lexer_AtBeforeLetter_IsAttributeMarker()
    {
        List<Token> tokens = Lex("@cache");

        Assert.Equal(TokenKind.At, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
    }

    [Fact]
    public void Lexer_RangeAfterNumber_IsNotDecimalPoint()
    {
        List<Token> tokens = Lex("1..10");

        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(TokenKind.DotDot, tokens[1].Kind);
        Assert.Equal(TokenKind.Number, tokens[2].Kind);
    }

    [Fact]
    public void Lexer_NewlineInsideParentheses_IsSuppressed()
    {
        List<Token> tokens = Lex("f(\n  1,\n  2\n)");

        Assert.DoesNotContain(tokens, token => token.Kind == TokenKind.Newline);
    }

    [Fact]
    public void Lexer_NewlineInsideBraces_IsSignificant()
    {
        List<Token> tokens = Lex("{\n  a\n  b\n}");

        Assert.Contains(tokens, token => token.Kind == TokenKind.Newline);
    }

    [Fact]
    public void Lexer_BlockInsideCall_KeepsStatementSeparators()
    {
        // Лямбда с телом-блоком внутри вызова: подавление переносов скобкой не должно
        // «склеивать» инструкции блока.
        List<Token> tokens = Lex("map(xs, x => {\n  let y = 1\n  y\n})");

        Assert.Contains(tokens, token => token.Kind == TokenKind.Newline);
    }

    [Fact]
    public void Lexer_Comment_IsSkipped()
    {
        List<Token> tokens = Lex("1 # комментарий\n2");

        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(TokenKind.Newline, tokens[1].Kind);
        Assert.Equal(TokenKind.Number, tokens[2].Kind);
    }

    [Fact]
    public void Lexer_DocComment_IsKept()
    {
        List<Token> tokens = Lex("#| Описание\nfn f() { }");

        Assert.Equal(TokenKind.DocComment, tokens[0].Kind);
        Assert.Equal("Описание", tokens[0].Value);
    }

    [Fact]
    public void Lexer_HashInsideString_IsNotComment()
    {
        List<Token> tokens = Lex("\"a # b\"");

        Assert.Equal(TokenKind.String, tokens[0].Kind);
        Assert.Equal("a # b", tokens[0].Value);
    }

    [Theory]
    [InlineData("\"a\\nb\"", "a\nb")]
    [InlineData("\"a\\tb\"", "a\tb")]
    [InlineData("\"a\\\"b\"", "a\"b")]
    [InlineData("\"a\\\\b\"", "a\\b")]
    [InlineData("\"a\\$b\"", "a$b")]
    public void Lexer_Escapes_AreDecoded(string source, string expected)
    {
        List<Token> tokens = Lex(source);

        Assert.Equal(expected, tokens[0].Value);
    }

    [Fact]
    public void Lexer_Interpolation_SplitsIntoParts()
    {
        List<Token> tokens = Lex("\"k=${k}!\"");
        IReadOnlyList<StringPart> parts = tokens[0].Parts!;

        Assert.Equal(3, parts.Count);
        Assert.Equal("k=", parts[0].Text);
        Assert.Equal("k", parts[1].Expression);
        Assert.Equal("!", parts[2].Text);
    }

    [Fact]
    public void Lexer_NestedBracesInInterpolation_AreBalanced()
    {
        List<Token> tokens = Lex("\"${ {a: 1}.a }\"");
        IReadOnlyList<StringPart> parts = tokens[0].Parts!;

        Assert.Single(parts);
        Assert.Equal(" {a: 1}.a ", parts[0].Expression);
    }

    [Fact]
    public void Lexer_UnterminatedString_Reports()
    {
        _ = Lex("\"abc", out DiagnosticBag diagnostics);

        Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.UnterminatedString);
    }

    [Fact]
    public void Lexer_SinglePipe_ReportsWithHint()
    {
        _ = Lex("a | b", out DiagnosticBag diagnostics);

        Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.InvalidCharacter);
    }

    [Fact]
    public void Lexer_MultilineString_RemovesCommonIndent()
    {
        const string source = "\"\"\"\n    первая\n    вторая\n    \"\"\"";

        List<Token> tokens = Lex(source);

        Assert.Equal("первая\nвторая", tokens[0].Value);
    }

    [Fact]
    public void Lexer_MultilineString_KeepsRelativeIndent()
    {
        const string source = "\"\"\"\n    a\n      b\n    \"\"\"";

        List<Token> tokens = Lex(source);

        Assert.Equal("a\n  b", tokens[0].Value);
    }

    [Fact]
    public void Lexer_Underscore_IsPlaceholderToken()
    {
        List<Token> tokens = Lex("_ _x");

        Assert.Equal(TokenKind.Underscore, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
    }

    [Fact]
    public void Lexer_Position_PointsAtCharacter()
    {
        var text = new SourceText("let x = 1\nlet y = 2");

        LinePosition position = text.GetLinePosition(text.Text.IndexOf('y', StringComparison.Ordinal));

        Assert.Equal(2, position.Line);
        Assert.Equal(5, position.Column);
    }
}

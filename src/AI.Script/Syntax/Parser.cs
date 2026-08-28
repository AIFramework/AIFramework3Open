using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Syntax.Ast;

namespace AI.Script.Syntax;

/// <summary>
/// Синтаксический анализатор AIScript: рекурсивный спуск с приоритетами из DESIGN.md §7.1.
/// </summary>
/// <remarks>
/// Рекурсивный спуск, а не построчные регулярные выражения (как в прежнем вычислителе
/// <c>AI.ClassicMath/Calculator</c>): регулярки не дают ни вложенности, ни позиции ошибки, а
/// позиция ошибки — половина ценности диагностики для того, кто скрипт правит.
/// </remarks>
public sealed partial class Parser
{
    private readonly SourceText _source;
    private readonly DiagnosticBag _diagnostics;
    private readonly List<Token> _tokens;

    private int _position;
    private string? _pendingDocumentation;

    /// <summary>Создаёт парсер над исходным текстом.</summary>
    public Parser(SourceText source, DiagnosticBag diagnostics)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _tokens = new Lexer(source, diagnostics).Lex();
    }

    private Parser(SourceText source, DiagnosticBag diagnostics, List<Token> tokens)
    {
        _source = source;
        _diagnostics = diagnostics;
        _tokens = tokens;
    }

    /// <summary>Разбирает файл целиком.</summary>
    public ScriptUnit ParseUnit()
    {
        var unit = new ScriptUnit();
        int start = Current.Span.Start;

        SkipNewlines();

        if (Current.Kind == TokenKind.Options)
        {
            unit.Options = ParseOptions();
            EndStatement();
        }

        while (true)
        {
            SkipNewlines();
            if (Current.Kind == TokenKind.EndOfFile) break;

            if (Current.Kind == TokenKind.Options)
            {
                Error(DiagnosticCodes.MisplacedOptions, Current.Span,
                    "блок 'options' допустим только первой инструкцией файла",
                    "перенесите его в начало скрипта");
                _ = ParseOptions();
                EndStatement();
                continue;
            }

            int before = _position;

            Stmt? statement = ParseStatement(topLevel: true);
            if (statement != null) unit.Statements.Add(statement);

            EndStatement();

            // Страховка от зацикливания на неразбираемой лексеме: разбор обязан двигаться
            // вперёд, иначе одна лишняя '}' подвесила бы процесс вместо выдачи диагностики.
            if (_position == before) _ = Advance();
        }

        unit.Span = TextSpan.FromBounds(start, Current.Span.End);
        return unit;
    }

    /// <summary>Разбирает одиночное выражение; используется для подстановок в строках.</summary>
    private Expr ParseExpressionEntry()
    {
        SkipNewlines();
        Expr expression = ParseExpression();
        SkipNewlines();

        if (Current.Kind != TokenKind.EndOfFile)
        {
            Error(DiagnosticCodes.UnexpectedToken, Current.Span,
                $"лишнее в подстановке: {Token.Describe(Current.Kind)}");
        }

        return expression;
    }

    // --- инфраструктура ---

    private Token Current => _tokens[_position];

    private Token Peek(int offset)
    {
        int index = _position + offset;
        return index < _tokens.Count ? _tokens[index] : _tokens[^1];
    }

    private Token Advance()
    {
        Token token = _tokens[_position];
        if (_position < _tokens.Count - 1) _position++;
        return token;
    }

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind) return false;
        _ = Advance();
        return true;
    }

    private Token Expect(TokenKind kind, string context)
    {
        if (Current.Kind == kind) return Advance();

        Error(DiagnosticCodes.UnexpectedToken, Current.Span,
            $"ожидалось {Token.Describe(kind)}, встречено {Token.Describe(Current.Kind)}",
            context);

        return new Token(kind, Current.Span, string.Empty);
    }

    /// <summary>Идёт ли за переводами строк лексема заданного вида.</summary>
    private bool PeekPast(TokenKind kind)
    {
        int index = _position;

        while (index < _tokens.Count && _tokens[index].Kind is TokenKind.Newline or TokenKind.DocComment) index++;

        return index < _tokens.Count && _tokens[index].Kind == kind;
    }

    private void SkipNewlines()
    {
        while (Current.Kind is TokenKind.Newline or TokenKind.DocComment)
        {
            if (Current.Kind == TokenKind.DocComment) _pendingDocumentation = (string?)Current.Value;
            _ = Advance();
        }
    }

    private void EndStatement()
    {
        if (Current.Kind is TokenKind.Newline or TokenKind.EndOfFile or TokenKind.RBrace) return;

        Error(DiagnosticCodes.UnexpectedToken, Current.Span,
            $"ожидался конец инструкции, встречено {Token.Describe(Current.Kind)}",
            "инструкции разделяются переводом строки; точка с запятой в языке не используется");

        while (Current.Kind is not (TokenKind.Newline or TokenKind.EndOfFile or TokenKind.RBrace))
            _ = Advance();
    }

    private void Error(string code, TextSpan span, string message, string? hint = null) =>
        _diagnostics.Error(code, span, message, hint);

    private (string Name, TextSpan Span) ExpectIdentifier(string context)
    {
        if (Current.Kind == TokenKind.Identifier)
        {
            Token token = Advance();
            return ((string)token.Value!, token.Span);
        }

        Error(DiagnosticCodes.UnexpectedToken, Current.Span,
            $"ожидалось имя, встречено {Token.Describe(Current.Kind)}", context);

        return (string.Empty, Current.Span);
    }

    private ScriptType? ParseTypeAnnotation()
    {
        (string name, TextSpan span) = ExpectIdentifier("после ':' указывается тип");
        ScriptType? type = ScriptTypeNames.Parse(name);

        if (type == null)
        {
            Error(DiagnosticCodes.UnexpectedToken, span,
                $"неизвестный тип '{name}'",
                "типы языка: num bool str date dur vec cvec mat tensor list record table fn handle any");
            return null;
        }

        if (type == ScriptType.Handle && Current.Kind == TokenKind.Less)
        {
            _ = Advance();

            while (Current.Kind is not (TokenKind.Greater or TokenKind.EndOfFile or TokenKind.Newline))
                _ = Advance();

            _ = Match(TokenKind.Greater);
        }

        return type;
    }
}

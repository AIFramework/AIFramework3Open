using AI.Script.Semantics;
using System.Globalization;
using System.Text;

namespace AI.Script.Syntax;

/// <summary>
/// Лексический анализатор AIScript.
/// </summary>
/// <remarks>
/// Переводы строк значимы (они завершают инструкцию), но не везде: внутри <c>(</c> и <c>[</c>
/// они подавляются, а внутри <c>{</c> — нет, потому что блок состоит из инструкций. Поэтому
/// подавление ведётся стеком, а не счётчиком: лямбда с телом-блоком внутри вызова функции
/// иначе потеряла бы разделители между своими инструкциями.
/// </remarks>
public sealed class Lexer
{
    private static readonly Dictionary<string, TokenKind> s_keywords = new(StringComparer.Ordinal)
    {
        ["options"] = TokenKind.Options,
        ["let"] = TokenKind.Let,
        ["set"] = TokenKind.Set,
        ["fn"] = TokenKind.Fn,
        ["stage"] = TokenKind.Stage,
        ["return"] = TokenKind.Return,
        ["if"] = TokenKind.If,
        ["else"] = TokenKind.Else,
        ["for"] = TokenKind.For,
        ["in"] = TokenKind.In,
        ["by"] = TokenKind.By,
        ["while"] = TokenKind.While,
        ["break"] = TokenKind.Break,
        ["continue"] = TokenKind.Continue,
        ["try"] = TokenKind.Try,
        ["catch"] = TokenKind.Catch,
        ["use"] = TokenKind.Use,
        ["as"] = TokenKind.As,
        ["emit"] = TokenKind.Emit,
        ["show"] = TokenKind.Show,
        ["assert"] = TokenKind.Assert,
        ["true"] = TokenKind.True,
        ["false"] = TokenKind.False,
        ["none"] = TokenKind.None,
        ["nan"] = TokenKind.Nan,
        ["inf"] = TokenKind.Inf,
    };

    private readonly SourceText _source;
    private readonly DiagnosticBag _diagnostics;
    private readonly int _end;
    private readonly List<bool> _suppressNewline = [];

    private int _position;

    /// <summary>Создаёт лексер над (частью) исходного текста.</summary>
    /// <param name="source">Исходный текст.</param>
    /// <param name="diagnostics">Накопитель диагностик.</param>
    /// <param name="start">Смещение начала разбора.</param>
    /// <param name="end">Смещение конца разбора; отрицательное — до конца текста.</param>
    public Lexer(SourceText source, DiagnosticBag diagnostics, int start = 0, int end = -1)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _position = Math.Clamp(start, 0, source.Length);
        _end = end < 0 ? source.Length : Math.Clamp(end, _position, source.Length);
    }

    /// <summary>Разбирает весь вход в поток лексем, завершающийся <see cref="TokenKind.EndOfFile"/>.</summary>
    public List<Token> Lex()
    {
        var tokens = new List<Token>();

        while (true)
        {
            Token token = Next();
            tokens.Add(token);
            if (token.Kind == TokenKind.EndOfFile) break;
        }

        return tokens;
    }

    private char Current => Peek(0);

    private char Peek(int offset)
    {
        int index = _position + offset;
        return index < _end ? _source[index] : '\0';
    }

    private bool NewlineSuppressed => _suppressNewline.Count > 0 && _suppressNewline[^1];

    private Token Next()
    {
        while (true)
        {
            if (_position >= _end)
                return new Token(TokenKind.EndOfFile, new TextSpan(_end, 0), string.Empty);

            char c = Current;

            if (c is ' ' or '\t')
            {
                _position++;
                continue;
            }

            if (c is '\r' or '\n')
            {
                int start = _position;
                while (_position < _end && (Current is '\r' or '\n' or ' ' or '\t')) _position++;

                if (NewlineSuppressed) continue;

                return new Token(TokenKind.Newline, TextSpan.FromBounds(start, _position), "\\n");
            }

            if (c == '#')
            {
                Token? doc = ReadComment();
                if (doc != null) return doc;
                continue;
            }

            break;
        }

        return ReadToken();
    }

    private Token? ReadComment()
    {
        int start = _position;
        bool isDoc = Peek(1) == '|';

        _position += isDoc ? 2 : 1;

        int textStart = _position;
        while (_position < _end && Current is not ('\n' or '\r')) _position++;

        if (!isDoc) return null;

        string text = _source.Text[textStart.._position].Trim();
        return new Token(TokenKind.DocComment, TextSpan.FromBounds(start, _position), text, text);
    }

    private Token ReadToken()
    {
        int start = _position;
        char c = Current;

        if (char.IsAsciiDigit(c)) return ReadNumber();
        if (c == '"') return ReadString();
        if (c == '@' && char.IsAsciiDigit(Peek(1))) return ReadDate();
        if (char.IsLetter(c) || c == '_') return ReadIdentifier();

        _position++;

        switch (c)
        {
            case '(': Push(true); return Make(TokenKind.LParen, start);
            case '[': Push(true); return Make(TokenKind.LBracket, start);
            case '{': Push(false); return Make(TokenKind.LBrace, start);
            case ')': Pop(); return Make(TokenKind.RParen, start);
            case ']': Pop(); return Make(TokenKind.RBracket, start);
            case '}': Pop(); return Make(TokenKind.RBrace, start);
            case ',': return Make(TokenKind.Comma, start);
            case ':': return Make(TokenKind.Colon, start);
            case '@': return Make(TokenKind.At, start);
            case '^': return Make(TokenKind.Caret, start);
            case '%': return Make(TokenKind.Percent, start);

            case '.':
                if (Current == '.' && Peek(1) == '.') { _position += 2; return Make(TokenKind.Ellipsis, start); }
                if (Current == '.') { _position++; return Make(TokenKind.DotDot, start); }
                return Make(TokenKind.Dot, start);

            case '+':
                if (Current == '=') { _position++; return Make(TokenKind.PlusAssign, start); }
                return Make(TokenKind.Plus, start);

            case '-':
                if (Current == '=') { _position++; return Make(TokenKind.MinusAssign, start); }
                if (Current == '>') { _position++; return Make(TokenKind.Arrow, start); }
                return Make(TokenKind.Minus, start);

            case '*':
                if (Current == '=') { _position++; return Make(TokenKind.StarAssign, start); }
                return Make(TokenKind.Star, start);

            case '/':
                if (Current == '=') { _position++; return Make(TokenKind.SlashAssign, start); }
                return Make(TokenKind.Slash, start);

            case '=':
                if (Current == '=') { _position++; return Make(TokenKind.EqEq, start); }
                if (Current == '>') { _position++; return Make(TokenKind.FatArrow, start); }
                return Make(TokenKind.Assign, start);

            case '!':
                if (Current == '=') { _position++; return Make(TokenKind.NotEq, start); }
                return Make(TokenKind.Not, start);

            case '<':
                if (Current == '=') { _position++; return Make(TokenKind.LessEq, start); }
                return Make(TokenKind.Less, start);

            case '>':
                if (Current == '=') { _position++; return Make(TokenKind.GreaterEq, start); }
                return Make(TokenKind.Greater, start);

            case '|':
                if (Current == '>') { _position++; return Make(TokenKind.Pipe, start); }
                if (Current == '|') { _position++; return Make(TokenKind.OrOr, start); }
                _diagnostics.Error(
                    DiagnosticCodes.InvalidCharacter, new TextSpan(start, 1),
                    "одиночный '|' не является оператором",
                    "конвейер записывается как '|>', логическое ИЛИ — как '||'; побитовые операции — функции 'bit.*'");
                return Make(TokenKind.Not, start);

            case '&':
                if (Current == '&') { _position++; return Make(TokenKind.AndAnd, start); }
                _diagnostics.Error(
                    DiagnosticCodes.InvalidCharacter, new TextSpan(start, 1),
                    "одиночный '&' не является оператором",
                    "логическое И записывается как '&&'; побитовые операции — функции 'bit.*'");
                return Make(TokenKind.AndAnd, start);

            default:
                _diagnostics.Error(
                    DiagnosticCodes.InvalidCharacter, new TextSpan(start, 1),
                    $"недопустимый символ '{c}'",
                    c == ';'
                        ? "инструкции разделяются переводом строки; точка с запятой в языке не используется"
                        : null);
                return Next();
        }
    }

    private void Push(bool suppress) => _suppressNewline.Add(suppress);

    private void Pop()
    {
        if (_suppressNewline.Count > 0) _suppressNewline.RemoveAt(_suppressNewline.Count - 1);
    }

    private Token Make(TokenKind kind, int start) =>
        new(kind, TextSpan.FromBounds(start, _position), _source.Text[start.._position]);

    private Token ReadIdentifier()
    {
        int start = _position;

        while (_position < _end && (char.IsLetterOrDigit(Current) || Current == '_')) _position++;

        string text = _source.Text[start.._position];
        var span = TextSpan.FromBounds(start, _position);

        if (text == "_") return new Token(TokenKind.Underscore, span, text);

        return s_keywords.TryGetValue(text, out TokenKind keyword)
            ? new Token(keyword, span, text)
            : new Token(TokenKind.Identifier, span, text, text);
    }

    private Token ReadNumber()
    {
        int start = _position;
        double value;

        if (Current == '0' && (Peek(1) is 'x' or 'X'))
        {
            _position += 2;
            int digitsStart = _position;

            while (_position < _end && (Uri.IsHexDigit(Current) || Current == '_')) _position++;

            string hex = _source.Text[digitsStart.._position].Replace("_", string.Empty);

            if (hex.Length == 0 || !ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsed))
            {
                _diagnostics.Error(
                    DiagnosticCodes.InvalidNumber, TextSpan.FromBounds(start, _position),
                    "некорректный шестнадцатеричный литерал", "ожидалось, например, 0xFF");
                parsed = 0;
            }

            value = parsed;
        }
        else
        {
            while (_position < _end && (char.IsAsciiDigit(Current) || Current == '_')) _position++;

            if (Current == '.' && char.IsAsciiDigit(Peek(1)))
            {
                _position++;
                while (_position < _end && (char.IsAsciiDigit(Current) || Current == '_')) _position++;
            }

            if (Current is 'e' or 'E')
            {
                bool signed = Peek(1) is '+' or '-';
                if (char.IsAsciiDigit(Peek(signed ? 2 : 1)))
                {
                    _position += signed ? 2 : 1;
                    while (_position < _end && char.IsAsciiDigit(Current)) _position++;
                }
            }

            string text = _source.Text[start.._position].Replace("_", string.Empty);

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                _diagnostics.Error(
                    DiagnosticCodes.InvalidNumber, TextSpan.FromBounds(start, _position),
                    $"не удалось разобрать число '{text}'");
                value = 0;
            }
        }

        string? unit = ReadDurationUnit();

        if (unit == null)
            return new Token(TokenKind.Number, TextSpan.FromBounds(start, _position), _source.Text[start.._position], value);

        TimeSpan duration = unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(value),
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            _ => TimeSpan.FromDays(value),
        };

        return new Token(TokenKind.Duration, TextSpan.FromBounds(start, _position), _source.Text[start.._position], duration);
    }

    private string? ReadDurationUnit()
    {
        if (Current == 'm' && Peek(1) == 's' && !IsIdentifierPart(Peek(2)))
        {
            _position += 2;
            return "ms";
        }

        if (Current is 's' or 'm' or 'h' or 'd' && !IsIdentifierPart(Peek(1)))
        {
            string unit = Current.ToString();
            _position++;
            return unit;
        }

        return null;
    }

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private Token ReadDate()
    {
        int start = _position;
        _position++;

        while (_position < _end && (char.IsAsciiDigit(Current) || Current is '-' or ':' or 'T')) _position++;

        var span = TextSpan.FromBounds(start, _position);
        string text = _source.Text[start.._position];
        string body = text[1..];

        string[] formats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"];

        if (!DateTime.TryParseExact(body, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value))
        {
            _diagnostics.Error(
                DiagnosticCodes.InvalidDate, span,
                $"некорректная дата '{text}'",
                "ожидается @ГГГГ-ММ-ДД либо @ГГГГ-ММ-ДДTчч:мм");
            value = default;
        }

        return new Token(TokenKind.Date, span, text, value);
    }

    private Token ReadString()
    {
        int start = _position;
        bool triple = Peek(1) == '"' && Peek(2) == '"';
        int quoteLength = triple ? 3 : 1;

        _position += quoteLength;

        int contentStart = _position;
        var parts = new List<StringPart>();
        var buffer = new StringBuilder();
        bool terminated = false;

        while (_position < _end)
        {
            char c = Current;

            if (!triple && c is '\n' or '\r') break;

            if (c == '"')
            {
                if (!triple) { terminated = true; break; }
                if (Peek(1) == '"' && Peek(2) == '"') { terminated = true; break; }
            }

            if (c == '\\')
            {
                AppendEscape(buffer, triple);
                continue;
            }

            if (c == '$' && Peek(1) == '{')
            {
                if (buffer.Length > 0)
                {
                    parts.Add(StringPart.FromText(buffer.ToString()));
                    _ = buffer.Clear();
                }

                ReadInterpolation(parts, triple);
                continue;
            }

            _ = buffer.Append(c);
            _position++;
        }

        int contentEnd = _position;

        if (buffer.Length > 0) parts.Add(StringPart.FromText(buffer.ToString()));

        if (!terminated)
        {
            _diagnostics.Error(
                DiagnosticCodes.UnterminatedString, TextSpan.FromBounds(start, _position),
                "незакрытый строковый литерал",
                triple ? "многострочная строка закрывается тремя кавычками" : "закройте строку кавычкой на той же строке");
        }
        else
        {
            _position += quoteLength;
        }

        if (triple) Dedent(parts, _source.Text[contentStart..contentEnd]);

        var span = TextSpan.FromBounds(start, _position);
        string raw = _source.Text[start.._position];

        bool plain = parts.Count == 0 || (parts.Count == 1 && !parts[0].IsExpression);
        object? value = plain ? (parts.Count == 0 ? string.Empty : parts[0].Text) : null;

        return new Token(TokenKind.String, span, raw, value, parts);
    }

    private void AppendEscape(StringBuilder buffer, bool triple)
    {
        if (triple)
        {
            if (Peek(1) == '$')
            {
                _ = buffer.Append('$');
                _position += 2;
                return;
            }

            _ = buffer.Append('\\');
            _position++;
            return;
        }

        int escapeStart = _position;
        _position++;

        char escaped = Current;
        _position++;

        switch (escaped)
        {
            case 'n': _ = buffer.Append('\n'); break;
            case 't': _ = buffer.Append('\t'); break;
            case 'r': _ = buffer.Append('\r'); break;
            case '0': _ = buffer.Append('\0'); break;
            case '\\': _ = buffer.Append('\\'); break;
            case '"': _ = buffer.Append('"'); break;
            case '$': _ = buffer.Append('$'); break;
            default:
                _diagnostics.Warning(
                    DiagnosticCodes.UnknownEscape, TextSpan.FromBounds(escapeStart, _position),
                    $"неизвестная escape-последовательность '\\{escaped}'",
                    "известны: \\n \\t \\r \\0 \\\\ \\\" \\$");
                _ = buffer.Append(escaped);
                break;
        }
    }

    private void ReadInterpolation(List<StringPart> parts, bool triple)
    {
        int markerStart = _position;
        _position += 2;

        int exprStart = _position;
        int depth = 1;

        while (_position < _end)
        {
            char c = Current;

            if (c == '"') { SkipNestedString(); continue; }
            if (!triple && c is '\n' or '\r') break;

            if (c == '{') { depth++; _position++; continue; }

            if (c == '}')
            {
                depth--;
                if (depth == 0) break;
                _position++;
                continue;
            }

            _position++;
        }

        int exprEnd = _position;

        if (depth != 0)
        {
            _diagnostics.Error(
                DiagnosticCodes.UnterminatedInterpolation, TextSpan.FromBounds(markerStart, _position),
                "незакрытая подстановка '${'", "подстановка закрывается '}'");
        }
        else
        {
            _position++;
        }

        parts.Add(StringPart.FromExpression(_source.Text[exprStart..exprEnd], exprStart));
    }

    private void SkipNestedString()
    {
        _position++;

        while (_position < _end && Current != '"')
        {
            if (Current == '\\') _position++;
            _position++;
        }

        if (_position < _end) _position++;
    }

    /// <summary>
    /// Снимает общий отступ у многострочного литерала.
    /// </summary>
    /// <remarks>
    /// Отступ считается по сырому содержимому — там видны начала строк целиком, включая те,
    /// что начинаются подстановкой. Снимается он затем только у текстовых частей: перевод
    /// строки может находиться лишь в них.
    /// </remarks>
    private static void Dedent(List<StringPart> parts, string raw)
    {
        string[] lines = raw.Replace("\r\n", "\n").Split('\n');
        int indent = int.MaxValue;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Trim().Length == 0 && i != lines.Length - 1) continue;

            int width = 0;
            while (width < line.Length && line[width] is ' ' or '\t') width++;

            if (width < indent) indent = width;
        }

        if (indent is int.MaxValue or 0)
        {
            TrimEdges(parts);
            return;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].IsExpression) continue;

            parts[i] = StringPart.FromText(StripIndent(parts[i].Text!, indent));
        }

        TrimEdges(parts);
    }

    private static string StripIndent(string text, int indent)
    {
        var builder = new StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            _ = builder.Append(text[i]);

            if (text[i] != '\n') continue;

            int removed = 0;
            while (removed < indent && i + 1 < text.Length && text[i + 1] is ' ' or '\t')
            {
                i++;
                removed++;
            }
        }

        return builder.ToString();
    }

    private static void TrimEdges(List<StringPart> parts)
    {
        if (parts.Count == 0) return;

        if (!parts[0].IsExpression)
        {
            string first = parts[0].Text!;
            int cut = 0;

            while (cut < first.Length && first[cut] is ' ' or '\t') cut++;

            if (cut < first.Length && first[cut] == '\n')
                parts[0] = StringPart.FromText(first[(cut + 1)..]);
        }

        int last = parts.Count - 1;

        if (!parts[last].IsExpression)
        {
            string text = parts[last].Text!;
            int end = text.Length;

            while (end > 0 && text[end - 1] is ' ' or '\t') end--;
            if (end > 0 && text[end - 1] == '\n') end--;

            parts[last] = StringPart.FromText(text[..end]);
        }

        if (parts.Count > 0 && !parts[0].IsExpression && parts[0].Text!.Length == 0 && parts.Count > 1)
            parts.RemoveAt(0);
    }
}

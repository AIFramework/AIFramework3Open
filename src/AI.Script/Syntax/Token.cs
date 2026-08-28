namespace AI.Script.Syntax;

/// <summary>
/// Часть строкового литерала: либо готовый текст, либо подстановка <c>${...}</c>.
/// </summary>
/// <remarks>
/// Выражение внутри подстановки лексер НЕ разбирает: он лишь вырезает его текст и запоминает
/// абсолютное смещение. Разбор делает парсер вложенным проходом — так позиции в диагностике
/// указывают внутрь строки, а не на строку целиком.
/// </remarks>
public readonly struct StringPart
{
    /// <summary>Готовый текст; <c>null</c> для подстановки.</summary>
    public string? Text { get; }

    /// <summary>Текст выражения подстановки; <c>null</c> для готового текста.</summary>
    public string? Expression { get; }

    /// <summary>Абсолютное смещение выражения в исходном файле.</summary>
    public int ExpressionStart { get; }

    private StringPart(string? text, string? expression, int expressionStart)
    {
        Text = text;
        Expression = expression;
        ExpressionStart = expressionStart;
    }

    /// <summary>Является ли часть подстановкой.</summary>
    public bool IsExpression => Expression != null;

    /// <summary>Создаёт текстовую часть.</summary>
    public static StringPart FromText(string text) => new(text, null, 0);

    /// <summary>Создаёт часть-подстановку.</summary>
    public static StringPart FromExpression(string expression, int start) => new(null, expression, start);
}

/// <summary>Лексема: вид, отрезок исходника и разобранное значение.</summary>
public sealed class Token
{
    /// <summary>Вид лексемы.</summary>
    public TokenKind Kind { get; }

    /// <summary>Отрезок исходника.</summary>
    public TextSpan Span { get; }

    /// <summary>Текст лексемы как он записан в исходнике.</summary>
    public string Text { get; }

    /// <summary>
    /// Разобранное значение: <see cref="double"/> для числа, <see cref="string"/> для строки
    /// без подстановок и идентификатора, <see cref="DateTime"/> и <see cref="TimeSpan"/>
    /// для соответствующих литералов.
    /// </summary>
    public object? Value { get; }

    /// <summary>Части строкового литерала; не <c>null</c> только для <see cref="TokenKind.String"/>.</summary>
    public IReadOnlyList<StringPart>? Parts { get; }

    /// <summary>Создаёт лексему.</summary>
    public Token(TokenKind kind, TextSpan span, string text, object? value = null, IReadOnlyList<StringPart>? parts = null)
    {
        Kind = kind;
        Span = span;
        Text = text;
        Value = value;
        Parts = parts;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind} '{Text}' {Span}";

    /// <summary>
    /// Человекочитаемое имя вида лексемы для сообщений об ошибках.
    /// </summary>
    /// <remarks>
    /// Именно эту строку читает тот, кто правит скрипт. «Ожидалось <c>Greater</c>» — имя
    /// перечисления, а не языка: понять по нему, чего не хватает, нельзя.
    /// </remarks>
    public static string Describe(TokenKind kind) => kind switch
    {
        TokenKind.EndOfFile => "конец файла",
        TokenKind.Newline => "перевод строки",
        TokenKind.DocComment => "документирующий комментарий",
        TokenKind.Number => "число",
        TokenKind.String => "строка",
        TokenKind.Duration => "длительность",
        TokenKind.Date => "дата",
        TokenKind.Identifier => "имя",
        TokenKind.Underscore => "'_'",
        TokenKind.LParen => "'('",
        TokenKind.RParen => "')'",
        TokenKind.LBracket => "'['",
        TokenKind.RBracket => "']'",
        TokenKind.LBrace => "'{'",
        TokenKind.RBrace => "'}'",
        TokenKind.Comma => "','",
        TokenKind.Colon => "':'",
        TokenKind.Dot => "'.'",
        TokenKind.DotDot => "'..'",
        TokenKind.Ellipsis => "'...'",
        TokenKind.At => "'@'",
        TokenKind.Assign => "'='",
        TokenKind.FatArrow => "'=>'",
        TokenKind.Arrow => "'->'",
        TokenKind.Pipe => "'|>'",
        TokenKind.OrOr => "'||'",
        TokenKind.AndAnd => "'&&'",
        TokenKind.Not => "'!'",
        TokenKind.EqEq => "'=='",
        TokenKind.NotEq => "'!='",
        TokenKind.Less => "'<'",
        TokenKind.Greater => "'>'",
        TokenKind.LessEq => "'<='",
        TokenKind.GreaterEq => "'>='",
        TokenKind.Plus => "'+'",
        TokenKind.Minus => "'-'",
        TokenKind.Star => "'*'",
        TokenKind.Slash => "'/'",
        TokenKind.Percent => "'%'",
        TokenKind.Caret => "'^'",
        TokenKind.PlusAssign => "'+='",
        TokenKind.MinusAssign => "'-='",
        TokenKind.StarAssign => "'*='",
        TokenKind.SlashAssign => "'/='",
        _ => $"'{kind.ToString().ToLowerInvariant()}'",
    };
}

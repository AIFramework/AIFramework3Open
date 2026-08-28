using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Syntax.Ast;

namespace AI.Script.Syntax;

public sealed partial class Parser
{
    private Expr ParseExpression() => ParsePipe();

    private Expr ParsePipe()
    {
        Expr left = ParseOr();

        while (PipeAhead())
        {
            SkipNewlines();
            Token op = Advance();
            SkipNewlines();

            Expr right = ParseOr();

            if (right is not CallExpr call)
            {
                Error(DiagnosticCodes.PipeTargetNotCall, right.Span,
                    "правое звено конвейера должно быть вызовом функции",
                    "запись 'x |> f(a: 1)' означает 'f(x, a: 1)'; вероятно, пропущены скобки вызова");

                left = right;
                continue;
            }

            left = new PipeExpr
            {
                Left = left,
                Right = call,
                OperatorSpan = op.Span,
                Span = left.Span.Union(call.Span),
            };
        }

        return left;
    }

    /// <summary>
    /// Начинается ли следующая непустая строка со звена конвейера.
    /// </summary>
    /// <remarks>
    /// Это правило и делает возможной запись конвейера лесенкой — ради него перевод строки
    /// перестаёт завершать инструкцию, если дальше идёт <c>|&gt;</c>.
    /// </remarks>
    private bool PipeAhead()
    {
        if (Current.Kind == TokenKind.Pipe) return true;
        if (Current.Kind != TokenKind.Newline) return false;

        int index = _position;

        while (index < _tokens.Count && _tokens[index].Kind is TokenKind.Newline or TokenKind.DocComment) index++;

        return index < _tokens.Count && _tokens[index].Kind == TokenKind.Pipe;
    }

    private Expr ParseOr()
    {
        Expr left = ParseAnd();

        while (Current.Kind == TokenKind.OrOr)
        {
            Token op = Advance();
            SkipNewlines();
            Expr right = ParseAnd();
            left = Binary(BinaryOperator.Or, left, right, op.Span);
        }

        return left;
    }

    private Expr ParseAnd()
    {
        Expr left = ParseComparison();

        while (Current.Kind == TokenKind.AndAnd)
        {
            Token op = Advance();
            SkipNewlines();
            Expr right = ParseComparison();
            left = Binary(BinaryOperator.And, left, right, op.Span);
        }

        return left;
    }

    /// <summary>
    /// Сравнение. Не ассоциативно: <c>a &lt; b &lt; c</c> — ошибка, а не <c>(a &lt; b) &lt; c</c>.
    /// </summary>
    private Expr ParseComparison()
    {
        Expr left = ParseRange();

        BinaryOperator? op = ComparisonOperator(Current.Kind);
        if (op == null) return left;

        Token token = Advance();
        SkipNewlines();
        Expr right = ParseRange();
        Expr result = Binary(op.Value, left, right, token.Span);

        if (ComparisonOperator(Current.Kind) != null)
        {
            Error(DiagnosticCodes.ChainedComparison, Current.Span,
                "цепочка сравнений не поддерживается",
                "запишите два сравнения через '&&', например 'a < b && b < c'");

            _ = Advance();
            _ = ParseRange();
        }

        return result;
    }

    private static BinaryOperator? ComparisonOperator(TokenKind kind) => kind switch
    {
        TokenKind.EqEq => BinaryOperator.Equal,
        TokenKind.NotEq => BinaryOperator.NotEqual,
        TokenKind.Less => BinaryOperator.Less,
        TokenKind.Greater => BinaryOperator.Greater,
        TokenKind.LessEq => BinaryOperator.LessOrEqual,
        TokenKind.GreaterEq => BinaryOperator.GreaterOrEqual,
        _ => null,
    };

    private Expr ParseRange()
    {
        Expr from = ParseAdditive();

        if (Current.Kind != TokenKind.DotDot) return from;

        _ = Advance();
        SkipNewlines();

        Expr to = ParseAdditive();
        Expr? by = null;

        if (Current.Kind == TokenKind.By)
        {
            _ = Advance();
            by = ParseAdditive();
        }

        return new RangeExpr
        {
            From = from,
            To = to,
            By = by,
            Span = from.Span.Union((by ?? to).Span),
        };
    }

    private Expr ParseAdditive()
    {
        Expr left = ParseMultiplicative();

        while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            BinaryOperator op = Current.Kind == TokenKind.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            Token token = Advance();
            SkipNewlines();
            Expr right = ParseMultiplicative();
            left = Binary(op, left, right, token.Span);
        }

        return left;
    }

    private Expr ParseMultiplicative()
    {
        Expr left = ParseUnary();

        while (Current.Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
        {
            BinaryOperator op = Current.Kind switch
            {
                TokenKind.Star => BinaryOperator.Multiply,
                TokenKind.Slash => BinaryOperator.Divide,
                _ => BinaryOperator.Modulo,
            };

            Token token = Advance();
            SkipNewlines();
            Expr right = ParseUnary();
            left = Binary(op, left, right, token.Span);
        }

        return left;
    }

    private Expr ParseUnary()
    {
        if (Current.Kind is TokenKind.Minus or TokenKind.Not)
        {
            UnaryOperator op = Current.Kind == TokenKind.Minus ? UnaryOperator.Negate : UnaryOperator.Not;
            Token token = Advance();
            Expr operand = ParseUnary();

            return new UnaryExpr
            {
                Operator = op,
                Operand = operand,
                Span = TextSpan.FromBounds(token.Span.Start, operand.Span.End),
            };
        }

        return ParsePower();
    }

    /// <summary>
    /// Возведение в степень: правая ассоциативность и приоритет выше унарного минуса,
    /// поэтому <c>-2^2</c> равно <c>-4</c>, как в математической записи.
    /// </summary>
    private Expr ParsePower()
    {
        Expr left = ParsePostfixChain();

        if (Current.Kind != TokenKind.Caret) return left;

        Token token = Advance();
        SkipNewlines();
        Expr right = ParseUnary();

        return Binary(BinaryOperator.Power, left, right, token.Span);
    }

    private Expr ParsePostfixChain()
    {
        Expr expression = ParsePrimary();

        while (true)
        {
            switch (Current.Kind)
            {
                case TokenKind.Dot:
                    {
                        _ = Advance();
                        (string name, TextSpan nameSpan) = ExpectIdentifier("после точки указывается имя поля, функции либо метода");

                        expression = new MemberExpr
                        {
                            Target = expression,
                            Name = name,
                            NameSpan = nameSpan,
                            Span = expression.Span.Union(nameSpan),
                        };
                        break;
                    }

                case TokenKind.LParen:
                    expression = ParseCall(expression);
                    break;

                case TokenKind.LBracket:
                    expression = ParseIndex(expression);
                    break;

                default:
                    return expression;
            }
        }
    }

    private CallExpr ParseCall(Expr callee)
    {
        Token open = Advance();
        var call = new CallExpr { Callee = callee };

        bool seenNamed = false;
        int placeholders = 0;

        SkipNewlines();

        while (Current.Kind is not (TokenKind.RParen or TokenKind.EndOfFile))
        {
            var argument = new ArgumentNode();

            if (Current.Kind == TokenKind.Underscore)
            {
                Token underscore = Advance();
                argument.IsPlaceholder = true;
                argument.Span = underscore.Span;
                placeholders++;
            }
            else if (IsNameLike(Current) && Peek(1).Kind == TokenKind.Colon)
            {
                Token nameToken = Advance();
                _ = Advance();
                SkipNewlines();

                argument.Name = nameToken.Text;
                argument.NameSpan = nameToken.Span;
                argument.Value = ParseExpression();
                argument.Span = nameToken.Span.Union(argument.Value.Span);
                seenNamed = true;
            }
            else
            {
                argument.Value = ParseExpression();
                argument.Span = argument.Value.Span;

                if (seenNamed)
                {
                    Error(DiagnosticCodes.PositionalAfterNamed, argument.Span,
                        "позиционный аргумент после именованного",
                        "позиционным может быть только первый аргумент — предмет действия; остальные передаются по имени");
                }
            }

            call.Arguments.Add(argument);

            SkipNewlines();
            if (!Match(TokenKind.Comma)) break;
            SkipNewlines();
        }

        Token close = Expect(TokenKind.RParen, "аргументы вызова закрываются ')'");

        if (placeholders > 1)
        {
            Error(DiagnosticCodes.DuplicatePlaceholder, TextSpan.FromBounds(open.Span.Start, close.Span.End),
                "в звене конвейера допустим только один плейсхолдер '_'");
        }

        call.ArgumentsSpan = TextSpan.FromBounds(open.Span.Start, close.Span.End);
        call.Span = callee.Span.Union(call.ArgumentsSpan);

        return call;
    }

    private IndexExpr ParseIndex(Expr target)
    {
        Token open = Advance();
        var index = new IndexExpr { Target = target };

        while (Current.Kind is not (TokenKind.RBracket or TokenKind.EndOfFile))
        {
            var argument = new IndexArgument();

            if (Current.Kind == TokenKind.Colon)
            {
                Token colon = Advance();
                argument.IsAll = true;
                argument.Span = colon.Span;
            }
            else
            {
                argument.Value = ParseExpression();
                argument.Span = argument.Value.Span;
            }

            index.Arguments.Add(argument);

            if (!Match(TokenKind.Comma)) break;
        }

        Token close = Expect(TokenKind.RBracket, "индекс закрывается ']'");
        index.Span = target.Span.Union(TextSpan.FromBounds(open.Span.Start, close.Span.End));

        if (index.Arguments.Count == 0)
        {
            Error(DiagnosticCodes.UnexpectedToken, index.Span,
                "пустой индекс", "укажите индекс, срез 'a..b' либо ':' для всего измерения");
        }

        return index;
    }

    private Expr ParsePrimary()
    {
        Token token = Current;

        switch (token.Kind)
        {
            case TokenKind.Number:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.Num((double)token.Value!), Span = token.Span };

            case TokenKind.Duration:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.Dur((TimeSpan)token.Value!), Span = token.Span };

            case TokenKind.Date:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.Date((DateTime)token.Value!), Span = token.Span };

            case TokenKind.True:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.True, Span = token.Span };

            case TokenKind.False:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.False, Span = token.Span };

            case TokenKind.None:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.None, Span = token.Span };

            case TokenKind.Nan:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.Num(double.NaN), Span = token.Span };

            case TokenKind.Inf:
                _ = Advance();
                return new LiteralExpr { Value = ScriptValue.Num(double.PositiveInfinity), Span = token.Span };

            case TokenKind.String:
                return ParseStringExpression();

            case TokenKind.Underscore:
                _ = Advance();
                return new PlaceholderExpr { Span = token.Span };

            case TokenKind.Identifier:
                if (Peek(1).Kind == TokenKind.FatArrow) return ParseLambda();
                _ = Advance();
                return new NameExpr { Name = (string)token.Value!, Span = token.Span };

            case TokenKind.LParen:
                if (IsParenthesisedLambda()) return ParseLambda();
                return ParseGrouping();

            case TokenKind.LBracket:
                return ParseListLiteral();

            case TokenKind.Less:
                return ParseVectorLiteral();

            case TokenKind.LBrace:
                return LooksLikeRecord() ? ParseRecordLiteral() : ParseBlock();

            case TokenKind.If:
                return ParseIfExpression();

            default:
                Error(DiagnosticCodes.UnexpectedToken, token.Span,
                    $"здесь ожидалось выражение, встречено {Token.Describe(token.Kind)}");

                if (token.Kind is not (TokenKind.EndOfFile or TokenKind.Newline or TokenKind.RBrace)) _ = Advance();

                return new LiteralExpr { Value = ScriptValue.None, Span = token.Span };
        }
    }

    private Expr ParseGrouping()
    {
        Token open = Advance();
        SkipNewlines();

        Expr inner = ParseExpression();

        SkipNewlines();
        Token close = Expect(TokenKind.RParen, "скобка закрывается ')'");

        inner.Span = TextSpan.FromBounds(open.Span.Start, close.Span.End);
        return inner;
    }

    private Expr ParseIfExpression()
    {
        Token keyword = Advance();
        Expr condition = ParseExpression();
        BlockExpr then = ParseBlock();

        var node = new IfExpr
        {
            Condition = condition,
            Then = then,
            Span = TextSpan.FromBounds(keyword.Span.Start, then.Span.End),
        };

        if (!ElseAhead()) return node;

        SkipNewlines();
        _ = Advance();

        node.Else = Current.Kind == TokenKind.If ? ParseIfExpression() : ParseBlock();
        node.Span = TextSpan.FromBounds(keyword.Span.Start, node.Else.Span.End);

        return node;
    }

    private bool ElseAhead()
    {
        if (Current.Kind == TokenKind.Else) return true;
        if (Current.Kind != TokenKind.Newline) return false;

        int index = _position;

        while (index < _tokens.Count && _tokens[index].Kind is TokenKind.Newline or TokenKind.DocComment) index++;

        return index < _tokens.Count && _tokens[index].Kind == TokenKind.Else;
    }

    private Expr ParseLambda()
    {
        int start = Current.Span.Start;
        var lambda = new LambdaExpr();

        if (Current.Kind == TokenKind.Identifier)
        {
            lambda.Parameters.Add((string)Advance().Value!);
        }
        else
        {
            _ = Advance();

            while (Current.Kind is not (TokenKind.RParen or TokenKind.EndOfFile))
            {
                lambda.Parameters.Add(ExpectIdentifier("имя параметра лямбды").Name);
                if (!Match(TokenKind.Comma)) break;
            }

            _ = Expect(TokenKind.RParen, "список параметров лямбды закрывается ')'");
        }

        _ = Expect(TokenKind.FatArrow, "лямбда записывается как 'x => выражение'");

        lambda.Body = Current.Kind == TokenKind.LBrace && !LooksLikeRecord()
            ? ParseBlock()
            : ParseExpression();

        lambda.Span = TextSpan.FromBounds(start, lambda.Body.Span.End);

        return lambda;
    }

    private bool IsParenthesisedLambda()
    {
        int depth = 0;

        for (int index = _position; index < _tokens.Count; index++)
        {
            TokenKind kind = _tokens[index].Kind;

            if (kind == TokenKind.LParen)
            {
                depth++;
                continue;
            }

            if (kind == TokenKind.RParen)
            {
                depth--;
                if (depth > 0) continue;

                return index + 1 < _tokens.Count && _tokens[index + 1].Kind == TokenKind.FatArrow;
            }

            if (kind == TokenKind.EndOfFile) return false;
        }

        return false;
    }

    private Expr ParseListLiteral()
    {
        Token open = Advance();
        var list = new ListExpr();

        SkipNewlines();

        while (Current.Kind is not (TokenKind.RBracket or TokenKind.EndOfFile))
        {
            list.Items.Add(ParseExpression());
            SkipNewlines();

            if (!Match(TokenKind.Comma)) break;
            SkipNewlines();
        }

        Token close = Expect(TokenKind.RBracket, "список закрывается ']'");
        list.Span = TextSpan.FromBounds(open.Span.Start, close.Span.End);

        return list;
    }

    /// <summary>
    /// Литерал вектора <c>&lt;1, 2, 3&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Неоднозначности со сравнением нет: литерал допустим только там, где ожидается операнд,
    /// а сравнение — только там, где операнд уже разобран.
    /// </remarks>
    private Expr ParseVectorLiteral()
    {
        Token open = Advance();
        var vector = new VectorExpr();

        SkipNewlines();

        while (Current.Kind is not (TokenKind.Greater or TokenKind.EndOfFile))
        {
            // Элементы разбираются НИЖЕ уровня сравнений: иначе закрывающая '>' была бы
            // прочитана как оператор «больше», и <1, 2, 3> не дожило бы до конца.
            vector.Items.Add(ParseRange());
            SkipNewlines();

            if (!Match(TokenKind.Comma)) break;
            SkipNewlines();
        }

        Token close = Expect(TokenKind.Greater, "литерал вектора закрывается '>'");
        vector.Span = TextSpan.FromBounds(open.Span.Start, close.Span.End);

        return vector;
    }

    /// <summary>
    /// Отличает литерал записи от блока инструкций.
    /// </summary>
    /// <remarks>
    /// Переносы строк после <c>{</c> пропускаются: запись из нескольких полей почти всегда
    /// записывается в столбик, и по первой же лексеме после скобки — переводу строки — её
    /// нельзя было бы отличить от блока.
    /// </remarks>
    private bool LooksLikeRecord()
    {
        if (Current.Kind != TokenKind.LBrace) return false;

        int index = _position + 1;

        while (index < _tokens.Count && _tokens[index].Kind is TokenKind.Newline or TokenKind.DocComment) index++;

        if (index >= _tokens.Count) return false;

        Token first = _tokens[index];

        if (first.Kind is TokenKind.RBrace or TokenKind.Ellipsis) return true;

        if (!IsNameLike(first) && first.Kind != TokenKind.String) return false;

        return index + 1 < _tokens.Count && _tokens[index + 1].Kind == TokenKind.Colon;
    }

    private Expr ParseRecordLiteral()
    {
        Token open = Advance();
        var record = new RecordExpr();

        SkipNewlines();

        while (Current.Kind is not (TokenKind.RBrace or TokenKind.EndOfFile))
        {
            var field = new RecordFieldNode();

            if (Current.Kind == TokenKind.Ellipsis)
            {
                Token ellipsis = Advance();
                field.IsSpread = true;
                field.Value = ParseExpression();
                field.Span = TextSpan.FromBounds(ellipsis.Span.Start, field.Value.Span.End);
            }
            else
            {
                Token nameToken = Advance();

                field.Name = nameToken.Kind == TokenKind.String
                    ? (string?)nameToken.Value ?? nameToken.Text
                    : nameToken.Text;

                _ = Expect(TokenKind.Colon, "поле записи записывается как 'имя: значение'");
                SkipNewlines();

                field.Value = ParseExpression();
                field.Span = nameToken.Span.Union(field.Value.Span);
            }

            record.Fields.Add(field);

            SkipNewlines();
            if (!Match(TokenKind.Comma)) break;
            SkipNewlines();
        }

        Token close = Expect(TokenKind.RBrace, "запись закрывается '}'");
        record.Span = TextSpan.FromBounds(open.Span.Start, close.Span.End);

        return record;
    }

    private Expr ParseStringExpression()
    {
        Token token = Advance();
        IReadOnlyList<StringPart> parts = token.Parts ?? [];

        bool interpolated = false;
        foreach (StringPart part in parts)
        {
            if (part.IsExpression) { interpolated = true; break; }
        }

        if (!interpolated)
        {
            string text = parts.Count == 0 ? string.Empty : parts[0].Text ?? string.Empty;
            return new LiteralExpr { Value = ScriptValue.Str(text), Span = token.Span };
        }

        var node = new InterpolationExpr { Span = token.Span };

        foreach (StringPart part in parts)
        {
            node.Parts.Add(part.IsExpression
                ? new InterpolationPart { Expression = ParseSubExpression(part.Expression!, part.ExpressionStart) }
                : new InterpolationPart { Text = part.Text });
        }

        return node;
    }

    private Expr ParseSubExpression(string text, int start)
    {
        var tokens = new Lexer(_source, _diagnostics, start, start + text.Length).Lex();
        var parser = new Parser(_source, _diagnostics, tokens);

        return parser.ParseExpressionEntry();
    }

    /// <summary>
    /// Годится ли лексема в качестве имени аргумента или поля записи.
    /// </summary>
    /// <remarks>
    /// Ключевые слова здесь допустимы намеренно: имена аргументов задаёт привязка к C#, и
    /// запрет на <c>in:</c> или <c>for:</c> сделал бы часть библиотеки недоступной из-за
    /// совпадения с грамматикой, к которой она отношения не имеет.
    /// </remarks>
    private static bool IsNameLike(Token token) =>
        token.Kind == TokenKind.Identifier || (token.Text.Length > 0 && char.IsLetter(token.Text[0]));

    private static BinaryExpr Binary(BinaryOperator op, Expr left, Expr right, TextSpan operatorSpan) => new()
    {
        Operator = op,
        Left = left,
        Right = right,
        OperatorSpan = operatorSpan,
        Span = left.Span.Union(right.Span),
    };
}

using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Syntax.Ast;

namespace AI.Script.Syntax;

public sealed partial class Parser
{
    private Stmt? ParseStatement(bool topLevel)
    {
        var attributes = new List<AttributeNode>();

        while (Current.Kind == TokenKind.At)
        {
            attributes.Add(ParseAttribute());
            SkipNewlines();
        }

        if (attributes.Count > 0 && Current.Kind is not (TokenKind.Fn or TokenKind.Stage))
        {
            Error(DiagnosticCodes.UnexpectedToken, attributes[0].Span,
                "атрибут допустим только перед 'fn' и 'stage'",
                "атрибуты задают поведение объявления: @cache, @retry(n), @timeout(d)");
        }

        switch (Current.Kind)
        {
            case TokenKind.Let: return ParseLet();
            case TokenKind.Set: return ParseSet();
            case TokenKind.Fn:
            case TokenKind.Stage: return ParseFunction(topLevel, attributes);
            case TokenKind.For: return ParseFor();
            case TokenKind.While: return ParseWhile();
            case TokenKind.Try: return ParseTry();
            case TokenKind.Use: return ParseUse();
            case TokenKind.Emit: return ParseEmit();
            case TokenKind.Show: return ParseShow();
            case TokenKind.Assert: return ParseAssert();

            case TokenKind.Break:
                return new BreakStmt { Span = Advance().Span };

            case TokenKind.Continue:
                return new ContinueStmt { Span = Advance().Span };

            case TokenKind.Return:
                return ParseReturn();

            // Фигурная скобка на месте инструкции — это блок, кроме случая, когда за ней
            // читается запись: значением тела функции вполне может быть '{ поле: ... }', и
            // разбор такого текста блоком давал непонятную жалобу на двоеточие.
            case TokenKind.LBrace when !LooksLikeRecord():
                {
                    BlockExpr block = ParseBlock();
                    return new ExpressionStmt { Expression = block, Span = block.Span };
                }

            case TokenKind.EndOfFile:
                return null;

            default:
                {
                    Expr expression = ParseExpression();
                    return new ExpressionStmt { Expression = expression, Span = expression.Span };
                }
        }
    }

    private AttributeNode ParseAttribute()
    {
        Token at = Advance();
        (string name, TextSpan nameSpan) = ExpectIdentifier("после '@' указывается имя атрибута");
        var node = new AttributeNode { Name = name, Span = TextSpan.FromBounds(at.Span.Start, nameSpan.End) };

        if (Current.Kind != TokenKind.LParen) return node;

        _ = Advance();

        while (Current.Kind is not (TokenKind.RParen or TokenKind.EndOfFile))
        {
            node.Arguments.Add(ParseExpression());
            if (!Match(TokenKind.Comma)) break;
        }

        Token close = Expect(TokenKind.RParen, "аргументы атрибута закрываются ')'");
        node.Span = TextSpan.FromBounds(at.Span.Start, close.Span.End);

        return node;
    }

    private OptionsStmt ParseOptions()
    {
        Token keyword = Advance();
        var options = new OptionsStmt { Span = keyword.Span };

        _ = Expect(TokenKind.LBrace, "блок options записывается как: options { seed: 42 }");
        SkipNewlines();

        while (Current.Kind is not (TokenKind.RBrace or TokenKind.EndOfFile))
        {
            (string name, TextSpan nameSpan) = ExpectIdentifier("поле блока options");
            _ = Expect(TokenKind.Colon, "поле блока options записывается как 'имя: значение'");

            Expr value = ParseExpression();
            ScriptValue? constant = ConstantValue(value);

            if (constant == null)
            {
                Error(DiagnosticCodes.UnexpectedToken, value.Span,
                    $"значение опции '{name}' должно быть литералом",
                    "в блоке options допустимы только литералы: числа, строки, длительности, true/false");
            }
            else
            {
                options.Fields.Add(new OptionFieldNode
                {
                    Name = name,
                    Value = constant.Value,
                    Span = TextSpan.FromBounds(nameSpan.Start, value.Span.End),
                });
            }

            _ = Match(TokenKind.Comma);
            SkipNewlines();
        }

        Token close = Expect(TokenKind.RBrace, "блок options закрывается '}'");
        options.Span = TextSpan.FromBounds(keyword.Span.Start, close.Span.End);

        return options;
    }

    private static ScriptValue? ConstantValue(Expr expression) => expression switch
    {
        LiteralExpr literal => literal.Value,
        UnaryExpr { Operator: UnaryOperator.Negate, Operand: LiteralExpr { Value.Type: ScriptType.Num } inner }
            => ScriptValue.Num(-inner.Value.RawNumber),
        _ => null,
    };

    private LetStmt ParseLet()
    {
        Token keyword = Advance();
        (string name, TextSpan nameSpan) = ExpectIdentifier("после 'let' указывается имя переменной");

        ScriptType? declared = Match(TokenKind.Colon) ? ParseTypeAnnotation() : null;

        _ = Expect(TokenKind.Assign, "объявление записывается как 'let имя = значение'");
        SkipNewlines();

        Expr value = ParseExpression();

        return new LetStmt
        {
            Name = name,
            NameSpan = nameSpan,
            DeclaredType = declared,
            Value = value,
            Span = TextSpan.FromBounds(keyword.Span.Start, value.Span.End),
        };
    }

    private SetStmt ParseSet()
    {
        Token keyword = Advance();
        Expr target = ParsePostfixChain();

        if (target is not (NameExpr or IndexExpr or MemberExpr))
        {
            Error(DiagnosticCodes.UnexpectedToken, target.Span,
                "присваивать можно имени, элементу по индексу либо полю записи");
        }

        BinaryOperator? compound = Current.Kind switch
        {
            TokenKind.PlusAssign => BinaryOperator.Add,
            TokenKind.MinusAssign => BinaryOperator.Subtract,
            TokenKind.StarAssign => BinaryOperator.Multiply,
            TokenKind.SlashAssign => BinaryOperator.Divide,
            _ => null,
        };

        if (compound != null) _ = Advance();
        else _ = Expect(TokenKind.Assign, "присваивание записывается как 'set имя = значение'");

        SkipNewlines();

        Expr value = ParseExpression();

        return new SetStmt
        {
            Target = target,
            Compound = compound,
            Value = value,
            Span = TextSpan.FromBounds(keyword.Span.Start, value.Span.End),
        };
    }

    private FunctionDeclStmt ParseFunction(bool topLevel, List<AttributeNode> attributes)
    {
        bool isStage = Current.Kind == TokenKind.Stage;
        Token keyword = Advance();

        string? documentation = _pendingDocumentation;
        _pendingDocumentation = null;

        (string name, TextSpan nameSpan) = ExpectIdentifier(isStage
            ? "стадия объявляется как 'stage имя(параметры) { ... }'"
            : "функция объявляется как 'fn имя(параметры) { ... }'");

        var declaration = new FunctionDeclStmt
        {
            Name = name,
            NameSpan = nameSpan,
            IsStage = isStage,
            Documentation = documentation,
        };

        declaration.Attributes.AddRange(attributes);

        if (!topLevel)
        {
            Error(DiagnosticCodes.NotTopLevel, keyword.Span,
                isStage
                    ? "стадия объявляется только на верхнем уровне файла"
                    : "функция объявляется только на верхнем уровне файла",
                "вложенные объявления не нужны: функции файла видны во всём файле");
        }

        _ = Expect(TokenKind.LParen, "после имени идёт список параметров в скобках");
        ParseParameters(declaration.Parameters);
        _ = Expect(TokenKind.RParen, "список параметров закрывается ')'");

        if (Match(TokenKind.Arrow)) declaration.ReturnType = ParseTypeAnnotation();

        declaration.Body = ParseBlock();
        declaration.Span = TextSpan.FromBounds(keyword.Span.Start, declaration.Body.Span.End);

        return declaration;
    }

    private void ParseParameters(List<ParameterNode> parameters)
    {
        while (Current.Kind is not (TokenKind.RParen or TokenKind.EndOfFile))
        {
            (string name, TextSpan span) = ExpectIdentifier("имя параметра");
            var parameter = new ParameterNode { Name = name, Span = span };

            if (Match(TokenKind.Colon)) parameter.DeclaredType = ParseTypeAnnotation();
            if (Match(TokenKind.Assign)) parameter.Default = ParseExpression();

            parameters.Add(parameter);

            if (!Match(TokenKind.Comma)) break;
        }
    }

    private ForStmt ParseFor()
    {
        Token keyword = Advance();
        var statement = new ForStmt();

        if (Match(TokenKind.LParen))
        {
            while (Current.Kind is not (TokenKind.RParen or TokenKind.EndOfFile))
            {
                statement.Names.Add(ExpectIdentifier("имя переменной цикла").Name);
                if (!Match(TokenKind.Comma)) break;
            }

            _ = Expect(TokenKind.RParen, "список имён закрывается ')'");
        }
        else
        {
            statement.Names.Add(ExpectIdentifier("цикл записывается как 'for x in ... { }'").Name);
        }

        _ = Expect(TokenKind.In, "цикл записывается как 'for x in последовательность { }'");

        statement.Iterable = ParseExpression();

        if (Match(TokenKind.By)) statement.By = ParseExpression();

        statement.Body = ParseBlock();
        statement.Span = TextSpan.FromBounds(keyword.Span.Start, statement.Body.Span.End);

        return statement;
    }

    private WhileStmt ParseWhile()
    {
        Token keyword = Advance();
        Expr condition = ParseExpression();
        BlockExpr body = ParseBlock();

        return new WhileStmt
        {
            Condition = condition,
            Body = body,
            Span = TextSpan.FromBounds(keyword.Span.Start, body.Span.End),
        };
    }

    private ReturnStmt ParseReturn()
    {
        Token keyword = Advance();
        var statement = new ReturnStmt { Span = keyword.Span };

        if (Current.Kind is TokenKind.Newline or TokenKind.RBrace or TokenKind.EndOfFile) return statement;

        statement.Value = ParseExpression();
        statement.Span = TextSpan.FromBounds(keyword.Span.Start, statement.Value.Span.End);

        return statement;
    }

    private TryStmt ParseTry()
    {
        Token keyword = Advance();
        BlockExpr body = ParseBlock();

        SkipNewlines();
        _ = Expect(TokenKind.Catch, "перехват записывается как 'try { ... } catch e { ... }'");

        (string name, _) = ExpectIdentifier("после 'catch' указывается имя переменной с описанием отказа");
        BlockExpr handler = ParseBlock();

        return new TryStmt
        {
            Body = body,
            ErrorName = name,
            Handler = handler,
            Span = TextSpan.FromBounds(keyword.Span.Start, handler.Span.End),
        };
    }

    private UseStmt ParseUse()
    {
        Token keyword = Advance();
        (string ns, _) = ExpectIdentifier("после 'use' указывается пространство имён");

        _ = Expect(TokenKind.As, "псевдоним записывается как 'use пространство as имя'");

        (string alias, TextSpan aliasSpan) = ExpectIdentifier("после 'as' указывается псевдоним");

        return new UseStmt
        {
            Namespace = ns,
            Alias = alias,
            Span = TextSpan.FromBounds(keyword.Span.Start, aliasSpan.End),
        };
    }

    private EmitStmt ParseEmit()
    {
        Token keyword = Advance();
        (string name, TextSpan nameSpan) = ExpectIdentifier("результат объявляется как 'emit имя = значение'");

        _ = Expect(TokenKind.Assign, "результат объявляется как 'emit имя = значение'");
        SkipNewlines();

        Expr value = ParseExpression();

        return new EmitStmt
        {
            Name = name,
            NameSpan = nameSpan,
            Value = value,
            Span = TextSpan.FromBounds(keyword.Span.Start, value.Span.End),
        };
    }

    private ShowStmt ParseShow()
    {
        Token keyword = Advance();
        Expr value = ParseExpression();

        return new ShowStmt
        {
            Value = value,
            Span = TextSpan.FromBounds(keyword.Span.Start, value.Span.End),
        };
    }

    private AssertStmt ParseAssert()
    {
        Token keyword = Advance();
        Expr condition = ParseExpression();
        Expr? message = null;

        if (Match(TokenKind.Comma))
        {
            SkipNewlines();
            message = ParseExpression();
        }

        return new AssertStmt
        {
            Condition = condition,
            Message = message,
            Span = TextSpan.FromBounds(keyword.Span.Start, (message ?? condition).Span.End),
        };
    }

    /// <summary>
    /// Блок в фигурных скобках; открывающая скобка может стоять на следующей строке.
    /// </summary>
    /// <remarks>
    /// Отступы в языке не значимы, поэтому запрещать привычный по C# перенос скобки не за что.
    /// </remarks>
    private BlockExpr ParseBlock()
    {
        if (Current.Kind == TokenKind.Newline && PeekPast(TokenKind.LBrace)) SkipNewlines();

        Token open = Expect(TokenKind.LBrace, "блок заключается в фигурные скобки");
        var block = new BlockExpr();

        SkipNewlines();

        while (Current.Kind is not (TokenKind.RBrace or TokenKind.EndOfFile))
        {
            int before = _position;

            Stmt? statement = ParseStatement(topLevel: false);
            if (statement != null) block.Statements.Add(statement);

            EndStatement();
            SkipNewlines();

            if (_position == before) _ = Advance();
        }

        Token close = Expect(TokenKind.RBrace, "блок закрывается '}'");
        block.Span = TextSpan.FromBounds(open.Span.Start, close.Span.End);

        return block;
    }
}

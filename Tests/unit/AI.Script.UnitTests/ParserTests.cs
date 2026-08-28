using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Синтаксис: приоритеты, конвейер, литералы, неоднозначности.</summary>
public sealed class ParserTests
{
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("2 ^ 3 ^ 2", 512)]          // правая ассоциативность
    [InlineData("-2 ^ 2", -4)]              // '^' выше унарного минуса, как в математике
    [InlineData("(-2) ^ 2", 4)]
    [InlineData("10 - 3 - 2", 5)]           // левая ассоциативность
    [InlineData("10 / 4", 2.5)]
    [InlineData("10 % 3", 1)]
    [InlineData("2 * 3 + 4 * 5", 26)]
    public void Parser_Precedence_MatchesSpecification(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 12);

    [Theory]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("1 < 2 && 3 > 2", true)]
    [InlineData("!true", false)]
    [InlineData("!(1 > 2)", true)]
    public void Parser_LogicalOperators_Work(string expression, bool expected) =>
        Assert.Equal(expected, Script.Flag(expression));

    [Fact]
    public void Parser_ChainedComparison_IsRejected()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = 1 < 2 < 3");

        Assert.Equal(DiagnosticCodes.ChainedComparison, error.Code);
        Assert.Contains("&&", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_PipeAcrossNewline_ContinuesExpression()
    {
        const string source = """
            let xs = [1, 2, 3]
            emit r =
                xs
                |> core.map(x => x * 2)
                |> core.reduce((a, b) => a + b)
            """;

        Assert.Equal(12.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Parser_PipeFeedsFirstPositionalArgument()
    {
        Assert.Equal(3.0, Script.Number("[1, 2, 3] |> len()"));
    }

    [Fact]
    public void Parser_PipePlaceholder_TakesNamedSlot()
    {
        // При наличии '_' значение конвейера идёт ТОЛЬКО в него и первым аргументом не встаёт.
        Assert.Equal("a.b", Script.Text("\".\" |> str.replace(\"a,b\", from: \",\", to: _)"));
    }

    [Fact]
    public void Parser_PipeTargetMustBeCall()
    {
        Diagnostic error = Script.CheckFailsWith("let x = 1\nemit r = x |> x");

        Assert.Equal(DiagnosticCodes.PipeTargetNotCall, error.Code);
    }

    [Fact]
    public void Parser_TwoPlaceholders_AreRejected()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = 1 |> math.pow(_, _)");

        Assert.Equal(DiagnosticCodes.DuplicatePlaceholder, error.Code);
    }

    [Fact]
    public void Parser_VectorLiteral_IsNotComparison()
    {
        Assert.Equal(3.0, Script.Number("len(<1, 2, 3>)"));
    }

    [Fact]
    public void Parser_ComparisonStillWorksAfterVectorLiteral()
    {
        Assert.True(Script.Flag("1 < 2"));
    }

    [Fact]
    public void Parser_RecordLiteralInExpressionPosition()
    {
        Assert.Equal(2.0, Script.Number("{ a: 1, b: 2 }.b"));
    }

    /// <summary>
    /// Значением тела функции бывает запись, и её многострочная запись не должна разбираться
    /// как блок: раньше такой текст давал непонятную жалобу на двоеточие.
    /// </summary>
    [Fact]
    public void Parser_RecordLiteralAsFunctionValue()
    {
        RunResult result = Script.RunOk("""
            fn описание(x: num) -> record {
                let удвоенное = x * 2

                {
                    исходное: x,
                    удвоенное: удвоенное
                }
            }

            let r = описание(21)

            emit исходное = r.исходное
            emit удвоенное = r.удвоенное
            """);

        Assert.Equal(21.0, result.Emitted["исходное"]);
        Assert.Equal(42.0, result.Emitted["удвоенное"]);
    }

    [Fact]
    public void Parser_BraceInStatementPosition_IsBlock()
    {
        const string source = """
            let x = 1
            {
                print("внутри блока")
            }
            emit r = x
            """;

        Assert.Equal(1.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Parser_LambdaWithBlockBody()
    {
        const string source = """
            let f = x => {
                let y = x + 1
                y * 2
            }
            emit r = f(3)
            """;

        Assert.Equal(8.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Parser_LambdaWithTwoParameters()
    {
        Assert.Equal(12.0, Script.Number("((a, b) => a * b)(3, 4)"));
    }

    [Fact]
    public void Parser_IfAsExpression()
    {
        Assert.Equal(10.0, Script.Number("if 2 > 1 { 10 } else { 20 }"));
    }

    [Fact]
    public void Parser_IfExpressionWithoutElse_IsRejectedAtRuntime()
    {
        Diagnostic error = Script.FailsWith("emit r = if 1 > 2 { 1 }");

        Assert.Contains("else", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_ElseOnNextLine_IsAccepted()
    {
        const string source = """
            let x = 5
            if x > 10
            {
                emit r = "много"
            }
            else
            {
                emit r = "мало"
            }
            """;

        Assert.Equal("мало", Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Parser_ArgumentsMayWrapAcrossLines()
    {
        const string source = """
            emit r = math.clamp(
                5,
                low: 0,
                high: 3)
            """;

        Assert.Equal(3.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Parser_KeywordAsArgumentName_IsAllowed()
    {
        // Имена аргументов задаёт привязка к C#; совпадение с ключевым словом языка не должно
        // делать функцию недоступной.
        Assert.Equal(3.0, Script.Number("math.log(8, base: 2)"));
    }

    [Fact]
    public void Parser_PositionalAfterNamed_IsRejected()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.clamp(1, low: 0, 5)");

        Assert.Equal(DiagnosticCodes.PositionalAfterNamed, error.Code);
    }

    [Fact]
    public void Parser_SemicolonIsNotAccepted()
    {
        Diagnostic error = Script.CheckFailsWith("let a = 1; let b = 2");

        Assert.Equal(DiagnosticCodes.InvalidCharacter, error.Code);
        Assert.Contains("точка с запятой", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_StrayBrace_DoesNotHang()
    {
        CheckResult result = Script.Check("let x = 1\n}\nlet y = 2");

        Assert.False(result.Success);
    }

    [Fact]
    public void Parser_MisplacedOptions_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("let x = 1\noptions { seed: 1 }");

        Assert.Equal(DiagnosticCodes.MisplacedOptions, error.Code);
    }

    [Fact]
    public void Parser_NestedFunction_IsRejected()
    {
        const string source = """
            fn outer() {
                fn inner() { 1 }
                inner()
            }
            emit r = outer()
            """;

        Diagnostic error = Script.CheckFailsWith(source);

        Assert.Equal(DiagnosticCodes.NotTopLevel, error.Code);
    }

    [Fact]
    public void Parser_RangeWithStep()
    {
        Assert.Equal(5.0, Script.Number("len(0..10 by 2)"));
    }

    [Fact]
    public void Parser_DiagnosticRendersSourceFragment()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.sqrtt(4)");
        string rendered = error.Render();

        Assert.Contains("script.ais:1:", rendered, StringComparison.Ordinal);
        Assert.Contains("^", rendered, StringComparison.Ordinal);
        Assert.Contains("sqrt", rendered, StringComparison.Ordinal);
    }
}

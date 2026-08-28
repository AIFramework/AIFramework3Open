using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Проверка до запуска — главный ответ языка на вопрос «почему не просто вызовы инструментов».
/// Эти тесты фиксируют, что типичная ошибка модели ловится без единого вычисления.
/// </summary>
public sealed class CheckerTests
{
    [Fact]
    public void Checker_UnknownNamespace_IsReportedAsUnboundName()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = quantum.entangle(1)\n");

        Assert.Equal(DiagnosticCodes.UnboundName, error.Code);
    }

    [Fact]
    public void Checker_TypoInFunctionName_SuggestsClosest()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.sqrtt(4)");

        Assert.Equal(DiagnosticCodes.UnknownFunction, error.Code);
        Assert.Contains("math.sqrt", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_TypoInArgumentName_SuggestsClosest()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.clamp(5, lo: 0, high: 1)");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("low", error.Hint, StringComparison.Ordinal);
        Assert.Contains("сигнатура", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_MissingRequiredArgument_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.pow(2)");

        Assert.Equal(DiagnosticCodes.MissingArgument, error.Code);
        Assert.Contains("y", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_ExtraPositionalArgument_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.sqrt(4, 5)");

        Assert.Equal(DiagnosticCodes.ExtraPositional, error.Code);
        Assert.Contains("по имени", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_DuplicateArgument_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = math.clamp(5, low: 0, low: 1)");

        Assert.Equal(DiagnosticCodes.DuplicateArgument, error.Code);
    }

    [Fact]
    public void Checker_RequiredArgumentsMayBePositional()
    {
        CheckResult result = Script.Check("emit r = math.pow(2, 10)");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Checker_UnboundName_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = unknown + 1");

        Assert.Equal(DiagnosticCodes.UnboundName, error.Code);
    }

    [Fact]
    public void Checker_TypoInVariableName_SuggestsClosest()
    {
        Diagnostic error = Script.CheckFailsWith("let revenue = 10\nemit r = revenu");

        Assert.Contains("revenue", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_DuplicateLet_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("let x = 1\nlet x = 2");

        Assert.Equal(DiagnosticCodes.DuplicateLet, error.Code);
        Assert.Contains("set x", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_SetOfUnboundName_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("set x = 1");

        Assert.Equal(DiagnosticCodes.UnboundSet, error.Code);
        Assert.Contains("let x", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_Shadowing_IsWarningNotError()
    {
        const string source = """
            let x = 1
            if true {
                let x = 2
                print(x)
            }
            emit r = x
            """;

        CheckResult result = Script.Check(source);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.Shadowing);
    }

    [Fact]
    public void Checker_BreakOutsideLoop_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("break");

        Assert.Equal(DiagnosticCodes.NotInLoop, error.Code);
    }

    [Fact]
    public void Checker_ReturnOutsideFunction_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("return 1");

        Assert.Equal(DiagnosticCodes.ReturnOutsideFunction, error.Code);
        Assert.Contains("emit", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_DuplicateFunction_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("fn f() { 1 }\nfn f() { 2 }");

        Assert.Equal(DiagnosticCodes.DuplicateFunction, error.Code);
    }

    [Fact]
    public void Checker_UnknownNamespaceInUse_IsReported()
    {
        Diagnostic error = Script.CheckFailsWith("use nope as n");

        Assert.Equal(DiagnosticCodes.UnknownNamespace, error.Code);
    }

    [Fact]
    public void Checker_AliasResolvesNamespace()
    {
        const string source = """
            use math as m
            emit r = m.sqrt(16)
            """;

        Assert.Equal(4.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Checker_FunctionsAreVisibleBeforeDeclaration()
    {
        const string source = """
            emit r = double(21)

            fn double(x) { x * 2 }
            """;

        Assert.Equal(42.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Checker_StageSeesOnlyItsParameters()
    {
        const string source = """
            let outside = 1

            stage f(x) { x + outside }

            emit r = f(1)
            """;

        Diagnostic error = Script.CheckFailsWith(source);

        Assert.Equal(DiagnosticCodes.UnboundName, error.Code);
        Assert.Contains("outside", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Checker_StageCannotEmit()
    {
        const string source = """
            stage f(x) {
                emit inner = x
                x
            }

            emit r = f(1)
            """;

        CheckResult result = Script.Check(source);

        Assert.False(result.Success);
    }

    /// <summary>
    /// Стадия с атрибутами проходит проверку молча: пока кэша не было, здесь выдавалось
    /// замечание «ещё не реализовано», и его исчезновение — часть приёмки этапа M4.
    /// </summary>
    [Fact]
    public void Checker_StageAttributes_AreAccepted()
    {
        const string source = """
            @cache
            @retry(3)
            @timeout(90s)
            stage f(x) { x }

            emit r = f(1)
            """;

        CheckResult result = Script.Check(source);

        Assert.True(result.Success, result.Render());
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Checker_AttributeOnPlainFunction_IsRejected()
    {
        Diagnostic error = Script.CheckFailsWith("@cache\nfn f(x) { x }\nemit r = f(1)");

        Assert.Equal(DiagnosticCodes.UnexpectedToken, error.Code);
    }

    [Fact]
    public void Checker_UnknownAttribute_IsRejected()
    {
        Diagnostic error = Script.CheckFailsWith("@memoize\nstage f(x) { x }\nemit r = f(1)");

        Assert.Equal(DiagnosticCodes.UnexpectedToken, error.Code);
    }

    [Fact]
    public void Checker_KnownConstantsAreBound()
    {
        CheckResult result = Script.Check("emit r = pi + e + tau + phi");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Checker_SeededNamesAreBound()
    {
        var host = Script.Host();
        CheckResult result = host.Check("emit r = prices", "script.ais", ["prices"]);

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Checker_DoesNotRunTheScript()
    {
        // Проверка обязана быть без побочных эффектов: иначе цикл «проверил → исправил»
        // стоил бы столько же, сколько прогон.
        CheckResult result = Script.Check("print(\"побочный эффект\")");

        Assert.True(result.Success);
    }

    [Fact]
    public void Checker_LambdaParametersAreBound()
    {
        CheckResult result = Script.Check("emit r = [1, 2] |> core.map(x => x + 1)");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Checker_LoopVariableIsBound()
    {
        CheckResult result = Script.Check("let s = 0\nfor i in 0..3 { set s += i }\nemit r = s");

        Assert.True(result.Success, result.Render());
    }
}

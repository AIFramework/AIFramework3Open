using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Syntax;

namespace AI.Script.UnitTests;

/// <summary>Вывод типов и проверки, которые он делает возможными.</summary>
public sealed class TypeCheckTests
{
    /// <summary>
    /// Эталонный набор типовых ошибок модели: каждая обязана ловиться проверкой до запуска.
    /// </summary>
    /// <remarks>
    /// Это приёмочный критерий этапа M2. Набор растёт по мере того, как обнаруживаются новые
    /// способы ошибиться, — и каждый новый способ здесь фиксируется, чтобы не вернуться.
    /// </remarks>
    [Theory]
    [InlineData("emit r = math.sqrtt(4)", DiagnosticCodes.UnknownFunction)]
    [InlineData("emit r = math.clamp(5, lo: 0)", DiagnosticCodes.UnknownArgument)]
    [InlineData("emit r = math.pow(2)", DiagnosticCodes.MissingArgument)]
    [InlineData("emit r = core.round(1.5, 2)", DiagnosticCodes.ExtraPositional)]
    [InlineData("let revenue = 1\nemit r = revenu", DiagnosticCodes.UnboundName)]
    [InlineData("let x = 1\nlet x = 2", DiagnosticCodes.DuplicateLet)]
    [InlineData("set x = 1", DiagnosticCodes.UnboundSet)]
    [InlineData("let n = 5\nif n { print(\"да\") }", DiagnosticCodes.ConditionNotBool)]
    [InlineData("emit r = vec.sum(\"строка\")", DiagnosticCodes.TypeMismatch)]
    [InlineData("emit r = 1 + \"два\"", DiagnosticCodes.BadOperandTypes)]
    [InlineData("emit r = 1 < 2 < 3", DiagnosticCodes.ChainedComparison)]
    [InlineData("let x = 5\nemit r = x(1)", DiagnosticCodes.NotCallable)]
    [InlineData("use nope as n", DiagnosticCodes.UnknownNamespace)]
    [InlineData("emit r = nope.thing(1)", DiagnosticCodes.UnboundName)]
    [InlineData("emit r = math.clamp(1, low: 0, low: 1)", DiagnosticCodes.DuplicateArgument)]
    [InlineData("return 1", DiagnosticCodes.ReturnOutsideFunction)]
    [InlineData("fn f(a, b) { a + b }\nemit r = f(1)", DiagnosticCodes.MissingArgument)]
    [InlineData("fn f(a) { a }\nemit r = f(1, 2)", DiagnosticCodes.ExtraPositional)]
    [InlineData("for x in 5 { print(x) }", DiagnosticCodes.NotIterable)]
    [InlineData("let v: num = \"строка\"", DiagnosticCodes.DeclaredTypeMismatch)]
    [InlineData("emit r = !1", DiagnosticCodes.BadOperandTypes)]
    [InlineData("fn f(x: vec) -> num { x }\nemit r = f(<1>)", DiagnosticCodes.DeclaredTypeMismatch)]
    public void Check_CatchesTypicalMistake(string source, string expected)
    {
        IReadOnlyList<string> codes = Script.CheckCodes(source);

        Assert.Contains(expected, codes);
    }

    [Theory]
    [InlineData("emit r = math.sqrt(16)")]
    [InlineData("emit r = math.pow(2, 10)")]
    [InlineData("emit r = core.round(1.5, digits: 2)")]
    [InlineData("emit r = [1, 2] |> core.map(x => x * 2)")]
    [InlineData("emit r = vec.sum([1, 2, 3])")]
    [InlineData("emit r = vec.sum(0..10)")]
    [InlineData("let t = table.of({ a: <1, 2> })\nemit r = table.to_matrix(t)")]
    [InlineData("let m = mat.eye(2)\nemit r = m * m")]
    [InlineData("let m = mat.eye(2)\nemit r = m * <1, 1>")]
    [InlineData("emit r = @2026-01-01 + 1d")]
    [InlineData("emit r = @2026-01-02 - @2026-01-01")]
    [InlineData("emit r = 30s + 1m")]
    [InlineData("fn f(x: vec) -> num { vec.sum(x) }\nemit r = f(<1, 2>)")]
    [InlineData("let xs = [1]\nfor x in xs { print(x) }")]
    [InlineData("let t = table.of({ a: <1> })\nfor row in t { print(row.a) }")]
    [InlineData("emit r = if 1 > 0 { \"да\" } else { \"нет\" }")]
    public void Check_AcceptsCorrectScript(string source)
    {
        CheckResult result = Script.Check(source);

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Check_UnknownTypeDoesNotProduceDiagnostics()
    {
        // Проверка, которая ошибается, хуже отсутствующей: там, где тип неизвестен, она молчит.
        CheckResult result = Script.Check("""
            let xs = [1, "два"]
            for x in xs {
                emit r = x + 1
            }
            """);

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Check_FloatEquality_IsWarningNotError()
    {
        IReadOnlyList<string> warnings = Script.CheckWarnings("let a = 0.1 + 0.2\nemit r = a == 0.3");

        Assert.Contains(DiagnosticCodes.ExactFloatComparison, warnings);
        Assert.True(Script.Check("let a = 0.1 + 0.2\nemit r = a == 0.3").Success);
    }

    [Fact]
    public void Check_IntegerEquality_IsNotWarned()
    {
        Assert.DoesNotContain(DiagnosticCodes.ExactFloatComparison, Script.CheckWarnings("let n = 1\nemit r = n == 1"));
    }

    [Fact]
    public void Check_ComparingDifferentTypes_IsWarned()
    {
        IReadOnlyList<string> warnings = Script.CheckWarnings("emit r = 1 == \"1\"");

        Assert.Contains(DiagnosticCodes.ComparingDifferentTypes, warnings);
    }

    [Fact]
    public void Check_MatrixTimesMatrix_HintsAtHadamard()
    {
        Diagnostic error = Script.CheckFailsWith("let a = mat.eye(2)\nemit r = a % a");

        Assert.Equal(DiagnosticCodes.BadOperandTypes, error.Code);
    }

    [Fact]
    public void Check_ArgumentTypeError_ShowsSignature()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = str.upper(42)");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("ожидался str", error.Message, StringComparison.Ordinal);
        Assert.Contains("сигнатура", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_UserFunctionArgumentType_IsVerified()
    {
        Diagnostic error = Script.CheckFailsWith("fn f(x: vec) { x }\nemit r = f(\"строка\")");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("сигнатура: f(x: vec)", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_UserFunctionUnknownParameter_SuggestsClosest()
    {
        // Диагностик здесь две: неизвестный параметр и пропущенный обязательный. Обе верны,
        // поэтому ищем нужную по коду, а не по порядку.
        Diagnostic error = Script.CheckDiagnostic(
            "fn f(alpha) { alpha }\nemit r = f(alfa: 1)",
            DiagnosticCodes.UnknownArgument);

        Assert.Contains("alpha", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_PipedArgumentIsTypeChecked()
    {
        Diagnostic error = Script.CheckFailsWith("emit r = \"строка\" |> vec.sum()");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
    }

    [Fact]
    public void Check_ReturnTypeOfNativeFunction_FlowsIntoInference()
    {
        // math.sqrt -> num, значит условие 'if' с ним корректно, а сложение со строкой — нет.
        Assert.True(Script.Check("emit r = if math.sqrt(4) > 1 { 1 } else { 2 }").Success);
        Assert.Contains(DiagnosticCodes.BadOperandTypes, Script.CheckCodes("emit r = math.sqrt(4) + \"x\""));
    }

    [Fact]
    public void Check_SetChangesKnownType()
    {
        // После присваивания тип имени — тип нового значения, а не прежнего.
        Assert.Contains(DiagnosticCodes.ConditionNotBool, Script.CheckCodes("""
            let x = true
            set x = 1
            if x { print("да") }
            """));
    }

    [Fact]
    public void Check_LoopVariableTypeIsInferredFromSequence()
    {
        Assert.Contains(DiagnosticCodes.BadOperandTypes, Script.CheckCodes("""
            for x in <1, 2, 3> {
                emit r = x + "строка"
            }
            """));
    }

    [Fact]
    public void Check_TableRowIsRecord()
    {
        CheckResult result = Script.Check("""
            let t = table.of({ a: <1, 2> })
            for row in t { print(row.a) }
            """);

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Check_IndexOfVectorIsNumber()
    {
        Assert.Contains(DiagnosticCodes.BadOperandTypes, Script.CheckCodes("let v = <1, 2>\nemit r = v[0] + \"x\""));
    }

    [Fact]
    public void Check_SliceOfVectorIsVector()
    {
        CheckResult result = Script.Check("let v = <1, 2, 3>\nemit r = vec.sum(v[0..2])");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Check_VariadicParameterAcceptsAnything()
    {
        CheckResult result = Script.Check("print(1, \"два\", <3>, {a: 4})");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Check_NoneIsAcceptedEverywhere()
    {
        CheckResult result = Script.Check("emit r = core.parse_num(\"1\", fallback: none)");

        Assert.True(result.Success, result.Render());
    }

    [Theory]
    [InlineData(BinaryOperator.Add, ScriptType.Num, ScriptType.Num, ScriptType.Num)]
    [InlineData(BinaryOperator.Add, ScriptType.Str, ScriptType.Str, ScriptType.Str)]
    [InlineData(BinaryOperator.Add, ScriptType.List, ScriptType.List, ScriptType.List)]
    [InlineData(BinaryOperator.Add, ScriptType.Vec, ScriptType.Num, ScriptType.Vec)]
    [InlineData(BinaryOperator.Multiply, ScriptType.Mat, ScriptType.Mat, ScriptType.Mat)]
    [InlineData(BinaryOperator.Multiply, ScriptType.Mat, ScriptType.Vec, ScriptType.Vec)]
    [InlineData(BinaryOperator.Subtract, ScriptType.Date, ScriptType.Date, ScriptType.Dur)]
    [InlineData(BinaryOperator.Add, ScriptType.Date, ScriptType.Dur, ScriptType.Date)]
    [InlineData(BinaryOperator.Less, ScriptType.Num, ScriptType.Num, ScriptType.Bool)]
    [InlineData(BinaryOperator.And, ScriptType.Bool, ScriptType.Bool, ScriptType.Bool)]
    public void TypeRules_BinaryResults(BinaryOperator op, ScriptType left, ScriptType right, ScriptType expected) =>
        Assert.Equal(expected, TypeRules.Binary(op, left, right));

    [Theory]
    [InlineData(BinaryOperator.Add, ScriptType.Num, ScriptType.Str)]
    [InlineData(BinaryOperator.Subtract, ScriptType.Str, ScriptType.Str)]
    [InlineData(BinaryOperator.Multiply, ScriptType.Record, ScriptType.Num)]
    [InlineData(BinaryOperator.Less, ScriptType.Bool, ScriptType.Bool)]
    [InlineData(BinaryOperator.And, ScriptType.Num, ScriptType.Num)]
    public void TypeRules_UndefinedCombinations(BinaryOperator op, ScriptType left, ScriptType right) =>
        Assert.Null(TypeRules.Binary(op, left, right));

    [Theory]
    [InlineData(ScriptType.Vec, ScriptType.List, true)]
    [InlineData(ScriptType.Vec, ScriptType.Range, true)]
    [InlineData(ScriptType.Mat, ScriptType.Table, true)]
    [InlineData(ScriptType.Any, ScriptType.Str, true)]
    [InlineData(ScriptType.Num, ScriptType.None, true)]
    [InlineData(ScriptType.Num, ScriptType.Str, false)]
    [InlineData(ScriptType.Str, ScriptType.Num, false)]
    [InlineData(ScriptType.Bool, ScriptType.Num, false)]
    public void TypeRules_Assignability(ScriptType parameter, ScriptType argument, bool expected) =>
        Assert.Equal(expected, TypeRules.Accepts(parameter, argument));

    /// <summary>
    /// Таблица типов обязана предсказывать рантайм: если она говорит «операция определена»,
    /// исполнение не должно отказывать, и наоборот.
    /// </summary>
    [Theory]
    [InlineData("1 + 2")]
    [InlineData("\"a\" + \"b\"")]
    [InlineData("<1, 2> * 2")]
    [InlineData("2 * <1, 2>")]
    [InlineData("[1] + [2]")]
    [InlineData("@2026-01-01 + 1d")]
    [InlineData("1m * 2")]
    [InlineData("mat.eye(2) + mat.eye(2)")]
    [InlineData("mat.eye(2) * 3")]
    [InlineData("-30s")]
    [InlineData("-mat.eye(2)")]
    public void TypeRules_AgreeWithRuntime(string expression)
    {
        CheckResult check = Script.Check($"emit r = {expression}");
        RunResult run = Script.Run($"emit r = {expression}");

        Assert.True(check.Success, check.Render());
        Assert.True(run.Success, Script.Report(run));
    }
}

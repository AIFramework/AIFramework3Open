using AI.DataStructs.Algebraic;
using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Исполнение: значения, операторы, управляющие конструкции, функции.</summary>
public sealed class InterpreterTests
{
    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("7 / 2", 3.5)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("-5 % 3", -2)]
    [InlineData("pi > 3.14", 1)]
    public void Numbers_AreEvaluated(string expression, double expected)
    {
        object? value = Script.Eval(expression);

        Assert.Equal(expected, value is bool flag ? (flag ? 1 : 0) : (double)value!, 12);
    }

    [Fact]
    public void Strings_Concatenate()
    {
        Assert.Equal("абв", Script.Text("\"аб\" + \"в\""));
    }

    [Fact]
    public void Strings_Interpolate()
    {
        const string source = """
            let k = 4
            emit r = "k = ${k}, k^2 = ${k * k}"
            """;

        Assert.Equal("k = 4, k^2 = 16", Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Interpolation_UsesInvariantCulture()
    {
        Assert.Equal("0.5", Script.Text("\"${1 / 2}\""));
    }

    [Fact]
    public void MultilineString_KeepsInterpolation()
    {
        const string source = "let n = 2\nemit r = \"\"\"\n    строка ${n}\n    \"\"\"";

        Assert.Equal("строка 2", Script.RunOk(source).Emitted["r"]);
    }

    [Theory]
    [InlineData("1 == 1", true)]
    [InlineData("1 == 2", false)]
    [InlineData("\"a\" == \"a\"", true)]
    [InlineData("1 == \"1\"", false)]
    [InlineData("[1, 2] == [1, 2]", true)]
    [InlineData("<1, 2> == <1, 2>", true)]
    [InlineData("{a: 1} == {a: 1}", true)]
    public void Equality_ComparesByValue(string expression, bool expected) =>
        Assert.Equal(expected, Script.Flag(expression));

    [Fact]
    public void NumberToBool_HasNoImplicitConversion()
    {
        // Ловится проверкой, а не исполнением: скрипт вообще не запускается.
        Diagnostic error = Script.CheckFailsWith("let n = 1\nif n { print(\"да\") }");

        Assert.Equal(DiagnosticCodes.ConditionNotBool, error.Code);
        Assert.Contains("x > 0", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void NumberToBool_IsAlsoRejectedAtRuntimeWhenTypeIsUnknown()
    {
        // Тип элемента списка статически неизвестен, поэтому отказ приходит из рантайма.
        Diagnostic error = Script.FailsWith("let xs = [1]\nfor n in xs { if n { print(\"да\") } }");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("x > 0", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorArithmetic_IsElementwise()
    {
        var vector = Assert.IsType<Vector>(Script.Eval("<1, 2, 3> * 2 + <1, 1, 1>"));

        Assert.Equal([3.0, 5.0, 7.0], vector.ToArray());
    }

    [Fact]
    public void VectorSizeMismatch_ReportsBothSizes()
    {
        Diagnostic error = Script.FailsWith("emit r = <1, 2> + <1, 2, 3>");

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("2", error.Message, StringComparison.Ordinal);
        Assert.Contains("3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dates_SupportDurationArithmetic()
    {
        Assert.Equal("2026-08-29", Script.Text("core.to_str(@2026-08-28 + 1d)"));
    }

    [Fact]
    public void Dates_SubtractToDuration()
    {
        Assert.Equal(86400.0, Script.Number("core.to_num(@2026-08-29 - @2026-08-28)"), 6);
    }

    [Theory]
    [InlineData("[10, 20, 30][0]", 10)]
    [InlineData("[10, 20, 30][-1]", 30)]
    [InlineData("<1, 2, 3>[1]", 2)]
    [InlineData("len([10, 20, 30][1..3])", 2)]
    [InlineData("len(\"abcdef\"[1..4])", 3)]
    public void Indexing_Works(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 12);

    [Fact]
    public void Indexing_OutOfRange_ReportsBounds()
    {
        Diagnostic error = Script.FailsWith("emit r = [1, 2][5]");

        Assert.Equal(DiagnosticCodes.IndexOutOfRange, error.Code);
        Assert.Contains("длиной 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_FieldAccess()
    {
        Assert.Equal(0.2, Script.Number("{ model: \"x\", temp: 0.2 }.temp"), 12);
    }

    [Fact]
    public void Record_MissingField_SuggestsClosest()
    {
        Diagnostic error = Script.FailsWith("let cfg = { temp: 1 }\nemit r = cfg.tmp");

        Assert.Contains("temp", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_SpreadOverridesField()
    {
        Assert.Equal(0.7, Script.Number("{ ...{ temp: 0.2, n: 1 }, temp: 0.7 }.temp"), 12);
    }

    [Fact]
    public void Values_AreImmutable()
    {
        const string source = """
            let a = [1, 2, 3]
            let b = a
            set a[0] = 99
            emit r = b[0]
            """;

        Assert.Equal(1.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Set_UpdatesBinding()
    {
        const string source = """
            let n = 1
            set n += 4
            set n *= 2
            emit r = n
            """;

        Assert.Equal(10.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Set_OnRecordField()
    {
        const string source = """
            let cfg = { temp: 0.2 }
            set cfg.temp = 0.9
            emit r = cfg.temp
            """;

        Assert.Equal(0.9, (double)Script.RunOk(source).Emitted["r"]!, 12);
    }

    [Fact]
    public void For_OverRange()
    {
        const string source = """
            let s = 0
            for i in 1..5 { set s += i }
            emit r = s
            """;

        Assert.Equal(10.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void For_WithStep()
    {
        const string source = """
            let s = 0
            for i in 0..10 by 2 { set s += i }
            emit r = s
            """;

        Assert.Equal(20.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void For_OverListWithBreakAndContinue()
    {
        const string source = """
            let s = 0
            for x in [1, 2, 3, 4, 5, 6] {
                if x % 2 == 0 { continue }
                if x > 4 { break }
                set s += x
            }
            emit r = s
            """;

        Assert.Equal(4.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void For_DestructuresPairs()
    {
        const string source = """
            let cfg = { a: 1, b: 2 }
            let s = 0
            for (name, value) in core.pairs(cfg) { set s += value }
            emit r = s
            """;

        Assert.Equal(3.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void While_Loops()
    {
        const string source = """
            let n = 1
            while n < 100 { set n *= 2 }
            emit r = n
            """;

        Assert.Equal(128.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Function_ReturnsLastExpression()
    {
        const string source = """
            fn zscore(x, mean, std) {
                (x - mean) / std
            }
            emit r = zscore(12, mean: 10, std: 2)
            """;

        Assert.Equal(1.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Function_EarlyReturn()
    {
        const string source = """
            fn sign(x) {
                if x > 0 { return 1 }
                if x < 0 { return -1 }
                0
            }
            emit r = sign(-7)
            """;

        Assert.Equal(-1.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Function_ReturnFromLoop()
    {
        const string source = """
            fn firstNegative(xs) {
                for x in xs {
                    if x < 0 { return x }
                }
                0
            }
            emit r = firstNegative([3, 1, -4, 5])
            """;

        Assert.Equal(-4.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Function_DefaultParameter()
    {
        const string source = """
            fn scale(x, factor = 2) { x * factor }
            emit r = scale(5)
            """;

        Assert.Equal(10.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Function_Recursion()
    {
        const string source = """
            fn fact(n) {
                if n <= 1 { return 1 }
                n * fact(n - 1)
            }
            emit r = fact(6)
            """;

        Assert.Equal(720.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Function_RecursionDepth_IsLimited()
    {
        const string source = """
            fn loop(n) { loop(n + 1) }
            emit r = loop(0)
            """;

        Diagnostic error = Script.FailsWith(source);

        Assert.Equal(DiagnosticCodes.CallDepthLimit, error.Code);
    }

    [Fact]
    public void Lambda_CapturesDefiningScope()
    {
        const string source = """
            let factor = 3
            let scale = x => x * factor
            emit r = [1, 2] |> core.map(scale) |> core.reduce((a, b) => a + b)
            """;

        Assert.Equal(9.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void FunctionValue_CanBePassedToMap()
    {
        const string source = """
            fn twice(x) { x * 2 }
            emit r = [1, 2, 3] |> core.map(twice) |> core.reduce((a, b) => a + b)
            """;

        Assert.Equal(12.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void NativeFunction_CanBePassedAsValue()
    {
        Assert.Equal(6.0, Script.Number("[1, 4, 9] |> core.map(math.sqrt) |> core.reduce((a, b) => a + b)"));
    }

    [Fact]
    public void Assert_FailureCarriesOperandValues()
    {
        Diagnostic error = Script.FailsWith("let x = 1\nassert x > 10, \"слишком мало\"");

        Assert.Equal(DiagnosticCodes.AssertionFailed, error.Code);
        Assert.Equal("слишком мало", error.Message);
        Assert.Contains("слева: 1", error.Hint, StringComparison.Ordinal);
        Assert.Contains("справа: 10", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Assert_PassesSilently()
    {
        RunResult result = Script.RunOk("assert 1 < 2\nemit r = 1");

        Assert.Equal(1.0, result.Emitted["r"]);
    }

    [Fact]
    public void Try_CatchesRuntimeFailure()
    {
        const string source = """
            let handled = "нет"
            try {
                let x = [1][5]
            } catch e {
                set handled = e.code
            }
            emit r = handled
            """;

        Assert.Equal(DiagnosticCodes.IndexOutOfRange, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Try_DoesNotCatchLimitAbort()
    {
        // Иначе скрипт мог бы проигнорировать собственный потолок, и лимит перестал бы им быть.
        const string source = """
            let n = 0
            try {
                while true { set n += 1 }
            } catch e {
                print("поймано")
            }
            emit r = n
            """;

        var options = new RunOptions();
        options.Limits.Steps = 5000;

        RunResult result = Script.Run(source, options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.StepLimit, result.Error!.Code);
    }

    [Fact]
    public void Print_GoesToTranscript()
    {
        RunResult result = Script.RunOk("print(\"строка\", 42)");

        Assert.Contains("строка 42", result.Transcript);
    }

    [Fact]
    public void Show_ProducesArtifact()
    {
        RunResult result = Script.RunOk("show <1, 2, 3>");

        Assert.Single(result.Artifacts);
        Assert.Equal("<1, 2, 3>", result.Artifacts[0].Text);
    }

    [Fact]
    public void Emit_RepeatedName_WarnsAndOverwrites()
    {
        RunResult result = Script.RunOk("emit r = 1\nemit r = 2");

        Assert.Equal(2.0, result.Emitted["r"]);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Emit_UnwrapsRecordToDictionary()
    {
        RunResult result = Script.RunOk("emit r = { a: 1, b: \"x\" }");
        var record = Assert.IsType<Dictionary<string, object?>>(result.Emitted["r"]);

        Assert.Equal(1.0, record["a"]);
        Assert.Equal("x", record["b"]);
    }

    [Fact]
    public void TranscriptSurvivesFailure()
    {
        RunResult result = Script.Run("print(\"до срыва\")\nemit r = [1][9]");

        Assert.False(result.Success);
        Assert.Contains("до срыва", result.Transcript);
    }

    [Fact]
    public void RuntimeError_CarriesPosition()
    {
        Diagnostic error = Script.FailsWith("let a = 1\nlet b = a + \"строка\"");

        Assert.Equal(2, error.Position.Line);
    }

    [Fact]
    public void FunctionFromLibrary_FailureIsNamed()
    {
        Diagnostic error = Script.FailsWith("emit r = math.factorial(-1)");

        Assert.Contains("math.factorial", error.Message, StringComparison.Ordinal);
    }
}

using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Хост: настройки прогона, лимиты, данные от вызывающего, расширение модулями.</summary>
public sealed class HostTests
{
    [Fact]
    public void Host_SeededData_BecomesVariables()
    {
        var options = new RunOptions
        {
            Seeded = new Dictionary<string, object?>
            {
                ["prices"] = new[] { 10.0, 20.0, 30.0 },
                ["name"] = "квартал",
                ["count"] = 3,
            },
        };

        RunResult result = Script.RunOk("emit total = vec.sum(prices)\nemit label = name", options);

        Assert.Equal(60.0, result.Emitted["total"]);
        Assert.Equal("квартал", result.Emitted["label"]);
    }

    [Fact]
    public void Host_SeededRecordAndList_AreConverted()
    {
        var options = new RunOptions
        {
            Seeded = new Dictionary<string, object?>
            {
                ["cfg"] = new Dictionary<string, object?> { ["temp"] = 0.2, ["model"] = "x" },
                ["mixed"] = new List<object?> { 1.0, "две", true },
            },
        };

        RunResult result = Script.RunOk("emit t = cfg.temp\nemit m = mixed[1]\nemit n = len(mixed)", options);

        Assert.Equal(0.2, result.Emitted["t"]);
        Assert.Equal("две", result.Emitted["m"]);
        Assert.Equal(3.0, result.Emitted["n"]);
    }

    [Fact]
    public void Host_EmittedRecord_RoundTripsBackIntoScript()
    {
        RunResult first = Script.RunOk("emit r = { a: 1, xs: [1, 2] }");

        var options = new RunOptions
        {
            Seeded = new Dictionary<string, object?> { ["prev"] = first.Emitted["r"] },
        };

        RunResult second = Script.RunOk("emit r = prev.a + len(prev.xs)", options);

        Assert.Equal(3.0, second.Emitted["r"]);
    }

    [Fact]
    public void Host_TypeAnnotation_ConvertsListToVector()
    {
        RunResult result = Script.RunOk("let v: vec = [1, 2, 3]\nemit r = type(v)");

        Assert.Equal("vec", result.Emitted["r"]);
    }

    [Fact]
    public void Host_TypeAnnotation_RejectsWrongType()
    {
        Semantics.Diagnostic error = Script.CheckFailsWith("let v: num = \"строка\"");

        Assert.Equal(DiagnosticCodes.DeclaredTypeMismatch, error.Code);
    }

    [Fact]
    public void Host_SeededName_UnfitForIdentifier_IsSkipped()
    {
        var options = new RunOptions
        {
            Seeded = new Dictionary<string, object?> { ["не имя"] = 1.0, ["ok"] = 2.0 },
        };

        RunResult result = Script.RunOk("emit r = ok", options);

        Assert.Equal(2.0, result.Emitted["r"]);
    }

    [Fact]
    public void Host_StepLimit_StopsRunawayLoop()
    {
        var options = new RunOptions();
        options.Limits.Steps = 1000;

        RunResult result = Script.Run("let n = 0\nwhile true { set n += 1 }", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.StepLimit, result.Error!.Code);
        Assert.Contains("options.steps", result.Error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_AllocationLimit_StopsHugeVector()
    {
        // Потолок шагов здесь не спасает: одна аллокация укладывает процесс за один шаг.
        var options = new RunOptions();
        options.Limits.Allocations = 1_000;

        RunResult result = Script.Run("emit r = vec.zeros(10000000)", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.MemoryLimit, result.Error!.Code);
    }

    [Fact]
    public void Host_ScriptOptions_SetSeedAndSteps()
    {
        const string source = """
            options { seed: 5, steps: 100000 }
            emit r = math.randint(low: 0, high: 100)
            """;

        RunResult first = Script.RunOk(source);
        RunResult second = Script.RunOk(source);

        Assert.Equal(first.Emitted["r"], second.Emitted["r"]);
    }

    [Fact]
    public void Host_LockedOption_IsNotAppliedAndWarns()
    {
        var options = new RunOptions();
        _ = options.LockedOptions.Add("steps");
        options.Limits.Steps = 1000;

        RunResult result = Script.Run("options { steps: 100000000 }\nlet n = 0\nwhile true { set n += 1 }", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.StepLimit, result.Error!.Code);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Host_UnknownOption_Warns()
    {
        RunResult result = Script.RunOk("options { verbose: true }\nemit r = 1");

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnknownArgument);
    }

    [Fact]
    public void Host_Timeout_StopsLongRun()
    {
        var options = new RunOptions();
        options.Limits.Timeout = TimeSpan.FromMilliseconds(80);
        options.Limits.Steps = 0;

        RunResult result = Script.Run("let n = 0\nwhile true { set n += 1 }", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.Timeout, result.Error!.Code);
    }

    [Fact]
    public void Host_Cancellation_IsReportedSeparatelyFromTimeout()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        RunResult result = Script.RunWith(
            Script.Host(),
            "let n = 0\nwhile true { set n += 1 }",
            new RunOptions(),
            source.Token);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.Cancelled, result.Error!.Code);
    }

    [Fact]
    public void Host_Stats_AreCollected()
    {
        RunResult result = Script.RunOk("let s = 0\nfor i in 0..100 { set s += i }\nemit r = s");

        Assert.True(result.Stats.Steps > 100);
        Assert.Equal(4950.0, result.Emitted["r"]);
    }

    [Fact]
    public void Host_ParseError_PreventsExecution()
    {
        RunResult result = Script.Run("print(\"выполнено\")\nlet = 1");

        Assert.False(result.Success);
        Assert.Empty(result.Transcript);
    }

    [Fact]
    public void Host_CustomModule_IsCallable()
    {
        ScriptHost host = Std.StandardLibrary.CreateHost().Use(ScriptModule.FromType(typeof(DemoModule)));

        RunResult result = Script.RunWith(host, "emit r = demo.triple(7)");

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(21.0, result.Emitted["r"]);
    }

    [Fact]
    public void Host_CustomModule_HandleAndMethod()
    {
        ScriptHost host = Std.StandardLibrary.CreateHost().Use(ScriptModule.FromType(typeof(DemoModule)));

        const string source = """
            let box = demo.make_box(3)
            emit r = box.scale(4)
            """;

        RunResult result = Script.RunWith(host, source);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(12.0, result.Emitted["r"]);
    }

    [Fact]
    public void Host_CustomModule_AsyncFunctionIsAwaited()
    {
        ScriptHost host = Std.StandardLibrary.CreateHost().Use(ScriptModule.FromType(typeof(DemoModule)));

        RunResult result = Script.RunWith(host, "emit r = demo.slow_add(2, 3)");

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(5.0, result.Emitted["r"]);
    }

    [Fact]
    public void Host_CustomModule_VectorMarshalling()
    {
        ScriptHost host = Std.StandardLibrary.CreateHost().Use(ScriptModule.FromType(typeof(DemoModule)));

        RunResult result = Script.RunWith(host, "emit r = vec.sum(demo.doubled(<1, 2, 3>))");

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(12.0, result.Emitted["r"]);
    }

    [Fact]
    public void Host_SnakeCaseConversion()
    {
        Assert.Equal("read_csv", ScriptModule.ToSnakeCase("ReadCsv"));
        Assert.Equal("to_matrix", ScriptModule.ToSnakeCase("ToMatrix"));
        Assert.Equal("len", ScriptModule.ToSnakeCase("Len"));
        Assert.Equal("z_score", ScriptModule.ToSnakeCase("ZScore"));
    }

    [Fact]
    public void Host_RegistryExposesSignatures()
    {
        ScriptFunction? function = Script.Host().Registry.Find("math.clamp");

        Assert.NotNull(function);
        Assert.Contains("low", function!.Signature, StringComparison.Ordinal);
        Assert.Contains("-> num", function.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_ModuleWithoutAttribute_IsRejectedOnRegistration()
    {
        _ = Assert.Throws<InvalidOperationException>(() => ScriptModule.FromType<HostTests>());
    }

    /// <summary>Демонстрационный модуль: проверяет привязку, дескрипторы и асинхронность.</summary>
    [ScriptModule("demo", "Модуль для тестов привязки")]
    public static class DemoModule
    {
        /// <summary>Утраивает число.</summary>
        [ScriptFn("triple", "Утраивает число")]
        public static double Triple([ScriptParam("число")] double x) => x * 3;

        /// <summary>Создаёт коробку с числом внутри.</summary>
        [ScriptFn("make_box", "Создаёт коробку", Returns = "demo.box")]
        public static Box MakeBox([ScriptParam("значение")] double value) => new(value);

        /// <summary>Умножает содержимое коробки.</summary>
        [ScriptFn("scale", "Умножает содержимое коробки")]
        [ScriptMethod("demo.box")]
        public static double Scale(Box box, [ScriptParam("множитель")] double by) => box.Value * by;

        /// <summary>Асинхронное сложение.</summary>
        [ScriptFn("slow_add", "Складывает два числа асинхронно")]
        public static async Task<double> SlowAdd(
            [ScriptParam("первое")] double a,
            [ScriptParam("второе")] double b)
        {
            await Task.Yield();
            return a + b;
        }

        /// <summary>Удваивает вектор.</summary>
        [ScriptFn("doubled", "Удваивает элементы вектора")]
        public static Vector Doubled([ScriptParam("вектор")] Vector v) => v * 2.0;

        /// <summary>Коробка с числом: объект фреймворка в роли дескриптора.</summary>
        public sealed class Box
        {
            /// <summary>Значение внутри.</summary>
            public double Value { get; }

            /// <summary>Создаёт коробку.</summary>
            public Box(double value) => Value = value;
        }
    }
}

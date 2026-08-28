using AI.DataStructs.Algebraic;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Стандартная библиотека: core, math, vec, stat, str.</summary>
public sealed class StandardLibraryTests
{
    [Theory]
    [InlineData("len(\"абвг\")", 4)]
    [InlineData("len([1, 2, 3])", 3)]
    [InlineData("len(<1, 2>)", 2)]
    [InlineData("len({a: 1, b: 2})", 2)]
    [InlineData("len(0..7)", 7)]
    public void Core_Len(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 12);

    [Theory]
    [InlineData("type(1)", "num")]
    [InlineData("type(true)", "bool")]
    [InlineData("type(\"x\")", "str")]
    [InlineData("type([1])", "list")]
    [InlineData("type(<1>)", "vec")]
    [InlineData("type({a: 1})", "record")]
    [InlineData("type(none)", "none")]
    [InlineData("type(1..2)", "range")]
    [InlineData("type(30s)", "dur")]
    [InlineData("type(@2026-01-01)", "date")]
    [InlineData("type(x => x)", "fn")]
    public void Core_Type(string expression, string expected) =>
        Assert.Equal(expected, Script.Text(expression));

    [Fact]
    public void Core_Help_ListsNamespaces()
    {
        string help = Script.Text("help()");

        Assert.Contains("math", help, StringComparison.Ordinal);
        Assert.Contains("core", help, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_Help_DescribesFunction()
    {
        string help = Script.Text("help(\"math.clamp\")");

        Assert.Contains("math.clamp", help, StringComparison.Ordinal);
        Assert.Contains("low", help, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_Help_SuggestsOnMiss()
    {
        string help = Script.Text("help(\"maht\")");

        Assert.Contains("math", help, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("core.round(2.345, digits: 2)", 2.35)]
    [InlineData("core.round(2.5)", 3)]
    [InlineData("core.parse_num(\"3.5\")", 3.5)]
    [InlineData("core.parse_num(\"нет\", fallback: -1)", -1)]
    [InlineData("core.to_num(true)", 1)]
    [InlineData("core.index_of([1, 2, 3], 2)", 1)]
    [InlineData("core.index_of([1, 2, 3], 9)", -1)]
    [InlineData("len(core.unique([1, 1, 2, 2, 3]))", 3)]
    [InlineData("len(core.zip([1, 2, 3], [1, 2]))", 2)]
    [InlineData("len(core.take([1, 2, 3, 4], 2))", 2)]
    [InlineData("len(core.skip([1, 2, 3, 4], 2))", 2)]
    [InlineData("core.first([5, 6])", 5)]
    [InlineData("core.last([5, 6])", 6)]
    [InlineData("core.reverse([1, 2, 3])[0]", 3)]
    public void Core_Sequences(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 12);

    [Fact]
    public void Core_Contains()
    {
        Assert.True(Script.Flag("core.contains([1, 2], 2)"));
        Assert.False(Script.Flag("core.contains([1, 2], 3)"));
    }

    [Fact]
    public void Core_Has()
    {
        Assert.True(Script.Flag("core.has({a: 1}, \"a\")"));
        Assert.False(Script.Flag("core.has({a: 1}, \"b\")"));
    }

    [Fact]
    public void Core_Filter()
    {
        Assert.Equal(2.0, Script.Number("len([1, -2, 3, -4] |> core.filter(x => x > 0))"), 12);
    }

    [Fact]
    public void Core_Reduce_WithInitialValue()
    {
        Assert.Equal(16.0, Script.Number("[1, 2, 3] |> core.reduce((a, b) => a + b, from: 10)"), 12);
    }

    [Fact]
    public void Core_Reduce_EmptyWithoutInitial_Fails()
    {
        Diagnostic error = Script.FailsWith("emit r = [] |> core.reduce((a, b) => a + b)");

        Assert.Equal(DiagnosticCodes.IndexOutOfRange, error.Code);
    }

    [Fact]
    public void Core_Sort_ByKey()
    {
        const string source = """
            let rows = [{ n: "b", v: 2 }, { n: "a", v: 1 }]
            emit r = rows |> core.sort(by: row => row.v) |> core.first() |> core.to_str()
            """;

        Assert.Contains("a", (string)Script.RunOk(source).Emitted["r"]!, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_Sort_Descending()
    {
        Assert.Equal(3.0, Script.Number("[1, 3, 2] |> core.sort(desc: true) |> core.first()"), 12);
    }

    [Fact]
    public void Core_AnyAll()
    {
        Assert.True(Script.Flag("[1, -1] |> core.any(x => x < 0)"));
        Assert.False(Script.Flag("[1, -1] |> core.all(x => x > 0)"));
    }

    [Theory]
    [InlineData("math.sqrt(16)", 4)]
    [InlineData("math.abs(-3)", 3)]
    [InlineData("math.floor(2.7)", 2)]
    [InlineData("math.ceil(2.1)", 3)]
    [InlineData("math.trunc(-2.7)", -2)]
    [InlineData("math.sign(-5)", -1)]
    [InlineData("math.pow(2, 10)", 1024)]
    [InlineData("math.log(8, base: 2)", 3)]
    [InlineData("math.log10(1000)", 3)]
    [InlineData("math.log2(1024)", 10)]
    [InlineData("math.min(3, 7)", 3)]
    [InlineData("math.max(3, 7)", 7)]
    [InlineData("math.clamp(5, low: 0, high: 1)", 1)]
    [InlineData("math.hypot(3, 4)", 5)]
    [InlineData("math.degrees(pi)", 180)]
    [InlineData("math.factorial(5)", 120)]
    [InlineData("math.gcd(12, 18)", 6)]
    [InlineData("math.lcm(4, 6)", 12)]
    [InlineData("math.cbrt(27)", 3)]
    public void Math_Functions(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 9);

    [Fact]
    public void Math_Approx_ExistsBecauseEqualityIsExact()
    {
        Assert.False(Script.Flag("0.1 + 0.2 == 0.3"));
        Assert.True(Script.Flag("math.approx(0.1 + 0.2, 0.3, eps: 1e-12)"));
    }

    [Fact]
    public void Math_Random_IsReproducibleBySeed()
    {
        const string source = """
            options { seed: 123 }
            emit r = math.random()
            """;

        Assert.Equal(Script.RunOk(source).Emitted["r"], Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Math_Random_DiffersBetweenSeeds()
    {
        object? a = Script.RunOk("options { seed: 1 }\nemit r = math.random()").Emitted["r"];
        object? b = Script.RunOk("options { seed: 2 }\nemit r = math.random()").Emitted["r"];

        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("len(vec.zeros(5))", 5)]
    [InlineData("vec.sum(vec.ones(4))", 4)]
    [InlineData("len(vec.linspace(0, 1, n: 11))", 11)]
    [InlineData("vec.linspace(0, 1, n: 11)[10]", 1)]
    [InlineData("len(vec.arange(0, 10, by: 2))", 5)]
    [InlineData("vec.dot(<1, 2>, <3, 4>)", 11)]
    [InlineData("vec.norm(<3, 4>)", 5)]
    [InlineData("vec.prod(<1, 2, 3, 4>)", 24)]
    [InlineData("vec.cumsum(<1, 2, 3>)[2]", 6)]
    [InlineData("len(vec.diff(<1, 2, 4>))", 2)]
    [InlineData("vec.diff(<1, 2, 4>)[1]", 2)]
    [InlineData("vec.argmax(<1, 9, 3>)", 1)]
    [InlineData("vec.argmin(<1, 9, 3>)", 0)]
    [InlineData("vec.sort(<3, 1, 2>)[0]", 1)]
    [InlineData("len(vec.concat(<1>, <2, 3>))", 3)]
    [InlineData("vec.clip(<-1, 2>, low: 0, high: 1)[0]", 0)]
    [InlineData("len(vec.slice(<1, 2, 3, 4>, from: 1, to: 3))", 2)]
    public void Vec_Functions(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 9);

    [Fact]
    public void Vec_AcceptsListOfNumbers()
    {
        // Единственное неявное приведение языка: список чисел там, где ожидается вектор.
        Assert.Equal(6.0, Script.Number("vec.sum([1, 2, 3])"), 12);
    }

    [Fact]
    public void Vec_RejectsListOfStrings()
    {
        Diagnostic error = Script.FailsWith("emit r = vec.sum([\"a\"])");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
    }

    [Theory]
    [InlineData("stat.mean(<1, 2, 3>)", 2)]
    [InlineData("stat.median(<1, 3, 2>)", 2)]
    [InlineData("stat.min(<3, 1>)", 1)]
    [InlineData("stat.max(<3, 1>)", 3)]
    [InlineData("stat.quantile(<1, 2, 3, 4>, q: 0)", 1)]
    [InlineData("stat.quantile(<1, 2, 3, 4>, q: 1)", 4)]
    [InlineData("stat.corr(<1, 2, 3>, <2, 4, 6>)", 1)]
    [InlineData("stat.corr(<1, 2, 3>, <6, 4, 2>)", -1)]
    [InlineData("stat.rmse(<1, 2>, <1, 2>)", 0)]
    [InlineData("stat.mae(<1, 3>, <1, 1>)", 1)]
    [InlineData("stat.r2(<1, 2, 3>, <1, 2, 3>)", 1)]
    [InlineData("stat.std(<2, 2, 2>)", 0)]
    public void Stat_Functions(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression), 9);

    [Fact]
    public void Stat_ZScore_HasZeroMean()
    {
        Assert.Equal(0.0, Script.Number("stat.mean(stat.zscore(<1, 2, 3, 4>))"), 9);
    }

    [Fact]
    public void Stat_MinMax_MapsToUnitRange()
    {
        Assert.Equal(0.0, Script.Number("stat.min(stat.minmax(<3, 5, 9>))"), 9);
        Assert.Equal(1.0, Script.Number("stat.max(stat.minmax(<3, 5, 9>))"), 9);
    }

    [Fact]
    public void Stat_Hist_ReturnsEdgesAndCounts()
    {
        const string source = """
            let h = stat.hist(<1, 2, 3, 4>, bins: 2)
            emit r = vec.sum(h.counts)
            """;

        Assert.Equal(4.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Stat_EmptySample_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = stat.mean(<>)");

        Assert.Equal(DiagnosticCodes.IndexOutOfRange, error.Code);
    }

    [Fact]
    public void Stat_SizeMismatch_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = stat.rmse(<1, 2>, <1>)");

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
    }

    [Theory]
    [InlineData("str.upper(\"код\")", "КОД")]
    [InlineData("str.lower(\"КОД\")", "код")]
    [InlineData("str.trim(\"  x  \")", "x")]
    [InlineData("str.replace(\"a,b\", from: \",\", to: \".\")", "a.b")]
    [InlineData("str.sub(\"abcdef\", from: 1, to: 3)", "bc")]
    [InlineData("str.repeat(\"ab\", times: 3)", "ababab")]
    [InlineData("str.pad_left(\"7\", width: 3, with: \"0\")", "007")]
    [InlineData("[\"a\", \"b\"] |> str.join(by: \"-\")", "a-b")]
    public void Str_Functions(string expression, string expected) =>
        Assert.Equal(expected, Script.Text(expression));

    [Fact]
    public void Str_Split()
    {
        Assert.Equal(3.0, Script.Number("len(str.split(\"a,b,c\", by: \",\"))"), 12);
    }

    [Fact]
    public void Str_Predicates()
    {
        Assert.True(Script.Flag("str.starts_with(\"скрипт.ais\", \"скрипт\")"));
        Assert.True(Script.Flag("str.ends_with(\"скрипт.ais\", \".ais\")"));
        Assert.True(Script.Flag("str.contains(\"скрипт\", \"рип\")"));
    }

    [Fact]
    public void Str_Lines()
    {
        Assert.Equal(3.0, Script.Number("len(str.lines(\"a\\nb\\nc\"))"), 12);
    }

    [Fact]
    public void Vector_ReturnsToHostAsFrameworkType()
    {
        var vector = Assert.IsType<Vector>(Script.Eval("vec.linspace(0, 1, n: 5)"));

        Assert.Equal(5, vector.Count);
    }
}

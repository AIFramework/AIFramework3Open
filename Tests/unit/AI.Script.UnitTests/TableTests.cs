using AI.DataStructs.Algebraic;
using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Колоночные таблицы: построение, преобразования, группировка, соединение.</summary>
public sealed class TableTests
{
    private const string Sales = """
        let t = table.of({
            client: ["a", "b", "a", "c"],
            region: ["север", "юг", "север", "юг"],
            amount: <10, 20, 30, 40>
        })
        """;

    [Fact]
    public void Table_Of_BuildsColumns()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit rows = len(t)\nemit cols = len(table.columns(t))");

        Assert.Equal(4.0, result.Emitted["rows"]);
        Assert.Equal(3.0, result.Emitted["cols"]);
    }

    [Fact]
    public void Table_ColumnsOfDifferentLength_AreRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = table.of({ a: <1, 2>, b: <1> })");

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
    }

    [Fact]
    public void Table_ColumnByStringIndex()
    {
        Assert.Equal(100.0, Script.Number("vec.sum(t[\"amount\"])", prelude: Sales));
    }

    [Fact]
    public void Table_RowByNumericIndex()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit r = t[0].client");

        Assert.Equal("a", result.Emitted["r"]);
    }

    [Fact]
    public void Table_SliceByRange()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit r = len(t[1..3])");

        Assert.Equal(2.0, result.Emitted["r"]);
    }

    [Fact]
    public void Table_UnknownColumn_SuggestsClosest()
    {
        Diagnostic error = Script.FailsWith($"{Sales}\nemit r = t[\"amaunt\"]");

        Assert.Contains("amount", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_IteratesRows()
    {
        const string source = """
            let t = table.of({ x: <1, 2, 3> })
            let s = 0
            for row in t { set s += row.x }
            emit r = s
            """;

        Assert.Equal(6.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_Filter()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit r = len(t |> table.filter(row => row.amount > 15))");

        Assert.Equal(3.0, result.Emitted["r"]);
    }

    [Fact]
    public void Table_Derive()
    {
        const string source = """
            let t = table.of({ a: <1, 2>, b: <10, 20> })
            let d = t |> table.derive(cols: { c: row => row.a + row.b })
            emit r = vec.sum(d["c"])
            """;

        Assert.Equal(33.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_Derive_RequiresFunctions()
    {
        const string source = """
            let t = table.of({ a: <1, 2> })
            emit r = t |> table.derive(cols: { c: 5 })
            """;

        Diagnostic error = Script.FailsWith(source);

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
    }

    [Fact]
    public void Table_SelectAndDrop()
    {
        RunResult result = Script.RunOk($"""
            {Sales}
            emit selected = table.columns(t |> table.select(["amount", "client"]))
            emit dropped = table.columns(t |> table.drop(["region"]))
            """);

        var selected = Assert.IsType<List<object?>>(result.Emitted["selected"]);
        var dropped = Assert.IsType<List<object?>>(result.Emitted["dropped"]);

        Assert.Equal(["amount", "client"], selected);
        Assert.Equal(["client", "amount"], dropped);
    }

    [Fact]
    public void Table_Rename()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit r = table.columns(t |> table.rename(from: \"amount\", to: \"сумма\"))");

        Assert.Contains("сумма", Assert.IsType<List<object?>>(result.Emitted["r"]));
    }

    [Fact]
    public void Table_SortByColumn()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit r = (t |> table.sort(by: \"amount\", desc: true))[0].amount");

        Assert.Equal(40.0, result.Emitted["r"]);
    }

    [Fact]
    public void Table_SortByFunction()
    {
        RunResult result = Script.RunOk($"{Sales}\nemit r = (t |> table.sort(by: row => -row.amount))[0].amount");

        Assert.Equal(40.0, result.Emitted["r"]);
    }

    [Fact]
    public void Table_GroupBy()
    {
        const string source = """
            let t = table.of({ k: ["a", "b", "a"], v: <1, 2, 3> })
            let g = t |> table.group_by("k", agg: {
                total: rows => vec.sum(rows["v"]),
                n: rows => len(rows)
            })
            emit groups = len(g)
            emit first = g[0].total
            emit count = g[0].n
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["groups"]);
        Assert.Equal(4.0, result.Emitted["first"]);
        Assert.Equal(2.0, result.Emitted["count"]);
    }

    [Fact]
    public void Table_GroupBy_KeepsFirstAppearanceOrder()
    {
        const string source = """
            let t = table.of({ k: ["b", "a", "b"], v: <1, 2, 3> })
            let g = t |> table.group_by("k", agg: { n: rows => len(rows) })
            emit r = g[0].k
            """;

        Assert.Equal("b", Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_GroupBy_DoesNotMixTypesInKey()
    {
        // Строка "1" и число 1 — разные ключи: иначе две разные группы слились бы молча.
        const string source = """
            let t = table.of({ k: [1, "1"], v: <10, 20> })
            let g = t |> table.group_by("k", agg: { n: rows => len(rows) })
            emit r = len(g)
            """;

        Assert.Equal(2.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_Join_Inner()
    {
        const string source = """
            let a = table.of({ id: <1, 2, 3>, x: <10, 20, 30> })
            let b = table.of({ id: <2, 3>, y: <200, 300> })
            let j = table.join(a, b, on: "id")
            emit rows = len(j)
            emit sum = vec.sum(j["y"])
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(500.0, result.Emitted["sum"]);
    }

    [Fact]
    public void Table_Join_Left_FillsMissingWithNone()
    {
        const string source = """
            let a = table.of({ id: <1, 2>, x: <10, 20> })
            let b = table.of({ id: <2>, y: <200> })
            let j = table.join(a, b, on: "id", how: "left")
            emit rows = len(j)
            emit missing = type(j[0].y)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal("none", result.Emitted["missing"]);
    }

    [Fact]
    public void Table_Join_CollidingColumns_AreRejected()
    {
        const string source = """
            let a = table.of({ id: <1>, x: <10> })
            let b = table.of({ id: <1>, x: <20> })
            emit r = table.join(a, b, on: "id")
            """;

        Diagnostic error = Script.FailsWith(source);

        Assert.Equal(DiagnosticCodes.DuplicateArgument, error.Code);
        Assert.Contains("rename", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_OneHot_IsDeterministicAndOrdered()
    {
        const string source = """
            let t = table.of({ c: ["юг", "север", "юг"] })
            emit cols = table.columns(t |> table.one_hot(["c"]))
            emit sum = vec.sum((t |> table.one_hot(["c"]))["c=юг"])
            """;

        RunResult result = Script.RunOk(source);
        var cols = Assert.IsType<List<object?>>(result.Emitted["cols"]);

        Assert.Equal(["c=север", "c=юг"], cols);
        Assert.Equal(2.0, result.Emitted["sum"]);
    }

    [Fact]
    public void Table_Encode()
    {
        const string source = """
            let t = table.of({ c: ["b", "a", "b"] })
            emit r = vec.sum((t |> table.encode(["c"]))["c"])
            """;

        Assert.Equal(2.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_ToMatrix_RejectsNonNumericColumns()
    {
        Diagnostic error = Script.FailsWith($"{Sales}\nemit r = table.to_matrix(t)");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("client", error.Message, StringComparison.Ordinal);
        Assert.Contains("one_hot", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_ToMatrix_RoundTrips()
    {
        const string source = """
            let t = table.of({ a: <1, 2>, b: <3, 4> })
            let m = table.to_matrix(t)
            let back = table.from_matrix(m, cols: ["a", "b"])
            emit r = vec.sum(back["b"])
            emit rows = mat.rows(m)
            emit cols = mat.cols(m)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(7.0, result.Emitted["r"]);
        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(2.0, result.Emitted["cols"]);
    }

    [Fact]
    public void Table_HeadTailConcatDistinct()
    {
        const string source = """
            let t = table.of({ x: <1, 2, 3, 3> })
            emit head = len(t |> table.head(2))
            emit tail = (t |> table.tail(1))[0].x
            emit concat = len(table.concat(t, t))
            emit distinct = len(t |> table.distinct())
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["head"]);
        Assert.Equal(3.0, result.Emitted["tail"]);
        Assert.Equal(8.0, result.Emitted["concat"]);
        Assert.Equal(3.0, result.Emitted["distinct"]);
    }

    [Fact]
    public void Table_Concat_RejectsDifferentColumns()
    {
        const string source = """
            let a = table.of({ x: <1> })
            let b = table.of({ y: <1> })
            emit r = table.concat(a, b)
            """;

        Assert.Equal(DiagnosticCodes.SizeMismatch, Script.FailsWith(source).Code);
    }

    [Fact]
    public void Table_MissingValues_AreDroppedAndFilled()
    {
        const string source = """
            let t = table.of({ x: [1, none, 3] })
            emit dropped = len(t |> table.drop_na())
            emit filled = vec.sum((t |> table.fill_na(value: 0))["x"])
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["dropped"]);
        Assert.Equal(4.0, result.Emitted["filled"]);
    }

    [Fact]
    public void Table_MissingNumber_BecomesNanInVector()
    {
        // Пропуск не должен превращаться в ноль: ноль сместил бы любое среднее ниже.
        const string source = """
            let t = table.of({ x: [1, none, 3] })
            emit r = math.is_nan(t["x"][1])
            """;

        Assert.Equal(true, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_Split_IsReproducible()
    {
        const string source = """
            options { seed: 7 }
            let t = table.of({ x: vec.arange(0, 100) })
            let s = table.split(t, test: 0.2)
            emit train = len(s.train)
            emit test = len(s.test)
            emit firstTest = s.test[0].x
            """;

        RunResult first = Script.RunOk(source);
        RunResult second = Script.RunOk(source);

        Assert.Equal(80.0, first.Emitted["train"]);
        Assert.Equal(20.0, first.Emitted["test"]);
        Assert.Equal(first.Emitted["firstTest"], second.Emitted["firstTest"]);
    }

    [Fact]
    public void Table_Describe()
    {
        const string source = """
            let t = table.of({ x: <1, 2, 3>, name: ["a", "b", "c"] })
            let d = table.describe(t)
            emit rows = len(d)
            emit mean = d[0].mean
            emit type = d[1].type
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(2.0, result.Emitted["mean"]);
        Assert.Equal("str", result.Emitted["type"]);
    }

    [Fact]
    public void Table_With_AddsColumn()
    {
        const string source = """
            let t = table.of({ x: <1, 2> })
            emit r = vec.sum((t |> table.with(name: "y", values: <10, 20>))["y"])
            """;

        Assert.Equal(30.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Table_With_RejectsWrongLength()
    {
        const string source = """
            let t = table.of({ x: <1, 2> })
            emit r = t |> table.with(name: "y", values: <1>)
            """;

        Assert.Equal(DiagnosticCodes.SizeMismatch, Script.FailsWith(source).Code);
    }

    [Fact]
    public void Table_EmittedAsColumnDictionary()
    {
        RunResult result = Script.RunOk("emit r = table.of({ a: <1, 2>, b: [\"x\", \"y\"] })");
        var columns = Assert.IsType<Dictionary<string, object?>>(result.Emitted["r"]);

        Assert.Equal(2, columns.Count);
        Assert.IsType<Vector>(columns["a"]);
        Assert.IsType<List<object?>>(columns["b"]);
    }

    [Fact]
    public void Table_Formatting_ShowsHeaderAndRows()
    {
        RunResult result = Script.RunOk("show table.of({ a: <1, 2>, b: [\"x\", \"y\"] })");
        string text = result.Artifacts[0].Text;

        Assert.Contains("table 2×2", text, StringComparison.Ordinal);
        Assert.Contains("a", text, StringComparison.Ordinal);
        Assert.Contains("x", text, StringComparison.Ordinal);
    }
}

using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Перекладка таблицы: сводная, разворот, окно, накопление, ранг и сдвиг.
/// </summary>
/// <remarks>
/// Это то, ради чего открывают Excel, и до сих пор скрипту пришлось бы делать это циклами —
/// каждый раз по-своему. Поэтому проверяется не только число в ячейке, но и форма результата:
/// имена колонок и что стоит там, где данных не хватило.
/// </remarks>
public sealed class TableShapeTests
{
    private const string Sales = """
        let t = table.of({
            город: ["Москва", "Москва", "Пермь", "Пермь", "Пермь"],
            месяц: ["янв", "фев", "янв", "фев", "фев"],
            сумма: <10, 20, 30, 40, 5>
        })
        """;

    private const string Series = """
        let t = table.of({ выручка: <10, 20, 30, 40> })
        """;

    // --- сводная и разворот ---

    [Fact]
    public void Pivot_SumsIntoCells()
    {
        RunResult result = Script.RunOk($"""
            {Sales}
            let p = t |> table.pivot(rows: "город", cols: "месяц", value: "сумма")
            emit rows = len(p)
            emit cols = len(table.columns(p))
            emit пермь_фев = p[1]["фев"]
            """);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(3.0, result.Emitted["cols"]);
        Assert.Equal(45.0, result.Emitted["пермь_фев"]);
    }

    /// <summary>Пересечения без данных быть не должно молча нулём: там пропуск.</summary>
    [Fact]
    public void Pivot_EmptyCell_IsMissing()
    {
        Assert.True(Script.Flag(
            "(t |> table.pivot(rows: \"г\", cols: \"м\", value: \"s\"))[0][\"фев\"] == none",
            prelude: "let t = table.of({ г: [\"а\", \"б\"], м: [\"янв\", \"фев\"], s: <1, 2> })"));
    }

    [Fact]
    public void Pivot_Count_CountsRows()
    {
        Assert.Equal(2.0, Script.Number(
            "(t |> table.pivot(rows: \"город\", cols: \"месяц\", value: \"сумма\", kind: \"count\"))[1][\"фев\"]",
            prelude: Sales));
    }

    /// <summary>Деньги в сводной остаются точными: иначе итог разошёлся бы с исходной книгой.</summary>
    [Fact]
    public void Pivot_KeepsExactMoney()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({
                город: ["Пермь", "Пермь"],
                месяц: ["янв", "янв"],
                сумма: ["0,10 ₽", "0,20 ₽"]
            }) |> table.clean()
            emit r = (t |> table.pivot(rows: "город", cols: "месяц", value: "сумма"))[0]["янв"]
            """);

        Assert.Equal(0.30m, result.Emitted["r"]);
    }

    [Fact]
    public void Pivot_Totals_AddRowAndColumn()
    {
        RunResult result = Script.RunOk($"""
            {Sales}
            let p = t |> table.pivot(rows: "город", cols: "месяц", value: "сумма", totals: true)
            emit rows = len(p)
            emit москва = p[0]["Итого"]
            emit итог_фев = p[2]["фев"]
            emit всего = p[2]["Итого"]
            emit подпись = p[2]["город"]
            """);

        Assert.Equal(3.0, result.Emitted["rows"]);
        Assert.Equal(30.0, result.Emitted["москва"]);
        Assert.Equal(65.0, result.Emitted["итог_фев"]);
        Assert.Equal(105.0, result.Emitted["всего"]);
        Assert.Equal("Итого", result.Emitted["подпись"]);
    }

    /// <summary>Итог среднего считается по строкам, а не средним средних: так считает Excel.</summary>
    [Fact]
    public void Pivot_MeanTotal_IsMeanOfRows()
    {
        Assert.Equal(25.0, Script.Number(
            "(t |> table.pivot(rows: \"город\", cols: \"месяц\", value: \"сумма\", kind: \"mean\", totals: true))[1][\"Итого\"]",
            prelude: Sales));
    }

    [Fact]
    public void Pivot_ManyRowFields_WithoutColumns()
    {
        RunResult result = Script.RunOk($"""
            {Sales}
            let p = t |> table.pivot(rows: ["город", "месяц"], cols: [], value: "сумма")
            emit rows = len(p)
            emit names = table.columns(p)
            emit пермь_фев = p[3]["сумма"]
            """);

        Assert.Equal(4.0, result.Emitted["rows"]);
        Assert.Equal(new object?[] { "город", "месяц", "сумма" }, (IEnumerable<object?>)result.Emitted["names"]!);
        Assert.Equal(45.0, result.Emitted["пермь_фев"]);
    }

    /// <summary>Месяцы по календарю, как в Excel, а не по алфавиту.</summary>
    [Fact]
    public void Pivot_Sort_MonthsByCalendar()
    {
        RunResult result = Script.RunOk("""
            let p = table.of({ m: ["дек", "апр", "мар"], s: <1, 2, 3> }) |> table.pivot(rows: "m", cols: [], value: "s", sort: true)
            emit a = p[0]["m"]
            emit b = p[1]["m"]
            emit c = p[2]["m"]
            """);

        Assert.Equal(["мар", "апр", "дек"], new[] { result.Emitted["a"], result.Emitted["b"], result.Emitted["c"] });
    }

    [Fact]
    public void Pivot_Sort_OrdersRowsAndColumns()
    {
        RunResult result = Script.RunOk($"""
            {Sales}
            emit names = table.columns(t |> table.pivot(rows: "город", cols: "месяц", value: "сумма", sort: true))
            """);

        Assert.Equal(new object?[] { "город", "янв", "фев" }, (IEnumerable<object?>)result.Emitted["names"]!);
    }

    /// <summary>Опечатка в поле сводной ловится проверкой, до прогона по выгрузке.</summary>
    [Fact]
    public void Pivot_UnknownField_CaughtByCheck()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn,
            Script.CheckCodes($"{Sales}\nemit p = t |> table.pivot(rows: \"горд\", cols: \"месяц\", value: \"сумма\")"));
    }

    /// <summary>Свод большой выгрузки идет за один проход: сто тысяч строк укладываются в секунды.</summary>
    [Fact]
    public void Pivot_LargeTable_IsFast()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        RunResult result = Script.RunOk("""
            let n = 100000
            let t = table.of({
                г: range(n) |> core.map(i => "город" + core.to_str(i % 50)),
                м: range(n) |> core.map(i => i % 12),
                s: vec.of(range(n) |> core.map(i => i * 1.0))
            })
            emit rows = len(t |> table.pivot(rows: "г", cols: "м", value: "s", totals: true))
            """);

        Assert.Equal(51.0, result.Emitted["rows"]);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"свод шел {watch.Elapsed}");
    }

    [Fact]
    public void Melt_TurnsColumnsIntoPairs()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ город: ["Москва"], план: <10>, факт: <12> })
            let m = t |> table.melt(keep: ["город"])
            emit rows = len(m)
            emit name = m[1].variable
            emit value = m[1].value
            """);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal("факт", result.Emitted["name"]);
        Assert.Equal(12.0, result.Emitted["value"]);
    }

    // --- окно, накопление, ранг, сдвиг ---

    /// <summary>Неполное окно даёт пропуск: среднее по двум точкам — это другое число.</summary>
    [Fact]
    public void Rolling_ShortWindow_IsMissing()
    {
        RunResult result = Script.RunOk($"""
            {Series}
            let r = t |> table.rolling(by: "выручка", window: 3)
            emit first = r[0]["выручка_mean"] == none
            emit third = r[2]["выручка_mean"]
            """);

        Assert.True((bool)result.Emitted["first"]!);
        Assert.Equal(20.0, result.Emitted["third"]);
    }

    [Fact]
    public void Rolling_NamesColumnByRequest()
    {
        Assert.Equal(70.0, Script.Number(
            "(t |> table.rolling(by: \"выручка\", window: 2, kind: \"sum\", to: \"окно\"))[3][\"окно\"]",
            prelude: Series));
    }

    [Fact]
    public void Rolling_ZeroWindow_IsRejected()
    {
        Diagnostic error = Script.FailsWith($"{Series}\nemit r = t |> table.rolling(by: \"выручка\", window: 0)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    [Fact]
    public void Cum_AccumulatesFromStart()
    {
        Assert.Equal(100.0, Script.Number("(t |> table.cum(by: \"выручка\"))[3][\"выручка_cum\"]", prelude: Series));
    }

    [Fact]
    public void Rank_CountsFromLargest()
    {
        RunResult result = Script.RunOk($"""
            {Series}
            let r = t |> table.rank(by: "выручка")
            emit top = r[3]["выручка_rank"]
            emit low = r[0]["выручка_rank"]
            """);

        Assert.Equal(1.0, result.Emitted["top"]);
        Assert.Equal(4.0, result.Emitted["low"]);
    }

    [Fact]
    public void Lag_PutsPreviousPeriodNextToCurrent()
    {
        RunResult result = Script.RunOk($"""
            {Series}
            let r = t |> table.lag(by: "выручка", offset: 1)
            emit first = r[0]["выручка_lag"] == none
            emit second = r[1]["выручка_lag"]
            """);

        Assert.True((bool)result.Emitted["first"]!);
        Assert.Equal(10.0, result.Emitted["second"]);
    }

    [Fact]
    public void Lag_NegativeOffset_LooksForward()
    {
        Assert.Equal(20.0, Script.Number(
            "(t |> table.lag(by: \"выручка\", offset: -1))[0][\"выручка_lag\"]",
            prelude: Series));
    }

    // --- найденное при проверке ---

    [Fact]
    public void Cum_IsLinearAndCorrect()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ v: vec.arange(0, 40000) }) |> table.cum(by: "v")
            emit last = t[39999].v_cum
            """);

        Assert.Equal(39999.0 * 40000 / 2, result.Emitted["last"]);
    }

    [Fact]
    public void Aggregate_UnknownKind_FailsEvenForOneValue()
    {
        Assert.NotEmpty(Script.FailsWith("""
            emit t = table.of({ g: ["a"], v: <1> }) |> table.pivot(rows: "g", cols: "g", value: "v", kind: "median")
            """).Message);
    }

    [Fact]
    public void Aggregate_TextColumn_FailsInsteadOfZero()
    {
        Diagnostic error = Script.FailsWith("""
            emit t = table.of({ g: ["a", "a"], v: ["x", "y"] }) |> table.pivot(rows: "g", cols: "g", value: "v")
            """);

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
    }
}

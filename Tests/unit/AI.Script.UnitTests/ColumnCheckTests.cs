using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Имена колонок проверяются до запуска, когда схема таблицы известна.
/// </summary>
/// <remarks>
/// Опечатка в имени колонки — самая частая ошибка модели в табличном скрипте, и до сих пор
/// её было видно только при исполнении. Вторая половина набора не менее важна: там, где схема
/// неизвестна, проверка обязана молчать, иначе ей перестанут верить.
/// </remarks>
public sealed class ColumnCheckTests
{
    private const string T = "let t = table.of({ сумма: <1, 2>, город: [\"a\", \"b\"] })\n";

    [Fact]
    public void RowField_Typo_IsCaught()
    {
        Diagnostic error = Script.CheckDiagnostic(T + "emit r = t |> table.filter(row => row.сума > 0)", DiagnosticCodes.UnknownColumn);

        Assert.Contains("сумма", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrectNames_Pass()
    {
        Assert.Empty(Script.CheckCodes(T + "emit r = t |> table.filter(row => row.сумма > 0) |> table.sort(by: \"город\")"));
    }

    [Fact]
    public void Select_DropsColumnsForTheRestOfPipeline()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn,
            Script.CheckCodes(T + "emit r = t |> table.select([\"сумма\"]) |> table.filter(row => row.город == \"a\")"));
    }

    [Fact]
    public void Derive_AddsColumn()
    {
        Assert.Empty(Script.CheckCodes(
            T + "emit r = t |> table.derive(cols: { налог: row => row.сумма * 0.2 }) |> table.filter(row => row.налог > 0)"));
    }

    [Fact]
    public void GroupBy_ChecksAggregatesAndKnowsItsResult()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn,
            Script.CheckCodes(T + "let g = t |> table.group_by(\"город\", agg: { итого: rows => vec.sum(rows[\"сума\"]) })"));

        const string grouped = T + "let g = t |> table.group_by(\"город\", agg: { итого: rows => vec.sum(rows[\"сумма\"]) })\n";

        Assert.Empty(Script.CheckCodes(grouped + "emit r = g |> table.filter(row => row.итого > 0)"));
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.CheckCodes(grouped + "emit r = g |> table.filter(row => row.итог > 0)"));
    }

    [Fact]
    public void LoopRow_IsChecked()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.CheckCodes(T + "for row in t { print(row.горд) }"));
    }

    [Fact]
    public void RowByIndex_IsChecked()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.CheckCodes(T + "let first = t[0]\nemit r = first.сума"));
    }

    [Fact]
    public void ColumnByName_IsChecked()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.CheckCodes(T + "emit r = t[\"сума\"]"));
    }

    [Fact]
    public void ColumnArguments_AreChecked()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.CheckCodes(T + "emit r = t |> table.sort(by: \"сума\")"));
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.CheckCodes(T + "emit r = t |> table.select([\"город\", \"сума\"])"));
    }

    [Fact]
    public void DeclaredColumnsOfLibraryFunction_AreChecked()
    {
        Diagnostic error = Script.CheckDiagnostic("emit r = io.files() |> table.filter(row => row.sise > 0)", DiagnosticCodes.UnknownColumn);

        Assert.Contains("size", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void SeededTable_IsCheckedByHost()
    {
        ScriptTable sales = ScriptTable.Create([ScriptColumn.From("сумма", [ScriptValue.Num(1), ScriptValue.Num(2)])]);
        var options = new RunOptions { Seeded = new Dictionary<string, object?> { ["продажи"] = sales } };

        CheckResult result = Script.Host().Check("emit r = продажи |> table.filter(row => row.сума > 0)", options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnknownColumn);
    }

    // --- там, где схема неизвестна, проверка молчит ---

    [Fact]
    public void UnknownSource_IsSilent()
    {
        Assert.Empty(Script.CheckCodes("let t = io.read_csv(\"x.csv\")\nemit r = t |> table.filter(row => row.что_угодно > 0)"));
    }

    [Fact]
    public void Reassignment_ForgetsColumns()
    {
        Assert.Empty(Script.CheckCodes(T + "set t = io.read_csv(\"x.csv\")\nemit r = t |> table.filter(row => row.что_угодно > 0)"));
    }

    [Fact]
    public void NestedLambda_IsNotARow()
    {
        Assert.Empty(Script.CheckCodes(T + "emit r = t |> table.filter(row => core.any([1, 2], x => x > 0))"));
    }

    [Fact]
    public void Records_AreNotChecked()
    {
        Assert.Empty(Script.CheckCodes("let cfg = { a: 1 }\nemit r = if core.has(cfg, \"b\") { cfg.b } else { 0 }"));
    }

    // --- проверка колонок не мешает верным скриптам ---

    /// <summary>derive строит колонки по очереди: следующая лямбда видит предыдущую.</summary>
    [Fact]
    public void Derive_SeesEarlierDerivedColumn()
    {
        Assert.Empty(Script.CheckCodes(T + "emit r = t |> table.derive({s2: row => row.сумма * 2, s4: row => row.s2 * 2})"));
    }

    /// <summary>Накопление в пустую таблицу: колонки берутся у приклеенной.</summary>
    [Fact]
    public void Concat_OntoEmptyTable_TakesColumnsOfSecond()
    {
        Assert.Empty(Script.CheckCodes("""
            let acc = table.of({})
            for i in [1, 2] { set acc = table.concat(acc, table.of({x: [i]})) }
            emit r = acc["x"]
            """));
    }

    [Fact]
    public void RowFieldAssignment_ForgetsColumns()
    {
        Assert.Empty(Script.CheckCodes(T + "for row in t { set row[\"b\"] = row.сумма * 2\nprint(row.b) }"));
    }

    [Fact]
    public void TableAssignmentInBranch_ForgetsColumns()
    {
        Assert.Empty(Script.CheckCodes(T + "if false { set t = table.of({b: [1]}) }\nemit r = t[\"b\"]"));
    }
}

using AI.Script.Hosting;
using AI.Script.Semantics;
using ClosedXML.Excel;

namespace AI.Script.UnitTests;

/// <summary>
/// Книга со встроенной сводной таблицей Excel: <c>xls.pivot</c>.
/// </summary>
/// <remarks>
/// Проверяется то, ради чего сводную просят у Excel: в книге живая сводная над умной таблицей
/// данных, она пересчитывается при открытии, а лист итогов несет те же числа значениями.
/// </remarks>
public sealed class XlsPivotTests : IDisposable
{
    private const string Sales = """
        let t = table.of({
            город: ["Москва", "Москва", "Пермь", "Пермь", "Пермь"],
            месяц: ["янв", "фев", "янв", "фев", "фев"],
            канал: ["сайт", "магазин", "сайт", "сайт", "магазин"],
            сумма: ["10,00 ₽", "20,00 ₽", "30,00 ₽", "40,00 ₽", "5,00 ₽"]
        }) |> table.clean()
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aiscript-tests", Guid.NewGuid().ToString("N"));

    public XlsPivotTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки не должна ронять тест.
        }
    }

    [Fact]
    public void Pivot_WritesNativePivotTable()
    {
        RunResult result = RunOk($"""
            {Sales}
            emit r = t |> xls.pivot("свод.xlsx", rows: "город", cols: "месяц", values: "сумма", filters: "канал")
            """);

        Assert.Equal("свод.xlsx", result.Emitted["r"]);
        Assert.Contains(result.Artifacts, artifact => artifact.Kind == "file" && artifact.Title == "свод.xlsx");

        using var workbook = new XLWorkbook(Path.Combine(_root, "свод.xlsx"));

        Assert.Equal(new[] { "Данные", "Сводная", "Итоги" }, workbook.Worksheets.Select(sheet => sheet.Name));
        Assert.Single(workbook.Worksheet("Данные").Tables);

        IXLPivotTable pivot = Assert.Single(workbook.Worksheet("Сводная").PivotTables);

        Assert.Equal(["город"], pivot.RowLabels.Select(field => field.SourceName));
        Assert.Equal(["месяц"], pivot.ColumnLabels.Select(field => field.SourceName));
        Assert.Equal(["канал"], pivot.ReportFilters.Select(field => field.SourceName));
        Assert.Equal("сумма", Assert.Single(pivot.Values).SourceName);
        Assert.Contains("refreshOnLoad=\"1\"", PackagePart("свод.xlsx", "pivotCacheDefinition1.xml"));
    }

    /// <summary>Часть пакета xlsx текстом: признаки сводной, которые ClosedXML при чтении не поднимает.</summary>
    private string PackagePart(string file, string name)
    {
        using System.IO.Compression.ZipArchive zip = System.IO.Compression.ZipFile.OpenRead(Path.Combine(_root, file));
        using var reader = new StreamReader(zip.Entries.First(entry => entry.FullName.EndsWith(name, StringComparison.Ordinal)).Open());

        return reader.ReadToEnd();
    }

    /// <summary>Лист итогов несет те же числа значениями: его видит предпросмотр без сводных.</summary>
    [Fact]
    public void Pivot_ValuesSheet_HoldsTotals()
    {
        RunOk($"""
            {Sales}
            t |> xls.pivot("свод.xlsx", rows: "город", cols: "месяц", values: "сумма")
            """);

        using var workbook = new XLWorkbook(Path.Combine(_root, "свод.xlsx"));
        IXLWorksheet sheet = workbook.Worksheet("Итоги");

        Assert.Equal("Сумма: сумма", sheet.Cell(1, 1).GetString());
        Assert.Equal(["город", "янв", "фев", "Итого"], Enumerable.Range(1, 4).Select(j => sheet.Cell(2, j).GetString()));
        Assert.Equal("Итого", sheet.Cell(5, 1).GetString());
        Assert.Equal(105.0, sheet.Cell(5, 4).GetDouble());
    }

    /// <summary>Выгрузка в сто тысяч строк ложится в книгу со сводной за секунды, а не минуты.</summary>
    [Fact]
    public void Pivot_LargeTable_IsFast()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        RunOk("""
            let n = 100000
            let t = table.of({
                город: range(n) |> core.map(i => "город" + core.to_str(i % 50)),
                месяц: range(n) |> core.map(i => i % 12),
                сумма: vec.of(range(n) |> core.map(i => i * 1.0))
            })
            t |> xls.pivot("большой.xlsx", rows: "город", cols: "месяц", values: "сумма")
            """);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"книга писалась {watch.Elapsed}");
    }

    [Fact]
    public void Pivot_FirstKind_IsRejected()
    {
        RunResult result = Run($"""
            {Sales}
            t |> xls.pivot("свод.xlsx", rows: "город", values: "сумма", kind: "first")
            """);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnknownArgument);
    }

    [Fact]
    public void Pivot_UnknownField_CaughtByCheck()
    {
        Assert.Contains(DiagnosticCodes.UnknownColumn, Script.FullHost()
            .Check("let t = table.of({ город: [\"а\"], сумма: <1> })\nt |> xls.pivot(\"свод.xlsx\", rows: \"горд\", values: \"сумма\")")
            .Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    private RunResult Run(string source) =>
        Script.RunWith(Script.FullHost(), source, new RunOptions { Sandbox = new WorkspaceSandbox(_root) });

    private RunResult RunOk(string source)
    {
        RunResult result = Run(source);

        Assert.True(result.Success, Script.Report(result));

        return result;
    }
}

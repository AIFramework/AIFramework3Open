using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;
using ClosedXML.Excel;

namespace AI.Script.Office;

/// <summary>
/// Книга со встроенной сводной таблицей Excel.
/// </summary>
/// <remarks>
/// Человек просит «сводную» и ждет инструмент Excel: поля можно перетаскивать, фильтровать и
/// обновлять, а не смотреть на застывшие числа. Поэтому книга несет настоящую сводную
/// (PivotTable) над листом данных, оформленным умной таблицей: Excel пересчитывает ее сам при
/// открытии, и новые строки данных попадают в нее по «Обновить».
/// <para>
/// Третий лист держит те же итоги значениями, посчитанными <c>table.pivot</c>. Его видят
/// просмотрщики без сводных (предпросмотр чата, редактор таблиц), по нему же отвечает модель,
/// и числа на нем совпадают со сводной, потому что посчитаны по тем же строкам.
/// </para>
/// </remarks>
public static partial class XlsModule
{
    private const string DataSheet = "Данные";
    private const string PivotSheet = "Сводная";
    private const string ValuesSheet = "Итоги";
    private const string MoneyFormat = "#,##0.00 ₽";
    private const string IntegerFormat = "#,##0";
    private const string FractionFormat = "#,##0.00";

    /// <summary>Свертки сводной Excel; «first» в Excel нет, и молча подменять ее нельзя.</summary>
    private static readonly Dictionary<string, (XLPivotSummary Summary, string Title)> s_summaries = new(StringComparer.Ordinal)
    {
        ["sum"] = (XLPivotSummary.Sum, "Сумма"),
        ["mean"] = (XLPivotSummary.Average, "Среднее"),
        ["count"] = (XLPivotSummary.Count, "Количество"),
        ["min"] = (XLPivotSummary.Minimum, "Минимум"),
        ["max"] = (XLPivotSummary.Maximum, "Максимум"),
    };

    [ScriptFn("pivot", "Книга Excel со встроенной сводной таблицей: лист данных, сводная Excel, итоги значениями",
        Example = "t |> xls.pivot(\"свод.xlsx\", rows: \"город\", cols: \"месяц\", values: \"сумма\")")]
    public static async Task<string> Pivot(
        IScriptContext context,
        [ScriptParam("таблица исходных строк, не свернутая")] ScriptTable t,
        [ScriptParam("путь .xlsx относительно рабочей папки")] string path,
        [ScriptParam("колонка либо список колонок в строки сводной")] ScriptValue rows,
        [ScriptParam("колонка либо список колонок со значениями")] ScriptValue values,
        [ScriptParam("колонка либо список колонок в колонки сводной; пусто — без разворота")] ScriptValue cols = default,
        [ScriptParam("свёртка: \"sum\", \"mean\", \"count\", \"min\", \"max\"")] string kind = "sum",
        [ScriptParam("колонки в фильтры над сводной")] ScriptValue filters = default,
        [ScriptParam("строка и колонка общих итогов")] bool totals = true,
        [ScriptParam("порядок строк и колонок по ключам; иначе порядок появления")] bool sort = true)
    {
        if (!s_summaries.TryGetValue(kind, out var summary))
        {
            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"xls.pivot: свёртки «{kind}» в сводной Excel нет",
                "известны: sum, mean, count, min, max");
        }

        IReadOnlyList<string> rowNames = Fields(rows, t);
        IReadOnlyList<string> valueNames = Fields(values, t);
        IReadOnlyList<string> colNames = Fields(cols, t);
        IReadOnlyList<string> filterNames = Fields(filters, t);

        if (rowNames.Count == 0 || valueNames.Count == 0)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "xls.pivot: нужны хотя бы одно поле строк и одно поле значений",
                "например: rows: \"город\", values: \"сумма\"");
        }

        using var workbook = new XLWorkbook();

        IXLWorksheet data = workbook.Worksheets.Add(DataSheet);
        IXLWorksheet pivot = workbook.Worksheets.Add(PivotSheet);

        Fill(data, t, 1);

        IXLTable source = data.Range(1, 1, Math.Max(2, t.RowCount + 1), Math.Max(1, t.ColumnCount)).CreateTable(DataSheet);

        // Место под фильтры над сводной ClosedXML отводит сам, сдвигая ее вниз.
        IXLPivotTable table = pivot.PivotTables.Add(PivotSheet, pivot.Cell(1, 1), source);

        table.Layout = XLPivotLayout.Tabular;
        table.ShowGrandTotalsRows = totals;
        table.ShowGrandTotalsColumns = totals;
        table.PivotCache.SetRefreshDataOnOpen(true);

        foreach (string name in filterNames) table.ReportFilters.Add(name);
        // Порядок «от А до Я» задается и сводной Excel, и листу итогов: иначе месяцы на двух
        // листах стояли бы по-разному, и сверять их пришлось бы глазами.
        XLPivotSortType order = sort ? XLPivotSortType.Ascending : XLPivotSortType.Default;

        foreach (string name in rowNames) table.RowLabels.Add(name).SetSort(order);
        foreach (string name in colNames) table.ColumnLabels.Add(name).SetSort(order);

        foreach (string name in valueNames)
        {
            IXLPivotValue value = table.Values.Add(name, $"{summary.Title}: {name}").SetSummaryFormula(summary.Summary);

            value.NumberFormat.Format = ValueFormat(t.Column(name), kind);
        }

        IXLWorksheet snapshot = workbook.Worksheets.Add(ValuesSheet);

        Snapshot(snapshot, t, rows, cols, valueNames, kind, summary.Title, totals, sort);

        // Автоширину сводной Excel при пересчете на открытии не применяет, и крупные итоги
        // стояли решетками «########». Ширина берется с листа итогов: там те же числа и подписи.
        pivot.ColumnWidth = Math.Clamp(snapshot.ColumnsUsed().Select(column => column.Width).DefaultIfEmpty(0).Max(), 10, 40);

        data.Columns().AdjustToContents(1, Math.Min(t.RowCount + 1, 200));
        workbook.Worksheet(PivotSheet).SetTabActive();

        return await Save(context, workbook, path).ConfigureAwait(false);
    }

    /// <summary>
    /// Лист итогов значениями: по блоку на каждое поле значений.
    /// </summary>
    private static void Snapshot(
        IXLWorksheet sheet, ScriptTable t, ScriptValue rows, ScriptValue cols,
        IReadOnlyList<string> valueNames, string kind, string title, bool totals, bool sort)
    {
        ScriptValue columns = cols.IsNone ? ScriptValue.List(ScriptList.Own([])) : cols;
        int top = 1;

        foreach (string name in valueNames)
        {
            ScriptTable block = TableModule.Pivot(t, rows, columns, name, kind, totals, sort);

            sheet.Cell(top, 1).Value = $"{title}: {name}";
            sheet.Cell(top, 1).Style.Font.Bold = true;

            Fill(sheet, block, top + 1);

            // Слева поля строк, правее числа: им тот же формат, что значениям сводной.
            int keys = Fields(rows, t).Count;

            if (block.RowCount > 0 && keys < block.ColumnCount)
                sheet.Range(top + 2, keys + 1, top + 1 + block.RowCount, block.ColumnCount).Style.NumberFormat.Format = ValueFormat(t.Column(name), kind);

            if (totals && block.RowCount > 0) sheet.Row(top + 1 + block.RowCount).Style.Font.Bold = true;

            top += block.RowCount + 3;
        }

        sheet.Columns().AdjustToContents();
    }

    /// <summary>Поля сводной: колонка либо список колонок; каждая обязана быть в таблице.</summary>
    private static IReadOnlyList<string> Fields(ScriptValue value, ScriptTable t)
    {
        if (value.IsNone) return [];

        IReadOnlyList<string> names = [.. TableModule.ColumnNames(value, "xls.pivot").Where(name => name.Length > 0)];

        foreach (string name in names) _ = t.Column(name);

        return names;
    }

    /// <summary>
    /// Формат значений сводной: деньги рублями, целые без дробной части, прочее до сотых.
    /// </summary>
    /// <remarks>
    /// Маска «# ##0.##» оставляла у целого висящую запятую, а пробел в маске Excel читает буквой:
    /// разряды разделяет запятая кода формата, на экране она становится разделителем языка.
    /// </remarks>
    private static string ValueFormat(ScriptColumn column, string kind)
    {
        if (kind == "count") return IntegerFormat;
        if (IsExact(column)) return MoneyFormat;
        if (kind == "mean") return FractionFormat;

        for (int i = 0; i < column.Count; i++)
        {
            if (column[i].Type == ScriptType.Num && column[i].RawNumber != Math.Floor(column[i].RawNumber)) return FractionFormat;
        }

        return IntegerFormat;
    }

    /// <summary>Вся колонка в точных числах: тогда это деньги, и формат у них денежный.</summary>
    private static bool IsExact(ScriptColumn column)
    {
        bool any = false;

        for (int i = 0; i < column.Count; i++)
        {
            if (column[i].IsNone) continue;
            if (column[i].Type != ScriptType.Dec) return false;

            any = true;
        }

        return any;
    }
}

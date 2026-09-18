using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using ClosedXML.Excel;
using System.Text;

namespace AI.Script.Office;

/// <summary>
/// Пространство <c>xls</c>: книги Excel.
/// </summary>
/// <remarks>
/// Большая часть пользовательских таблиц — это xlsx, и до сих пор язык их не открывал: CSV
/// получался пересохранением, на котором терялись и листы, и форматы, и деньги.
/// <para>
/// Деньги в книге помечены форматом ячейки, а не текстом, поэтому колонка с денежным форматом
/// читается точным числом: сумма по ней сойдётся с итогом в самой книге. Всё остальное —
/// обычные числа, даты и текст.
/// </para>
/// </remarks>
[ScriptModule("xls", "Книги Excel: листы, диапазоны, шапки в две строки", Version = "0.1", Group = "данные")]
public static class XlsModule
{
    /// <summary>Разделитель уровней в шапке из нескольких строк.</summary>
    private const string HeaderSeparator = " / ";

    [ScriptFn("sheets", "Имена листов книги", Example = "xls.sheets(\"продажи.xlsx\")")]
    public static async Task<string[]> Sheets(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        using XLWorkbook workbook = await Open(context, path).ConfigureAwait(false);

        return [.. workbook.Worksheets.Select(sheet => sheet.Name)];
    }

    /// <summary>
    /// Читает лист книги таблицей.
    /// </summary>
    /// <remarks>
    /// Шапка бывает в две строки с объединёнными ячейками («Выручка» над «план» и «факт»), и
    /// разобрать её обязана библиотека: скрипт, который делает это сам, каждый раз делает
    /// по-своему. Имена уровней склеиваются через дробь — «Выручка / план».
    /// </remarks>
    [ScriptFn("read", "Читает лист книги таблицей: диапазон, шапка в одну либо две строки",
        Example = "xls.read(\"продажи.xlsx\", sheet: \"Факт\", header: 2)", Reads = "xlsx")]
    public static async Task<ScriptTable> Read(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("имя листа; пусто — первый")] string sheet = "",
        [ScriptParam("диапазон вида A1:H500; пусто — занятая область")] string range = "",
        [ScriptParam("сколько строк занимает шапка; 0 — шапки нет")] int header = 1)
    {
        using XLWorkbook workbook = await Open(context, path).ConfigureAwait(false);

        IXLWorksheet worksheet = Sheet(workbook, sheet, path);
        IXLRange? used = range.Length > 0 ? worksheet.Range(range) : worksheet.RangeUsed();

        if (used == null) return ScriptTable.Empty;

        int rows = used.RowCount();
        int width = used.ColumnCount();
        int titles = Math.Clamp(header, 0, Math.Max(0, rows));

        var names = Names(used, titles, width);
        var columns = new List<ScriptColumn>(width);

        for (int j = 1; j <= width; j++)
        {
            var values = new ScriptValue[rows - titles];

            for (int i = titles + 1; i <= rows; i++) values[i - titles - 1] = Cell(used.Cell(i, j));

            columns.Add(ScriptColumn.Own(names[j - 1], values));
        }

        context.CountAllocation((long)Math.Max(0, rows - titles) * width);

        return ScriptTable.Create(columns);
    }

    [ScriptFn("write", "Пишет таблицу листом книги", Example = "сводная |> xls.write(\"итог.xlsx\")",
        Writes = "xlsx")]
    public static async Task<string> Write(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("имя листа")] string sheet = "Лист1")
    {
        using var workbook = new XLWorkbook();

        IXLWorksheet worksheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheet) ? "Лист1" : sheet);

        for (int j = 0; j < t.ColumnCount; j++)
        {
            worksheet.Cell(1, j + 1).Value = t[j].Name;
            worksheet.Cell(1, j + 1).Style.Font.Bold = true;

            for (int i = 0; i < t.RowCount; i++) Write(worksheet.Cell(i + 2, j + 1), t[j][i]);
        }

        if (t.ColumnCount > 0) worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        byte[] bytes = stream.ToArray();

        await context.Sandbox.WriteAsync(path, bytes, context.Cancellation).ConfigureAwait(false);

        context.FileSaved(new Hosting.ScriptFileInfo(path, bytes.LongLength, DateTimeOffset.UtcNow));

        return path;
    }

    private static async Task<XLWorkbook> Open(IScriptContext context, string path)
    {
        if (await context.Sandbox.InfoAsync(path, context.Cancellation).ConfigureAwait(false) == null)
            throw new ScriptError(DiagnosticCodes.FileNotFound, $"xls: файл не найден — {path}");

        byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

        try
        {
            return new XLWorkbook(new MemoryStream(bytes, writable: false));
        }
        catch (Exception exception) when (exception is not ScriptError)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"xls: '{path}' не открывается как книга Excel — {exception.Message}",
                "старый формат .xls не читается: пересохраните в .xlsx");
        }
    }

    private static IXLWorksheet Sheet(XLWorkbook workbook, string name, string path)
    {
        if (name.Length == 0) return workbook.Worksheet(1);

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            if (string.Equals(worksheet.Name, name, StringComparison.OrdinalIgnoreCase)) return worksheet;
        }

        throw new ScriptError(
            DiagnosticCodes.UnknownArgument,
            $"xls: в книге '{path}' нет листа «{name}»",
            $"листы: {string.Join(", ", workbook.Worksheets.Select(sheet => sheet.Name))}");
    }

    /// <summary>
    /// Имена колонок по шапке из нескольких строк.
    /// </summary>
    /// <remarks>
    /// Пустая ячейка верхнего уровня достаётся от соседа слева: так записывают объединённую
    /// ячейку, и без этого правила «Выручка» над «план» и «факт» превратилась бы в одну колонку
    /// с именем и одну без.
    /// </remarks>
    private static List<string> Names(IXLRange used, int titles, int width)
    {
        var names = new List<string>(width);

        for (int j = 1; j <= width; j++)
        {
            var parts = new List<string>(Math.Max(1, titles));

            for (int i = 1; i <= titles; i++)
            {
                string part = Title(used, i, j, titles);

                if (part.Length > 0 && (parts.Count == 0 || !string.Equals(parts[^1], part, StringComparison.Ordinal)))
                    parts.Add(part);
            }

            string name = parts.Count > 0 ? string.Join(HeaderSeparator, parts) : $"c{j - 1}";

            while (names.Contains(name, StringComparer.Ordinal)) name += "_";

            names.Add(name);
        }

        return names;
    }

    private static string Title(IXLRange used, int row, int column, int titles)
    {
        IXLCell cell = used.Cell(row, column);
        string text = Text(cell);

        if (text.Length > 0) return text;

        if (cell.IsMerged()) return Text(cell.MergedRange().FirstCell());

        // Верхний уровень шапки пишут один раз на группу колонок: пустая ячейка наследует
        // заголовок слева. Нижняя строка шапки так не наследует — там пустая ячейка означает,
        // что у колонки просто нет второго уровня, и сосед слева к ней отношения не имеет.
        if (row >= titles) return string.Empty;

        for (int j = column - 1; j >= 1; j--)
        {
            string left = Text(used.Cell(row, j));

            if (left.Length > 0) return left;
        }

        return string.Empty;
    }

    private static string Text(IXLCell cell)
    {
        XLCellValue value = cell.Value;

        return value.IsBlank ? string.Empty : value.ToString()?.Trim() ?? string.Empty;
    }

    /// <summary>Значение ячейки: деньги — точным числом, остальное — числом, датой либо текстом.</summary>
    private static ScriptValue Cell(IXLCell cell)
    {
        XLCellValue value = cell.Value;

        if (value.IsBlank) return ScriptValue.None;
        if (value.IsBoolean) return ScriptValue.Bool(value.GetBoolean());
        if (value.IsDateTime) return ScriptValue.Date(value.GetDateTime());
        if (value.IsTimeSpan) return ScriptValue.Dur(value.GetTimeSpan());

        if (value.IsNumber)
        {
            double number = value.GetNumber();

            return IsMoney(cell) ? ScriptValue.Dec(Exact(number)) : ScriptValue.Num(number);
        }

        string text = value.ToString() ?? string.Empty;

        return text.Length == 0 ? ScriptValue.None : ScriptValue.Str(text);
    }

    /// <summary>Помечена ли ячейка денежным форматом: знак валюты в маске числа.</summary>
    private static bool IsMoney(IXLCell cell)
    {
        string format = cell.Style.NumberFormat.Format;

        if (format.Length == 0) return false;

        // Метка локали «[$-419]» знака валюты не несет, а «[$₽-419]» несет: внутри скобок валюта
        // только между «$» и «-», снаружи скобок любой знак валюты.
        foreach (System.Text.RegularExpressions.Match tag in System.Text.RegularExpressions.Regex.Matches(format, @"\[\$([^\]-]*)(?:-[^\]]*)?\]"))
        {
            if (tag.Groups[1].Value.Length > 0) return true;
        }

        format = System.Text.RegularExpressions.Regex.Replace(format, @"\[[^\]]*\]", string.Empty);

        foreach (char c in format)
        {
            if ("₽$€£¥₴₸".Contains(c, StringComparison.Ordinal)) return true;
        }

        return format.Contains("руб", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Число ячейки точным числом.
    /// </summary>
    /// <remarks>
    /// Книга хранит число двоичным, поэтому копейка восстанавливается округлением до сотых:
    /// денежная ячейка и записана в копейках, а разницы в пятнадцатом знаке в отчёте быть не
    /// должно.
    /// </remarks>
    private static decimal Exact(double number) => Math.Round((decimal)number, 2, MidpointRounding.ToEven);

    private static void Write(IXLCell cell, ScriptValue value)
    {
        switch (value.Type)
        {
            case ScriptType.None:
                break;

            case ScriptType.Num:
                cell.Value = value.RawNumber;
                break;

            case ScriptType.Dec:
                cell.Value = value.AsDecimal();
                cell.Style.NumberFormat.Format = "# ##0.00 ₽";
                break;

            case ScriptType.Bool:
                cell.Value = value.RawNumber != 0;
                break;

            case ScriptType.Date:
                cell.Value = value.AsDate();
                break;

            default:
                cell.Value = ScriptFormatter.Format(value);
                break;
        }
    }
}

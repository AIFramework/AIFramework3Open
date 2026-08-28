using AI.DataStructs.Algebraic;
using System.Globalization;
using System.Text;

namespace AI.Script.Runtime;

/// <summary>
/// Печать значений языка.
/// </summary>
/// <remarks>
/// Числа печатаются с инвариантной культурой: транскрипт читают и человек, и модель, а
/// запятая в роли десятичного разделителя ломает второе прочтение молча.
/// <para>
/// Длинные векторы и списки усекаются: транскрипт — это отчёт о ходе работы, а не выгрузка
/// данных. Полное значение вынимается через <c>emit</c>.
/// </para>
/// </remarks>
public static class ScriptFormatter
{
    /// <summary>Сколько элементов последовательности печатается до усечения.</summary>
    public const int PreviewLimit = 12;

    /// <summary>Сколько строк таблицы печатается до усечения.</summary>
    public const int TableRowLimit = 8;

    /// <summary>Предельная ширина ячейки таблицы при печати.</summary>
    public const int TableCellLimit = 24;

    /// <summary>Печатает значение так, как его увидит пользователь в транскрипте.</summary>
    public static string Format(ScriptValue value) => Format(value, quoteStrings: false);

    /// <summary>Печатает значение; строки внутри контейнеров берутся в кавычки.</summary>
    public static string Format(ScriptValue value, bool quoteStrings) => value.Type switch
    {
        ScriptType.None => "none",
        ScriptType.Num => Number(value.RawNumber),
        ScriptType.Bool => value.RawNumber != 0 ? "true" : "false",
        ScriptType.Str => quoteStrings ? Quote(value.AsString()) : value.AsString(),
        ScriptType.Date => FormatDate(value.AsDate()),
        ScriptType.Dur => FormatDuration(value.AsDuration()),
        ScriptType.Vec => FormatVector(value.AsVector()),
        ScriptType.Mat => FormatMatrix(value.AsMatrix()),
        ScriptType.Table => FormatTable(value.AsTable()),
        ScriptType.List => FormatList(value.AsList()),
        ScriptType.Record => FormatRecord(value.AsRecord()),
        ScriptType.Range => value.AsRange().ToString(),
        ScriptType.Fn => value.AsCallable().ToString() ?? "<fn>",
        ScriptType.Handle => value.AsHandle().ToString(),
        _ => "?",
    };

    /// <summary>
    /// Краткое описание значения: тип и размер, но не содержимое.
    /// </summary>
    /// <remarks>
    /// Нужно там, где значение только упоминается — в графе прогона и в сообщениях о стадиях.
    /// Печатать туда таблицу целиком нельзя: строка на десять тысяч символов в подписи узла
    /// делает граф нечитаемым, а сам отчёт — бесполезным.
    /// </remarks>
    public static string Summary(ScriptValue value) => value.Type switch
    {
        ScriptType.None => "none",
        ScriptType.Num or ScriptType.Bool or ScriptType.Date or ScriptType.Dur => Format(value),
        ScriptType.Str => $"str({value.AsString().Length})",
        ScriptType.Vec => $"vec({value.AsVector().Count})",
        ScriptType.Mat => $"mat({value.AsMatrix().Height}×{value.AsMatrix().Width})",
        ScriptType.Table => $"table({value.AsTable().RowCount}×{value.AsTable().ColumnCount})",
        ScriptType.List => $"list({value.AsList().Count})",
        ScriptType.Record => $"record({value.AsRecord().Count})",
        ScriptType.Handle => value.AsHandle().TypeName,
        _ => value.Type.ToName(),
    };

    /// <summary>Печатает число без потери значащих цифр и без локальных разделителей.</summary>
    public static string Number(double value)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsPositiveInfinity(value)) return "inf";
        if (double.IsNegativeInfinity(value)) return "-inf";

        if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
            return ((long)value).ToString(CultureInfo.InvariantCulture);

        return value.ToString("G15", CultureInfo.InvariantCulture);
    }

    private static string Quote(string text) => $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string FormatDate(DateTime value) =>
        value.TimeOfDay == TimeSpan.Zero
            ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalMilliseconds < 1000) return $"{Number(value.TotalMilliseconds)}ms";
        if (value.TotalSeconds < 60) return $"{Number(value.TotalSeconds)}s";
        if (value.TotalMinutes < 60) return $"{Number(value.TotalMinutes)}m";
        if (value.TotalHours < 24) return $"{Number(value.TotalHours)}h";
        return $"{Number(value.TotalDays)}d";
    }

    private static string FormatVector(Vector vector)
    {
        var builder = new StringBuilder("<");
        int shown = Math.Min(vector.Count, PreviewLimit);

        for (int i = 0; i < shown; i++)
        {
            if (i > 0) _ = builder.Append(", ");
            _ = builder.Append(Number(vector[i]));
        }

        if (vector.Count > shown) _ = builder.Append($", ... ({vector.Count})");

        return builder.Append('>').ToString();
    }

    private static string FormatMatrix(Matrix matrix) =>
        $"<mat {matrix.Height}×{matrix.Width}>";

    /// <summary>
    /// Печатает таблицу шапкой и первыми строками.
    /// </summary>
    /// <remarks>
    /// Транскрипт — отчёт о ходе работы, а не выгрузка данных: печатать сто тысяч строк
    /// незачем, а знать имена колонок и увидеть первые значения нужно почти всегда.
    /// </remarks>
    private static string FormatTable(ScriptTable table)
    {
        if (table.ColumnCount == 0) return "<table: пустая>";

        var builder = new StringBuilder($"<table {table.RowCount}×{table.ColumnCount}>");
        var widths = new int[table.ColumnCount];
        int shown = Math.Min(table.RowCount, TableRowLimit);

        for (int j = 0; j < table.ColumnCount; j++)
        {
            widths[j] = table[j].Name.Length;

            for (int i = 0; i < shown; i++)
                widths[j] = Math.Max(widths[j], Cell(table[j][i]).Length);

            widths[j] = Math.Min(widths[j], TableCellLimit);
        }

        _ = builder.AppendLine().Append("  ");

        for (int j = 0; j < table.ColumnCount; j++)
            _ = builder.Append(Fit(table[j].Name, widths[j])).Append("  ");

        for (int i = 0; i < shown; i++)
        {
            _ = builder.AppendLine().Append("  ");

            for (int j = 0; j < table.ColumnCount; j++)
                _ = builder.Append(Fit(Cell(table[j][i]), widths[j])).Append("  ");
        }

        if (table.RowCount > shown) _ = builder.AppendLine().Append($"  … ещё {table.RowCount - shown} строк");

        return builder.ToString();
    }

    private static string Cell(ScriptValue value) => Format(value, quoteStrings: false);

    private static string Fit(string text, int width) =>
        text.Length > width ? text[..Math.Max(1, width - 1)] + "…" : text.PadRight(width);

    private static string FormatList(ScriptList list)
    {
        var builder = new StringBuilder("[");
        int shown = Math.Min(list.Count, PreviewLimit);

        for (int i = 0; i < shown; i++)
        {
            if (i > 0) _ = builder.Append(", ");
            _ = builder.Append(Format(list[i], quoteStrings: true));
        }

        if (list.Count > shown) _ = builder.Append($", ... ({list.Count})");

        return builder.Append(']').ToString();
    }

    private static string FormatRecord(ScriptRecord record)
    {
        var builder = new StringBuilder("{");
        bool first = true;

        foreach (var pair in record.Pairs())
        {
            if (!first) _ = builder.Append(", ");
            first = false;
            _ = builder.Append(pair.Key).Append(": ").Append(Format(pair.Value, quoteStrings: true));
        }

        return builder.Append('}').ToString();
    }
}

using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Globalization;
using System.Text;

namespace AI.Script.Std;

/// <summary>
/// Чтение и запись CSV.
/// </summary>
/// <remarks>
/// Свой разбор, а не готовый загрузчик из <c>AI.DataPrepaire</c>: таблице языка нужен вывод
/// типа по колонке и внятная диагностика с номером строки, а не «массив строк». Формат
/// разбирается по RFC 4180: кавычки, удвоенные кавычки внутри поля, переводы строк в поле.
/// </remarks>
public static class Csv
{
    /// <summary>Разбирает CSV в таблицу.</summary>
    /// <param name="text">Содержимое файла.</param>
    /// <param name="separator">Разделитель полей; пусто — определить по первой строке.</param>
    /// <param name="header">Есть ли строка заголовка.</param>
    /// <param name="source">Имя источника для сообщений об ошибках.</param>
    public static ScriptTable Parse(string text, string separator, bool header, string source)
    {
        char delimiter = separator.Length > 0 ? separator[0] : Sniff(text);
        List<List<string>> rows = ReadRows(text, delimiter, source);

        if (rows.Count == 0) return ScriptTable.Empty;

        List<string> names = header
            ? MakeUnique(rows[0])
            : [.. Enumerable.Range(0, rows[0].Count).Select(i => $"c{i}")];

        int first = header ? 1 : 0;
        int width = names.Count;
        var cells = new List<string?[]>(rows.Count - first);

        for (int i = first; i < rows.Count; i++)
        {
            List<string> row = rows[i];

            if (row.Count != width)
            {
                throw new ScriptError(
                    DiagnosticCodes.BadFileFormat,
                    $"{source}: в строке {i + 1} полей {row.Count}, а в заголовке {width}",
                    "проверьте разделитель и кавычки; разделитель задаётся аргументом sep");
            }

            var line = new string?[width];

            for (int j = 0; j < width; j++) line[j] = row[j];

            cells.Add(line);
        }

        var columns = new List<ScriptColumn>(width);

        for (int j = 0; j < width; j++) columns.Add(BuildColumn(names[j], cells, j));

        return ScriptTable.Create(columns);
    }

    /// <summary>Печатает таблицу в CSV.</summary>
    public static string Write(ScriptTable table, string separator)
    {
        char delimiter = separator.Length > 0 ? separator[0] : ',';
        var builder = new StringBuilder();

        for (int j = 0; j < table.ColumnCount; j++)
        {
            if (j > 0) _ = builder.Append(delimiter);
            _ = builder.Append(Escape(table[j].Name, delimiter));
        }

        for (int i = 0; i < table.RowCount; i++)
        {
            _ = builder.Append('\n');

            for (int j = 0; j < table.ColumnCount; j++)
            {
                if (j > 0) _ = builder.Append(delimiter);

                ScriptValue value = table[j][i];
                string text = value.IsNone ? string.Empty : ScriptFormatter.Format(value, quoteStrings: false);

                _ = builder.Append(Escape(text, delimiter));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Определяет разделитель по первой строке.
    /// </summary>
    /// <remarks>
    /// Точка с запятой в первую очередь: русскоязычные выгрузки из Excel используют её, и
    /// молча прочитанный такой файл превращается в таблицу из одной колонки.
    /// </remarks>
    private static char Sniff(string text)
    {
        int end = text.IndexOf('\n');
        string line = end < 0 ? text : text[..end];

        foreach (char candidate in new[] { ';', ',', '\t', '|' })
        {
            if (line.Contains(candidate, StringComparison.Ordinal)) return candidate;
        }

        return ',';
    }

    private static List<List<string>> ReadRows(string text, char delimiter, string source)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool any = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c != '"')
                {
                    _ = field.Append(c);
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    _ = field.Append('"');
                    i++;
                    continue;
                }

                quoted = false;
                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                quoted = true;
                any = true;
                continue;
            }

            if (c == delimiter)
            {
                row.Add(field.ToString());
                _ = field.Clear();
                any = true;
                continue;
            }

            if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                if (any || field.Length > 0 || row.Count > 0)
                {
                    row.Add(field.ToString());
                    rows.Add(row);
                    row = [];
                    _ = field.Clear();
                    any = false;
                }

                continue;
            }

            _ = field.Append(c);
            any = true;
        }

        if (quoted)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"{source}: незакрытая кавычка в конце файла");
        }

        if (any || field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Собирает колонку, выводя её тип по содержимому.
    /// </summary>
    /// <remarks>
    /// Колонка считается числовой, только если числом является КАЖДОЕ непустое поле: одна
    /// строка «н/д» посреди чисел означает, что колонка не числовая, и молча превращать её
    /// в числа с пропусками нельзя — это меняет данные.
    /// </remarks>
    private static ScriptColumn BuildColumn(string name, List<string?[]> cells, int index)
    {
        bool numeric = true;

        foreach (string?[] row in cells)
        {
            string? cell = row[index];
            if (string.IsNullOrEmpty(cell)) continue;

            if (TryParseNumber(cell, out _)) continue;

            numeric = false;
            break;
        }

        var values = new ScriptValue[cells.Count];

        for (int i = 0; i < cells.Count; i++)
        {
            string? cell = cells[i][index];

            if (string.IsNullOrEmpty(cell))
            {
                values[i] = ScriptValue.None;
                continue;
            }

            values[i] = numeric && TryParseNumber(cell, out double number)
                ? ScriptValue.Num(number)
                : ScriptValue.Str(cell);
        }

        return ScriptColumn.Own(name, values);
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static List<string> MakeUnique(List<string> names)
    {
        var result = new List<string>(names.Count);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i].Trim();
            if (name.Length == 0) name = $"c{i}";

            if (seen.TryGetValue(name, out int count))
            {
                seen[name] = count + 1;
                name = $"{name}_{count + 1}";
            }
            else
            {
                seen[name] = 0;
            }

            result.Add(name);
        }

        return result;
    }

    private static string Escape(string text, char delimiter)
    {
        bool needsQuotes = text.Contains(delimiter) || text.Contains('"')
            || text.Contains('\n') || text.Contains('\r');

        return needsQuotes ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
    }
}

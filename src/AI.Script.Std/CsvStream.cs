using System.Text;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// CSV потоком: запись за записью, без чтения файла целиком.
/// </summary>
/// <remarks>
/// Разбор тот же, что у <see cref="Csv"/> (RFC 4180: кавычки, удвоенные кавычки, переводы строк в
/// поле), но по символам из потока. Тип колонки решается по первым <see cref="Sample"/> строкам:
/// всего файла заранее не видно. Колонка числовая, если числом было каждое непустое поле
/// образца; нечисло, встреченное в такой колонке позже, остается строкой, а не пропадает.
/// </remarks>
internal sealed class CsvStream(TextReader reader, string source)
{
    /// <summary>Сколько строк смотреть, решая тип колонки.</summary>
    public const int Sample = 1000;

    private readonly StringBuilder _field = new();

    /// <summary>Символ, прочитанный заглядыванием вперед; -1 — такого нет.</summary>
    /// <remarks>
    /// Свой, а не <see cref="StreamReader.Peek"/>: тот на границе буфера сетевого потока отвечает
    /// «конец», хотя данные еще идут, и удвоенная кавычка в поле разбиралась бы как закрывающая.
    /// </remarks>
    private int _pending = -1;

    /// <summary>Разбирает все записи файла и отдает их типизированными в отбор.</summary>
    public async Task ScanAsync(string separator, Func<IReadOnlyList<string>, RowScan> start, CancellationToken ct)
    {
        string? headerLine = await HeaderAsync(ct).ConfigureAwait(false);

        if (headerLine is null) return;

        char delimiter = separator.Length > 0 ? separator[0] : Csv.Sniff(headerLine);
        List<string> names = Csv.MakeUnique(Parse(headerLine, delimiter));
        RowScan scan = start(names);
        var sample = new List<List<string>>(Sample);
        List<string>? row;

        while (sample.Count < Sample && (row = Next(delimiter, ct)) is not null)
            sample.Add(Check(row, names.Count, sample.Count + 2));

        bool[] numeric = [.. Enumerable.Range(0, names.Count).Select(j => sample.All(r => r[j].Length == 0 || Csv.TryParseNumber(r[j], out _)))];

        foreach (List<string> sampled in sample)
        {
            if (!await scan.OfferAsync(Typed(sampled, numeric)).ConfigureAwait(false)) return;
        }

        long line = sample.Count + 1;

        while ((row = Next(delimiter, ct)) is not null)
        {
            if (!await scan.OfferAsync(Typed(Check(row, names.Count, ++line), numeric)).ConfigureAwait(false)) return;
        }
    }

    /// <summary>Следующая запись; <c>null</c> — файл кончился.</summary>
    /// <remarks>
    /// Посимвольно и синхронно: читатель потока буферизует сам, а асинхронный вызов на каждый
    /// символ файла в гигабайты стоил бы дороже самого разбора.
    /// </remarks>
    private List<string>? Next(char delimiter, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var row = new List<string>();
        bool quoted = false;
        bool any = false;

        _field.Clear();

        while (true)
        {
            int read = ReadChar();

            if (read < 0)
            {
                if (quoted) throw new ScriptError(DiagnosticCodes.BadFileFormat, $"{source}: незакрытая кавычка в конце файла");

                if (!any && _field.Length == 0 && row.Count == 0) return null;

                row.Add(_field.ToString());
                return row;
            }

            char c = (char)read;

            if (quoted)
            {
                if (c != '"')
                {
                    _field.Append(c);
                }
                else if (PeekChar() == '"')
                {
                    _field.Append('"');
                    _ = ReadChar();
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            if (c == '"' && _field.Length == 0)
            {
                quoted = true;
                any = true;
            }
            else if (c == delimiter)
            {
                row.Add(_field.ToString());
                _field.Clear();
                any = true;
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && PeekChar() == '\n') _ = ReadChar();

                if (any || _field.Length > 0 || row.Count > 0)
                {
                    row.Add(_field.ToString());
                    return row;
                }
            }
            else
            {
                _field.Append(c);
                any = true;
            }
        }
    }

    /// <summary>
    /// Строка заголовка: пустые строки в начале пропускаются, поле в кавычках может занимать
    /// несколько строк.
    /// </summary>
    private async Task<string?> HeaderAsync(CancellationToken ct)
    {
        string? line;

        do
        {
            line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        }
        while (line is not null && line.Trim().Length == 0);

        while (line is not null && line.Count(c => c == '"') % 2 == 1
            && await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } next)
            line += "\n" + next;

        return line;
    }

    private int ReadChar()
    {
        if (_pending < 0) return reader.Read();

        int read = _pending;

        _pending = -1;
        return read;
    }

    private int PeekChar()
    {
        if (_pending < 0) _pending = reader.Read();

        return _pending;
    }

    private List<string> Check(List<string> row, int width, long line) =>
        row.Count == width
            ? row
            : throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"{source}: в строке {line} полей {row.Count}, а в заголовке {width}",
                "проверьте разделитель и кавычки; разделитель задается аргументом sep");

    private static ScriptValue[] Typed(List<string> row, bool[] numeric)
    {
        var values = new ScriptValue[row.Count];

        for (int j = 0; j < row.Count; j++)
        {
            string cell = row[j];

            values[j] = cell.Length == 0 ? ScriptValue.None
                : numeric[j] && Csv.TryParseNumber(cell, out double number) ? ScriptValue.Num(number)
                : ScriptValue.Str(cell);
        }

        return values;
    }

    /// <summary>Поля одной строки заголовка; кавычки внутри заголовка разбираются так же, как в записи.</summary>
    private static List<string> Parse(string line, char delimiter)
    {
        using var single = new StringReader(line);

        return new CsvStream(single, "заголовок").Next(delimiter, CancellationToken.None) ?? [];
    }
}

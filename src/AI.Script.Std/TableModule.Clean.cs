using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Globalization;

namespace AI.Script.Std;

/// <summary>
/// Чистка выгрузки: числа, деньги, даты и пропуски, записанные для человека.
/// </summary>
/// <remarks>
/// Выгрузка из учётной системы приходит текстом: «1 234,50 ₽», «12 %», «(500)», «15.03.26»,
/// «н/д». Разбор типов при чтении CSV такую колонку оставляет строковой, и дальше по конвейеру
/// она отказывает в первом же счёте. Чистка переводит её в числа один раз и на входе.
/// <para>
/// Колонка определяется большинством: если разбирается меньше
/// <see cref="CleanShare"/> непустых значений, она остаётся текстовой целиком. Иначе одна
/// строка примечания превращала бы всю колонку в дырявые числа.
/// </para>
/// <para>
/// Что прочитать не удалось, становится пропуском, а не нулём, и попадает в
/// <c>table.issues</c>: молча подставленный ноль смещает любое среднее ниже по конвейеру, и
/// заметить это нечем.
/// </para>
/// </remarks>
public static partial class TableModule
{
    /// <summary>Доля непустых значений, при которой колонка считается числовой либо датой.</summary>
    public const double CleanShare = 0.8;

    /// <summary>Пометки пропуска, которые встречаются в выгрузках.</summary>
    private static readonly HashSet<string> s_missingMarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "-", "--", "—", "–", "н/д", "нд", "нет данных", "нет", "n/a", "na", "null", "nan", "#н/д", "#N/A",
    };

    /// <summary>Форматы дат, которые пишут люди и выгружают учётные системы.</summary>
    private static readonly string[] s_dateFormats =
    [
        "dd.MM.yyyy", "dd.MM.yy", "d.M.yyyy", "d.M.yy",
        "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy",
        "dd.MM.yyyy HH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss",
    ];

    [ScriptFn("clean", "Читает выгрузку человеческой записи: «1 234,50 ₽», «12 %», «15.03.26», «н/д»",
        Example = "io.load(\"продажи.csv\") |> table.clean(locale: \"ru\")")]
    public static ScriptTable Clean(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("локаль записи: \"ru\" либо \"en\"")] string locale = "ru",
        [ScriptParam("колонки, которые считать деньгами")] string[]? money = null)
    {
        var columns = new List<ScriptColumn>(t.ColumnCount);

        foreach (ScriptColumn column in t.Columns) columns.Add(Analyse(column, locale, money).Cleaned);

        return ScriptTable.Create(columns);
    }

    [ScriptFn("issues", "Ячейки выгрузки, которые не прочитались числом либо датой",
        Example = "emit issues = table.issues(t)")]
    public static ScriptTable Issues(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("локаль записи: \"ru\" либо \"en\"")] string locale = "ru",
        [ScriptParam("колонки, которые считать деньгами")] string[]? money = null)
    {
        var names = new List<ScriptValue>();
        var rows = new List<ScriptValue>();
        var values = new List<ScriptValue>();
        var expected = new List<ScriptValue>();

        foreach (ScriptColumn column in t.Columns)
        {
            Analysis analysis = Analyse(column, locale, money);

            foreach ((int row, string text) in analysis.Issues)
            {
                names.Add(ScriptValue.Str(column.Name));
                rows.Add(ScriptValue.Num(row + 1));
                values.Add(ScriptValue.Str(text));
                expected.Add(ScriptValue.Str(analysis.Expected));
            }
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("column", [.. names]),
            ScriptColumn.Own("row", [.. rows]),
            ScriptColumn.Own("value", [.. values]),
            ScriptColumn.Own("expected", [.. expected]),
        ]);
    }

    /// <summary>Число из значения колонки; точное переводится в двоичное.</summary>
    /// <remarks>
    /// Текст и дата числом не становятся: их внутреннее число равно нулю, и сумма по текстовой
    /// колонке молча давала бы ноль вместо отказа.
    /// </remarks>
    internal static double Numeric(ScriptValue value) => value.Type switch
    {
        ScriptType.Dec => (double)value.AsDecimal(),
        ScriptType.Num or ScriptType.Bool => value.RawNumber,
        _ => throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"ожидалось число, а в ячейке {value.Type.ToName()}: {ScriptFormatter.Format(value)}",
            "текстовую выгрузку сперва приведите: table.clean(t)"),
    };

    /// <summary>
    /// Разбирает колонку и решает, чем она является.
    /// </summary>
    /// <remarks>
    /// Один проход на чистку и на отчёт о нечитаемых ячейках: два прохода с разными правилами
    /// разошлись бы, и отчёт перестал бы описывать то, что получилось.
    /// </remarks>
    private static Analysis Analyse(ScriptColumn column, string locale, string[]? money)
    {
        if (column.Type is ScriptType.Num or ScriptType.Dec or ScriptType.Bool or ScriptType.Date)
            return new Analysis(column, "число", []);

        int present = 0;
        int numbers = 0;
        int dates = 0;
        bool currency = money != null && Array.IndexOf(money, column.Name) >= 0;
        bool percent = false;

        for (int i = 0; i < column.Count; i++)
        {
            // Смешанная колонка: уже числа и даты считаются сами собой, а не пропусками.
            if (column[i].Type is ScriptType.Num or ScriptType.Dec)
            {
                present++;
                numbers++;
                continue;
            }

            if (column[i].Type == ScriptType.Date)
            {
                present++;
                dates++;
                continue;
            }

            string? text = Text(column[i]);

            if (text == null) continue;

            present++;

            if (DecimalText.TryParse(text, locale, out _, out bool asPercent))
            {
                numbers++;
                percent |= asPercent;
                currency |= HasCurrency(text);
                continue;
            }

            if (TryParseDate(text, out _)) dates++;
        }

        if (present == 0) return new Analysis(column, "число", []);

        bool numeric = (double)numbers / present >= CleanShare;
        bool temporal = !numeric && (double)dates / present >= CleanShare;

        if (!numeric && !temporal) return new Analysis(Trimmed(column), "текст", []);

        var values = new ScriptValue[column.Count];
        var issues = new List<(int Row, string Text)>();
        bool exact = numeric && currency && !percent;

        for (int i = 0; i < column.Count; i++)
        {
            ScriptValue cell = column[i];

            if (numeric && cell.Type is ScriptType.Num or ScriptType.Dec)
            {
                values[i] = exact && cell.Type == ScriptType.Num ? ScriptValue.Dec((decimal)cell.RawNumber) : cell;
                continue;
            }

            if (temporal && cell.Type == ScriptType.Date)
            {
                values[i] = cell;
                continue;
            }

            string? text = Text(cell);

            if (text == null)
            {
                values[i] = cell.Type is ScriptType.Str || cell.IsNone ? ScriptValue.None : cell;

                if (!cell.IsNone && cell.Type != ScriptType.Str) issues.Add((i, ScriptFormatter.Format(cell)));

                continue;
            }

            if (numeric && DecimalText.TryParse(text, locale, out decimal number, out _))
            {
                values[i] = exact ? ScriptValue.Dec(number) : ScriptValue.Num((double)number);
                continue;
            }

            if (temporal && TryParseDate(text, out DateTime moment))
            {
                values[i] = ScriptValue.Date(moment);
                continue;
            }

            values[i] = ScriptValue.None;
            issues.Add((i, text));
        }

        return new Analysis(ScriptColumn.Own(column.Name, values), temporal ? "дата" : "число", issues);
    }

    /// <summary>Текст ячейки; <c>null</c> — пропуск либо уже не текст.</summary>
    private static string? Text(ScriptValue value)
    {
        if (value.IsNone || value.Type != ScriptType.Str) return null;

        string text = value.AsString().Trim();

        return text.Length == 0 || s_missingMarks.Contains(text) ? null : text;
    }

    private static ScriptColumn Trimmed(ScriptColumn column)
    {
        var values = new ScriptValue[column.Count];

        for (int i = 0; i < column.Count; i++)
        {
            string? text = Text(column[i]);

            values[i] = text == null
                ? column[i].Type == ScriptType.Str || column[i].IsNone ? ScriptValue.None : column[i]
                : ScriptValue.Str(text);
        }

        return ScriptColumn.Own(column.Name, values);
    }

    private static bool HasCurrency(string text)
    {
        foreach (char c in text)
        {
            if ("₽$€£¥₴₸".Contains(c, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool TryParseDate(string text, out DateTime value) =>
        DateTime.TryParseExact(text, s_dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
        || DateTime.TryParse(text, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out value);

    /// <summary>Разобранная колонка: что получилось, чего ждали и что не прочиталось.</summary>
    private readonly record struct Analysis(
        ScriptColumn Cleaned,
        string Expected,
        IReadOnlyList<(int Row, string Text)> Issues);
}

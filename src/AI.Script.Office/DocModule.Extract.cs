using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;
using System.Text.RegularExpressions;

namespace AI.Script.Office;

/// <summary>
/// Извлечение реквизитов документа в таблицу по схеме.
/// </summary>
/// <remarks>
/// «Вытащи из договоров сроки и суммы» — это работа над КОРПУСОМ: десять документов дают
/// таблицу, по которой дальше считают, сортируют и сверяют. Пока такой таблицы нет, вместо неё
/// пересказ модели, а пересказ проверяется только другим пересказом.
/// <para>
/// У каждой строки есть АДРЕС источника — раздел, страница и номер блока. Извлечённое число без
/// адреса нечем проверить: человек не может открыть документ и посмотреть, откуда оно взялось,
/// а значит, обязан верить на слово.
/// </para>
/// <para>
/// Здесь только правила: подпись поля рядом со значением нужного вида и явное регулярное
/// выражение. Схемы под конкретные виды документов (что важно в договоре, что в акте) — знание
/// продукта, а не языка, и живут у того, кто эти документы разбирает. Извлечение моделью — это
/// <c>llm.extract</c>: обращение к сети остаётся видимым в скрипте, а не прячется внутри
/// функции чтения документа.
/// </para>
/// </remarks>
public static partial class DocModule
{
    /// <summary>
    /// Префикс правила-выражения: «re:» и дальше сам шаблон.
    /// </summary>
    /// <remarks>
    /// Шаблон с обратными косыми пишут многострочной строкой (<c>"""\d+"""</c>): в обычной
    /// строке <c>\d</c> — неизвестное экранирование, и косая из шаблона пропадёт.
    /// </remarks>
    private const string RegexPrefix = "re:";

    /// <summary>Сколько знаков текста блока попадает в отчёт: строка-источник, а не весь абзац.</summary>
    private const int SourceLimit = 200;

    [ScriptFn("extract", "Достаёт реквизиты по схеме: поле, значение и адрес в документе",
        Example = "doc.extract(d, schema: { срок: \"date\", сумма: \"dec\" })",
        Columns = "field,value,section,page,block,address,source")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptTable Extract(
        IScriptContext context,
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("запись «поле → вид значения»: date, dec, num, str либо \"re:шаблон\"")] ScriptRecord schema,
        [ScriptParam("слова-подписи для полей: поле → список синонимов")] ScriptRecord? labels = null)
    {
        IReadOnlyList<int> sections = d.Sections();

        var fields = new List<ScriptValue>();
        var values = new List<ScriptValue>();
        var inSections = new List<ScriptValue>();
        var pages = new List<ScriptValue>();
        var blocks = new List<ScriptValue>();
        var addresses = new List<ScriptValue>();
        var sources = new List<ScriptValue>();

        foreach (var field in schema.Pairs())
        {
            string rule = field.Value.AsString($"doc.extract: вид поля «{field.Key}»");
            IReadOnlyList<string> words = Labels(field.Key, labels);

            for (int i = 0; i < d.Blocks.Count; i++)
            {
                foreach (ScriptValue found in Values(d.Blocks[i], rule, words))
                {
                    fields.Add(ScriptValue.Str(field.Key));
                    values.Add(found);
                    inSections.Add(ScriptValue.Num(sections[i]));
                    pages.Add(ScriptValue.Num(d.Blocks[i].Page));
                    blocks.Add(ScriptValue.Num(i));
                    addresses.Add(ScriptValue.Str(d.Blocks[i].Address ?? string.Empty));
                    sources.Add(ScriptValue.Str(Shorten(d.Blocks[i].Text)));
                }
            }
        }

        context.CountAllocation(fields.Count);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("field", [.. fields]),
            ScriptColumn.Own("value", [.. values]),
            ScriptColumn.Own("section", [.. inSections]),
            ScriptColumn.Own("page", [.. pages]),
            ScriptColumn.Own("block", [.. blocks]),
            ScriptColumn.Own("address", [.. addresses]),
            ScriptColumn.Own("source", [.. sources]),
        ]);
    }

    /// <summary>Подписи поля: имя самого поля и синонимы, если их дали.</summary>
    private static IReadOnlyList<string> Labels(string field, ScriptRecord? labels)
    {
        var words = new List<string> { field };

        if (labels is null || !labels.TryGet(field, out ScriptValue given)) return words;

        if (given.Type == ScriptType.Str)
        {
            words.Add(given.AsString());
            return words;
        }

        ScriptList list = given.AsList($"doc.extract: синонимы поля «{field}»");

        for (int i = 0; i < list.Count; i++) words.Add(list[i].AsString($"doc.extract: синоним поля «{field}»"));

        return words;
    }

    /// <summary>
    /// Значения нужного вида в одном блоке.
    /// </summary>
    /// <remarks>
    /// Значение берётся ТОЛЬКО из блока, где стоит подпись поля: число само по себе ничего не
    /// значит, а «сумма договора» рядом с ним — значит. Исключение одно — явное регулярное
    /// выражение: его автор уже сказал, что именно ищет.
    /// </remarks>
    private static IEnumerable<ScriptValue> Values(DocBlock block, string rule, IReadOnlyList<string> labels)
    {
        if (rule.StartsWith(RegexPrefix, StringComparison.Ordinal))
        {
            foreach (ScriptValue value in Matches(block.Text, rule[RegexPrefix.Length..])) yield return value;

            yield break;
        }

        // Таблица документа со столбцом-подписью: значения берутся из её ячеек, а не из
        // текстовой свёртки, — иначе «Аванс | 500 000 ₽» разбирался бы как одна строка текста.
        if (block.Table is { } grid)
        {
            foreach (ScriptValue value in FromTable(grid, rule, labels)) yield return value;

            yield break;
        }

        int at = LabelAt(block.Text, labels);

        if (at < 0) yield break;

        string tail = block.Text[at..];

        foreach (ScriptValue value in Scan(tail, rule)) yield return value;
    }

    /// <summary>
    /// Значения из таблицы: по колонке с подписью в шапке либо по строке с подписью в первой ячейке.
    /// </summary>
    /// <remarks>
    /// Реквизиты договора чаще всего сводят в таблицу из двух колонок «Поле | Значение», и подпись
    /// там стоит в первой ячейке строки, а не в шапке.
    /// </remarks>
    private static IEnumerable<ScriptValue> FromTable(ScriptTable grid, string rule, IReadOnlyList<string> labels)
    {
        for (int j = 0; j < grid.ColumnCount; j++)
        {
            if (LabelAt(grid[j].Name, labels) < 0) continue;

            for (int i = 0; i < grid.RowCount; i++)
            {
                foreach (ScriptValue value in Scan(Text(grid[j][i]), rule)) yield return value;
            }
        }

        if (grid.ColumnCount < 2) yield break;

        for (int i = 0; i < grid.RowCount; i++)
        {
            if (LabelAt(Text(grid[0][i]), labels) < 0) continue;

            for (int j = 1; j < grid.ColumnCount; j++)
            {
                foreach (ScriptValue value in Scan(Text(grid[j][i]), rule)) yield return value;
            }
        }
    }

    private static string Text(ScriptValue value) =>
        value.IsNone ? string.Empty : value.Type == ScriptType.Str ? value.AsString() : ScriptFormatter.Format(value);

    /// <summary>Позиция подписи поля в тексте; −1 — подписи нет.</summary>
    /// <remarks>
    /// Сравнение по началу слова, а не по вхождению подстроки: «срок» иначе находился бы внутри
    /// «сроков» — это как раз нужно, — но и внутри «просрочка», а это уже другое поле.
    /// </remarks>
    private static int LabelAt(string text, IReadOnlyList<string> labels)
    {
        foreach (string label in labels)
        {
            int at = 0;

            while (at < text.Length)
            {
                int found = text.IndexOf(label, at, StringComparison.OrdinalIgnoreCase);

                if (found < 0) break;

                if (found == 0 || !char.IsLetter(text[found - 1])) return found;

                at = found + 1;
            }
        }

        return -1;
    }

    /// <summary>Значения заданного вида в куске текста.</summary>
    private static IEnumerable<ScriptValue> Scan(string text, string rule)
    {
        switch (rule)
        {
            case "date":
                foreach (Match match in DatePattern().Matches(text))
                {
                    if (DocDates.TryParse(match.Value, out DateTime moment)) yield return ScriptValue.Date(moment);
                }

                break;

            case "dec":
            case "num":
                foreach (Match match in NumberPattern().Matches(text))
                {
                    if (!DecimalText.TryParse(match.Value, "ru", out decimal number, out _)) continue;

                    yield return rule == "dec" ? ScriptValue.Dec(number) : ScriptValue.Num((double)number);
                }

                break;

            case "str":
                string value = text.Trim();

                if (value.Length > 0) yield return ScriptValue.Str(Shorten(value));

                break;

            default:
                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"doc.extract: неизвестный вид значения «{rule}»",
                    "известны: date, dec, num, str и \"re:шаблон\" с одной скобочной группой");
        }
    }

    private static IEnumerable<ScriptValue> Matches(string text, string pattern)
    {
        Regex regex;

        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException exception)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"doc.extract: выражение «{pattern}» не разбирается — {exception.Message}");
        }

        foreach (Match match in regex.Matches(text))
        {
            // Группа, если она есть: в «№ (\S+)» автора интересует номер, а не слово «№».
            yield return ScriptValue.Str(match.Groups.Count > 1 ? match.Groups[1].Value : match.Value);
        }
    }

    private static string Shorten(string text) =>
        text.Length <= SourceLimit ? text : text[..SourceLimit] + "…";

    [GeneratedRegex(@"\b\d{1,2}[.\-/]\d{1,2}[.\-/]\d{2,4}\b|\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}\s+[А-Яа-яЁё]{3,8}\s+\d{4}\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"[-+]?\(?\d[\d  ]*(?:[.,]\d+)?\)?\s*(?:₽|руб\.?|р\.|%)?")]
    private static partial Regex NumberPattern();
}

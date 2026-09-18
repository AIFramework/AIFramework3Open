using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Office;

/// <summary>
/// Точечные правки документа: замена текста, вставка блока, заполнение шаблона.
/// </summary>
/// <remarks>
/// Опыт с редактурой — это одна и та же правка на корпусе документов и проверка через
/// <c>doc.diff</c>, что изменилось ровно заказанное. Поэтому правка не пересобирает документ, а
/// меняет его на месте: у Word — сам файл с его оформлением, у разметки — текст блоков.
/// </remarks>
public static partial class DocModule
{
    /// <summary>Как в шаблоне отмечено поле: <c>{{имя}}</c>.</summary>
    private const string FieldOpen = "{{";

    /// <inheritdoc cref="FieldOpen"/>
    private const string FieldClose = "}}";

    [ScriptFn("replace", "Заменяет текст в документе, сохраняя оформление",
        Example = "d |> doc.replace(from: \"Исполнитель\", to: \"Подрядчик\")", Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptDocument Replace(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("что заменить")] string from,
        [ScriptParam("чем заменить")] string to)
    {
        if (string.IsNullOrEmpty(from))
            throw new ScriptError(DiagnosticCodes.BadOperand, "doc.replace: заменять пустую строку нечего");

        if (d.Source is { } source) return Reread(d, DocxEditor.Replace(source, from, to).Bytes);

        return new ScriptDocument(d.Name, d.Format, [.. d.Blocks.Select(block => Replaced(block, from, to))], d.PageCount);
    }

    /// <summary>
    /// Вставляет блок после блока с номером из <c>doc.blocks</c>.
    /// </summary>
    /// <remarks>
    /// Адрес — номер блока, а не текст-якорь: одна и та же фраза встречается в договоре не раз, а
    /// номер указывает ровно на одно место и совпадает с тем, что показал <c>doc.blocks</c>.
    /// </remarks>
    [ScriptFn("insert", "Вставляет абзац либо заголовок после блока с номером",
        Example = "d |> doc.insert(after: 4, text: \"Новый пункт.\")", Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptDocument Insert(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("номер блока из doc.blocks; −1 — в начало")] int after,
        [ScriptParam("текст")] string text,
        [ScriptParam("вид: \"paragraph\" либо \"heading\"")] string kind = "paragraph",
        [ScriptParam("уровень заголовка")] int level = 2)
    {
        if (after < -1 || after >= d.Blocks.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"doc.insert: блока {after} нет, в документе их {d.Blocks.Count}",
                "номера блоков показывает doc.blocks(d); −1 — вставить в начало");
        }

        var block = kind switch
        {
            "paragraph" => new DocBlock(DocBlockKind.Paragraph, text),
            "heading" => new DocBlock(DocBlockKind.Heading, text, Math.Clamp(level, 1, 9)),
            _ => throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"doc.insert: неизвестный вид блока «{kind}»",
                "известны: \"paragraph\" и \"heading\""),
        };

        if (d.Source is { } source) return Reread(d, DocxEditor.Insert(source, after, block));

        var blocks = d.Blocks.ToList();

        blocks.Insert(after + 1, block);

        return new ScriptDocument(d.Name, d.Format, blocks, d.PageCount);
    }

    /// <summary>
    /// Заполняет поля шаблона <c>{{имя}}</c> значениями записи.
    /// </summary>
    /// <remarks>
    /// Поле, для которого значения нет, — отказ, а не пустое место: договор с незаполненной суммой
    /// выглядит готовым, и заметить пропуск по нему уже нечем.
    /// </remarks>
    [ScriptFn("fill", "Заполняет поля шаблона {{имя}} значениями записи",
        Example = "шаблон |> doc.fill(data: { клиент: \"ООО Альфа\", сумма: итог })", Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptDocument Fill(
        [ScriptParam("шаблон")] ScriptDocument d,
        [ScriptParam("значения полей")] ScriptRecord data)
    {
        ScriptDocument filled = d;

        foreach (var field in data.Pairs())
        {
            string value = field.Value.Type == ScriptType.Str ? field.Value.AsString() : ScriptFormatter.Format(field.Value);

            filled = Replace(filled, FieldOpen + field.Key + FieldClose, value);
        }

        if (Unfilled(filled.Text) is { Count: > 0 } left)
        {
            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"doc.fill: в шаблоне остались незаполненные поля: {string.Join(", ", left)}",
                "передайте значения для них в data");
        }

        return filled;
    }

    /// <summary>
    /// Блок с заменой в тексте и в строковых ячейках таблицы.
    /// </summary>
    /// <remarks>
    /// Ячейки отдельно от текста: у прочитанной таблицы текст это только ее свертка, и замена в нем
    /// одном давала бы документ, где проверка видит заполненное поле, а файл хранит прежнее.
    /// </remarks>
    private static DocBlock Replaced(DocBlock block, string from, string to) => block with
    {
        Text = block.Text.Replace(from, to, StringComparison.Ordinal),
        Table = block.Table is { } table ? ReplacedCells(table, from, to) : null,
    };

    private static ScriptTable ReplacedCells(ScriptTable table, string from, string to) =>
        ScriptTable.Create([.. table.Columns.Select(column => ScriptColumn.Own(
            column.Name.Replace(from, to, StringComparison.Ordinal),
            [.. Enumerable.Range(0, column.Count).Select(i => column[i].Type == ScriptType.Str
                ? ScriptValue.Str(column[i].AsString().Replace(from, to, StringComparison.Ordinal))
                : column[i])]))]);

    /// <summary>Имена полей <c>{{...}}</c>, оставшихся в тексте.</summary>
    private static IReadOnlyList<string> Unfilled(string text)
    {
        var names = new List<string>();
        int at = 0;

        while ((at = text.IndexOf(FieldOpen, at, StringComparison.Ordinal)) >= 0)
        {
            int end = text.IndexOf(FieldClose, at, StringComparison.Ordinal);

            if (end < 0) break;

            string name = text[(at + FieldOpen.Length)..end];

            if (!names.Contains(name, StringComparer.Ordinal)) names.Add(name);

            at = end + FieldClose.Length;
        }

        return names;
    }
}

using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text;

namespace AI.Script.Office;

/// <summary>
/// Сборка документа по звеньям и сохранение.
/// </summary>
/// <remarks>
/// Итог опыта — документ для человека: заголовок, таблица итогов, график, вывод. Звенья
/// приставляются конвейером (<c>doc.new() |> doc.table(t) |> doc.figure(g)</c>), каждое
/// возвращает новый документ: значения языка неизменяемы, и промежуточный документ можно
/// сохранить или продолжить по-разному.
/// <para>
/// К документу, прочитанному из Word, звенья дописываются в сам файл: его оформление остаётся
/// на месте, а новые абзацы и таблицы получают стили Word, а не прямое оформление.
/// </para>
/// </remarks>
public static partial class DocModule
{
    /// <summary>Размер картинки графика по умолчанию, в пикселях.</summary>
    public const int FigureWidth = 1200;

    /// <inheritdoc cref="FigureWidth"/>
    public const int FigureHeight = 700;

    [ScriptFn("new", "Новый документ: дальше звенья heading, paragraph, table, figure",
        Example = "doc.new(title: \"Отчёт об опыте\")", Returns = ScriptDocument.TypeName)]
    public static ScriptDocument New([ScriptParam("заголовок документа; пусто — без заголовка")] string title = "")
    {
        IReadOnlyList<DocBlock> blocks = string.IsNullOrWhiteSpace(title)
            ? []
            : [new DocBlock(DocBlockKind.Heading, title.Trim(), 1)];

        return new ScriptDocument("документ", "new", blocks);
    }

    [ScriptFn("heading", "Добавляет заголовок раздела", Example = "d |> doc.heading(\"Итоги\", level: 2)",
        Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptDocument Heading(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("текст заголовка")] string text,
        [ScriptParam("уровень: 1 — самый крупный")] int level = 1)
        => Add(d, new DocBlock(DocBlockKind.Heading, text, Math.Clamp(level, 1, 9)));

    [ScriptFn("paragraph", "Добавляет абзац текста", Example = "d |> doc.paragraph(\"Разница не доказана.\")",
        Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptDocument AddParagraph(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("текст абзаца")] string text)
        => Add(d, new DocBlock(DocBlockKind.Paragraph, text));

    [ScriptFn("table", "Добавляет таблицу с шапкой из имён колонок", Example = "d |> doc.table(итоги, title: \"Итоги\")",
        Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptDocument AddTable(
        IScriptContext context,
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("подпись над таблицей")] string title = "")
    {
        context.CountAllocation((long)t.RowCount * Math.Max(1, t.ColumnCount));

        return Add(d, new DocBlock(DocBlockKind.Table, title, Table: t, Caption: title.Length > 0 ? title : null));
    }

    /// <summary>
    /// Добавляет рисунок: график языка либо картинку из файла.
    /// </summary>
    /// <remarks>
    /// График рисуется здесь же картинкой: Word описание Plotly не исполняет. Что именно умеет
    /// нарисовать себя картинкой, решает сам график (<see cref="IScriptImageSource"/>), и
    /// документам не нужно знать про графики.
    /// </remarks>
    [ScriptFn("figure", "Добавляет рисунок: график либо картинку png из файла",
        Example = "d |> doc.figure(plot.line(ряд), caption: \"Рис. 1\")", Returns = ScriptDocument.TypeName)]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static async Task<ScriptDocument> Figure(
        IScriptContext context,
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("график либо путь к картинке png")] ScriptValue image,
        [ScriptParam("подпись под рисунком")] string caption = "",
        [ScriptParam("ширина в пикселях")] int width = FigureWidth,
        [ScriptParam("высота в пикселях")] int height = FigureHeight)
    {
        byte[] png = await Png(context, image, Math.Clamp(width, 100, 4000), Math.Clamp(height, 100, 4000)).ConfigureAwait(false);

        return Add(d, new DocBlock(DocBlockKind.Image, caption, Image: png));
    }

    /// <summary>
    /// Сохраняет документ файлом: Word либо разметка.
    /// </summary>
    /// <remarks>
    /// Документ, прочитанный из Word и поправленный, сохраняется своим файлом — со всеми
    /// стилями. Собранный с нуля — собирается писателем. В разметке рисунки ложатся рядом
    /// отдельными файлами: вложить картинку в текст разметки нельзя.
    /// </remarks>
    [ScriptFn("save", "Сохраняет документ файлом: docx либо md", Example = "d |> doc.save(\"отчёт.docx\")",
        Writes = "docx,md")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static async Task<string> Save(
        IScriptContext context,
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        string extension = ScriptFileKinds.Extension(path);

        byte[] bytes = extension switch
        {
            "docx" => d.Source ?? DocxWriter.Write(d),
            "md" => Encoding.UTF8.GetBytes(await MarkdownAsync(context, d, path).ConfigureAwait(false)),
            _ => throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"doc.save: документ не пишется в .{extension}",
                "записываются docx и md; pdf собирается из docx программой, которая его откроет"),
        };

        await context.Sandbox.WriteAsync(path, bytes, context.Cancellation).ConfigureAwait(false);

        context.FileSaved(new ScriptFileInfo(path, bytes.LongLength, DateTimeOffset.UtcNow));

        return path;
    }

    /// <summary>Добавляет блок: в файл Word, если документ из него, иначе в список блоков.</summary>
    private static ScriptDocument Add(ScriptDocument d, DocBlock block) =>
        d.Source is { } source
            ? Reread(d, DocxEditor.Append(source, block))
            : new ScriptDocument(d.Name, d.Format, [.. d.Blocks, block], d.PageCount);

    /// <summary>Документ заново из поправленного файла Word: блоки обязаны описывать сам файл.</summary>
    private static ScriptDocument Reread(ScriptDocument d, byte[] bytes) =>
        DocumentReader.Read(bytes, d.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? d.Name : d.Name + ".docx");

    private static async Task<byte[]> Png(IScriptContext context, ScriptValue image, int width, int height)
    {
        if (image.Type == ScriptType.Handle && image.AsHandle().Target is IScriptImageSource source)
            return source.RenderPng(width, height);

        if (image.Type == ScriptType.Str)
        {
            string path = image.AsString();
            byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

            if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return bytes;

            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"doc.figure: '{path}' — не картинка PNG",
                "сохраните картинку в png: cv.save(изображение, \"рисунок.png\")");
        }

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"doc.figure: ожидался график либо путь к картинке, получено {image.Type.ToName()}",
            "рисунок делают plot.line, plot.bar и подобные либо файл png");
    }

    private static async Task<string> MarkdownAsync(IScriptContext context, ScriptDocument d, string path)
    {
        var text = new StringBuilder();
        string stem = Path.GetFileNameWithoutExtension(path);
        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        int figures = 0;

        foreach (DocBlock block in d.Blocks)
        {
            if (text.Length > 0) _ = text.Append('\n');

            switch (block.Kind)
            {
                case DocBlockKind.Heading:
                    _ = text.Append(new string('#', Math.Clamp(block.Level, 1, 6))).Append(' ').Append(block.Text).Append('\n');
                    break;

                case DocBlockKind.ListItem:
                    _ = text.Append("- ").Append(block.Text).Append('\n');
                    break;

                case DocBlockKind.Table when block.Table is { } table:
                    if (block.Caption is { Length: > 0 } caption) _ = text.Append(caption).Append("\n\n");

                    _ = text.Append(Pipe(table));
                    break;

                case DocBlockKind.Image when block.Image is { } png:
                    string name = $"{stem}-{++figures}.png";
                    string file = folder.Length == 0 ? name : $"{folder}/{name}";

                    await context.Sandbox.WriteAsync(file, png, context.Cancellation).ConfigureAwait(false);
                    context.FileSaved(new ScriptFileInfo(file, png.LongLength, DateTimeOffset.UtcNow));

                    _ = text.Append("![").Append(block.Text).Append("](").Append(name).Append(")\n");
                    break;

                default:
                    _ = text.Append(block.Text).Append('\n');
                    break;
            }
        }

        return text.ToString();
    }

    private static string Pipe(ScriptTable table)
    {
        var text = new StringBuilder();

        _ = text.Append("| ").AppendJoin(" | ", table.Columns.Select(c => c.Name)).Append(" |\n");
        _ = text.Append('|').Append(string.Concat(table.Columns.Select(_ => "---|"))).Append('\n');

        for (int i = 0; i < table.RowCount; i++)
        {
            _ = text.Append("| ")
                .AppendJoin(" | ", table.Columns.Select(c => c[i].IsNone ? string.Empty : ScriptFormatter.Format(c[i])))
                .Append(" |\n");
        }

        return text.ToString();
    }
}

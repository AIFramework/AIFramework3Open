using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AI.Script.Office;

/// <summary>
/// Чтение документа из байтов: Word, PDF, разметка, HTML, обычный текст.
/// </summary>
/// <remarks>
/// Каждый формат знает о себе разное, и дерево блоков получается разной полноты: у Word есть
/// уровни заголовков и настоящие таблицы, у PDF — страницы, но заголовки в нём опознаются
/// только по виду строки. Достраивать недостающее догадками нельзя: скрипт обопрётся на
/// структуру, которой в файле нет.
/// </remarks>
public static partial class DocumentReader
{
    /// <summary>Читает документ, выбирая читателя по расширению.</summary>
    public static ScriptDocument Read(byte[] bytes, string path)
    {
        string format = ScriptFileKinds.Extension(path);

        return format switch
        {
            "docx" => Word(bytes, path),
            "pdf" => Pdf(bytes, path),
            "md" or "markdown" => Markdown(Text(bytes), path, "md"),
            "html" or "htm" => Html(Text(bytes), path),
            "txt" or "log" => Plain(Text(bytes), path),
            _ => throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"doc.load: формат .{format} не читается как документ",
                "читаются: docx, pdf, md, html, txt"),
        };
    }

    private static ScriptDocument Word(byte[] bytes, string path)
    {
        using var stream = new MemoryStream(bytes, writable: false);

        try
        {
            using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: false);

            Body? body = document.MainDocumentPart?.Document?.Body;

            if (body == null) return new ScriptDocument(path, "docx", []);

            var blocks = new List<DocBlock>();
            IReadOnlyDictionary<string, int> headings = HeadingStyles(document.MainDocumentPart?.StyleDefinitionsPart?.Styles);

            foreach (OpenXmlElement element in WordBlocks(body))
            {
                blocks.Add(element is Table table
                    ? new DocBlock(DocBlockKind.Table, TableText(table), Table: Read(table))
                    : Paragraph((Paragraph)element, headings)!);
            }

            return new ScriptDocument(path, "docx", blocks, source: bytes);
        }
        catch (OpenXmlPackageException exception)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"doc.load: '{path}' не открывается как документ Word — {exception.Message}",
                "старый формат .doc не читается: пересохраните в .docx");
        }
        catch (FileFormatException exception)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"doc.load: '{path}' не похож на документ Word — {exception.Message}");
        }
    }

    /// <summary>
    /// Элементы тела документа, которые становятся блоками, — в порядке блоков.
    /// </summary>
    /// <remarks>
    /// Одно правило на чтение и на правку: номер блока, который скрипт видел в
    /// <c>doc.blocks</c>, обязан указывать на тот же элемент, в который правка вставляет текст.
    /// Пустые абзацы блоками не считаются ни там, ни там.
    /// </remarks>
    internal static IReadOnlyList<OpenXmlElement> WordBlocks(Body body)
    {
        var elements = new List<OpenXmlElement>();

        foreach (OpenXmlElement element in body.ChildElements)
        {
            if (element is Table || (element is Paragraph paragraph && Paragraph(paragraph) != null))
                elements.Add(element);
        }

        return elements;
    }

    private static DocBlock? Paragraph(Paragraph paragraph, IReadOnlyDictionary<string, int>? headings = null)
    {
        string text = paragraph.InnerText.Trim();
        bool image = paragraph.Descendants<Drawing>().Any();

        if (image) return new DocBlock(DocBlockKind.Image, text);
        if (text.Length == 0) return null;

        string? style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        int level = style is not null && headings is not null && headings.TryGetValue(style, out int known)
            ? known
            : HeadingLevel(style);

        if (level == 0 && paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value is int outline && outline < 9)
            level = outline + 1;

        if (level > 0) return new DocBlock(DocBlockKind.Heading, text, level);

        return paragraph.ParagraphProperties?.NumberingProperties != null
            ? new DocBlock(DocBlockKind.ListItem, text)
            : new DocBlock(DocBlockKind.Paragraph, text);
    }

    /// <summary>
    /// Уровни заголовков по стилям документа.
    /// </summary>
    /// <remarks>
    /// Русский Word хранит стиль заголовка под номером («1», «2»), а смысл лежит в имени стиля
    /// («heading 1») и в уровне структуры. По одному номеру заголовок не опознать: «1» бывает и
    /// обычным стилем.
    /// </remarks>
    private static IReadOnlyDictionary<string, int> HeadingStyles(Styles? styles)
    {
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);

        if (styles is null) return levels;

        foreach (Style style in styles.Elements<Style>())
        {
            if (style.StyleId?.Value is not { } id) continue;

            int level = HeadingLevel(style.StyleName?.Val?.Value?.Replace(" ", string.Empty));

            if (level == 0 && style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is int outline && outline < 9)
                level = outline + 1;

            if (level > 0) levels[id] = level;
        }

        return levels;
    }

    /// <summary>
    /// Уровень заголовка по имени стиля.
    /// </summary>
    /// <remarks>
    /// Стиль называется и «Heading2», и «Заголовок2», и просто «2» — зависит от того, в какой
    /// программе документ сделан. Цифра в конце имени и есть уровень; «Title» — первый.
    /// </remarks>
    private static int HeadingLevel(string? style)
    {
        if (string.IsNullOrEmpty(style)) return 0;

        if (style.StartsWith("Title", StringComparison.OrdinalIgnoreCase)) return 1;

        bool heading = style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("Заголовок", StringComparison.OrdinalIgnoreCase);

        if (!heading) return 0;

        string digits = new([.. style.Where(char.IsDigit)]);

        return digits.Length > 0 && int.TryParse(digits, out int level) ? Math.Clamp(level, 1, 9) : 1;
    }

    private static ScriptTable Read(Table table)
    {
        var rows = new List<List<string>>();

        foreach (TableRow row in table.Elements<TableRow>())
        {
            var cells = new List<string>();

            foreach (TableCell cell in row.Elements<TableCell>()) cells.Add(cell.InnerText.Trim());

            if (cells.Count > 0) rows.Add(cells);
        }

        return Grid(rows);
    }

    private static string TableText(Table table)
    {
        var builder = new StringBuilder();

        foreach (TableRow row in table.Elements<TableRow>())
        {
            var cells = new List<string>();

            foreach (TableCell cell in row.Elements<TableCell>()) cells.Add(cell.InnerText.Trim());

            if (builder.Length > 0) _ = builder.Append('\n');

            _ = builder.Append(string.Join(" | ", cells));
        }

        return builder.ToString();
    }

    private static ScriptDocument Pdf(byte[] bytes, string path)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(bytes);

            var blocks = new List<DocBlock>();
            int pages = 0;

            foreach (Page page in document.GetPages())
            {
                pages++;

                // Текст страницы в порядке чтения и с переводами строк: у page.Text их нет, и страница
                // приходила одним блоком, в котором не найти ни заголовка, ни абзаца.
                foreach (string line in Lines(ContentOrderTextExtractor.GetText(page)))
                {
                    // Уровней заголовков у PDF нет: разметка в нём не хранится. Строка, похожая
                    // на заголовок по виду, помечается первым уровнем, и это честный максимум
                    // того, что формат позволяет узнать.
                    blocks.Add(LooksLikeHeading(line)
                        ? new DocBlock(DocBlockKind.Heading, line, 1, pages)
                        : new DocBlock(DocBlockKind.Paragraph, line, 0, pages));
                }
            }

            return new ScriptDocument(path, "pdf", blocks, pages);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException
            or UglyToad.PdfPig.Core.PdfDocumentFormatException)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"doc.load: '{path}' не открывается как PDF — {exception.Message}");
        }
    }

    private static ScriptDocument Markdown(string text, string path, string format)
    {
        var blocks = new List<DocBlock>();
        var table = new List<List<string>>();
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            if (paragraph.Length == 0) return;

            blocks.Add(new DocBlock(DocBlockKind.Paragraph, paragraph.ToString().Trim()));
            _ = paragraph.Clear();
        }

        void FlushTable()
        {
            if (table.Count == 0) return;

            blocks.Add(new DocBlock(DocBlockKind.Table, string.Join("\n", table.Select(row => string.Join(" | ", row))),
                Table: Grid(table)));

            table.Clear();
        }

        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.TrimEnd();

            if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 1)
            {
                FlushParagraph();

                var cells = line[1..^1].Split('|').Select(cell => cell.Trim()).ToList();

                // Строка-разделитель шапки («|---|---|») в данные не попадает: это разметка.
                if (!cells.All(cell => cell.Length > 0 && cell.All(c => c is '-' or ':'))) table.Add(cells);

                continue;
            }

            FlushTable();

            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            int level = 0;

            while (level < line.Length && line[level] == '#') level++;

            if (level is > 0 and <= 6 && level < line.Length && line[level] == ' ')
            {
                FlushParagraph();
                blocks.Add(new DocBlock(DocBlockKind.Heading, line[(level + 1)..].Trim(), level));
                continue;
            }

            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new DocBlock(DocBlockKind.ListItem, trimmed[2..].Trim()));
                continue;
            }

            if (paragraph.Length > 0) _ = paragraph.Append(' ');

            _ = paragraph.Append(trimmed);
        }

        FlushParagraph();
        FlushTable();

        return new ScriptDocument(path, format, blocks);
    }

    private static ScriptDocument Html(string html, string path)
    {
        string text = Scripts().Replace(html, " ");

        text = Headings().Replace(text, match => $"\n{new string('#', int.Parse(match.Groups[1].Value))} {match.Groups[2].Value}\n");
        text = Items().Replace(text, match => $"\n- {match.Groups[1].Value}\n");
        text = Paragraphs().Replace(text, match => $"\n{match.Groups[1].Value}\n");
        text = Tags().Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);

        ScriptDocument document = Markdown(Squeeze(text), path, "html");

        return document;
    }

    private static ScriptDocument Plain(string text, string path) => Markdown(text, path, "txt");

    /// <summary>Таблица из строк ячеек: первая строка становится шапкой.</summary>
    private static ScriptTable Grid(IReadOnlyList<List<string>> rows)
    {
        if (rows.Count == 0) return ScriptTable.Empty;

        int width = rows.Max(row => row.Count);
        var names = new List<string>(width);

        for (int j = 0; j < width; j++)
        {
            string name = j < rows[0].Count && rows[0][j].Length > 0 ? rows[0][j] : $"c{j}";

            while (names.Contains(name, StringComparer.Ordinal)) name += "_";

            names.Add(name);
        }

        var columns = new List<ScriptColumn>(width);

        for (int j = 0; j < width; j++)
        {
            var values = new ScriptValue[Math.Max(0, rows.Count - 1)];

            for (int i = 1; i < rows.Count; i++)
            {
                string cell = j < rows[i].Count ? rows[i][j] : string.Empty;
                values[i - 1] = cell.Length == 0 ? ScriptValue.None : ScriptValue.Str(cell);
            }

            columns.Add(ScriptColumn.Own(names[j], values));
        }

        return ScriptTable.Create(columns);
    }

    private static IEnumerable<string> Lines(string text)
    {
        foreach (string line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string trimmed = line.Trim();

            if (trimmed.Length > 0) yield return trimmed;
        }
    }

    /// <summary>Похожа ли строка на заголовок: коротка, без точки в конце, часто в верхнем регистре.</summary>
    private static bool LooksLikeHeading(string line)
    {
        if (line.Length is 0 or > 80) return false;
        if (line.EndsWith('.') || line.EndsWith(';')) return false;

        string letters = new([.. line.Where(char.IsLetter)]);

        if (letters.Length == 0) return false;

        bool upper = letters.All(char.IsUpper);
        bool numbered = char.IsDigit(line[0]) && line.Contains('.', StringComparison.Ordinal);

        return upper || numbered;
    }

    private static string Text(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private static string Squeeze(string text)
    {
        var builder = new StringBuilder(text.Length);
        int newlines = 0;

        foreach (char c in text.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            if (c == '\n')
            {
                newlines++;

                if (newlines <= 2) _ = builder.Append('\n');

                continue;
            }

            newlines = 0;
            _ = builder.Append(c);
        }

        return builder.ToString();
    }

    private static void Add(List<DocBlock> blocks, DocBlock? block)
    {
        if (block != null) blocks.Add(block);
    }

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Scripts();

    [GeneratedRegex(@"<h([1-6])[^>]*>(.*?)</h\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Headings();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Items();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Paragraphs();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}

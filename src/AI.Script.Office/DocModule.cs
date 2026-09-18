using AI.NLP.Lemmatization;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text;

namespace AI.Script.Office;

/// <summary>
/// Пространство <c>doc</c>: документы Word, PDF и разметки.
/// </summary>
/// <remarks>
/// Один набор функций на все форматы: скрипт пишет <c>doc.tables(d)</c>, не думая, docx это или
/// pdf. Разбор формата — дело читателя, а не автора скрипта.
/// <para>
/// Счёт по документу — арифметика, а не оценка модели. «Сколько знаков в договоре» и «сколько
/// раз встречается неустойка» обязаны совпадать со «Статистикой» Word и с поиском по файлу, а
/// не быть похожими на правду.
/// </para>
/// </remarks>
[ScriptModule("doc", "Документы Word, PDF и разметки: разделы, таблицы, точный счёт", Version = "0.1", Group = "данные")]
public static partial class DocModule
{
    private static readonly Lazy<ILemmatizer> s_lemmatizer = new(() => Lemmatizer.CreateRussian());

    [ScriptFn("load", "Открывает документ: docx, pdf, md, html, txt",
        Example = "let d = doc.load(\"договор.docx\")",
        Reads = "docx,pdf,md,markdown,html,htm", Returns = ScriptDocument.TypeName)]
    public static async Task<ScriptDocument> Load(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        if (await context.Sandbox.InfoAsync(path, context.Cancellation).ConfigureAwait(false) == null)
            throw new ScriptError(DiagnosticCodes.FileNotFound, $"doc.load: файл не найден — {path}");

        byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

        ScriptDocument document = DocumentReader.Read(bytes, path);

        context.CountAllocation(document.Blocks.Count);

        return document;
    }

    [ScriptFn("text", "Текст документа либо одного его раздела",
        Example = "doc.text(d, section: 2)")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static string Text(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("номер раздела; 0 — весь документ")] int section = 0)
        => section <= 0 ? d.Text : d.SectionText(section);

    [ScriptFn("outline", "Дерево разделов: номер, уровень, заголовок",
        Example = "show doc.outline(d)", Columns = "section,level,title")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptTable Outline([ScriptParam("документ")] ScriptDocument d)
    {
        IReadOnlyList<int> sections = d.Sections();
        var numbers = new List<ScriptValue>();
        var levels = new List<ScriptValue>();
        var titles = new List<ScriptValue>();

        for (int i = 0; i < d.Blocks.Count; i++)
        {
            if (d.Blocks[i].Kind != DocBlockKind.Heading) continue;

            numbers.Add(ScriptValue.Num(sections[i]));
            levels.Add(ScriptValue.Num(d.Blocks[i].Level));
            titles.Add(ScriptValue.Str(d.Blocks[i].Text));
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("section", [.. numbers]),
            ScriptColumn.Own("level", [.. levels]),
            ScriptColumn.Own("title", [.. titles]),
        ]);
    }

    [ScriptFn("blocks", "Блоки документа с адресом: раздел, страница, вид",
        Example = "doc.blocks(d, kind: \"table\")", Columns = "index,section,kind,level,page,address,text")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptTable Blocks(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("вид: \"heading\", \"paragraph\", \"list_item\", \"table\", \"image\"; пусто — все")] string kind = "")
    {
        IReadOnlyList<int> sections = d.Sections();
        var indexes = new List<ScriptValue>();
        var numbers = new List<ScriptValue>();
        var kinds = new List<ScriptValue>();
        var levels = new List<ScriptValue>();
        var pages = new List<ScriptValue>();
        var addresses = new List<ScriptValue>();
        var texts = new List<ScriptValue>();

        for (int i = 0; i < d.Blocks.Count; i++)
        {
            string name = Name(d.Blocks[i].Kind);

            if (kind.Length > 0 && !string.Equals(kind, name, StringComparison.Ordinal)) continue;

            indexes.Add(ScriptValue.Num(i));
            numbers.Add(ScriptValue.Num(sections[i]));
            kinds.Add(ScriptValue.Str(name));
            levels.Add(ScriptValue.Num(d.Blocks[i].Level));
            pages.Add(ScriptValue.Num(d.Blocks[i].Page));
            addresses.Add(ScriptValue.Str(d.Blocks[i].Address ?? string.Empty));
            texts.Add(ScriptValue.Str(d.Blocks[i].Text));
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("index", [.. indexes]),
            ScriptColumn.Own("section", [.. numbers]),
            ScriptColumn.Own("kind", [.. kinds]),
            ScriptColumn.Own("level", [.. levels]),
            ScriptColumn.Own("page", [.. pages]),
            ScriptColumn.Own("address", [.. addresses]),
            ScriptColumn.Own("text", [.. texts]),
        ]);
    }

    [ScriptFn("tables", "Таблицы документа списком", Example = "doc.tables(d)[0] |> table.clean()")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptList Tables([ScriptParam("документ")] ScriptDocument d)
    {
        var tables = new List<ScriptValue>();

        foreach (DocBlock block in d.Blocks)
        {
            if (block.Table != null) tables.Add(ScriptValue.Table(block.Table));
        }

        return ScriptList.From(tables);
    }

    /// <summary>
    /// Счёт по документу.
    /// </summary>
    /// <remarks>
    /// Знаки, слова и предложения считает арифметика: «в договоре 18 400 знаков» — это факт,
    /// который проверяется, а не оценка модели, которая правдоподобна ровно до первой сверки.
    /// Разбивка по разделам нужна там, где объём согласован по частям.
    /// </remarks>
    [ScriptFn("stats", "Счёт по документу: знаки, слова, предложения, абзацы, таблицы, страницы",
        Example = "doc.stats(d).chars_no_spaces")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptValue Stats(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("разбивка: \"section\"; пусто — по документу целиком")] string by = "")
    {
        if (!string.Equals(by, "section", StringComparison.Ordinal))
        {
            if (by.Length > 0)
            {
                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"doc.stats: неизвестная разбивка «{by}»",
                    "известна одна: \"section\" — по разделам документа");
            }

            return ScriptValue.Record(Count(d.Blocks, d.Text, d.PageCount));
        }

        int count = d.SectionCount();

        var numbers = new List<ScriptValue>();
        var titles = new List<ScriptValue>();
        var levels = new List<ScriptValue>();
        var records = new List<ScriptRecord>();

        // Раздел считается вместе с подразделами, как и его текст: «2.» включает «2.1». Чтобы итог по
        // документу не складывался дважды, у строки есть уровень: сумма по разделам первого уровня
        // равна счету документа без текста до первого заголовка.
        for (int section = 1; section <= count; section++)
        {
            IReadOnlyList<DocBlock> blocks = d.SectionBlocks(section);

            numbers.Add(ScriptValue.Num(section));
            titles.Add(ScriptValue.Str(blocks.Count > 0 ? blocks[0].Text : string.Empty));
            levels.Add(ScriptValue.Num(blocks.Count > 0 ? blocks[0].Level : 0));
            records.Add(Count(blocks, d.SectionText(section), 0));
        }

        var columns = new List<ScriptColumn>
        {
            ScriptColumn.Own("section", [.. numbers]),
            ScriptColumn.Own("title", [.. titles]),
            ScriptColumn.Own("level", [.. levels]),
        };

        foreach (string field in Fields)
        {
            var values = new ScriptValue[records.Count];

            for (int i = 0; i < records.Count; i++) values[i] = records[i].TryGet(field, out ScriptValue value) ? value : ScriptValue.None;

            columns.Add(ScriptColumn.Own(field, values));
        }

        return ScriptValue.Table(ScriptTable.Create(columns));
    }

    /// <summary>
    /// Сколько раз слово встречается в документе.
    /// </summary>
    /// <remarks>
    /// По умолчанию считается точное вхождение слова целиком, а не подстроки: «акт» не должен
    /// находиться внутри «контракта». С <c>forms: true</c> сравниваются начальные формы, и
    /// «неустойки» считается вместе с «неустойка».
    /// </remarks>
    [ScriptFn("count", "Сколько раз слово встречается в документе",
        Example = "doc.count(d, term: \"неустойка\", forms: true)")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static double Count(
        [ScriptParam("документ")] ScriptDocument d,
        [ScriptParam("слово либо словосочетание")] string term,
        [ScriptParam("считать словоформы одного слова вместе")] bool forms = false)
    {
        if (string.IsNullOrWhiteSpace(term)) return 0;

        string[] needle = Words(term);

        if (needle.Length == 0) return 0;

        string[] words = Words(d.Text);

        if (forms)
        {
            needle = [.. needle.Select(Normal)];
            words = [.. words.Select(Normal)];
        }

        int found = 0;

        for (int i = 0; i + needle.Length <= words.Length; i++)
        {
            bool match = true;

            for (int j = 0; j < needle.Length && match; j++)
                match = string.Equals(words[i + j], needle[j], StringComparison.OrdinalIgnoreCase);

            if (match) found++;
        }

        return found;
    }

    /// <summary>Поля счёта: один список на запись и на разбивку по разделам.</summary>
    private static string[] Fields =>
        ["chars", "chars_no_spaces", "words", "sentences", "paragraphs", "headings", "tables", "images", "pages"];

    private static ScriptRecord Count(IReadOnlyList<DocBlock> blocks, string text, int pages)
    {
        int paragraphs = 0;
        int headings = 0;
        int tables = 0;
        int images = 0;

        foreach (DocBlock block in blocks)
        {
            switch (block.Kind)
            {
                case DocBlockKind.Heading: headings++; break;
                case DocBlockKind.Table: tables++; break;
                case DocBlockKind.Image: images++; break;
                default: paragraphs++; break;
            }
        }

        int spaces = 0;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) spaces++;
        }

        return ScriptRecord.From(
        [
            Field("chars", text.Length),
            Field("chars_no_spaces", text.Length - spaces),
            Field("words", Words(text).Length),
            Field("sentences", Sentences(text)),
            Field("paragraphs", paragraphs),
            Field("headings", headings),
            Field("tables", tables),
            Field("images", images),
            Field("pages", pages),
        ]);
    }

    private static KeyValuePair<string, ScriptValue> Field(string name, double value) =>
        new(name, ScriptValue.Num(value));

    private static string[] Words(string text) =>
        text.Split([' ', '\t', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Trim('.', ',', ';', ':', '!', '?', '«', '»', '"', '(', ')', '[', ']', '—', '–', '-'))
            .Where(word => word.Length > 0)
            .ToArray();

    /// <summary>
    /// Сколько предложений в тексте.
    /// </summary>
    /// <remarks>
    /// Точка, восклицательный и вопросительный знаки подряд считаются одним концом: «Что?!» —
    /// это одно предложение, а не два.
    /// </remarks>
    private static int Sentences(string text)
    {
        int count = 0;
        bool inside = false;

        foreach (char c in text)
        {
            if (c is '.' or '!' or '?' or '…')
            {
                if (inside) count++;

                inside = false;
                continue;
            }

            if (!char.IsWhiteSpace(c)) inside = true;
        }

        return inside ? count + 1 : count;
    }

    private static string Normal(string word) => s_lemmatizer.Value.Lemmatize(word.ToLowerInvariant());

    private static string Name(DocBlockKind kind) => kind switch
    {
        DocBlockKind.Heading => "heading",
        DocBlockKind.Paragraph => "paragraph",
        DocBlockKind.ListItem => "list_item",
        DocBlockKind.Table => "table",
        _ => "image",
    };
}

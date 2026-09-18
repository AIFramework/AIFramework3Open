using AI.Script.Hosting;
using AI.Script.Semantics;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AI.Script.UnitTests;

/// <summary>
/// Книги Excel и документы Word и разметки.
/// </summary>
/// <remarks>
/// Файлы для проверки собираются здесь же теми же библиотеками: тест, прикладывающий готовый
/// xlsx, проверяет заодно и содержимое чужого файла, а сломавшись, не говорит, что именно
/// разошлось.
/// <para>
/// Деньги проверяются отдельно от прочих чисел: колонка с денежным форматом обязана прийти
/// точной, иначе итог отчёта разойдётся с итогом в самой книге.
/// </para>
/// </remarks>
public sealed class OfficeTests : IDisposable
{
    private const string Report = """
        # Договор

        Общие положения. Стороны договорились о поставке.

        ## Неустойка

        За просрочку начисляется неустойка. Размер неустойки — 0,1 % за день.

        | Этап | Сумма |
        |---|---|
        | Аванс | 1 000,00 ₽ |
        | Остаток | 2 000,00 ₽ |

        ## Сроки

        Поставка в течение месяца.
        """;

    private const string Requisites = """
        # Договор поставки

        Срок поставки — 15 марта 2026 г. Сумма договора составляет 1 250 000,00 ₽.

        Номер договора № 45-П от 01.02.2026.

        ## Гарантия

        Гарантийный срок 24 месяца.
        """;

    private const string Revised = """
        # Договор

        Общие положения. Стороны договорились о поставке.

        ## Неустойка

        За просрочку начисляется неустойка. Размер неустойки — 0,5 % за день.

        | Этап | Сумма |
        |---|---|
        | Аванс | 1 000,00 ₽ |
        | Остаток | 2 000,00 ₽ |

        ## Сроки

        Поставка в течение месяца.

        ## Ответственность

        Стороны несут ответственность по закону.
        """;

    private readonly string _root;

    public OfficeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aiscript-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки не должна ронять тест.
        }
    }

    private RunResult Run(string source) =>
        Script.RunWith(Script.FullHost(), source, new RunOptions { Sandbox = new WorkspaceSandbox(_root) });

    private RunResult RunOk(string source)
    {
        RunResult result = Run(source);

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    private string Path_(string name) => Path.Combine(_root, name);

    private void WriteText(string name, string content) => File.WriteAllText(Path_(name), content);

    /// <summary>Книга с шапкой в две строки и объединённой ячейкой верхнего уровня.</summary>
    private void WriteWorkbook(string name)
    {
        using var workbook = new XLWorkbook();

        IXLWorksheet sheet = workbook.Worksheets.Add("Факт");

        sheet.Cell(1, 1).Value = "Город";
        sheet.Cell(1, 2).Value = "Выручка";
        sheet.Range(1, 2, 1, 3).Merge();
        sheet.Cell(2, 2).Value = "план";
        sheet.Cell(2, 3).Value = "факт";

        sheet.Cell(3, 1).Value = "Пермь";
        sheet.Cell(3, 2).Value = 100.10;
        sheet.Cell(3, 3).Value = 200.20;
        sheet.Cell(4, 1).Value = "Москва";
        sheet.Cell(4, 2).Value = 300.30;
        sheet.Cell(4, 3).Value = 400.40;

        sheet.Range(3, 2, 4, 3).Style.NumberFormat.Format = "# ##0.00 ₽";

        _ = workbook.Worksheets.Add("Черновик");

        workbook.SaveAs(Path_(name));
    }

    /// <summary>Документ Word: два заголовка и абзац между ними.</summary>
    private void WriteWord(string name)
    {
        using WordprocessingDocument document = WordprocessingDocument.Create(
            Path_(name), WordprocessingDocumentType.Document);

        MainDocumentPart main = document.AddMainDocumentPart();

        main.Document = new Document(new Body(
            Heading("Договор", "Heading1"),
            Text("Стороны договорились о поставке."),
            Heading("Неустойка", "Heading2"),
            Text("За просрочку начисляется неустойка.")));

        main.Document.Save();
    }

    private static Paragraph Heading(string text, string style) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = style }),
            new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text)));

    private static Paragraph Text(string text) =>
        new(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text)));

    // --- книги Excel ---

    [Fact]
    public void Xls_Sheets_AreListedInOrder()
    {
        WriteWorkbook("книга.xlsx");

        RunResult result = RunOk("emit r = xls.sheets(\"книга.xlsx\")");

        Assert.Equal(new object?[] { "Факт", "Черновик" }, (System.Collections.IEnumerable)result.Emitted["r"]!);
    }

    /// <summary>Шапку в две строки разбирает библиотека: скрипт каждый раз делал бы это по-своему.</summary>
    [Fact]
    public void Xls_TwoRowHeader_JoinsLevels()
    {
        WriteWorkbook("книга.xlsx");

        RunResult result = RunOk("""
            let t = xls.read("книга.xlsx", header: 2)

            emit cols = table.columns(t)
            emit rows = len(t)
            """);

        Assert.Equal(
            new object?[] { "Город", "Выручка / план", "Выручка / факт" },
            (System.Collections.IEnumerable)result.Emitted["cols"]!);

        Assert.Equal(2.0, result.Emitted["rows"]);
    }

    /// <summary>Денежный формат ячейки — это обещание точности: колонка приходит точным числом.</summary>
    [Fact]
    public void Xls_MoneyColumn_IsExact()
    {
        WriteWorkbook("книга.xlsx");

        RunResult result = RunOk("""
            let t = xls.read("книга.xlsx", header: 2)

            emit сумма = dec.sum(t["Выручка / факт"])
            """);

        Assert.Equal(600.60m, result.Emitted["сумма"]);
    }

    [Fact]
    public void Xls_Range_LimitsWhatIsRead()
    {
        WriteWorkbook("книга.xlsx");

        Assert.Equal(1.0, (double)RunOk("emit r = len(xls.read(\"книга.xlsx\", range: \"A1:C3\", header: 2))").Emitted["r"]!);
    }

    [Fact]
    public void Xls_UnknownSheet_ListsSheets()
    {
        WriteWorkbook("книга.xlsx");

        RunResult result = Run("emit r = xls.read(\"книга.xlsx\", sheet: \"Итоги\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.UnknownArgument, result.Error!.Code);
        Assert.Contains("Черновик", result.Error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Записанное языком читается им же, и копейка по дороге не теряется.</summary>
    [Fact]
    public void Xls_Write_ThenRead_KeepsExactMoney()
    {
        RunResult result = RunOk("""
            let t = table.of({ клиент: ["а", "б"], сумма: ["1 000,10 ₽", "2 000,20 ₽"] }) |> table.clean()

            t |> xls.write("итог.xlsx")

            emit r = dec.sum(xls.read("итог.xlsx")["сумма"])
            """);

        Assert.Equal(3000.30m, result.Emitted["r"]);
        Assert.True(File.Exists(Path_("итог.xlsx")));
    }

    [Fact]
    public void Xls_Write_RegistersFileArtifact()
    {
        RunResult result = RunOk("emit r = io.save(table.of({ a: <1> }), \"итог.xlsx\")");

        Assert.Contains(result.Artifacts, artifact => artifact.Kind == "file" && artifact.Title == "итог.xlsx");
    }

    [Fact]
    public void Xls_NotAWorkbook_SuggestsResaving()
    {
        WriteText("книга.xlsx", "это не книга");

        RunResult result = Run("emit r = xls.sheets(\"книга.xlsx\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.BadFileFormat, result.Error!.Code);
    }

    // --- документы ---

    [Fact]
    public void Doc_Outline_ListsHeadingsWithLevels()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let d = doc.load("договор.md")
            let o = doc.outline(d)

            emit rows = len(o)
            emit second = o[1].title
            emit level = o[1].level
            """);

        Assert.Equal(3.0, result.Emitted["rows"]);
        Assert.Equal("Неустойка", result.Emitted["second"]);
        Assert.Equal(2.0, result.Emitted["level"]);
    }

    /// <summary>Счёт по документу — арифметика: цифры обязаны сходиться с поиском по файлу.</summary>
    [Fact]
    public void Doc_Stats_CountsCharactersAndWords()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let d = doc.load("договор.md")
            let s = doc.stats(d)

            emit chars = s.chars
            emit no_spaces = s.chars_no_spaces
            emit headings = s.headings
            emit tables = s.tables
            """);

        Assert.True((double)result.Emitted["chars"]! > (double)result.Emitted["no_spaces"]!);
        Assert.Equal(3.0, result.Emitted["headings"]);
        Assert.Equal(1.0, result.Emitted["tables"]);
    }

    [Fact]
    public void Doc_Stats_BySection_GivesRowPerSection()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let s = doc.stats(doc.load("договор.md"), by: "section")

            emit rows = len(s)
            emit title = s[1].title
            """);

        Assert.Equal(3.0, result.Emitted["rows"]);
        Assert.Equal("Неустойка", result.Emitted["title"]);
    }

    /// <summary>Раздел считается с подразделами, и уровень строки не дает сложить их дважды.</summary>
    [Fact]
    public void Doc_Stats_BySection_CountsSubsectionsWithLevel()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let s = doc.stats(doc.load("договор.md"), by: "section")

            emit levels = [s[0].level, s[1].level, s[2].level]
            emit tables = s[0].tables
            emit whole = s[0].words
            emit parts = s[1].words + s[2].words
            """);

        Assert.Equal(new object?[] { 1.0, 2.0, 2.0 }, (System.Collections.IEnumerable)result.Emitted["levels"]!);
        Assert.Equal(1.0, result.Emitted["tables"]);
        Assert.True((double)result.Emitted["whole"]! > (double)result.Emitted["parts"]!);
    }

    /// <summary>«акт» не должен находиться внутри «контракта»: считается слово целиком.</summary>
    [Fact]
    public void Doc_Count_CountsWholeWordsAndForms()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let d = doc.load("договор.md")

            emit exact = doc.count(d, term: "неустойка")
            emit forms = doc.count(d, term: "неустойка", forms: true)
            emit missing = doc.count(d, term: "акт")
            """);

        // Заголовок раздела — тоже текст документа, поэтому «неустойка» встречается дважды, а
        // со словоформами к ним добавляется «неустойки».
        Assert.Equal(2.0, result.Emitted["exact"]);
        Assert.Equal(3.0, result.Emitted["forms"]);
        Assert.Equal(0.0, result.Emitted["missing"]);
    }

    [Fact]
    public void Doc_Text_TakesOneSection()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("emit r = doc.text(doc.load(\"договор.md\"), section: 3)");

        Assert.Contains("Поставка", (string)result.Emitted["r"]!, StringComparison.Ordinal);
        Assert.DoesNotContain("Аванс", (string)result.Emitted["r"]!, StringComparison.Ordinal);
    }

    /// <summary>Таблица документа уходит дальше по конвейеру обычной таблицей.</summary>
    [Fact]
    public void Doc_Tables_FeedTableModule()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let t = doc.tables(doc.load("договор.md"))[0] |> table.clean()

            emit rows = len(t)
            emit сумма = dec.sum(t["Сумма"])
            """);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(3000.00m, result.Emitted["сумма"]);
    }

    [Fact]
    public void Doc_Blocks_FilterByKind()
    {
        WriteText("договор.md", Report);

        Assert.Equal(3.0, (double)RunOk("emit r = len(doc.blocks(doc.load(\"договор.md\"), kind: \"heading\"))").Emitted["r"]!);
    }

    [Fact]
    public void Doc_Word_ReadsHeadingsAndText()
    {
        WriteWord("договор.docx");

        RunResult result = RunOk("""
            let d = doc.load("договор.docx")

            emit headings = len(doc.outline(d))
            emit level = doc.outline(d)[1].level
            emit words = doc.count(d, term: "неустойка", forms: true)
            """);

        Assert.Equal(2.0, result.Emitted["headings"]);
        Assert.Equal(2.0, result.Emitted["level"]);
        Assert.Equal(2.0, result.Emitted["words"]);
    }

    /// <summary>Формат определяет читателя, поэтому общий <c>io.load</c> открывает и документ.</summary>
    [Fact]
    public void Io_Load_OpensDocument()
    {
        WriteWord("договор.docx");

        Assert.Equal(2.0, (double)RunOk("emit r = len(doc.outline(io.load(\"договор.docx\")))").Emitted["r"]!);
    }

    // --- извлечение по схеме ---

    /// <summary>Значение берётся из блока с подписью поля: число само по себе ничего не значит.</summary>
    [Fact]
    public void Doc_Extract_FindsValuesWithAddress()
    {
        WriteText("договор.md", Requisites);

        RunResult result = RunOk("""
            let d = doc.load("договор.md")
            let r = d |> doc.extract(schema: { срок: "date", сумма: "dec" })

            emit строк = len(r)
            emit сумма = (r |> table.filter(row => row.field == "сумма"))[0]["value"]
            emit год = date.year((r |> table.filter(row => row.field == "срок"))[0]["value"])
            emit раздел = r[0].section
            """);

        Assert.Equal(2026.0, result.Emitted["год"]);
        Assert.Equal(1_250_000.00m, result.Emitted["сумма"]);
        Assert.Equal(1.0, result.Emitted["раздел"]);
        Assert.True((double)result.Emitted["строк"]! >= 2);
    }

    /// <summary>Подпись бывает не словом поля: синонимы задаются рядом со схемой.</summary>
    [Fact]
    public void Doc_Extract_UsesLabels()
    {
        WriteText("договор.md", Requisites);

        RunResult result = RunOk("""
            let r = doc.extract(doc.load("договор.md"),
                schema: { поставка: "date" },
                labels: { поставка: ["Срок"] })

            emit нашлось = len(r) > 0
            """);

        Assert.Equal(true, result.Emitted["нашлось"]);
    }

    [Fact]
    public void Doc_Extract_RegexRule()
    {
        WriteText("договор.md", Requisites);

        RunResult result = RunOk("""
            let r = doc.extract(doc.load("договор.md"), schema: { номер: "re:№ *([0-9]+-[А-Я])" })

            emit номер = r[0].value
            """);

        Assert.Equal("45-П", result.Emitted["номер"]);
    }

    [Fact]
    public void Doc_Extract_TableColumn()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let r = doc.extract(doc.load("договор.md"), schema: { Сумма: "dec" })

            emit итого = dec.sum(r["value"])
            """);

        Assert.Equal(3000.00m, result.Emitted["итого"]);
    }

    [Fact]
    public void Doc_Extract_UnknownKind_ListsKnown()
    {
        WriteText("договор.md", Requisites);

        RunResult result = Run("emit r = doc.extract(doc.load(\"договор.md\"), schema: { срок: \"когда\" })");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.UnknownArgument, result.Error!.Code);
    }

    // --- сравнение версий ---

    /// <summary>Неизменённые блоки в отчёт не попадают: иначе его невозможно читать.</summary>
    [Fact]
    public void Doc_Diff_ShowsOnlyChanges()
    {
        WriteText("старый.md", Report);
        WriteText("новый.md", Revised);

        RunResult result = RunOk("""
            let d = doc.diff(doc.load("старый.md"), doc.load("новый.md"))

            emit строк = len(d)
            emit переписано = len(d |> table.filter(row => row.kind == "changed"))
            emit добавлено = len(d |> table.filter(row => row.kind == "added"))
            """);

        Assert.True((double)result.Emitted["переписано"]! >= 1, "переписанный пункт не найден");
        Assert.True((double)result.Emitted["добавлено"]! >= 1, "новый раздел не найден");
        Assert.True((double)result.Emitted["строк"]! < 8, "в отчёт попали неизменённые блоки");
    }

    /// <summary>У переписанного блока есть мера сходства: этим он и отличается от замены.</summary>
    [Fact]
    public void Doc_Diff_ChangedBlockKeepsSimilarity()
    {
        WriteText("старый.md", Report);
        WriteText("новый.md", Revised);

        RunResult result = RunOk("""
            let d = doc.diff(doc.load("старый.md"), doc.load("новый.md"))
                |> table.filter(row => row.kind == "changed")

            emit сходство = d[0].similarity > 0.4
            emit адрес = d[0].old_block != none
            """);

        Assert.Equal(true, result.Emitted["сходство"]);
        Assert.Equal(true, result.Emitted["адрес"]);
    }

    [Fact]
    public void Doc_Diff_SameDocument_HasNoChanges()
    {
        WriteText("а.md", Report);
        WriteText("б.md", Report);

        Assert.Equal(0.0, (double)RunOk("emit r = len(doc.diff(doc.load(\"а.md\"), doc.load(\"б.md\")))").Emitted["r"]!);
    }

    [Fact]
    public void Doc_MissingFile_IsReported()
    {
        RunResult result = Run("emit r = doc.load(\"нет.md\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.FileNotFound, result.Error!.Code);
    }

    // --- найденное при проверке ---

    /// <summary>Русский Word хранит заголовки стилями «1», «2»: уровень берется из имени стиля.</summary>
    [Fact]
    public void Doc_RussianWordHeadingStyles_AreHeadings()
    {
        using (WordprocessingDocument document = WordprocessingDocument.Create(Path_("ру.docx"), WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            StyleDefinitionsPart styles = main.AddNewPart<StyleDefinitionsPart>();

            styles.Styles = new Styles(
                new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "1" },
                new Style(new StyleName { Val = "heading 2" }) { Type = StyleValues.Paragraph, StyleId = "2" });
            styles.Styles.Save();

            main.Document = new Document(new Body(
                Heading("Договор", "1"), Text("Текст."), Heading("Сроки", "2"), Text("Месяц.")));
            main.Document.Save();
        }

        RunResult result = RunOk("emit n = len(doc.outline(doc.load(\"ру.docx\")))");

        Assert.Equal(2.0, result.Emitted["n"]);
    }

    /// <summary>Раздел включает свои подразделы.</summary>
    [Fact]
    public void Doc_SectionText_IncludesSubsections()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("emit r = doc.text(doc.load(\"договор.md\"), section: 1)");

        Assert.Contains("неустойка", (string)result.Emitted["r"]!, StringComparison.Ordinal);
        Assert.Contains("Поставка в течение", (string)result.Emitted["r"]!, StringComparison.Ordinal);
    }

    /// <summary>Прочитанная таблица при записи не получает подписью свою же свертку.</summary>
    [Fact]
    public void Doc_ReadTable_SavedWithoutTextCaption()
    {
        WriteText("договор.md", Report);

        RunOk("doc.save(doc.load(\"договор.md\"), \"копия.md\")");

        string copy = File.ReadAllText(Path_("копия.md"));

        Assert.DoesNotContain(copy.Split('\n'), line => !line.TrimStart().StartsWith('|') && line.Contains(" | ", StringComparison.Ordinal));
    }

    [Fact]
    public void Doc_Replace_ChangesTableCells()
    {
        WriteText("договор.md", Report);

        RunResult result = RunOk("""
            let r = doc.replace(doc.load("договор.md"), from: "Аванс", to: "Предоплата")
            emit этап = doc.tables(r)[0][0]["Этап"]
            """);

        Assert.Equal("Предоплата", result.Emitted["этап"]);
    }

    /// <summary>Реквизиты в таблице «поле | значение»: подпись в первой ячейке строки.</summary>
    [Fact]
    public void Doc_Extract_LabelValueTable()
    {
        WriteText("реквизиты.md", """
            # Договор

            | Поле | Значение |
            |---|---|
            | Сумма | 500 000,00 ₽ |
            | Город | Пермь |
            """);

        RunResult result = RunOk("""
            let x = doc.extract(doc.load("реквизиты.md"), schema: { сумма: "dec" })
            emit n = len(x)
            emit v = x[0].value
            """);

        Assert.Equal(1.0, result.Emitted["n"]);
        Assert.Equal(500000.00m, result.Emitted["v"]);
    }

    /// <summary>Метка локали в формате ячейки не делает число деньгами.</summary>
    [Fact]
    public void Xls_LocaleTag_IsNotMoney()
    {
        using (var workbook = new XLWorkbook())
        {
            IXLWorksheet sheet = workbook.Worksheets.Add("Лист1");

            sheet.Cell(1, 1).Value = "доля";
            sheet.Cell(2, 1).Value = 0.123456;
            sheet.Cell(2, 1).Style.NumberFormat.Format = "[$-419]0.000000";
            workbook.SaveAs(Path_("доли.xlsx"));
        }

        RunResult result = RunOk("emit v = xls.read(\"доли.xlsx\")[0].доля");

        Assert.Equal(0.123456, result.Emitted["v"]);
    }

    /// <summary>PDF читается построчно: страница не сливается в один блок.</summary>
    [Fact]
    public void Doc_Pdf_ReadsLines()
    {
        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        page.AddText("INTRODUCTION", 14, new UglyToad.PdfPig.Core.PdfPoint(40, 780), font);
        page.AddText("This is the first paragraph line.", 11, new UglyToad.PdfPig.Core.PdfPoint(40, 750), font);
        page.AddText("Another line of the same text.", 11, new UglyToad.PdfPig.Core.PdfPoint(40, 730), font);
        File.WriteAllBytes(Path_("text.pdf"), builder.Build());

        RunResult result = RunOk("emit n = len(doc.blocks(doc.load(\"text.pdf\")))");

        Assert.True((double)result.Emitted["n"]! >= 3, $"блоков {result.Emitted["n"]}");
    }
}

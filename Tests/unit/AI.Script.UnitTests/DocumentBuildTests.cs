using AI.Script.Hosting;
using AI.Script.Semantics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AI.Script.UnitTests;

/// <summary>
/// Сборка документа по звеньям и точечная правка с сохранением оформления.
/// </summary>
/// <remarks>
/// Документ проверяется двумя путями: валидатором формата Word (файл обязан открываться без
/// предупреждений) и обратным чтением тем же языком (таблицы и рисунки на месте). Правка
/// проверяется по самому файлу: оформление слова, которое заменили, обязано уцелеть.
/// </remarks>
public sealed class DocumentBuildTests : IDisposable
{
    private readonly string _root;

    public DocumentBuildTests()
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

    private string File_(string name) => Path.Combine(_root, name);

    /// <summary>
    /// Договор, где слово «Исполнитель» разрезано на два фрагмента с разным оформлением, а в
    /// документе есть свой стиль: так Word и хранит текст после правок.
    /// </summary>
    private void WriteContract(string name)
    {
        using WordprocessingDocument document = WordprocessingDocument.Create(File_(name), WordprocessingDocumentType.Document);

        MainDocumentPart main = document.AddMainDocumentPart();
        StyleDefinitionsPart styles = main.AddNewPart<StyleDefinitionsPart>();

        styles.Styles = new Styles(new Style(new StyleName { Val = "Особый" }) { Type = StyleValues.Paragraph, StyleId = "Special" });

        main.Document = new Document(new Body(
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text("Договор"))),
            new Paragraph(
                new Run(new RunProperties(new Bold()), new Text("Испол")),
                new Run(new Text("нитель обязуется выполнить работы.") { Space = SpaceProcessingModeValues.Preserve })),
            new Paragraph(new Run(new Text("Клиент: {{клиент}}, сумма {{сумма}}."))),
            new Paragraph(new Run(new Text("Подписи сторон.")))));

        main.Document.Save();
    }

    /// <summary>Замечания валидатора формата Word; пусто — файл открывается без предупреждений.</summary>
    private static IReadOnlyList<string> Validate(string path)
    {
        using WordprocessingDocument document = WordprocessingDocument.Open(path, isEditable: false);

        return [.. new OpenXmlValidator().Validate(document).Select(e => $"{e.Path?.XPath}: {e.Description}")];
    }

    // --- сборка ---

    /// <summary>Отчёт открывается в Word без предупреждений: таблица и график на месте.</summary>
    [Fact]
    public void Build_ReportWithTableAndPlot_IsValidWord()
    {
        RunResult result = RunOk("""
            let итоги = table.of({ вариант: ["a", "b"], score: <0.61, 0.74> })

            doc.new(title: "Отчёт об опыте")
                |> doc.paragraph("Сравнивали два варианта по пять повторов.")
                |> doc.heading("Итоги", level: 2)
                |> doc.table(итоги, title: "Таблица 1")
                |> doc.figure(plot.line(<1, 3, 2, 5>, title: "Ряд"), caption: "Рис. 1")
                |> doc.save("отчёт.docx")

            let d = doc.load("отчёт.docx")

            emit разделов = len(doc.outline(d))
            emit таблиц = len(doc.tables(d))
            emit рисунков = len(doc.blocks(d, kind: "image"))
            """);

        Assert.Equal(2.0, result.Emitted["разделов"]);
        Assert.Equal(1.0, result.Emitted["таблиц"]);
        Assert.Equal(1.0, result.Emitted["рисунков"]);
        IReadOnlyList<string> problems = Validate(File_("отчёт.docx"));

        Assert.True(problems.Count == 0, string.Join(" || ", problems));
    }

    [Fact]
    public void Build_SaveRegistersFileArtifact()
    {
        RunResult result = RunOk("emit r = doc.new(title: \"Отчёт\") |> io.save(\"отчёт.docx\")");

        Assert.Contains(result.Artifacts, a => a.Kind == "file" && a.Title == "отчёт.docx");
    }

    /// <summary>Разметку нельзя сделать с вложенной картинкой: рисунок ложится рядом файлом.</summary>
    [Fact]
    public void Build_Markdown_PutsFiguresNextToIt()
    {
        RunOk("""
            doc.new(title: "Отчёт")
                |> doc.figure(plot.bar(<3, 5, 2>), caption: "Столбцы")
                |> doc.save("отчёт.md")
            """);

        Assert.True(File.Exists(File_("отчёт-1.png")));
        Assert.Contains("![Столбцы](отчёт-1.png)", File.ReadAllText(File_("отчёт.md")), StringComparison.Ordinal);
    }

    /// <summary>Вид графика, который движок нарисовать не может, — отказ, а не пустая рамка.</summary>
    [Fact]
    public void Build_UnrenderablePlot_IsRefused()
    {
        RunResult result = Run("emit r = doc.new() |> doc.figure(plot.heatmap(mat.eye(3)))");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.NotImplementedYet, result.Error!.Code);
    }

    [Fact]
    public void Build_PdfIsNotWritten_AndSaysWhy()
    {
        RunResult result = Run("emit r = doc.new(title: \"x\") |> doc.save(\"отчёт.pdf\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.BadFileFormat, result.Error!.Code);
    }

    // --- правка ---

    /// <summary>
    /// Замена находит слово, разрезанное на фрагменты, и кладёт замену в оформление начала слова.
    /// </summary>
    [Fact]
    public void Replace_KeepsFormattingOfReplacedWord()
    {
        WriteContract("договор.docx");

        RunOk("""
            doc.load("договор.docx")
                |> doc.replace(from: "Исполнитель", to: "Подрядчик")
                |> doc.save("договор_v2.docx")
            """);

        using WordprocessingDocument document = WordprocessingDocument.Open(File_("договор_v2.docx"), isEditable: false);
        Body body = document.MainDocumentPart!.Document.Body!;
        Run bold = body.Descendants<Run>().First(run => run.RunProperties?.Bold != null);

        Assert.Equal("Подрядчик", bold.InnerText);
        Assert.Contains("Подрядчик обязуется", body.InnerText, StringComparison.Ordinal);
        Assert.NotNull(document.MainDocumentPart.StyleDefinitionsPart!.Styles!
            .Elements<Style>().FirstOrDefault(style => style.StyleId == "Special"));
    }

    /// <summary>После правки doc.diff показывает ровно заказанное изменение.</summary>
    [Fact]
    public void Replace_DiffShowsOnlyRequestedChange()
    {
        WriteContract("договор.docx");

        RunResult result = RunOk("""
            let старый = doc.load("договор.docx")
            let новый = старый |> doc.replace(from: "Исполнитель", to: "Подрядчик")
            let правки = doc.diff(старый, новый)

            emit строк = len(правки)
            emit вид = правки[0].kind
            """);

        Assert.Equal(1.0, result.Emitted["строк"]);
        Assert.Equal("changed", result.Emitted["вид"]);
    }

    [Fact]
    public void Insert_AfterBlockNumber()
    {
        WriteContract("договор.docx");

        RunResult result = RunOk("""
            let d = doc.load("договор.docx") |> doc.insert(after: 1, text: "Срок выполнения — месяц.")

            emit текст = doc.blocks(d)[2].text
            emit блоков = len(doc.blocks(d))
            """);

        Assert.Equal("Срок выполнения — месяц.", result.Emitted["текст"]);
        Assert.Equal(5.0, result.Emitted["блоков"]);
    }

    [Fact]
    public void Insert_BadAddress_IsRefused()
    {
        WriteContract("договор.docx");

        RunResult result = Run("emit r = doc.load(\"договор.docx\") |> doc.insert(after: 40, text: \"x\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.IndexOutOfRange, result.Error!.Code);
    }

    [Fact]
    public void Fill_ReplacesTemplateFields()
    {
        WriteContract("шаблон.docx");

        RunResult result = RunOk("""
            let d = doc.load("шаблон.docx") |> doc.fill(data: { клиент: "ООО Альфа", сумма: fmt.money(dec.of(1500)) })

            emit текст = doc.blocks(d)[2].text
            """);

        Assert.Equal("Клиент: ООО Альфа, сумма 1 500,00 ₽.", result.Emitted["текст"]);
    }

    /// <summary>Незаполненное поле — отказ: договор без суммы выглядит готовым.</summary>
    [Fact]
    public void Fill_MissingField_IsRefused()
    {
        WriteContract("шаблон.docx");

        RunResult result = Run("emit r = doc.load(\"шаблон.docx\") |> doc.fill(data: { клиент: \"ООО Альфа\" })");

        Assert.False(result.Success);
        Assert.Contains("сумма", result.Error!.Message, StringComparison.Ordinal);
    }

    /// <summary>Дописанное в чужой документ получает стили Word, а стили самого документа остаются.</summary>
    [Fact]
    public void Append_ToLoadedWord_KeepsItsStyles()
    {
        WriteContract("договор.docx");

        RunOk("""
            doc.load("договор.docx")
                |> doc.heading("Приложение", level: 2)
                |> doc.table(table.of({ этап: ["аванс"], сумма: <500> }))
                |> doc.save("договор_v3.docx")
            """);

        using WordprocessingDocument document = WordprocessingDocument.Open(File_("договор_v3.docx"), isEditable: false);

        Assert.Single(document.MainDocumentPart!.Document.Body!.Elements<Table>());
        Assert.NotNull(document.MainDocumentPart.StyleDefinitionsPart!.Styles!
            .Elements<Style>().FirstOrDefault(style => style.StyleId == "Special"));
    }

    /// <summary>Правка документа разметки — правка текста блоков.</summary>
    [Fact]
    public void Replace_Markdown_ChangesBlocks()
    {
        File.WriteAllText(File_("заметка.md"), "# Заметка\n\nИсполнитель готов.\n");

        RunResult result = RunOk("""
            emit текст = doc.text(doc.load("заметка.md") |> doc.replace(from: "Исполнитель", to: "Подрядчик"))
            """);

        Assert.Contains("Подрядчик готов.", (string)result.Emitted["текст"]!, StringComparison.Ordinal);
    }
}

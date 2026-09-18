using AI.Script.Runtime;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace AI.Script.Office;

/// <summary>
/// Блоки документа — в файл Word.
/// </summary>
/// <remarks>
/// Отчёт об опыте нужен человеку документом, а не транскриптом прогона. Стили задаются
/// настоящими стилями Word («Заголовок 1», «Сетка таблицы»), а не прямым оформлением абзаца:
/// тогда у документа работает оглавление и навигация, и его можно перекрасить одним движением.
/// <para>
/// Элементы строятся отдельно от пакета (<see cref="Elements"/>): те же абзацы, таблицы и
/// картинки дописываются и в чужой документ при правке, и оформление у них одно.
/// </para>
/// </remarks>
internal static class DocxWriter
{
    /// <summary>Ширина текстовой полосы страницы A4 с полями по 2 см, в EMU.</summary>
    private const long PageWidthEmu = 6_120_000;

    /// <summary>EMU в пикселе при 96 точках на дюйм.</summary>
    private const long EmuPerPixel = 9_525;

    /// <summary>Собирает новый документ Word.</summary>
    public static byte[] Write(ScriptDocument document)
    {
        using var stream = new MemoryStream();

        using (WordprocessingDocument package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = package.AddMainDocumentPart();

            main.Document = new Document(new Body());
            AddStyles(main);

            Body body = main.Document.Body!;

            foreach (DocBlock block in document.Blocks)
            {
                foreach (OpenXmlElement element in Elements(main, block)) body.Append(element);
            }

            main.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>Элементы Word для одного блока: абзац, таблица либо картинка с подписью.</summary>
    public static IEnumerable<OpenXmlElement> Elements(MainDocumentPart main, DocBlock block)
    {
        switch (block.Kind)
        {
            case DocBlockKind.Heading:
                yield return Styled(block.Text, $"Heading{Math.Clamp(block.Level, 1, 9)}");
                break;

            case DocBlockKind.ListItem:
                yield return Plain("• " + block.Text);
                break;

            case DocBlockKind.Table when block.Table is { } table:
                if (block.Caption is { Length: > 0 } caption) yield return Styled(caption, "Caption");

                yield return Table(table);
                break;

            case DocBlockKind.Image when block.Image is { } png:
                yield return Picture(main, png, block.Text);

                if (block.Text.Length > 0) yield return Styled(block.Text, "Caption");

                break;

            default:
                yield return Plain(block.Text);
                break;
        }
    }

    private static Paragraph Plain(string text) => new(Run(text));

    private static Paragraph Styled(string text, string style) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = style }), Run(text));

    private static Run Run(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>Таблица с шапкой из имён колонок и сеткой.</summary>
    private static Table Table(ScriptTable source)
    {
        var table = new Table(new TableProperties(
            new TableStyle { Val = "TableGrid" },
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            Borders()));

        // Сетка колонок обязательна: без неё Word чинит файл при открытии и спрашивает об этом.
        var grid = new TableGrid();

        foreach (ScriptColumn _ in source.Columns) grid.Append(new GridColumn());

        table.Append(grid);

        var header = new TableRow();

        foreach (ScriptColumn column in source.Columns)
            header.Append(Cell(column.Name, bold: true));

        table.Append(header);

        for (int i = 0; i < source.RowCount; i++)
        {
            var row = new TableRow();

            foreach (ScriptColumn column in source.Columns)
                row.Append(Cell(column[i].IsNone ? string.Empty : ScriptFormatter.Format(column[i]), bold: false));

            table.Append(row);
        }

        return table;
    }

    /// <summary>Границы таблицы — в порядке, которого требует схема Word: верх, лево, низ, право.</summary>
    private static TableBorders Borders() => new(
        new TopBorder { Val = BorderValues.Single, Size = 4 },
        new LeftBorder { Val = BorderValues.Single, Size = 4 },
        new BottomBorder { Val = BorderValues.Single, Size = 4 },
        new RightBorder { Val = BorderValues.Single, Size = 4 },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });

    private static TableCell Cell(string text, bool bold)
    {
        Run run = Run(text);

        if (bold) run.RunProperties = new RunProperties(new Bold());

        return new TableCell(new Paragraph(run));
    }

    /// <summary>
    /// Картинка в строке текста, вписанная в ширину полосы.
    /// </summary>
    /// <remarks>
    /// Размер пересчитывается из пикселей с сохранением пропорций: график шириной 1200 точек
    /// иначе вылез бы за поля страницы.
    /// </remarks>
    private static Paragraph Picture(MainDocumentPart main, byte[] png, string title)
    {
        ImagePart part = main.AddImagePart(ImagePartType.Png);

        using (var stream = new MemoryStream(png, writable: false)) part.FeedData(stream);

        (long width, long height) = Size(png);
        uint id = (uint)(main.ImageParts.Count() + 1);
        string relation = main.GetIdOfPart(part);

        var inline = new DW.Inline(
            new DW.Extent { Cx = width, Cy = height },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = id, Name = $"Рисунок {id}", Description = title },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"image{id}.png" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(
                        new A.Blip { Embed = relation },
                        new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0L, Y = 0L },
                            new A.Extents { Cx = width, Cy = height }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        };

        return new Paragraph(new Run(new Drawing(inline)));
    }

    /// <summary>Размер картинки в EMU по заголовку PNG, вписанный в ширину полосы.</summary>
    private static (long Width, long Height) Size(byte[] png)
    {
        // Ширина и высота лежат в заголовке IHDR: байты 16–23, старшим байтом вперёд.
        long pixelsWide = png.Length >= 24 ? (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19] : 800;
        long pixelsHigh = png.Length >= 24 ? (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23] : 500;

        long width = Math.Max(1, pixelsWide) * EmuPerPixel;
        long height = Math.Max(1, pixelsHigh) * EmuPerPixel;

        if (width <= PageWidthEmu) return (width, height);

        return (PageWidthEmu, height * PageWidthEmu / width);
    }

    /// <summary>
    /// Стили документа: заголовки, подпись и сетка таблицы.
    /// </summary>
    /// <remarks>
    /// Имена стилей — встроенные имена Word, поэтому документ, открытый в Word, получает его
    /// собственное оформление этих стилей и оглавление по заголовкам.
    /// </remarks>
    private static void AddStyles(MainDocumentPart main)
    {
        StyleDefinitionsPart part = main.AddNewPart<StyleDefinitionsPart>();

        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri" },
                    new FontSize { Val = "22" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "120" }))));

        for (int level = 1; level <= 3; level++)
        {
            styles.Append(new Style(
                new StyleName { Val = $"heading {level}" },
                new BasedOn { Val = "Normal" },
                new NextParagraphStyle { Val = "Normal" },
                new PrimaryStyle(),
                new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines { Before = "240", After = "120" },
                    new OutlineLevel { Val = level - 1 }),
                new StyleRunProperties(new Bold(), new FontSize { Val = (36 - (level * 4)).ToString() }))
            {
                Type = StyleValues.Paragraph,
                StyleId = $"Heading{level}",
            });
        }

        styles.Append(new Style(
            new StyleName { Val = "caption" },
            new BasedOn { Val = "Normal" },
            new StyleRunProperties(new Italic(), new FontSize { Val = "18" }))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Caption",
        });

        styles.Append(new Style(
            new StyleName { Val = "Table Grid" },
            new StyleTableProperties(Borders()))
        {
            Type = StyleValues.Table,
            StyleId = "TableGrid",
        });

        part.Styles = styles;
        part.Styles.Save();
    }
}

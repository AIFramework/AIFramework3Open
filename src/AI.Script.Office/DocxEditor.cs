using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AI.Script.Office;

/// <summary>
/// Точечная правка файла Word с сохранением оформления.
/// </summary>
/// <remarks>
/// Правится сам файл, а не модель блоков: у документа Word есть шрифты, стили, поля и
/// колонтитулы, которых в модели нет, и пересборка из блоков молча потеряла бы всё это.
/// <para>
/// Word режет текст абзаца на фрагменты по оформлению и по истории правок, и слово «Исполнитель»
/// легко оказывается в трёх фрагментах. Поэтому поиск идёт по склеенному тексту абзаца, а замена
/// ложится в фрагмент, где совпадение началось, — с его оформлением.
/// </para>
/// </remarks>
internal static class DocxEditor
{
    /// <summary>Сколько замен допускается в одном абзаце: защита от бесконечной замены «а» на «аа».</summary>
    private const int ReplacementsPerParagraph = 1000;

    /// <summary>Заменяет текст во всём документе; возвращает новый файл и число замен.</summary>
    public static (byte[] Bytes, int Count) Replace(byte[] source, string find, string replacement)
    {
        int count = 0;

        byte[] bytes = Edit(source, main =>
        {
            foreach (OpenXmlPartRootElement root in Roots(main))
            {
                foreach (Paragraph paragraph in root.Descendants<Paragraph>().ToList())
                    count += ReplaceIn(paragraph, find, replacement);
            }
        });

        return (bytes, count);
    }

    /// <summary>Вставляет блок после блока с номером <paramref name="after"/>; −1 — в начало.</summary>
    public static byte[] Insert(byte[] source, int after, DocBlock block) => Edit(source, main =>
    {
        Body body = main.Document.Body!;
        IReadOnlyList<OpenXmlElement> blocks = DocumentReader.WordBlocks(body);

        if (after >= blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(after), $"в документе {blocks.Count} блоков");

        OpenXmlElement anchor = after < 0 ? null! : blocks[after];

        foreach (OpenXmlElement element in DocxWriter.Elements(main, block).Reverse())
        {
            if (after < 0) body.InsertAt(element, 0);
            else anchor.InsertAfterSelf(element);
        }
    });

    /// <summary>Дописывает блок в конец документа — перед параметрами раздела, где они есть.</summary>
    public static byte[] Append(byte[] source, DocBlock block) => Edit(source, main =>
    {
        Body body = main.Document.Body!;
        SectionProperties? section = body.Elements<SectionProperties>().LastOrDefault();

        foreach (OpenXmlElement element in DocxWriter.Elements(main, block))
        {
            if (section is null) body.Append(element);
            else section.InsertBeforeSelf(element);
        }
    });

    private static byte[] Edit(byte[] source, Action<MainDocumentPart> change)
    {
        using var stream = new MemoryStream();

        stream.Write(source, 0, source.Length);
        stream.Position = 0;

        using (WordprocessingDocument package = WordprocessingDocument.Open(stream, isEditable: true))
        {
            MainDocumentPart main = package.MainDocumentPart
                ?? throw new InvalidOperationException("в файле нет основной части документа");

            change(main);

            main.Document.Save();

            foreach (HeaderPart header in main.HeaderParts) header.Header.Save();
            foreach (FooterPart footer in main.FooterParts) footer.Footer.Save();
        }

        return stream.ToArray();
    }

    /// <summary>Тело документа и колонтитулы: реквизиты стоят и там.</summary>
    private static IEnumerable<OpenXmlPartRootElement> Roots(MainDocumentPart main)
    {
        yield return main.Document;

        foreach (HeaderPart header in main.HeaderParts) yield return header.Header;
        foreach (FooterPart footer in main.FooterParts) yield return footer.Footer;
    }

    /// <summary>Замены в одном абзаце; возвращает их число.</summary>
    private static int ReplaceIn(Paragraph paragraph, string find, string replacement)
    {
        int count = 0;
        int from = 0;

        while (count < ReplacementsPerParagraph)
        {
            List<Text> texts = [.. paragraph.Descendants<Text>()];
            string full = string.Concat(texts.Select(text => text.Text));
            int at = full.IndexOf(find, from, StringComparison.Ordinal);

            if (at < 0) return count;

            Splice(texts, at, find.Length, replacement);

            count++;
            from = at + replacement.Length;
        }

        return count;
    }

    /// <summary>
    /// Заменяет кусок склеенного текста, разложенного по фрагментам.
    /// </summary>
    /// <remarks>
    /// Начало совпадения получает замену целиком, из остальных задетых фрагментов вырезается их
    /// часть совпадения. Так замена наследует оформление места, где стояло исходное слово.
    /// </remarks>
    private static void Splice(IReadOnlyList<Text> texts, int at, int length, string replacement)
    {
        int offset = 0;
        int end = at + length;
        bool placed = false;

        foreach (Text text in texts)
        {
            string value = text.Text;
            int start = offset;
            int stop = offset + value.Length;

            offset = stop;

            if (stop <= at || start >= end)
            {
                if (start >= end) break;

                continue;
            }

            int cutFrom = Math.Max(at, start) - start;
            int cutTo = Math.Min(end, stop) - start;
            string head = value[..cutFrom];
            string tail = value[cutTo..];

            text.Text = placed ? head + tail : head + replacement + tail;
            text.Space = SpaceProcessingModeValues.Preserve;
            placed = true;
        }
    }
}

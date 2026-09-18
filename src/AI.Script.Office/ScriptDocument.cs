using AI.Script.Runtime;
using System.Text;

namespace AI.Script.Office;

/// <summary>Вид блока документа.</summary>
public enum DocBlockKind
{
    /// <summary>Заголовок раздела.</summary>
    Heading,

    /// <summary>Абзац.</summary>
    Paragraph,

    /// <summary>Пункт списка.</summary>
    ListItem,

    /// <summary>Таблица.</summary>
    Table,

    /// <summary>Рисунок.</summary>
    Image,
}

/// <summary>Блок документа: строка дерева, у которой есть адрес.</summary>
/// <param name="Kind">Вид блока.</param>
/// <param name="Text">Текст блока; у таблицы — её текстовая свёртка, у рисунка — подпись.</param>
/// <param name="Level">Уровень заголовка; 0 для остальных блоков.</param>
/// <param name="Page">Страница, если формат её знает; 0 — не знает.</param>
/// <param name="Table">Таблица, если это таблица.</param>
/// <param name="Image">Картинка PNG у собранного документа; у прочитанного — <c>null</c>.</param>
/// <param name="Address">
/// Где блок в источнике, если это не страница: время в записи разговора, кадр ролика, область
/// скана. <c>null</c> — адресом служат номер блока и страница.
/// </param>
/// <param name="Caption">
/// Подпись над таблицей собранного документа; <c>null</c> — подписи нет. Отдельно от текста: у
/// прочитанной таблицы текст это ее строки через « | », и при записи он не должен становиться подписью.
/// </param>
public sealed record DocBlock(
    DocBlockKind Kind, string Text, int Level = 0, int Page = 0, ScriptTable? Table = null, byte[]? Image = null,
    string? Address = null, string? Caption = null);

/// <summary>
/// Документ: дерево блоков, одинаковое для docx, pdf, md, html и txt.
/// </summary>
/// <remarks>
/// Одна модель на все форматы — ради того, чтобы скрипт не спрашивал, откуда документ пришёл.
/// <c>doc.tables(d)</c> работает и над книгой Word, и над разметкой; счёт знаков не зависит от
/// того, кто эти знаки записал.
/// <para>
/// Чего формат не знает, того в модели нет, и это видно: у PDF нет уровней заголовков, у
/// разметки нет страниц. Достраивать недостающее догадками значило бы отдавать скрипту
/// выдуманную структуру, на которую он будет опираться как на настоящую.
/// </para>
/// </remarks>
public sealed class ScriptDocument
{
    private readonly Lazy<string> _text;

    /// <summary>Тип-тег дескриптора в языке.</summary>
    public const string TypeName = "doc.document";

    /// <summary>Создаёт документ.</summary>
    /// <param name="name">Имя файла, из которого он прочитан.</param>
    /// <param name="format">Формат: <c>docx</c>, <c>pdf</c>, <c>md</c>, <c>html</c>, <c>txt</c>.</param>
    /// <param name="blocks">Блоки в порядке чтения.</param>
    /// <param name="pageCount">Число страниц; 0 — формат страниц не знает.</param>
    /// <param name="source">Исходный файл docx; <c>null</c> — документ собран либо прочитан не из Word.</param>
    public ScriptDocument(
        string name, string format, IReadOnlyList<DocBlock> blocks, int pageCount = 0, byte[]? source = null)
    {
        Name = name;
        Format = format;
        Blocks = blocks;
        PageCount = pageCount;
        Source = source;
        _text = new Lazy<string>(() => Join(blocks));
    }

    /// <summary>
    /// Исходный файл Word, если документ из него прочитан.
    /// </summary>
    /// <remarks>
    /// Правка документа Word обязана сохранить его оформление: шрифты, стили, поля, колонтитулы.
    /// Модель из блоков этого не хранит и хранить не должна, поэтому правится сам файл, а блоки
    /// перечитываются из результата.
    /// </remarks>
    public byte[]? Source { get; }

    /// <summary>Имя файла.</summary>
    public string Name { get; }

    /// <summary>Формат, из которого документ прочитан.</summary>
    public string Format { get; }

    /// <summary>Блоки в порядке чтения.</summary>
    public IReadOnlyList<DocBlock> Blocks { get; }

    /// <summary>Число страниц; 0 — формат страниц не знает.</summary>
    public int PageCount { get; }

    /// <summary>Весь текст документа.</summary>
    public string Text => _text.Value;

    /// <summary>
    /// Номер раздела для каждого блока: 0 — до первого заголовка.
    /// </summary>
    /// <remarks>
    /// Номер ближайшего раздела: блок из «2.1» получает номер «2.1», а не «2.». Что раздел
    /// включает свои подразделы, учитывает <see cref="SectionText"/>.
    /// </remarks>
    public IReadOnlyList<int> Sections()
    {
        var sections = new int[Blocks.Count];
        var open = new Stack<(int Level, int Section)>();
        int number = 0;

        for (int i = 0; i < Blocks.Count; i++)
        {
            DocBlock block = Blocks[i];

            if (block.Kind == DocBlockKind.Heading)
            {
                while (open.Count > 0 && open.Peek().Level >= block.Level) _ = open.Pop();

                number++;
                open.Push((block.Level, number));
            }

            sections[i] = open.Count > 0 ? open.Peek().Section : 0;
        }

        return sections;
    }

    /// <summary>Текст одного раздела вместе с заголовком и подразделами; пустая строка — такого раздела нет.</summary>
    /// <remarks>
    /// Раздел тянется до следующего заголовка того же либо более высокого уровня: «2. Обязанности»
    /// включает «2.1», а «3.» уже нет. Раздел 0 это текст до первого заголовка.
    /// </remarks>
    public string SectionText(int section) => Join(SectionBlocks(section));

    /// <summary>Блоки раздела вместе с заголовком и подразделами; пусто — такого раздела нет.</summary>
    public IReadOnlyList<DocBlock> SectionBlocks(int section)
    {
        var blocks = new List<DocBlock>();
        int number = 0;
        int level = 0;
        bool inside = section == 0;

        foreach (DocBlock block in Blocks)
        {
            if (block.Kind == DocBlockKind.Heading)
            {
                number++;

                if (inside && (section == 0 || block.Level <= level)) break;

                if (number == section)
                {
                    inside = true;
                    level = block.Level;
                }
            }

            if (inside) blocks.Add(block);
        }

        return blocks;
    }

    /// <summary>Сколько разделов в документе.</summary>
    public int SectionCount()
    {
        int count = 0;

        foreach (DocBlock block in Blocks)
        {
            if (block.Kind == DocBlockKind.Heading) count++;
        }

        return count;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{Name}: блоков {Blocks.Count}, разделов {SectionCount()}" + (PageCount > 0 ? $", страниц {PageCount}" : string.Empty);

    private static string Join(IReadOnlyList<DocBlock> blocks)
    {
        var builder = new StringBuilder();

        foreach (DocBlock block in blocks)
        {
            if (block.Text.Length == 0) continue;

            if (builder.Length > 0) _ = builder.Append('\n');

            _ = builder.Append(block.Text);
        }

        return builder.ToString();
    }
}

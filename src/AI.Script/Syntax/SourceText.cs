namespace AI.Script.Syntax;

/// <summary>Позиция в тексте для человека: строка и колонка, обе с единицы.</summary>
public readonly struct LinePosition
{
    /// <summary>Номер строки, начиная с 1.</summary>
    public int Line { get; }

    /// <summary>Номер колонки, начиная с 1.</summary>
    public int Column { get; }

    /// <summary>Создаёт позицию.</summary>
    public LinePosition(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Line}:{Column}";
}

/// <summary>
/// Исходный текст скрипта вместе с картой строк.
/// </summary>
public sealed class SourceText
{
    private readonly int[] _lineStarts;

    /// <summary>Имя файла; попадает в диагностику.</summary>
    public string FileName { get; }

    /// <summary>Текст скрипта целиком.</summary>
    public string Text { get; }

    /// <summary>Длина текста в символах.</summary>
    public int Length => Text.Length;

    /// <summary>Число строк.</summary>
    public int LineCount => _lineStarts.Length;

    /// <summary>Символ по смещению.</summary>
    public char this[int index] => Text[index];

    /// <summary>Создаёт исходный текст.</summary>
    /// <param name="text">Содержимое скрипта.</param>
    /// <param name="fileName">Имя файла для диагностики.</param>
    public SourceText(string text, string fileName = "script.ais")
    {
        Text = text ?? string.Empty;
        FileName = string.IsNullOrWhiteSpace(fileName) ? "script.ais" : fileName;
        _lineStarts = BuildLineStarts(Text);
    }

    /// <summary>Переводит смещение в строку и колонку (обе с единицы).</summary>
    public LinePosition GetLinePosition(int offset)
    {
        if (offset < 0) offset = 0;
        if (offset > Length) offset = Length;

        int low = 0, high = _lineStarts.Length - 1, line = 0;

        while (low <= high)
        {
            int middle = (low + high) / 2;

            if (_lineStarts[middle] <= offset)
            {
                line = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return new LinePosition(line + 1, offset - _lineStarts[line] + 1);
    }

    /// <summary>Текст строки без завершающего перевода; номер строки с единицы.</summary>
    public string GetLineText(int line)
    {
        if (line < 1 || line > _lineStarts.Length) return string.Empty;

        int start = _lineStarts[line - 1];
        int end = line < _lineStarts.Length ? _lineStarts[line] : Length;

        while (end > start && (Text[end - 1] == '\n' || Text[end - 1] == '\r')) end--;

        return Text.Substring(start, end - start);
    }

    /// <summary>Текст отрезка.</summary>
    public string GetText(TextSpan span)
    {
        int start = Math.Clamp(span.Start, 0, Length);
        int end = Math.Clamp(span.End, start, Length);
        return Text.Substring(start, end - start);
    }

    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') starts.Add(i + 1);
        }

        return [.. starts];
    }
}

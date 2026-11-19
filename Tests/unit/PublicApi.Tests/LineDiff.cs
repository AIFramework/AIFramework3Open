using System.Text;

namespace AIFramework.PublicApi.Tests;

/// <summary>
/// Построчная разница двух текстов в виде, близком к unified diff:
/// «-» — строка есть только в снимке, «+» — только в коде, рядом по три строки контекста.
/// </summary>
internal static class LineDiff
{
    private const int ContextLines = 3;
    private const int MaxShownLines = 400;

    // Предел таблицы LCS (ячеек int): 4 млн — около 16 МБ. Больше бывает, только если
    // переписана почти вся сборка; тогда середина выводится целиком: сначала «-», потом «+»
    private const long MaxLcsCells = 4_000_000;

    /// <summary>
    /// Строит разницу между снимком и фактическим текстом.
    /// </summary>
    /// <param name="expected">Текст снимка.</param>
    /// <param name="actual">Фактический текст.</param>
    /// <returns>Текст разницы; пустая строка, если тексты совпадают.</returns>
    public static string Format(string expected, string actual)
    {
        string[] oldLines = expected.Split('\n');
        string[] newLines = actual.Split('\n');
        List<Edit> edits = Compare(oldLines, newLines);

        return edits.Any(edit => edit.Kind != ' ') ? Render(edits) : string.Empty;
    }

    private static List<Edit> Compare(string[] oldLines, string[] newLines)
    {
        int prefix = 0;

        while (prefix < oldLines.Length && prefix < newLines.Length && oldLines[prefix] == newLines[prefix])
            prefix++;

        int suffix = 0;

        while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
               && oldLines[oldLines.Length - 1 - suffix] == newLines[newLines.Length - 1 - suffix])
        {
            suffix++;
        }

        var edits = new List<Edit>();

        for (int i = 0; i < prefix; i++)
            edits.Add(new Edit(' ', i, i, oldLines[i]));

        CompareMiddle(oldLines, newLines, prefix, oldLines.Length - suffix, newLines.Length - suffix, edits);

        for (int k = suffix; k > 0; k--)
            edits.Add(new Edit(' ', oldLines.Length - k, newLines.Length - k, oldLines[oldLines.Length - k]));

        return edits;
    }

    private static void CompareMiddle(string[] oldLines, string[] newLines, int start, int oldEnd, int newEnd, List<Edit> edits)
    {
        int n = oldEnd - start;
        int m = newEnd - start;

        if ((long)(n + 1) * (m + 1) > MaxLcsCells)
        {
            for (int i = start; i < oldEnd; i++)
                edits.Add(new Edit('-', i, -1, oldLines[i]));

            for (int j = start; j < newEnd; j++)
                edits.Add(new Edit('+', -1, j, newLines[j]));

            return;
        }

        // lcs[i, j] — длина общей подпоследовательности хвостов, начиная с oldLines[start + i] и newLines[start + j]
        var lcs = new int[n + 1, m + 1];

        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = oldLines[start + i] == newLines[start + j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        int a = 0;
        int b = 0;

        while (a < n || b < m)
        {
            if (a < n && b < m && oldLines[start + a] == newLines[start + b])
            {
                edits.Add(new Edit(' ', start + a, start + b, oldLines[start + a]));
                a++;
                b++;
            }
            else if (a < n && (b == m || lcs[a + 1, b] >= lcs[a, b + 1]))
            {
                // При равенстве сначала удаление: «-» старой строки идёт перед «+» новой
                edits.Add(new Edit('-', start + a, -1, oldLines[start + a]));
                a++;
            }
            else
            {
                edits.Add(new Edit('+', -1, start + b, newLines[start + b]));
                b++;
            }
        }
    }

    private static string Render(List<Edit> edits)
    {
        // Показываем изменённые строки и контекст вокруг них; несмежные участки разделяются заголовком
        var shown = new bool[edits.Count];

        for (int i = 0; i < edits.Count; i++)
        {
            if (edits[i].Kind == ' ')
                continue;

            for (int k = Math.Max(0, i - ContextLines); k <= Math.Min(edits.Count - 1, i + ContextLines); k++)
                shown[k] = true;
        }

        var text = new StringBuilder();
        int written = 0;
        int hidden = 0;

        for (int i = 0; i < edits.Count; i++)
        {
            if (!shown[i])
                continue;

            if (written >= MaxShownLines)
            {
                if (edits[i].Kind != ' ')
                    hidden++;

                continue;
            }

            if (i == 0 || !shown[i - 1])
                text.AppendLine($"@@ снимок: строка {LineOf(edits, i, old: true)}, код: строка {LineOf(edits, i, old: false)} @@");

            text.Append(edits[i].Kind).Append(' ').AppendLine(edits[i].Text);
            written++;
        }

        if (hidden > 0)
            text.AppendLine($"… и ещё {hidden} изменённых строк не показано.");

        return text.ToString();
    }

    // Номер строки (с единицы) в снимке или в коде для заголовка участка: у строки «+» нет номера
    // в снимке, у «-» — в коде, тогда берётся ближайшая следующая строка, где он есть
    private static int LineOf(List<Edit> edits, int index, bool old)
    {
        for (int i = index; i < edits.Count; i++)
        {
            int line = old ? edits[i].OldIndex : edits[i].NewIndex;

            if (line >= 0)
                return line + 1;
        }

        return (old ? edits.Count(edit => edit.OldIndex >= 0) : edits.Count(edit => edit.NewIndex >= 0)) + 1;
    }

    private readonly record struct Edit(char Kind, int OldIndex, int NewIndex, string Text);
}

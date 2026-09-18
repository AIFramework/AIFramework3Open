using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Std;

namespace AI.Script.Office;

/// <summary>
/// Сравнение двух версий документа по блокам.
/// </summary>
/// <remarks>
/// «Что изменилось в новой редакции» — вопрос к ДОКУМЕНТУ, а не к тексту: человека интересует,
/// какой пункт переписали, а не какие символы сдвинулись. Поэтому сравниваются блоки, а
/// переписанный блок отличается от пары «удалили и добавили» — у него есть мера сходства со
/// старой редакцией.
/// <para>
/// Совпавшие блоки в список не попадают: отчёт о правках, где девяносто строк из ста —
/// «не изменилось», читать невозможно, а именно его и приходится читать, когда сравнение
/// возвращает весь документ.
/// </para>
/// </remarks>
public static partial class DocModule
{
    /// <summary>Сходство, ниже которого блоки считаются разными, а не переписанными.</summary>
    public const double ChangedFrom = 0.4;

    [ScriptFn("diff", "Изменения между версиями: добавлено, удалено, переписано",
        Example = "doc.diff(старый, новый) |> table.filter(row => row.kind == \"changed\")",
        Columns = "kind,old_block,new_block,similarity,text")]
    [ScriptMethod(ScriptDocument.TypeName)]
    public static ScriptTable Diff(
        IScriptContext context,
        [ScriptParam("прежняя версия")] ScriptDocument a,
        [ScriptParam("новая версия")] ScriptDocument b,
        [ScriptParam("сходство, при котором блок считается переписанным, а не заменённым")] double changed = ChangedFrom)
    {
        // Таблица сопоставления квадратична по числу блоков: два длинных PDF это сотни мегабайт, и
        // потолок памяти прогона должен сработать до выделения, а не после.
        context.CountAllocation((long)(a.Blocks.Count + 1) * (b.Blocks.Count + 1));

        var removed = new List<int>();
        var added = new List<int>();

        foreach ((int old, int fresh) in Align(a.Blocks, b.Blocks))
        {
            if (old >= 0 && fresh >= 0) continue;
            if (old >= 0) removed.Add(old);
            else added.Add(fresh);
        }

        var rows = new List<Change>();
        var paired = new HashSet<int>();
        var rewritten = new HashSet<int>();

        foreach (int old in removed)
        {
            int best = -1;
            double score = changed;

            foreach (int fresh in added)
            {
                if (paired.Contains(fresh)) continue;

                double sim = NlpModule.Similarity(a.Blocks[old].Text, b.Blocks[fresh].Text);

                if (sim > score) { score = sim; best = fresh; }
            }

            if (best < 0) continue;

            _ = paired.Add(best);
            _ = rewritten.Add(old);

            rows.Add(new Change("changed", old, best, score, b.Blocks[best].Text));
        }

        foreach (int old in removed)
        {
            if (!rewritten.Contains(old)) rows.Add(new Change("removed", old, -1, 0, a.Blocks[old].Text));
        }

        foreach (int fresh in added)
        {
            if (!paired.Contains(fresh)) rows.Add(new Change("added", -1, fresh, 0, b.Blocks[fresh].Text));
        }

        // Порядок — по новой версии: отчёт читают, открыв рядом новый документ, и удалённое
        // должно стоять там, где оно стояло, а не в конце списка.
        rows.Sort((left, right) => Place(left).CompareTo(Place(right)));

        context.CountAllocation(rows.Count);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("kind", [.. rows.Select(row => ScriptValue.Str(row.Kind))]),
            ScriptColumn.Own("old_block", [.. rows.Select(row => Index(row.Old))]),
            ScriptColumn.Own("new_block", [.. rows.Select(row => Index(row.New))]),
            ScriptColumn.Own("similarity", [.. rows.Select(row => ScriptValue.Num(row.Similarity))]),
            ScriptColumn.Own("text", [.. rows.Select(row => ScriptValue.Str(Shorten(row.Text)))]),
        ]);
    }

    /// <summary>
    /// Сопоставление блоков двух версий по наибольшей общей подпоследовательности.
    /// </summary>
    /// <remarks>
    /// Именно подпоследовательность, а не сравнение по номерам: вставленный в начале абзац
    /// сдвигает весь документ, и пономерное сравнение объявило бы изменённым каждый блок.
    /// </remarks>
    private static IReadOnlyList<(int Old, int New)> Align(IReadOnlyList<DocBlock> a, IReadOnlyList<DocBlock> b)
    {
        int n = a.Count;
        int m = b.Count;
        var length = new int[n + 1, m + 1];

        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                length[i, j] = Same(a[i], b[j])
                    ? length[i + 1, j + 1] + 1
                    : Math.Max(length[i + 1, j], length[i, j + 1]);
            }
        }

        var pairs = new List<(int, int)>(Math.Max(n, m));
        int x = 0;
        int y = 0;

        while (x < n && y < m)
        {
            if (Same(a[x], b[y])) { pairs.Add((x, y)); x++; y++; }
            else if (length[x + 1, y] >= length[x, y + 1]) { pairs.Add((x, -1)); x++; }
            else { pairs.Add((-1, y)); y++; }
        }

        while (x < n) pairs.Add((x++, -1));
        while (y < m) pairs.Add((-1, y++));

        return pairs;
    }

    /// <summary>Место строки в отчёте: номер блока новой версии, а у удалённого — старой.</summary>
    private static int Place(Change row) => row.New >= 0 ? row.New : row.Old;

    private static ScriptValue Index(int block) => block < 0 ? ScriptValue.None : ScriptValue.Num(block);

    /// <summary>Считаются ли блоки одинаковыми: вид и текст без краевых пробелов.</summary>
    private static bool Same(DocBlock a, DocBlock b) =>
        a.Kind == b.Kind && string.Equals(a.Text.Trim(), b.Text.Trim(), StringComparison.Ordinal);

    /// <summary>Одна строка отчёта о правках.</summary>
    private readonly record struct Change(string Kind, int Old, int New, double Similarity, string Text);
}

using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Развороты и окна: сводная таблица, длинный вид, скользящие и накопленные итоги.
/// </summary>
/// <remarks>
/// «Выручка по месяцам в разрезе городов» и «рост к прошлому месяцу» — два самых частых
/// вопроса к таблице, и до сих пор оба писались вручную циклом по строкам. Группировка их не
/// закрывает: она сворачивает строки, а здесь нужно развернуть их в колонки и посмотреть на
/// соседнюю строку.
/// <para>
/// Точные числа остаются точными: сумма денег по кварталу считается десятичной арифметикой,
/// а не переводится в двоичную ради общего кода.
/// </para>
/// </remarks>
public static partial class TableModule
{
    [ScriptFn("pivot", "Сводная таблица: строки, колонки и значение в пересечении",
        Example = "t |> table.pivot(rows: \"город\", cols: \"месяц\", value: \"сумма\")")]
    public static ScriptTable Pivot(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонка, чьи значения станут строками")] string rows,
        [ScriptParam("колонка, чьи значения станут колонками")] string cols,
        [ScriptParam("колонка со значениями")] string value,
        [ScriptParam("свёртка: \"sum\", \"mean\", \"count\", \"min\", \"max\", \"first\"")] string kind = "sum")
    {
        ScriptColumn rowKeys = t.Column(rows);
        ScriptColumn colKeys = t.Column(cols);
        ScriptColumn values = t.Column(value);

        var rowOrder = new List<ScriptValue>();
        var colOrder = new List<ScriptValue>();
        var rowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var colIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var cells = new Dictionary<(int Row, int Column), List<ScriptValue>>();

        for (int i = 0; i < t.RowCount; i++)
        {
            int row = Slot(rowIndex, rowOrder, rowKeys[i]);
            int column = Slot(colIndex, colOrder, colKeys[i]);

            if (!cells.TryGetValue((row, column), out List<ScriptValue>? bucket))
            {
                bucket = [];
                cells[(row, column)] = bucket;
            }

            bucket.Add(values[i]);
        }

        var built = new List<ScriptColumn>(colOrder.Count + 1)
        {
            ScriptColumn.Own(rows, [.. rowOrder]),
        };

        for (int column = 0; column < colOrder.Count; column++)
        {
            var cell = new ScriptValue[rowOrder.Count];

            for (int row = 0; row < rowOrder.Count; row++)
            {
                cell[row] = cells.TryGetValue((row, column), out List<ScriptValue>? bucket)
                    ? Aggregate(kind, bucket, "table.pivot")
                    : ScriptValue.None;
            }

            built.Add(ScriptColumn.Own(ScriptFormatter.Format(colOrder[column]), cell));
        }

        return ScriptTable.Create(built);
    }

    /// <summary>
    /// Разворачивает широкие колонки в пары «показатель, значение».
    /// </summary>
    /// <remarks>
    /// Обратная сводной: отчёт приходит широким (колонка на месяц), а группировать, фильтровать
    /// и строить графики удобно по длинному виду.
    /// </remarks>
    [ScriptFn("melt", "Разворачивает колонки в пары «показатель, значение»",
        Example = "t |> table.melt(keep: [\"город\"])")]
    public static ScriptTable Melt(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонки, которые остаются как есть")] string[] keep,
        [ScriptParam("имя колонки с названием показателя")] string name = "variable",
        [ScriptParam("имя колонки со значением")] string value = "value")
    {
        keep = [.. keep.Distinct(StringComparer.Ordinal)];

        var kept = new List<ScriptColumn>(keep.Length);

        foreach (string column in keep) kept.Add(t.Column(column));

        var melted = new List<ScriptColumn>(Math.Max(0, t.ColumnCount - keep.Length));

        foreach (ScriptColumn column in t.Columns)
        {
            if (Array.IndexOf(keep, column.Name) < 0) melted.Add(column);
        }

        if (melted.Count == 0)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "table.melt: разворачивать нечего — все колонки оставлены в keep");
        }

        int height = t.RowCount * melted.Count;
        var built = new List<ScriptColumn>(kept.Count + 2);

        foreach (ScriptColumn column in kept)
        {
            var repeated = new ScriptValue[height];

            for (int i = 0, k = 0; i < t.RowCount; i++)
            {
                for (int j = 0; j < melted.Count; j++) repeated[k++] = column[i];
            }

            built.Add(ScriptColumn.Own(column.Name, repeated));
        }

        var names = new ScriptValue[height];
        var values = new ScriptValue[height];

        for (int i = 0, k = 0; i < t.RowCount; i++)
        {
            foreach (ScriptColumn column in melted)
            {
                names[k] = ScriptValue.Str(column.Name);
                values[k] = column[i];
                k++;
            }
        }

        built.Add(ScriptColumn.Own(name, names));
        built.Add(ScriptColumn.Own(value, values));

        return ScriptTable.Create(built);
    }

    [ScriptFn("rolling", "Скользящее окно по колонке: среднее, сумма, минимум, максимум",
        Example = "t |> table.rolling(by: \"выручка\", window: 3)")]
    public static ScriptTable Rolling(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонка")] string by,
        [ScriptParam("ширина окна в строках")] int window,
        [ScriptParam("свёртка окна: \"mean\", \"sum\", \"min\", \"max\"")] string kind = "mean",
        [ScriptParam("имя новой колонки; пусто — от имени исходной")] string to = "")
    {
        if (window < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "table.rolling: окно должно быть положительным");

        ScriptColumn source = t.Column(by);
        var values = new ScriptValue[t.RowCount];
        var bucket = new List<ScriptValue>(window);

        for (int i = 0; i < t.RowCount; i++)
        {
            if (i + 1 < window)
            {
                // Окно ещё не набралось: пропуск, а не частичный итог. Среднее по двум точкам
                // в колонке «среднее по трём» — это другое число, и отличить его потом нечем.
                values[i] = ScriptValue.None;
                continue;
            }

            bucket.Clear();

            for (int j = i + 1 - window; j <= i; j++) bucket.Add(source[j]);

            values[i] = Aggregate(kind, bucket, "table.rolling");
        }

        return t.With(ScriptColumn.Own(Named(to, by, kind), values));
    }

    [ScriptFn("cum", "Накопленный итог по колонке", Example = "t |> table.cum(by: \"выручка\")")]
    public static ScriptTable Cumulative(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонка")] string by,
        [ScriptParam("свёртка: \"sum\", \"min\", \"max\"")] string kind = "sum",
        [ScriptParam("имя новой колонки; пусто — от имени исходной")] string to = "")
    {
        if (kind is not ("sum" or "min" or "max")) throw UnknownKind(kind, "table.cum");

        ScriptColumn source = t.Column(by);
        var values = new ScriptValue[t.RowCount];
        ScriptValue running = ScriptValue.None;

        // Накопление за один проход: свертка префикса заново на каждой строке давала квадрат по
        // числу строк, и таблица в сорок тысяч строк считалась минутами.
        for (int i = 0; i < t.RowCount; i++)
        {
            ScriptValue item = source[i];

            if (!IsMissing(item))
                running = Aggregate(kind, running.IsNone ? [item] : [running, item], "table.cum");

            values[i] = running;
        }

        return t.With(ScriptColumn.Own(Named(to, by, "cum"), values));
    }

    [ScriptFn("rank", "Ранг строки по колонке: 1 — наибольшее значение",
        Example = "t |> table.rank(by: \"выручка\")")]
    public static ScriptTable Rank(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонка")] string by,
        [ScriptParam("считать ранги от наибольшего")] bool desc = true,
        [ScriptParam("имя новой колонки; пусто — от имени исходной")] string to = "")
    {
        ScriptColumn source = t.Column(by);
        var order = new List<int>(t.RowCount);

        for (int i = 0; i < t.RowCount; i++)
        {
            if (!IsMissing(source[i])) order.Add(i);
        }

        order.Sort((left, right) => desc ? Compare(source[right], source[left]) : Compare(source[left], source[right]));

        var values = new ScriptValue[t.RowCount];

        for (int i = 0; i < t.RowCount; i++) values[i] = ScriptValue.None;

        for (int place = 0; place < order.Count; place++) values[order[place]] = ScriptValue.Num(place + 1);

        return t.With(ScriptColumn.Own(Named(to, by, "rank"), values));
    }

    [ScriptFn("lag", "Значение колонки со сдвигом: прошлый период рядом с текущим",
        Example = "t |> table.lag(by: \"выручка\", offset: 1)")]
    public static ScriptTable Lag(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонка")] string by,
        [ScriptParam("на сколько строк назад; отрицательное — вперёд")] int offset = 1,
        [ScriptParam("имя новой колонки; пусто — от имени исходной")] string to = "")
    {
        ScriptColumn source = t.Column(by);
        var values = new ScriptValue[t.RowCount];

        for (int i = 0; i < t.RowCount; i++)
        {
            int from = i - offset;

            values[i] = from >= 0 && from < t.RowCount ? source[from] : ScriptValue.None;
        }

        return t.With(ScriptColumn.Own(Named(to, by, "lag"), values));
    }

    /// <summary>
    /// Свёртка набора значений.
    /// </summary>
    /// <remarks>
    /// Точность держится за данными: пока все значения точные, счёт идёт десятичной
    /// арифметикой, и сумма денег по кварталу сходится с книгой. Смешанный набор считается
    /// двоично — точность уже потеряна не здесь.
    /// </remarks>
    private static ScriptValue Aggregate(string kind, IReadOnlyList<ScriptValue> values, string what)
    {
        var present = new List<ScriptValue>(values.Count);

        foreach (ScriptValue value in values)
        {
            if (!IsMissing(value)) present.Add(value);
        }

        // Свертка проверяется до данных: иначе неизвестная свертка над одним значением молча
        // возвращала само значение.
        if (kind is not ("sum" or "mean" or "min" or "max" or "count" or "first")) throw UnknownKind(kind, what);

        if (string.Equals(kind, "count", StringComparison.Ordinal)) return ScriptValue.Num(present.Count);
        if (present.Count == 0) return ScriptValue.None;
        if (string.Equals(kind, "first", StringComparison.Ordinal)) return present[0];

        bool exact = true;

        foreach (ScriptValue value in present)
        {
            if (value.Type != ScriptType.Dec) exact = false;
        }

        return exact ? ExactAggregate(kind, present, what) : NumericAggregate(kind, present, what);
    }

    private static ScriptValue ExactAggregate(string kind, IReadOnlyList<ScriptValue> values, string what)
    {
        decimal total = values[0].AsDecimal();

        for (int i = 1; i < values.Count; i++)
        {
            decimal item = values[i].AsDecimal();

            total = kind switch
            {
                "sum" or "mean" => total + item,
                "min" => Math.Min(total, item),
                "max" => Math.Max(total, item),
                _ => throw UnknownKind(kind, what),
            };
        }

        return ScriptValue.Dec(kind == "mean" ? total / values.Count : total);
    }

    private static ScriptValue NumericAggregate(string kind, IReadOnlyList<ScriptValue> values, string what)
    {
        double total = Numeric(values[0]);

        for (int i = 1; i < values.Count; i++)
        {
            double item = Numeric(values[i]);

            total = kind switch
            {
                "sum" or "mean" => total + item,
                "min" => Math.Min(total, item),
                "max" => Math.Max(total, item),
                _ => throw UnknownKind(kind, what),
            };
        }

        return ScriptValue.Num(kind == "mean" ? total / values.Count : total);
    }

    private static ScriptError UnknownKind(string kind, string what) =>
        new(DiagnosticCodes.UnknownArgument,
            $"{what}: неизвестная свёртка «{kind}»",
            "известны: sum, mean, count, min, max, first");

    /// <summary>Место значения в порядке появления: одинаковые ключи попадают в одну строку.</summary>
    private static int Slot(Dictionary<string, int> index, List<ScriptValue> order, ScriptValue key)
    {
        string text = ScriptFormatter.Format(key, quoteStrings: false);

        if (index.TryGetValue(text, out int slot)) return slot;

        slot = order.Count;
        index[text] = slot;
        order.Add(key);

        return slot;
    }

    private static string Named(string to, string by, string suffix) =>
        string.IsNullOrWhiteSpace(to) ? $"{by}_{suffix}" : to;
}

using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>table</c>: колоночные таблицы.
/// </summary>
/// <remarks>
/// Преобразования не мутируют таблицу, а возвращают новую: конвейер
/// <c>t |&gt; table.filter(...) |&gt; table.select(...)</c> читается как последовательность
/// значений, а не как цепочка изменений одного объекта.
/// </remarks>
[ScriptModule("table", "Колоночные таблицы: выборка, фильтрация, группировка, соединение", Version = "0.1")]
public static class TableModule
{
    [ScriptFn("of", "Таблица из записи «имя колонки → значения»", Example = "table.of({ x: <1, 2>, y: [\"a\", \"b\"] })")]
    public static ScriptTable Of([ScriptParam("запись из колонок")] ScriptRecord cols)
    {
        var columns = new List<ScriptColumn>(cols.Count);

        foreach (var pair in cols.Pairs()) columns.Add(ToColumn(pair.Key, pair.Value));

        return ScriptTable.Create(columns);
    }

    [ScriptFn("from_matrix", "Таблица из матрицы", Example = "table.from_matrix(m, cols: [\"x\", \"y\"])")]
    public static ScriptTable FromMatrix(
        [ScriptParam("матрица")] Matrix m,
        [ScriptParam("имена колонок; по умолчанию c0, c1, …")] string[]? cols = null)
        => ScriptTable.FromMatrix(m, cols);

    [ScriptFn("to_matrix", "Матрица из числовых колонок", Example = "t |> table.to_matrix()")]
    public static Matrix ToMatrix([ScriptParam("таблица")] ScriptTable t) => t.ToMatrix();

    [ScriptFn("columns", "Имена колонок", Example = "table.columns(t)")]
    public static string[] Columns([ScriptParam("таблица")] ScriptTable t) => [.. t.Names()];

    [ScriptFn("column", "Колонка как вектор либо список", Example = "table.column(t, \"price\")")]
    public static ScriptValue Column(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имя колонки")] string name)
        => t.Column(name).AsValue();

    [ScriptFn("rows", "Строки таблицы записями", Example = "for row in table.rows(t) { }")]
    public static ScriptList Rows([ScriptParam("таблица")] ScriptTable t)
    {
        var rows = new ScriptValue[t.RowCount];

        for (int i = 0; i < t.RowCount; i++) rows[i] = ScriptValue.Record(t.Row(i));

        return ScriptList.Own(rows);
    }

    [ScriptFn("select", "Оставляет указанные колонки в указанном порядке", Example = "t |> table.select([\"x\", \"y\"])")]
    public static ScriptTable Select(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имена колонок")] string[] cols)
        => t.Select(cols);

    [ScriptFn("drop", "Убирает указанные колонки", Example = "t |> table.drop([\"id\"])")]
    public static ScriptTable Drop(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имена колонок")] string[] cols)
        => t.Without(new HashSet<string>(cols, StringComparer.Ordinal));

    [ScriptFn("rename", "Переименовывает колонку", Example = "t |> table.rename(from: \"x\", to: \"признак\")")]
    public static ScriptTable Rename(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("текущее имя")] string from,
        [ScriptParam("новое имя")] string to)
    {
        var columns = new List<ScriptColumn>(t.ColumnCount);
        _ = t.Column(from);

        foreach (ScriptColumn column in t.Columns)
        {
            columns.Add(string.Equals(column.Name, from, StringComparison.Ordinal) ? column.Renamed(to) : column);
        }

        return ScriptTable.Create(columns);
    }

    [ScriptFn("with", "Добавляет либо заменяет колонку готовыми значениями", Example = "t |> table.with(name: \"z\", values: <1, 2>)")]
    public static ScriptTable With(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имя колонки")] string name,
        [ScriptParam("значения")] ScriptValue values)
    {
        ScriptColumn column = ToColumn(name, values);

        if (t.ColumnCount > 0 && column.Count != t.RowCount)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"table.with: колонка '{name}' содержит {column.Count} значений, а в таблице {t.RowCount} строк");
        }

        return t.With(column);
    }

    [ScriptFn("head", "Первые n строк", Example = "t |> table.head(10)")]
    public static ScriptTable Head(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("сколько строк")] int n)
        => t.Take(Sequence(0, Math.Clamp(n, 0, t.RowCount)));

    [ScriptFn("tail", "Последние n строк", Example = "t |> table.tail(10)")]
    public static ScriptTable Tail(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("сколько строк")] int n)
    {
        int count = Math.Clamp(n, 0, t.RowCount);
        return t.Take(Sequence(t.RowCount - count, t.RowCount));
    }

    [ScriptFn("concat", "Склеивает таблицы по строкам", Example = "table.concat(a, b)")]
    public static ScriptTable Concat(
        [ScriptParam("первая таблица")] ScriptTable a,
        [ScriptParam("вторая таблица")] ScriptTable b)
    {
        if (a.ColumnCount == 0) return b;
        if (b.ColumnCount == 0) return a;

        var left = new HashSet<string>(a.Names(), StringComparer.Ordinal);

        if (!left.SetEquals(b.Names()))
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                "table.concat: наборы колонок различаются",
                $"слева: {string.Join(", ", a.Names())}\nсправа: {string.Join(", ", b.Names())}");
        }

        var columns = new List<ScriptColumn>(a.ColumnCount);

        foreach (ScriptColumn column in a.Columns)
        {
            var values = new List<ScriptValue>(a.RowCount + b.RowCount);

            values.AddRange(column.Values());
            values.AddRange(b.Column(column.Name).Values());

            columns.Add(ScriptColumn.From(column.Name, values));
        }

        return ScriptTable.Create(columns);
    }

    [ScriptFn("filter", "Оставляет строки, для которых предикат истинен", Example = "t |> table.filter(row => row.amount > 0)")]
    public static async Task<ScriptTable> Filter(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("предикат от строки")] ScriptCallable predicate)
    {
        ScriptValue callable = ScriptValue.Fn(predicate);
        var kept = new List<int>(t.RowCount);

        for (int i = 0; i < t.RowCount; i++)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            context.CountStep();

            ScriptValue verdict = await context
                .CallAsync(callable, ScriptValue.Record(t.Row(i)))
                .ConfigureAwait(false);

            if (verdict.AsBool("результат предиката table.filter")) kept.Add(i);
        }

        return t.Take(kept);
    }

    /// <summary>
    /// Добавляет вычисляемые колонки.
    /// </summary>
    /// <remarks>
    /// Имена колонок передаются записью, а не произвольными именованными аргументами
    /// (<c>table.derive(avg: row =&gt; …)</c>). Произвольные имена лишили бы проверку
    /// возможности ловить опечатки в аргументах, а это главная её работа.
    /// </remarks>
    [ScriptFn("derive", "Добавляет колонки, вычисляемые по строке",
        Example = "t |> table.derive(cols: { avg: row => row.sum / row.n })")]
    public static async Task<ScriptTable> Derive(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("запись «имя колонки → функция от строки»")] ScriptRecord cols)
    {
        ScriptTable result = t;

        foreach (var pair in cols.Pairs())
        {
            ScriptValue callable = pair.Value;

            if (callable.Type != ScriptType.Fn)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"table.derive: значение поля '{pair.Key}' имеет тип {callable.Type.ToName()}, а нужна функция",
                    "например: { avg: row => row.sum / row.n }");
            }

            var values = new ScriptValue[t.RowCount];

            for (int i = 0; i < t.RowCount; i++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                context.CountStep();

                values[i] = await context
                    .CallAsync(callable, ScriptValue.Record(result.Row(i)))
                    .ConfigureAwait(false);
            }

            result = result.With(ScriptColumn.Own(pair.Key, values));
        }

        return result;
    }

    [ScriptFn("sort", "Сортирует строки по колонке либо по функции",
        Example = "t |> table.sort(by: \"price\", desc: true)")]
    public static async Task<ScriptTable> Sort(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имя колонки либо функция от строки")] ScriptValue by,
        [ScriptParam("по убыванию")] bool desc = false)
    {
        var keys = new ScriptValue[t.RowCount];

        if (by.Type == ScriptType.Str)
        {
            ScriptColumn column = t.Column(by.AsString());

            for (int i = 0; i < t.RowCount; i++) keys[i] = column[i];
        }
        else
        {
            ScriptValue callable = by;

            if (callable.Type != ScriptType.Fn)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"table.sort: 'by' имеет тип {by.Type.ToName()}, а нужно имя колонки либо функция");
            }

            for (int i = 0; i < t.RowCount; i++)
            {
                keys[i] = await context
                    .CallAsync(callable, ScriptValue.Record(t.Row(i)))
                    .ConfigureAwait(false);
            }
        }

        int[] order = Sequence(0, t.RowCount);

        Array.Sort(order, (left, right) =>
        {
            int comparison = Compare(keys[left], keys[right]);
            return desc ? -comparison : comparison;
        });

        return t.Take(order);
    }

    [ScriptFn("distinct", "Убирает повторы строк", Example = "t |> table.distinct()")]
    public static ScriptTable Distinct(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонки, по которым сравнивать; по умолчанию все")] string[]? by = null)
    {
        IReadOnlyList<string> names = by ?? t.Names();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<int>(t.RowCount);

        for (int i = 0; i < t.RowCount; i++)
        {
            if (seen.Add(KeyOf(t, i, names))) kept.Add(i);
        }

        return t.Take(kept);
    }

    /// <summary>
    /// Группировка с агрегатами.
    /// </summary>
    /// <remarks>
    /// Агрегат — функция от подтаблицы группы, а не строка вида <c>"sum(amount)"</c>. Строки
    /// потребовали бы второго языка внутри первого: со своим разбором, своими ошибками и своим
    /// списком того, что в нём можно. Функция обходится тем, что в языке уже есть.
    /// </remarks>
    [ScriptFn("group_by", "Группирует строки и считает агрегаты",
        Example = "t |> table.group_by(\"client\", agg: { total: g => vec.sum(g[\"amount\"]) })")]
    public static async Task<ScriptTable> GroupBy(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имя колонки либо список имён")] ScriptValue by,
        [ScriptParam("запись «имя колонки → функция от подтаблицы группы»")] ScriptRecord agg)
    {
        IReadOnlyList<string> keys = ColumnNames(by, "table.group_by");

        foreach (string key in keys) _ = t.Column(key);

        var order = new List<string>();
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (int i = 0; i < t.RowCount; i++)
        {
            string key = KeyOf(t, i, keys);

            if (!groups.TryGetValue(key, out List<int>? rows))
            {
                rows = [];
                groups[key] = rows;
                order.Add(key);
            }

            rows.Add(i);
        }

        var columns = new List<ScriptColumn>(keys.Count + agg.Count);

        foreach (string key in keys)
        {
            var values = new ScriptValue[order.Count];

            for (int g = 0; g < order.Count; g++) values[g] = t.Column(key)[groups[order[g]][0]];

            columns.Add(ScriptColumn.Own(key, values));
        }

        foreach (var pair in agg.Pairs())
        {
            if (pair.Value.Type != ScriptType.Fn)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"table.group_by: агрегат '{pair.Key}' имеет тип {pair.Value.Type.ToName()}, а нужна функция",
                    "например: { total: g => vec.sum(g[\"amount\"]) }");
            }

            var values = new ScriptValue[order.Count];

            for (int g = 0; g < order.Count; g++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                context.CountStep();

                ScriptTable group = t.Take(groups[order[g]]);
                values[g] = await context.CallAsync(pair.Value, ScriptValue.Table(group)).ConfigureAwait(false);
            }

            columns.Add(ScriptColumn.Own(pair.Key, values));
        }

        return ScriptTable.Create(columns);
    }

    [ScriptFn("join", "Соединяет таблицы по колонке", Example = "table.join(a, b, on: \"id\")")]
    public static ScriptTable Join(
        [ScriptParam("левая таблица")] ScriptTable left,
        [ScriptParam("правая таблица")] ScriptTable right,
        [ScriptParam("имя колонки-ключа")] string on,
        [ScriptParam("вид соединения: inner либо left")] string how = "inner")
    {
        if (how is not ("inner" or "left"))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"table.join: неизвестный вид соединения '{how}'",
                "поддержаны 'inner' и 'left'");
        }

        ScriptColumn leftKey = left.Column(on);
        ScriptColumn rightKey = right.Column(on);

        var collisions = new List<string>();

        foreach (string name in right.Names())
        {
            if (!string.Equals(name, on, StringComparison.Ordinal) && left.TryGet(name, out _)) collisions.Add(name);
        }

        if (collisions.Count > 0)
        {
            throw new ScriptError(
                DiagnosticCodes.DuplicateArgument,
                $"table.join: колонки повторяются в обеих таблицах: {string.Join(", ", collisions)}",
                "переименуйте их заранее: table.rename(t, from: \"...\", to: \"...\")");
        }

        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = right.RowCount - 1; i >= 0; i--) index[Key(rightKey[i])] = i;

        var leftRows = new List<int>(left.RowCount);
        var rightRows = new List<int>(left.RowCount);

        for (int i = 0; i < left.RowCount; i++)
        {
            if (index.TryGetValue(Key(leftKey[i]), out int match))
            {
                leftRows.Add(i);
                rightRows.Add(match);
                continue;
            }

            if (how != "left") continue;

            leftRows.Add(i);
            rightRows.Add(-1);
        }

        var columns = new List<ScriptColumn>(left.ColumnCount + right.ColumnCount - 1);

        foreach (ScriptColumn column in left.Columns) columns.Add(column.Take(leftRows));

        foreach (ScriptColumn column in right.Columns)
        {
            if (string.Equals(column.Name, on, StringComparison.Ordinal)) continue;

            var values = new ScriptValue[rightRows.Count];

            for (int i = 0; i < rightRows.Count; i++)
                values[i] = rightRows[i] < 0 ? ScriptValue.None : column[rightRows[i]];

            columns.Add(ScriptColumn.Own(column.Name, values));
        }

        return ScriptTable.Create(columns);
    }

    /// <summary>
    /// Кодирует категориальные колонки индикаторами.
    /// </summary>
    /// <remarks>
    /// Категории сортируются, а не берутся в порядке появления: иначе набор колонок зависел бы
    /// от порядка строк, и модель, обученная на одной выборке, не приняла бы другую.
    /// </remarks>
    [ScriptFn("one_hot", "Заменяет категориальные колонки индикаторными", Example = "t |> table.one_hot([\"region\"])")]
    public static ScriptTable OneHot(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имена колонок")] string[] cols)
    {
        var targets = new HashSet<string>(cols, StringComparer.Ordinal);

        foreach (string name in cols) _ = t.Column(name);

        var columns = new List<ScriptColumn>(t.ColumnCount + (cols.Length * 4));

        foreach (ScriptColumn column in t.Columns)
        {
            if (!targets.Contains(column.Name))
            {
                columns.Add(column);
                continue;
            }

            var categories = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < column.Count; i++)
            {
                string label = Label(column[i]);
                if (known.Add(label)) categories.Add(label);
            }

            categories.Sort(StringComparer.Ordinal);

            foreach (string category in categories)
            {
                var indicator = new Vector(column.Count);

                for (int i = 0; i < column.Count; i++) indicator[i] = Label(column[i]) == category ? 1 : 0;

                columns.Add(ScriptColumn.FromVector($"{column.Name}={category}", indicator));
            }
        }

        return ScriptTable.Create(columns);
    }

    [ScriptFn("encode", "Заменяет категории целыми кодами", Example = "t |> table.encode([\"region\"])")]
    public static ScriptTable Encode(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("имена колонок")] string[] cols)
    {
        var targets = new HashSet<string>(cols, StringComparer.Ordinal);

        foreach (string name in cols) _ = t.Column(name);

        var columns = new List<ScriptColumn>(t.ColumnCount);

        foreach (ScriptColumn column in t.Columns)
        {
            if (!targets.Contains(column.Name))
            {
                columns.Add(column);
                continue;
            }

            var categories = new List<string>();

            for (int i = 0; i < column.Count; i++) categories.Add(Label(column[i]));

            var sorted = new List<string>(new HashSet<string>(categories, StringComparer.Ordinal));
            sorted.Sort(StringComparer.Ordinal);

            var codes = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count; i++) codes[sorted[i]] = i;

            var values = new Vector(column.Count);
            for (int i = 0; i < column.Count; i++) values[i] = codes[categories[i]];

            columns.Add(ScriptColumn.FromVector(column.Name, values));
        }

        return ScriptTable.Create(columns);
    }

    [ScriptFn("drop_na", "Убирает строки с пропусками", Example = "t |> table.drop_na()")]
    public static ScriptTable DropNa(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонки для проверки; по умолчанию все")] string[]? cols = null)
    {
        IReadOnlyList<string> names = cols ?? t.Names();

        foreach (string name in names) _ = t.Column(name);

        var kept = new List<int>(t.RowCount);

        for (int i = 0; i < t.RowCount; i++)
        {
            bool ok = true;

            foreach (string name in names)
            {
                if (!IsMissing(t.Column(name)[i])) continue;
                ok = false;
                break;
            }

            if (ok) kept.Add(i);
        }

        return t.Take(kept);
    }

    [ScriptFn("fill_na", "Заменяет пропуски значением", Example = "t |> table.fill_na(value: 0)")]
    public static ScriptTable FillNa(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("чем заменять")] ScriptValue value,
        [ScriptParam("колонки; по умолчанию все")] string[]? cols = null)
    {
        var targets = new HashSet<string>(cols ?? t.Names(), StringComparer.Ordinal);

        foreach (string name in targets) _ = t.Column(name);

        var columns = new List<ScriptColumn>(t.ColumnCount);

        foreach (ScriptColumn column in t.Columns)
        {
            if (!targets.Contains(column.Name))
            {
                columns.Add(column);
                continue;
            }

            var values = column.Copy();

            for (int i = 0; i < values.Length; i++)
            {
                if (IsMissing(values[i])) values[i] = value;
            }

            columns.Add(ScriptColumn.Own(column.Name, values));
        }

        return ScriptTable.Create(columns);
    }

    [ScriptFn("shuffle", "Перемешивает строки", Example = "t |> table.shuffle()")]
    public static ScriptTable Shuffle(IScriptContext context, [ScriptParam("таблица")] ScriptTable t) =>
        t.Take(Shuffled(context, t.RowCount));

    /// <summary>
    /// Делит таблицу на обучающую и тестовую части.
    /// </summary>
    /// <remarks>
    /// Перемешивание идёт от ГСЧ прогона, засеянного <c>options.seed</c>: разбиение обязано
    /// повторяться от запуска к запуску, иначе сравнивать метрики двух прогонов бессмысленно.
    /// </remarks>
    [ScriptFn("split", "Делит таблицу на train и test", Example = "let s = table.split(t, test: 0.25)")]
    public static ScriptRecord Split(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("доля тестовой части от 0 до 1")] double test = 0.25,
        [ScriptParam("перемешивать перед разбиением")] bool shuffle = true)
    {
        if (test is < 0 or > 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "table.split: доля должна лежать в [0, 1]");

        int[] order = shuffle ? Shuffled(context, t.RowCount) : Sequence(0, t.RowCount);
        int testCount = (int)Math.Round(t.RowCount * test);

        var testRows = new int[testCount];
        var trainRows = new int[t.RowCount - testCount];

        Array.Copy(order, 0, testRows, 0, testCount);
        Array.Copy(order, testCount, trainRows, 0, trainRows.Length);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("train", ScriptValue.Table(t.Take(trainRows))),
            new KeyValuePair<string, ScriptValue>("test", ScriptValue.Table(t.Take(testRows))),
        ]);
    }

    [ScriptFn("describe", "Сводка по колонкам: тип, пропуски, статистики", Example = "show table.describe(t)")]
    public static ScriptTable Describe([ScriptParam("таблица")] ScriptTable t)
    {
        int n = t.ColumnCount;
        var names = new ScriptValue[n];
        var types = new ScriptValue[n];
        var counts = new Vector(n);
        var missing = new Vector(n);
        var means = new Vector(n);
        var stds = new Vector(n);
        var mins = new Vector(n);
        var maxs = new Vector(n);

        for (int j = 0; j < n; j++)
        {
            ScriptColumn column = t[j];
            names[j] = ScriptValue.Str(column.Name);
            types[j] = ScriptValue.Str(column.Type.ToName());
            counts[j] = column.Count;

            int gaps = 0;
            for (int i = 0; i < column.Count; i++)
            {
                if (IsMissing(column[i])) gaps++;
            }

            missing[j] = gaps;

            if (column.Type != ScriptType.Num || column.Count == gaps)
            {
                means[j] = double.NaN;
                stds[j] = double.NaN;
                mins[j] = double.NaN;
                maxs[j] = double.NaN;
                continue;
            }

            var present = new List<double>(column.Count);

            for (int i = 0; i < column.Count; i++)
            {
                if (!IsMissing(column[i])) present.Add(column[i].RawNumber);
            }

            var values = new Vector(present);
            means[j] = values.Mean();
            stds[j] = values.Std();
            mins[j] = values.Min();
            maxs[j] = values.Max();
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("column", names),
            ScriptColumn.Own("type", types),
            ScriptColumn.FromVector("count", counts),
            ScriptColumn.FromVector("missing", missing),
            ScriptColumn.FromVector("mean", means),
            ScriptColumn.FromVector("std", stds),
            ScriptColumn.FromVector("min", mins),
            ScriptColumn.FromVector("max", maxs),
        ]);
    }

    // --- вспомогательное ---

    private static ScriptColumn ToColumn(string name, ScriptValue values) => values.Type switch
    {
        ScriptType.Vec => ScriptColumn.FromVector(name, values.AsVector()),
        ScriptType.List => ScriptColumn.Own(name, values.AsList().ToArray()),
        ScriptType.Range => ScriptColumn.FromVector(name, new Vector(values.AsRange().Values())),
        _ => throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"колонка '{name}': ожидался вектор либо список, получен {values.Type.ToName()}"),
    };

    private static IReadOnlyList<string> ColumnNames(ScriptValue value, string what)
    {
        if (value.Type == ScriptType.Str) return [value.AsString()];

        if (value.Type == ScriptType.List)
        {
            ScriptList list = value.AsList();
            var names = new string[list.Count];

            for (int i = 0; i < list.Count; i++) names[i] = list[i].AsString($"{what}: имя колонки {i}");

            return names;
        }

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: ожидалось имя колонки либо список имён, получен {value.Type.ToName()}");
    }

    private static string KeyOf(ScriptTable t, int row, IReadOnlyList<string> names)
    {
        if (names.Count == 1) return Key(t.Column(names[0])[row]);

        // Части склеиваются с указанием длины: без него ключи («a|b», «») и («a», «b|»)
        // совпали бы, и две разные группы слились бы в одну.
        var key = new System.Text.StringBuilder();

        foreach (string name in names)
        {
            string part = Key(t.Column(name)[row]);
            _ = key.Append(part.Length).Append(':').Append(part).Append('|');
        }

        return key.ToString();
    }

    /// <summary>
    /// Ключ значения для группировки и соединения.
    /// </summary>
    /// <remarks>
    /// Тип входит в ключ: иначе строка "1" и число 1 попали бы в одну группу, а пропуск — в
    /// одну группу со строкой «none».
    /// </remarks>
    private static string Key(ScriptValue value) =>
        $"{value.Type.ToName()}:{ScriptFormatter.Format(value, quoteStrings: false)}";

    /// <summary>
    /// Читаемая метка категории: идёт в имена колонок <c>one_hot</c>.
    /// </summary>
    /// <remarks>
    /// Без типа в отличие от <see cref="Key"/>: имя колонки читает человек, и <c>region=юг</c>
    /// понятнее, чем <c>region=str:юг</c>. Для категориальной колонки риска смешать «1» и 1
    /// практически нет, а цена ошибки — нечитаемая шапка у каждой модели.
    /// </remarks>
    private static string Label(ScriptValue value) =>
        value.IsNone ? "none" : ScriptFormatter.Format(value, quoteStrings: false);

    private static bool IsMissing(ScriptValue value) =>
        value.IsNone || (value.Type == ScriptType.Num && double.IsNaN(value.RawNumber));

    private static int[] Sequence(int start, int end)
    {
        var items = new int[Math.Max(0, end - start)];

        for (int i = 0; i < items.Length; i++) items[i] = start + i;

        return items;
    }

    private static int[] Shuffled(IScriptContext context, int count)
    {
        int[] order = Sequence(0, count);

        for (int i = count - 1; i > 0; i--)
        {
            int j = context.Random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }

    private static int Compare(ScriptValue left, ScriptValue right)
    {
        if (left.IsNone || right.IsNone) return left.IsNone && right.IsNone ? 0 : left.IsNone ? -1 : 1;

        if (left.Type != right.Type)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"сортировка требует однотипных ключей, встречены {left.Type.ToName()} и {right.Type.ToName()}");
        }

        return left.Type switch
        {
            ScriptType.Num or ScriptType.Bool => left.RawNumber.CompareTo(right.RawNumber),
            ScriptType.Str => string.CompareOrdinal(left.AsString(), right.AsString()),
            ScriptType.Date => left.AsDate().CompareTo(right.AsDate()),
            ScriptType.Dur => left.AsDuration().CompareTo(right.AsDuration()),
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"значения типа {left.Type.ToName()} не упорядочиваются"),
        };
    }
}

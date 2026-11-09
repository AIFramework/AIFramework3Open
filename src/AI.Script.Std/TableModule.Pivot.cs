using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Сводная таблица: поля строк, поля колонок, значение в пересечении и итоги.
/// </summary>
/// <remarks>
/// Свод считается за один проход без промежуточных списков: в каждой ячейке живет накопитель,
/// а не набор всех ее значений. Ключи сравниваются значениями, а не их текстом, поэтому
/// выгрузка в сотни тысяч строк не форматирует каждую ячейку ради поиска группы.
/// <para>
/// Итоги считаются своими накопителями по исходным строкам, а не сверткой уже свернутых
/// ячеек: среднее средних и количество количеств дали бы другое число, чем Excel.
/// </para>
/// </remarks>
public static partial class TableModule
{
    /// <summary>Подпись строки и колонки итогов.</summary>
    internal const string TotalLabel = "Итого";

    /// <summary>Разделитель частей имени колонки, когда полей колонок несколько.</summary>
    private const string PivotSeparator = " / ";

    [ScriptFn("pivot", "Сводная таблица: поля строк и колонок, значение в пересечении, итоги",
        Example = "t |> table.pivot(rows: \"город\", cols: \"месяц\", value: \"сумма\", totals: true)")]
    public static ScriptTable Pivot(
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("колонка либо список колонок, чьи значения станут строками")] ScriptValue rows,
        [ScriptParam("колонка либо список колонок, чьи значения станут колонками; [] — без разворота")] ScriptValue cols,
        [ScriptParam("колонка со значениями")] string value,
        [ScriptParam("свёртка: \"sum\", \"mean\", \"count\", \"min\", \"max\", \"first\"")] string kind = "sum",
        [ScriptParam("добавить строку и колонку «Итого»")] bool totals = false,
        [ScriptParam("упорядочить строки и колонки по ключам; иначе порядок появления")] bool sort = false)
    {
        if (!PivotCell.Known(kind)) throw UnknownKind(kind, "table.pivot");

        IReadOnlyList<string> rowNames = ColumnNames(rows, "table.pivot");
        // Пустое имя поля колонок значит «без разворота», как и пустой список.
        IReadOnlyList<string> colNames = [.. ColumnNames(cols, "table.pivot").Where(name => name.Length > 0)];

        if (rowNames.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "table.pivot: нужно хотя бы одно поле строк");

        PivotKeys rowKeys = PivotKeys.Of(t, rowNames);
        PivotKeys colKeys = PivotKeys.Of(t, colNames);
        ScriptColumn values = t.Column(value);

        var cells = new Dictionary<(int Row, int Column), PivotCell>();
        var rowTotals = new PivotCell[rowKeys.Count];
        var colTotals = new PivotCell[colKeys.Count];
        var grand = new PivotCell();

        for (int i = 0; i < t.RowCount; i++)
        {
            int row = rowKeys.Ids[i];
            int column = colKeys.Ids[i];

            if (!cells.TryGetValue((row, column), out PivotCell? cell))
            {
                cell = new PivotCell();
                cells[(row, column)] = cell;
            }

            ScriptValue item = values[i];

            cell.Add(item, kind);

            if (!totals) continue;

            (rowTotals[row] ??= new PivotCell()).Add(item, kind);
            (colTotals[column] ??= new PivotCell()).Add(item, kind);
            grand.Add(item, kind);
        }

        int[] rowOrder = rowKeys.Order(sort);
        int[] colOrder = colKeys.Order(sort);
        int height = rowOrder.Length + (totals ? 1 : 0);
        var built = new List<ScriptColumn>(rowNames.Count + colOrder.Length + 1);
        var taken = new HashSet<string>(StringComparer.Ordinal);

        for (int part = 0; part < rowNames.Count; part++)
        {
            var labels = new ScriptValue[height];

            for (int r = 0; r < rowOrder.Length; r++) labels[r] = rowKeys.Parts[rowOrder[r]][part];

            if (totals) labels[^1] = part == 0 ? ScriptValue.Str(TotalLabel) : ScriptValue.None;

            built.Add(ScriptColumn.Own(Unique(taken, rowNames[part]), labels));
        }

        foreach (int column in colOrder)
        {
            var cell = new ScriptValue[height];

            for (int r = 0; r < rowOrder.Length; r++)
                cell[r] = cells.TryGetValue((rowOrder[r], column), out PivotCell? found) ? found.Result(kind) : ScriptValue.None;

            if (totals) cell[^1] = colTotals[column]?.Result(kind) ?? ScriptValue.None;

            string name = colNames.Count == 0 ? value : colKeys.Name(column, PivotSeparator);

            built.Add(ScriptColumn.Own(Unique(taken, name), cell));
        }

        // Без полей колонок единственная колонка значений уже и есть итог по строке.
        if (totals && colNames.Count > 0)
        {
            var sums = new ScriptValue[height];

            for (int r = 0; r < rowOrder.Length; r++) sums[r] = rowTotals[rowOrder[r]]?.Result(kind) ?? ScriptValue.None;

            sums[^1] = grand.Result(kind);
            built.Add(ScriptColumn.Own(Unique(taken, TotalLabel), sums));
        }

        return ScriptTable.Create(built);
    }

    /// <summary>Имя колонки без повтора: совпавшее с уже занятым получает хвост.</summary>
    private static string Unique(HashSet<string> taken, string name)
    {
        while (!taken.Add(name)) name += "_";

        return name;
    }

    /// <summary>
    /// Плотные номера составных ключей в порядке первого появления.
    /// </summary>
    /// <remarks>
    /// Составной ключ сворачивается попарно: номер префикса и значение следующей части дают
    /// номер длинного префикса. Так строке не нужен ни массив, ни склеенная строка ключа.
    /// </remarks>
    private sealed class PivotKeys
    {
        public int[] Ids { get; private init; } = [];
        public List<ScriptValue[]> Parts { get; } = [];
        public int Count => Parts.Count;

        public static PivotKeys Of(ScriptTable t, IReadOnlyList<string> names)
        {
            var keys = new PivotKeys { Ids = new int[t.RowCount] };

            // Без полей все строки попадают в одну группу.
            if (names.Count == 0)
            {
                if (t.RowCount > 0) keys.Parts.Add([]);
                return keys;
            }

            var columns = new ScriptColumn[names.Count];

            for (int p = 0; p < names.Count; p++) columns[p] = t.Column(names[p]);

            var steps = new Dictionary<(int Prefix, ScriptValue Part), int>();
            var dense = new Dictionary<int, int>();

            for (int i = 0; i < t.RowCount; i++)
            {
                int prefix = -1;

                for (int p = 0; p < columns.Length; p++)
                {
                    var step = (prefix, columns[p][i]);

                    if (!steps.TryGetValue(step, out int next))
                    {
                        next = steps.Count;
                        steps[step] = next;
                    }

                    prefix = next;
                }

                if (!dense.TryGetValue(prefix, out int id))
                {
                    id = keys.Parts.Count;
                    dense[prefix] = id;

                    var parts = new ScriptValue[columns.Length];

                    for (int p = 0; p < columns.Length; p++) parts[p] = columns[p][i];

                    keys.Parts.Add(parts);
                }

                keys.Ids[i] = id;
            }

            return keys;
        }

        public int[] Order(bool sort)
        {
            int[] order = Sequence(0, Count);

            if (sort) Array.Sort(order, (left, right) => CompareParts(Parts[left], Parts[right]));

            return order;
        }

        public string Name(int id, string separator) =>
            string.Join(separator, Parts[id].Select(TableModule.Label));

        private static int CompareParts(ScriptValue[] left, ScriptValue[] right)
        {
            for (int p = 0; p < left.Length; p++)
            {
                int order = CompareKey(left[p], right[p]);

                if (order != 0) return order;
            }

            return 0;
        }

        private static int CompareKey(ScriptValue left, ScriptValue right)
        {
            if (left.Type == ScriptType.Str && right.Type == ScriptType.Str)
            {
                int a = CalendarLists.Place(left.AsString());
                int b = CalendarLists.Place(right.AsString());

                // Как в Excel: слова встроенного списка идут его порядком и раньше прочих.
                if (a >= 0 || b >= 0) return a < 0 ? 1 : b < 0 ? -1 : a.CompareTo(b);
            }

            // Разнотипные ключи сводной не повод для отказа: их порядок задает тип.
            return left.Type == right.Type || left.IsNone || right.IsNone
                ? Compare(left, right)
                : left.Type.CompareTo(right.Type);
        }
    }

    /// <summary>
    /// Встроенные списки сортировки Excel: месяцы и дни недели.
    /// </summary>
    /// <remarks>
    /// Excel ставит «янв» раньше «фев», хотя по алфавиту наоборот: сортировка сводной сверяется
    /// со списками календаря. Лист итогов обязан стоять так же, иначе месяцы на двух листах одной
    /// книги разойдутся. Названия берутся из культур языков интерфейса, а не пишутся руками.
    /// </remarks>
    private static class CalendarLists
    {
        private static readonly Dictionary<string, int> s_places = Build();

        /// <summary>Место слова в своем списке; -1 — слово не из календаря.</summary>
        public static int Place(string text) =>
            s_places.TryGetValue(text.Trim().TrimEnd('.'), out int place) ? place : -1;

        private static Dictionary<string, int> Build()
        {
            var places = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string culture in new[] { "ru-RU", "en-US", "uz-Latn-UZ" })
            {
                System.Globalization.DateTimeFormatInfo format;

                try
                {
                    format = System.Globalization.CultureInfo.GetCultureInfo(culture).DateTimeFormat;
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    // Без данных культуры (инвариантный режим) остается порядок по алфавиту.
                    continue;
                }

                // Месяцы раньше дней: «май» должен остаться месяцем, даже если совпадет с чем-то еще.
                Add(places, format.AbbreviatedMonthNames, 0);
                Add(places, format.MonthNames, 0);
                Add(places, format.MonthGenitiveNames, 0);

                // Короткая форма списка Excel — первые три буквы («мар», «сен»), а ICU сокращает
                // иначе («март», «сент.»), и без этой формы месяцы встали бы по алфавиту.
                Add(places, [.. format.MonthNames.Select(name => name.Length > 3 ? name[..3] : name)], 0);

                // Неделя с понедельника, как в списках Excel.
                string[] shortDays = [.. format.AbbreviatedDayNames.Skip(1), format.AbbreviatedDayNames[0]];
                string[] days = [.. format.DayNames.Skip(1), format.DayNames[0]];

                Add(places, shortDays, 100);
                Add(places, days, 100);
            }

            return places;
        }

        private static void Add(Dictionary<string, int> places, string[] names, int list)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i].Trim().TrimEnd('.');

                if (name.Length > 0) places.TryAdd(name, list + i);
            }
        }
    }

    /// <summary>
    /// Накопитель ячейки сводной.
    /// </summary>
    /// <remarks>
    /// Точность держится за данными, как в <see cref="Aggregate"/>: пока все значения точные,
    /// счет идет десятичной арифметикой, и сумма денег сходится с книгой до копейки.
    /// </remarks>
    private sealed class PivotCell
    {
        private int _count;
        private bool _exact = true;
        private decimal _decimal;
        private double _double;
        private ScriptValue _first = ScriptValue.None;

        public static bool Known(string kind) => kind is "sum" or "mean" or "min" or "max" or "count" or "first";

        public void Add(ScriptValue value, string kind)
        {
            if (IsMissing(value)) return;

            if (_count == 0) _first = value;

            if (kind is "count" or "first")
            {
                _count++;
                return;
            }

            double number = Numeric(value);

            if (_exact && value.Type != ScriptType.Dec) _exact = false;

            if (_count == 0)
            {
                _double = number;
                if (_exact) _decimal = value.AsDecimal();
            }
            else
            {
                _double = Fold(kind, _double, number);
                if (_exact) _decimal = Fold(kind, _decimal, value.AsDecimal());
            }

            _count++;
        }

        public ScriptValue Result(string kind)
        {
            if (kind == "count") return ScriptValue.Num(_count);
            if (_count == 0) return ScriptValue.None;
            if (kind == "first") return _first;

            return _exact
                ? ScriptValue.Dec(kind == "mean" ? _decimal / _count : _decimal)
                : ScriptValue.Num(kind == "mean" ? _double / _count : _double);
        }

        private static double Fold(string kind, double total, double item) => kind switch
        {
            "min" => Math.Min(total, item),
            "max" => Math.Max(total, item),
            _ => total + item,
        };

        private static decimal Fold(string kind, decimal total, decimal item) => kind switch
        {
            "min" => Math.Min(total, item),
            "max" => Math.Max(total, item),
            _ => total + item,
        };
    }
}

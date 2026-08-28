using AI.Script.Binding;
using AI.Script.Docs;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>core</c>: то, что нужно в каждой строке скрипта.
/// </summary>
/// <remarks>
/// Единственное пространство, функции которого доступны без префикса. Список короткий
/// намеренно: чем он длиннее, тем выше шанс, что имя переменной случайно совпадёт с функцией.
/// </remarks>
[ScriptModule("core", "Базовые операции: длина, типы, обход последовательностей, вывод", Version = "0.1")]
public static class CoreModule
{
    [ScriptFn("len", "Длина строки, списка, вектора, записи или диапазона", Example = "len(xs)")]
    public static double Len([ScriptParam("значение")] ScriptValue value) => value.Type switch
    {
        ScriptType.Str => value.AsString().Length,
        ScriptType.List => value.AsList().Count,
        ScriptType.Vec => value.AsVector().Count,
        ScriptType.Record => value.AsRecord().Count,
        ScriptType.Range => value.AsRange().Count,
        ScriptType.Mat => value.AsMatrix().Height,
        ScriptType.Table => value.AsTable().RowCount,
        _ => throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"у значения типа {value.Type.ToName()} нет длины"),
    };

    [ScriptFn("type", "Имя типа значения", Example = "type(x) == \"num\"")]
    public static string TypeOf([ScriptParam("значение")] ScriptValue value) => value.Type.ToName();

    [ScriptFn("help", "Справка: список пространств имён либо описание функции", Example = "help(\"math.sqrt\")")]
    public static string Help(
        IScriptContext context,
        [ScriptParam("имя пространства или функции; пусто — список пространств")] string query = "")
        => Manifest.Describe(context.Modules, query);

    /// <summary>
    /// Поиск функции по словам задачи.
    /// </summary>
    /// <remarks>
    /// Ищут словами задачи («корреляция»), а не именем функции, которое ещё надо знать.
    /// Это второй уровень манифеста: сначала найти, потом спросить сигнатуру через help.
    /// </remarks>
    [ScriptFn("find_fn", "Ищет функции библиотеки по имени и описанию", Example = "find_fn(\"корреляция\")")]
    public static ScriptList FindFunction(
        IScriptContext context,
        [ScriptParam("что ищем")] string query,
        [ScriptParam("сколько показать")] int limit = 10)
    {
        IReadOnlyList<ManifestMatch> matches = ManifestBuilder.Search(context.Modules, query, limit);
        var items = new ScriptValue[matches.Count];

        for (int i = 0; i < matches.Count; i++)
        {
            items[i] = ScriptValue.Record(ScriptRecord.From(
            [
                new KeyValuePair<string, ScriptValue>("name", ScriptValue.Str(matches[i].Function.FullName)),
                new KeyValuePair<string, ScriptValue>("signature", ScriptValue.Str(matches[i].Function.Signature)),
                new KeyValuePair<string, ScriptValue>("about", ScriptValue.Str(matches[i].Function.Description)),
            ]));
        }

        return ScriptList.Own(items);
    }

    [ScriptFn("print", "Печатает значения в транскрипт через пробел", Example = "print(\"k =\", k)")]
    public static void Print(
        IScriptContext context,
        [ScriptParam("значения", Variadic = true)] ScriptList values)
    {
        var parts = new List<string>(values.Count);

        foreach (ScriptValue value in values) parts.Add(ScriptFormatter.Format(value));

        context.Print(string.Join(" ", parts));
    }

    [ScriptFn("round", "Округление до заданного числа знаков", Example = "core.round(x, digits: 3)")]
    public static double Round(
        [ScriptParam("число")] double value,
        [ScriptParam("число знаков после запятой")] int digits = 0)
        => Math.Round(value, Math.Clamp(digits, 0, 15), MidpointRounding.AwayFromZero);

    [ScriptFn("to_num", "Логическое значение или дату — в число", Example = "core.to_num(flag)")]
    public static double ToNumber([ScriptParam("значение")] ScriptValue value) => value.Type switch
    {
        ScriptType.Num => value.RawNumber,
        ScriptType.Bool => value.RawNumber,
        ScriptType.Dur => value.AsDuration().TotalSeconds,
        _ => throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"значение типа {value.Type.ToName()} не переводится в число",
            "строку разбирает core.parse_num(текст)"),
    };

    [ScriptFn("parse_num", "Разбирает число из строки", Example = "core.parse_num(\"3.14\")")]
    public static double ParseNumber(
        [ScriptParam("текст")] string text,
        [ScriptParam("значение при неудаче")] double fallback = double.NaN)
        => double.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double result) ? result : fallback;

    [ScriptFn("to_str", "Текстовое представление значения", Example = "core.to_str(42)")]
    public static string ToText([ScriptParam("значение")] ScriptValue value) => ScriptFormatter.Format(value);

    [ScriptFn("has", "Есть ли поле в записи", Example = "core.has(cfg, \"temp\")")]
    public static bool Has(
        [ScriptParam("запись")] ScriptRecord record,
        [ScriptParam("имя поля")] string name)
        => record.Has(name);

    [ScriptFn("keys", "Имена полей записи", Example = "core.keys(cfg)")]
    public static ScriptList Keys([ScriptParam("запись")] ScriptRecord record)
    {
        var items = new ScriptValue[record.Count];

        for (int i = 0; i < record.Count; i++) items[i] = ScriptValue.Str(record.Keys[i]);

        return ScriptList.Own(items);
    }

    [ScriptFn("values", "Значения полей записи", Example = "core.values(cfg)")]
    public static ScriptList Values([ScriptParam("запись")] ScriptRecord record)
    {
        var items = new ScriptValue[record.Count];

        for (int i = 0; i < record.Count; i++) items[i] = record.Values[i];

        return ScriptList.Own(items);
    }

    [ScriptFn("pairs", "Пары «имя, значение» записи для обхода циклом", Example = "for (k, v) in core.pairs(cfg) { }")]
    public static ScriptList Pairs([ScriptParam("запись")] ScriptRecord record)
    {
        var items = new ScriptValue[record.Count];

        for (int i = 0; i < record.Count; i++)
        {
            items[i] = ScriptValue.List(ScriptList.Own(
                [ScriptValue.Str(record.Keys[i]), record.Values[i]]));
        }

        return ScriptList.Own(items);
    }

    [ScriptFn("range", "Диапазон от нуля до заданного числа", Example = "core.range(10, by: 2)")]
    public static ScriptRange Range(
        [ScriptParam("верхняя граница, не включается")] double count,
        [ScriptParam("нижняя граница")] double from = 0,
        [ScriptParam("шаг")] double by = 1)
        => new(from, count, by);

    [ScriptFn("list", "Приводит вектор либо диапазон к списку", Example = "core.list(<1, 2, 3>)")]
    public static ScriptList ToList([ScriptParam("последовательность")] ScriptList items) => items;

    [ScriptFn("first", "Первый элемент", Example = "core.first(xs)")]
    public static ScriptValue First([ScriptParam("последовательность")] ScriptList items) =>
        items.Count > 0 ? items[0] : throw Empty("first");

    [ScriptFn("last", "Последний элемент", Example = "core.last(xs)")]
    public static ScriptValue Last([ScriptParam("последовательность")] ScriptList items) =>
        items.Count > 0 ? items[^1] : throw Empty("last");

    [ScriptFn("take", "Первые n элементов", Example = "xs |> core.take(5)")]
    public static ScriptList Take(
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("сколько взять")] int count)
        => items.Slice(0, Math.Clamp(count, 0, items.Count));

    [ScriptFn("skip", "Пропускает n первых элементов", Example = "xs |> core.skip(5)")]
    public static ScriptList Skip(
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("сколько пропустить")] int count)
        => items.Slice(Math.Clamp(count, 0, items.Count), items.Count);

    [ScriptFn("reverse", "Разворачивает последовательность", Example = "xs |> core.reverse()")]
    public static ScriptList Reverse([ScriptParam("последовательность")] ScriptList items)
    {
        var copy = new ScriptValue[items.Count];

        for (int i = 0; i < items.Count; i++) copy[i] = items[items.Count - 1 - i];

        return ScriptList.Own(copy);
    }

    [ScriptFn("contains", "Есть ли значение в последовательности", Example = "core.contains(xs, 3)")]
    public static bool Contains(
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("искомое значение")] ScriptValue value)
    {
        foreach (ScriptValue item in items)
        {
            if (item.Equals(value)) return true;
        }

        return false;
    }

    [ScriptFn("index_of", "Позиция значения в последовательности; -1, если нет", Example = "core.index_of(xs, 3)")]
    public static double IndexOf(
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("искомое значение")] ScriptValue value)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Equals(value)) return i;
        }

        return -1;
    }

    [ScriptFn("unique", "Убирает повторы, сохраняя порядок", Example = "xs |> core.unique()")]
    public static ScriptList Unique([ScriptParam("последовательность")] ScriptList items)
    {
        var seen = new HashSet<ScriptValue>();
        var result = new List<ScriptValue>(items.Count);

        foreach (ScriptValue item in items)
        {
            if (seen.Add(item)) result.Add(item);
        }

        return ScriptList.From(result);
    }

    [ScriptFn("zip", "Складывает две последовательности в список пар", Example = "core.zip(xs, ys)")]
    public static ScriptList Zip(
        [ScriptParam("первая последовательность")] ScriptList left,
        [ScriptParam("вторая последовательность")] ScriptList right)
    {
        int count = Math.Min(left.Count, right.Count);
        var items = new ScriptValue[count];

        for (int i = 0; i < count; i++)
            items[i] = ScriptValue.List(ScriptList.Own([left[i], right[i]]));

        return ScriptList.Own(items);
    }

    [ScriptFn("enumerate", "Список пар «индекс, значение»", Example = "for (i, x) in core.enumerate(xs) { }")]
    public static ScriptList Enumerate([ScriptParam("последовательность")] ScriptList items)
    {
        var result = new ScriptValue[items.Count];

        for (int i = 0; i < items.Count; i++)
            result[i] = ScriptValue.List(ScriptList.Own([ScriptValue.Num(i), items[i]]));

        return ScriptList.Own(result);
    }

    /// <summary>
    /// Применяет функцию к каждому элементу.
    /// </summary>
    /// <remarks>
    /// Единственная форма параллелизма в языке. Порядок результата сохраняется всегда, а каждая
    /// ветвь получает собственный поток случайных чисел, выведенный из зерна прогона и номера
    /// элемента, — поэтому <c>parallel: true</c> не делает результат невоспроизводимым. Что при
    /// этом действительно меняется — порядок строк, напечатанных из лямбды.
    /// </remarks>
    [ScriptFn("map", "Применяет функцию к каждому элементу", Example = "xs |> core.map(x => x * 2)")]
    public static async Task<ScriptList> Map(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("функция одного аргумента")] ScriptCallable transform,
        [ScriptParam("считать элементы одновременно (options.parallel ветвей)")] bool parallel = false)
    {
        ScriptValue[] result = await context
            .CallEachAsync(ScriptValue.Fn(transform), Items(items), Degree(context, parallel))
            .ConfigureAwait(false);

        return ScriptList.Own(result);
    }

    [ScriptFn("filter", "Оставляет элементы, для которых предикат истинен", Example = "xs |> core.filter(x => x > 0)")]
    public static async Task<ScriptList> Filter(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("предикат одного аргумента")] ScriptCallable predicate,
        [ScriptParam("проверять элементы одновременно")] bool parallel = false)
    {
        ScriptValue[] verdicts = await context
            .CallEachAsync(ScriptValue.Fn(predicate), Items(items), Degree(context, parallel))
            .ConfigureAwait(false);

        var result = new List<ScriptValue>(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            if (verdicts[i].AsBool("результат предиката")) result.Add(items[i]);
        }

        return ScriptList.From(result);
    }

    /// <summary>
    /// Применяет функцию, возвращающую список, и склеивает результаты.
    /// </summary>
    /// <remarks>
    /// Отдельная функция, а не <c>map</c> с последующим <c>concat</c>: промежуточный список
    /// списков на большом входе — лишняя копия всего содержимого.
    /// </remarks>
    [ScriptFn("flat_map", "Применяет функцию и склеивает полученные списки",
        Example = "docs |> core.flat_map(d => nlp.words(d))")]
    public static async Task<ScriptList> FlatMap(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("функция, возвращающая список")] ScriptCallable transform,
        [ScriptParam("считать элементы одновременно")] bool parallel = false)
    {
        ScriptValue[] parts = await context
            .CallEachAsync(ScriptValue.Fn(transform), Items(items), Degree(context, parallel))
            .ConfigureAwait(false);

        var result = new List<ScriptValue>(parts.Length);

        foreach (ScriptValue part in parts)
        {
            if (part.Type != ScriptType.List)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"core.flat_map ожидает список от каждого вызова, получен {part.Type.ToName()}",
                    "если функция возвращает одно значение, подойдёт core.map");
            }

            ScriptList inner = part.AsList();

            for (int i = 0; i < inner.Count; i++) result.Add(inner[i]);
        }

        return ScriptList.From(result);
    }

    private static IReadOnlyList<ScriptValue> Items(ScriptList items)
    {
        var copy = new ScriptValue[items.Count];

        for (int i = 0; i < items.Count; i++) copy[i] = items[i];

        return copy;
    }

    /// <summary>
    /// Сколько ветвей просить у прогона.
    /// </summary>
    /// <remarks>
    /// Число берётся из <c>options.parallel</c>, а не из аргумента: сколько потоков поднимать —
    /// свойство машины и прогона, а не отдельного вызова. Аргумент отвечает только на вопрос
    /// «можно ли здесь вообще параллелить».
    /// </remarks>
    private static int Degree(IScriptContext context, bool parallel) => parallel ? context.Parallelism : 1;

    [ScriptFn("reduce", "Свёртка последовательности", Example = "xs |> core.reduce((a, b) => a + b, from: 0)")]
    public static async Task<ScriptValue> Reduce(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("функция двух аргументов")] ScriptCallable combine,
        [ScriptParam("начальное значение")] ScriptValue from = default)
    {
        ScriptValue callable = ScriptValue.Fn(combine);
        ScriptValue accumulator = from;
        int start = 0;

        if (accumulator.IsNone)
        {
            if (items.Count == 0) throw Empty("reduce");

            accumulator = items[0];
            start = 1;
        }

        for (int i = start; i < items.Count; i++)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            accumulator = await context.CallAsync(callable, accumulator, items[i]).ConfigureAwait(false);
        }

        return accumulator;
    }

    [ScriptFn("any", "Истинен ли предикат хотя бы для одного элемента", Example = "xs |> core.any(x => x < 0)")]
    public static async Task<bool> Any(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("предикат")] ScriptCallable predicate)
    {
        ScriptValue callable = ScriptValue.Fn(predicate);

        foreach (ScriptValue item in items)
        {
            if ((await context.CallAsync(callable, item).ConfigureAwait(false)).AsBool("результат предиката"))
                return true;
        }

        return false;
    }

    [ScriptFn("all", "Истинен ли предикат для всех элементов", Example = "xs |> core.all(x => x > 0)")]
    public static async Task<bool> All(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("предикат")] ScriptCallable predicate)
    {
        ScriptValue callable = ScriptValue.Fn(predicate);

        foreach (ScriptValue item in items)
        {
            if (!(await context.CallAsync(callable, item).ConfigureAwait(false)).AsBool("результат предиката"))
                return false;
        }

        return true;
    }

    [ScriptFn("sort", "Сортировка; ключ задаётся функцией", Example = "xs |> core.sort(by: x => x.price)")]
    public static async Task<ScriptList> Sort(
        IScriptContext context,
        [ScriptParam("последовательность")] ScriptList items,
        [ScriptParam("функция ключа сортировки")] ScriptCallable? by = null,
        [ScriptParam("по убыванию")] bool desc = false)
    {
        var keys = new ScriptValue[items.Count];

        if (by == null)
        {
            for (int i = 0; i < items.Count; i++) keys[i] = items[i];
        }
        else
        {
            ScriptValue callable = ScriptValue.Fn(by);

            for (int i = 0; i < items.Count; i++)
                keys[i] = await context.CallAsync(callable, items[i]).ConfigureAwait(false);
        }

        var order = new int[items.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;

        Array.Sort(order, (left, right) =>
        {
            int comparison = CompareValues(keys[left], keys[right]);
            return desc ? -comparison : comparison;
        });

        var result = new ScriptValue[items.Count];
        for (int i = 0; i < order.Length; i++) result[i] = items[order[i]];

        return ScriptList.Own(result);
    }

    private static int CompareValues(ScriptValue left, ScriptValue right)
    {
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

    private static ScriptError Empty(string name) =>
        new(DiagnosticCodes.IndexOutOfRange,
            $"core.{name}: последовательность пуста",
            "проверьте длину заранее: len(xs) > 0");
}

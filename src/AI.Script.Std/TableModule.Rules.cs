using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Правила данных: что таблица обязана выполнять, чтобы по ней можно было считать.
/// </summary>
/// <remarks>
/// Опыт на данных с дублями и пропусками даёт правдоподобный неверный итог — и заметить это
/// по самому итогу нечем. Правила объявляются рядом с чтением и срывают прогон до счёта:
/// <c>assert len(нарушения) == 0</c>.
/// <para>
/// Функция, а не новый синтаксис: правило — это либо имя проверки, либо обычная лямбда. Язык
/// от этого не растёт, проверка видит обычный вызов, а набор правил можно собрать записью и
/// передать дальше.
/// </para>
/// </remarks>
public static partial class TableModule
{
    /// <summary>Имя правила-предиката в отчёте: у лямбды своего имени нет.</summary>
    private const string PredicateRule = "предикат";

    /// <summary>Имена встроенных правил — они же перечисляются в отказе на незнакомое имя.</summary>
    private static readonly string[] s_ruleNames =
        ["not_null", "unique", "positive", "non_negative", "number", "date", "text"];

    [ScriptFn("validate", "Проверяет таблицу правилами: пропуски, дубли, знак, тип",
        Example = "t |> table.validate(rules: { id: \"unique\", сумма: \"positive\" })",
        Columns = "column,row,value,rule")]
    public static async Task<ScriptTable> Validate(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("запись «колонка → правило»: имя проверки, предикат либо список из них")] ScriptRecord rules,
        [ScriptParam("сколько нарушений записать; 0 — все")] int limit = 0)
    {
        var columns = new List<ScriptValue>();
        var rows = new List<ScriptValue>();
        var values = new List<ScriptValue>();
        var broken = new List<ScriptValue>();
        int cap = limit > 0 ? limit : int.MaxValue;

        foreach (var rule in rules.Pairs())
        {
            ScriptColumn column = t.Column(rule.Key);

            foreach ((int row, string name) in await BrokenAsync(context, column, rule.Value).ConfigureAwait(false))
            {
                if (columns.Count >= cap) break;

                columns.Add(ScriptValue.Str(rule.Key));
                rows.Add(ScriptValue.Num(row + 1));
                values.Add(ScriptValue.Str(Label(column[row])));
                broken.Add(ScriptValue.Str(name));
            }
        }

        context.CountAllocation(columns.Count);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("column", [.. columns]),
            ScriptColumn.Own("row", [.. rows]),
            ScriptColumn.Own("value", [.. values]),
            ScriptColumn.Own("rule", [.. broken]),
        ]);
    }

    /// <summary>Строки колонки, нарушающие правило, вместе с именем нарушенного правила.</summary>
    private static async Task<IReadOnlyList<(int Row, string Rule)>> BrokenAsync(
        IScriptContext context, ScriptColumn column, ScriptValue rule)
    {
        // Несколько правил на колонку: «сумма: ["not_null", "positive"]». Нарушения идут по
        // правилам, а не по строкам, — так в отчёте видно, какая проверка не прошла.
        if (rule.Type == ScriptType.List)
        {
            ScriptList list = rule.AsList();
            var all = new List<(int, string)>();

            for (int i = 0; i < list.Count; i++)
                all.AddRange(await BrokenAsync(context, column, list[i]).ConfigureAwait(false));

            return all;
        }

        if (rule.Type == ScriptType.Fn) return await PredicateAsync(context, column, rule).ConfigureAwait(false);

        string name = rule.AsString("table.validate: правило");
        var found = new List<(int, string)>();

        if (string.Equals(name, "unique", StringComparison.Ordinal))
        {
            foreach (int row in Duplicates(column)) found.Add((row, name));

            return found;
        }

        for (int i = 0; i < column.Count; i++)
        {
            if (!Holds(name, column[i])) found.Add((i, name));
        }

        return found;
    }

    /// <summary>
    /// Правило-лямбда: предикат зовётся на каждое значение колонки.
    /// </summary>
    /// <remarks>
    /// Через асинхронный обратный вызов, а не через мост синхронного вызова: правило вправе
    /// обратиться к файлу либо к справочнику, и такой вызов не должен занимать поток ожиданием.
    /// </remarks>
    private static async Task<IReadOnlyList<(int Row, string Rule)>> PredicateAsync(
        IScriptContext context, ScriptColumn column, ScriptValue predicate)
    {
        var items = new ScriptValue[column.Count];

        for (int i = 0; i < column.Count; i++) items[i] = column[i];

        ScriptValue[] verdicts = await context.CallEachAsync(predicate, items, 1).ConfigureAwait(false);
        var found = new List<(int, string)>();

        for (int i = 0; i < verdicts.Length; i++)
        {
            if (!verdicts[i].AsBool("table.validate: результат правила")) found.Add((i, PredicateRule));
        }

        return found;
    }

    /// <summary>
    /// Выполняется ли правило на одном значении.
    /// </summary>
    /// <remarks>
    /// Пропуск нарушает только <c>not_null</c>: иначе одна пустая ячейка давала бы два
    /// нарушения подряд, и отчёт о качестве данных раздувался бы вдвое на ровном месте.
    /// </remarks>
    private static bool IsNumber(ScriptValue value) => value.Type is ScriptType.Num or ScriptType.Dec;

    private static bool Holds(string rule, ScriptValue value)
    {
        bool missing = IsMissing(value);

        return rule switch
        {
            "not_null" => !missing,
            "positive" => missing || (IsNumber(value) && Numeric(value) > 0),
            "non_negative" => missing || (IsNumber(value) && Numeric(value) >= 0),
            "number" => missing || value.Type is ScriptType.Num or ScriptType.Dec,
            "date" => missing || value.Type == ScriptType.Date,
            "text" => missing || value.Type == ScriptType.Str,
            _ => throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"table.validate: неизвестное правило «{rule}»",
                $"известны: {string.Join(", ", s_ruleNames)}; своё правило пишется лямбдой"),
        };
    }

    /// <summary>
    /// Строки с повторяющимся значением, кроме первой встречи.
    /// </summary>
    /// <remarks>
    /// Первая встреча нарушением не считается: нарушение — это ДУБЛЬ, и показывать обе строки
    /// значит требовать от читателя догадаться, какая из них лишняя.
    /// </remarks>
    private static IEnumerable<int> Duplicates(ScriptColumn column)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < column.Count; i++)
        {
            if (IsMissing(column[i])) continue;

            if (!seen.Add(Label(column[i]))) yield return i;
        }
    }
}

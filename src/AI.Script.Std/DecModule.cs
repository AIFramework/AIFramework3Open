using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>dec</c>: точные числа — деньги, ставки, нормативы.
/// </summary>
/// <remarks>
/// Обычное число двоичное, и сумма тысячи строк с копейками расходится с книгой Excel на
/// копейку — там, где отчёт обязан сойтись. Точное число считается десятичной арифметикой, как
/// считает бухгалтерия, поэтому складывать его с обычным язык не даёт: перевод пишется явно.
/// <para>
/// Умножать и делить точное число на обычное можно: доля, ставка и курс — это множители, а не
/// деньги. Так же устроена длительность.
/// </para>
/// </remarks>
[ScriptModule("dec", "Точные числа: деньги без потери копейки", Version = "0.1", Group = "данные")]
public static class DecModule
{
    [ScriptFn("of", "Точное число из обычного числа либо из текста", Example = "dec.of(1234.5)")]
    public static decimal Of([ScriptParam("число, точное число либо текст")] ScriptValue value) => Require(value, "dec.of");

    [ScriptFn("parse", "Разбирает число из денежной записи: пробелы, запятая, знак валюты",
        Example = "dec.parse(\"1 234,50 ₽\")")]
    public static decimal ParseText(
        [ScriptParam("текст")] string text,
        [ScriptParam("локаль записи: \"ru\" либо \"en\"")] string locale = "ru")
        => DecimalText.Parse(text, locale, "dec.parse");

    [ScriptFn("to_num", "Точное число в обычное; копейка при этом может потеряться",
        Example = "dec.to_num(итого)")]
    public static double ToNumber([ScriptParam("точное число")] ScriptValue value) => (double)Require(value, "dec.to_num");

    [ScriptFn("sum", "Точная сумма списка либо колонки", Example = "dec.sum(t[\"сумма\"])")]
    public static decimal Sum([ScriptParam("значения")] ScriptValue values)
    {
        decimal total = 0;

        foreach (decimal item in Items(values, "dec.sum")) total += item;

        return total;
    }

    [ScriptFn("mean", "Точное среднее списка либо колонки", Example = "dec.mean(t[\"чек\"])")]
    public static decimal Mean([ScriptParam("значения")] ScriptValue values)
    {
        IReadOnlyList<decimal> items = Items(values, "dec.mean");

        if (items.Count == 0) throw Empty("dec.mean");

        decimal total = 0;

        foreach (decimal item in items) total += item;

        return total / items.Count;
    }

    [ScriptFn("min", "Наименьшее точное число списка либо колонки", Example = "dec.min(t[\"сумма\"])")]
    public static decimal Min([ScriptParam("значения")] ScriptValue values)
    {
        IReadOnlyList<decimal> items = Items(values, "dec.min");

        if (items.Count == 0) throw Empty("dec.min");

        decimal best = items[0];

        foreach (decimal item in items) best = Math.Min(best, item);

        return best;
    }

    [ScriptFn("max", "Наибольшее точное число списка либо колонки", Example = "dec.max(t[\"сумма\"])")]
    public static decimal Max([ScriptParam("значения")] ScriptValue values)
    {
        IReadOnlyList<decimal> items = Items(values, "dec.max");

        if (items.Count == 0) throw Empty("dec.max");

        decimal best = items[0];

        foreach (decimal item in items) best = Math.Max(best, item);

        return best;
    }

    /// <summary>
    /// Округление точного числа.
    /// </summary>
    /// <remarks>
    /// По умолчанию банковское — к ближайшему чётному. На тысяче строк оно не накапливает
    /// смещение вверх, в отличие от «половину вверх», и именно так считают учётные системы.
    /// Смещение в полкопейки на строке к концу отчёта превращается в рубли.
    /// </remarks>
    [ScriptFn("round", "Округление точного числа: банковское либо половину вверх",
        Example = "dec.round(итого, digits: 2, kind: \"bank\")")]
    public static decimal Round(
        [ScriptParam("точное число")] ScriptValue value,
        [ScriptParam("сколько знаков после запятой")] int digits = 2,
        [ScriptParam("правило: \"bank\" либо \"half_up\"")] string kind = "bank")
    {
        MidpointRounding rounding = kind switch
        {
            "bank" => MidpointRounding.ToEven,
            "half_up" => MidpointRounding.AwayFromZero,
            _ => throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"dec.round: неизвестное правило округления «{kind}»",
                "известны: \"bank\" — к ближайшему чётному, \"half_up\" — половину вверх"),
        };

        return Math.Round(Require(value, "dec.round"), Math.Clamp(digits, 0, 28), rounding);
    }

    [ScriptFn("abs", "Модуль точного числа", Example = "dec.abs(разница)")]
    public static decimal Abs([ScriptParam("точное число")] ScriptValue value) => Math.Abs(Require(value, "dec.abs"));

    /// <summary>Точное число из значения языка; текст разбирается, обычное число переводится.</summary>
    private static decimal Require(ScriptValue value, string what) => value.Type switch
    {
        ScriptType.Dec => value.AsDecimal(what),
        ScriptType.Num => Widen(value.RawNumber, what),
        ScriptType.Str => DecimalText.Parse(value.AsString(), "ru", what),
        _ => throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: значение типа {value.Type.ToName()} точным числом не становится",
            "точное число делается из числа, текста либо колонки"),
    };

    private static IReadOnlyList<decimal> Items(ScriptValue values, string what)
    {
        var items = new List<decimal>();

        switch (values.Type)
        {
            case ScriptType.List:
                foreach (ScriptValue item in values.AsList())
                {
                    if (item.IsNone) continue;

                    items.Add(Require(item, what));
                }

                return items;

            case ScriptType.Vec:
                {
                    Vector vector = values.AsVector();

                    for (int i = 0; i < vector.Count; i++)
                    {
                        if (double.IsNaN(vector[i])) continue;

                        items.Add(Widen(vector[i], what));
                    }

                    return items;
                }

            case ScriptType.Dec:
            case ScriptType.Num:
                items.Add(Require(values, what));
                return items;

            default:
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"{what}: ожидались список либо колонка чисел, получено {values.Type.ToName()}");
        }
    }

    private static decimal Widen(double value, string what)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{what}: {ScriptFormatter.Number(value)} точным числом не становится");
        }

        try
        {
            return (decimal)value;
        }
        catch (OverflowException)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: число не помещается в точное");
        }
    }

    private static ScriptError Empty(string what) =>
        new(DiagnosticCodes.BadOperand, $"{what}: значений нет");
}

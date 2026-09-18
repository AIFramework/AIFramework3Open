using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Globalization;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>fmt</c>: числа для человека.
/// </summary>
/// <remarks>
/// Печать значений в транскрипте идёт с инвариантной культурой — её читают и человек, и
/// модель, и запятая в роли разделителя сломала бы второе прочтение. Но итог уходит в документ
/// и на экран, где «1234567.5» читается плохо, а «1 234 567,50 ₽» читается сразу. Поэтому
/// перевод в человеческую запись — отдельный явный шаг, а не свойство печати.
/// </remarks>
[ScriptModule("fmt", "Числа словами человека: разряды, запятая, валюта, проценты", Version = "0.1", Group = "основа")]
public static class FmtModule
{
    [ScriptFn("num", "Число с разрядами: 1 234 567,5", Example = "fmt.num(выручка, digits: 2)")]
    public static string Number(
        [ScriptParam("число либо точное число")] ScriptValue value,
        [ScriptParam("знаков после запятой; -1 — как есть")] int digits = -1,
        [ScriptParam("локаль записи: \"ru\" либо \"en\"")] string locale = "ru")
        => Write(value, digits, locale, "fmt.num");

    [ScriptFn("money", "Сумма со знаком валюты: 1 234 567,50 ₽", Example = "fmt.money(итого, cur: \"₽\")")]
    public static string Money(
        [ScriptParam("число либо точное число")] ScriptValue value,
        [ScriptParam("знак валюты")] string cur = "₽",
        [ScriptParam("знаков после запятой")] int digits = 2,
        [ScriptParam("локаль записи: \"ru\" либо \"en\"")] string locale = "ru")
    {
        string number = Write(value, digits, locale, "fmt.money");

        return string.IsNullOrEmpty(cur) ? number : $"{number} {cur}";
    }

    /// <summary>
    /// Доля процентом.
    /// </summary>
    /// <remarks>
    /// На вход идёт доля, а не проценты: 0.124 — это 12,4 %. Обратное соглашение («передавайте
    /// сразу 12.4») приводит к двойному умножению на сто там, где доля пришла из расчёта, и
    /// ошибка в сто раз в отчёте не бросается в глаза.
    /// </remarks>
    [ScriptFn("pct", "Доля процентом: 0.124 — это 12,4 %", Example = "fmt.pct(доля, digits: 1)")]
    public static string Percent(
        [ScriptParam("доля от единицы")] ScriptValue value,
        [ScriptParam("знаков после запятой")] int digits = 1,
        [ScriptParam("локаль записи: \"ru\" либо \"en\"")] string locale = "ru")
    {
        ScriptValue scaled = value.Type == ScriptType.Dec
            ? ScriptValue.Dec(value.AsDecimal() * 100m)
            : ScriptValue.Num(Require(value, "fmt.pct") * 100);

        return $"{Write(scaled, digits, locale, "fmt.pct")} %";
    }

    private static string Write(ScriptValue value, int digits, string locale, string what)
    {
        NumberFormatInfo format = Format(locale, what);

        if (value.Type == ScriptType.Dec)
        {
            decimal exact = value.AsDecimal();

            return digits < 0
                ? exact.ToString("#,0.############################", format)
                : exact.ToString("N" + Math.Clamp(digits, 0, 28), format);
        }

        double number = Require(value, what);

        if (double.IsNaN(number) || double.IsInfinity(number)) return ScriptFormatter.Number(number);

        return digits < 0
            ? number.ToString("#,0.###############", format)
            : number.ToString("N" + Math.Clamp(digits, 0, 15), format);
    }

    private static double Require(ScriptValue value, string what) => value.Type switch
    {
        ScriptType.Num => value.RawNumber,
        ScriptType.Dec => (double)value.AsDecimal(),
        _ => throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: ожидалось число, получено {value.Type.ToName()}",
            "текст переводится в число функцией core.parse_num либо dec.parse"),
    };

    /// <summary>
    /// Правила записи числа для локали.
    /// </summary>
    /// <remarks>
    /// Разделитель разрядов — обычный пробел, а не неразрывный: строка уходит и в документ, и
    /// в сравнение внутри скрипта, а неразрывный пробел выглядит так же, но не совпадает с
    /// тем, что напишет человек в ожидаемом значении.
    /// </remarks>
    private static NumberFormatInfo Format(string locale, string what)
    {
        bool russian = string.Equals(locale, "ru", StringComparison.OrdinalIgnoreCase);

        if (!russian && !string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{what}: неизвестная локаль «{locale}»",
                "известны: \"ru\" — запятая и пробел, \"en\" — точка и запятая");
        }

        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();

        format.NumberDecimalSeparator = russian ? "," : ".";
        format.NumberGroupSeparator = russian ? " " : ",";
        format.NumberGroupSizes = [3];

        return format;
    }
}

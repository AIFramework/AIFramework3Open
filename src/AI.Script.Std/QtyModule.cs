using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>qty</c>: величины с единицами.
/// </summary>
/// <remarks>
/// Литерал пишется прямо в тексте (<c>5 kg</c>, <c>120 rub</c>), арифметика — обычными
/// операторами. Здесь то, чего оператором не выразить: величина из числа, пришедшего из данных,
/// перевод в другую единицу и число обратно — для функций, которые единиц не знают.
/// </remarks>
[ScriptModule("qty", "Величины с единицами: перевод, число в единице, обозначение", Version = "0.1", Group = "данные")]
public static class QtyModule
{
    [ScriptFn("of", "Величина из числа и обозначения единицы", Example = "qty.of(t[0].вес, \"kg\")")]
    public static ScriptValue Of(
        [ScriptParam("число")] double value,
        [ScriptParam("обозначение: kg, g, m, l, pcs, rub, rub/kg")] string unit)
        => ScriptValue.Quantity(value, Unit(unit, "qty.of"));

    /// <summary>
    /// Та же величина в другой единице.
    /// </summary>
    /// <remarks>
    /// Меняется только запись: <c>qty.to(1.5 kg, "g")</c> — это <c>1500 g</c>, и сравнение с исходной
    /// величиной даёт равенство. Разная размерность — отказ, а не перевод по догадке.
    /// </remarks>
    [ScriptFn("to", "Переводит величину в другую единицу той же размерности", Example = "qty.to(вес, \"g\")")]
    public static ScriptValue To(
        [ScriptParam("величина")] ScriptValue value,
        [ScriptParam("обозначение единицы")] string unit)
    {
        MeasureUnit target = Unit(unit, "qty.to");
        MeasureUnit source = value.AsUnit("qty.to");

        if (!source.SameDimension(target))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"qty.to: {source.Symbol} не переводится в {target.Symbol} — разные размерности");
        }

        return ScriptValue.Qty(value.RawNumber, target);
    }

    [ScriptFn("value", "Число величины в заданной единице", Example = "qty.value(вес, unit: \"g\")")]
    public static double Value(
        [ScriptParam("величина")] ScriptValue value,
        [ScriptParam("обозначение единицы; пусто — в единице самой величины")] string unit = "")
        => string.IsNullOrWhiteSpace(unit) ? value.QuantityValue : To(value, unit).QuantityValue;

    [ScriptFn("unit", "Обозначение единицы величины", Example = "qty.unit(цена)")]
    public static string UnitOf([ScriptParam("величина")] ScriptValue value) => value.AsUnit("qty.unit").Symbol;

    private static MeasureUnit Unit(string symbol, string what) =>
        MeasureUnit.TryParse(symbol, out MeasureUnit unit)
            ? unit
            : throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{what}: неизвестная единица «{symbol}»",
                "известны единицы СИ с приставками (kg, g, km, l, W), валюты rub, usd, eur, штуки pcs и составные: rub/kg");
}

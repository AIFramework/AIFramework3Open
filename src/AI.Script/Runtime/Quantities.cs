using AI.Script.Semantics;
using AI.Script.Syntax;

namespace AI.Script.Runtime;

/// <summary>
/// Арифметика величин с единицами.
/// </summary>
/// <remarks>
/// Правило то же, что в физике и в бухгалтерии: складываются и сравниваются только величины одной
/// размерности, а умножение и деление составляют новую единицу. Цена в рублях за килограмм,
/// умноженная на килограммы, даёт рубли; рубли плюс килограммы — ошибку, а не число.
/// <para>
/// Сложение печатается в единице левого слагаемого (<c>1 kg + 500 g</c> — это <c>1.5 kg</c>): так
/// пишет человек, прибавляя к своему. Умножение и деление печатаются в базовых единицах: иначе
/// у «рублей на килограмм, умноженных на граммы» не было бы разумного обозначения.
/// </para>
/// </remarks>
internal static class Quantities
{
    /// <summary>Бинарная арифметика, где хотя бы один операнд — величина.</summary>
    public static ScriptValue Arithmetic(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        switch (op)
        {
            case BinaryOperator.Add:
            case BinaryOperator.Subtract:
                return Additive(op, left, right);

            case BinaryOperator.Multiply:
                return Product(left, right, divide: false);

            case BinaryOperator.Divide:
                return Product(left, right, divide: true);

            default:
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"оператор '{OperatorText.Of(op)}' для величин с единицами не определён",
                    "величины складываются, вычитаются, умножаются и делятся; для прочего возьмите число: qty.value(x, \"kg\")");
        }
    }

    /// <summary>Порядок двух величин одной размерности.</summary>
    public static int Compare(ScriptValue left, ScriptValue right, string operation)
    {
        Require(left.AsUnit(), right.AsUnit(), operation);

        return left.RawNumber.CompareTo(right.RawNumber);
    }

    private static ScriptValue Additive(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        if (left.Type != ScriptType.Qty || right.Type != ScriptType.Qty)
        {
            ScriptValue plain = left.Type == ScriptType.Qty ? right : left;

            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"оператор '{OperatorText.Of(op)}' не складывает величину с единицей и {plain.Type.ToName()} без единицы",
                "у числа укажите единицу: 5 kg, 120 rub; либо переведите величину в число: qty.value(x, \"kg\")");
        }

        MeasureUnit unit = left.AsUnit();

        Require(unit, right.AsUnit(), OperatorText.Of(op));

        double sum = op == BinaryOperator.Add ? left.RawNumber + right.RawNumber : left.RawNumber - right.RawNumber;

        return ScriptValue.Qty(sum, unit);
    }

    private static ScriptValue Product(ScriptValue left, ScriptValue right, bool divide)
    {
        if (left.Type == ScriptType.Qty && right.Type == ScriptType.Qty)
        {
            MeasureUnit unit = divide ? left.AsUnit().Divide(right.AsUnit()) : left.AsUnit().Multiply(right.AsUnit());
            double value = divide ? left.RawNumber / right.RawNumber : left.RawNumber * right.RawNumber;

            // Единицы сократились — это уже просто число: доля, коэффициент, «во сколько раз».
            return unit.IsDimensionless ? ScriptValue.Num(value) : ScriptValue.Qty(value, unit);
        }

        if (left.Type == ScriptType.Qty && right.Type == ScriptType.Num)
        {
            double value = divide ? left.RawNumber / right.RawNumber : left.RawNumber * right.RawNumber;

            return ScriptValue.Qty(value, left.AsUnit());
        }

        if (left.Type == ScriptType.Num && right.Type == ScriptType.Qty)
        {
            if (!divide) return ScriptValue.Qty(left.RawNumber * right.RawNumber, right.AsUnit());

            MeasureUnit inverse = right.AsUnit().Inverse();

            // Число на безразмерную величину (угол, радиан) дает число: у обратной единицы нет
            // ни размерности, ни обозначения, и величина с пустой единицей не читалась бы обратно.
            return inverse.IsDimensionless
                ? ScriptValue.Num(left.RawNumber / right.RawNumber)
                : ScriptValue.Qty(left.RawNumber / right.RawNumber, inverse);
        }

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"оператор '{(divide ? "/" : "*")}' не определён для типов {left.Type.ToName()} и {right.Type.ToName()}",
            "величину умножают и делят на число либо на другую величину");
    }

    private static void Require(MeasureUnit left, MeasureUnit right, string operation)
    {
        if (left.SameDimension(right)) return;

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"оператор '{operation}': {left.Symbol} и {right.Symbol} — разные размерности",
            "складывать и сравнивать можно только величины одной размерности; перевод — qty.to(x, \"g\")");
    }
}

/// <summary>Величина для кода вне языка: число в единицах обозначения и само обозначение.</summary>
/// <param name="Value">Число: у <c>500 g</c> это 500.</param>
/// <param name="Unit">Обозначение единицы: <c>g</c>, <c>rub/kg</c>.</param>
public sealed record QuantityValue(double Value, string Unit)
{
    /// <inheritdoc/>
    public override string ToString() => $"{ScriptFormatter.Number(Value)} {Unit}";
}

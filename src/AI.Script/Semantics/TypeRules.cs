using AI.Script.Runtime;
using AI.Script.Syntax;

namespace AI.Script.Semantics;

/// <summary>
/// Правила типов: совместимость аргументов и результаты операторов.
/// </summary>
/// <remarks>
/// Таблицы здесь повторяют то, что делает <see cref="Operations"/> и
/// <see cref="Binding.Marshaller"/> в рантайме. Дублирование намеренное и ограниченное: проверка
/// обязана предсказывать поведение исполнения, но не может его выполнять. Расхождение между
/// этими таблицами и рантаймом — ошибка, поэтому каждая строка таблицы закрыта тестом.
/// <para>
/// Неизвестный тип обозначается <c>null</c>, а не <see cref="ScriptType.Any"/>: «я не знаю»
/// и «подойдёт что угодно» — разные вещи, и путать их значит либо молчать там, где нужно
/// ругаться, либо ругаться там, где нечего сказать.
/// </para>
/// </remarks>
public static class TypeRules
{
    /// <summary>
    /// Годится ли значение типа <paramref name="argument"/> параметру типа <paramref name="parameter"/>.
    /// </summary>
    /// <remarks>
    /// <c>none</c> подходит всюду: он же значение по умолчанию у необязательных параметров, и
    /// ругаться на явно переданный <c>none</c> значило бы запретить «значение не задано».
    /// </remarks>
    public static bool Accepts(ScriptType parameter, ScriptType argument)
    {
        if (parameter is ScriptType.Any) return true;
        if (argument is ScriptType.Any or ScriptType.None) return true;
        if (parameter == argument) return true;

        return parameter switch
        {
            // Список чисел и диапазон принимаются там, где ждут вектор: единственное неявное
            // приведение языка.
            ScriptType.Vec => argument is ScriptType.List or ScriptType.Range,
            ScriptType.List => argument is ScriptType.Vec or ScriptType.Range,
            ScriptType.Mat => argument is ScriptType.Table or ScriptType.Vec,
            _ => false,
        };
    }

    /// <summary>Тип результата бинарной операции; <c>null</c> — операция не определена.</summary>
    public static ScriptType? Binary(BinaryOperator op, ScriptType left, ScriptType right)
    {
        if (op is BinaryOperator.Equal or BinaryOperator.NotEqual) return ScriptType.Bool;

        if (op is BinaryOperator.And or BinaryOperator.Or)
            return left == ScriptType.Bool && right == ScriptType.Bool ? ScriptType.Bool : null;

        if (OperatorText.IsComparison(op))
        {
            return left == right && left is ScriptType.Num or ScriptType.Str or ScriptType.Date or ScriptType.Dur
                ? ScriptType.Bool
                : null;
        }

        return Arithmetic(op, left, right);
    }

    /// <summary>Тип результата унарной операции; <c>null</c> — операция не определена.</summary>
    public static ScriptType? Unary(UnaryOperator op, ScriptType operand)
    {
        if (op == UnaryOperator.Not) return operand == ScriptType.Bool ? ScriptType.Bool : null;

        return operand switch
        {
            ScriptType.Num => ScriptType.Num,
            ScriptType.Vec => ScriptType.Vec,
            ScriptType.Mat => ScriptType.Mat,
            ScriptType.Dur => ScriptType.Dur,
            _ => null,
        };
    }

    /// <summary>Тип элемента при обходе значения циклом; <c>null</c> — элемент неизвестен.</summary>
    public static ScriptType? ElementOf(ScriptType sequence) => sequence switch
    {
        ScriptType.Vec or ScriptType.Range => ScriptType.Num,
        ScriptType.Str => ScriptType.Str,
        ScriptType.Table => ScriptType.Record,
        _ => null,
    };

    /// <summary>Можно ли вообще пройти по значению такого типа циклом.</summary>
    public static bool IsIterable(ScriptType type) => type
        is ScriptType.List or ScriptType.Vec or ScriptType.Range or ScriptType.Str or ScriptType.Table;

    private static ScriptType? Arithmetic(BinaryOperator op, ScriptType left, ScriptType right)
    {
        if (left == ScriptType.Num && right == ScriptType.Num) return ScriptType.Num;

        if (left == ScriptType.Mat || right == ScriptType.Mat) return MatrixResult(op, left, right);
        if (left == ScriptType.Vec || right == ScriptType.Vec) return VectorResult(op, left, right);

        if (op == BinaryOperator.Add)
        {
            if (left == ScriptType.Str && right == ScriptType.Str) return ScriptType.Str;
            if (left == ScriptType.List && right == ScriptType.List) return ScriptType.List;
        }

        return TemporalResult(op, left, right);
    }

    private static ScriptType? MatrixResult(BinaryOperator op, ScriptType left, ScriptType right)
    {
        if (left == ScriptType.Mat && right == ScriptType.Mat)
        {
            return op is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                ? ScriptType.Mat
                : null;
        }

        if (left == ScriptType.Mat && right == ScriptType.Vec)
            return op == BinaryOperator.Multiply ? ScriptType.Vec : null;

        if (left == ScriptType.Mat && right == ScriptType.Num)
        {
            return op is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                or BinaryOperator.Divide or BinaryOperator.Power
                ? ScriptType.Mat
                : null;
        }

        if (left == ScriptType.Num && right == ScriptType.Mat)
        {
            return op is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                or BinaryOperator.Divide
                ? ScriptType.Mat
                : null;
        }

        return null;
    }

    private static ScriptType? VectorResult(BinaryOperator op, ScriptType left, ScriptType right)
    {
        bool arithmetic = op is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
            or BinaryOperator.Divide or BinaryOperator.Modulo or BinaryOperator.Power;

        if (!arithmetic) return null;

        bool pair = (left == ScriptType.Vec && right is ScriptType.Vec or ScriptType.Num)
            || (left == ScriptType.Num && right == ScriptType.Vec);

        return pair ? ScriptType.Vec : null;
    }

    private static ScriptType? TemporalResult(BinaryOperator op, ScriptType left, ScriptType right)
    {
        if (op == BinaryOperator.Add)
        {
            if (left == ScriptType.Date && right == ScriptType.Dur) return ScriptType.Date;
            if (left == ScriptType.Dur && right == ScriptType.Date) return ScriptType.Date;
            if (left == ScriptType.Dur && right == ScriptType.Dur) return ScriptType.Dur;
        }

        if (op == BinaryOperator.Subtract)
        {
            if (left == ScriptType.Date && right == ScriptType.Date) return ScriptType.Dur;
            if (left == ScriptType.Date && right == ScriptType.Dur) return ScriptType.Date;
            if (left == ScriptType.Dur && right == ScriptType.Dur) return ScriptType.Dur;
        }

        if (left == ScriptType.Dur && right == ScriptType.Num && op is BinaryOperator.Multiply or BinaryOperator.Divide)
            return ScriptType.Dur;

        return null;
    }
}

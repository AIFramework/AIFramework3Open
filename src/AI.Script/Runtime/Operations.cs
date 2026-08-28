using AI.DataStructs.Algebraic;
using AI.Script.Semantics;
using AI.Script.Syntax;

namespace AI.Script.Runtime;

/// <summary>
/// Реализация операторов языка над значениями.
/// </summary>
/// <remarks>
/// Набор допустимых пар типов задан перечислением, а не «попробуем привести»: неявные
/// приведения <c>num</c>↔<c>bool</c>↔<c>str</c> в языке запрещены, и операторы — то место,
/// где этот запрет обязан быть виден. Иначе <c>if count</c> и <c>if count > 0</c> стали бы
/// одинаково допустимыми, а первое почти всегда описка.
/// </remarks>
public static class Operations
{
    /// <summary>Применяет бинарный оператор; <c>&amp;&amp;</c> и <c>||</c> сюда не попадают.</summary>
    public static ScriptValue Binary(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        switch (op)
        {
            case BinaryOperator.Equal: return ScriptValue.Bool(left.Equals(right));
            case BinaryOperator.NotEqual: return ScriptValue.Bool(!left.Equals(right));

            case BinaryOperator.Less:
            case BinaryOperator.Greater:
            case BinaryOperator.LessOrEqual:
            case BinaryOperator.GreaterOrEqual:
                return Compare(op, left, right);

            default:
                return Arithmetic(op, left, right);
        }
    }

    /// <summary>Применяет унарный оператор.</summary>
    public static ScriptValue Unary(UnaryOperator op, ScriptValue operand)
    {
        if (op == UnaryOperator.Not)
        {
            return operand.Type == ScriptType.Bool
                ? ScriptValue.Bool(operand.RawNumber == 0)
                : throw Bad("!", operand.Type, operand.Type);
        }

        return operand.Type switch
        {
            ScriptType.Num => ScriptValue.Num(-operand.RawNumber),
            ScriptType.Vec => ScriptValue.Vec(-operand.AsVector()),
            ScriptType.Mat => ScriptValue.Mat(operand.AsMatrix() * -1.0),
            ScriptType.Dur => ScriptValue.Dur(-operand.AsDuration()),
            _ => throw Bad("-", operand.Type, operand.Type),
        };
    }

    private static ScriptValue Compare(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        if (left.Type != right.Type)
            throw Bad(OperatorText.Of(op), left.Type, right.Type);

        int order = left.Type switch
        {
            ScriptType.Num => left.RawNumber.CompareTo(right.RawNumber),
            ScriptType.Str => string.CompareOrdinal(left.AsString(), right.AsString()),
            ScriptType.Date => left.AsDate().CompareTo(right.AsDate()),
            ScriptType.Dur => left.AsDuration().CompareTo(right.AsDuration()),
            _ => throw Bad(OperatorText.Of(op), left.Type, right.Type),
        };

        return ScriptValue.Bool(op switch
        {
            BinaryOperator.Less => order < 0,
            BinaryOperator.Greater => order > 0,
            BinaryOperator.LessOrEqual => order <= 0,
            _ => order >= 0,
        });
    }

    private static ScriptValue Arithmetic(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        if (left.Type == ScriptType.Num && right.Type == ScriptType.Num)
            return ScriptValue.Num(Scalar(op, left.RawNumber, right.RawNumber));

        if (left.Type == ScriptType.Mat || right.Type == ScriptType.Mat)
            return MatrixArithmetic(op, left, right);

        if (left.Type == ScriptType.Vec || right.Type == ScriptType.Vec)
            return VectorArithmetic(op, left, right);

        if (op == BinaryOperator.Add && left.Type == ScriptType.Str && right.Type == ScriptType.Str)
            return ScriptValue.Str(left.AsString() + right.AsString());

        if (op == BinaryOperator.Add && left.Type == ScriptType.List && right.Type == ScriptType.List)
            return ScriptValue.List(ScriptList.Concat(left.AsList(), right.AsList()));

        return TemporalArithmetic(op, left, right);
    }

    private static ScriptValue TemporalArithmetic(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        if (op == BinaryOperator.Add)
        {
            if (left.Type == ScriptType.Date && right.Type == ScriptType.Dur)
                return ScriptValue.Date(left.AsDate() + right.AsDuration());

            if (left.Type == ScriptType.Dur && right.Type == ScriptType.Date)
                return ScriptValue.Date(right.AsDate() + left.AsDuration());

            if (left.Type == ScriptType.Dur && right.Type == ScriptType.Dur)
                return ScriptValue.Dur(left.AsDuration() + right.AsDuration());
        }

        if (op == BinaryOperator.Subtract)
        {
            if (left.Type == ScriptType.Date && right.Type == ScriptType.Date)
                return ScriptValue.Dur(left.AsDate() - right.AsDate());

            if (left.Type == ScriptType.Date && right.Type == ScriptType.Dur)
                return ScriptValue.Date(left.AsDate() - right.AsDuration());

            if (left.Type == ScriptType.Dur && right.Type == ScriptType.Dur)
                return ScriptValue.Dur(left.AsDuration() - right.AsDuration());
        }

        if (left.Type == ScriptType.Dur && right.Type == ScriptType.Num)
        {
            double factor = right.RawNumber;

            if (op == BinaryOperator.Multiply) return ScriptValue.Dur(left.AsDuration() * factor);
            if (op == BinaryOperator.Divide) return ScriptValue.Dur(left.AsDuration() / factor);
        }

        throw Bad(OperatorText.Of(op), left.Type, right.Type);
    }

    /// <summary>
    /// Арифметика матриц.
    /// </summary>
    /// <remarks>
    /// <c>*</c> для двух матриц — матричное умножение, как в математической записи и в самом
    /// фреймворке; поэлементное произведение даёт <c>mat.hadamard</c>. Для векторов <c>*</c>,
    /// наоборот, поэлементное: вектор здесь — колонка данных, а матрица — линейный оператор.
    /// Различие описано в DESIGN.md §7.1, потому что запомнить его иначе неоткуда.
    /// </remarks>
    private static ScriptValue MatrixArithmetic(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        if (left.Type == ScriptType.Mat && right.Type == ScriptType.Mat)
        {
            Matrix a = left.AsMatrix(), b = right.AsMatrix();

            switch (op)
            {
                case BinaryOperator.Add:
                case BinaryOperator.Subtract:
                    RequireSameShape(a, b, OperatorText.Of(op));
                    return ScriptValue.Mat(op == BinaryOperator.Add ? a + b : a - b);

                case BinaryOperator.Multiply:
                    if (a.Width != b.Height)
                    {
                        throw new ScriptError(
                            DiagnosticCodes.SizeMismatch,
                            $"матрицы {a.Height}×{a.Width} и {b.Height}×{b.Width} не перемножаются",
                            "для произведения число столбцов слева обязано равняться числу строк справа; поэлементное произведение — mat.hadamard");
                    }

                    return ScriptValue.Mat(a * b);

                default:
                    throw Bad(OperatorText.Of(op), ScriptType.Mat, ScriptType.Mat);
            }
        }

        if (left.Type == ScriptType.Mat && right.Type == ScriptType.Vec && op == BinaryOperator.Multiply)
            return ScriptValue.Vec(MultiplyByVector(left.AsMatrix(), right.AsVector()));

        if (left.Type == ScriptType.Mat && right.Type == ScriptType.Num)
        {
            Matrix a = left.AsMatrix();
            double k = right.RawNumber;

            return ScriptValue.Mat(op switch
            {
                BinaryOperator.Add => a + k,
                BinaryOperator.Subtract => a - k,
                BinaryOperator.Multiply => a * k,
                BinaryOperator.Divide => a / k,
                BinaryOperator.Power => a.Transform(x => Math.Pow(x, k)),
                _ => throw Bad(OperatorText.Of(op), ScriptType.Mat, ScriptType.Num),
            });
        }

        if (left.Type == ScriptType.Num && right.Type == ScriptType.Mat)
        {
            double k = left.RawNumber;
            Matrix b = right.AsMatrix();

            return ScriptValue.Mat(op switch
            {
                BinaryOperator.Add => k + b,
                BinaryOperator.Subtract => k - b,
                BinaryOperator.Multiply => k * b,
                BinaryOperator.Divide => k / b,
                _ => throw Bad(OperatorText.Of(op), ScriptType.Num, ScriptType.Mat),
            });
        }

        throw Bad(OperatorText.Of(op), left.Type, right.Type);
    }

    private static Vector MultiplyByVector(Matrix matrix, Vector vector)
    {
        if (matrix.Width != vector.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"матрица {matrix.Height}×{matrix.Width} не умножается на вектор длины {vector.Count}",
                "число столбцов матрицы обязано равняться длине вектора");
        }

        var result = new Vector(matrix.Height);

        for (int i = 0; i < matrix.Height; i++)
        {
            double sum = 0;
            for (int j = 0; j < matrix.Width; j++) sum += matrix[i, j] * vector[j];
            result[i] = sum;
        }

        return result;
    }

    private static void RequireSameShape(Matrix a, Matrix b, string op)
    {
        if (a.Height == b.Height && a.Width == b.Width) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"'{op}': несовместимые размеры матриц {a.Height}×{a.Width} и {b.Height}×{b.Width}");
    }

    private static ScriptValue VectorArithmetic(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        if (left.Type == ScriptType.Vec && right.Type == ScriptType.Vec)
        {
            Vector a = left.AsVector(), b = right.AsVector();

            if (a.Count != b.Count)
            {
                throw new ScriptError(
                    DiagnosticCodes.SizeMismatch,
                    $"несовместимые размеры векторов: {a.Count} и {b.Count}",
                    "поэлементные операции требуют одинаковой длины; выровняйте данные до операции");
            }

            var result = new Vector(a.Count);
            for (int i = 0; i < a.Count; i++) result[i] = Scalar(op, a[i], b[i]);

            return ScriptValue.Vec(result);
        }

        if (left.Type == ScriptType.Vec && right.Type == ScriptType.Num)
        {
            Vector a = left.AsVector();
            double k = right.RawNumber;
            var result = new Vector(a.Count);

            for (int i = 0; i < a.Count; i++) result[i] = Scalar(op, a[i], k);

            return ScriptValue.Vec(result);
        }

        if (left.Type == ScriptType.Num && right.Type == ScriptType.Vec)
        {
            double k = left.RawNumber;
            Vector b = right.AsVector();
            var result = new Vector(b.Count);

            for (int i = 0; i < b.Count; i++) result[i] = Scalar(op, k, b[i]);

            return ScriptValue.Vec(result);
        }

        throw Bad(OperatorText.Of(op), left.Type, right.Type);
    }

    private static double Scalar(BinaryOperator op, double left, double right) => op switch
    {
        BinaryOperator.Add => left + right,
        BinaryOperator.Subtract => left - right,
        BinaryOperator.Multiply => left * right,
        BinaryOperator.Divide => left / right,
        BinaryOperator.Modulo => left % right,
        BinaryOperator.Power => Math.Pow(left, right),
        _ => throw new ScriptError(DiagnosticCodes.BadOperand, $"оператор '{OperatorText.Of(op)}' неприменим к числам"),
    };

    private static ScriptError Bad(string op, ScriptType left, ScriptType right) =>
        new(DiagnosticCodes.BadOperand,
            left == right
                ? $"оператор '{op}' не определён для типа {left.ToName()}"
                : $"оператор '{op}' не определён для типов {left.ToName()} и {right.ToName()}",
            "неявных приведений между num, bool и str в языке нет: приведите значения явно (core.to_num, core.parse_num)");
}

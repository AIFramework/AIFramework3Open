using AI.Script.Runtime;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;

namespace AI.Script.Semantics;

/// <summary>
/// Проверка размерностей до запуска.
/// </summary>
/// <remarks>
/// Тип <c>qty</c> говорит, что у числа есть единица, но не говорит какая: сложение рублей с
/// килограммами проходит проверку типов. Поэтому рядом с типами проверка ведёт единицы тех
/// выражений, где они выводятся из текста, — литералов, имён и арифметики над ними, — и ловит
/// сложение и сравнение разных размерностей до первой строки счёта.
/// <para>
/// Единица неизвестна (пришла из функции, из данных) — проверка молчит, а не угадывает: ложная
/// ошибка размерности в верном расчёте хуже пропущенной, её пришлось бы обходить.
/// </para>
/// </remarks>
public sealed partial class Checker
{
    /// <summary>
    /// Единицы имен по областям видимости, как колонки: параметр лямбды или переменная цикла
    /// заслоняют внешнее имя вместе с его единицей, а область уносит свои единицы с собой.
    /// </summary>
    private readonly Dictionary<Dictionary<string, ScriptType?>, Dictionary<string, MeasureUnit>> _units = new();

    /// <summary>Запоминает единицу имени в текущей области; <c>null</c> — единица неизвестна.</summary>
    private void DeclareUnit(string name, MeasureUnit? unit)
    {
        if (_scopes.Count > 0) SetUnit(_scopes[^1], name, unit);
    }

    /// <summary>
    /// Единица после присваивания.
    /// </summary>
    /// <remarks>
    /// Присваивание в той же области, где имя объявлено, заменяет единицу. Присваивание глубже
    /// (в ветке, в цикле) случается не на каждом пути, поэтому после него единица неизвестна: ни
    /// старая, ни новая не верна на всех путях, а ложная ошибка в верном расчете хуже молчания.
    /// </remarks>
    private void AssignUnit(string name, MeasureUnit? unit)
    {
        for (int i = _scopes.Count - 1; i >= 0; i--)
        {
            if (!_scopes[i].ContainsKey(name)) continue;

            SetUnit(_scopes[i], name, i == _scopes.Count - 1 ? unit : null);
            return;
        }
    }

    private void SetUnit(Dictionary<string, ScriptType?> scope, string name, MeasureUnit? unit)
    {
        if (unit is null)
        {
            if (_units.TryGetValue(scope, out var known)) _ = known.Remove(name);
            return;
        }

        if (!_units.TryGetValue(scope, out var units))
        {
            units = new Dictionary<string, MeasureUnit>(StringComparer.Ordinal);
            _units[scope] = units;
        }

        units[name] = unit;
    }

    /// <summary>Единица имени из той области, где имя объявлено.</summary>
    private MeasureUnit? UnitOfName(string name)
    {
        for (int i = _scopes.Count - 1; i >= 0; i--)
        {
            if (!_scopes[i].ContainsKey(name)) continue;

            return _units.TryGetValue(_scopes[i], out var units) && units.TryGetValue(name, out MeasureUnit? unit) ? unit : null;
        }

        return null;
    }

    /// <summary>
    /// Сложение, вычитание и сравнение порядка у разных размерностей — ошибка проверки.
    /// </summary>
    /// <remarks>
    /// Равенство не проверяется: при запуске <c>5 kg == 5 rub</c> это просто <c>false</c>, и
    /// проверка не должна запрещать то, что исполнение считает допустимым.
    /// </remarks>
    private void CheckDimensions(BinaryExpr binary)
    {
        bool additive = binary.Operator is BinaryOperator.Add or BinaryOperator.Subtract;
        bool ordering = OperatorText.IsComparison(binary.Operator)
            && binary.Operator is not (BinaryOperator.Equal or BinaryOperator.NotEqual);

        if (!additive && !ordering) return;

        if (UnitOf(binary.Left) is not { } left || UnitOf(binary.Right) is not { } right) return;

        if (left.SameDimension(right)) return;

        _diagnostics.Error(DiagnosticCodes.BadOperandTypes,
            binary.OperatorSpan.Length > 0 ? binary.OperatorSpan : binary.Span,
            $"оператор '{OperatorText.Of(binary.Operator)}': {left.Symbol} и {right.Symbol} — разные размерности",
            "складываются и сравниваются только величины одной размерности; умножение и деление составляют новую единицу");
    }

    /// <summary>Единица выражения, если она выводится из текста; иначе <c>null</c>.</summary>
    private MeasureUnit? UnitOf(Expr expression) => expression switch
    {
        LiteralExpr { Value.Type: ScriptType.Qty } literal => literal.Value.AsUnit(),
        NameExpr name => UnitOfName(name.Name),
        UnaryExpr unary => UnitOf(unary.Operand),
        BinaryExpr { Operator: BinaryOperator.Add or BinaryOperator.Subtract } binary => UnitOf(binary.Left),
        BinaryExpr { Operator: BinaryOperator.Multiply or BinaryOperator.Divide } binary => Product(binary),
        _ => null,
    };

    /// <summary>Единица произведения либо частного: число единицы не меняет.</summary>
    private MeasureUnit? Product(BinaryExpr binary)
    {
        bool divide = binary.Operator == BinaryOperator.Divide;
        MeasureUnit? left = UnitOf(binary.Left);
        MeasureUnit? right = UnitOf(binary.Right);

        if (left is not null && right is not null)
        {
            MeasureUnit unit = divide ? left.Divide(right) : left.Multiply(right);

            return unit.IsDimensionless ? null : unit;
        }

        if (left is not null && InferQuietly(binary.Right) == ScriptType.Num) return left;

        if (right is not null && InferQuietly(binary.Left) == ScriptType.Num)
            return !divide ? right : right.Inverse() is { IsDimensionless: false } inverse ? inverse : null;

        return null;
    }
}

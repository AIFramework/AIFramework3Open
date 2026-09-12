using System.Globalization;
using System.Text;

namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Линейное выражение над числовыми переменными: <c>Σ aᵢxᵢ + c</c>.
/// </summary>
/// <remarks>
/// Выражения складываются, вычитаются и умножаются на числа обычными операторами, а сравнения
/// <c>&lt;=</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&gt;</c> дают не <c>bool</c>, а атом формулы
/// <see cref="SmtFormula"/>. Равенство — <see cref="SmtFormula.Equal"/>: оператор <c>==</c>
/// не переопределён, чтобы не ломать сравнение ссылок.
/// </remarks>
public class LinearExpression
{
    private readonly Dictionary<NumericVariable, double> _terms;

    internal LinearExpression(Dictionary<NumericVariable, double> terms, double constant)
    {
        if (!double.IsFinite(constant))
            throw new ArgumentOutOfRangeException(nameof(constant), "Свободный член должен быть конечным числом");

        _terms = terms;
        Constant = constant;
    }

    /// <summary>Свободный член</summary>
    public double Constant { get; }

    /// <summary>Переменные с ненулевыми коэффициентами</summary>
    public virtual IReadOnlyDictionary<NumericVariable, double> Terms => _terms;

    /// <summary>Число как выражение</summary>
    /// <param name="value">Число</param>
    public static implicit operator LinearExpression(double value) => new(new Dictionary<NumericVariable, double>(), value);

    /// <summary>Сумма</summary>
    /// <param name="left">Первое слагаемое</param>
    /// <param name="right">Второе слагаемое</param>
    public static LinearExpression operator +(LinearExpression left, LinearExpression right) => Combine(left, right, 1);

    /// <summary>Разность</summary>
    /// <param name="left">Уменьшаемое</param>
    /// <param name="right">Вычитаемое</param>
    public static LinearExpression operator -(LinearExpression left, LinearExpression right) => Combine(left, right, -1);

    /// <summary>Смена знака</summary>
    /// <param name="value">Выражение</param>
    public static LinearExpression operator -(LinearExpression value) => Scale(value, -1);

    /// <summary>Умножение на число</summary>
    /// <param name="value">Выражение</param>
    /// <param name="factor">Множитель</param>
    public static LinearExpression operator *(LinearExpression value, double factor) => Scale(value, factor);

    /// <summary>Умножение на число</summary>
    /// <param name="factor">Множитель</param>
    /// <param name="value">Выражение</param>
    public static LinearExpression operator *(double factor, LinearExpression value) => Scale(value, factor);

    /// <summary>Атом <c>left ≤ right</c></summary>
    /// <param name="left">Левая часть</param>
    /// <param name="right">Правая часть</param>
    public static SmtFormula operator <=(LinearExpression left, LinearExpression right)
        => SmtFormula.Compare(left - right, strict: false);

    /// <summary>Атом <c>left ≥ right</c></summary>
    /// <param name="left">Левая часть</param>
    /// <param name="right">Правая часть</param>
    public static SmtFormula operator >=(LinearExpression left, LinearExpression right)
        => SmtFormula.Compare(right - left, strict: false);

    /// <summary>Атом <c>left &lt; right</c></summary>
    /// <param name="left">Левая часть</param>
    /// <param name="right">Правая часть</param>
    public static SmtFormula operator <(LinearExpression left, LinearExpression right)
        => SmtFormula.Compare(left - right, strict: true);

    /// <summary>Атом <c>left &gt; right</c></summary>
    /// <param name="left">Левая часть</param>
    /// <param name="right">Правая часть</param>
    public static SmtFormula operator >(LinearExpression left, LinearExpression right)
        => SmtFormula.Compare(right - left, strict: true);

    /// <summary>Сумма нескольких выражений</summary>
    /// <param name="items">Слагаемые</param>
    public static LinearExpression Sum(IEnumerable<LinearExpression> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        LinearExpression total = 0;

        foreach (LinearExpression item in items)
            total += item;

        return total;
    }

    /// <summary>Запись выражения</summary>
    public override string ToString()
    {
        var text = new StringBuilder();

        foreach ((NumericVariable variable, double coefficient) in Terms.OrderBy(t => t.Key.Index))
        {
            text.Append(text.Length == 0 ? (coefficient < 0 ? "−" : string.Empty) : (coefficient < 0 ? " − " : " + "));

            double magnitude = Math.Abs(coefficient);

            if (magnitude != 1)
                text.Append(magnitude.ToString("G6", CultureInfo.InvariantCulture)).Append('·');

            text.Append(variable.Name);
        }

        if (Constant != 0 || text.Length == 0)
        {
            text.Append(text.Length == 0 ? (Constant < 0 ? "−" : string.Empty) : (Constant < 0 ? " − " : " + "));
            text.Append(Math.Abs(Constant).ToString("G6", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static LinearExpression Combine(LinearExpression left, LinearExpression right, double sign)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var terms = new Dictionary<NumericVariable, double>(left.Terms);

        foreach ((NumericVariable variable, double coefficient) in right.Terms)
        {
            double value = (terms.TryGetValue(variable, out double existing) ? existing : 0) + (sign * coefficient);

            if (value == 0)
                terms.Remove(variable);
            else
                terms[variable] = value;
        }

        return new LinearExpression(terms, left.Constant + (sign * right.Constant));
    }

    private static LinearExpression Scale(LinearExpression value, double factor)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!double.IsFinite(factor))
            throw new ArgumentOutOfRangeException(nameof(factor), "Множитель должен быть конечным числом");

        var terms = new Dictionary<NumericVariable, double>();

        if (factor != 0)
        {
            foreach ((NumericVariable variable, double coefficient) in value.Terms)
                terms[variable] = coefficient * factor;
        }

        return new LinearExpression(terms, value.Constant * factor);
    }
}

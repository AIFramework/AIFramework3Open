using System.Globalization;
using System.Text;

namespace AI.Solvers.Constraints.Smt;

/// <summary>
/// Арифметический атом в каноническом виде: <c>Σ aᵢxᵢ ≤ b</c> или <c>Σ aᵢxᵢ &lt; b</c>,
/// переменные упорядочены, первый коэффициент положителен
/// </summary>
/// <remarks>
/// Канонический вид делает атом и его отрицание одной булевой переменной с разными знаками:
/// <c>x − y ≤ 3</c> и <c>y − x &lt; −3</c> — это один атом, взятый с разной полярностью.
/// Иначе булева часть не знала бы, что они исключают друг друга, и узнавала бы это только
/// от арифметики — ценой лишних лемм.
/// </remarks>
public sealed class ArithmeticAtom
{
    internal ArithmeticAtom(IReadOnlyList<(NumericVariable Variable, double Coefficient)> terms, bool strict, double bound)
    {
        Terms = terms;
        IsStrict = strict;
        Bound = bound;

        var key = new StringBuilder();

        foreach ((NumericVariable variable, double coefficient) in terms)
            key.Append(variable.Index).Append(':').Append(coefficient.ToString("R", CultureInfo.InvariantCulture)).Append(';');

        key.Append(strict ? '<' : '≤').Append(bound.ToString("R", CultureInfo.InvariantCulture));
        Key = key.ToString();
    }

    /// <summary>Слагаемые левой части, упорядоченные по номеру переменной</summary>
    public IReadOnlyList<(NumericVariable Variable, double Coefficient)> Terms { get; }

    /// <summary>Строгое ли неравенство</summary>
    public bool IsStrict { get; }

    /// <summary>Правая часть</summary>
    public double Bound { get; }

    internal string Key { get; }

    /// <summary>Выполняется ли атом при заданных значениях переменных</summary>
    /// <param name="value">Значение каждой переменной</param>
    /// <param name="tolerance">Допуск для нестрогого неравенства</param>
    public bool IsSatisfiedBy(Func<NumericVariable, double> value, double tolerance = 1e-7)
    {
        ArgumentNullException.ThrowIfNull(value);

        double left = 0;

        foreach ((NumericVariable variable, double coefficient) in Terms)
            left += coefficient * value(variable);

        return IsStrict ? left < Bound : left <= Bound + (tolerance * (1 + Math.Abs(Bound)));
    }

    /// <summary>Запись атома</summary>
    public override string ToString()
    {
        var text = new StringBuilder();

        foreach ((NumericVariable variable, double coefficient) in Terms)
        {
            text.Append(text.Length == 0 ? (coefficient < 0 ? "−" : string.Empty) : (coefficient < 0 ? " − " : " + "));

            double magnitude = Math.Abs(coefficient);

            if (magnitude != 1)
                text.Append(magnitude.ToString("G6", CultureInfo.InvariantCulture)).Append('·');

            text.Append(variable.Name);
        }

        return text.Append(IsStrict ? " < " : " ≤ ").Append(Bound.ToString("G6", CultureInfo.InvariantCulture)).ToString();
    }
}

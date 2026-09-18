using System.Globalization;
using System.Text;

namespace AI.Script.Runtime;

/// <summary>
/// Единица величины: размерность, обозначение и множитель к базовой единице.
/// </summary>
/// <remarks>
/// Расчёт себестоимости теряет единицы при переносе чисел между шагами: «120» — это рубли за
/// килограмм или за тонну, и на третьем шаге этого уже не видно. Единица едет вместе с числом, и
/// сложение рублей с килограммами становится ошибкой, а не правдоподобным итогом.
/// <para>
/// Величина хранит число в БАЗОВОЙ единице (килограмм, метр, штука, рубль), а обозначение — только
/// для печати. Поэтому <c>1 kg + 500 g</c> складывается без пересчёта по месту: оба уже в
/// килограммах.
/// </para>
/// <para>
/// Валюты — разные размерности, а не одна «деньги»: курса у языка нет, и складывать рубли с
/// долларами он не должен так же, как рубли с килограммами.
/// </para>
/// </remarks>
public sealed class MeasureUnit : IEquatable<MeasureUnit>
{
    private readonly SortedDictionary<string, int> _dimensions;

    private MeasureUnit(IReadOnlyDictionary<string, int> dimensions, string symbol, double scale)
    {
        _dimensions = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var pair in dimensions)
        {
            if (pair.Value != 0) _dimensions[pair.Key] = pair.Value;
        }

        Symbol = symbol;
        Scale = scale;
    }

    /// <summary>Обозначение для печати: <c>kg</c>, <c>g</c>, <c>rub/kg</c>.</summary>
    public string Symbol { get; }

    /// <summary>Сколько базовых единиц в одной единице обозначения: у грамма — 0,001.</summary>
    public double Scale { get; }

    /// <summary>Размерность: базовая единица — степень.</summary>
    public IReadOnlyDictionary<string, int> Dimensions => _dimensions;

    /// <summary>Безразмерная ли величина: единицы сократились.</summary>
    public bool IsDimensionless => _dimensions.Count == 0;

    /// <summary>Одна ли у единиц размерность: только тогда величины складываются и сравниваются.</summary>
    public bool SameDimension(MeasureUnit other) =>
        _dimensions.Count == other._dimensions.Count
        && _dimensions.All(pair => other._dimensions.TryGetValue(pair.Key, out int power) && power == pair.Value);

    /// <summary>Единица произведения — в базовых единицах.</summary>
    public MeasureUnit Multiply(MeasureUnit other) => Combine(other, 1);

    /// <summary>Единица частного — в базовых единицах.</summary>
    public MeasureUnit Divide(MeasureUnit other) => Combine(other, -1);

    /// <summary>Единица, обратная этой: для «число / величина».</summary>
    public MeasureUnit Inverse() => Canonical(_dimensions.ToDictionary(pair => pair.Key, pair => -pair.Value));

    /// <summary>Та же размерность в базовых единицах: так печатается итог умножения и деления.</summary>
    public static MeasureUnit Canonical(IReadOnlyDictionary<string, int> dimensions)
    {
        var up = new StringBuilder();
        var down = new StringBuilder();

        foreach (var pair in dimensions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value == 0) continue;

            StringBuilder side = pair.Value > 0 ? up : down;
            int power = Math.Abs(pair.Value);

            if (side.Length > 0) _ = side.Append('·');

            _ = side.Append(pair.Key);

            if (power > 1) _ = side.Append(power.ToString(CultureInfo.InvariantCulture));
        }

        string symbol = down.Length == 0
            ? up.ToString()
            : $"{(up.Length == 0 ? "1" : up.ToString())}/{down}";

        return new MeasureUnit(dimensions, symbol, 1);
    }

    /// <summary>
    /// Разбирает обозначение: простое (<c>kg</c>, <c>кг</c>) либо составное (<c>rub/kg</c>).
    /// </summary>
    public static bool TryParse(string symbol, out MeasureUnit unit)
    {
        unit = null!;

        if (string.IsNullOrWhiteSpace(symbol)) return false;

        string text = symbol.Trim();

        if (UnitCatalog.TryGet(text, out unit)) return true;

        string[] halves = text.Split('/');

        if (halves.Length > 2) return false;

        var dimensions = new Dictionary<string, int>(StringComparer.Ordinal);

        if (!Accumulate(halves[0], 1, dimensions)) return false;
        if (halves.Length == 2 && !Accumulate(halves[1], -1, dimensions)) return false;

        unit = Canonical(dimensions);

        return true;
    }

    /// <summary>Единица с произвольной размерностью, обозначением и множителем.</summary>
    internal static MeasureUnit From(IReadOnlyDictionary<string, int> dimensions, string symbol, double scale) =>
        new(dimensions, symbol, scale);

    /// <summary>Простая единица справочника.</summary>
    internal static MeasureUnit Simple(string symbol, string dimension, int power, double scale) =>
        new(new Dictionary<string, int> { [dimension] = power }, symbol, scale);

    /// <summary>Та же единица под другим обозначением той же размерности.</summary>
    internal MeasureUnit As(string symbol, double scale) => new(_dimensions, symbol, scale);

    /// <inheritdoc/>
    public bool Equals(MeasureUnit? other) =>
        other is not null && SameDimension(other) && Symbol == other.Symbol && Scale.Equals(other.Scale);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as MeasureUnit);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Symbol, Scale);

    /// <inheritdoc/>
    public override string ToString() => Symbol;

    private MeasureUnit Combine(MeasureUnit other, int sign)
    {
        var dimensions = new Dictionary<string, int>(_dimensions, StringComparer.Ordinal);

        foreach (var pair in other._dimensions)
            dimensions[pair.Key] = dimensions.GetValueOrDefault(pair.Key) + (sign * pair.Value);

        return Canonical(dimensions);
    }

    private static bool Accumulate(string half, int sign, Dictionary<string, int> dimensions)
    {
        foreach (string part in half.Split('·', '*'))
        {
            string name = part.Trim();
            int power = 1;
            int digits = name.Length;

            while (digits > 0 && char.IsAsciiDigit(name[digits - 1])) digits--;

            if (digits < name.Length)
            {
                power = int.Parse(name[digits..], CultureInfo.InvariantCulture);
                name = name[..digits];
            }

            if (name == "1" && sign > 0) continue;

            if (!UnitCatalog.TryGet(name, out MeasureUnit unit) || unit.Scale != 1 || unit._dimensions.Count != 1) return false;

            foreach (var pair in unit._dimensions)
                dimensions[pair.Key] = dimensions.GetValueOrDefault(pair.Key) + (sign * power * pair.Value);
        }

        return true;
    }
}


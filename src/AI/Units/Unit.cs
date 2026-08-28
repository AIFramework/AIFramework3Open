#nullable enable
using System;

namespace AI.Units;

/// <summary>
/// Единица измерения: символ, размерность и правило перевода в базовые единицы СИ.
/// Перевод задаётся как <c>si = value · Factor + Offset</c>; ненулевое смещение имеют
/// только шкальные (аффинные) единицы вроде градуса Цельсия.
/// </summary>
[Serializable]
public sealed class Unit : IEquatable<Unit>
{
    /// <summary>
    /// Символ единицы, например «m», «kW», «°C»
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Размерность величины, измеряемой этой единицей
    /// </summary>
    public Dimension Dimension { get; }

    /// <summary>
    /// Множитель перевода в базовые единицы СИ
    /// </summary>
    public double Factor { get; }

    /// <summary>
    /// Аддитивное смещение в базовых единицах СИ (нуль для всех мультипликативных единиц)
    /// </summary>
    public double Offset { get; }

    /// <summary>
    /// Допустимы ли десятичные приставки СИ для этой единицы
    /// </summary>
    public bool AllowPrefix { get; }

    /// <summary>
    /// Признак аффинной единицы со сдвинутым нулём (°C, °F)
    /// </summary>
    public bool IsAffine => Offset != 0.0;

    /// <summary>
    /// Создаёт единицу измерения
    /// </summary>
    /// <param name="symbol">Символ единицы</param>
    /// <param name="dimension">Размерность</param>
    /// <param name="factor">Множитель перевода в СИ</param>
    /// <param name="offset">Смещение в СИ (для аффинных шкал)</param>
    /// <param name="allowPrefix">Разрешить десятичные приставки</param>
    public Unit(string symbol, Dimension dimension, double factor = 1.0, double offset = 0.0, bool allowPrefix = true)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Символ единицы не может быть пустым", nameof(symbol));

        if (factor == 0.0 || double.IsNaN(factor) || double.IsInfinity(factor))
            throw new ArgumentOutOfRangeException(nameof(factor), "Множитель перевода должен быть конечным и ненулевым");

        Symbol = symbol;
        Dimension = dimension;
        Factor = factor;
        Offset = offset;
        AllowPrefix = allowPrefix;
    }

    /// <summary>
    /// Безразмерная единица (множитель 1)
    /// </summary>
    public static Unit One { get; } = new("1", Dimension.None, 1.0, 0.0, false);

    #region Перевод

    /// <summary>
    /// Переводит значение из этой единицы в базовые единицы СИ
    /// </summary>
    /// <param name="value">Значение в этой единице</param>
    public double ToSi(double value) => (value * Factor) + Offset;

    /// <summary>
    /// Переводит значение из базовых единиц СИ в эту единицу
    /// </summary>
    /// <param name="siValue">Значение в СИ</param>
    public double FromSi(double siValue) => (siValue - Offset) / Factor;

    #endregion

    #region Композиция

    /// <summary>
    /// Произведение единиц
    /// </summary>
    /// <exception cref="InvalidOperationException">Одна из единиц аффинная</exception>
    public static Unit operator *(Unit a, Unit b)
    {
        EnsureLinear(a);
        EnsureLinear(b);
        return new Unit($"{a.Symbol}·{b.Symbol}", a.Dimension * b.Dimension, a.Factor * b.Factor, 0.0, false);
    }

    /// <summary>
    /// Частное единиц
    /// </summary>
    /// <exception cref="InvalidOperationException">Одна из единиц аффинная</exception>
    public static Unit operator /(Unit a, Unit b)
    {
        EnsureLinear(a);
        EnsureLinear(b);
        return new Unit($"{a.Symbol}/{b.Symbol}", a.Dimension / b.Dimension, a.Factor / b.Factor, 0.0, false);
    }

    /// <summary>
    /// Кратная единица: числовой множитель слева
    /// </summary>
    public static Unit operator *(double scale, Unit unit)
    {
        EnsureLinear(unit);
        return new Unit($"{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}·{unit.Symbol}",
            unit.Dimension, scale * unit.Factor, 0.0, false);
    }

    /// <summary>
    /// Возведение единицы в целую степень
    /// </summary>
    /// <param name="exponent">Показатель степени</param>
    /// <exception cref="InvalidOperationException">Единица аффинная</exception>
    public Unit Pow(int exponent)
    {
        EnsureLinear(this);
        string symbol = exponent == 1 ? Symbol : $"{Symbol}^{exponent.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return new Unit(symbol, Dimension.Pow(exponent), Math.Pow(Factor, exponent), 0.0, false);
    }

    /// <summary>
    /// Единица с изменённым символом (для читаемого вывода составных единиц)
    /// </summary>
    /// <param name="symbol">Новый символ</param>
    public Unit WithSymbol(string symbol) => new(symbol, Dimension, Factor, Offset, false);

    private static void EnsureLinear(Unit unit)
    {
        if (unit.IsAffine)
            throw new InvalidOperationException($"Единица «{unit.Symbol}» имеет сдвинутый нуль и не участвует в композиции единиц");
    }

    #endregion

    #region Равенство и представление

    /// <summary>
    /// Сравнение единиц по размерности, множителю и смещению (символ не учитывается)
    /// </summary>
    public bool Equals(Unit? other)
    {
        return other is not null
            && Dimension == other.Dimension
            && Factor.Equals(other.Factor)
            && Offset.Equals(other.Offset);
    }

    /// <summary>
    /// Сравнение с произвольным объектом
    /// </summary>
    public override bool Equals(object? obj) => Equals(obj as Unit);

    /// <summary>
    /// Хеш-код единицы
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(Dimension, Factor, Offset);

    /// <summary>
    /// Символ единицы
    /// </summary>
    public override string ToString() => Symbol;

    #endregion
}

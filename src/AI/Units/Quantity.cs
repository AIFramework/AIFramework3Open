#nullable enable
using System;
using System.Globalization;

namespace AI.Units;

/// <summary>
/// Физическая величина — числовое значение вместе с размерностью. Значение хранится
/// в базовых единицах СИ, поэтому величины из разных источников складываются и сравниваются
/// напрямую, а несовпадение размерностей обнаруживается на месте операции.
/// </summary>
[Serializable]
public readonly struct Quantity : IEquatable<Quantity>, IComparable<Quantity>, IFormattable
{
    /// <summary>
    /// Значение в базовых единицах СИ
    /// </summary>
    public double SiValue { get; }

    /// <summary>
    /// Размерность величины
    /// </summary>
    public Dimension Dimension { get; }

    /// <summary>
    /// Создаёт величину по значению в базовых единицах СИ
    /// </summary>
    /// <param name="siValue">Значение в СИ</param>
    /// <param name="dimension">Размерность</param>
    public Quantity(double siValue, Dimension dimension)
    {
        SiValue = siValue;
        Dimension = dimension;
    }

    #region Создание

    /// <summary>
    /// Создаёт величину по значению в заданной единице
    /// </summary>
    /// <param name="value">Значение в единице <paramref name="unit"/></param>
    /// <param name="unit">Единица измерения</param>
    public static Quantity Of(double value, Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return new Quantity(unit.ToSi(value), unit.Dimension);
    }

    /// <summary>
    /// Создаёт величину по значению и символьной записи единицы, например <c>Of(120, "km/h")</c>
    /// </summary>
    /// <param name="value">Значение</param>
    /// <param name="unit">Символьная запись единицы</param>
    public static Quantity Of(double value, string unit) => Of(value, UnitRegistry.Parse(unit));

    /// <summary>
    /// Безразмерная величина
    /// </summary>
    /// <param name="value">Значение</param>
    public static Quantity Dimensionless(double value) => new(value, Dimension.None);

    /// <summary>
    /// Нулевая величина заданной размерности
    /// </summary>
    /// <param name="dimension">Размерность</param>
    public static Quantity Zero(Dimension dimension) => new(0.0, dimension);

    /// <summary>
    /// Разбирает запись вида «9.81 m/s^2». Разделитель дробной части — точка (инвариантная культура).
    /// </summary>
    /// <param name="text">Разбираемая запись</param>
    /// <exception cref="FormatException">Запись не распознана</exception>
    public static Quantity Parse(string text)
    {
        return TryParse(text, out Quantity result)
            ? result
            : throw new FormatException($"Не удалось разобрать величину «{text}»");
    }

    /// <summary>
    /// Пытается разобрать запись вида «9.81 m/s^2»
    /// </summary>
    /// <param name="text">Разбираемая запись</param>
    /// <param name="result">Разобранная величина</param>
    public static bool TryParse(string? text, out Quantity result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string source = text.Trim();
        int split = 0;

        while (split < source.Length && (char.IsDigit(source[split]) || source[split] is '+' or '-' or '.' or 'e' or 'E'))
        {
            if (source[split] is 'e' or 'E' && split + 1 < source.Length && source[split + 1] is '+' or '-')
                split++;

            split++;
        }

        if (split == 0)
            return false;

        if (!double.TryParse(source[..split], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return false;

        string tail = source[split..].Trim();

        if (tail.Length == 0)
        {
            result = Dimensionless(value);
            return true;
        }

        if (!UnitRegistry.TryParse(tail, out Unit? unit))
            return false;

        result = Of(value, unit!);
        return true;
    }

    #endregion

    #region Чтение значения

    /// <summary>
    /// Значение величины в заданной единице
    /// </summary>
    /// <param name="unit">Целевая единица</param>
    /// <exception cref="DimensionMismatchException">Размерность единицы не совпадает с размерностью величины</exception>
    public double In(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.Dimension != Dimension)
            throw new DimensionMismatchException(unit.Dimension, Dimension, $"перевод в «{unit.Symbol}»");

        return unit.FromSi(SiValue);
    }

    /// <summary>
    /// Значение величины в единице, заданной символьной записью
    /// </summary>
    /// <param name="unit">Символьная запись единицы</param>
    public double In(string unit) => In(UnitRegistry.Parse(unit));

    /// <summary>
    /// Проверяет размерность и возвращает значение в СИ. Предназначен для проверки
    /// аргументов на границе публичного API.
    /// </summary>
    /// <param name="expected">Ожидаемая размерность</param>
    /// <param name="paramName">Имя проверяемого параметра</param>
    /// <exception cref="DimensionMismatchException">Размерность не совпала</exception>
    public double RequireSi(Dimension expected, string? paramName = null)
    {
        if (Dimension != expected)
            throw new DimensionMismatchException(expected, Dimension, paramName);

        return SiValue;
    }

    /// <summary>
    /// Значение безразмерной величины
    /// </summary>
    /// <exception cref="DimensionMismatchException">Величина имеет размерность</exception>
    public double Value => RequireSi(Dimension.None);

    /// <summary>
    /// Признак совместимости размерностей (величины можно складывать и сравнивать)
    /// </summary>
    /// <param name="other">Вторая величина</param>
    public bool IsCompatibleWith(Quantity other) => Dimension == other.Dimension;

    #endregion

    #region Арифметика

    /// <summary>
    /// Сумма величин одинаковой размерности
    /// </summary>
    public static Quantity operator +(Quantity a, Quantity b)
    {
        EnsureSameDimension(a, b, "сложение");
        return new Quantity(a.SiValue + b.SiValue, a.Dimension);
    }

    /// <summary>
    /// Разность величин одинаковой размерности
    /// </summary>
    public static Quantity operator -(Quantity a, Quantity b)
    {
        EnsureSameDimension(a, b, "вычитание");
        return new Quantity(a.SiValue - b.SiValue, a.Dimension);
    }

    /// <summary>
    /// Смена знака величины
    /// </summary>
    public static Quantity operator -(Quantity a) => new(-a.SiValue, a.Dimension);

    /// <summary>
    /// Произведение величин: значения перемножаются, размерности складываются
    /// </summary>
    public static Quantity operator *(Quantity a, Quantity b) => new(a.SiValue * b.SiValue, a.Dimension * b.Dimension);

    /// <summary>
    /// Частное величин: значения делятся, размерности вычитаются
    /// </summary>
    public static Quantity operator /(Quantity a, Quantity b) => new(a.SiValue / b.SiValue, a.Dimension / b.Dimension);

    /// <summary>
    /// Умножение величины на безразмерный множитель
    /// </summary>
    public static Quantity operator *(Quantity a, double k) => new(a.SiValue * k, a.Dimension);

    /// <summary>
    /// Умножение безразмерного множителя на величину
    /// </summary>
    public static Quantity operator *(double k, Quantity a) => new(k * a.SiValue, a.Dimension);

    /// <summary>
    /// Деление величины на безразмерный делитель
    /// </summary>
    public static Quantity operator /(Quantity a, double k) => new(a.SiValue / k, a.Dimension);

    /// <summary>
    /// Деление безразмерного числа на величину
    /// </summary>
    public static Quantity operator /(double k, Quantity a) => new(k / a.SiValue, Dimension.None / a.Dimension);

    /// <summary>
    /// Возведение величины в целую степень
    /// </summary>
    /// <param name="exponent">Показатель степени</param>
    public Quantity Pow(int exponent) => new(Math.Pow(SiValue, exponent), Dimension.Pow(exponent));

    /// <summary>
    /// Квадратный корень из величины
    /// </summary>
    public Quantity Sqrt() => new(Math.Sqrt(SiValue), Dimension.Sqrt());

    /// <summary>
    /// Модуль величины
    /// </summary>
    public Quantity Abs() => new(Math.Abs(SiValue), Dimension);

    /// <summary>
    /// Неявное преобразование безразмерного числа в величину
    /// </summary>
    /// <param name="value">Значение</param>
    public static implicit operator Quantity(double value) => Dimensionless(value);

    private static void EnsureSameDimension(Quantity a, Quantity b, string operation)
    {
        if (a.Dimension != b.Dimension)
            throw new DimensionMismatchException(a.Dimension, b.Dimension, operation);
    }

    #endregion

    #region Сравнение

    /// <summary>
    /// Сравнение величин одинаковой размерности
    /// </summary>
    /// <param name="other">Вторая величина</param>
    public int CompareTo(Quantity other)
    {
        EnsureSameDimension(this, other, "сравнение");
        return SiValue.CompareTo(other.SiValue);
    }

    /// <summary>Величина строго меньше</summary>
    public static bool operator <(Quantity a, Quantity b) => a.CompareTo(b) < 0;

    /// <summary>Величина строго больше</summary>
    public static bool operator >(Quantity a, Quantity b) => a.CompareTo(b) > 0;

    /// <summary>Величина меньше либо равна</summary>
    public static bool operator <=(Quantity a, Quantity b) => a.CompareTo(b) <= 0;

    /// <summary>Величина больше либо равна</summary>
    public static bool operator >=(Quantity a, Quantity b) => a.CompareTo(b) >= 0;

    /// <summary>
    /// Точное равенство значения и размерности
    /// </summary>
    /// <param name="other">Вторая величина</param>
    public bool Equals(Quantity other) => Dimension == other.Dimension && SiValue.Equals(other.SiValue);

    /// <summary>
    /// Приближённое равенство с относительным допуском
    /// </summary>
    /// <param name="other">Вторая величина</param>
    /// <param name="relativeTolerance">Относительный допуск</param>
    public bool AlmostEquals(Quantity other, double relativeTolerance = 1e-9)
    {
        if (Dimension != other.Dimension)
            return false;

        double scale = Math.Max(Math.Abs(SiValue), Math.Abs(other.SiValue));
        return Math.Abs(SiValue - other.SiValue) <= relativeTolerance * Math.Max(scale, 1e-300);
    }

    /// <summary>
    /// Сравнение с произвольным объектом
    /// </summary>
    public override bool Equals(object? obj) => obj is Quantity other && Equals(other);

    /// <summary>
    /// Хеш-код величины
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(SiValue, Dimension);

    /// <summary>Равенство величин</summary>
    public static bool operator ==(Quantity a, Quantity b) => a.Equals(b);

    /// <summary>Неравенство величин</summary>
    public static bool operator !=(Quantity a, Quantity b) => !a.Equals(b);

    #endregion

    #region Представление

    /// <summary>
    /// Строковое представление в единице вывода по умолчанию, например «9.81 m/s²»
    /// </summary>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>
    /// Строковое представление с заданным форматом числа
    /// </summary>
    /// <param name="format">Формат числа</param>
    /// <param name="formatProvider">Поставщик форматирования</param>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Unit unit = UnitRegistry.DisplayUnitFor(Dimension);
        return ToString(unit, format, formatProvider);
    }

    /// <summary>
    /// Строковое представление в заданной единице
    /// </summary>
    /// <param name="unit">Единица вывода</param>
    /// <param name="format">Формат числа</param>
    /// <param name="formatProvider">Поставщик форматирования</param>
    public string ToString(Unit unit, string? format = null, IFormatProvider? formatProvider = null)
    {
        ArgumentNullException.ThrowIfNull(unit);

        IFormatProvider provider = formatProvider ?? CultureInfo.InvariantCulture;
        string value = In(unit).ToString(format, provider);
        return Dimension.IsDimensionless && unit.Equals(Unit.One) ? value : $"{value} {unit.Symbol}";
    }

    #endregion
}

#nullable enable
using AI.DataStructs.Algebraic;
using System;
using System.Collections;
using System.Collections.Generic;

namespace AI.Units;

/// <summary>
/// Ряд однородных величин: вектор значений в базовых единицах СИ с общей размерностью.
/// Служит границей между размерным миром и алгоритмами фреймворка, которые работают
/// с безразмерным <see cref="Vector"/>: размерность проверяется один раз при входе
/// и восстанавливается при выходе.
/// </summary>
/// <example>
/// <code>
/// var speeds = QuantityVector.Of(new Vector(90, 120, 60), "km/h");
/// Vector si = speeds.ToVector(Si.MetrePerSecond);   // в алгоритм — уже в м/с
/// </code>
/// </example>
[Serializable]
public sealed class QuantityVector : IEnumerable<Quantity>
{
    private readonly Vector _si;

    /// <summary>
    /// Размерность всех элементов ряда
    /// </summary>
    public Dimension Dimension { get; }

    /// <summary>
    /// Количество элементов
    /// </summary>
    public int Count => _si.Count;

    /// <summary>
    /// Создаёт ряд величин по вектору значений в базовых единицах СИ
    /// </summary>
    /// <param name="siValues">Значения в СИ</param>
    /// <param name="dimension">Размерность</param>
    public QuantityVector(Vector siValues, Dimension dimension)
    {
        ArgumentNullException.ThrowIfNull(siValues);
        _si = siValues.Clone();
        Dimension = dimension;
    }

    #region Создание

    /// <summary>
    /// Создаёт ряд величин по вектору значений в заданной единице
    /// </summary>
    /// <param name="values">Значения в единице <paramref name="unit"/></param>
    /// <param name="unit">Единица измерения</param>
    public static QuantityVector Of(Vector values, Unit unit)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(unit);

        var si = new Vector(values.Count);

        for (int i = 0; i < values.Count; i++)
            si[i] = unit.ToSi(values[i]);

        return new QuantityVector(si, unit.Dimension);
    }

    /// <summary>
    /// Создаёт ряд величин по вектору значений и символьной записи единицы
    /// </summary>
    /// <param name="values">Значения</param>
    /// <param name="unit">Символьная запись единицы</param>
    public static QuantityVector Of(Vector values, string unit) => Of(values, UnitRegistry.Parse(unit));

    #endregion

    #region Доступ и перевод

    /// <summary>
    /// Величина по индексу
    /// </summary>
    /// <param name="index">Индекс элемента</param>
    public Quantity this[int index] => new(_si[index], Dimension);

    /// <summary>
    /// Значения ряда в заданной единице
    /// </summary>
    /// <param name="unit">Целевая единица</param>
    /// <exception cref="DimensionMismatchException">Размерность единицы не совпадает с размерностью ряда</exception>
    public Vector ToVector(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.Dimension != Dimension)
            throw new DimensionMismatchException(unit.Dimension, Dimension, $"перевод ряда в «{unit.Symbol}»");

        var result = new Vector(_si.Count);

        for (int i = 0; i < _si.Count; i++)
            result[i] = unit.FromSi(_si[i]);

        return result;
    }

    /// <summary>
    /// Значения ряда в единице, заданной символьной записью
    /// </summary>
    /// <param name="unit">Символьная запись единицы</param>
    public Vector ToVector(string unit) => ToVector(UnitRegistry.Parse(unit));

    /// <summary>
    /// Значения ряда в базовых единицах СИ
    /// </summary>
    public Vector ToSiVector() => _si.Clone();

    /// <summary>
    /// Проверяет размерность и возвращает значения в СИ. Предназначен для проверки
    /// аргументов на границе публичного API.
    /// </summary>
    /// <param name="expected">Ожидаемая размерность</param>
    /// <param name="paramName">Имя проверяемого параметра</param>
    public Vector RequireSi(Dimension expected, string? paramName = null)
    {
        return Dimension != expected
            ? throw new DimensionMismatchException(expected, Dimension, paramName)
            : ToSiVector();
    }

    #endregion

    #region Операции

    /// <summary>
    /// Поэлементная сумма рядов одинаковой размерности
    /// </summary>
    public static QuantityVector operator +(QuantityVector a, QuantityVector b)
    {
        EnsureSameDimension(a, b, "сложение рядов");
        return new QuantityVector(a._si + b._si, a.Dimension);
    }

    /// <summary>
    /// Поэлементная разность рядов одинаковой размерности
    /// </summary>
    public static QuantityVector operator -(QuantityVector a, QuantityVector b)
    {
        EnsureSameDimension(a, b, "вычитание рядов");
        return new QuantityVector(a._si - b._si, a.Dimension);
    }

    /// <summary>
    /// Умножение всех элементов ряда на величину
    /// </summary>
    public static QuantityVector operator *(QuantityVector a, Quantity k)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new QuantityVector(a._si * k.SiValue, a.Dimension * k.Dimension);
    }

    /// <summary>
    /// Деление всех элементов ряда на величину
    /// </summary>
    public static QuantityVector operator /(QuantityVector a, Quantity k)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new QuantityVector(a._si / k.SiValue, a.Dimension / k.Dimension);
    }

    /// <summary>
    /// Сумма элементов ряда
    /// </summary>
    public Quantity Sum()
    {
        double sum = 0.0;

        for (int i = 0; i < _si.Count; i++)
            sum += _si[i];

        return new Quantity(sum, Dimension);
    }

    /// <summary>
    /// Среднее арифметическое элементов ряда
    /// </summary>
    /// <exception cref="InvalidOperationException">Ряд пуст</exception>
    public Quantity Mean()
    {
        return _si.Count == 0
            ? throw new InvalidOperationException("Ряд величин пуст")
            : new Quantity(Sum().SiValue / _si.Count, Dimension);
    }

    private static void EnsureSameDimension(QuantityVector a, QuantityVector b, string operation)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Dimension != b.Dimension)
            throw new DimensionMismatchException(a.Dimension, b.Dimension, operation);
    }

    #endregion

    #region Перечисление и представление

    /// <summary>
    /// Перечислитель величин ряда
    /// </summary>
    public IEnumerator<Quantity> GetEnumerator()
    {
        for (int i = 0; i < _si.Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Краткое представление ряда: количество элементов и единица вывода
    /// </summary>
    public override string ToString()
    {
        Unit unit = UnitRegistry.DisplayUnitFor(Dimension);
        return $"QuantityVector[{_si.Count}] в {unit.Symbol}";
    }

    #endregion
}

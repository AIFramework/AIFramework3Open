using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Complex = System.Numerics.Complex;

namespace AI.ClassicMath.Calculator;

/// <summary>
/// Операции над списками скрипта: перебор, замена элемента, добавление.
/// </summary>
/// <remarks>
/// Список в языке — это вектор чисел (<see cref="ComplexVector"/>) либо массив строк, смотря
/// что в нём лежит. Отдельного типа-списка нет намеренно: он потянул бы за собой правила
/// приведения, операторы и печать, а всё, ради чего список заводят в проверке документа —
/// «накопить и пройти» — закрывается этими тремя операциями.
/// <para>
/// Замена элемента строит НОВЫЙ список, а не правит старый на месте. Векторы фиксированной
/// длины этого и не позволяют, но важнее другое: без общих ссылок нет и вопроса, изменил ли
/// вызов функции список вызывающего.
/// </para>
/// </remarks>
public static class ListOps
{
    /// <summary>Элементы списка по порядку.</summary>
    public static IReadOnlyList<object> Items(object list, string what)
    {
        switch (list)
        {
            case null:
                throw new ArgumentException($"'{what}' ожидает список, но значение не задано.");

            case string[] strings:
                return strings;

            case ComplexVector complex:
                return complex.Select(value => (object)value).ToList();

            case Vector real:
                return real.Select(value => (object)new Complex(value, 0)).ToList();

            default:
                throw new ArgumentException(
                    $"'{what}' ожидает список (например [1, 2, 3]), но получил {list.GetType().Name}.");
        }
    }

    /// <summary>Список с заменённым элементом.</summary>
    public static object SetAt(object list, int index, object value, string name)
    {
        var items = Items(list, name).ToList();

        if (index < 0 || index >= items.Count)
            throw new IndexOutOfRangeException(
                $"Индекс {index} выходит за границы списка '{name}' (длина: {items.Count}).");

        items[index] = value;

        return Build(items);
    }

    /// <summary>Список с добавленным в конец элементом.</summary>
    public static object Append(object list, object value)
    {
        var items = Items(list, "append").ToList();
        items.Add(value);

        return Build(items);
    }

    /// <summary>
    /// Собирает список обратно в значение языка.
    /// </summary>
    /// <remarks>
    /// Хоть одна строка — весь список становится строковым: разнотипного списка в языке нет, а
    /// накапливают в проверках именно строки-нарушения.
    /// </remarks>
    private static object Build(IReadOnlyList<object> items)
    {
        if (items.Count == 0) return new ComplexVector(0);

        return items.Any(item => item is string)
            ? items.Select(AsText).ToArray()
            : new ComplexVector(items.Select(item => CastsVar.CastToComplex(item, "список")));
    }

    /// <summary>Элемент строкой: числа печатаются инвариантно, чтобы не появилась запятая.</summary>
    private static string AsText(object item)
    {
        switch (item)
        {
            case null: return "";
            case string text: return text;
            case Complex complex when Math.Abs(complex.Imaginary) < 1e-12:
                return complex.Real.ToString("G15", CultureInfo.InvariantCulture);
            case double number:
                return number.ToString("G15", CultureInfo.InvariantCulture);
            default: return item.ToString();
        }
    }
}

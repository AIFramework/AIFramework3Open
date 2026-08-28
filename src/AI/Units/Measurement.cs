#nullable enable
using System;
using System.Globalization;

namespace AI.Units;

/// <summary>
/// Результат измерения: величина вместе со стандартной неопределённостью.
/// Неопределённость переносится через операции по линейному закону (метод первого порядка).
/// </summary>
/// <remarks>
/// Все правила переноса предполагают <b>независимость</b> операндов. Для коррелированных
/// величин (например, <c>x - x</c>) линейный закон завышает неопределённость: результат такой
/// операции считать нельзя, его нужно преобразовывать аналитически до подстановки чисел.
/// </remarks>
[Serializable]
public readonly struct Measurement : IEquatable<Measurement>, IFormattable
{
    /// <summary>
    /// Измеренная величина
    /// </summary>
    public Quantity Value { get; }

    /// <summary>
    /// Стандартная неопределённость в базовых единицах СИ (всегда неотрицательна)
    /// </summary>
    public double SiUncertainty { get; }

    /// <summary>
    /// Создаёт результат измерения
    /// </summary>
    /// <param name="value">Величина</param>
    /// <param name="siUncertainty">Стандартная неопределённость в СИ</param>
    public Measurement(Quantity value, double siUncertainty = 0.0)
    {
        if (siUncertainty < 0.0 || double.IsNaN(siUncertainty))
            throw new ArgumentOutOfRangeException(nameof(siUncertainty), "Неопределённость должна быть неотрицательной");

        Value = value;
        SiUncertainty = siUncertainty;
    }

    #region Создание

    /// <summary>
    /// Создаёт результат измерения по значению и неопределённости в заданной единице
    /// </summary>
    /// <param name="value">Значение</param>
    /// <param name="uncertainty">Неопределённость в той же единице</param>
    /// <param name="unit">Единица измерения</param>
    public static Measurement Of(double value, double uncertainty, Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return new Measurement(Quantity.Of(value, unit), Math.Abs(uncertainty * unit.Factor));
    }

    /// <summary>
    /// Создаёт результат измерения с относительной неопределённостью
    /// </summary>
    /// <param name="value">Величина</param>
    /// <param name="relativeUncertainty">Относительная неопределённость (доля, не проценты)</param>
    public static Measurement Relative(Quantity value, double relativeUncertainty)
    {
        return new Measurement(value, Math.Abs(value.SiValue * relativeUncertainty));
    }

    /// <summary>
    /// Точно известная величина (нулевая неопределённость)
    /// </summary>
    /// <param name="value">Величина</param>
    public static Measurement Exact(Quantity value) => new(value);

    #endregion

    #region Чтение

    /// <summary>
    /// Размерность измеренной величины
    /// </summary>
    public Dimension Dimension => Value.Dimension;

    /// <summary>
    /// Относительная неопределённость (доля от значения); равна нулю при нулевой неопределённости
    /// </summary>
    public double RelativeUncertainty => SiUncertainty == 0.0 ? 0.0 : SiUncertainty / Math.Abs(Value.SiValue);

    /// <summary>
    /// Неопределённость в заданной единице
    /// </summary>
    /// <param name="unit">Целевая единица</param>
    public double UncertaintyIn(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.Dimension != Value.Dimension)
            throw new DimensionMismatchException(unit.Dimension, Value.Dimension, $"перевод в «{unit.Symbol}»");

        return SiUncertainty / Math.Abs(unit.Factor);
    }

    /// <summary>
    /// Границы интервала охвата шириной <paramref name="k"/> стандартных неопределённостей
    /// </summary>
    /// <param name="k">Коэффициент охвата (k = 2 — примерно 95 % для нормального распределения)</param>
    public (Quantity Low, Quantity High) Interval(double k = 2.0)
    {
        double half = Math.Abs(k) * SiUncertainty;
        return (new Quantity(Value.SiValue - half, Value.Dimension), new Quantity(Value.SiValue + half, Value.Dimension));
    }

    #endregion

    #region Арифметика с переносом неопределённости

    /// <summary>
    /// Сумма: неопределённости складываются в квадратуре
    /// </summary>
    public static Measurement operator +(Measurement a, Measurement b)
    {
        return new Measurement(a.Value + b.Value, Hypot(a.SiUncertainty, b.SiUncertainty));
    }

    /// <summary>
    /// Разность: неопределённости складываются в квадратуре
    /// </summary>
    public static Measurement operator -(Measurement a, Measurement b)
    {
        return new Measurement(a.Value - b.Value, Hypot(a.SiUncertainty, b.SiUncertainty));
    }

    /// <summary>
    /// Смена знака
    /// </summary>
    public static Measurement operator -(Measurement a) => new(-a.Value, a.SiUncertainty);

    /// <summary>
    /// Произведение: относительные неопределённости складываются в квадратуре
    /// </summary>
    public static Measurement operator *(Measurement a, Measurement b)
    {
        Quantity value = a.Value * b.Value;
        double uncertainty = Hypot(a.SiUncertainty * b.Value.SiValue, b.SiUncertainty * a.Value.SiValue);
        return new Measurement(value, Math.Abs(uncertainty));
    }

    /// <summary>
    /// Частное: относительные неопределённости складываются в квадратуре
    /// </summary>
    public static Measurement operator /(Measurement a, Measurement b)
    {
        Quantity value = a.Value / b.Value;
        double denominator = b.Value.SiValue * b.Value.SiValue;
        double uncertainty = Hypot(a.SiUncertainty / b.Value.SiValue, a.Value.SiValue * b.SiUncertainty / denominator);
        return new Measurement(value, Math.Abs(uncertainty));
    }

    /// <summary>
    /// Умножение на точный числовой множитель
    /// </summary>
    public static Measurement operator *(Measurement a, double k) => new(a.Value * k, a.SiUncertainty * Math.Abs(k));

    /// <summary>
    /// Умножение точного числового множителя на измерение
    /// </summary>
    public static Measurement operator *(double k, Measurement a) => a * k;

    /// <summary>
    /// Деление на точный числовой делитель
    /// </summary>
    public static Measurement operator /(Measurement a, double k) => new(a.Value / k, a.SiUncertainty / Math.Abs(k));

    /// <summary>
    /// Возведение в целую степень: относительная неопределённость умножается на модуль показателя
    /// </summary>
    /// <param name="exponent">Показатель степени</param>
    public Measurement Pow(int exponent)
    {
        Quantity value = Value.Pow(exponent);
        double uncertainty = Math.Abs(exponent) * Math.Abs(value.SiValue) * RelativeUncertainty;
        return new Measurement(value, uncertainty);
    }

    /// <summary>
    /// Квадратный корень: относительная неопределённость уменьшается вдвое
    /// </summary>
    public Measurement Sqrt()
    {
        Quantity value = Value.Sqrt();
        return new Measurement(value, 0.5 * Math.Abs(value.SiValue) * RelativeUncertainty);
    }

    /// <summary>
    /// Перенос неопределённости через произвольную функцию одной переменной.
    /// Производная вычисляется численно центральной разностью.
    /// </summary>
    /// <param name="function">Функция над значением в СИ</param>
    /// <param name="resultDimension">Размерность результата</param>
    public Measurement Apply(Func<double, double> function, Dimension resultDimension)
    {
        ArgumentNullException.ThrowIfNull(function);

        double x = Value.SiValue;
        double step = Math.Max(Math.Abs(x), 1.0) * 1e-6;
        double derivative = (function(x + step) - function(x - step)) / (2.0 * step);

        return new Measurement(new Quantity(function(x), resultDimension), Math.Abs(derivative) * SiUncertainty);
    }

    private static double Hypot(double a, double b) => Math.Sqrt((a * a) + (b * b));

    /// <summary>
    /// Неявное преобразование точной величины в результат измерения
    /// </summary>
    /// <param name="value">Величина</param>
    public static implicit operator Measurement(Quantity value) => new(value);

    /// <summary>
    /// Явное преобразование к величине с потерей неопределённости
    /// </summary>
    /// <param name="measurement">Результат измерения</param>
    public static explicit operator Quantity(Measurement measurement) => measurement.Value;

    #endregion

    #region Согласованность

    /// <summary>
    /// Проверяет согласованность двух измерений: модуль разности не превышает
    /// <paramref name="k"/> суммарных стандартных неопределённостей.
    /// </summary>
    /// <param name="other">Второе измерение</param>
    /// <param name="k">Коэффициент охвата</param>
    public bool IsConsistentWith(Measurement other, double k = 2.0)
    {
        if (Dimension != other.Dimension)
            return false;

        double combined = Hypot(SiUncertainty, other.SiUncertainty);
        double difference = Math.Abs(Value.SiValue - other.Value.SiValue);

        return combined == 0.0 ? difference == 0.0 : difference <= k * combined;
    }

    #endregion

    #region Равенство и представление

    /// <summary>
    /// Точное равенство величины и неопределённости
    /// </summary>
    /// <param name="other">Второе измерение</param>
    public bool Equals(Measurement other) => Value.Equals(other.Value) && SiUncertainty.Equals(other.SiUncertainty);

    /// <summary>
    /// Сравнение с произвольным объектом
    /// </summary>
    public override bool Equals(object? obj) => obj is Measurement other && Equals(other);

    /// <summary>
    /// Хеш-код измерения
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(Value, SiUncertainty);

    /// <summary>Равенство измерений</summary>
    public static bool operator ==(Measurement a, Measurement b) => a.Equals(b);

    /// <summary>Неравенство измерений</summary>
    public static bool operator !=(Measurement a, Measurement b) => !a.Equals(b);

    /// <summary>
    /// Строковое представление вида «9.81 ± 0.02 m/s²»
    /// </summary>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>
    /// Строковое представление с заданным форматом числа
    /// </summary>
    /// <param name="format">Формат числа</param>
    /// <param name="formatProvider">Поставщик форматирования</param>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString(UnitRegistry.DisplayUnitFor(Value.Dimension), format, formatProvider);
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
        string value = Value.In(unit).ToString(format, provider);

        if (SiUncertainty == 0.0)
            return Value.Dimension.IsDimensionless && unit.Equals(Unit.One) ? value : $"{value} {unit.Symbol}";

        string uncertainty = UncertaintyIn(unit).ToString(format, provider);
        return Value.Dimension.IsDimensionless && unit.Equals(Unit.One)
            ? $"{value} ± {uncertainty}"
            : $"{value} ± {uncertainty} {unit.Symbol}";
    }

    #endregion
}

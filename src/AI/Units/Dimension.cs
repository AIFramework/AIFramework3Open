#nullable enable
using System;
using System.Text;

namespace AI.Units;

/// <summary>
/// Размерность физической величины — вектор показателей степени семи базовых величин СИ
/// (длина, масса, время, сила тока, температура, количество вещества, сила света).
/// </summary>
/// <remarks>
/// Показатели хранятся в половинах (значение 2 соответствует степени 1), что позволяет точно
/// представлять корни второй степени — например, спектральную плотность шума В/√Гц.
/// Степени с знаменателем больше двух не поддерживаются.
/// </remarks>
[Serializable]
public readonly struct Dimension : IEquatable<Dimension>
{
    private readonly sbyte _length;
    private readonly sbyte _mass;
    private readonly sbyte _time;
    private readonly sbyte _current;
    private readonly sbyte _temperature;
    private readonly sbyte _amount;
    private readonly sbyte _luminousIntensity;

    #region Конструкторы

    /// <summary>
    /// Создаёт размерность по целым показателям степени базовых величин.
    /// </summary>
    /// <param name="length">Показатель длины (м)</param>
    /// <param name="mass">Показатель массы (кг)</param>
    /// <param name="time">Показатель времени (с)</param>
    /// <param name="current">Показатель силы тока (А)</param>
    /// <param name="temperature">Показатель термодинамической температуры (К)</param>
    /// <param name="amount">Показатель количества вещества (моль)</param>
    /// <param name="luminousIntensity">Показатель силы света (кд)</param>
    public Dimension(int length, int mass = 0, int time = 0, int current = 0,
        int temperature = 0, int amount = 0, int luminousIntensity = 0)
        : this(Halves(length, nameof(length)), Halves(mass, nameof(mass)), Halves(time, nameof(time)),
               Halves(current, nameof(current)), Halves(temperature, nameof(temperature)),
               Halves(amount, nameof(amount)), Halves(luminousIntensity, nameof(luminousIntensity)))
    {
    }

    private Dimension(sbyte length, sbyte mass, sbyte time, sbyte current,
        sbyte temperature, sbyte amount, sbyte luminousIntensity)
    {
        _length = length;
        _mass = mass;
        _time = time;
        _current = current;
        _temperature = temperature;
        _amount = amount;
        _luminousIntensity = luminousIntensity;
    }

    /// <summary>
    /// Создаёт размерность по показателям, выраженным в половинах степени
    /// (значение 2 — степень 1, значение 1 — степень 1/2).
    /// </summary>
    public static Dimension FromHalves(int length, int mass = 0, int time = 0, int current = 0,
        int temperature = 0, int amount = 0, int luminousIntensity = 0)
    {
        return new Dimension(Clamp(length, nameof(length)), Clamp(mass, nameof(mass)), Clamp(time, nameof(time)),
            Clamp(current, nameof(current)), Clamp(temperature, nameof(temperature)),
            Clamp(amount, nameof(amount)), Clamp(luminousIntensity, nameof(luminousIntensity)));
    }

    private static sbyte Halves(int exponent, string name)
    {
        return Clamp(exponent * 2, name);
    }

    private static sbyte Clamp(int halves, string name)
    {
        if (halves is < sbyte.MinValue or > sbyte.MaxValue)
            throw new ArgumentOutOfRangeException(name, "Показатель степени размерности вне допустимого диапазона");

        return (sbyte)halves;
    }

    #endregion

    #region Показатели степени

    /// <summary>Показатель степени длины</summary>
    public double Length => _length / 2.0;

    /// <summary>Показатель степени массы</summary>
    public double Mass => _mass / 2.0;

    /// <summary>Показатель степени времени</summary>
    public double Time => _time / 2.0;

    /// <summary>Показатель степени силы тока</summary>
    public double Current => _current / 2.0;

    /// <summary>Показатель степени температуры</summary>
    public double Temperature => _temperature / 2.0;

    /// <summary>Показатель степени количества вещества</summary>
    public double Amount => _amount / 2.0;

    /// <summary>Показатель степени силы света</summary>
    public double LuminousIntensity => _luminousIntensity / 2.0;

    /// <summary>
    /// Признак безразмерной величины (все показатели нулевые)
    /// </summary>
    public bool IsDimensionless => OrOfHalves() == 0;

    private int OrOfHalves()
    {
        return (byte)_length | (byte)_mass | (byte)_time | (byte)_current
            | (byte)_temperature | (byte)_amount | (byte)_luminousIntensity;
    }

    #endregion

    #region Базовые размерности

    /// <summary>Безразмерная величина</summary>
    public static Dimension None => default;

    /// <summary>Длина, м</summary>
    public static Dimension LengthDim => new(1);

    /// <summary>Масса, кг</summary>
    public static Dimension MassDim => new(0, 1);

    /// <summary>Время, с</summary>
    public static Dimension TimeDim => new(0, 0, 1);

    /// <summary>Сила тока, А</summary>
    public static Dimension CurrentDim => new(0, 0, 0, 1);

    /// <summary>Термодинамическая температура, К</summary>
    public static Dimension TemperatureDim => new(0, 0, 0, 0, 1);

    /// <summary>Количество вещества, моль</summary>
    public static Dimension AmountDim => new(0, 0, 0, 0, 0, 1);

    /// <summary>Сила света, кд</summary>
    public static Dimension LuminousIntensityDim => new(0, 0, 0, 0, 0, 0, 1);

    #endregion

    #region Производные размерности

    /// <summary>Площадь, м²</summary>
    public static Dimension Area => new(2);

    /// <summary>Объём, м³</summary>
    public static Dimension Volume => new(3);

    /// <summary>Частота, с⁻¹</summary>
    public static Dimension Frequency => new(0, 0, -1);

    /// <summary>Скорость, м·с⁻¹</summary>
    public static Dimension Velocity => new(1, 0, -1);

    /// <summary>Ускорение, м·с⁻²</summary>
    public static Dimension Acceleration => new(1, 0, -2);

    /// <summary>Сила, кг·м·с⁻²</summary>
    public static Dimension Force => new(1, 1, -2);

    /// <summary>Давление, кг·м⁻¹·с⁻²</summary>
    public static Dimension Pressure => new(-1, 1, -2);

    /// <summary>Энергия, кг·м²·с⁻²</summary>
    public static Dimension Energy => new(2, 1, -2);

    /// <summary>Мощность, кг·м²·с⁻³</summary>
    public static Dimension Power => new(2, 1, -3);

    /// <summary>Электрический заряд, А·с</summary>
    public static Dimension Charge => new(0, 0, 1, 1);

    /// <summary>Электрическое напряжение, кг·м²·с⁻³·А⁻¹</summary>
    public static Dimension Voltage => new(2, 1, -3, -1);

    /// <summary>Электрическое сопротивление, кг·м²·с⁻³·А⁻²</summary>
    public static Dimension Resistance => new(2, 1, -3, -2);

    /// <summary>Электрическая ёмкость, кг⁻¹·м⁻²·с⁴·А²</summary>
    public static Dimension Capacitance => new(-2, -1, 4, 2);

    /// <summary>Индуктивность, кг·м²·с⁻²·А⁻²</summary>
    public static Dimension Inductance => new(2, 1, -2, -2);

    /// <summary>Магнитная индукция, кг·с⁻²·А⁻¹</summary>
    public static Dimension MagneticFluxDensity => new(0, 1, -2, -1);

    /// <summary>Плотность, кг·м⁻³</summary>
    public static Dimension Density => new(-3, 1);

    #endregion

    #region Операции

    /// <summary>
    /// Сложение показателей — размерность произведения величин
    /// </summary>
    public static Dimension operator *(Dimension a, Dimension b)
    {
        return new Dimension(
            Clamp(a._length + b._length, "length"),
            Clamp(a._mass + b._mass, "mass"),
            Clamp(a._time + b._time, "time"),
            Clamp(a._current + b._current, "current"),
            Clamp(a._temperature + b._temperature, "temperature"),
            Clamp(a._amount + b._amount, "amount"),
            Clamp(a._luminousIntensity + b._luminousIntensity, "luminousIntensity"));
    }

    /// <summary>
    /// Вычитание показателей — размерность частного величин
    /// </summary>
    public static Dimension operator /(Dimension a, Dimension b)
    {
        return new Dimension(
            Clamp(a._length - b._length, "length"),
            Clamp(a._mass - b._mass, "mass"),
            Clamp(a._time - b._time, "time"),
            Clamp(a._current - b._current, "current"),
            Clamp(a._temperature - b._temperature, "temperature"),
            Clamp(a._amount - b._amount, "amount"),
            Clamp(a._luminousIntensity - b._luminousIntensity, "luminousIntensity"));
    }

    /// <summary>
    /// Возведение размерности в целую степень
    /// </summary>
    /// <param name="exponent">Показатель степени</param>
    public Dimension Pow(int exponent)
    {
        return new Dimension(
            Clamp(_length * exponent, "length"),
            Clamp(_mass * exponent, "mass"),
            Clamp(_time * exponent, "time"),
            Clamp(_current * exponent, "current"),
            Clamp(_temperature * exponent, "temperature"),
            Clamp(_amount * exponent, "amount"),
            Clamp(_luminousIntensity * exponent, "luminousIntensity"));
    }

    /// <summary>
    /// Квадратный корень из размерности. Требует, чтобы все показатели были кратны 1/2
    /// после деления, то есть хранимые половины — чётными.
    /// </summary>
    /// <exception cref="InvalidOperationException">Показатель не делится на два нацело</exception>
    public Dimension Sqrt()
    {
        if ((OrOfHalves() & 1) != 0)
            throw new InvalidOperationException($"Размерность {this} не имеет представимого квадратного корня");

        return new Dimension(
            (sbyte)(_length / 2), (sbyte)(_mass / 2), (sbyte)(_time / 2), (sbyte)(_current / 2),
            (sbyte)(_temperature / 2), (sbyte)(_amount / 2), (sbyte)(_luminousIntensity / 2));
    }

    #endregion

    #region Равенство и представление

    /// <summary>
    /// Сравнение размерностей
    /// </summary>
    public bool Equals(Dimension other)
    {
        return _length == other._length && _mass == other._mass && _time == other._time
            && _current == other._current && _temperature == other._temperature
            && _amount == other._amount && _luminousIntensity == other._luminousIntensity;
    }

    /// <summary>
    /// Сравнение с произвольным объектом
    /// </summary>
    public override bool Equals(object? obj) => obj is Dimension other && Equals(other);

    /// <summary>
    /// Хеш-код размерности
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(_length, _mass, _time, _current, _temperature, _amount, _luminousIntensity);
    }

    /// <summary>
    /// Равенство размерностей
    /// </summary>
    public static bool operator ==(Dimension a, Dimension b) => a.Equals(b);

    /// <summary>
    /// Неравенство размерностей
    /// </summary>
    public static bool operator !=(Dimension a, Dimension b) => !a.Equals(b);

    /// <summary>
    /// Запись размерности в символах базовых единиц СИ, например «kg·m²·s⁻³»
    /// </summary>
    public override string ToString()
    {
        if (IsDimensionless)
            return "1";

        var sb = new StringBuilder();
        Append(sb, "kg", _mass);
        Append(sb, "m", _length);
        Append(sb, "s", _time);
        Append(sb, "A", _current);
        Append(sb, "K", _temperature);
        Append(sb, "mol", _amount);
        Append(sb, "cd", _luminousIntensity);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string symbol, sbyte halves)
    {
        if (halves == 0)
            return;

        if (sb.Length > 0)
            _ = sb.Append('·');

        _ = sb.Append(symbol);

        if (halves == 2)
            return;

        _ = (halves & 1) == 0
            ? sb.Append(Superscript(halves / 2))
            : sb.Append('^').Append((halves / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Superscript(int exponent)
    {
        const string Digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var sb = new StringBuilder();

        if (exponent < 0)
            _ = sb.Append('⁻');

        foreach (char c in Math.Abs(exponent).ToString(System.Globalization.CultureInfo.InvariantCulture))
            _ = sb.Append(Digits[c - '0']);

        return sb.ToString();
    }

    #endregion
}

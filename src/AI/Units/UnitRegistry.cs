#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AI.Units;

/// <summary>
/// Реестр единиц измерения: разбор символьной записи («kW·h», «m/s^2», «mg/L»),
/// десятичные приставки СИ и выбор единицы для вывода величины.
/// </summary>
/// <remarks>
/// Разбор идёт слева направо: множители разделяются символами <c>*</c>, <c>·</c> или пробелом,
/// деление — символом <c>/</c>, показатель степени — <c>^n</c> или надстрочными цифрами.
/// Сначала ищется точное совпадение символа, и лишь затем — приставка с остатком,
/// поэтому «T» — это тесла, а не тера, а «min» — минута, а не милли-дюйм.
/// </remarks>
public static class UnitRegistry
{
    private static readonly Dictionary<string, Unit> Symbols = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> Prefixes = new(StringComparer.Ordinal);
    private static readonly Dictionary<Dimension, Unit> Display = new();
    private static readonly object SyncRoot = new();

    static UnitRegistry()
    {
        RegisterPrefixes();
        RegisterUnits();
        RegisterDisplayUnits();
    }

    #region Регистрация

    private static void RegisterPrefixes()
    {
        Prefixes["Q"] = 1e30; Prefixes["R"] = 1e27; Prefixes["Y"] = 1e24; Prefixes["Z"] = 1e21;
        Prefixes["E"] = 1e18; Prefixes["P"] = 1e15; Prefixes["T"] = 1e12; Prefixes["G"] = 1e9;
        Prefixes["M"] = 1e6; Prefixes["k"] = 1e3; Prefixes["h"] = 1e2; Prefixes["da"] = 1e1;
        Prefixes["d"] = 1e-1; Prefixes["c"] = 1e-2; Prefixes["m"] = 1e-3;
        Prefixes["µ"] = 1e-6; Prefixes["μ"] = 1e-6; Prefixes["u"] = 1e-6;
        Prefixes["n"] = 1e-9; Prefixes["p"] = 1e-12; Prefixes["f"] = 1e-15; Prefixes["a"] = 1e-18;
        Prefixes["z"] = 1e-21; Prefixes["y"] = 1e-24; Prefixes["r"] = 1e-27; Prefixes["q"] = 1e-30;
    }

    private static void RegisterUnits()
    {
        Add(Si.Metre); Add(Si.Kilogram); Add(Si.Gram); Add(Si.Second); Add(Si.Ampere);
        Add(Si.Kelvin); Add(Si.Mole); Add(Si.Candela);

        Add(Si.Hertz); Add(Si.Newton); Add(Si.Pascal); Add(Si.Joule); Add(Si.Watt);
        Add(Si.Coulomb); Add(Si.Volt); Add(Si.Farad); Add(Si.Ohm); Add(Si.Siemens);
        Add(Si.Weber); Add(Si.Tesla); Add(Si.Henry); Add(Si.Lumen); Add(Si.Lux);
        Add(Si.Becquerel); Add(Si.Gray); Add(Si.Sievert); Add(Si.Katal);
        Add(Si.Radian); Add(Si.Steradian);

        Add(Si.Percent); Add(Si.Degree); Add(Si.Minute); Add(Si.Hour); Add(Si.Day);
        Add(Si.Litre); Add(Si.Tonne); Add(Si.Hectare); Add(Si.Bar); Add(Si.Atmosphere);
        Add(Si.MillimetreOfMercury); Add(Si.ElectronVolt); Add(Si.Calorie);
        Add(Si.Angstrom); Add(Si.AstronomicalUnit); Add(Si.Dalton);
        Add(Si.DegreeCelsius); Add(Si.DegreeFahrenheit);
        Add(Si.Inch); Add(Si.Foot); Add(Si.Mile); Add(Si.Pound);

        Symbols["1"] = Unit.One;
        Symbols["l"] = Si.Litre;
        Symbols["Ohm"] = Si.Ohm;
        Symbols["ohm"] = Si.Ohm;
        Symbols["Ω"] = Si.Ohm;
        Symbols["u"] = Si.Dalton;
        Symbols["deg"] = Si.Degree;
        Symbols["degC"] = Si.DegreeCelsius;
        Symbols["degF"] = Si.DegreeFahrenheit;
    }

    private static void RegisterDisplayUnits()
    {
        Display[Dimension.LengthDim] = Si.Metre;
        Display[Dimension.MassDim] = Si.Kilogram;
        Display[Dimension.TimeDim] = Si.Second;
        Display[Dimension.CurrentDim] = Si.Ampere;
        Display[Dimension.TemperatureDim] = Si.Kelvin;
        Display[Dimension.AmountDim] = Si.Mole;
        Display[Dimension.LuminousIntensityDim] = Si.Candela;

        Display[Dimension.Frequency] = Si.Hertz;
        Display[Dimension.Force] = Si.Newton;
        Display[Dimension.Pressure] = Si.Pascal;
        Display[Dimension.Energy] = Si.Joule;
        Display[Dimension.Power] = Si.Watt;
        Display[Dimension.Charge] = Si.Coulomb;
        Display[Dimension.Voltage] = Si.Volt;
        Display[Dimension.Resistance] = Si.Ohm;
        Display[Dimension.Capacitance] = Si.Farad;
        Display[Dimension.Inductance] = Si.Henry;
        Display[Dimension.MagneticFluxDensity] = Si.Tesla;

        Display[Dimension.Velocity] = Si.MetrePerSecond;
        Display[Dimension.Acceleration] = Si.MetrePerSecondSquared;
        Display[Dimension.Area] = Si.SquareMetre;
        Display[Dimension.Volume] = Si.CubicMetre;
        Display[Dimension.Density] = Si.KilogramPerCubicMetre;
    }

    private static void Add(Unit unit) => Symbols[unit.Symbol] = unit;

    /// <summary>
    /// Регистрирует пользовательскую единицу под её символом. Повторная регистрация
    /// того же символа заменяет прежнюю единицу.
    /// </summary>
    /// <param name="unit">Регистрируемая единица</param>
    public static void Register(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        lock (SyncRoot)
            Symbols[unit.Symbol] = unit;
    }

    /// <summary>
    /// Назначает единицу, в которой выводятся величины заданной размерности
    /// </summary>
    /// <param name="unit">Единица вывода</param>
    public static void RegisterDisplayUnit(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.IsAffine)
            throw new ArgumentException("Аффинная единица не может быть единицей вывода по умолчанию", nameof(unit));

        lock (SyncRoot)
            Display[unit.Dimension] = unit;
    }

    #endregion

    #region Разбор

    /// <summary>
    /// Разбирает символьную запись единицы, например «kW·h», «mg/L», «m/s^2»
    /// </summary>
    /// <param name="text">Запись единицы</param>
    /// <exception cref="FormatException">Запись не распознана</exception>
    public static Unit Parse(string text)
    {
        return TryParse(text, out Unit? unit)
            ? unit!
            : throw new FormatException($"Не удалось разобрать единицу измерения «{text}»");
    }

    /// <summary>
    /// Пытается разобрать символьную запись единицы
    /// </summary>
    /// <param name="text">Запись единицы</param>
    /// <param name="unit">Разобранная единица или <c>null</c></param>
    public static bool TryParse(string? text, out Unit? unit)
    {
        unit = null;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string source = text.Trim();

        lock (SyncRoot)
        {
            if (Symbols.TryGetValue(source, out Unit? exact))
            {
                unit = exact;
                return true;
            }
        }

        double factor = 1.0;
        Dimension dimension = Dimension.None;
        int sign = 1;
        int i = 0;
        bool any = false;

        // Знак операции требует единицы с обеих сторон: «g//mol», «kg·» и «/s» — опечатки,
        // а не запись величины. Без этой проверки повторный разделитель молча игнорировался.
        bool operandExpected = true;

        while (i < source.Length)
        {
            char c = source[i];

            if (c == ' ')
            {
                i++;
                continue;
            }

            if (c is '*' or '·' or '⋅')
            {
                if (operandExpected)
                    return false;

                sign = 1;
                operandExpected = true;
                i++;
                continue;
            }

            if (c == '/')
            {
                if (operandExpected)
                    return false;

                sign = -1;
                operandExpected = true;
                i++;
                continue;
            }

            int start = i;
            while (i < source.Length && !IsSeparator(source[i]) && !IsExponentStart(source[i]))
                i++;

            if (i == start)
                return false;

            string symbol = source[start..i];

            if (!TryResolve(symbol, out Unit? resolved))
                return false;

            if (resolved!.IsAffine)
                return false;

            int exponent = 1;

            if (i < source.Length && IsExponentStart(source[i]))
            {
                if (!TryReadExponent(source, ref i, out exponent))
                    return false;
            }

            int signedExponent = sign * exponent;
            factor *= Math.Pow(resolved.Factor, signedExponent);
            dimension *= resolved.Dimension.Pow(signedExponent);
            any = true;
            operandExpected = false;
        }

        if (!any || operandExpected)
            return false;

        unit = new Unit(Normalize(source), dimension, factor, 0.0, false);
        return true;
    }

    private static bool IsSeparator(char c) => c is '*' or '·' or '⋅' or '/' or ' ';

    private static bool IsExponentStart(char c) => c == '^' || IsSuperscript(c);

    private static bool IsSuperscript(char c) => "⁰¹²³⁴⁵⁶⁷⁸⁹⁻".IndexOf(c) >= 0;

    private static bool TryReadExponent(string source, ref int i, out int exponent)
    {
        exponent = 0;

        if (source[i] == '^')
        {
            i++;
            int start = i;

            if (i < source.Length && (source[i] == '-' || source[i] == '+'))
                i++;

            while (i < source.Length && char.IsDigit(source[i]))
                i++;

            return i > start && int.TryParse(source[start..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent);
        }

        var sb = new StringBuilder();

        while (i < source.Length && IsSuperscript(source[i]))
        {
            char c = source[i];
            _ = c == '⁻' ? sb.Append('-') : sb.Append((char)('0' + "⁰¹²³⁴⁵⁶⁷⁸⁹".IndexOf(c)));
            i++;
        }

        return sb.Length > 0 && int.TryParse(sb.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent);
    }

    private static bool TryResolve(string symbol, out Unit? unit)
    {
        lock (SyncRoot)
        {
            if (Symbols.TryGetValue(symbol, out unit))
                return true;

            for (int prefixLength = 2; prefixLength >= 1; prefixLength--)
            {
                if (symbol.Length <= prefixLength)
                    continue;

                string prefix = symbol[..prefixLength];
                string rest = symbol[prefixLength..];

                if (!Prefixes.TryGetValue(prefix, out double scale))
                    continue;

                if (!Symbols.TryGetValue(rest, out Unit? baseUnit) || !baseUnit.AllowPrefix)
                    continue;

                unit = new Unit(symbol, baseUnit.Dimension, scale * baseUnit.Factor, 0.0, false);
                return true;
            }
        }

        unit = null;
        return false;
    }

    private static string Normalize(string source) => source.Replace('*', '·').Replace(" ", string.Empty);

    #endregion

    #region Вывод

    /// <summary>
    /// Возвращает единицу, в которой по умолчанию выводится величина заданной размерности.
    /// Если единица не назначена, используется запись через базовые единицы СИ.
    /// </summary>
    /// <param name="dimension">Размерность</param>
    public static Unit DisplayUnitFor(Dimension dimension)
    {
        if (dimension.IsDimensionless)
            return Unit.One;

        lock (SyncRoot)
        {
            if (Display.TryGetValue(dimension, out Unit? unit))
                return unit;
        }

        return new Unit(dimension.ToString(), dimension, 1.0, 0.0, false);
    }

    #endregion
}

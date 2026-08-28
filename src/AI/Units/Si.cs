#nullable enable
using System;

namespace AI.Units;

/// <summary>
/// Готовые единицы измерения: семь базовых единиц СИ, производные единицы
/// со специальными названиями и наиболее употребительные внесистемные единицы.
/// </summary>
public static class Si
{
    #region Базовые единицы СИ

    /// <summary>Метр — единица длины</summary>
    public static Unit Metre { get; } = new("m", Dimension.LengthDim);

    /// <summary>Килограмм — единица массы (приставки применяются к грамму)</summary>
    public static Unit Kilogram { get; } = new("kg", Dimension.MassDim, 1.0, 0.0, false);

    /// <summary>Грамм</summary>
    public static Unit Gram { get; } = new("g", Dimension.MassDim, 1e-3);

    /// <summary>Секунда — единица времени</summary>
    public static Unit Second { get; } = new("s", Dimension.TimeDim);

    /// <summary>Ампер — единица силы тока</summary>
    public static Unit Ampere { get; } = new("A", Dimension.CurrentDim);

    /// <summary>Кельвин — единица термодинамической температуры</summary>
    public static Unit Kelvin { get; } = new("K", Dimension.TemperatureDim);

    /// <summary>Моль — единица количества вещества</summary>
    public static Unit Mole { get; } = new("mol", Dimension.AmountDim);

    /// <summary>Кандела — единица силы света</summary>
    public static Unit Candela { get; } = new("cd", Dimension.LuminousIntensityDim);

    #endregion

    #region Производные единицы СИ

    /// <summary>Герц — единица частоты</summary>
    public static Unit Hertz { get; } = new("Hz", Dimension.Frequency);

    /// <summary>Ньютон — единица силы</summary>
    public static Unit Newton { get; } = new("N", Dimension.Force);

    /// <summary>Паскаль — единица давления</summary>
    public static Unit Pascal { get; } = new("Pa", Dimension.Pressure);

    /// <summary>Джоуль — единица энергии</summary>
    public static Unit Joule { get; } = new("J", Dimension.Energy);

    /// <summary>Ватт — единица мощности</summary>
    public static Unit Watt { get; } = new("W", Dimension.Power);

    /// <summary>Кулон — единица электрического заряда</summary>
    public static Unit Coulomb { get; } = new("C", Dimension.Charge);

    /// <summary>Вольт — единица электрического напряжения</summary>
    public static Unit Volt { get; } = new("V", Dimension.Voltage);

    /// <summary>Фарад — единица электрической ёмкости</summary>
    public static Unit Farad { get; } = new("F", Dimension.Capacitance);

    /// <summary>Ом — единица электрического сопротивления</summary>
    public static Unit Ohm { get; } = new("Ω", Dimension.Resistance);

    /// <summary>Сименс — единица электрической проводимости</summary>
    public static Unit Siemens { get; } = new("S", Dimension.Resistance.Pow(-1));

    /// <summary>Вебер — единица магнитного потока</summary>
    public static Unit Weber { get; } = new("Wb", Dimension.MagneticFluxDensity * Dimension.Area);

    /// <summary>Тесла — единица магнитной индукции</summary>
    public static Unit Tesla { get; } = new("T", Dimension.MagneticFluxDensity);

    /// <summary>Генри — единица индуктивности</summary>
    public static Unit Henry { get; } = new("H", Dimension.Inductance);

    /// <summary>Люмен — единица светового потока</summary>
    public static Unit Lumen { get; } = new("lm", Dimension.LuminousIntensityDim);

    /// <summary>Люкс — единица освещённости</summary>
    public static Unit Lux { get; } = new("lx", Dimension.LuminousIntensityDim / Dimension.Area);

    /// <summary>Беккерель — единица активности радионуклида</summary>
    public static Unit Becquerel { get; } = new("Bq", Dimension.Frequency);

    /// <summary>Грей — единица поглощённой дозы</summary>
    public static Unit Gray { get; } = new("Gy", Dimension.Energy / Dimension.MassDim);

    /// <summary>Зиверт — единица эквивалентной дозы</summary>
    public static Unit Sievert { get; } = new("Sv", Dimension.Energy / Dimension.MassDim);

    /// <summary>Катал — единица каталитической активности</summary>
    public static Unit Katal { get; } = new("kat", Dimension.AmountDim / Dimension.TimeDim);

    /// <summary>Радиан — безразмерная единица плоского угла</summary>
    public static Unit Radian { get; } = new("rad", Dimension.None, 1.0, 0.0, false);

    /// <summary>Стерадиан — безразмерная единица телесного угла</summary>
    public static Unit Steradian { get; } = new("sr", Dimension.None, 1.0, 0.0, false);

    #endregion

    #region Составные единицы

    /// <summary>Метр в секунду — единица скорости</summary>
    public static Unit MetrePerSecond { get; } = new("m/s", Dimension.Velocity);

    /// <summary>Метр на секунду в квадрате — единица ускорения</summary>
    public static Unit MetrePerSecondSquared { get; } = new("m/s²", Dimension.Acceleration);

    /// <summary>Квадратный метр — единица площади</summary>
    public static Unit SquareMetre { get; } = new("m²", Dimension.Area);

    /// <summary>Кубический метр — единица объёма</summary>
    public static Unit CubicMetre { get; } = new("m³", Dimension.Volume);

    /// <summary>Килограмм на кубический метр — единица плотности</summary>
    public static Unit KilogramPerCubicMetre { get; } = new("kg/m³", Dimension.Density);

    #endregion

    #region Внесистемные единицы

    /// <summary>Безразмерная единица</summary>
    public static Unit One => Unit.One;

    /// <summary>Процент — сотая доля безразмерной величины</summary>
    public static Unit Percent { get; } = new("%", Dimension.None, 0.01, 0.0, false);

    /// <summary>Градус плоского угла</summary>
    public static Unit Degree { get; } = new("°", Dimension.None, Math.PI / 180.0, 0.0, false);

    /// <summary>Минута</summary>
    public static Unit Minute { get; } = new("min", Dimension.TimeDim, 60.0, 0.0, false);

    /// <summary>Час</summary>
    public static Unit Hour { get; } = new("h", Dimension.TimeDim, 3600.0, 0.0, false);

    /// <summary>Сутки</summary>
    public static Unit Day { get; } = new("d", Dimension.TimeDim, 86400.0, 0.0, false);

    /// <summary>Литр</summary>
    public static Unit Litre { get; } = new("L", Dimension.Volume, 1e-3);

    /// <summary>Тонна</summary>
    public static Unit Tonne { get; } = new("t", Dimension.MassDim, 1e3);

    /// <summary>Гектар</summary>
    public static Unit Hectare { get; } = new("ha", Dimension.Area, 1e4, 0.0, false);

    /// <summary>Бар</summary>
    public static Unit Bar { get; } = new("bar", Dimension.Pressure, 1e5);

    /// <summary>Стандартная атмосфера</summary>
    public static Unit Atmosphere { get; } = new("atm", Dimension.Pressure, 101325.0, 0.0, false);

    /// <summary>Миллиметр ртутного столба</summary>
    public static Unit MillimetreOfMercury { get; } = new("mmHg", Dimension.Pressure, 133.322387415, 0.0, false);

    /// <summary>Электронвольт</summary>
    public static Unit ElectronVolt { get; } = new("eV", Dimension.Energy, 1.602176634e-19);

    /// <summary>Калория термохимическая</summary>
    public static Unit Calorie { get; } = new("cal", Dimension.Energy, 4.184);

    /// <summary>Ватт-час</summary>
    public static Unit WattHour { get; } = new("W·h", Dimension.Energy, 3600.0);

    /// <summary>Ангстрем</summary>
    public static Unit Angstrom { get; } = new("Å", Dimension.LengthDim, 1e-10, 0.0, false);

    /// <summary>Астрономическая единица</summary>
    public static Unit AstronomicalUnit { get; } = new("au", Dimension.LengthDim, 1.495978707e11, 0.0, false);

    /// <summary>Дальтон (атомная единица массы)</summary>
    public static Unit Dalton { get; } = new("Da", Dimension.MassDim, 1.66053906892e-27);

    /// <summary>Градус Цельсия — аффинная шкала температуры</summary>
    public static Unit DegreeCelsius { get; } = new("°C", Dimension.TemperatureDim, 1.0, 273.15, false);

    /// <summary>Градус Фаренгейта — аффинная шкала температуры</summary>
    public static Unit DegreeFahrenheit { get; } = new("°F", Dimension.TemperatureDim, 5.0 / 9.0, 273.15 - (32.0 * 5.0 / 9.0), false);

    /// <summary>Дюйм</summary>
    public static Unit Inch { get; } = new("in", Dimension.LengthDim, 0.0254, 0.0, false);

    /// <summary>Фут</summary>
    public static Unit Foot { get; } = new("ft", Dimension.LengthDim, 0.3048, 0.0, false);

    /// <summary>Миля (международная)</summary>
    public static Unit Mile { get; } = new("mi", Dimension.LengthDim, 1609.344, 0.0, false);

    /// <summary>Фунт (международный)</summary>
    public static Unit Pound { get; } = new("lb", Dimension.MassDim, 0.45359237, 0.0, false);

    #endregion
}

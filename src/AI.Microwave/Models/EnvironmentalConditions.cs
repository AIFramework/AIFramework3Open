namespace AI.Microwave.Models;

/// <summary>
/// Условия окружающей среды, определяющие порог СВЧ-пробоя воздуха.
/// </summary>
public class EnvironmentalConditions
{
    /// <summary>Температура, градусы Цельсия.</summary>
    public double Temperature { get; set; } = 20;

    /// <summary>Относительная влажность, %.</summary>
    public double Humidity { get; set; } = 50;

    /// <summary>
    /// Давление на уровне моря, атм. Наддув герметичного тракта задаётся
    /// значением больше единицы; поправка на высоту применяется отдельно.
    /// </summary>
    public double Pressure { get; set; } = 1.0;

    /// <summary>Высота над уровнем моря, м.</summary>
    public double Altitude { get; set; } = 0;

    /// <summary>Базовый порог СВЧ-пробоя сухого воздуха, В/м (20 C, 1 атм, CW).</summary>
    public const double DryAirBreakdownAtSeaLevel = 3.0e6;

    /// <summary>Высота однородной атмосферы, м.</summary>
    private const double ScaleHeight = 8500.0;

    /// <summary>
    /// Абсолютное давление, атм: барометрическая поправка на высоту поверх
    /// заданного <see cref="Pressure"/>.
    /// </summary>
    /// <remarks>
    /// Раньше давление и высота входили в порог пробоя двумя независимыми
    /// множителями без пояснения, что означает их сочетание. Здесь связь
    /// названа явно: Pressure - это давление на уровне моря, а не абсолютное.
    /// </remarks>
    public double GetAbsolutePressureAtm()
        => Pressure * Math.Exp(-Altitude / ScaleHeight);

    /// <summary>
    /// Пороговая напряжённость СВЧ-пробоя воздуха, В/м.
    /// </summary>
    /// <remarks>
    /// Порог пропорционален плотности газа (закон подобия по приведённому
    /// полю E/N), то есть отношению p/T. Водяной пар электроотрицателен и
    /// слегка повышает порог; прежняя модель снижала его на 20 %, то есть
    /// ошибалась и в знаке, и в величине. Реальная опасность влаги -
    /// конденсат и загрязнения на поверхностях, а не объёмный пробой.
    /// </remarks>
    public double GetBreakdownFieldStrength()
    {
        double kelvin = Math.Max(273.15 + Temperature, 1.0);
        double densityFactor = GetAbsolutePressureAtm() * 293.15 / kelvin;
        double humidityFactor = 1.0 + 0.02 * Math.Clamp(Humidity, 0.0, 100.0) / 100.0;
        return DryAirBreakdownAtSeaLevel * densityFactor * humidityFactor;
    }

    /// <summary>
    /// Точка росы по приближению Магнуса, градусы Цельсия. Если температура
    /// холодной стенки опускается до неё, на диэлектрике появляется плёнка
    /// воды - и порог пробоя определяется уже поверхностью, а не воздухом.
    /// </summary>
    public double GetDewPoint()
    {
        double rh = Math.Clamp(Humidity, 1.0, 100.0) / 100.0;
        const double a = 17.27, b = 237.7;
        double gamma = a * Temperature / (b + Temperature) + Math.Log(rh);
        return b * gamma / (a - gamma);
    }
}

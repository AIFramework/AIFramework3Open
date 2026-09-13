using System.Numerics;
using AI.Units;

namespace AI.Microwave.Propagation;

/// <summary>
/// Электрические свойства материала для расчёта отражения: относительная диэлектрическая проницаемость
/// и проводимость, в общем случае зависящие от частоты.
/// </summary>
/// <remarks>
/// <para>
/// Модель ITU-R P.2040: <c>ε′ = a·f^b</c>, <c>σ = c·f^d</c>, частота f — в гигагерцах, проводимость — в См/м.
/// Комплексная проницаемость <c>ε = ε′ − j·σ/(2πf·ε₀)</c> (временная зависимость e^(jωt)). Готовые
/// материалы — из таблицы 3 рекомендации, каждый в своём диапазоне частот; вне диапазона модель
/// не экстраполируется, а сообщает об ошибке.
/// </para>
/// <para>
/// Грунты в рекомендации заданы на 1–10 ГГц; для сотовых частот ниже 1 ГГц задайте проницаемость
/// и проводимость грунта явно через <see cref="FromConstants"/>.
/// </para>
/// </remarks>
public sealed class RadioMaterial
{
    private static readonly double VacuumPermittivity = PhysicalConstants.VacuumPermittivity.SiValue;

    private readonly double _a, _b, _c, _d;

    private RadioMaterial(string name, double a, double b, double c, double d, double minFrequencyHz, double maxFrequencyHz, bool perfect)
    {
        Name = name;
        _a = a;
        _b = b;
        _c = c;
        _d = d;
        MinFrequencyHz = minFrequencyHz;
        MaxFrequencyHz = maxFrequencyHz;
        IsPerfectConductor = perfect;
    }

    /// <summary>Название</summary>
    public string Name { get; }

    /// <summary>Идеальный проводник: коэффициенты отражения −1 и +1 при любом угле</summary>
    public bool IsPerfectConductor { get; }

    /// <summary>Нижняя граница применимости, Гц</summary>
    public double MinFrequencyHz { get; }

    /// <summary>Верхняя граница применимости, Гц</summary>
    public double MaxFrequencyHz { get; }

    /// <summary>Материал с постоянными проницаемостью и проводимостью</summary>
    /// <param name="name">Название</param>
    /// <param name="relativePermittivity">Относительная диэлектрическая проницаемость ε′</param>
    /// <param name="conductivitySiemensPerMetre">Проводимость σ, См/м</param>
    public static RadioMaterial FromConstants(string name, double relativePermittivity, double conductivitySiemensPerMetre)
    {
        Wave.RequirePositive(relativePermittivity, nameof(relativePermittivity));

        if (!(conductivitySiemensPerMetre >= 0) || double.IsInfinity(conductivitySiemensPerMetre))
            throw new ArgumentOutOfRangeException(nameof(conductivitySiemensPerMetre), "Проводимость — конечное неотрицательное число");

        return new RadioMaterial(name, relativePermittivity, 0, conductivitySiemensPerMetre, 0, 0, double.PositiveInfinity, false);
    }

    /// <summary>Материал по частотной модели ITU-R P.2040</summary>
    /// <param name="name">Название</param>
    /// <param name="a">Множитель проницаемости</param>
    /// <param name="b">Показатель степени проницаемости</param>
    /// <param name="c">Множитель проводимости, См/м</param>
    /// <param name="d">Показатель степени проводимости</param>
    /// <param name="minFrequencyGHz">Нижняя граница диапазона, ГГц</param>
    /// <param name="maxFrequencyGHz">Верхняя граница диапазона, ГГц</param>
    public static RadioMaterial FromItuModel(string name, double a, double b, double c, double d, double minFrequencyGHz, double maxFrequencyGHz)
    {
        Wave.RequirePositive(a, nameof(a));

        if (!(c >= 0) || !(minFrequencyGHz > 0) || !(maxFrequencyGHz > minFrequencyGHz))
            throw new ArgumentException("Проводимость неотрицательна, диапазон частот непуст");

        return new RadioMaterial(name, a, b, c, d, minFrequencyGHz * 1e9, maxFrequencyGHz * 1e9, false);
    }

    /// <summary>Идеальный проводник</summary>
    public static RadioMaterial PerfectConductor { get; } = new("идеальный проводник", 1, 0, 0, 0, 0, double.PositiveInfinity, true);

    /// <summary>Бетон, 1–100 ГГц (ITU-R P.2040, табл. 3)</summary>
    public static RadioMaterial Concrete { get; } = FromItuModel("бетон", 5.24, 0, 0.0462, 0.7822, 1, 100);

    /// <summary>Кирпич, 1–40 ГГц</summary>
    public static RadioMaterial Brick { get; } = FromItuModel("кирпич", 3.91, 0, 0.0238, 0.16, 1, 40);

    /// <summary>Гипсокартон, 1–100 ГГц</summary>
    public static RadioMaterial Plasterboard { get; } = FromItuModel("гипсокартон", 2.73, 0, 0.0085, 0.9395, 1, 100);

    /// <summary>Дерево, 1 МГц — 100 ГГц</summary>
    public static RadioMaterial Wood { get; } = FromItuModel("дерево", 1.99, 0, 0.0047, 1.0718, 0.001, 100);

    /// <summary>Стекло, 0,1–100 ГГц</summary>
    public static RadioMaterial Glass { get; } = FromItuModel("стекло", 6.31, 0, 0.0036, 1.3394, 0.1, 100);

    /// <summary>Потолочная плита, 1–100 ГГц</summary>
    public static RadioMaterial CeilingBoard { get; } = FromItuModel("потолочная плита", 1.48, 0, 0.0011, 1.0750, 1, 100);

    /// <summary>ДСП, 1–100 ГГц</summary>
    public static RadioMaterial Chipboard { get; } = FromItuModel("ДСП", 2.58, 0, 0.0217, 0.7800, 1, 100);

    /// <summary>Фанера, 1–40 ГГц</summary>
    public static RadioMaterial Plywood { get; } = FromItuModel("фанера", 2.71, 0, 0.33, 0, 1, 40);

    /// <summary>Мрамор, 1–60 ГГц</summary>
    public static RadioMaterial Marble { get; } = FromItuModel("мрамор", 7.074, 0, 0.0055, 0.9262, 1, 60);

    /// <summary>Паркет, 50–100 ГГц</summary>
    public static RadioMaterial Floorboard { get; } = FromItuModel("паркет", 3.66, 0, 0.0044, 1.3515, 50, 100);

    /// <summary>Металл, 1–100 ГГц: проводимость 10⁷ См/м</summary>
    public static RadioMaterial Metal { get; } = FromItuModel("металл", 1, 0, 1e7, 0, 1, 100);

    /// <summary>Очень сухой грунт, 1–10 ГГц</summary>
    public static RadioMaterial VeryDryGround { get; } = FromItuModel("очень сухой грунт", 3, 0, 0.00015, 2.52, 1, 10);

    /// <summary>Грунт средней влажности, 1–10 ГГц</summary>
    public static RadioMaterial MediumDryGround { get; } = FromItuModel("грунт средней влажности", 15, -0.1, 0.035, 1.63, 1, 10);

    /// <summary>Влажный грунт, 1–10 ГГц</summary>
    public static RadioMaterial WetGround { get; } = FromItuModel("влажный грунт", 30, -0.4, 0.15, 1.30, 1, 10);

    /// <summary>Относительная диэлектрическая проницаемость ε′ на частоте</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public double RelativePermittivity(double frequencyHz) => _a * Math.Pow(Check(frequencyHz) / 1e9, _b);

    /// <summary>Проводимость на частоте, См/м</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public double Conductivity(double frequencyHz) => _c * Math.Pow(Check(frequencyHz) / 1e9, _d);

    /// <summary>Комплексная относительная проницаемость ε′ − j·σ/(2πf·ε₀)</summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    public Complex ComplexPermittivity(double frequencyHz)
    {
        if (IsPerfectConductor)
            throw new InvalidOperationException("У идеального проводника проницаемость бесконечна: коэффициенты отражения берутся напрямую");

        return new Complex(RelativePermittivity(frequencyHz), -Conductivity(frequencyHz) / (2 * Math.PI * frequencyHz * VacuumPermittivity));
    }

    /// <summary>Название</summary>
    public override string ToString() => Name;

    private double Check(double frequencyHz)
    {
        Wave.RequirePositive(frequencyHz, nameof(frequencyHz));

        return frequencyHz >= MinFrequencyHz * (1 - 1e-12) && frequencyHz <= MaxFrequencyHz * (1 + 1e-12)
            ? frequencyHz
            : throw new ArgumentOutOfRangeException(nameof(frequencyHz), frequencyHz,
                $"Модель «{Name}» определена на {MinFrequencyHz / 1e9:G4}–{MaxFrequencyHz / 1e9:G4} ГГц");
    }
}

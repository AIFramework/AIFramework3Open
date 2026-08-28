#nullable enable
using System;

namespace AI.Units;

/// <summary>
/// Фундаментальные физические константы в базовых единицах СИ по рекомендациям CODATA 2022.
/// Константы, входящие в определение единиц СИ (c, h, e, k, N<sub>A</sub>), точны по определению;
/// для измеряемых констант стандартная неопределённость доступна в <see cref="WithUncertainty"/>.
/// </summary>
public static class PhysicalConstants
{
    #region Точные по определению

    /// <summary>Скорость света в вакууме, 299 792 458 м/с (точно)</summary>
    public static Quantity SpeedOfLight { get; } = new(299_792_458.0, Dimension.Velocity);

    /// <summary>Постоянная Планка, 6.626 070 15·10⁻³⁴ Дж·с (точно)</summary>
    public static Quantity PlanckConstant { get; } = new(6.62607015e-34, Dimension.Energy * Dimension.TimeDim);

    /// <summary>Приведённая постоянная Планка ℏ = h/2π (точно)</summary>
    public static Quantity ReducedPlanckConstant { get; } = new(6.62607015e-34 / (2.0 * Math.PI), Dimension.Energy * Dimension.TimeDim);

    /// <summary>Элементарный заряд, 1.602 176 634·10⁻¹⁹ Кл (точно)</summary>
    public static Quantity ElementaryCharge { get; } = new(1.602176634e-19, Dimension.Charge);

    /// <summary>Постоянная Больцмана, 1.380 649·10⁻²³ Дж/К (точно)</summary>
    public static Quantity BoltzmannConstant { get; } = new(1.380649e-23, Dimension.Energy / Dimension.TemperatureDim);

    /// <summary>Число Авогадро, 6.022 140 76·10²³ моль⁻¹ (точно)</summary>
    public static Quantity AvogadroConstant { get; } = new(6.02214076e23, Dimension.AmountDim.Pow(-1));

    /// <summary>Универсальная газовая постоянная R = N<sub>A</sub>k, 8.314 462 618 153 24 Дж/(моль·К) (точно)</summary>
    public static Quantity GasConstant { get; } = new(8.31446261815324, Dimension.Energy / Dimension.AmountDim / Dimension.TemperatureDim);

    /// <summary>Постоянная Фарадея F = N<sub>A</sub>e, 96 485.332 123 310 01 Кл/моль (точно)</summary>
    public static Quantity FaradayConstant { get; } = new(96485.33212331001, Dimension.Charge / Dimension.AmountDim);

    /// <summary>Постоянная Стефана — Больцмана, 5.670 374 419·10⁻⁸ Вт/(м²·К⁴) (точно)</summary>
    public static Quantity StefanBoltzmannConstant { get; } =
        new(5.670374419184431e-8, Dimension.Power / Dimension.Area / Dimension.TemperatureDim.Pow(4));

    /// <summary>Стандартное ускорение свободного падения, 9.806 65 м/с² (точно по определению)</summary>
    public static Quantity StandardGravity { get; } = new(9.80665, Dimension.Acceleration);

    /// <summary>Стандартная атмосфера, 101 325 Па (точно по определению)</summary>
    public static Quantity StandardAtmosphere { get; } = new(101_325.0, Dimension.Pressure);

    /// <summary>Абсолютный нуль в градусах Цельсия — точка отсчёта шкалы, 273.15 К (точно)</summary>
    public static Quantity Icepoint { get; } = new(273.15, Dimension.TemperatureDim);

    #endregion

    #region Измеряемые

    /// <summary>Гравитационная постоянная, 6.674 30·10⁻¹¹ м³/(кг·с²)</summary>
    public static Quantity GravitationalConstant { get; } = WithUncertainty.GravitationalConstant.Value;

    /// <summary>Электрическая постоянная ε₀, 8.854 187 8188·10⁻¹² Ф/м</summary>
    public static Quantity VacuumPermittivity { get; } = WithUncertainty.VacuumPermittivity.Value;

    /// <summary>Магнитная постоянная μ₀, 1.256 637 061 27·10⁻⁶ Н/А²</summary>
    public static Quantity VacuumPermeability { get; } = WithUncertainty.VacuumPermeability.Value;

    /// <summary>Масса покоя электрона, 9.109 383 7139·10⁻³¹ кг</summary>
    public static Quantity ElectronMass { get; } = WithUncertainty.ElectronMass.Value;

    /// <summary>Масса покоя протона, 1.672 621 925 95·10⁻²⁷ кг</summary>
    public static Quantity ProtonMass { get; } = WithUncertainty.ProtonMass.Value;

    /// <summary>Масса покоя нейтрона, 1.674 927 500 56·10⁻²⁷ кг</summary>
    public static Quantity NeutronMass { get; } = WithUncertainty.NeutronMass.Value;

    /// <summary>Атомная единица массы, 1.660 539 068 92·10⁻²⁷ кг</summary>
    public static Quantity AtomicMassUnit { get; } = WithUncertainty.AtomicMassUnit.Value;

    /// <summary>Постоянная тонкой структуры α, 7.297 352 5643·10⁻³ (безразмерная)</summary>
    public static Quantity FineStructureConstant { get; } = WithUncertainty.FineStructureConstant.Value;

    /// <summary>Радиус Бора, 5.291 772 105 44·10⁻¹¹ м</summary>
    public static Quantity BohrRadius { get; } = WithUncertainty.BohrRadius.Value;

    /// <summary>Постоянная Ридберга, 10 973 731.568 157 м⁻¹</summary>
    public static Quantity RydbergConstant { get; } = WithUncertainty.RydbergConstant.Value;

    #endregion

    /// <summary>
    /// Те же константы вместе со стандартной неопределённостью CODATA 2022.
    /// Для точных по определению констант неопределённость равна нулю.
    /// </summary>
    public static class WithUncertainty
    {
        /// <summary>Гравитационная постоянная</summary>
        public static Measurement GravitationalConstant { get; } = new(
            new Quantity(6.67430e-11, Dimension.Volume / Dimension.MassDim / Dimension.TimeDim.Pow(2)), 0.00015e-11);

        /// <summary>Электрическая постоянная ε₀</summary>
        public static Measurement VacuumPermittivity { get; } = new(
            new Quantity(8.8541878188e-12, Dimension.Capacitance / Dimension.LengthDim), 0.0000000014e-12);

        /// <summary>Магнитная постоянная μ₀</summary>
        public static Measurement VacuumPermeability { get; } = new(
            new Quantity(1.25663706127e-6, Dimension.Inductance / Dimension.LengthDim), 0.00000000020e-6);

        /// <summary>Масса покоя электрона</summary>
        public static Measurement ElectronMass { get; } = new(new Quantity(9.1093837139e-31, Dimension.MassDim), 0.0000000028e-31);

        /// <summary>Масса покоя протона</summary>
        public static Measurement ProtonMass { get; } = new(new Quantity(1.67262192595e-27, Dimension.MassDim), 0.00000000052e-27);

        /// <summary>Масса покоя нейтрона</summary>
        public static Measurement NeutronMass { get; } = new(new Quantity(1.67492750056e-27, Dimension.MassDim), 0.00000000085e-27);

        /// <summary>Атомная единица массы</summary>
        public static Measurement AtomicMassUnit { get; } = new(new Quantity(1.66053906892e-27, Dimension.MassDim), 0.00000000052e-27);

        /// <summary>Постоянная тонкой структуры</summary>
        public static Measurement FineStructureConstant { get; } = new(Quantity.Dimensionless(7.2973525643e-3), 0.0000000011e-3);

        /// <summary>Радиус Бора</summary>
        public static Measurement BohrRadius { get; } = new(new Quantity(5.29177210544e-11, Dimension.LengthDim), 0.00000000082e-11);

        /// <summary>Постоянная Ридберга</summary>
        public static Measurement RydbergConstant { get; } = new(new Quantity(10_973_731.568157, Dimension.LengthDim.Pow(-1)), 0.000012);

        /// <summary>Скорость света (точно)</summary>
        public static Measurement SpeedOfLight { get; } = Measurement.Exact(new Quantity(299_792_458.0, Dimension.Velocity));

        /// <summary>Постоянная Планка (точно)</summary>
        public static Measurement PlanckConstant { get; } =
            Measurement.Exact(new Quantity(6.62607015e-34, Dimension.Energy * Dimension.TimeDim));

        /// <summary>Постоянная Больцмана (точно)</summary>
        public static Measurement BoltzmannConstant { get; } =
            Measurement.Exact(new Quantity(1.380649e-23, Dimension.Energy / Dimension.TemperatureDim));
    }
}

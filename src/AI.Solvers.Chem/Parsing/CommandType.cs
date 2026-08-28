namespace AI.Solvers.Chem.Parsing;

public enum CommandType
{
    Unknown,

    // Неорганика
    Balance,
    CalculateMass,
    Stoichiometry,
    MolarityCalculation,
    Dilution,
    MixSolutions,
    PhCalculation,
    BufferPH,
    Titration,
    OxidationStates,
    RedoxBalance,
    IdealGas,
    CombinedGas,
    PartialPressure,
    ThermoCalculation,
    HessLaw,
    RateLaw,
    HalfLife,
    Arrhenius,
    NernstEquation,
    FaradayLaw,

    // Растворимость
    Solubility,
    SolubilityCommonIon,
    PredictPrecipitation,
    FractionalPrecipitation,

    // Комплексные соединения
    ComplexFormation,
    StepwiseComplexation,
    ComplexationAtPH,
    ChelateEffect,

    // Кинетика расширенная
    DetermineOrder,
    IntegratedRateLaw,

    // Органика
    ParseSmiles,
    GenerateSmiles,
    GenerateIsomers,
    FunctionalGroups,
    PredictProduct,
    Retrosynthesis,
    IUPACNaming,

    // Справочные
    ElementInfo,
    CompoundLookup,
    Help,
    
    // Анализ структуры
    Properties,

    // Медицинские расчёты
    Pharmacokinetics,
    PharmacokineticsHalfLife, // Добавлено
    CalculateDose,
    BloodGasAnalysis,
    CalculateBicarbonate,
    BaseExcess,

    // Кинетика ферментов
    MichaelisMenten,
    LineweaverBurk,
    EnzymeInhibition,
    SpecificActivity,

    // Спектроскопия
    BeersLaw,
    MixtureAnalysis,
    CalibrationCurve
}

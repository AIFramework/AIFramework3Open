// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:


using FractalAgentsAI.Solvers.Chem.Database;
using FractalAgentsAI.Solvers.Chem.Parsing;
using FractalAgentsAI.Solvers.Chem.Processors;
using FractalAgentsAI.Solvers.Chem.Processors.Inorganic;
using FractalAgentsAI.Solvers.Chem.Processors.Organic;
using FractalAgentsAI.Solvers.Chem.Processors.Physical;
using FractalAgentsAI.Solvers.Chem.Processors.Medical;
using FractalAgentsAI.Solvers.Chem.Processors.Analytical;
using System.Text;

namespace FractalAgentsAI.Solvers.Chem.Core;

// ОСНОВНОЙ ДВИЖОК

public class ChemEngine : IChemEngine
{
    private readonly CommandParser _parser;
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    // Процессоры - Неорганическая химия
    private readonly EquationBalancer _balancer;
    private readonly StoichiometryCalculator _stoichiometry;
    private readonly SolutionCalculator _solutions;
    private readonly ThermochemistryCalculator _thermo;
    private readonly AcidBaseCalculator _acidBase;
    private readonly RedoxProcessor _redox;
    private readonly SolubilityCalculator _solubility;
    private readonly ComplexCompoundsCalculator _complexes;
    
    // Процессоры - Физическая химия
    private readonly GasLawCalculator _gasLaws;
    private readonly KineticsCalculator _kinetics;
    private readonly ElectrochemistryCalculator _electrochem;
    
    // Процессоры - Органическая химия
    private readonly OrganicChemProcessor _organic;
    private readonly MolecularPropertiesCalculator _propsCalculator;
    
    // Процессоры - Медицинские расчёты
    private readonly PharmacokineticCalculator _pharmacokinetics;
    private readonly BloodGasAnalyzer _bloodGas;
    private readonly EnzymeKineticsCalculator _enzymeKinetics;
    
    // Процессоры - Аналитическая химия
    private readonly SpectroscopyCalculator _spectroscopy;

    public ChemEngine(VerbosityLevel verbosity = VerbosityLevel.Normal)
    {
        _verbosity = verbosity;
        _parser = new CommandParser();
        _database = new ChemDatabase();

        // Инициализация процессоров - Неорганическая химия
        _balancer = new EquationBalancer(_database, _verbosity);
        _stoichiometry = new StoichiometryCalculator(_database, _verbosity);
        _solutions = new SolutionCalculator(_database, _verbosity);
        _thermo = new ThermochemistryCalculator(_database, _verbosity);
        _acidBase = new AcidBaseCalculator(_database, _verbosity);
        _redox = new RedoxProcessor(_database, _verbosity);
        _solubility = new SolubilityCalculator(_database, _verbosity);
        _complexes = new ComplexCompoundsCalculator(_database, _verbosity);
        
        // Физическая химия
        _gasLaws = new GasLawCalculator(_verbosity);
        _kinetics = new KineticsCalculator(_verbosity);
        _electrochem = new ElectrochemistryCalculator(_database, _verbosity);
        
        // Органическая химия
        _organic = new OrganicChemProcessor(_database, _verbosity);
        _propsCalculator = new MolecularPropertiesCalculator(_verbosity);
        
        // Медицинские расчёты
        _pharmacokinetics = new PharmacokineticCalculator(_verbosity);
        _bloodGas = new BloodGasAnalyzer(_verbosity);
        _enzymeKinetics = new EnzymeKineticsCalculator(_verbosity);
        
        // Аналитическая химия
        _spectroscopy = new SpectroscopyCalculator(_verbosity);

        // Загрузка базовых данных
        _database.Initialize();
    }

    public ChemResult Execute(string command)
    {
        try
        {
            // Проверка на команду помощи
            if (command.Trim().ToLower().StartsWith("help"))
            {
                var topic = command.Trim().Length > 4 ? command.Trim().Substring(4).Trim() : "";
                return ChemResult.Ok(HelpSystem.GetHelp(topic));
            }

            var parsed = _parser.Parse(command);

            if (!parsed.Success)
                return ChemResult.Error(parsed.ErrorMessage);

            return parsed.CommandType switch
            {
                // Неорганическая химия
                CommandType.Balance => _balancer.Balance(parsed),
                CommandType.CalculateMass => _stoichiometry.CalculateMolarMass(parsed),
                CommandType.Stoichiometry => _stoichiometry.Calculate(parsed),
                CommandType.MolarityCalculation => _solutions.CalculateMolarity(parsed),
                CommandType.Dilution => _solutions.Dilute(parsed),
                CommandType.MixSolutions => _solutions.Mix(parsed),
                CommandType.PhCalculation => _acidBase.CalculatePH(parsed),
                CommandType.BufferPH => _acidBase.CalculateBufferPH(parsed),
                CommandType.Titration => _acidBase.Titration(parsed),
                CommandType.OxidationStates => _redox.FindOxidationStates(parsed),
                CommandType.RedoxBalance => _redox.BalanceRedox(parsed),
                
                // Растворимость
                CommandType.Solubility => _solubility.CalculateSolubility(parsed),
                CommandType.SolubilityCommonIon => _solubility.CalculateSolubilityWithCommonIon(parsed),
                CommandType.PredictPrecipitation => _solubility.PredictPrecipitation(parsed),
                CommandType.FractionalPrecipitation => _solubility.FractionalPrecipitation(parsed),
                
                // Комплексные соединения
                CommandType.ComplexFormation => _complexes.CalculateComplexConcentration(parsed),
                CommandType.StepwiseComplexation => _complexes.StepwiseComplexation(parsed),
                CommandType.ComplexationAtPH => _complexes.ComplexationAtPH(parsed),
                CommandType.ChelateEffect => _complexes.ChelateEffect(parsed),
                
                // Физическая химия
                CommandType.IdealGas => _gasLaws.IdealGasLaw(parsed),
                CommandType.CombinedGas => _gasLaws.CombinedGasLaw(parsed),
                CommandType.PartialPressure => _gasLaws.DaltonLaw(parsed),
                CommandType.ThermoCalculation => _thermo.CalculateDeltaH(parsed),
                CommandType.HessLaw => _thermo.HessLaw(parsed),
                CommandType.RateLaw => _kinetics.CalculateRate(parsed),
                CommandType.HalfLife => _kinetics.CalculateHalfLife(parsed),
                CommandType.Arrhenius => _kinetics.Arrhenius(parsed),
                CommandType.DetermineOrder => _kinetics.DetermineOrder(parsed),
                CommandType.IntegratedRateLaw => _kinetics.IntegratedRateLaw(parsed),
                CommandType.NernstEquation => _electrochem.Nernst(parsed),
                CommandType.FaradayLaw => _electrochem.Faraday(parsed),

                // Органическая химия
                CommandType.ParseSmiles => _organic.ParseSmiles(parsed),
                CommandType.GenerateSmiles => _organic.GenerateSmiles(parsed),
                CommandType.GenerateIsomers => _organic.GenerateIsomers(parsed),
                CommandType.FunctionalGroups => _organic.IdentifyFunctionalGroups(parsed),
                CommandType.PredictProduct => _organic.PredictProduct(parsed),
                CommandType.Retrosynthesis => _organic.Retrosynthesis(parsed),
                CommandType.IUPACNaming => _organic.IUPACName(parsed),
                CommandType.Properties => _propsCalculator.CalculateProperties(parsed.Parameters["smiles"]),

                // Медицинские расчёты
                CommandType.Pharmacokinetics => _pharmacokinetics.OneCompartmentModel(parsed),
                CommandType.PharmacokineticsHalfLife => _pharmacokinetics.CalculateHalfLife(parsed),
                CommandType.CalculateDose => _pharmacokinetics.CalculateDose(parsed),
                CommandType.BloodGasAnalysis => _bloodGas.AnalyzeBloodGas(parsed),
                CommandType.CalculateBicarbonate => _bloodGas.CalculateBicarbonate(parsed),
                CommandType.BaseExcess => _bloodGas.CalculateBaseExcess(parsed),
                
                // Кинетика ферментов
                CommandType.MichaelisMenten => _enzymeKinetics.MichaelisMenten(parsed),
                CommandType.LineweaverBurk => _enzymeKinetics.LineweaverBurk(parsed),
                CommandType.EnzymeInhibition => parsed.Parameters.GetValueOrDefault("inhibition_type", "competitive") == "competitive"
                    ? _enzymeKinetics.CompetitiveInhibition(parsed)
                    : _enzymeKinetics.NonCompetitiveInhibition(parsed),
                CommandType.SpecificActivity => _enzymeKinetics.CalculateSpecificActivity(parsed),
                
                // Аналитическая химия
                CommandType.BeersLaw => _spectroscopy.BeersLaw(parsed),
                CommandType.MixtureAnalysis => _spectroscopy.MixtureAnalysis(parsed),
                CommandType.CalibrationCurve => _spectroscopy.CalibrationCurve(parsed),

                // Справочные
                CommandType.ElementInfo => GetElementInfo(parsed),
                CommandType.CompoundLookup => LookupCompound(parsed),

                _ => ChemResult.Error($"Command type '{parsed.CommandType}' not implemented")
            };
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Execution error: {ex.Message}");
        }
    }

    public async Task<ChemResult> ExecuteAsync(string command)
    {
        return await Task.Run(() => Execute(command));
    }

    public void SetVerbosity(VerbosityLevel level)
    {
        // Обновить для всех процессоров
    }

    public void LoadCustomDatabase(string jsonPath)
    {
        _database.LoadFromJson(jsonPath);
    }

    public void LoadReactionRules(string rulesPath)
    {
        _organic.LoadReactionRules(rulesPath);
    }

    private ChemResult GetElementInfo(ParsedCommand cmd)
    {
        var symbol = cmd.Parameters["element"];
        var element = _database.GetElement(symbol);

        if (element == null)
            return ChemResult.Error($"Element '{symbol}' not found");

        var result = new StringBuilder();
        result.AppendLine($"Element: {element.Name} ({element.Symbol})");
        result.AppendLine($"Atomic Number: {element.AtomicNumber}");
        result.AppendLine($"Atomic Mass: {element.AtomicMass:F4} u");
        result.AppendLine($"Group: {element.Group}, Period: {element.Period}");
        result.AppendLine($"Electronegativity: {element.Electronegativity:F2}");
        result.AppendLine($"Electron Configuration: {element.ElectronConfiguration}");
        result.AppendLine($"Oxidation States: {string.Join(", ", element.OxidationStates)}");

        return new ChemResult
        {
            Success = true,
            Result = result.ToString(),
            Data = new Dictionary<string, object> { ["element"] = element }
        };
    }

    private ChemResult LookupCompound(ParsedCommand cmd)
    {
        var identifier = cmd.Parameters["compound"];
        var compound = _database.LookupCompound(identifier);

        if (compound == null)
            return ChemResult.Error($"Compound '{identifier}' not found");

        var result = new StringBuilder();
        result.AppendLine($"Compound: {compound.CommonName}");
        result.AppendLine($"Formula: {compound.Formula}");
        result.AppendLine($"SMILES: {compound.SMILES}");
        result.AppendLine($"Molar Mass: {compound.MolarMass:F2} g/mol");

        if (compound.Properties != null)
        {
            result.AppendLine("\nPhysical Properties:");
            if (compound.Properties.MeltingPoint.HasValue)
                result.AppendLine($"  Melting Point: {compound.Properties.MeltingPoint:F1} °C");
            if (compound.Properties.BoilingPoint.HasValue)
                result.AppendLine($"  Boiling Point: {compound.Properties.BoilingPoint:F1} °C");
            if (compound.Properties.Density.HasValue)
                result.AppendLine($"  Density: {compound.Properties.Density:F2} g/cm³");
        }

        return ChemResult.Ok(result.ToString());
    }
}

using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// ТЕРМОХИМИЯ
// ═══════════════════════════════════════════════════════════
public class ThermochemistryCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    public ThermochemistryCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult CalculateDeltaH(ParsedCommand cmd)
    {
        try
        {
            // ΔH = Σ ΔHf(products) - Σ ΔHf(reactants)
            var reactants = MolecularFormula.ParseSide(cmd.GetString("reactants"));
            var products = MolecularFormula.ParseSide(cmd.GetString("products"));

            // Отсутствие ΔHf в справочнике - это ошибка, а не ноль: иначе ΔH молча занижается
            var missing = reactants.Concat(products)
                .Where(f => _database.GetStandardEnthalpy(f.CoreFormula, f.State) == null)
                .Select(f => f.CoreFormula + (f.State == null ? string.Empty : $"({f.State})"))
                .Distinct()
                .ToList();

            if (missing.Count > 0)
                return ChemResult.Error($"Standard enthalpy of formation is not available for: {string.Join(", ", missing)}");

            double deltaH_reactants = reactants.Sum(r => _database.GetStandardEnthalpy(r.CoreFormula, r.State).Value * r.Coefficient);
            double deltaH_products = products.Sum(p => _database.GetStandardEnthalpy(p.CoreFormula, p.State).Value * p.Coefficient);

            var deltaH = deltaH_products - deltaH_reactants;

            var result = ChemResult.Ok($"ΔH = {deltaH:F1} kJ");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Using: ΔH = Σ ΔHf(products) - Σ ΔHf(reactants)");
                result.Steps.Add("\nReactants:");
                foreach (var r in reactants)
                {
                    var enthalpy = _database.GetStandardEnthalpy(r.CoreFormula, r.State);
                    result.Steps.Add($"  {r}: {enthalpy:F1} kJ/mol × {r.Coefficient}");
                }
                result.Steps.Add($"  Sum = {deltaH_reactants:F1} kJ");

                result.Steps.Add("\nProducts:");
                foreach (var p in products)
                {
                    var enthalpy = _database.GetStandardEnthalpy(p.CoreFormula, p.State);
                    result.Steps.Add($"  {p}: {enthalpy:F1} kJ/mol × {p.Coefficient}");
                }
                result.Steps.Add($"  Sum = {deltaH_products:F1} kJ");

                result.Steps.Add($"\nΔH = {deltaH_products:F1} - ({deltaH_reactants:F1}) = {deltaH:F1} kJ");

                if (deltaH < 0)
                    result.Steps.Add("Reaction is exothermic (releases heat)");
                else
                    result.Steps.Add("Reaction is endothermic (absorbs heat)");
            }

            result.Data["deltaH"] = deltaH;

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Thermochemistry calculation failed: {ex.Message}");
        }
    }

    public ChemResult HessLaw(ParsedCommand cmd)
    {
        return ChemResult.Error("Hess's law calculations not yet implemented");
    }
}

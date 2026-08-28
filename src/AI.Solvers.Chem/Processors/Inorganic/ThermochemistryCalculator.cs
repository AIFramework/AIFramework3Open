using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using System.Text.RegularExpressions;

namespace FractalAgentsAI.Solvers.Chem.Processors.Inorganic;

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
            var reactants = ParseSide(cmd.Parameters["reactants"]);
            var products = ParseSide(cmd.Parameters["products"]);

            double deltaH_reactants = 0;
            double deltaH_products = 0;

            foreach (var r in reactants)
            {
                var enthalpy = _database.GetStandardEnthalpy(r.Formula);
                if (enthalpy.HasValue)
                    deltaH_reactants += enthalpy.Value * r.Coefficient;
            }

            foreach (var p in products)
            {
                var enthalpy = _database.GetStandardEnthalpy(p.Formula);
                if (enthalpy.HasValue)
                    deltaH_products += enthalpy.Value * p.Coefficient;
            }

            var deltaH = deltaH_products - deltaH_reactants;

            var result = ChemResult.Ok($"ΔH = {deltaH:F1} kJ");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Using: ΔH = Σ ΔHf(products) - Σ ΔHf(reactants)");
                result.Steps.Add("\nReactants:");
                foreach (var r in reactants)
                {
                    var enthalpy = _database.GetStandardEnthalpy(r.Formula);
                    result.Steps.Add($"  {r.Formula}: {enthalpy:F1} kJ/mol × {r.Coefficient}");
                }
                result.Steps.Add($"  Sum = {deltaH_reactants:F1} kJ");

                result.Steps.Add("\nProducts:");
                foreach (var p in products)
                {
                    var enthalpy = _database.GetStandardEnthalpy(p.Formula);
                    result.Steps.Add($"  {p.Formula}: {enthalpy:F1} kJ/mol × {p.Coefficient}");
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

    private List<(string Formula, int Coefficient)> ParseSide(string side)
    {
        var result = new List<(string, int)>();
        var parts = side.Split('+').Select(p => p.Trim()).ToList();

        foreach (var part in parts)
        {
            // Извлекаем коэффициент
            var match = Regex.Match(part, @"^(\d+)?\s*(.+)");
            if (match.Success)
            {
                var coeff = string.IsNullOrEmpty(match.Groups[1].Value)
                    ? 1
                    : int.Parse(match.Groups[1].Value);
                var formula = match.Groups[2].Value.Trim();

                // Убираем состояния (g), (l), (s)
                formula = Regex.Replace(formula, @"\(.*?\)", "").Trim();

                result.Add((formula, coeff));
            }
        }

        return result;
    }

    public ChemResult HessLaw(ParsedCommand cmd)
    {
        return ChemResult.Error("Hess's law calculations not yet implemented");
    }
}

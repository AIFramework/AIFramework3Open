using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// СТЕХИОМЕТРИЧЕСКИЕ РАСЧЕТЫ
// ═══════════════════════════════════════════════════════════
public class StoichiometryCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    public StoichiometryCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult CalculateMolarMass(ParsedCommand cmd)
    {
        try
        {
            var formula = cmd.GetString("formula", "compound", "substance");
            var molecular = new MolecularFormula(formula);
            var mass = molecular.CalculateMolarMass(_database);

            var result = ChemResult.Ok($"Molar mass of {formula}: {mass:F3} g/mol");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Element composition:");
                foreach (var kvp in molecular.Elements)
                {
                    var element = _database.GetElement(kvp.Key);
                    var elementMass = element.AtomicMass * kvp.Value;
                    result.Steps.Add($"  {kvp.Key}: {kvp.Value} × {element.AtomicMass:F3} = {elementMass:F3} g/mol");
                }
                result.Steps.Add($"Total: {mass:F3} g/mol");
            }

            result.Data["molar_mass"] = mass;
            result.Data["formula"] = formula;
            result.Data["elements"] = molecular.Elements;

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Error calculating molar mass: {ex.Message}");
        }
    }

    public ChemResult Calculate(ParsedCommand cmd)
    {
        try
        {
            var target = cmd.GetString("target");
            var mass = cmd.GetDouble("mass");
            var source = cmd.GetString("source");

            // Простейший случай: A -> B
            var sourceMol = new MolecularFormula(source);
            var targetMol = new MolecularFormula(target);

            var sourceMolarMass = sourceMol.CalculateMolarMass(_database);
            var targetMolarMass = targetMol.CalculateMolarMass(_database);

            // Моли исходного вещества
            var sourceMoles = mass / sourceMolarMass;

            // Предполагаем коэффициенты 1:1 (упрощение)
            var targetMoles = sourceMoles;
            var targetMass = targetMoles * targetMolarMass;

            var result = ChemResult.Ok(
                $"Mass of {target} produced: {targetMass:F2} g");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"1. Molar mass of {source}: {sourceMolarMass:F2} g/mol");
                result.Steps.Add($"2. Moles of {source}: {mass:F2} g ÷ {sourceMolarMass:F2} g/mol = {sourceMoles:F4} mol");
                result.Steps.Add($"3. Molar mass of {target}: {targetMolarMass:F2} g/mol");
                result.Steps.Add($"4. Moles of {target}: {targetMoles:F4} mol (1:1 ratio)");
                result.Steps.Add($"5. Mass of {target}: {targetMoles:F4} mol × {targetMolarMass:F2} g/mol = {targetMass:F2} g");
            }

            result.Data["source_moles"] = sourceMoles;
            result.Data["target_moles"] = targetMoles;
            result.Data["target_mass"] = targetMass;

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Stoichiometry calculation failed: {ex.Message}");
        }
    }
}

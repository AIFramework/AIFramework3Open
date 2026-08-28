using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// РАСЧЕТЫ С РАСТВОРАМИ
// ═══════════════════════════════════════════════════════════
public class SolutionCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    public SolutionCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult CalculateMolarity(ParsedCommand cmd)
    {
        try
        {
            var mass = cmd.GetDouble("mass", "m");
            var substance = cmd.GetString("substance", "compound", "formula");
            var volumeML = cmd.GetDouble("volume", "V");
            var volumeL = volumeML / 1000.0;

            var formula = new MolecularFormula(substance);
            var molarMass = formula.CalculateMolarMass(_database);
            var moles = mass / molarMass;
            var molarity = moles / volumeL;

            var result = ChemResult.Ok(
                $"Molarity: {molarity:F3} M ({molarity:F3} mol/L)");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"1. Molar mass of {substance}: {molarMass:F2} g/mol");
                result.Steps.Add($"2. Moles: {mass:F2} g ÷ {molarMass:F2} g/mol = {moles:F4} mol");
                result.Steps.Add($"3. Volume: {volumeML:F0} mL = {volumeL:F3} L");
                result.Steps.Add($"4. Molarity: {moles:F4} mol ÷ {volumeL:F3} L = {molarity:F3} M");
            }

            result.Data["molarity"] = molarity;
            result.Data["moles"] = moles;
            result.Data["volume_L"] = volumeL;

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Molarity calculation failed: {ex.Message}");
        }
    }

    public ChemResult Dilute(ParsedCommand cmd)
    {
        try
        {
            var C1 = cmd.GetDouble("C1", "c1", "stock");
            var C2 = cmd.GetDouble("C2", "c2", "target");
            var V2 = cmd.GetDouble("V2", "v2", "volume");

            // C1*V1 = C2*V2
            var V1 = C2 * V2 / C1;
            var waterToAdd = V2 - V1;

            var result = ChemResult.Ok(
                $"Take {V1:F2} mL of {C1:F2} M solution and add {waterToAdd:F2} mL of water to get {V2:F2} mL of {C2:F2} M solution");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Using dilution formula: C₁V₁ = C₂V₂");
                result.Steps.Add($"Given: C₁ = {C1:F2} M, C₂ = {C2:F2} M, V₂ = {V2:F2} mL");
                result.Steps.Add($"V₁ = (C₂ × V₂) / C₁ = ({C2:F2} × {V2:F2}) / {C1:F2} = {V1:F2} mL");
                result.Steps.Add($"Water to add: V₂ - V₁ = {V2:F2} - {V1:F2} = {waterToAdd:F2} mL");
            }

            result.Data["V1"] = V1;
            result.Data["water_volume"] = waterToAdd;

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Dilution calculation failed: {ex.Message}");
        }
    }

    public ChemResult Mix(ParsedCommand cmd)
    {
        // Смешивание растворов - упрощенная версия
        return ChemResult.Error("Solution mixing not yet implemented");
    }
}

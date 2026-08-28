using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FractalAgentsAI.Solvers.Chem.Processors.Physical;

// ═══════════════════════════════════════════════════════════
// ЭЛЕКТРОХИМИЯ
// ═══════════════════════════════════════════════════════════
public class ElectrochemistryCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;
    private const double F = 96485; // Фарадея, Кл/моль
    private const double R = 8.314; // Дж/(моль·К)

    public ElectrochemistryCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult Nernst(ParsedCommand cmd)
    {
        try
        {
            // E = E0 - (RT/nF) * ln(Q)
            // E = E0 - (0.05916/n) * log10(Q) при 25°C
            
            double E0 = double.Parse(cmd.Parameters.GetValueOrDefault("E0", "0"), CultureInfo.InvariantCulture);
            double T = double.Parse(cmd.Parameters.GetValueOrDefault("T", "298.15"), CultureInfo.InvariantCulture);
            
            // Попытка определить n (число электронов)
            int n = 1;
            if (cmd.Parameters.ContainsKey("n"))
            {
                n = int.Parse(cmd.Parameters["n"]);
            }
            else
            {
                // Поиск "2e-" в команде
                var electronsMatch = Regex.Match(cmd.OriginalCommand, @"(\d+)e-");
                if (electronsMatch.Success)
                {
                    n = int.Parse(electronsMatch.Groups[1].Value);
                }
            }

            // Q = [Red]/[Ox] или наоборот
            // Предположим восстановление металла: M^n+ + ne- -> M
            // Q = 1 / [M^n+]
            
            double Q = 1.0;
            string metalIon = null;
            double ionConc = 1.0;

            // Ищем концентрацию иона в параметрах (например, [Cu2+]=0.01)
            foreach (var kvp in cmd.Parameters)
            {
                if (kvp.Key.StartsWith("[") && kvp.Key.EndsWith("]"))
                {
                    metalIon = kvp.Key.Trim('[', ']');
                    if (double.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out ionConc))
                    {
                        // Для восстановления металла Q = 1 / [Ox]
                        Q = 1.0 / ionConc;
                        break;
                    }
                }
            }

            double E = E0 - (R * T / (n * F)) * Math.Log(Q);
            
            var result = ChemResult.Ok($"E = {E:F3} V");
            result.Data["E"] = E;
            result.Data["E0"] = E0;
            result.Data["Q"] = Q;
            result.Data["n"] = n;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Nernst Equation");
                result.Steps.Add($"E = E° - (RT/nF)·ln(Q)");
                result.Steps.Add($"E° = {E0:F3} V");
                result.Steps.Add($"Temperature T = {T} K");
                result.Steps.Add($"Electrons n = {n}");
                if (metalIon != null)
                    result.Steps.Add($"[{metalIon}] = {ionConc} M => Q = 1/{ionConc} = {Q:F3}");
                result.Steps.Add($"\nE = {E0:F3} - ({R}·{T}/({n}·{F}))·ln({Q:F3})");
                result.Steps.Add($"E = {E:F3} V");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Nernst equation failed: {ex.Message}");
        }
    }

    public ChemResult Faraday(ParsedCommand cmd)
    {
        try
        {
            // m = (M * I * t) / (n * F)
            
            if (!cmd.Parameters.ContainsKey("I") && !cmd.Parameters.ContainsKey("current"))
                return ChemResult.Error("Current (I) is required");
                
            double I = double.Parse(cmd.Parameters.ContainsKey("I") ? cmd.Parameters["I"] : cmd.Parameters["current"], CultureInfo.InvariantCulture);
            double t = double.Parse(cmd.Parameters["time"], CultureInfo.InvariantCulture);
            
            string substance = cmd.Parameters["substance"];
            var element = _database.GetElement(substance);
            
            if (element == null)
                return ChemResult.Error($"Element '{substance}' not found");
                
            double M = element.AtomicMass;
            
            // Определяем валентность n
            int n = 1;
            if (cmd.Parameters.ContainsKey("n"))
            {
                n = int.Parse(cmd.Parameters["n"]);
            }
            else if (cmd.Parameters.ContainsKey("z"))
            {
                n = int.Parse(cmd.Parameters["z"]);
            }
            else if (element.OxidationStates.Length > 0)
            {
                // Берем наиболее вероятную положительную степень окисления
                n = element.OxidationStates.Where(s => s > 0).DefaultIfEmpty(1).Max();
            }

            double mass = (M * I * t) / (n * F);

            var result = ChemResult.Ok($"Mass deposited = {mass:F3} g");
            result.Data["mass"] = mass;
            result.Data["I"] = I;
            result.Data["time"] = t;
            result.Data["n"] = n;
            result.Data["M"] = M;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Faraday's Law of Electrolysis");
                result.Steps.Add($"Substance: {element.Name} ({element.Symbol})");
                result.Steps.Add($"Molar mass M = {M:F2} g/mol");
                result.Steps.Add($"Valency n = {n}");
                result.Steps.Add($"Current I = {I:F2} A");
                result.Steps.Add($"Time t = {t} s");
                result.Steps.Add($"Faraday constant F = {F} C/mol");
                result.Steps.Add($"\nm = (M·I·t)/(n·F)");
                result.Steps.Add($"m = ({M:F2}·{I:F2}·{t})/({n}·{F})");
                result.Steps.Add($"m = {mass:F3} g");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Faraday calculation failed: {ex.Message}");
        }
    }
}
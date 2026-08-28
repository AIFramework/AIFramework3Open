using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Physical;

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
            
            double T = cmd.GetDoubleOrDefault(298.15, "T", "temperature");

            // Ион: "ion=Cu2+" либо ключ-концентрация "[Cu2+]=0.01"
            string metalIon = cmd.GetStringOrDefault(null, "ion", "species", "metal");
            double ionConc = 1.0;
            bool ionConcKnown = false;

            if (metalIon != null)
                ionConcKnown = cmd.TryGetConcentration(out ionConc, metalIon);

            if (!ionConcKnown)
            {
                var bracketed = cmd.ConcentrationParameters
                    .FirstOrDefault(kvp => double.TryParse(kvp.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _));

                if (bracketed.Key != null)
                {
                    metalIon ??= bracketed.Key.Trim('[', ']');
                    ionConc = double.Parse(bracketed.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                    ionConcKnown = true;
                }
            }

            if (!ionConcKnown)
                ionConcKnown = cmd.TryGetDouble(out ionConc, "concentration", "c");

            // Число электронов: "n=2" или запись полуреакции "2e-"
            int n = cmd.GetIntOrDefault(0, "n", "z", "electrons");

            if (n == 0)
            {
                var electronsMatch = Regex.Match(cmd.OriginalCommand, @"(\d+)\s*e-");
                n = electronsMatch.Success
                    ? int.Parse(electronsMatch.Groups[1].Value, CultureInfo.InvariantCulture)
                    : ChargeOf(metalIon) is int charge and > 0 ? charge : 1;
            }

            // Стандартный потенциал: параметр либо справочник пары "Cu2+/Cu"
            string couple = null;
            double E0;

            if (cmd.TryGetDouble(out double e0Param, "E0", "E°", "standard_potential"))
            {
                E0 = e0Param;
            }
            else
            {
                couple = BuildCouple(metalIon);
                double? tabulated = couple == null ? null : _database.GetStandardPotential(couple);

                if (tabulated == null)
                    return ChemResult.Error(couple == null
                        ? "Standard potential E0 is required (or specify the ion, e.g. ion=Cu2+)"
                        : $"Standard potential for '{couple}' is not in the database, specify E0=");

                E0 = tabulated.Value;
            }

            // Восстановление металла M^n+ + ne- -> M, поэтому Q = 1/[M^n+]
            double Q = cmd.TryGetDouble(out double qParam, "Q")
                ? qParam
                : ionConcKnown && ionConc > 0 ? 1.0 / ionConc : 1.0;

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

                if (couple != null)
                    result.Steps.Add($"E° taken from the database for the couple {couple}");

                if (metalIon != null && ionConcKnown)
                    result.Steps.Add($"[{metalIon}] = {ionConc:G} M => Q = 1/[{metalIon}] = {Q:G4}");
                else
                    result.Steps.Add("Ion concentration is not specified, standard conditions assumed (Q = 1)");
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
            
            double I = cmd.GetDouble("I", "current");
            double t = cmd.GetDouble("time", "t", "duration");

            string substance = cmd.GetString("substance", "element", "metal", "ion");
            var element = _database.GetElement(ElementSymbolOf(substance) ?? substance);

            if (element == null)
                return ChemResult.Error($"Element '{substance}' not found");

            double M = element.AtomicMass;

            // Определяем валентность n
            int n = cmd.GetIntOrDefault(0, "n", "z", "electrons");

            if (n > 0)
            {
                // задано явно
            }
            else if (ChargeOf(substance) is int ionCharge and > 0)
            {
                n = ionCharge;
            }
            else if (element.OxidationStates.Length > 0)
            {
                // Берем наиболее вероятную положительную степень окисления
                n = element.OxidationStates.Where(s => s > 0).DefaultIfEmpty(1).Max();
            }

            if (n <= 0)
                n = 1;

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

    // Заряд иона из его записи: "Cu2+" -> 2, "Cl-" -> -1, "Cu" -> null
    private static int? ChargeOf(string species)
    {
        if (string.IsNullOrWhiteSpace(species))
            return null;

        return MolecularFormula.TryParse(species, out var formula, out _) && formula.Charge != 0
            ? formula.Charge
            : null;
    }

    // Символ элемента из записи иона: "Cu2+" -> "Cu"
    private static string ElementSymbolOf(string species)
    {
        if (!MolecularFormula.TryParse(species, out var formula, out _) || formula.Elements.Count != 1)
            return null;

        return formula.Elements.Keys.First();
    }

    // Пара для справочника потенциалов: "Cu2+" -> "Cu2+/Cu"
    private static string BuildCouple(string ion)
    {
        if (string.IsNullOrWhiteSpace(ion))
            return null;

        if (ion.Contains('/'))
            return ion;

        string symbol = ElementSymbolOf(ion);

        return symbol == null ? null : $"{ion}/{symbol}";
    }
}

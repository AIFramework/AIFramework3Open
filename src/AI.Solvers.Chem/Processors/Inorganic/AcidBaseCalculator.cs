using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using System.Globalization;

namespace FractalAgentsAI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// pH И КИСЛОТНО-ОСНОВНЫЕ РАСЧЕТЫ
// ═══════════════════════════════════════════════════════════
public class AcidBaseCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    // Известные сильные кислоты и основания
    private readonly HashSet<string> _strongAcids = new()
        { "HCl", "HBr", "HI", "HNO3", "H2SO4", "HClO4" };
    private readonly HashSet<string> _strongBases = new()
        { "NaOH", "KOH", "LiOH", "Ca(OH)2", "Ba(OH)2" };

    public AcidBaseCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult CalculatePH(ParsedCommand cmd)
    {
        try
        {
            var concentration = double.Parse(cmd.Parameters["concentration"], CultureInfo.InvariantCulture);
            var substance = cmd.Parameters["substance"];

            double pH;
            string explanation;

            if (_strongAcids.Contains(substance))
            {
                // Сильная кислота: pH = -log[H+]
                var H_concentration = concentration;

                // Для H2SO4 удваиваем концентрацию H+
                if (substance == "H2SO4")
                    H_concentration *= 2;

                pH = -Math.Log10(H_concentration);
                explanation = "Strong acid - complete dissociation";
            }
            else if (_strongBases.Contains(substance))
            {
                // Сильное основание: pOH = -log[OH-], pH = 14 - pOH
                var OH_concentration = concentration;

                // Для Ca(OH)2 и Ba(OH)2 удваиваем
                if (substance == "Ca(OH)2" || substance == "Ba(OH)2")
                    OH_concentration *= 2;

                var pOH = -Math.Log10(OH_concentration);
                pH = 14 - pOH;
                explanation = "Strong base - complete dissociation";
            }
            else if (cmd.Parameters.ContainsKey("Ka"))
            {
                // Слабая кислота
                var Ka = double.Parse(cmd.Parameters["Ka"], CultureInfo.InvariantCulture);

                // [H+] = sqrt(Ka * C)
                var H_concentration = Math.Sqrt(Ka * concentration);
                pH = -Math.Log10(H_concentration);
                explanation = $"Weak acid with Ka = {Ka:E2}";
            }
            else
            {
                // Попытка найти в базе данных
                var compound = _database.LookupCompound(substance);
                if (compound?.Properties?.PKa != null)
                {
                    var pKa = compound.Properties.PKa.Value;
                    var Ka = Math.Pow(10, -pKa);
                    var H_concentration = Math.Sqrt(Ka * concentration);
                    pH = -Math.Log10(H_concentration);
                    explanation = $"Weak acid with pKa = {pKa:F2}";
                }
                else
                {
                    return ChemResult.Error($"Cannot determine acid/base properties of {substance}");
                }
            }

            var result = ChemResult.Ok($"pH = {pH:F2}");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Substance: {substance}");
                result.Steps.Add($"Concentration: {concentration:E2} M");
                result.Steps.Add($"Type: {explanation}");
                result.Steps.Add($"Calculated pH: {pH:F2}");

                if (pH < 7)
                    result.Steps.Add("Solution is acidic");
                else if (pH > 7)
                    result.Steps.Add("Solution is basic");
                else
                    result.Steps.Add("Solution is neutral");
            }

            result.Data["pH"] = pH;
            result.Data["concentration"] = concentration;

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"pH calculation failed: {ex.Message}");
        }
    }

    public ChemResult CalculateBufferPH(ParsedCommand cmd)
    {
        try
        {
            // Henderson-Hasselbalch: pH = pKa + log([A-]/[HA])
            double pKa, acidConc, baseConc, pH;
            
            if (cmd.Parameters.ContainsKey("pKa"))
            {
                pKa = double.Parse(cmd.Parameters["pKa"], CultureInfo.InvariantCulture);
            }
            else if (cmd.Parameters.ContainsKey("Ka"))
            {
                var Ka = double.Parse(cmd.Parameters["Ka"], CultureInfo.InvariantCulture);
                pKa = -Math.Log10(Ka);
            }
            else
            {
                return ChemResult.Error("pKa or Ka required for buffer calculation");
            }

            acidConc = double.Parse(cmd.Parameters["acid"], CultureInfo.InvariantCulture);
            baseConc = double.Parse(cmd.Parameters["base"], CultureInfo.InvariantCulture);

            if (acidConc <= 0 || baseConc <= 0)
                return ChemResult.Error("Concentrations must be positive");

            // Henderson-Hasselbalch equation
            pH = pKa + Math.Log10(baseConc / acidConc);

            // Расчёт буферной ёмкости
            double totalConc = acidConc + baseConc;
            double fraction = acidConc / totalConc;
            double bufferCapacity = 2.3 * totalConc * fraction * (1 - fraction);

            var result = ChemResult.Ok($"pH = {pH:F2}");
            result.Data["pH"] = pH;
            result.Data["pKa"] = pKa;
            result.Data["bufferCapacity"] = bufferCapacity;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Henderson-Hasselbalch Equation: pH = pKa + log([A⁻]/[HA])");
                result.Steps.Add($"pKa = {pKa:F2}");
                result.Steps.Add($"[HA] (acid) = {acidConc:E2} M");
                result.Steps.Add($"[A⁻] (conjugate base) = {baseConc:E2} M");
                result.Steps.Add($"log([A⁻]/[HA]) = log({baseConc}/{acidConc}) = {Math.Log10(baseConc / acidConc):F3}");
                result.Steps.Add($"pH = {pKa:F2} + {Math.Log10(baseConc / acidConc):F3} = {pH:F2}");
                result.Steps.Add($"\nBuffer capacity (β) = {bufferCapacity:F3} mol/(L·pH unit)");
                
                // Оценка эффективности буфера
                double ratio = baseConc / acidConc;
                if (ratio >= 0.1 && ratio <= 10)
                    result.Steps.Add("✓ Buffer is effective (ratio within 0.1-10)");
                else
                    result.Steps.Add("⚠ Buffer may be less effective (ratio outside 0.1-10)");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Buffer pH calculation failed: {ex.Message}");
        }
    }

    public ChemResult Titration(ParsedCommand cmd)
    {
        try
        {
            var titrationType = cmd.Parameters.GetValueOrDefault("type", "strong-strong");
            
            return titrationType switch
            {
                "strong-strong" => TitrateStrongAcidStrongBase(cmd),
                "weak-strong" => TitrateWeakAcidStrongBase(cmd),
                "polyprotic" => TitratePolyprotic(cmd),
                _ => ChemResult.Error($"Unknown titration type: {titrationType}")
            };
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Titration calculation failed: {ex.Message}");
        }
    }

    private ChemResult TitrateStrongAcidStrongBase(ParsedCommand cmd)
    {
        double Va = double.Parse(cmd.Parameters["Va"], CultureInfo.InvariantCulture); // объём кислоты, мл
        double Ca = double.Parse(cmd.Parameters["Ca"], CultureInfo.InvariantCulture); // концентрация кислоты, М
        double Cb = double.Parse(cmd.Parameters["Cb"], CultureInfo.InvariantCulture); // концентрация основания, М
        double Vb = double.Parse(cmd.Parameters["Vb"], CultureInfo.InvariantCulture); // добавленный объём основания, мл

        double totalVolume = Va + Vb;
        double molesAcid = Ca * Va / 1000; // моли кислоты
        double molesBase = Cb * Vb / 1000; // моли основания

        double pH;
        string pointType;

        if (Math.Abs(molesAcid - molesBase) < 1e-10)
        {
            // Точка эквивалентности
            pH = 7.0;
            pointType = "Equivalence Point";
        }
        else if (molesBase < molesAcid)
        {
            // Избыток кислоты
            double excessAcid = molesAcid - molesBase;
            double H_concentration = excessAcid / (totalVolume / 1000);
            pH = -Math.Log10(H_concentration);
            pointType = "Before Equivalence Point (excess acid)";
        }
        else
        {
            // Избыток основания
            double excessBase = molesBase - molesAcid;
            double OH_concentration = excessBase / (totalVolume / 1000);
            double pOH = -Math.Log10(OH_concentration);
            pH = 14 - pOH;
            pointType = "After Equivalence Point (excess base)";
        }

        var result = ChemResult.Ok($"pH = {pH:F2}");
        result.Data["pH"] = pH;
        result.Data["Vb_equivalence"] = (Ca * Va) / Cb; // объём в точке эквивалентности

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Strong Acid - Strong Base Titration");
            result.Steps.Add($"Initial: {Ca:F3} M acid, {Va:F1} mL");
            result.Steps.Add($"Added: {Cb:F3} M base, {Vb:F1} mL");
            result.Steps.Add($"Moles acid: {molesAcid * 1000:F3} mmol");
            result.Steps.Add($"Moles base: {molesBase * 1000:F3} mmol");
            result.Steps.Add($"Point: {pointType}");
            result.Steps.Add($"pH = {pH:F2}");
            result.Steps.Add($"\nVolume at equivalence point: {result.Data["Vb_equivalence"]:F2} mL");
        }

        return result;
    }

    private ChemResult TitrateWeakAcidStrongBase(ParsedCommand cmd)
    {
        double Va = double.Parse(cmd.Parameters["Va"], CultureInfo.InvariantCulture);
        double Ca = double.Parse(cmd.Parameters["Ca"], CultureInfo.InvariantCulture);
        double Cb = double.Parse(cmd.Parameters["Cb"], CultureInfo.InvariantCulture);
        double Vb = double.Parse(cmd.Parameters["Vb"], CultureInfo.InvariantCulture);
        double pKa = double.Parse(cmd.Parameters["pKa"], CultureInfo.InvariantCulture);
        double Ka = Math.Pow(10, -pKa);

        double totalVolume = Va + Vb;
        double molesAcid = Ca * Va / 1000;
        double molesBase = Cb * Vb / 1000;

        double pH;
        string pointType;

        if (Math.Abs(molesAcid - molesBase) < 1e-10)
        {
            // Точка эквивалентности - гидролиз соли
            double saltConc = molesAcid / (totalVolume / 1000);
            double Kb = 1e-14 / Ka;
            double OH_concentration = Math.Sqrt(Kb * saltConc);
            double pOH = -Math.Log10(OH_concentration);
            pH = 14 - pOH;
            pointType = "Equivalence Point (salt hydrolysis)";
        }
        else if (molesBase < molesAcid)
        {
            // Буферная область: pH = pKa + log([A-]/[HA])
            double molesRemaining = molesAcid - molesBase;
            pH = pKa + Math.Log10(molesBase / molesRemaining);
            pointType = "Buffer Region";
        }
        else
        {
            // Избыток основания
            double excessBase = molesBase - molesAcid;
            double OH_concentration = excessBase / (totalVolume / 1000);
            double pOH = -Math.Log10(OH_concentration);
            pH = 14 - pOH;
            pointType = "After Equivalence Point";
        }

        var result = ChemResult.Ok($"pH = {pH:F2}");
        result.Data["pH"] = pH;
        result.Data["pKa"] = pKa;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Weak Acid - Strong Base Titration");
            result.Steps.Add($"Weak acid: pKa = {pKa:F2}");
            result.Steps.Add($"Point: {pointType}");
            result.Steps.Add($"pH = {pH:F2}");
        }

        return result;
    }

    private ChemResult TitratePolyprotic(ParsedCommand cmd)
    {
        return ChemResult.Error("Polyprotic acid titration not yet implemented");
    }
}
using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using System.Text;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// pH И КИСЛОТНО-ОСНОВНЫЕ РАСЧЕТЫ
// ═══════════════════════════════════════════════════════════
public class AcidBaseCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    // Алиасы параметров титрования: документированный синтаксис ("acid=0.1M V_acid=25ml")
    // и внутренние имена (Ca, Va) описывают одно и то же
    private static readonly string[] ConcAcidNames = { "Ca", "acid", "acid_concentration", "C_acid" };
    private static readonly string[] ConcBaseNames = { "Cb", "base", "base_concentration", "C_base" };
    private static readonly string[] VolumeAcidNames = { "Va", "V_acid", "volume_acid" };
    private static readonly string[] VolumeBaseNames = { "Vb", "V_base", "volume_base", "added" };

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
            var concentration = cmd.GetDouble("concentration", "c", "C");
            var substance = cmd.GetString("substance", "compound", "acid", "base");

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
            else if (cmd.Has("Ka", "pKa"))
            {
                // Слабая кислота
                var Ka = cmd.Has("Ka")
                    ? cmd.GetDouble("Ka")
                    : Math.Pow(10, -cmd.GetDouble("pKa"));

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

            if (cmd.Has("pKa"))
            {
                pKa = cmd.GetDouble("pKa");
            }
            else if (cmd.Has("Ka"))
            {
                pKa = -Math.Log10(cmd.GetDouble("Ka"));
            }
            else
            {
                return ChemResult.Error("pKa or Ka required for buffer calculation");
            }

            acidConc = cmd.GetDouble("acid", "HA", "[HA]", "acid_concentration");
            baseConc = cmd.GetDouble("base", "A", "[A-]", "base_concentration");

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
                    result.Steps.Add("Buffer is effective (ratio within 0.1-10)");
                else
                    result.Steps.Add("Buffer may be less effective (ratio outside 0.1-10)");
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
            // Тип титрования выводится из данных: задана pKa - слабая кислота, иначе сильная
            var titrationType = cmd.GetStringOrDefault(
                cmd.Has("pKa", "Ka") ? "weak-strong" : "strong-strong",
                "type", "titration_type");

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
        double Va = cmd.GetDouble(VolumeAcidNames);   // объём кислоты, мл
        double Ca = cmd.GetDouble(ConcAcidNames);     // концентрация кислоты, М
        double Cb = cmd.GetDouble(ConcBaseNames);     // концентрация основания, М
        double Veq = Ca * Va / Cb;                    // объём щёлочи в точке эквивалентности, мл

        // Объём титранта не задан - строится кривая титрования (как обещает справка)
        if (!cmd.TryGetDouble(out double Vb, VolumeBaseNames))
            return BuildTitrationCurve("Strong Acid - Strong Base Titration", Veq,
                v => StrongStrongPoint(Ca, Va, Cb, v));

        var (pH, pointType) = StrongStrongPoint(Ca, Va, Cb, Vb);

        var result = ChemResult.Ok($"pH = {pH:F2}");
        result.Data["pH"] = pH;
        result.Data["Vb_equivalence"] = Veq;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Strong Acid - Strong Base Titration");
            result.Steps.Add($"Initial: {Ca:F3} M acid, {Va:F1} mL");
            result.Steps.Add($"Added: {Cb:F3} M base, {Vb:F1} mL");
            result.Steps.Add($"Moles acid: {Ca * Va:F3} mmol");
            result.Steps.Add($"Moles base: {Cb * Vb:F3} mmol");
            result.Steps.Add($"Point: {pointType}");
            result.Steps.Add($"pH = {pH:F2}");
            result.Steps.Add($"\nVolume at equivalence point: {Veq:F2} mL");
        }

        return result;
    }

    private ChemResult TitrateWeakAcidStrongBase(ParsedCommand cmd)
    {
        double Va = cmd.GetDouble(VolumeAcidNames);
        double Ca = cmd.GetDouble(ConcAcidNames);
        double Cb = cmd.GetDouble(ConcBaseNames);
        double pKa = cmd.Has("pKa") ? cmd.GetDouble("pKa") : -Math.Log10(cmd.GetDouble("Ka"));
        double Veq = Ca * Va / Cb;

        if (!cmd.TryGetDouble(out double Vb, VolumeBaseNames))
            return BuildTitrationCurve($"Weak Acid (pKa = {pKa:F2}) - Strong Base Titration", Veq,
                v => WeakStrongPoint(Ca, Va, Cb, v, pKa));

        var (pH, pointType) = WeakStrongPoint(Ca, Va, Cb, Vb, pKa);

        var result = ChemResult.Ok($"pH = {pH:F2}");
        result.Data["pH"] = pH;
        result.Data["pKa"] = pKa;
        result.Data["Vb_equivalence"] = Veq;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Weak Acid - Strong Base Titration");
            result.Steps.Add($"Weak acid: pKa = {pKa:F2}");
            result.Steps.Add($"Added: {Cb:F3} M base, {Vb:F1} mL");
            result.Steps.Add($"Point: {pointType}");
            result.Steps.Add($"pH = {pH:F2}");
            result.Steps.Add($"\nVolume at equivalence point: {Veq:F2} mL");
        }

        return result;
    }

    // Точка кривой титрования сильной кислоты сильным основанием
    private static (double pH, string Point) StrongStrongPoint(double Ca, double Va, double Cb, double Vb)
    {
        double totalVolume = (Va + Vb) / 1000.0;      // л
        double molesAcid = Ca * Va / 1000.0;
        double molesBase = Cb * Vb / 1000.0;

        if (Math.Abs(molesAcid - molesBase) < 1e-12)
            return (7.0, "Equivalence Point");

        if (molesBase < molesAcid)
            return (-Math.Log10((molesAcid - molesBase) / totalVolume), "Before Equivalence Point (excess acid)");

        double pOH = -Math.Log10((molesBase - molesAcid) / totalVolume);
        return (14 - pOH, "After Equivalence Point (excess base)");
    }

    // Точка кривой титрования слабой кислоты сильным основанием
    private static (double pH, string Point) WeakStrongPoint(double Ca, double Va, double Cb, double Vb, double pKa)
    {
        double Ka = Math.Pow(10, -pKa);
        double totalVolume = (Va + Vb) / 1000.0;
        double molesAcid = Ca * Va / 1000.0;
        double molesBase = Cb * Vb / 1000.0;

        // Титрант ещё не добавлен: диссоциация слабой кислоты, [H+] = sqrt(Ka·C)
        if (molesBase <= 1e-12)
            return (0.5 * (pKa - Math.Log10(Ca)), "Initial Point (weak acid)");

        if (Math.Abs(molesAcid - molesBase) < 1e-12)
        {
            // Точка эквивалентности - гидролиз соли
            double saltConc = molesAcid / totalVolume;
            double Kb = 1e-14 / Ka;
            double pOHeq = -Math.Log10(Math.Sqrt(Kb * saltConc));
            return (14 - pOHeq, "Equivalence Point (salt hydrolysis)");
        }

        if (molesBase < molesAcid)
            return (pKa + Math.Log10(molesBase / (molesAcid - molesBase)), "Buffer Region");

        double pOH = -Math.Log10((molesBase - molesAcid) / totalVolume);
        return (14 - pOH, "After Equivalence Point");
    }

    // Кривая титрования: характерные точки относительно объёма эквивалентности
    private ChemResult BuildTitrationCurve(string title, double Veq, Func<double, (double pH, string Point)> point)
    {
        double[] fractions = { 0, 0.5, 0.9, 0.99, 1.0, 1.01, 1.1, 1.5, 2.0 };

        var text = new StringBuilder();
        text.AppendLine(title);
        text.AppendLine($"Equivalence point: V(base) = {Veq:F2} mL");
        text.AppendLine();
        text.AppendLine("  V(base), mL |   pH  | point");

        var curve = new List<(double Volume, double PH)>();

        foreach (double fraction in fractions)
        {
            double volume = Veq * fraction;
            var (pH, pointType) = point(volume);
            curve.Add((volume, pH));
            text.AppendLine($"  {volume,10:F2}  | {pH,5:F2} | {pointType}");
        }

        var result = ChemResult.Ok(text.ToString());
        result.Data["Vb_equivalence"] = Veq;
        result.Data["curve"] = curve;
        result.Steps.Add("Specify V_base= to get the pH at a particular point of the curve");

        return result;
    }

    private ChemResult TitratePolyprotic(ParsedCommand cmd)
    {
        return ChemResult.Error("Polyprotic acid titration not yet implemented");
    }
}

using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using System.Globalization;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// КОМПЛЕКСНЫЕ СОЕДИНЕНИЯ
// ═══════════════════════════════════════════════════════════
public class ComplexCompoundsCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    // Константы устойчивости комплексов (lgK)
    private readonly Dictionary<string, Dictionary<string, double>> _stabilityConstants = new()
    {
        ["Ag+"] = new Dictionary<string, double>
        {
            ["NH3"] = 7.24,  // [Ag(NH3)2]+, суммарная lgβ2
            ["Cl-"] = 5.25,  // [AgCl2]-
            ["CN-"] = 20.5,  // [Ag(CN)2]-
            ["S2O3^2-"] = 13.5, // [Ag(S2O3)2]3-
        },
        ["Cu2+"] = new Dictionary<string, double>
        {
            ["NH3"] = 13.3,  // [Cu(NH3)4]2+
            ["EDTA"] = 18.8,
            ["en"] = 20.0,   // этилендиамин
        },
        ["Fe3+"] = new Dictionary<string, double>
        {
            ["SCN-"] = 2.3,  // [Fe(SCN)]2+
            ["EDTA"] = 25.1,
        },
        ["Ni2+"] = new Dictionary<string, double>
        {
            ["NH3"] = 8.6,   // [Ni(NH3)6]2+
            ["EDTA"] = 18.6,
            ["CN-"] = 30.2,  // [Ni(CN)4]2-
        },
        ["Zn2+"] = new Dictionary<string, double>
        {
            ["NH3"] = 9.5,   // [Zn(NH3)4]2+
            ["EDTA"] = 16.5,
            ["OH-"] = 15.5,  // [Zn(OH)4]2-
        },
        ["Ca2+"] = new Dictionary<string, double>
        {
            ["EDTA"] = 10.7,
        },
        ["Mg2+"] = new Dictionary<string, double>
        {
            ["EDTA"] = 8.7,
        },
    };

    /// <summary>
    /// Приводит имя металла к ключу таблицы констант: "Cu" -> "Cu2+"
    /// </summary>
    private string NormalizeMetal(string metal)
    {
        if (metal == null || _stabilityConstants.ContainsKey(metal))
            return metal;

        foreach (string key in _stabilityConstants.Keys)
        {
            // "Cu2+" -> "Cu"
            string symbol = new string(key.TakeWhile(char.IsLetter).ToArray());

            if (string.Equals(symbol, metal, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return metal;
    }

    /// <summary>
    /// Приводит имя лиганда к ключу таблицы констант: "Cl" -> "Cl-"
    /// </summary>
    private string NormalizeLigand(string metal, string ligand)
    {
        if (ligand == null || !_stabilityConstants.TryGetValue(metal, out var ligands) || ligands.ContainsKey(ligand))
            return ligand;

        foreach (string key in ligands.Keys)
        {
            string core = key.TrimEnd('+', '-', '^', '0', '1', '2', '3', '4', '5', '6');

            if (string.Equals(core, ligand, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return ligand;
    }

    // Концентрация: "[metal]=0.1M" / "metal_concentration=0.1" / "[Cu2+]=0.1M"
    private static bool TryGetSpeciesConcentration(ParsedCommand cmd, string role, string species, out double value)
        => cmd.TryGetConcentration(out value, role)
        || (species != null && cmd.TryGetConcentration(out value, species))
        || cmd.TryGetDouble(out value, $"{role}_total", $"{role}_conc");

    private static double GetSpeciesConcentration(ParsedCommand cmd, string role, string species)
        => TryGetSpeciesConcentration(cmd, role, species, out double value)
            ? value
            : throw new MissingParameterException(new[] { $"[{role}]", $"[{species}]", $"{role}_concentration" });

    public ComplexCompoundsCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    // Расчёт концентрации комплекса
    public ChemResult CalculateComplexConcentration(ParsedCommand cmd)
    {
        try
        {
            var metal = NormalizeMetal(cmd.GetString("metal", "M"));
            var ligand = NormalizeLigand(metal, cmd.GetString("ligand", "L"));
            double metalConc = GetSpeciesConcentration(cmd, "metal", metal);
            double ligandConc = GetSpeciesConcentration(cmd, "ligand", ligand);

            if (!_stabilityConstants.ContainsKey(metal) || !_stabilityConstants[metal].ContainsKey(ligand))
                return ChemResult.Error($"Stability constant not available for {metal}-{ligand}");

            double lgBeta = _stabilityConstants[metal][ligand];
            double beta = Math.Pow(10, lgBeta);

            // Для простоты: предполагаем избыток лиганда и полное связывание
            // [ML] = β·[M]·[L]^n, где n - координационное число
            
            // Упрощённый расчёт для 1:1 комплекса
            double complexConc = (beta * metalConc * ligandConc) / (1 + beta * ligandConc);

            double percentComplexed = (complexConc / metalConc) * 100;

            var result = ChemResult.Ok($"[Complex] = {complexConc:E3} M ({percentComplexed:F1}% complexed)");
            result.Data["complex_concentration"] = complexConc;
            result.Data["beta"] = beta;
            result.Data["lgBeta"] = lgBeta;
            result.Data["percent_complexed"] = percentComplexed;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Complex Formation: {metal} + {ligand}");
                result.Steps.Add($"Stability constant: lgβ = {lgBeta:F2}");
                result.Steps.Add($"β = 10^{lgBeta} = {beta:E3}");
                result.Steps.Add($"\nInitial concentrations:");
                result.Steps.Add($"[{metal}] = {metalConc:E3} M");
                result.Steps.Add($"[{ligand}] = {ligandConc:E3} M");
                result.Steps.Add($"\nEquilibrium:");
                result.Steps.Add($"[Complex] = {complexConc:E3} M");
                result.Steps.Add($"Complexation: {percentComplexed:F1}%");

                if (percentComplexed > 99)
                    result.Steps.Add("Nearly complete complexation");
                else if (percentComplexed > 90)
                    result.Steps.Add("Good complexation");
                else if (percentComplexed > 50)
                    result.Steps.Add("Partial complexation");
                else
                    result.Steps.Add("Weak complexation - consider increasing ligand concentration");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Complex concentration calculation failed: {ex.Message}");
        }
    }

    // Ступенчатое комплексообразование
    public ChemResult StepwiseComplexation(ParsedCommand cmd)
    {
        try
        {
            var metal = NormalizeMetal(cmd.GetString("metal", "M"));
            var ligand = NormalizeLigand(metal, cmd.GetString("ligand", "L"));
            double ligandConc = GetSpeciesConcentration(cmd, "ligand", ligand);

            // Общая концентрация металла нужна только для пересчёта долей в концентрации
            bool metalConcKnown = TryGetSpeciesConcentration(cmd, "metal", metal, out double metalConc);

            // Для Cu2+ + NH3 (пример):
            // K1 = 1.9e4, K2 = 3.9e3, K3 = 1.0e3, K4 = 1.5e2
            // β1 = K1, β2 = K1·K2, β3 = K1·K2·K3, β4 = K1·K2·K3·K4

            if (metal == "Cu2+" && ligand == "NH3")
            {
                double[] K = { 1.9e4, 3.9e3, 1.0e3, 1.5e2 };
                double[] beta = new double[5];
                beta[0] = 1; // β0 = 1
                beta[1] = K[0];
                beta[2] = K[0] * K[1];
                beta[3] = K[0] * K[1] * K[2];
                beta[4] = K[0] * K[1] * K[2] * K[3];

                // Расчёт мольных долей: α_n = β_n·[L]^n / Σ(β_i·[L]^i)
                double denominator = 0;
                for (int i = 0; i <= 4; i++)
                    denominator += beta[i] * Math.Pow(ligandConc, i);

                double[] alpha = new double[5];
                for (int i = 0; i <= 4; i++)
                    alpha[i] = (beta[i] * Math.Pow(ligandConc, i)) / denominator;

                var result = ChemResult.Ok($"Dominant species: [Cu(NH3)_{GetDominantIndex(alpha)}]²⁺");
                
                for (int i = 0; i <= 4; i++)
                {
                    result.Data[$"alpha_{i}"] = alpha[i];

                    if (metalConcKnown)
                        result.Data[$"concentration_ML{i}"] = alpha[i] * metalConc;
                }

                if (!metalConcKnown)
                    result.Steps.Add("Total metal concentration is not specified: only mole fractions are reported");

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add("Stepwise Complexation: Cu²⁺ + NH₃");
                    result.Steps.Add($"\nStepwise constants:");
                    for (int i = 0; i < K.Length; i++)
                        result.Steps.Add($"K{i + 1} = {K[i]:E2}");

                    result.Steps.Add($"\nOverall stability constants (β):");
                    for (int i = 1; i <= 4; i++)
                        result.Steps.Add($"β{i} = {beta[i]:E2}");

                    result.Steps.Add($"\n[NH₃] = {ligandConc:E3} M");
                    result.Steps.Add($"\nSpecies distribution:");
                    result.Steps.Add($"Cu²⁺:          α₀ = {alpha[0]:F4} ({alpha[0] * 100:F2}%)");
                    result.Steps.Add($"[Cu(NH₃)]²⁺:   α₁ = {alpha[1]:F4} ({alpha[1] * 100:F2}%)");
                    result.Steps.Add($"[Cu(NH₃)₂]²⁺:  α₂ = {alpha[2]:F4} ({alpha[2] * 100:F2}%)");
                    result.Steps.Add($"[Cu(NH₃)₃]²⁺:  α₃ = {alpha[3]:F4} ({alpha[3] * 100:F2}%)");
                    result.Steps.Add($"[Cu(NH₃)₄]²⁺:  α₄ = {alpha[4]:F4} ({alpha[4] * 100:F2}%)");
                }

                return result;
            }

            return ChemResult.Error("Stepwise complexation data not available for this system");
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Stepwise complexation calculation failed: {ex.Message}");
        }
    }

    // Влияние pH на комплексообразование
    public ChemResult ComplexationAtPH(ParsedCommand cmd)
    {
        try
        {
            var metal = NormalizeMetal(cmd.GetString("metal", "M"));
            var ligand = NormalizeLigand(metal, cmd.GetString("ligand", "L"));
            double pH = cmd.GetDouble("pH");
            double metalConc = GetSpeciesConcentration(cmd, "metal", metal);
            double ligandTotalConc = GetSpeciesConcentration(cmd, "ligand", ligand);

            // Для EDTA (пример)
            if (ligand == "EDTA")
            {
                // α_Y4- зависит от pH (доля Y4- формы EDTA)
                double alphaY4 = CalculateAlphaEDTA(pH);

                if (!_stabilityConstants.ContainsKey(metal) || !_stabilityConstants[metal].ContainsKey(ligand))
                    return ChemResult.Error($"Stability constant not available for {metal}-EDTA");

                double lgK = _stabilityConstants[metal][ligand];
                double K = Math.Pow(10, lgK);

                // Условная константа устойчивости: K' = K·α_Y4-
                double K_prime = K * alphaY4;
                double lgK_prime = Math.Log10(K_prime);

                var result = ChemResult.Ok($"K' = {K_prime:E3} (lgK' = {lgK_prime:F2}) at pH {pH}");
                result.Data["K_prime"] = K_prime;
                result.Data["lgK_prime"] = lgK_prime;
                result.Data["alpha_Y4"] = alphaY4;

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add($"EDTA Complexation at pH {pH}");
                    result.Steps.Add($"Metal: {metal}");
                    result.Steps.Add($"\nThermodynamic constant:");
                    result.Steps.Add($"lgK = {lgK:F2}, K = {K:E3}");
                    result.Steps.Add($"\nEDTA protonation (pH effect):");
                    result.Steps.Add($"α(Y⁴⁻) at pH {pH} = {alphaY4:E3}");
                    result.Steps.Add($"\nConditional stability constant:");
                    result.Steps.Add($"K' = K·α(Y⁴⁻) = {K:E3}·{alphaY4:E3}");
                    result.Steps.Add($"lgK' = {lgK_prime:F2}");

                    if (lgK_prime > 8)
                        result.Steps.Add("\nStrong complexation possible at this pH");
                    else if (lgK_prime > 6)
                        result.Steps.Add("\nModerate complexation at this pH");
                    else
                        result.Steps.Add("\nWeak complexation at this pH - consider increasing pH");
                }

                return result;
            }

            return ChemResult.Error("pH-dependent complexation calculation only implemented for EDTA");
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"pH-dependent complexation failed: {ex.Message}");
        }
    }

    // Хелатный эффект
    public ChemResult ChelateEffect(ParsedCommand cmd)
    {
        try
        {
            // Сравнение монодентатного и бидентатного лиганда
            // Пример: Cu2+ с NH3 vs этилендиамин (en)
            
            var result = ChemResult.Ok("Chelate effect demonstration");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Chelate Effect: Cu²⁺ complexes");
                result.Steps.Add("\nMonodentate ligand (NH₃):");
                result.Steps.Add("Cu²⁺ + 4NH₃ ⇌ [Cu(NH₃)₄]²⁺");
                result.Steps.Add("lgβ₄ = 13.3");
                
                result.Steps.Add("\nBidentate ligand (ethylenediamine, en):");
                result.Steps.Add("Cu²⁺ + 2en ⇌ [Cu(en)₂]²⁺");
                result.Steps.Add("lgβ₂ = 20.0");
                
                result.Steps.Add("\nChelate effect:");
                result.Steps.Add("Δlgβ = 20.0 - 13.3 = 6.7");
                result.Steps.Add("The chelate complex is ~10⁷ times more stable!");
                
                result.Steps.Add("\nReason:");
                result.Steps.Add("- Entropy favors chelate (fewer particles released)");
                result.Steps.Add("- Ring formation provides additional stability");
                result.Steps.Add("- 5- or 6-membered rings are most stable");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Chelate effect demonstration failed: {ex.Message}");
        }
    }

    // Вспомогательные методы
    private double CalculateAlphaEDTA(double pH)
    {
        // Упрощённая аппроксимация для α_Y4- (доля Y4-)
        double[] pKa = { 2.0, 2.67, 6.16, 10.26 };
        
        double H = Math.Pow(10, -pH);
        double denominator = 1;
        for (int i = 0; i < pKa.Length; i++)
        {
            double term = 1;
            for (int j = i; j < pKa.Length; j++)
                term *= Math.Pow(10, -pKa[j]) * H;
            denominator += term;
        }

        // Для практических целей вернем табличные значения, если расчет сложен
        if (pH < 2) return 3.7e-14; // Исправлено для точности
        if (pH < 3) return 2.5e-11;
        if (pH < 4) return 3.6e-9;
        if (pH < 5) return 3.5e-7;
        if (pH < 6) return 2.2e-5;
        if (pH < 7) return 4.8e-4;
        if (pH < 8) return 5.4e-3;
        if (pH < 9) return 5.2e-2;
        if (pH < 10) return 0.35;
        if (pH < 11) return 0.85;
        if (pH < 12) return 0.98;
        return 1.0;
    }

    private int GetDominantIndex(double[] alpha)
    {
        int maxIndex = 0;
        double maxValue = alpha[0];
        for (int i = 1; i < alpha.Length; i++)
        {
            if (alpha[i] > maxValue)
            {
                maxValue = alpha[i];
                maxIndex = i;
            }
        }
        return maxIndex;
    }
}

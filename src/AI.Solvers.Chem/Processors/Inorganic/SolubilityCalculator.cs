using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using System.Globalization;

namespace FractalAgentsAI.Solvers.Chem.Processors.Inorganic;

// ═══════════════════════════════════════════════════════════
// РАСТВОРИМОСТЬ И ПРОИЗВЕДЕНИЕ РАСТВОРИМОСТИ (Ksp)
// ═══════════════════════════════════════════════════════════
public class SolubilityCalculator
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    // Базовые данные Ksp для распространённых соединений
    private readonly Dictionary<string, (double Ksp, string Equation)> _kspData = new()
    {
        // Хлориды
        ["AgCl"] = (1.77e-10, "Ag⁺ + Cl⁻"),
        ["PbCl2"] = (1.70e-5, "Pb²⁺ + 2Cl⁻"),
        ["Hg2Cl2"] = (1.43e-18, "Hg₂²⁺ + 2Cl⁻"),
        
        // Бромиды
        ["AgBr"] = (5.35e-13, "Ag⁺ + Br⁻"),
        ["PbBr2"] = (6.60e-6, "Pb²⁺ + 2Br⁻"),
        
        // Йодиды
        ["AgI"] = (8.52e-17, "Ag⁺ + I⁻"),
        ["PbI2"] = (9.8e-9, "Pb²⁺ + 2I⁻"),
        
        // Сульфаты
        ["BaSO4"] = (1.08e-10, "Ba²⁺ + SO₄²⁻"),
        ["CaSO4"] = (4.93e-5, "Ca²⁺ + SO₄²⁻"),
        ["PbSO4"] = (2.53e-8, "Pb²⁺ + SO₄²⁻"),
        ["Ag2SO4"] = (1.20e-5, "2Ag⁺ + SO₄²⁻"),
        
        // Карбонаты
        ["CaCO3"] = (3.36e-9, "Ca²⁺ + CO₃²⁻"),
        ["BaCO3"] = (2.58e-9, "Ba²⁺ + CO₃²⁻"),
        ["MgCO3"] = (6.82e-6, "Mg²⁺ + CO₃²⁻"),
        ["Ag2CO3"] = (8.46e-12, "2Ag⁺ + CO₃²⁻"),
        
        // Гидроксиды
        ["Mg(OH)2"] = (5.61e-12, "Mg²⁺ + 2OH⁻"),
        ["Ca(OH)2"] = (5.02e-6, "Ca²⁺ + 2OH⁻"),
        ["Fe(OH)3"] = (2.79e-39, "Fe³⁺ + 3OH⁻"),
        ["Al(OH)3"] = (3.00e-34, "Al³⁺ + 3OH⁻"),
        
        // Сульфиды
        ["CuS"] = (1.27e-36, "Cu²⁺ + S²⁻"),
        ["ZnS"] = (2.00e-25, "Zn²⁺ + S²⁻"),
        ["FeS"] = (6.00e-19, "Fe²⁺ + S²⁻"),
        ["PbS"] = (9.04e-29, "Pb²⁺ + S²⁻"),
        
        // Фосфаты
        ["Ca3(PO4)2"] = (2.07e-33, "3Ca²⁺ + 2PO₄³⁻"),
        ["Ag3PO4"] = (8.89e-17, "3Ag⁺ + PO₄³⁻"),
        
        // Хроматы
        ["BaCrO4"] = (1.17e-10, "Ba²⁺ + CrO₄²⁻"),
        ["Ag2CrO4"] = (1.12e-12, "2Ag⁺ + CrO₄²⁻"),
        ["PbCrO4"] = (2.8e-13, "Pb²⁺ + CrO₄²⁻"),
    };

    public SolubilityCalculator(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    // Расчёт растворимости из Ksp
    public ChemResult CalculateSolubility(ParsedCommand cmd)
    {
        try
        {
            var compound = cmd.Parameters["compound"];
            
            if (!_kspData.ContainsKey(compound))
                return ChemResult.Error($"Ksp data not available for {compound}");

            var (ksp, equation) = _kspData[compound];
            
            // Определение стехиометрии
            var (cationCoeff, anionCoeff) = GetStoichiometry(compound);
            
            // Расчёт растворимости: для AmBn → Ksp = (m·s)^m · (n·s)^n
            double solubility;
            
            if (cationCoeff == 1 && anionCoeff == 1)
            {
                // AB → A⁺ + B⁻, Ksp = s²
                solubility = Math.Sqrt(ksp);
            }
            else if (cationCoeff == 1 && anionCoeff == 2)
            {
                // AB₂ → A²⁺ + 2B⁻, Ksp = s·(2s)² = 4s³
                solubility = Math.Pow(ksp / 4, 1.0 / 3.0);
            }
            else if (cationCoeff == 2 && anionCoeff == 1)
            {
                // A₂B → 2A⁺ + B²⁻, Ksp = (2s)²·s = 4s³
                solubility = Math.Pow(ksp / 4, 1.0 / 3.0);
            }
            else if (cationCoeff == 1 && anionCoeff == 3)
            {
                // AB₃ → A³⁺ + 3B⁻, Ksp = s·(3s)³ = 27s⁴
                solubility = Math.Pow(ksp / 27, 1.0 / 4.0);
            }
            else if (cationCoeff == 3 && anionCoeff == 2)
            {
                // A₃B₂ → 3A²⁺ + 2B³⁻, Ksp = (3s)³·(2s)² = 108s⁵
                solubility = Math.Pow(ksp / 108, 1.0 / 5.0);
            }
            else if (cationCoeff == 2 && anionCoeff == 3)
            {
                // A₂B₃ → 2A³⁺ + 3B²⁻, Ksp = (2s)²·(3s)³ = 108s⁵
                solubility = Math.Pow(ksp / 108, 1.0 / 5.0);
            }
            else
            {
                return ChemResult.Error($"Stoichiometry not supported: {cationCoeff}:{anionCoeff}");
            }

            var result = ChemResult.Ok($"Solubility = {solubility:E3} mol/L = {solubility * 1000:E3} mmol/L");
            result.Data["solubility"] = solubility;
            result.Data["Ksp"] = ksp;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Compound: {compound}");
                result.Steps.Add($"Dissociation: {compound} ⇌ {equation}");
                result.Steps.Add($"Ksp = {ksp:E3}");
                result.Steps.Add($"Stoichiometry: {cationCoeff}:{anionCoeff}");
                
                string kspExpression = GetKspExpression(cationCoeff, anionCoeff);
                result.Steps.Add($"Ksp expression: {kspExpression}");
                result.Steps.Add($"Solubility (s) = {solubility:E3} mol/L");
                result.Steps.Add($"Solubility (s) = {solubility * 1000:E3} mmol/L");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Solubility calculation failed: {ex.Message}");
        }
    }

    // Расчёт растворимости с учётом общего ионного эффекта
    public ChemResult CalculateSolubilityWithCommonIon(ParsedCommand cmd)
    {
        try
        {
            var compound = cmd.Parameters["compound"];
            var commonIon = cmd.Parameters["common_ion"];
            var commonIonConc = double.Parse(cmd.Parameters["concentration"], CultureInfo.InvariantCulture);

            if (!_kspData.ContainsKey(compound))
                return ChemResult.Error($"Ksp data not available for {compound}");

            var (ksp, equation) = _kspData[compound];
            var (cationCoeff, anionCoeff) = GetStoichiometry(compound);

            // Упрощённый расчёт для случая AB → A⁺ + B⁻
            double solubility;
            
            if (cationCoeff == 1 && anionCoeff == 1)
            {
                // Ksp = [A⁺][B⁻] = s·(s + c) ≈ s·c при большом c
                solubility = ksp / commonIonConc;
            }
            else if (cationCoeff == 1 && anionCoeff == 2)
            {
                // AB₂ с общим ионом B⁻: Ksp = s·(2s + c)² ≈ s·c²
                solubility = ksp / (commonIonConc * commonIonConc);
            }
            else
            {
                return ChemResult.Error("Common ion effect calculation supported only for simple stoichiometries");
            }

            // Расчёт без общего иона для сравнения
            double normalSolubility = Math.Sqrt(ksp);

            var result = ChemResult.Ok($"Solubility = {solubility:E3} mol/L");
            result.Data["solubility_with_common_ion"] = solubility;
            result.Data["normal_solubility"] = normalSolubility;
            result.Data["suppression_factor"] = normalSolubility / solubility;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Compound: {compound}");
                result.Steps.Add($"Common ion: {commonIon} at {commonIonConc:E3} M");
                result.Steps.Add($"Ksp = {ksp:E3}");
                result.Steps.Add($"\nWithout common ion: s = {normalSolubility:E3} mol/L");
                result.Steps.Add($"With common ion: s = {solubility:E3} mol/L");
                result.Steps.Add($"Suppression factor: {normalSolubility / solubility:F1}×");
                result.Steps.Add("\n✓ Common ion effect decreases solubility");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Common ion effect calculation failed: {ex.Message}");
        }
    }

    // Определение, образуется ли осадок
    public ChemResult PredictPrecipitation(ParsedCommand cmd)
    {
        try
        {
            var compound = cmd.Parameters["compound"];
            var cationConc = double.Parse(cmd.Parameters["cation"], CultureInfo.InvariantCulture);
            var anionConc = double.Parse(cmd.Parameters["anion"], CultureInfo.InvariantCulture);

            if (!_kspData.ContainsKey(compound))
                return ChemResult.Error($"Ksp data not available for {compound}");

            var (ksp, equation) = _kspData[compound];
            var (cationCoeff, anionCoeff) = GetStoichiometry(compound);

            // Расчёт ионного произведения Q
            double Q;
            if (cationCoeff == 1 && anionCoeff == 1)
            {
                Q = cationConc * anionConc;
            }
            else if (cationCoeff == 1 && anionCoeff == 2)
            {
                Q = cationConc * Math.Pow(anionConc, 2);
            }
            else if (cationCoeff == 2 && anionCoeff == 1)
            {
                Q = Math.Pow(cationConc, 2) * anionConc;
            }
            else
            {
                return ChemResult.Error("Stoichiometry not supported for precipitation prediction");
            }

            bool willPrecipitate = Q > ksp;
            string prediction;

            if (Q > ksp)
                prediction = "YES - Precipitation will occur (Q > Ksp)";
            else if (Q < ksp)
                prediction = "NO - Solution is unsaturated (Q < Ksp)";
            else
                prediction = "EQUILIBRIUM - Solution is saturated (Q = Ksp)";

            var result = ChemResult.Ok(prediction);
            result.Data["Q"] = Q;
            result.Data["Ksp"] = ksp;
            result.Data["will_precipitate"] = willPrecipitate;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Compound: {compound}");
                result.Steps.Add($"Dissociation: {compound} ⇌ {equation}");
                result.Steps.Add($"Ksp = {ksp:E3}");
                result.Steps.Add($"\n[Cation] = {cationConc:E3} M");
                result.Steps.Add($"[Anion] = {anionConc:E3} M");
                result.Steps.Add($"\nIonic product Q = {Q:E3}");
                result.Steps.Add($"Comparison: Q/Ksp = {Q / ksp:F2}");
                result.Steps.Add($"\nPrediction: {prediction}");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Precipitation prediction failed: {ex.Message}");
        }
    }

    // Дробное осаждение
    public ChemResult FractionalPrecipitation(ParsedCommand cmd)
    {
        try
        {
            var compound1 = cmd.Parameters["compound1"];
            var compound2 = cmd.Parameters["compound2"];
            // По умолчанию ищем параметр anion, если нет - то anion_concentration
            var anionParam = cmd.Parameters.ContainsKey("anion") ? cmd.Parameters["anion"] : 
                             cmd.Parameters.ContainsKey("anion_concentration") ? cmd.Parameters["anion_concentration"] : null;
            
            if (anionParam == null)
                 return ChemResult.Error("Anion concentration required (use 'anion' or 'anion_concentration')");

            var anionConc = double.Parse(anionParam, CultureInfo.InvariantCulture);

            if (!_kspData.ContainsKey(compound1) || !_kspData.ContainsKey(compound2))
                return ChemResult.Error("Ksp data not available for one or both compounds");

            var (ksp1, eq1) = _kspData[compound1];
            var (ksp2, eq2) = _kspData[compound2];

            // Для простоты рассматриваем случай AB (1:1)
            double cationConc1 = ksp1 / anionConc;
            double cationConc2 = ksp2 / anionConc;

            string first = ksp1 < ksp2 ? compound1 : compound2;
            string second = ksp1 < ksp2 ? compound2 : compound1;

            var result = ChemResult.Ok($"Order of precipitation: {first} first, then {second}");
            result.Data["first_to_precipitate"] = first;
            result.Data["ksp1"] = ksp1;
            result.Data["ksp2"] = ksp2;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Fractional Precipitation Analysis");
                result.Steps.Add($"{compound1}: Ksp = {ksp1:E3}");
                result.Steps.Add($"{compound2}: Ksp = {ksp2:E3}");
                result.Steps.Add($"\nWith [Anion] = {anionConc:E3} M:");
                result.Steps.Add($"{compound1} precipitates when [cation] > {cationConc1:E3} M");
                result.Steps.Add($"{compound2} precipitates when [cation] > {cationConc2:E3} M");
                result.Steps.Add($"\n✓ {first} precipitates first (lower Ksp)");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Fractional precipitation analysis failed: {ex.Message}");
        }
    }

    // Вспомогательные методы
    private (int cation, int anion) GetStoichiometry(string compound)
    {
        // Упрощённое определение стехиометрии на основе формулы
        // В реальности нужен парсер формул
        return compound switch
        {
            "AgCl" or "AgBr" or "AgI" or "BaSO4" or "CaSO4" or "PbSO4" or 
            "CaCO3" or "BaCO3" or "MgCO3" or "BaCrO4" or "PbCrO4" or
            "CuS" or "ZnS" or "FeS" or "PbS" => (1, 1),
            
            "PbCl2" or "PbBr2" or "PbI2" or "Hg2Cl2" or "Mg(OH)2" or "Ca(OH)2" => (1, 2),
            
            "Ag2SO4" or "Ag2CO3" or "Ag2CrO4" => (2, 1),
            
            "Fe(OH)3" or "Al(OH)3" => (1, 3),
            
            "Ag3PO4" or "3Ag⁺ + PO₄³⁻" => (3, 1), // исправление возможной опечатки в switch
            
            "Ca3(PO4)2" => (3, 2),
            
            _ => (1, 1) // по умолчанию
        };
    }

    private string GetKspExpression(int m, int n)
    {
        if (m == 1 && n == 1) return "Ksp = s²";
        if (m == 1 && n == 2) return "Ksp = s·(2s)² = 4s³";
        if (m == 2 && n == 1) return "Ksp = (2s)²·s = 4s³";
        if (m == 1 && n == 3) return "Ksp = s·(3s)³ = 27s⁴";
        if (m == 3 && n == 1) return "Ksp = (3s)³·s = 27s⁴";
        if (m == 3 && n == 2) return "Ksp = (3s)³·(2s)² = 108s⁵";
        return "Ksp = (ms)^m·(ns)^n";
    }
}
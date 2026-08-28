using AI.Solvers.Chem.Core;
using System.Globalization;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Medical;

// ═══════════════════════════════════════════════════════════
// КИСЛОТНО-ОСНОВНОЕ РАВНОВЕСИЕ КРОВИ (BLOOD GAS ANALYSIS)
// ═══════════════════════════════════════════════════════════
public class BloodGasAnalyzer
{
    private readonly VerbosityLevel _verbosity;

    public BloodGasAnalyzer(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    // Анализ газов крови
    public ChemResult AnalyzeBloodGas(ParsedCommand cmd)
    {
        try
        {
            double pH = cmd.GetDouble("pH");
            double pCO2 = cmd.GetDouble("pCO2"); // mmHg
            double HCO3 = cmd.GetDoubleOrDefault(0, "HCO3"); // mEq/L

            // Если HCO3 не задан, рассчитываем по Henderson-Hasselbalch
            if (HCO3 == 0)
            {
                // pH = 6.1 + log(HCO3 / (0.03 * pCO2))
                // HCO3 = 0.03 * pCO2 * 10^(pH - 6.1)
                HCO3 = 0.03 * pCO2 * Math.Pow(10, pH - 6.1);
            }

            // Нормальные значения
            const double normalPH = 7.40;
            const double normalPCO2 = 40.0;
            const double normalHCO3 = 24.0;
            
            // Допустимые диапазоны
            bool pHNormal = pH >= 7.35 && pH <= 7.45;
            bool pCO2Normal = pCO2 >= 35 && pCO2 <= 45;
            bool HCO3Normal = HCO3 >= 22 && HCO3 <= 26;

            // Определение типа нарушения
            string diagnosis = DetermineAcidBaseDisturbance(pH, pCO2, HCO3);

            // Расчёт анионной разницы (если есть данные по электролитам)
            double? anionGap = null;
            if (cmd.Has("Na") && cmd.Has("Cl"))
            {
                double Na = cmd.GetDouble("Na");
                double Cl = cmd.GetDouble("Cl");
                anionGap = Na - (Cl + HCO3);
            }

            // Проверка компенсации
            string compensation = CheckCompensation(pH, pCO2, HCO3, diagnosis);

            var result = ChemResult.Ok(diagnosis);
            result.Data["pH"] = pH;
            result.Data["pCO2"] = pCO2;
            result.Data["HCO3"] = HCO3;
            result.Data["diagnosis"] = diagnosis;
            result.Data["compensation"] = compensation;
            if (anionGap.HasValue)
                result.Data["anion_gap"] = anionGap.Value;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Blood Gas Analysis (Arterial)");
                result.Steps.Add("\n═══ Measured Values ═══");
                result.Steps.Add($"pH = {pH:F2} {(pHNormal ? "" : "")} (normal: 7.35-7.45)");
                result.Steps.Add($"pCO₂ = {pCO2:F1} mmHg {(pCO2Normal ? "" : "")} (normal: 35-45)");
                result.Steps.Add($"HCO₃⁻ = {HCO3:F1} mEq/L {(HCO3Normal ? "" : "")} (normal: 22-26)");

                if (anionGap.HasValue)
                {
                    bool agNormal = anionGap >= 8 && anionGap <= 16;
                    result.Steps.Add($"\nAnion Gap = {anionGap:F1} {(agNormal ? "" : "")} (normal: 8-16)");
                    result.Steps.Add($"AG = Na⁺ - (Cl⁻ + HCO₃⁻)");
                }

                result.Steps.Add("\n═══ Interpretation ═══");
                result.Steps.Add($"Primary disorder: {diagnosis}");
                result.Steps.Add($"Compensation: {compensation}");

                // Объяснение механизма
                result.Steps.Add("\n═══ Mechanism ═══");
                if (diagnosis.Contains("Respiratory"))
                {
                    result.Steps.Add("Primary problem: Ventilation (pCO₂)");
                    result.Steps.Add("Compensation: Renal (HCO₃⁻) - takes days");
                }
                else if (diagnosis.Contains("Metabolic"))
                {
                    result.Steps.Add("Primary problem: Metabolic (HCO₃⁻)");
                    result.Steps.Add("Compensation: Respiratory (pCO₂) - minutes to hours");
                }

                // Клинические рекомендации
                result.Steps.Add("\n═══ Clinical Notes ═══");
                if (diagnosis.Contains("Acidosis"))
                    result.Steps.Add("Acidosis present - check for causes");
                else if (diagnosis.Contains("Alkalosis"))
                    result.Steps.Add("Alkalosis present - check for causes");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Blood gas analysis failed: {ex.Message}");
        }
    }

    // Расчёт HCO3 по Henderson-Hasselbalch
    public ChemResult CalculateBicarbonate(ParsedCommand cmd)
    {
        try
        {
            double pH = cmd.GetDouble("pH");
            double pCO2 = cmd.GetDouble("pCO2"); // mmHg

            // Henderson-Hasselbalch для бикарбонатной буферной системы:
            // pH = 6.1 + log([HCO3-] / [H2CO3])
            // [H2CO3] = 0.03 * pCO2 (растворимость CO2)
            // HCO3 = 0.03 * pCO2 * 10^(pH - 6.1)
            
            double HCO3 = 0.03 * pCO2 * Math.Pow(10, pH - 6.1);

            var result = ChemResult.Ok($"[HCO₃⁻] = {HCO3:F1} mEq/L");
            result.Data["HCO3"] = HCO3;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Henderson-Hasselbalch Equation for Blood");
                result.Steps.Add("pH = pKa + log([HCO₃⁻]/[H₂CO₃])");
                result.Steps.Add($"pKa = 6.1 for H₂CO₃/HCO₃⁻ system");
                result.Steps.Add($"[H₂CO₃] = 0.03 × pCO₂ = 0.03 × {pCO2} = {0.03 * pCO2:F2} mM");
                result.Steps.Add($"\nRearranging:");
                result.Steps.Add($"{pH:F2} = 6.1 + log([HCO₃⁻]/{0.03 * pCO2:F2})");
                result.Steps.Add($"[HCO₃⁻] = {0.03 * pCO2:F2} × 10^({pH:F2} - 6.1)");
                result.Steps.Add($"[HCO₃⁻] = {HCO3:F1} mEq/L");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Bicarbonate calculation failed: {ex.Message}");
        }
    }

    // Расчёт дефицита/избытка оснований (Base Excess)
    public ChemResult CalculateBaseExcess(ParsedCommand cmd)
    {
        try
        {
            double HCO3 = cmd.GetDouble("HCO3"); // mEq/L
            double pH = cmd.GetDouble("pH");

            // Упрощённая формула Van Slyke:
            // BE = 0.93 × (HCO3 - 24.4 + 14.8 × (pH - 7.4))
            double BE = 0.93 * (HCO3 - 24.4 + 14.8 * (pH - 7.4));

            string interpretation;
            if (BE > 2)
                interpretation = "Metabolic Alkalosis (excess base)";
            else if (BE < -2)
                interpretation = "Metabolic Acidosis (base deficit)";
            else
                interpretation = "Normal";

            var result = ChemResult.Ok($"Base Excess = {BE:F1} mEq/L ({interpretation})");
            result.Data["base_excess"] = BE;
            result.Data["interpretation"] = interpretation;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Base Excess Calculation (Van Slyke formula)");
                result.Steps.Add("BE = 0.93 × (HCO₃⁻ - 24.4 + 14.8 × (pH - 7.4))");
                result.Steps.Add($"BE = 0.93 × ({HCO3:F1} - 24.4 + 14.8 × ({pH:F2} - 7.4))");
                result.Steps.Add($"BE = {BE:F1} mEq/L");
                result.Steps.Add($"\nInterpretation:");
                result.Steps.Add($"Normal range: -2 to +2 mEq/L");
                result.Steps.Add($"Result: {interpretation}");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Base excess calculation failed: {ex.Message}");
        }
    }

    // Вспомогательные методы
    private string DetermineAcidBaseDisturbance(double pH, double pCO2, double HCO3)
    {
        // pH < 7.35 = Acidosis, pH > 7.45 = Alkalosis
        // pCO2 > 45 = Respiratory acidosis, pCO2 < 35 = Respiratory alkalosis
        // HCO3 < 22 = Metabolic acidosis, HCO3 > 26 = Metabolic alkalosis

        if (pH < 7.35) // Acidosis
        {
            if (pCO2 > 45)
                return "Respiratory Acidosis";
            else if (HCO3 < 22)
                return "Metabolic Acidosis";
            else
                return "Mixed Acidosis";
        }
        else if (pH > 7.45) // Alkalosis
        {
            if (pCO2 < 35)
                return "Respiratory Alkalosis";
            else if (HCO3 > 26)
                return "Metabolic Alkalosis";
            else
                return "Mixed Alkalosis";
        }
        else // pH normal
        {
            if (pCO2 > 45 && HCO3 > 26)
                return "Compensated Respiratory Acidosis";
            else if (pCO2 < 35 && HCO3 < 22)
                return "Compensated Respiratory Alkalosis";
            else if (HCO3 < 22 && pCO2 < 35)
                return "Compensated Metabolic Acidosis";
            else if (HCO3 > 26 && pCO2 > 45)
                return "Compensated Metabolic Alkalosis";
            else
                return "Normal";
        }
    }

    private string CheckCompensation(double pH, double pCO2, double HCO3, string diagnosis)
    {
        // Правила ожидаемой компенсации:
        
        if (diagnosis.Contains("Metabolic Acidosis"))
        {
            // Ожидаемый pCO2 = 1.5 × HCO3 + 8 (±2)
            double expectedPCO2 = 1.5 * HCO3 + 8;
            if (Math.Abs(pCO2 - expectedPCO2) <= 2)
                return "Appropriate respiratory compensation";
            else if (pCO2 > expectedPCO2 + 2)
                return "Inadequate compensation (respiratory component present)";
            else
                return "Overcompensation (consider mixed disorder)";
        }
        else if (diagnosis.Contains("Metabolic Alkalosis"))
        {
            // Ожидаемый pCO2 = 0.7 × HCO3 + 20 (±5)
            double expectedPCO2 = 0.7 * HCO3 + 20;
            if (Math.Abs(pCO2 - expectedPCO2) <= 5)
                return "Appropriate respiratory compensation";
            else
                return "Check for mixed disorder";
        }
        else if (diagnosis.Contains("Respiratory Acidosis"))
        {
            // Острый: HCO3 увеличивается на 1 на каждые 10 mmHg ↑pCO2
            // Хронический: HCO3 увеличивается на 3.5 на каждые 10 mmHg ↑pCO2
            double deltaPCO2 = pCO2 - 40;
            double expectedAcuteHCO3 = 24 + (deltaPCO2 / 10);
            double expectedChronicHCO3 = 24 + (deltaPCO2 / 10) * 3.5;
            
            if (HCO3 < expectedAcuteHCO3)
                return "Acute respiratory acidosis (uncompensated)";
            else if (HCO3 > expectedChronicHCO3)
                return "Chronic respiratory acidosis (compensated)";
            else
                return "Partial compensation";
        }
        else if (diagnosis.Contains("Respiratory Alkalosis"))
        {
            // Острый: HCO3 снижается на 2 на каждые 10 mmHg ↓pCO2
            // Хронический: HCO3 снижается на 5 на каждые 10 mmHg ↓pCO2
            double deltaPCO2 = 40 - pCO2;
            double expectedAcuteHCO3 = 24 - (deltaPCO2 / 10) * 2;
            double expectedChronicHCO3 = 24 - (deltaPCO2 / 10) * 5;
            
            if (HCO3 > expectedAcuteHCO3)
                return "Acute respiratory alkalosis (uncompensated)";
            else if (HCO3 < expectedChronicHCO3)
                return "Chronic respiratory alkalosis (compensated)";
            else
                return "Partial compensation";
        }

        return "Not applicable";
    }
}


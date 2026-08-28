using AI.Solvers.Chem.Core;
using System.Globalization;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.Processors.Medical;

// ═══════════════════════════════════════════════════════════
// ФАРМАКОКИНЕТИКА
// ═══════════════════════════════════════════════════════════
public class PharmacokineticCalculator
{
    private readonly VerbosityLevel _verbosity;

    public PharmacokineticCalculator(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    // Однокамерная модель: C(t) = C0 * exp(-k*t)
    public ChemResult OneCompartmentModel(ParsedCommand cmd)
    {
        try
        {
            var modelType = cmd.GetStringOrDefault("iv_bolus", "type");

            return modelType switch
            {
                "iv_bolus" => IVBolus(cmd),
                "continuous" => ContinuousInfusion(cmd),
                "oral" => OralAdministration(cmd),
                _ => ChemResult.Error($"Unknown model type: {modelType}")
            };
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Pharmacokinetic calculation failed: {ex.Message}");
        }
    }

    // Внутривенное болюсное введение
    private ChemResult IVBolus(ParsedCommand cmd)
    {
        double dose = cmd.GetDouble("dose"); // мг
        double Vd = cmd.GetDouble("Vd"); // L (объём распределения)
        double t_half = cmd.GetDouble("t_half"); // часы
        double time = cmd.GetDoubleOrDefault(0, "time"); // часы

        // Константа элиминации: k = 0.693 / t_half
        double k = 0.693 / t_half;
        
        // Начальная концентрация: C0 = Dose / Vd
        double C0 = dose / Vd;
        
        // Концентрация в момент времени t: C(t) = C0 * exp(-k*t)
        double Ct = C0 * Math.Exp(-k * time);
        
        // Клиренс: CL = k * Vd
        double CL = k * Vd;
        
        // AUC (площадь под кривой): AUC = C0 / k = Dose / CL
        double AUC = C0 / k;

        var result = ChemResult.Ok($"C({time}h) = {Ct:F2} mg/L");
        result.Data["C0"] = C0;
        result.Data["Ct"] = Ct;
        result.Data["k"] = k;
        result.Data["CL"] = CL;
        result.Data["AUC"] = AUC;
        result.Data["t_half"] = t_half;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("One-Compartment Model - IV Bolus");
            result.Steps.Add($"Dose = {dose} mg");
            result.Steps.Add($"Volume of distribution (Vd) = {Vd} L");
            result.Steps.Add($"Half-life (t½) = {t_half} h");
            result.Steps.Add($"\nElimination rate constant: k = 0.693/t½ = {k:F4} h⁻¹");
            result.Steps.Add($"Initial concentration: C₀ = Dose/Vd = {C0:F2} mg/L");
            result.Steps.Add($"Clearance: CL = k·Vd = {CL:F2} L/h");
            result.Steps.Add($"AUC₀₋∞ = Dose/CL = {AUC:F2} mg·h/L");
            result.Steps.Add($"\nConcentration at t={time}h:");
            result.Steps.Add($"C(t) = C₀·e⁻ᵏᵗ = {C0:F2}·e^(-{k:F4}·{time}) = {Ct:F2} mg/L");
        }

        return result;
    }

    // Непрерывная инфузия
    private ChemResult ContinuousInfusion(ParsedCommand cmd)
    {
        double infusionRate = cmd.GetDouble("infusion_rate"); // мг/ч
        double Vd = cmd.GetDouble("Vd"); // L
        double t_half = cmd.GetDouble("t_half"); // часы
        double time = cmd.GetDoubleOrDefault(0, "time"); // часы

        double k = 0.693 / t_half;
        double CL = k * Vd;
        
        // Стационарная концентрация: Css = R / CL
        double Css = infusionRate / CL;
        
        // Концентрация в момент времени t: C(t) = Css * (1 - exp(-k*t))
        double Ct = Css * (1 - Math.Exp(-k * time));
        
        // Время достижения 50%, 90%, 95% Css
        double t_50 = t_half; // 1 период полувыведения
        double t_90 = 3.32 * t_half; // ~3.3 периода
        double t_95 = 4.32 * t_half; // ~4.3 периода

        var result = ChemResult.Ok($"C({time}h) = {Ct:F2} mg/L, Css = {Css:F2} mg/L");
        result.Data["Css"] = Css;
        result.Data["Ct"] = Ct;
        result.Data["time_to_90_percent"] = t_90;
        result.Data["time_to_95_percent"] = t_95;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Continuous Infusion - One-Compartment Model");
            result.Steps.Add($"Infusion rate (R) = {infusionRate} mg/h");
            result.Steps.Add($"Vd = {Vd} L, t½ = {t_half} h");
            result.Steps.Add($"CL = {CL:F2} L/h");
            result.Steps.Add($"\nSteady-state concentration:");
            result.Steps.Add($"Css = R/CL = {infusionRate}/{CL:F2} = {Css:F2} mg/L");
            result.Steps.Add($"\nTime to reach steady state:");
            result.Steps.Add($"50% Css: {t_50:F1} h (1 half-life)");
            result.Steps.Add($"90% Css: {t_90:F1} h (3.3 half-lives)");
            result.Steps.Add($"95% Css: {t_95:F1} h (4.3 half-lives)");
            result.Steps.Add($"\nAt t={time}h: C(t) = Css·(1-e⁻ᵏᵗ) = {Ct:F2} mg/L");
            result.Steps.Add($"Progress: {(Ct / Css * 100):F1}% of Css reached");
        }

        return result;
    }

    // Пероральное введение (с абсорбцией)
    private ChemResult OralAdministration(ParsedCommand cmd)
    {
        double dose = cmd.GetDouble("dose"); // мг
        double F = cmd.GetDoubleOrDefault(1.0, "bioavailability"); // биодоступность
        double Vd = cmd.GetDouble("Vd"); // L
        double ka = cmd.GetDouble("ka"); // константа абсорбции, ч⁻¹
        double t_half = cmd.GetDouble("t_half"); // часы
        double time = cmd.GetDoubleOrDefault(0, "time"); // часы

        double k = 0.693 / t_half; // константа элиминации
        
        // C(t) = (F·D·ka)/(Vd·(ka-k)) · (e^(-k·t) - e^(-ka·t))
        double factor = (F * dose * ka) / (Vd * (ka - k));
        double Ct = factor * (Math.Exp(-k * time) - Math.Exp(-ka * time));
        
        // Время достижения Cmax: tmax = ln(ka/k) / (ka - k)
        double tmax = Math.Log(ka / k) / (ka - k);
        double Cmax = factor * (Math.Exp(-k * tmax) - Math.Exp(-ka * tmax));

        var result = ChemResult.Ok($"C({time}h) = {Ct:F2} mg/L, Cmax = {Cmax:F2} mg/L at {tmax:F2}h");
        result.Data["Ct"] = Ct;
        result.Data["Cmax"] = Cmax;
        result.Data["tmax"] = tmax;
        result.Data["bioavailability"] = F;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Oral Administration - One-Compartment Model");
            result.Steps.Add($"Dose = {dose} mg");
            result.Steps.Add($"Bioavailability (F) = {F * 100}%");
            result.Steps.Add($"Absorption rate (ka) = {ka:F3} h⁻¹");
            result.Steps.Add($"Elimination rate (k) = {k:F4} h⁻¹");
            result.Steps.Add($"Vd = {Vd} L");
            result.Steps.Add($"\nPeak concentration:");
            result.Steps.Add($"tmax = {tmax:F2} h");
            result.Steps.Add($"Cmax = {Cmax:F2} mg/L");
            result.Steps.Add($"\nAt t={time}h: C(t) = {Ct:F2} mg/L");
        }

        return result;
    }

    // Расчёт дозы для достижения целевой концентрации
    public ChemResult CalculateDose(ParsedCommand cmd)
    {
        try
        {
            double targetConc = cmd.GetDouble("target_concentration"); // мг/L
            double Vd = cmd.GetDouble("Vd"); // L
            double F = cmd.GetDoubleOrDefault(1.0, "bioavailability");

            // Loading dose: LD = Ctarget * Vd / F
            double loadingDose = targetConc * Vd / F;

            var result = ChemResult.Ok($"Loading dose = {loadingDose:F1} mg");
            result.Data["loading_dose"] = loadingDose;

            if (cmd.Has("t_half"))
            {
                double t_half = cmd.GetDouble("t_half");
                double k = 0.693 / t_half;
                double CL = k * Vd;
                
                // Maintenance dose rate: R = Ctarget * CL / F
                double maintenanceRate = targetConc * CL / F;
                
                result.Result = $"Loading dose = {loadingDose:F1} mg, Maintenance = {maintenanceRate:F2} mg/h";
                result.Data["maintenance_rate"] = maintenanceRate;

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add("Dose Calculation");
                    result.Steps.Add($"Target concentration = {targetConc} mg/L");
                    result.Steps.Add($"Vd = {Vd} L, F = {F * 100}%");
                    result.Steps.Add($"\nLoading dose: LD = Ctarget·Vd/F = {loadingDose:F1} mg");
                    result.Steps.Add($"\nFor continuous maintenance:");
                    result.Steps.Add($"CL = {CL:F2} L/h");
                    result.Steps.Add($"Maintenance rate = Ctarget·CL/F = {maintenanceRate:F2} mg/h");
                }
            }
            else
            {
                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add("Loading Dose Calculation");
                    result.Steps.Add($"Target concentration = {targetConc} mg/L");
                    result.Steps.Add($"Vd = {Vd} L, F = {F * 100}%");
                    result.Steps.Add($"LD = Ctarget·Vd/F = {loadingDose:F1} mg");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Dose calculation failed: {ex.Message}");
        }
    }

    // Двухкамерная модель (упрощённая)
    public ChemResult TwoCompartmentModel(ParsedCommand cmd)
    {
        // Более сложная модель с центральным и периферическим компартментами
        // C(t) = A·e^(-α·t) + B·e^(-β·t)
        // где α - быстрая фаза распределения, β - медленная фаза элиминации
        
        return ChemResult.Error("Two-compartment model requires advanced implementation");
    }

    // Расчёт периода полувыведения из данных
    public ChemResult CalculateHalfLife(ParsedCommand cmd)
    {
        try
        {
            double C1 = cmd.GetDouble("C1"); // концентрация в момент t1
            double C2 = cmd.GetDouble("C2"); // концентрация в момент t2
            double t1 = cmd.GetDouble("t1"); // часы
            double t2 = cmd.GetDouble("t2"); // часы

            // C2 = C1 * exp(-k*(t2-t1))
            // k = -ln(C2/C1) / (t2-t1)
            double k = -Math.Log(C2 / C1) / (t2 - t1);
            double t_half = 0.693 / k;

            var result = ChemResult.Ok($"Half-life = {t_half:F2} hours");
            result.Data["t_half"] = t_half;
            result.Data["k"] = k;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Half-Life Calculation from Concentration Data");
                result.Steps.Add($"C({t1}h) = {C1:F2} mg/L");
                result.Steps.Add($"C({t2}h) = {C2:F2} mg/L");
                result.Steps.Add($"\nElimination rate constant:");
                result.Steps.Add($"k = -ln(C₂/C₁)/(t₂-t₁) = -ln({C2}/{C1})/{t2 - t1} = {k:F4} h⁻¹");
                result.Steps.Add($"\nHalf-life: t½ = 0.693/k = {t_half:F2} h");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Half-life calculation failed: {ex.Message}");
        }
    }
}

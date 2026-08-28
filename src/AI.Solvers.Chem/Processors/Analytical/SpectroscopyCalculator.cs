using FractalAgentsAI.Solvers.Chem.Core;
using System.Globalization;

namespace FractalAgentsAI.Solvers.Chem.Processors.Analytical;

// ═══════════════════════════════════════════════════════════
// СПЕКТРОСКОПИЯ (ЗАКОН БУГЕРА-ЛАМБЕРТА-БЕРА)
// ═══════════════════════════════════════════════════════════
public class SpectroscopyCalculator
{
    private readonly VerbosityLevel _verbosity;

    public SpectroscopyCalculator(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    // Закон Бера: A = ε·c·l
    public ChemResult BeersLaw(ParsedCommand cmd)
    {
        try
        {
            // Нормализация параметров (поддержка алиасов)
            NormalizeParameters(cmd.Parameters);

            var calculation = cmd.Parameters.GetValueOrDefault("calculate");

            // Автоматическое определение, что считать, если не указано
            if (string.IsNullOrEmpty(calculation))
            {
                if (!cmd.Parameters.ContainsKey("absorbance") && cmd.Parameters.ContainsKey("epsilon") && cmd.Parameters.ContainsKey("concentration"))
                    calculation = "absorbance";
                else if (!cmd.Parameters.ContainsKey("concentration") && cmd.Parameters.ContainsKey("absorbance") && cmd.Parameters.ContainsKey("epsilon"))
                    calculation = "concentration";
                else if (!cmd.Parameters.ContainsKey("epsilon") && cmd.Parameters.ContainsKey("absorbance") && cmd.Parameters.ContainsKey("concentration"))
                    calculation = "molar_absorptivity";
                else if (cmd.Parameters.ContainsKey("transmittance") || (cmd.Parameters.ContainsKey("absorbance") && !cmd.Parameters.ContainsKey("epsilon")))
                    calculation = "transmittance"; // Конвертация, если мало данных для закона Бера
                else
                    calculation = "concentration"; // По умолчанию
            }

            return calculation switch
            {
                "concentration" => CalculateConcentration(cmd),
                "absorbance" => CalculateAbsorbance(cmd),
                "molar_absorptivity" => CalculateMolarAbsorptivity(cmd),
                "transmittance" => ConvertTransmittance(cmd),
                _ => ChemResult.Error($"Unknown calculation type: {calculation}")
            };
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Beer's Law calculation failed: {ex.Message}");
        }
    }

    private void NormalizeParameters(Dictionary<string, string> p)
    {
        // Absorbance
        if (!p.ContainsKey("absorbance") && p.ContainsKey("A")) p["absorbance"] = p["A"];
        
        // Concentration
        if (!p.ContainsKey("concentration") && p.ContainsKey("c")) p["concentration"] = p["c"];
        
        // Epsilon
        if (!p.ContainsKey("epsilon") && p.ContainsKey("eps")) p["epsilon"] = p["eps"];
        
        // Pathlength
        if (!p.ContainsKey("pathlength"))
        {
            if (p.ContainsKey("l")) p["pathlength"] = p["l"];
            else if (p.ContainsKey("L")) p["pathlength"] = p["L"];
            else p["pathlength"] = "1.0"; // Default 1 cm
        }

        // Transmittance
        if (!p.ContainsKey("transmittance") && p.ContainsKey("T")) p["transmittance"] = p["T"];
        if (!p.ContainsKey("transmittance") && p.ContainsKey("percentT")) p["transmittance"] = p["percentT"];
    }

    // Расчёт концентрации по поглощению
    private ChemResult CalculateConcentration(ParsedCommand cmd)
    {
        if (!cmd.Parameters.ContainsKey("absorbance")) return ChemResult.Error("Absorbance (A) is required");
        if (!cmd.Parameters.ContainsKey("epsilon")) return ChemResult.Error("Molar absorptivity (epsilon) is required");

        double absorbance = double.Parse(cmd.Parameters["absorbance"], CultureInfo.InvariantCulture); // A
        double epsilon = double.Parse(cmd.Parameters["epsilon"], CultureInfo.InvariantCulture); // L/(mol·cm), молярный коэффициент поглощения
        double pathlength = double.Parse(cmd.Parameters["pathlength"], CultureInfo.InvariantCulture); // cm

        // A = ε·c·l → c = A / (ε·l)
        double concentration = absorbance / (epsilon * pathlength);

        var result = ChemResult.Ok($"Concentration = {concentration:E3} M");
        result.Data["concentration"] = concentration;
        result.Data["absorbance"] = absorbance;
        result.Data["epsilon"] = epsilon;
        result.Data["pathlength"] = pathlength;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Beer-Lambert Law: A = ε·c·l");
            result.Steps.Add($"Absorbance (A) = {absorbance:F3}");
            result.Steps.Add($"Molar absorptivity (ε) = {epsilon:E2} L/(mol·cm)");
            result.Steps.Add($"Path length (l) = {pathlength} cm");
            result.Steps.Add($"\nRearranging: c = A / (ε·l)");
            result.Steps.Add($"c = {absorbance:F3} / ({epsilon:E2} × {pathlength})");
            result.Steps.Add($"c = {concentration:E3} M");
            result.Steps.Add($"c = {concentration * 1000:E3} mM");
            result.Steps.Add($"c = {concentration * 1e6:F2} µM");
        }

        return result;
    }

    // Расчёт поглощения
    private ChemResult CalculateAbsorbance(ParsedCommand cmd)
    {
        if (!cmd.Parameters.ContainsKey("concentration")) return ChemResult.Error("Concentration (c) is required");
        if (!cmd.Parameters.ContainsKey("epsilon")) return ChemResult.Error("Molar absorptivity (epsilon) is required");

        double concentration = double.Parse(cmd.Parameters["concentration"], CultureInfo.InvariantCulture); // M
        double epsilon = double.Parse(cmd.Parameters["epsilon"], CultureInfo.InvariantCulture); // L/(mol·cm)
        double pathlength = double.Parse(cmd.Parameters["pathlength"], CultureInfo.InvariantCulture); // cm

        // A = ε·c·l
        double absorbance = epsilon * concentration * pathlength;

        // Transmittance: T = 10^(-A)
        double transmittance = Math.Pow(10, -absorbance);
        double percentTransmittance = transmittance * 100;

        var result = ChemResult.Ok($"Absorbance = {absorbance:F3}, %T = {percentTransmittance:F2}%");
        result.Data["absorbance"] = absorbance;
        result.Data["transmittance"] = transmittance;
        result.Data["percent_transmittance"] = percentTransmittance;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Beer-Lambert Law: A = ε·c·l");
            result.Steps.Add($"Concentration (c) = {concentration:E3} M");
            result.Steps.Add($"Molar absorptivity (ε) = {epsilon:E2} L/(mol·cm)");
            result.Steps.Add($"Path length (l) = {pathlength} cm");
            result.Steps.Add($"\nA = {epsilon:E2} × {concentration:E3} × {pathlength}");
            result.Steps.Add($"A = {absorbance:F3}");
            result.Steps.Add($"\nTransmittance: T = 10⁻ᴬ = {transmittance:F4}");
            result.Steps.Add($"%T = {percentTransmittance:F2}%");
        }

        return result;
    }

    // Определение молярного коэффициента поглощения
    private ChemResult CalculateMolarAbsorptivity(ParsedCommand cmd)
    {
        if (!cmd.Parameters.ContainsKey("absorbance")) return ChemResult.Error("Absorbance (A) is required");
        if (!cmd.Parameters.ContainsKey("concentration")) return ChemResult.Error("Concentration (c) is required");

        double absorbance = double.Parse(cmd.Parameters["absorbance"], CultureInfo.InvariantCulture);
        double concentration = double.Parse(cmd.Parameters["concentration"], CultureInfo.InvariantCulture); // M
        double pathlength = double.Parse(cmd.Parameters["pathlength"], CultureInfo.InvariantCulture); // cm

        // ε = A / (c·l)
        double epsilon = absorbance / (concentration * pathlength);

        var result = ChemResult.Ok($"Molar Absorptivity (ε) = {epsilon:E3} L/(mol·cm)");
        result.Data["epsilon"] = epsilon;

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            result.Steps.Add("Calculating Molar Absorptivity");
            result.Steps.Add($"A = {absorbance:F3}");
            result.Steps.Add($"c = {concentration:E3} M");
            result.Steps.Add($"l = {pathlength} cm");
            result.Steps.Add($"\nε = A / (c·l) = {epsilon:E3} L/(mol·cm)");
        }

        return result;
    }

    // Конвертация между поглощением и пропусканием
    private ChemResult ConvertTransmittance(ParsedCommand cmd)
    {
        if (cmd.Parameters.ContainsKey("absorbance"))
        {
            double absorbance = double.Parse(cmd.Parameters["absorbance"], CultureInfo.InvariantCulture);
            double transmittance = Math.Pow(10, -absorbance);
            double percentT = transmittance * 100;

            var result = ChemResult.Ok($"%T = {percentT:F2}%, T = {transmittance:F4}");
            result.Data["transmittance"] = transmittance;
            result.Data["percent_transmittance"] = percentT;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Absorbance → Transmittance");
                result.Steps.Add($"A = {absorbance:F3}");
                result.Steps.Add($"T = 10⁻ᴬ = 10^(-{absorbance:F3}) = {transmittance:F4}");
                result.Steps.Add($"%T = {percentT:F2}%");
            }

            return result;
        }
        else if (cmd.Parameters.ContainsKey("transmittance"))
        {
            double percentT = double.Parse(cmd.Parameters["transmittance"], CultureInfo.InvariantCulture); // %
            double transmittance = percentT / 100;
            double absorbance = -Math.Log10(transmittance);

            var result = ChemResult.Ok($"Absorbance = {absorbance:F3}");
            result.Data["absorbance"] = absorbance;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Transmittance → Absorbance");
                result.Steps.Add($"%T = {percentT}%");
                result.Steps.Add($"T = {transmittance:F4}");
                result.Steps.Add($"A = -log₁₀(T) = -log₁₀({transmittance:F4}) = {absorbance:F3}");
            }

            return result;
        }

        return ChemResult.Error("Provide either absorbance (A) or transmittance (T)");
    }

    // Анализ смеси веществ (система линейных уравнений)
    public ChemResult MixtureAnalysis(ParsedCommand cmd)
    {
        try
        {
            // Алиасы для смеси
            var p = cmd.Parameters;
            if (!p.ContainsKey("eps1_lambda1") && p.ContainsKey("eps1_1")) p["eps1_lambda1"] = p["eps1_1"];
            if (!p.ContainsKey("eps1_lambda2") && p.ContainsKey("eps1_2")) p["eps1_lambda2"] = p["eps1_2"];
            if (!p.ContainsKey("eps2_lambda1") && p.ContainsKey("eps2_1")) p["eps2_lambda1"] = p["eps2_1"];
            if (!p.ContainsKey("eps2_lambda2") && p.ContainsKey("eps2_2")) p["eps2_lambda2"] = p["eps2_2"];
            if (!p.ContainsKey("pathlength") && p.ContainsKey("l")) p["pathlength"] = p["l"];

            // Для двух компонентов при двух длинах волн:
            // A1 = ε1,λ1·c1·l + ε2,λ1·c2·l
            // A2 = ε1,λ2·c1·l + ε2,λ2·c2·l

            double A1 = double.Parse(cmd.Parameters["A1"], CultureInfo.InvariantCulture); // поглощение на λ1
            double A2 = double.Parse(cmd.Parameters["A2"], CultureInfo.InvariantCulture); // поглощение на λ2
            
            double eps1_lambda1 = double.Parse(cmd.Parameters["eps1_lambda1"], CultureInfo.InvariantCulture);
            double eps1_lambda2 = double.Parse(cmd.Parameters["eps1_lambda2"], CultureInfo.InvariantCulture);
            double eps2_lambda1 = double.Parse(cmd.Parameters["eps2_lambda1"], CultureInfo.InvariantCulture);
            double eps2_lambda2 = double.Parse(cmd.Parameters["eps2_lambda2"], CultureInfo.InvariantCulture);
            
            double pathlength = double.Parse(cmd.Parameters.GetValueOrDefault("pathlength", "1.0"), CultureInfo.InvariantCulture);

            // Решение системы 2x2:
            // c1 = (A1·ε2,λ2 - A2·ε2,λ1) / (ε1,λ1·ε2,λ2 - ε1,λ2·ε2,λ1) / l
            // c2 = (A2·ε1,λ1 - A1·ε1,λ2) / (ε1,λ1·ε2,λ2 - ε1,λ2·ε2,λ1) / l

            double denominator = (eps1_lambda1 * eps2_lambda2 - eps1_lambda2 * eps2_lambda1) * pathlength;
            
            if (Math.Abs(denominator) < 1e-10)
                return ChemResult.Error("System is singular - wavelengths may not be suitable");

            double c1 = (A1 * eps2_lambda2 - A2 * eps2_lambda1) / denominator;
            double c2 = (A2 * eps1_lambda1 - A1 * eps1_lambda2) / denominator;

            if (c1 < 0 || c2 < 0)
            {
                var errorResult = ChemResult.Error("Negative concentration - check input data");
                errorResult.Data["c1"] = c1;
                errorResult.Data["c2"] = c2;
                return errorResult;
            }

            var result = ChemResult.Ok($"c1 = {c1:E3} M, c2 = {c2:E3} M");
            result.Data["c1"] = c1;
            result.Data["c2"] = c2;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Mixture Analysis - Two Components, Two Wavelengths");
                result.Steps.Add($"\nMeasured absorbances:");
                result.Steps.Add($"A(λ1) = {A1:F3}");
                result.Steps.Add($"A(λ2) = {A2:F3}");
                
                result.Steps.Add($"\nMolar absorptivities:");
                result.Steps.Add($"Component 1: ε(λ1) = {eps1_lambda1:E2}, ε(λ2) = {eps1_lambda2:E2}");
                result.Steps.Add($"Component 2: ε(λ1) = {eps2_lambda1:E2}, ε(λ2) = {eps2_lambda2:E2}");
                result.Steps.Add($"Path length: {pathlength} cm");
                
                result.Steps.Add($"\nSystem of equations:");
                result.Steps.Add($"A1 = ε1,λ1·c1·l + ε2,λ1·c2·l");
                result.Steps.Add($"A2 = ε1,λ2·c1·l + ε2,λ2·c2·l");
                
                result.Steps.Add($"\nSolution:");
                result.Steps.Add($"[Component 1] = {c1:E3} M = {c1 * 1000:F3} mM");
                result.Steps.Add($"[Component 2] = {c2:E3} M = {c2 * 1000:F3} mM");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Mixture analysis failed: {ex.Message}");
        }
    }

    // Калибровочная кривая
    public ChemResult CalibrationCurve(ParsedCommand cmd)
    {
        try
        {
            // Алиасы
            var p = cmd.Parameters;
            if (!p.ContainsKey("absorbances") && p.ContainsKey("absorbance")) p["absorbances"] = p["absorbance"];
            if (!p.ContainsKey("concentrations") && p.ContainsKey("concentration")) p["concentrations"] = p["concentration"];

            var concentrations = cmd.Parameters["concentrations"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var absorbances = cmd.Parameters["absorbances"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            if (concentrations.Length != absorbances.Length || concentrations.Length < 3)
                return ChemResult.Error("Need at least 3 data points");

            // Линейная регрессия: A = m·c + b
            var (slope, intercept, r2) = LinearRegression(concentrations, absorbances);

            // slope = ε·l (если концентрация в M и длина в см)
            double epsilon = slope; // если pathlength = 1 cm

            var result = ChemResult.Ok($"Calibration: A = {slope:E3}·c + {intercept:F4}, R² = {r2:F4}");
            result.Data["slope"] = slope;
            result.Data["intercept"] = intercept;
            result.Data["r_squared"] = r2;
            result.Data["epsilon_approx"] = epsilon;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Calibration Curve (Linear Regression)");
                result.Steps.Add($"\nData points: {concentrations.Length}");
                
                result.Steps.Add("\nConc (M)\tAbs");
                for (int i = 0; i < concentrations.Length; i++)
                    result.Steps.Add($"{concentrations[i]:E3}\t{absorbances[i]:F3}");

                result.Steps.Add($"\nLinear fit: A = m·c + b");
                result.Steps.Add($"Slope (m) = {slope:E3}");
                result.Steps.Add($"Intercept (b) = {intercept:F4}");
                result.Steps.Add($"R² = {r2:F4}");

                if (r2 >= 0.99)
                    result.Steps.Add("✓ Excellent linearity (R² ≥ 0.99)");
                else if (r2 >= 0.95)
                    result.Steps.Add("✓ Good linearity (R² ≥ 0.95)");
                else
                    result.Steps.Add("⚠ Poor linearity (R² < 0.95) - check data");

                if (Math.Abs(intercept) > 0.05)
                    result.Steps.Add($"⚠ Non-zero intercept ({intercept:F4}) - possible systematic error");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Calibration curve analysis failed: {ex.Message}");
        }
    }

    // Линейная регрессия
    private (double slope, double intercept, double r2) LinearRegression(double[] x, double[] y)
    {
        int n = x.Length;
        double sumX = x.Sum();
        double sumY = y.Sum();
        double sumXY = x.Zip(y, (a, b) => a * b).Sum();
        double sumX2 = x.Sum(a => a * a);
        double sumY2 = y.Sum(a => a * a);

        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        double intercept = (sumY - slope * sumX) / n;

        double meanY = sumY / n;
        double ssTotal = y.Sum(yi => Math.Pow(yi - meanY, 2));
        double ssResidual = x.Zip(y, (xi, yi) => Math.Pow(yi - (slope * xi + intercept), 2)).Sum();
        double r2 = 1 - (ssResidual / ssTotal);

        return (slope, intercept, r2);
    }
}
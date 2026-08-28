using FractalAgentsAI.Solvers.Chem.Core;
using System.Globalization;

namespace FractalAgentsAI.Solvers.Chem.Processors.Physical;

// ═══════════════════════════════════════════════════════════
// КИНЕТИКА (РАСШИРЕННАЯ)
// ═══════════════════════════════════════════════════════════
public class KineticsCalculator
{
    private readonly VerbosityLevel _verbosity;

    public KineticsCalculator(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    // Расчёт скорости по закону действующих масс
    public ChemResult CalculateRate(ParsedCommand cmd)
    {
        try
        {
            double k = double.Parse(cmd.Parameters["k"], CultureInfo.InvariantCulture); // константа скорости
            var concentrations = cmd.Parameters["concentrations"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var orders = cmd.Parameters["orders"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            if (concentrations.Length != orders.Length)
                return ChemResult.Error("Number of concentrations must match number of orders");

            // v = k·[A]^m·[B]^n·...
            double rate = k;
            for (int i = 0; i < concentrations.Length; i++)
                rate *= Math.Pow(concentrations[i], orders[i]);

            int totalOrder = (int)orders.Sum();

            var result = ChemResult.Ok($"Rate = {rate:E3} M/s");
            result.Data["rate"] = rate;
            result.Data["rate_constant"] = k;
            result.Data["overall_order"] = totalOrder;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Rate Law Calculation");
                result.Steps.Add($"Rate constant k = {k:E3}");
                
                string rateLaw = "v = k";
                for (int i = 0; i < concentrations.Length; i++)
                {
                    rateLaw += $"·[R{i + 1}]";
                    if (orders[i] != 1)
                        rateLaw += $"^{orders[i]}";
                }
                result.Steps.Add($"Rate law: {rateLaw}");
                
                for (int i = 0; i < concentrations.Length; i++)
                    result.Steps.Add($"[R{i + 1}] = {concentrations[i]:E3} M, order = {orders[i]}");
                
                result.Steps.Add($"\nOverall order: {totalOrder}");
                result.Steps.Add($"Rate = {rate:E3} M/s");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Rate calculation failed: {ex.Message}");
        }
    }

    // Расчёт периода полураспада
    public ChemResult CalculateHalfLife(ParsedCommand cmd)
    {
        try
        {
            int order = int.Parse(cmd.Parameters["order"]); // порядок реакции
            double k = double.Parse(cmd.Parameters["k"], CultureInfo.InvariantCulture); // константа скорости
            double C0 = order >= 1 ? double.Parse(cmd.Parameters.GetValueOrDefault("C0", "1.0"), CultureInfo.InvariantCulture) : 1.0;

            double t_half;
            string formula;

            switch (order)
            {
                case 0:
                    // t1/2 = [A]0 / (2k)
                    t_half = C0 / (2 * k);
                    formula = "t₁/₂ = [A]₀/(2k)";
                    break;
                case 1:
                    // t1/2 = ln(2) / k = 0.693 / k
                    t_half = 0.693 / k;
                    formula = "t₁/₂ = 0.693/k";
                    break;
                case 2:
                    // t1/2 = 1 / (k·[A]0)
                    t_half = 1.0 / (k * C0);
                    formula = "t₁/₂ = 1/(k·[A]₀)";
                    break;
                default:
                    return ChemResult.Error($"Half-life formula for order {order} not implemented");
            }

            var result = ChemResult.Ok($"t₁/₂ = {t_half:E3} (time units)");
            result.Data["half_life"] = t_half;
            result.Data["order"] = order;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Half-Life Calculation (Order {order})");
                result.Steps.Add($"Rate constant k = {k:E3}");
                if (order != 1)
                    result.Steps.Add($"Initial concentration [A]₀ = {C0:E3} M");
                result.Steps.Add($"\nFormula: {formula}");
                result.Steps.Add($"t₁/₂ = {t_half:E3}");

                if (order == 1)
                    result.Steps.Add("\n✓ First-order: half-life is independent of concentration");
                else if (order == 0)
                    result.Steps.Add("\n✓ Zero-order: half-life decreases as concentration decreases");
                else if (order == 2)
                    result.Steps.Add("\n✓ Second-order: half-life increases as concentration decreases");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Half-life calculation failed: {ex.Message}");
        }
    }

    // Уравнение Аррениуса
    public ChemResult Arrhenius(ParsedCommand cmd)
    {
        try
        {
            var calcType = cmd.Parameters.GetValueOrDefault("calculate", "k");

            if (calcType == "k")
            {
                // Расчёт константы скорости: k = A·exp(-Ea/RT)
                double A = double.Parse(cmd.Parameters["A"], CultureInfo.InvariantCulture); // предэкспоненциальный множитель
                double Ea = double.Parse(cmd.Parameters["Ea"], CultureInfo.InvariantCulture); // энергия активации, кДж/моль
                double T = double.Parse(cmd.Parameters["T"], CultureInfo.InvariantCulture); // температура, K
                const double R = 8.314; // Дж/(моль·К)

                double k = A * Math.Exp(-Ea * 1000 / (R * T));

                var result = ChemResult.Ok($"k = {k:E3} (rate units)");
                result.Data["k"] = k;
                result.Data["temperature"] = T;

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add("Arrhenius Equation: k = A·exp(-Ea/RT)");
                    result.Steps.Add($"Pre-exponential factor A = {A:E3}");
                    result.Steps.Add($"Activation energy Ea = {Ea} kJ/mol");
                    result.Steps.Add($"Temperature T = {T} K ({T - 273.15:F1}°C)");
                    result.Steps.Add($"Gas constant R = 8.314 J/(mol·K)");
                    result.Steps.Add($"\nk = {A:E3}·exp(-{Ea * 1000}/{R}·{T})");
                    result.Steps.Add($"k = {k:E3}");
                }

                return result;
            }
            else if (calcType == "Ea")
            {
                // Расчёт энергии активации из двух температур
                double k1 = double.Parse(cmd.Parameters["k1"], CultureInfo.InvariantCulture);
                double T1 = double.Parse(cmd.Parameters["T1"], CultureInfo.InvariantCulture); // K
                double k2 = double.Parse(cmd.Parameters["k2"], CultureInfo.InvariantCulture);
                double T2 = double.Parse(cmd.Parameters["T2"], CultureInfo.InvariantCulture); // K
                const double R = 8.314;

                // ln(k2/k1) = -Ea/R · (1/T2 - 1/T1)
                // Ea = -R·ln(k2/k1) / (1/T2 - 1/T1)
                double Ea = -R * Math.Log(k2 / k1) / (1.0 / T2 - 1.0 / T1) / 1000; // кДж/моль

                var result = ChemResult.Ok($"Ea = {Ea:F2} kJ/mol");
                result.Data["Ea"] = Ea;

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add("Calculating Activation Energy from Temperature Dependence");
                    result.Steps.Add($"At T₁ = {T1} K: k₁ = {k1:E3}");
                    result.Steps.Add($"At T₂ = {T2} K: k₂ = {k2:E3}");
                    result.Steps.Add($"\nTwo-point Arrhenius equation:");
                    result.Steps.Add($"ln(k₂/k₁) = -Ea/R·(1/T₂ - 1/T₁)");
                    result.Steps.Add($"ln({k2:E3}/{k1:E3}) = {Math.Log(k2 / k1):F4}");
                    result.Steps.Add($"Ea = {Ea:F2} kJ/mol");
                }

                return result;
            }

            return ChemResult.Error("Specify calculate='k' or calculate='Ea'");
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Arrhenius equation failed: {ex.Message}");
        }
    }

    // Определение порядка реакции из экспериментальных данных
    public ChemResult DetermineOrder(ParsedCommand cmd)
    {
        try
        {
            var method = cmd.Parameters.GetValueOrDefault("method", "graphical");

            if (method == "initial_rates")
            {
                // Метод начальных скоростей
                var concentrations = cmd.Parameters["concentrations"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                var rates = cmd.Parameters["rates"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

                if (concentrations.Length < 2 || rates.Length < 2)
                    return ChemResult.Error("Need at least 2 data points");

                // Для двух точек: r2/r1 = (C2/C1)^n
                // n = log(r2/r1) / log(C2/C1)
                double order = Math.Log(rates[1] / rates[0]) / Math.Log(concentrations[1] / concentrations[0]);

                var result = ChemResult.Ok($"Reaction order n = {order:F2}");
                result.Data["order"] = order;

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.Steps.Add("Method of Initial Rates");
                    result.Steps.Add($"[A]₁ = {concentrations[0]:E3} M, v₁ = {rates[0]:E3} M/s");
                    result.Steps.Add($"[A]₂ = {concentrations[1]:E3} M, v₂ = {rates[1]:E3} M/s");
                    result.Steps.Add($"\nv₂/v₁ = ([A]₂/[A]₁)ⁿ");
                    result.Steps.Add($"{rates[1] / rates[0]:F3} = ({concentrations[1] / concentrations[0]:F3})ⁿ");
                    result.Steps.Add($"n = log({rates[1] / rates[0]:F3}) / log({concentrations[1] / concentrations[0]:F3})");
                    result.Steps.Add($"n = {order:F2}");

                    if (Math.Abs(order - 0) < 0.2)
                        result.Steps.Add("\n✓ Likely zero-order reaction");
                    else if (Math.Abs(order - 1) < 0.2)
                        result.Steps.Add("\n✓ Likely first-order reaction");
                    else if (Math.Abs(order - 2) < 0.2)
                        result.Steps.Add("\n✓ Likely second-order reaction");
                }

                return result;
            }

            return ChemResult.Error("Only 'initial_rates' method implemented");
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Order determination failed: {ex.Message}");
        }
    }

    // Интегрированные уравнения скорости
    public ChemResult IntegratedRateLaw(ParsedCommand cmd)
    {
        try
        {
            int order = int.Parse(cmd.Parameters["order"]);
            double k = double.Parse(cmd.Parameters["k"], CultureInfo.InvariantCulture);
            double C0 = double.Parse(cmd.Parameters["C0"], CultureInfo.InvariantCulture);
            double t = double.Parse(cmd.Parameters["t"], CultureInfo.InvariantCulture);

            double Ct;
            string equation;

            switch (order)
            {
                case 0:
                    // [A] = [A]0 - kt
                    Ct = C0 - k * t;
                    equation = "[A] = [A]₀ - kt";
                    break;
                case 1:
                    // [A] = [A]0·exp(-kt)
                    Ct = C0 * Math.Exp(-k * t);
                    equation = "[A] = [A]₀·e⁻ᵏᵗ";
                    break;
                case 2:
                    // 1/[A] = 1/[A]0 + kt
                    Ct = 1.0 / (1.0 / C0 + k * t);
                    equation = "1/[A] = 1/[A]₀ + kt";
                    break;
                default:
                    return ChemResult.Error($"Order {order} not supported");
            }

            var result = ChemResult.Ok($"[A](t={t}) = {Ct:E3} M");
            result.Data["concentration"] = Ct;
            result.Data["fraction_remaining"] = Ct / C0;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"Integrated Rate Law (Order {order})");
                result.Steps.Add($"Equation: {equation}");
                result.Steps.Add($"k = {k:E3}, [A]₀ = {C0:E3} M, t = {t}");
                result.Steps.Add($"[A](t) = {Ct:E3} M");
                result.Steps.Add($"Fraction remaining: {Ct / C0:F3} ({Ct / C0 * 100:F1}%)");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Integrated rate law calculation failed: {ex.Message}");
        }
    }
}
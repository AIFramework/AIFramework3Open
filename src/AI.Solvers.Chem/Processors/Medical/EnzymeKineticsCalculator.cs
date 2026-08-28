using FractalAgentsAI.Solvers.Chem.Core;
using System.Globalization;

namespace FractalAgentsAI.Solvers.Chem.Processors.Medical;

// ═══════════════════════════════════════════════════════════
// КИНЕТИКА ФЕРМЕНТОВ (МИХАЭЛИС-МЕНТЕН)
// ═══════════════════════════════════════════════════════════
public class EnzymeKineticsCalculator
{
    private readonly VerbosityLevel _verbosity;

    public EnzymeKineticsCalculator(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    // Уравнение Михаэлиса-Ментен: v = (Vmax * [S]) / (Km + [S])
    public ChemResult MichaelisMenten(ParsedCommand cmd)
    {
        try
        {
            double Vmax = double.Parse(cmd.Parameters["Vmax"], CultureInfo.InvariantCulture); // максимальная скорость
            double Km = double.Parse(cmd.Parameters["Km"], CultureInfo.InvariantCulture); // константа Михаэлиса
            double S = double.Parse(cmd.Parameters["S"], CultureInfo.InvariantCulture); // концентрация субстрата

            // v = (Vmax * [S]) / (Km + [S])
            double v = (Vmax * S) / (Km + S);

            // Процент от Vmax
            double percentVmax = (v / Vmax) * 100;

            var result = ChemResult.Ok($"v = {v:F3} (units), {percentVmax:F1}% of Vmax");
            result.Data["velocity"] = v;
            result.Data["Vmax"] = Vmax;
            result.Data["Km"] = Km;
            result.Data["substrate_conc"] = S;
            result.Data["percent_Vmax"] = percentVmax;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Michaelis-Menten Kinetics");
                result.Steps.Add($"Vmax = {Vmax:F3} (maximum velocity)");
                result.Steps.Add($"Km = {Km:F3} M (Michaelis constant)");
                result.Steps.Add($"[S] = {S:F3} M (substrate concentration)");
                result.Steps.Add($"\nEquation: v = (Vmax·[S]) / (Km + [S])");
                result.Steps.Add($"v = ({Vmax}·{S}) / ({Km} + {S})");
                result.Steps.Add($"v = {v:F3}");
                result.Steps.Add($"\nVelocity is {percentVmax:F1}% of Vmax");

                // Интерпретация Km
                result.Steps.Add($"\nInterpretation of Km:");
                if (S < Km)
                    result.Steps.Add($"[S] < Km: First-order kinetics (v ≈ {v:F3})");
                else if (S > 10 * Km)
                    result.Steps.Add($"[S] >> Km: Zero-order kinetics (v ≈ Vmax = {Vmax:F3})");
                else if (Math.Abs(S - Km) < 0.1 * Km)
                    result.Steps.Add($"[S] ≈ Km: v = Vmax/2 = {Vmax / 2:F3}");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Michaelis-Menten calculation failed: {ex.Message}");
        }
    }

    // Определение Km и Vmax из экспериментальных данных (Lineweaver-Burk)
    public ChemResult LineweaverBurk(ParsedCommand cmd)
    {
        try
        {
            // Ожидаем массивы данных [S] и v
            var substrateData = cmd.Parameters["substrate"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var velocityData = cmd.Parameters["velocity"].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            if (substrateData.Length != velocityData.Length || substrateData.Length < 3)
                return ChemResult.Error("Need at least 3 data points with matching S and v values");

            // Двойная обратная форма: 1/v = (Km/Vmax)·(1/[S]) + 1/Vmax
            // y = mx + b, где y = 1/v, x = 1/[S], m = Km/Vmax, b = 1/Vmax

            var x = substrateData.Select(s => 1.0 / s).ToArray();
            var y = velocityData.Select(v => 1.0 / v).ToArray();

            // Линейная регрессия
            var (slope, intercept, r2) = LinearRegression(x, y);

            double Vmax = 1.0 / intercept;
            double Km = slope * Vmax;

            var result = ChemResult.Ok($"Vmax = {Vmax:F3}, Km = {Km:F3} M");
            result.Data["Vmax"] = Vmax;
            result.Data["Km"] = Km;
            result.Data["R_squared"] = r2;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Lineweaver-Burk Plot (Double Reciprocal)");
                result.Steps.Add("Equation: 1/v = (Km/Vmax)·(1/[S]) + 1/Vmax");
                result.Steps.Add($"\nData points: {substrateData.Length}");
                
                result.Steps.Add("\n[S] (M)\tv (units)");
                for (int i = 0; i < substrateData.Length; i++)
                    result.Steps.Add($"{substrateData[i]:F4}\t{velocityData[i]:F3}");

                result.Steps.Add($"\nLinear regression (1/v vs 1/[S]):");
                result.Steps.Add($"Slope (m) = Km/Vmax = {slope:F4}");
                result.Steps.Add($"Intercept (b) = 1/Vmax = {intercept:F4}");
                result.Steps.Add($"R² = {r2:F4}");

                result.Steps.Add($"\nDerived parameters:");
                result.Steps.Add($"Vmax = 1/intercept = {Vmax:F3}");
                result.Steps.Add($"Km = slope × Vmax = {Km:F3} M");

                if (r2 < 0.95)
                    result.Steps.Add($"\n⚠ Warning: Low R² ({r2:F3}) - data may not fit Michaelis-Menten model");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Lineweaver-Burk analysis failed: {ex.Message}");
        }
    }

    // Конкурентное ингибирование
    public ChemResult CompetitiveInhibition(ParsedCommand cmd)
    {
        try
        {
            double Vmax = double.Parse(cmd.Parameters["Vmax"], CultureInfo.InvariantCulture);
            double Km = double.Parse(cmd.Parameters["Km"], CultureInfo.InvariantCulture);
            double S = double.Parse(cmd.Parameters["S"], CultureInfo.InvariantCulture);
            double I = double.Parse(cmd.Parameters["I"], CultureInfo.InvariantCulture); // концентрация ингибитора
            double Ki = double.Parse(cmd.Parameters["Ki"], CultureInfo.InvariantCulture); // константа ингибирования

            // Конкурентное ингибирование: v = Vmax·[S] / (Km·(1 + [I]/Ki) + [S])
            // Эффект: Km увеличивается, Vmax не изменяется
            double Km_apparent = Km * (1 + I / Ki);
            double v = (Vmax * S) / (Km_apparent + S);

            // Скорость без ингибитора для сравнения
            double v_uninhibited = (Vmax * S) / (Km + S);
            double inhibitionPercent = ((v_uninhibited - v) / v_uninhibited) * 100;

            var result = ChemResult.Ok($"v = {v:F3} ({inhibitionPercent:F1}% inhibition)");
            result.Data["velocity_inhibited"] = v;
            result.Data["velocity_uninhibited"] = v_uninhibited;
            result.Data["Km_apparent"] = Km_apparent;
            result.Data["inhibition_percent"] = inhibitionPercent;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Competitive Inhibition");
                result.Steps.Add($"Vmax = {Vmax:F3}");
                result.Steps.Add($"Km = {Km:F3} M");
                result.Steps.Add($"[S] = {S:F3} M");
                result.Steps.Add($"[I] = {I:F3} M (inhibitor)");
                result.Steps.Add($"Ki = {Ki:F3} M (inhibition constant)");
                
                result.Steps.Add($"\nApparent Km:");
                result.Steps.Add($"Km' = Km·(1 + [I]/Ki) = {Km}·(1 + {I}/{Ki}) = {Km_apparent:F3} M");
                
                result.Steps.Add($"\nVelocity with inhibitor:");
                result.Steps.Add($"v = Vmax·[S] / (Km' + [S]) = {v:F3}");
                
                result.Steps.Add($"\nWithout inhibitor: v = {v_uninhibited:F3}");
                result.Steps.Add($"Inhibition: {inhibitionPercent:F1}%");
                
                result.Steps.Add($"\n✓ Competitive inhibition increases Km but does not affect Vmax");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Competitive inhibition calculation failed: {ex.Message}");
        }
    }

    // Неконкурентное ингибирование
    public ChemResult NonCompetitiveInhibition(ParsedCommand cmd)
    {
        try
        {
            double Vmax = double.Parse(cmd.Parameters["Vmax"], CultureInfo.InvariantCulture);
            double Km = double.Parse(cmd.Parameters["Km"], CultureInfo.InvariantCulture);
            double S = double.Parse(cmd.Parameters["S"], CultureInfo.InvariantCulture);
            double I = double.Parse(cmd.Parameters["I"], CultureInfo.InvariantCulture);
            double Ki = double.Parse(cmd.Parameters["Ki"], CultureInfo.InvariantCulture);

            // Неконкурентное ингибирование: v = (Vmax·[S]) / ((1 + [I]/Ki)·(Km + [S]))
            // Эффект: Vmax уменьшается, Km не изменяется
            double Vmax_apparent = Vmax / (1 + I / Ki);
            double v = (Vmax_apparent * S) / (Km + S);

            double v_uninhibited = (Vmax * S) / (Km + S);
            double inhibitionPercent = ((v_uninhibited - v) / v_uninhibited) * 100;

            var result = ChemResult.Ok($"v = {v:F3} ({inhibitionPercent:F1}% inhibition)");
            result.Data["velocity_inhibited"] = v;
            result.Data["Vmax_apparent"] = Vmax_apparent;
            result.Data["inhibition_percent"] = inhibitionPercent;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Non-Competitive Inhibition");
                result.Steps.Add($"Vmax = {Vmax:F3}");
                result.Steps.Add($"Km = {Km:F3} M (unchanged)");
                result.Steps.Add($"[I] = {I:F3} M, Ki = {Ki:F3} M");
                
                result.Steps.Add($"\nApparent Vmax:");
                result.Steps.Add($"Vmax' = Vmax / (1 + [I]/Ki) = {Vmax_apparent:F3}");
                
                result.Steps.Add($"\nv = {v:F3}");
                result.Steps.Add($"Inhibition: {inhibitionPercent:F1}%");
                
                result.Steps.Add($"\n✓ Non-competitive inhibition decreases Vmax but does not affect Km");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Non-competitive inhibition calculation failed: {ex.Message}");
        }
    }

    // Расчёт удельной активности фермента
    public ChemResult CalculateSpecificActivity(ParsedCommand cmd)
    {
        try
        {
            double enzymeActivity = double.Parse(cmd.Parameters["activity"], CultureInfo.InvariantCulture); // units
            double proteinConc = double.Parse(cmd.Parameters["protein"], CultureInfo.InvariantCulture); // mg

            // Удельная активность = units / mg protein
            double specificActivity = enzymeActivity / proteinConc;

            var result = ChemResult.Ok($"Specific Activity = {specificActivity:F2} units/mg protein");
            result.Data["specific_activity"] = specificActivity;

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add("Specific Activity Calculation");
                result.Steps.Add($"Enzyme activity = {enzymeActivity} units");
                result.Steps.Add($"Protein concentration = {proteinConc} mg");
                result.Steps.Add($"Specific activity = {enzymeActivity}/{proteinConc} = {specificActivity:F2} units/mg");
                result.Steps.Add($"\n✓ Higher specific activity indicates purer enzyme");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Specific activity calculation failed: {ex.Message}");
        }
    }

    // Линейная регрессия (вспомогательный метод)
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

        // R²
        double meanY = sumY / n;
        double ssTotal = y.Sum(yi => Math.Pow(yi - meanY, 2));
        double ssResidual = x.Zip(y, (xi, yi) => Math.Pow(yi - (slope * xi + intercept), 2)).Sum();
        double r2 = 1 - (ssResidual / ssTotal);

        return (slope, intercept, r2);
    }
}
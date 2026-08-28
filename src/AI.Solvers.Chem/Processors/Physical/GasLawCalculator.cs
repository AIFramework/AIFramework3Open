using FractalAgentsAI.Solvers.Chem.Core;

namespace FractalAgentsAI.Solvers.Chem.Processors.Physical;

// ═══════════════════════════════════════════════════════════
// ГАЗОВЫЕ ЗАКОНЫ
// ═══════════════════════════════════════════════════════════
public class GasLawCalculator
{
    private readonly VerbosityLevel _verbosity;
    private const double R = 0.0821; // L·atm/(mol·K)

    public GasLawCalculator(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    public ChemResult IdealGasLaw(ParsedCommand cmd)
    {
        try
        {
            // PV = nRT
            double? P = cmd.Parameters.ContainsKey("P") ? double.Parse(cmd.Parameters["P"]) : null;
            double? V = cmd.Parameters.ContainsKey("V") ? double.Parse(cmd.Parameters["V"]) : null;
            double? n = cmd.Parameters.ContainsKey("N") ? double.Parse(cmd.Parameters["N"]) : null;
            double? T = cmd.Parameters.ContainsKey("T") ? double.Parse(cmd.Parameters["T"]) : null;

            var find = cmd.Parameters.ContainsKey("find") ? cmd.Parameters["find"] : "";

            double result = 0;
            string variable = "";

            if (string.IsNullOrEmpty(find))
            {
                // Определяем, что искать (что не задано)
                if (!P.HasValue) { find = "P"; }
                else if (!V.HasValue) { find = "V"; }
                else if (!n.HasValue) { find = "N"; }
                else if (!T.HasValue) { find = "T"; }
            }

            switch (find.ToUpper())
            {
                case "P":
                    if (!n.HasValue || !T.HasValue || !V.HasValue)
                        return ChemResult.Error("Insufficient parameters to calculate P");
                    result = n.Value * R * T.Value / V.Value;
                    variable = "P (pressure)";
                    break;

                case "V":
                    if (!n.HasValue || !T.HasValue || !P.HasValue)
                        return ChemResult.Error("Insufficient parameters to calculate V");
                    result = n.Value * R * T.Value / P.Value;
                    variable = "V (volume)";
                    break;

                case "N":
                    if (!P.HasValue || !V.HasValue || !T.HasValue)
                        return ChemResult.Error("Insufficient parameters to calculate n");
                    result = P.Value * V.Value / (R * T.Value);
                    variable = "n (moles)";
                    break;

                case "T":
                    if (!P.HasValue || !V.HasValue || !n.HasValue)
                        return ChemResult.Error("Insufficient parameters to calculate T");
                    result = P.Value * V.Value / (n.Value * R);
                    variable = "T (temperature)";
                    break;

                default:
                    return ChemResult.Error("Unknown variable to find");
            }

            var chemResult = ChemResult.Ok($"{variable} = {result:F3}");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                chemResult.Steps.Add("Ideal Gas Law: PV = nRT");
                chemResult.Steps.Add($"R = {R} L·atm/(mol·K)");
                chemResult.Steps.Add("Given:");
                if (P.HasValue) chemResult.Steps.Add($"  P = {P.Value:F2} atm");
                if (V.HasValue) chemResult.Steps.Add($"  V = {V.Value:F2} L");
                if (n.HasValue) chemResult.Steps.Add($"  n = {n.Value:F4} mol");
                if (T.HasValue) chemResult.Steps.Add($"  T = {T.Value:F2} K");
                chemResult.Steps.Add($"Solving for {variable}:");
                chemResult.Steps.Add($"  {variable} = {result:F3}");
            }

            chemResult.Data[find] = result;

            return chemResult;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Ideal gas law calculation failed: {ex.Message}");
        }
    }

    public ChemResult CombinedGasLaw(ParsedCommand cmd)
    {
        // P1*V1/T1 = P2*V2/T2
        return ChemResult.Error("Combined gas law not yet implemented");
    }

    public ChemResult DaltonLaw(ParsedCommand cmd)
    {
        // Закон Дальтона для парциальных давлений
        return ChemResult.Error("Dalton's law not yet implemented");
    }
}

// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:


using FractalAgentsAI.Solvers.Chem.Database;
using System.Text.RegularExpressions;

namespace FractalAgentsAI.Solvers.Chem;

public class MolecularFormula
{
    public string Formula { get; }
    public Dictionary<string, int> Elements { get; }

    public MolecularFormula(string formula)
    {
        Formula = formula.Trim();
        Elements = ParseFormula(Formula);
    }

    private Dictionary<string, int> ParseFormula(string formula)
    {
        var elements = new Dictionary<string, int>();
        var regex = new Regex(@"([A-Z][a-z]?)(\d*)");

        foreach (Match match in regex.Matches(formula))
        {
            string element = match.Groups[1].Value;
            int count = string.IsNullOrEmpty(match.Groups[2].Value)
                ? 1
                : int.Parse(match.Groups[2].Value);

            if (elements.ContainsKey(element))
                elements[element] += count;
            else
                elements[element] = count;
        }

        return elements;
    }

    public double CalculateMolarMass(ChemDatabase database)
    {
        double mass = 0;
        foreach (var kvp in Elements)
        {
            var element = database.GetElement(kvp.Key);
            if (element != null)
                mass += element.AtomicMass * kvp.Value;
        }
        return mass;
    }
}

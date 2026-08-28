using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using System.Text;

namespace FractalAgentsAI.Solvers.Chem;

// ═══════════════════════════════════════════════════════════
// ОКИСЛИТЕЛЬНО-ВОССТАНОВИТЕЛЬНЫЕ РЕАКЦИИ
// ═══════════════════════════════════════════════════════════
public class RedoxProcessor
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    // Правила определения степеней окисления
    private readonly Dictionary<string, int> _fixedOxidationStates = new()
    {
        ["H"] = 1,   // в большинстве соединений
        ["O"] = -2,  // в большинстве соединений
        ["F"] = -1,  // всегда
        ["Na"] = 1,
        ["K"] = 1,
        ["Li"] = 1,
        ["Rb"] = 1,
        ["Cs"] = 1,
        ["Mg"] = 2,
        ["Ca"] = 2,
        ["Sr"] = 2,
        ["Ba"] = 2,
        ["Al"] = 3,
        ["Zn"] = 2,
        ["Ag"] = 1
    };

    public RedoxProcessor(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult FindOxidationStates(ParsedCommand cmd)
    {
        try
        {
            var formula = cmd.Parameters.ContainsKey("formula")
                ? cmd.Parameters["formula"]
                : cmd.Parameters["reactants"];

            var molecular = new MolecularFormula(formula);
            var oxidationStates = CalculateOxidationStates(molecular);

            var result = new StringBuilder();
            result.AppendLine($"Oxidation states in {formula}:");

            foreach (var kvp in oxidationStates)
            {
                var sign = kvp.Value >= 0 ? "+" : "";
                result.AppendLine($"  {kvp.Key}: {sign}{kvp.Value}");
            }

            return ChemResult.Ok(result.ToString());
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Oxidation state calculation failed: {ex.Message}");
        }
    }

    private Dictionary<string, int> CalculateOxidationStates(MolecularFormula formula)
    {
        var states = new Dictionary<string, int>();

        // Простые случаи
        if (formula.Elements.Count == 1)
        {
            var element = formula.Elements.Keys.First();
            states[element] = 0; // Простое вещество
            return states;
        }

        // Применяем известные правила
        int totalCharge = 0;
        var unknownElements = new List<string>();

        foreach (var kvp in formula.Elements)
        {
            var element = kvp.Key;
            var count = kvp.Value;

            if (_fixedOxidationStates.ContainsKey(element))
            {
                var state = _fixedOxidationStates[element];

                // Исключения
                if (element == "H" && formula.Elements.ContainsKey("O") &&
                    formula.Elements.ContainsKey("Na"))
                {
                    // В NaH водород -1
                    state = -1;
                }
                else if (element == "O" && formula.Elements.ContainsKey("F"))
                {
                    // В OF2 кислород положительный
                    state = 2;
                }

                states[element] = state;
                totalCharge += state * count;
            }
            else
            {
                unknownElements.Add(element);
            }
        }

        // Определяем неизвестные элементы
        if (unknownElements.Count == 1)
        {
            var element = unknownElements[0];
            var count = formula.Elements[element];
            var oxidationState = -totalCharge / count;
            states[element] = oxidationState;
        }
        else if (unknownElements.Count > 1)
        {
            // Сложный случай - используем типичные степени окисления
            foreach (var element in unknownElements)
            {
                var elementData = _database.GetElement(element);
                if (elementData != null && elementData.OxidationStates.Length > 0)
                {
                    // Берем наиболее распространенную
                    states[element] = elementData.OxidationStates[0];
                }
                else
                {
                    states[element] = 0;
                }
            }
        }

        return states;
    }

    public ChemResult BalanceRedox(ParsedCommand cmd)
    {
        try
        {
            var reactants = cmd.Parameters["reactants"].Split('+').Select(s => s.Trim()).ToList();
            var products = cmd.Parameters["products"].Split('+').Select(s => s.Trim()).ToList();
            var medium = cmd.Parameters.GetValueOrDefault("medium", "acidic");

            var result = new StringBuilder();
            result.AppendLine($"Redox Balance in {medium} medium");
            result.AppendLine("──────────────────────────────────────────────────");

            // 1. Перманганат (MnO4-)
            if (reactants.Any(r => r.Contains("MnO4") || r.Contains("KMnO4")))
            {
                if (medium == "acidic")
                {
                    result.AppendLine("Half-reaction (Reduction):");
                    result.AppendLine("  MnO₄⁻ + 8H⁺ + 5e⁻ → Mn²⁺ + 4H₂O");
                    
                    if (reactants.Any(r => r.Contains("Fe") || r.Contains("Fe2+")))
                    {
                        result.AppendLine("Half-reaction (Oxidation):");
                        result.AppendLine("  Fe²⁺ → Fe³⁺ + e⁻  (×5)");
                        result.AppendLine();
                        result.AppendLine("Balanced Equation:");
                        result.AppendLine("  MnO₄⁻ + 5Fe²⁺ + 8H⁺ → Mn²⁺ + 5Fe³⁺ + 4H₂O");
                    }
                    else if (reactants.Any(r => r.Contains("SO3") || r.Contains("H2SO3")))
                    {
                        result.AppendLine("Half-reaction (Oxidation):");
                        result.AppendLine("  SO₃²⁻ + H₂O → SO₄²⁻ + 2H⁺ + 2e⁻");
                        result.AppendLine();
                        result.AppendLine("Balanced Equation:");
                        result.AppendLine("  2MnO₄⁻ + 5SO₃²⁻ + 6H⁺ → 2Mn²⁺ + 5SO₄²⁻ + 3H₂O");
                    }
                    else
                    {
                        result.AppendLine("Second half-reaction not identified. Showing general reduction of MnO4-.");
                    }
                }
                else // basic or neutral
                {
                    result.AppendLine("Half-reaction (Reduction in basic/neutral):");
                    result.AppendLine("  MnO₄⁻ + 2H₂O + 3e⁻ → MnO₂ + 4OH⁻");
                }
                return ChemResult.Ok(result.ToString());
            }
            
            // 2. Дихромат (Cr2O7^2-)
            if (reactants.Any(r => r.Contains("Cr2O7") || r.Contains("K2Cr2O7")))
            {
                result.AppendLine("Half-reaction (Reduction):");
                result.AppendLine("  Cr₂O₇²⁻ + 14H⁺ + 6e⁻ → 2Cr³⁺ + 7H₂O");
                
                if (reactants.Any(r => r.Contains("Fe")))
                {
                    result.AppendLine("Half-reaction (Oxidation):");
                    result.AppendLine("  Fe²⁺ → Fe³⁺ + e⁻  (×6)");
                    result.AppendLine();
                    result.AppendLine("Balanced Equation:");
                    result.AppendLine("  Cr₂O₇²⁻ + 6Fe²⁺ + 14H⁺ → 2Cr³⁺ + 6Fe³⁺ + 7H₂O");
                }
                else if (reactants.Any(r => r.Contains("I-") || r.Contains("KI")))
                {
                    result.AppendLine("Half-reaction (Oxidation):");
                    result.AppendLine("  2I⁻ → I₂ + 2e⁻  (×3)");
                    result.AppendLine();
                    result.AppendLine("Balanced Equation:");
                    result.AppendLine("  Cr₂O₇²⁻ + 6I⁻ + 14H⁺ → 2Cr³⁺ + 3I₂ + 7H₂O");
                }
                
                return ChemResult.Ok(result.ToString());
            }

            return ChemResult.Error("Automatic redox balancing is currently implemented for common oxidizers (KMnO4, K2Cr2O7). General solver is under development.");
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Redox balancing failed: {ex.Message}");
        }
    }
}
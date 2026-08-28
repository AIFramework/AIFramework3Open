// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

using FractalAgentsAI.Solvers.Chem.Core;

namespace FractalAgentsAI.Solvers.Chem;

// ГЛАВНЫЙ ИНТЕРФЕЙС ДВИЖКА
public interface IChemEngine
{
    ChemResult Execute(string command);
    Task<ChemResult> ExecuteAsync(string command);
    void SetVerbosity(VerbosityLevel level);
    void LoadCustomDatabase(string jsonPath);
    void LoadReactionRules(string rulesPath);
}

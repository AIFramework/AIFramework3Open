// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

using AI.Solvers.Chem.Core;

namespace AI.Solvers.Chem.Core;

// ГЛАВНЫЙ ИНТЕРФЕЙС ДВИЖКА
public interface IChemEngine
{
    ChemResult Execute(string command);
    Task<ChemResult> ExecuteAsync(string command);
    void SetVerbosity(VerbosityLevel level);
    void LoadCustomDatabase(string jsonPath);
    void LoadReactionRules(string rulesPath);
}

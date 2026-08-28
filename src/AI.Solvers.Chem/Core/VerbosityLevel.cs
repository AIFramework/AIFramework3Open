// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

namespace AI.Solvers.Chem.Core;

public enum VerbosityLevel
{
    Silent,      // только результат
    Normal,      // результат + основные шаги
    Detailed,    // подробное объяснение
    Debug        // все промежуточные вычисления
}

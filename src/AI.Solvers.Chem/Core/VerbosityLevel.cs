// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

namespace FractalAgentsAI.Solvers.Chem;

public enum VerbosityLevel
{
    Silent,      // только результат
    Normal,      // результат + основные шаги
    Detailed,    // подробное объяснение
    Debug        // все промежуточные вычисления
}

// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

using FractalAgentsAI.Solvers.Chem.Parsing;

namespace FractalAgentsAI.Solvers.Chem;

public class ParsedCommand
{
    public bool Success { get; set; }
    public CommandType CommandType { get; set; }
    public string OriginalCommand { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string ErrorMessage { get; set; }

    public static ParsedCommand Error(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}

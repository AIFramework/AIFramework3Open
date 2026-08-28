// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

namespace AI.Solvers.Chem.Core;

// РЕЗУЛЬТАТ ВЫПОЛНЕНИЯ

public class ChemResult
{
    public bool Success { get; set; }
    public string Result { get; set; }
    public string DetailedExplanation { get; set; }
    public List<string> Steps { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
    public ChemVisualization Visualization { get; set; }
    public string ErrorMessage { get; set; }

    public static ChemResult Error(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        Result = $"Error: {message}"
    };

    public static ChemResult Ok(string result) => new()
    {
        Success = true,
        Result = result
    };
}

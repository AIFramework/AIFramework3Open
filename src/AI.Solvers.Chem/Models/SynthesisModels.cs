namespace FractalAgentsAI.Solvers.Chem.Models;

/// <summary>
/// Представляет целевое соединение для синтеза
/// </summary>
public class TargetCompound
{
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public string Formula { get; set; } = string.Empty;
    public string SMILES { get; set; } = string.Empty;
    public string IUPAC { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SynthesisRoute> Routes { get; set; } = new();
}

/// <summary>
/// Представляет маршрут синтеза
/// </summary>
public class SynthesisRoute
{
    public string StartingMaterial { get; set; } = string.Empty;
    public string RouteType { get; set; } = string.Empty; // "industrial", "laboratory", "classic"
    public string Difficulty { get; set; } = string.Empty; // "easy", "medium", "hard"
    public string Yield { get; set; } = string.Empty; // "40-50%"
    public int StepCount { get; set; }
    public List<SynthesisStep> Steps { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Представляет один шаг синтеза
/// </summary>
public class SynthesisStep
{
    public int StepNumber { get; set; }
    public string ReactionType { get; set; } = string.Empty; // "nitration", "reduction", etc.
    public string Description { get; set; } = string.Empty;
    public string Equation { get; set; } = string.Empty; // "Benzene + HNO3 → Nitrobenzene"
    public List<string> Reagents { get; set; } = new();
    public List<string> Conditions { get; set; } = new();
    public string Catalyst { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string Pressure { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Yield { get; set; } = string.Empty;
    public string Mechanism { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// База данных синтезов
/// </summary>
public class SynthesisDatabase
{
    public string Version { get; set; } = "1.0";
    public string LastUpdated { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public List<TargetCompound> Compounds { get; set; } = new();
}


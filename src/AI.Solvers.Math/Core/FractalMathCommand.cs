namespace AI.Solvers.Math.Core;

public class FractalMathCommand
{
    public CommandType Type { get; set; }
    public string Expression { get; set; } = "";
    public string Variable { get; set; } = "x";
    public string Variable2 { get; set; } = "y";
    public double? LowerBound { get; set; }
    public double? UpperBound { get; set; }
    public int Order { get; set; } = 1;
    public Dictionary<string, string> InitialConditions { get; set; } = new();
    public string LimitPoint { get; set; } = "";
    public string EquationType { get; set; } = "";
    public List<string> Equations { get; set; } = new();
}


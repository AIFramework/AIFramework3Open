namespace FractalAgentsAI.Solvers.Chem.Models;

public class Compound
{
    public string Formula { get; set; }
    public string CommonName { get; set; }
    public string SMILES { get; set; }
    public double MolarMass { get; set; }
    public PhysicalProperties Properties { get; set; }
}

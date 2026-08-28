// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:


using System;
using System.Collections.Generic;
using System.Linq;
using NCDK;
using NCDK.Smiles;
namespace FractalAgentsAI.Solvers.Chem.Models;

public class Element
{
    public string Symbol { get; set; }
    public string Name { get; set; }
    public int AtomicNumber { get; set; }
    public double AtomicMass { get; set; }
    public double Electronegativity { get; set; }
    public int[] OxidationStates { get; set; }
    public int Group { get; set; }
    public int Period { get; set; }
    public string ElectronConfiguration { get; set; }
    public double IonizationEnergy { get; set; }
    public double AtomicRadius { get; set; }
    public double ElectronAffinity { get; set; }
    public string Block { get; set; } // s, p, d, f
}

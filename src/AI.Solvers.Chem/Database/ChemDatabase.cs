// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:

using FractalAgentsAI.Solvers.Chem.Models;

namespace FractalAgentsAI.Solvers.Chem.Database;

public partial class ChemDatabase
{
    private Dictionary<string, Element> _elements;
    private Dictionary<string, Compound> _compounds;
    private Dictionary<string, double> _standardEnthalpies;
    private Dictionary<string, double> _standardPotentials;



    public void Initialize()
    {
        InitializeElements();
        InitializeCommonCompounds();
        InitializeThermodynamicData();
        InitializeElectrochemicalData();
    }


    private void InitializeElements()
    {
        _elements = new Dictionary<string, Element>
        {
            // Период 1
            ["H"] = new Element
            {
                Symbol = "H",
                Name = "Hydrogen",
                AtomicNumber = 1,
                AtomicMass = 1.008,
                Electronegativity = 2.20,
                OxidationStates = new[] { -1, 1 },
                Group = 1,
                Period = 1,
                ElectronConfiguration = "1s¹",
                IonizationEnergy = 1312.0,
                AtomicRadius = 37,
                ElectronAffinity = 72.8,
                Block = "s"
            },
            ["He"] = new Element
            {
                Symbol = "He",
                Name = "Helium",
                AtomicNumber = 2,
                AtomicMass = 4.003,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 1,
                ElectronConfiguration = "1s²",
                IonizationEnergy = 2372.3,
                AtomicRadius = 32,
                Block = "s"
            },

            // Период 2
            ["Li"] = new Element
            {
                Symbol = "Li",
                Name = "Lithium",
                AtomicNumber = 3,
                AtomicMass = 6.94,
                Electronegativity = 0.98,
                OxidationStates = new[] { 1 },
                Group = 1,
                Period = 2,
                ElectronConfiguration = "1s² 2s¹",
                IonizationEnergy = 520.2,
                AtomicRadius = 152,
                ElectronAffinity = 59.6,
                Block = "s"
            },
            ["Be"] = new Element
            {
                Symbol = "Be",
                Name = "Beryllium",
                AtomicNumber = 4,
                AtomicMass = 9.012,
                Electronegativity = 1.57,
                OxidationStates = new[] { 2 },
                Group = 2,
                Period = 2,
                ElectronConfiguration = "1s² 2s²",
                IonizationEnergy = 899.5,
                AtomicRadius = 112,
                Block = "s"
            },
            ["B"] = new Element
            {
                Symbol = "B",
                Name = "Boron",
                AtomicNumber = 5,
                AtomicMass = 10.81,
                Electronegativity = 2.04,
                OxidationStates = new[] { 3 },
                Group = 13,
                Period = 2,
                ElectronConfiguration = "1s² 2s² 2p¹",
                IonizationEnergy = 800.6,
                AtomicRadius = 85,
                ElectronAffinity = 26.7,
                Block = "p"
            },
            ["C"] = new Element
            {
                Symbol = "C",
                Name = "Carbon",
                AtomicNumber = 6,
                AtomicMass = 12.011,
                Electronegativity = 2.55,
                OxidationStates = new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4 },
                Group = 14,
                Period = 2,
                ElectronConfiguration = "1s² 2s² 2p²",
                IonizationEnergy = 1086.5,
                AtomicRadius = 77,
                ElectronAffinity = 153.9,
                Block = "p"
            },
            ["N"] = new Element
            {
                Symbol = "N",
                Name = "Nitrogen",
                AtomicNumber = 7,
                AtomicMass = 14.007,
                Electronegativity = 3.04,
                OxidationStates = new[] { -3, -2, -1, 1, 2, 3, 4, 5 },
                Group = 15,
                Period = 2,
                ElectronConfiguration = "1s² 2s² 2p³",
                IonizationEnergy = 1402.3,
                AtomicRadius = 75,
                ElectronAffinity = 7.0,
                Block = "p"
            },
            ["O"] = new Element
            {
                Symbol = "O",
                Name = "Oxygen",
                AtomicNumber = 8,
                AtomicMass = 15.999,
                Electronegativity = 3.44,
                OxidationStates = new[] { -2, -1, 0 },
                Group = 16,
                Period = 2,
                ElectronConfiguration = "1s² 2s² 2p⁴",
                IonizationEnergy = 1313.9,
                AtomicRadius = 73,
                ElectronAffinity = 141.0,
                Block = "p"
            },
            ["F"] = new Element
            {
                Symbol = "F",
                Name = "Fluorine",
                AtomicNumber = 9,
                AtomicMass = 18.998,
                Electronegativity = 3.98,
                OxidationStates = new[] { -1 },
                Group = 17,
                Period = 2,
                ElectronConfiguration = "1s² 2s² 2p⁵",
                IonizationEnergy = 1681.0,
                AtomicRadius = 71,
                ElectronAffinity = 328.0,
                Block = "p"
            },
            ["Ne"] = new Element
            {
                Symbol = "Ne",
                Name = "Neon",
                AtomicNumber = 10,
                AtomicMass = 20.180,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 2,
                ElectronConfiguration = "1s² 2s² 2p⁶",
                IonizationEnergy = 2080.7,
                AtomicRadius = 69,
                Block = "p"
            },

            // Период 3
            ["Na"] = new Element
            {
                Symbol = "Na",
                Name = "Sodium",
                AtomicNumber = 11,
                AtomicMass = 22.990,
                Electronegativity = 0.93,
                OxidationStates = new[] { 1 },
                Group = 1,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s¹",
                IonizationEnergy = 495.8,
                AtomicRadius = 186,
                ElectronAffinity = 52.8,
                Block = "s"
            },
            ["Mg"] = new Element
            {
                Symbol = "Mg",
                Name = "Magnesium",
                AtomicNumber = 12,
                AtomicMass = 24.305,
                Electronegativity = 1.31,
                OxidationStates = new[] { 2 },
                Group = 2,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s²",
                IonizationEnergy = 737.7,
                AtomicRadius = 160,
                Block = "s"
            },
            ["Al"] = new Element
            {
                Symbol = "Al",
                Name = "Aluminum",
                AtomicNumber = 13,
                AtomicMass = 26.982,
                Electronegativity = 1.61,
                OxidationStates = new[] { 3 },
                Group = 13,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s² 3p¹",
                IonizationEnergy = 577.5,
                AtomicRadius = 143,
                ElectronAffinity = 42.5,
                Block = "p"
            },
            ["Si"] = new Element
            {
                Symbol = "Si",
                Name = "Silicon",
                AtomicNumber = 14,
                AtomicMass = 28.085,
                Electronegativity = 1.90,
                OxidationStates = new[] { -4, 4 },
                Group = 14,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s² 3p²",
                IonizationEnergy = 786.5,
                AtomicRadius = 118,
                ElectronAffinity = 133.6,
                Block = "p"
            },
            ["P"] = new Element
            {
                Symbol = "P",
                Name = "Phosphorus",
                AtomicNumber = 15,
                AtomicMass = 30.974,
                Electronegativity = 2.19,
                OxidationStates = new[] { -3, 3, 5 },
                Group = 15,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s² 3p³",
                IonizationEnergy = 1011.8,
                AtomicRadius = 110,
                ElectronAffinity = 72.0,
                Block = "p"
            },
            ["S"] = new Element
            {
                Symbol = "S",
                Name = "Sulfur",
                AtomicNumber = 16,
                AtomicMass = 32.06,
                Electronegativity = 2.58,
                OxidationStates = new[] { -2, 2, 4, 6 },
                Group = 16,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s² 3p⁴",
                IonizationEnergy = 999.6,
                AtomicRadius = 104,
                ElectronAffinity = 200.0,
                Block = "p"
            },
            ["Cl"] = new Element
            {
                Symbol = "Cl",
                Name = "Chlorine",
                AtomicNumber = 17,
                AtomicMass = 35.45,
                Electronegativity = 3.16,
                OxidationStates = new[] { -1, 1, 3, 5, 7 },
                Group = 17,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s² 3p⁵",
                IonizationEnergy = 1251.2,
                AtomicRadius = 99,
                ElectronAffinity = 349.0,
                Block = "p"
            },
            ["Ar"] = new Element
            {
                Symbol = "Ar",
                Name = "Argon",
                AtomicNumber = 18,
                AtomicMass = 39.948,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 3,
                ElectronConfiguration = "1s² 2s² 2p⁶ 3s² 3p⁶",
                IonizationEnergy = 1520.6,
                AtomicRadius = 97,
                Block = "p"
            },

            // Период 4
            ["K"] = new Element
            {
                Symbol = "K",
                Name = "Potassium",
                AtomicNumber = 19,
                AtomicMass = 39.098,
                Electronegativity = 0.82,
                OxidationStates = new[] { 1 },
                Group = 1,
                Period = 4,
                ElectronConfiguration = "[Ar] 4s¹",
                IonizationEnergy = 418.8,
                AtomicRadius = 227,
                ElectronAffinity = 48.4,
                Block = "s"
            },
            ["Ca"] = new Element
            {
                Symbol = "Ca",
                Name = "Calcium",
                AtomicNumber = 20,
                AtomicMass = 40.078,
                Electronegativity = 1.00,
                OxidationStates = new[] { 2 },
                Group = 2,
                Period = 4,
                ElectronConfiguration = "[Ar] 4s²",
                IonizationEnergy = 589.8,
                AtomicRadius = 197,
                ElectronAffinity = 2.37,
                Block = "s"
            },
            ["Sc"] = new Element
            {
                Symbol = "Sc",
                Name = "Scandium",
                AtomicNumber = 21,
                AtomicMass = 44.956,
                Electronegativity = 1.36,
                OxidationStates = new[] { 3 },
                Group = 3,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹ 4s²",
                IonizationEnergy = 633.1,
                AtomicRadius = 162,
                Block = "d"
            },
            ["Ti"] = new Element
            {
                Symbol = "Ti",
                Name = "Titanium",
                AtomicNumber = 22,
                AtomicMass = 47.867,
                Electronegativity = 1.54,
                OxidationStates = new[] { 2, 3, 4 },
                Group = 4,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d² 4s²",
                IonizationEnergy = 658.8,
                AtomicRadius = 147,
                Block = "d"
            },
            ["V"] = new Element
            {
                Symbol = "V",
                Name = "Vanadium",
                AtomicNumber = 23,
                AtomicMass = 50.942,
                Electronegativity = 1.63,
                OxidationStates = new[] { 2, 3, 4, 5 },
                Group = 5,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d³ 4s²",
                IonizationEnergy = 650.9,
                AtomicRadius = 134,
                Block = "d"
            },
            ["Cr"] = new Element
            {
                Symbol = "Cr",
                Name = "Chromium",
                AtomicNumber = 24,
                AtomicMass = 51.996,
                Electronegativity = 1.66,
                OxidationStates = new[] { 2, 3, 6 },
                Group = 6,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d⁵ 4s¹",
                IonizationEnergy = 652.9,
                AtomicRadius = 128,
                Block = "d"
            },
            ["Mn"] = new Element
            {
                Symbol = "Mn",
                Name = "Manganese",
                AtomicNumber = 25,
                AtomicMass = 54.938,
                Electronegativity = 1.55,
                OxidationStates = new[] { 2, 3, 4, 6, 7 },
                Group = 7,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d⁵ 4s²",
                IonizationEnergy = 717.3,
                AtomicRadius = 127,
                Block = "d"
            },
            ["Fe"] = new Element
            {
                Symbol = "Fe",
                Name = "Iron",
                AtomicNumber = 26,
                AtomicMass = 55.845,
                Electronegativity = 1.83,
                OxidationStates = new[] { 2, 3 },
                Group = 8,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d⁶ 4s²",
                IonizationEnergy = 762.5,
                AtomicRadius = 126,
                Block = "d"
            },
            ["Co"] = new Element
            {
                Symbol = "Co",
                Name = "Cobalt",
                AtomicNumber = 27,
                AtomicMass = 58.933,
                Electronegativity = 1.88,
                OxidationStates = new[] { 2, 3 },
                Group = 9,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d⁷ 4s²",
                IonizationEnergy = 760.4,
                AtomicRadius = 125,
                Block = "d"
            },
            ["Ni"] = new Element
            {
                Symbol = "Ni",
                Name = "Nickel",
                AtomicNumber = 28,
                AtomicMass = 58.693,
                Electronegativity = 1.91,
                OxidationStates = new[] { 2, 3 },
                Group = 10,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d⁸ 4s²",
                IonizationEnergy = 737.1,
                AtomicRadius = 124,
                Block = "d"
            },
            ["Cu"] = new Element
            {
                Symbol = "Cu",
                Name = "Copper",
                AtomicNumber = 29,
                AtomicMass = 63.546,
                Electronegativity = 1.90,
                OxidationStates = new[] { 1, 2 },
                Group = 11,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s¹",
                IonizationEnergy = 745.5,
                AtomicRadius = 128,
                ElectronAffinity = 118.4,
                Block = "d"
            },
            ["Zn"] = new Element
            {
                Symbol = "Zn",
                Name = "Zinc",
                AtomicNumber = 30,
                AtomicMass = 65.38,
                Electronegativity = 1.65,
                OxidationStates = new[] { 2 },
                Group = 12,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s²",
                IonizationEnergy = 906.4,
                AtomicRadius = 134,
                Block = "d"
            },
            ["Ga"] = new Element
            {
                Symbol = "Ga",
                Name = "Gallium",
                AtomicNumber = 31,
                AtomicMass = 69.723,
                Electronegativity = 1.81,
                OxidationStates = new[] { 3 },
                Group = 13,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s² 4p¹",
                IonizationEnergy = 578.8,
                AtomicRadius = 135,
                Block = "p"
            },
            ["Ge"] = new Element
            {
                Symbol = "Ge",
                Name = "Germanium",
                AtomicNumber = 32,
                AtomicMass = 72.630,
                Electronegativity = 2.01,
                OxidationStates = new[] { 4 },
                Group = 14,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s² 4p²",
                IonizationEnergy = 762.2,
                AtomicRadius = 122,
                Block = "p"
            },
            ["As"] = new Element
            {
                Symbol = "As",
                Name = "Arsenic",
                AtomicNumber = 33,
                AtomicMass = 74.922,
                Electronegativity = 2.18,
                OxidationStates = new[] { -3, 3, 5 },
                Group = 15,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s² 4p³",
                IonizationEnergy = 947.0,
                AtomicRadius = 121,
                Block = "p"
            },
            ["Se"] = new Element
            {
                Symbol = "Se",
                Name = "Selenium",
                AtomicNumber = 34,
                AtomicMass = 78.971,
                Electronegativity = 2.55,
                OxidationStates = new[] { -2, 4, 6 },
                Group = 16,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s² 4p⁴",
                IonizationEnergy = 941.0,
                AtomicRadius = 116,
                Block = "p"
            },
            ["Br"] = new Element
            {
                Symbol = "Br",
                Name = "Bromine",
                AtomicNumber = 35,
                AtomicMass = 79.904,
                Electronegativity = 2.96,
                OxidationStates = new[] { -1, 1, 3, 5, 7 },
                Group = 17,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s² 4p⁵",
                IonizationEnergy = 1139.9,
                AtomicRadius = 114,
                ElectronAffinity = 324.6,
                Block = "p"
            },
            ["Kr"] = new Element
            {
                Symbol = "Kr",
                Name = "Krypton",
                AtomicNumber = 36,
                AtomicMass = 83.798,
                Electronegativity = 3.00,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 4,
                ElectronConfiguration = "[Ar] 3d¹⁰ 4s² 4p⁶",
                IonizationEnergy = 1350.8,
                AtomicRadius = 112,
                Block = "p"
            },

            // Период 5
            ["Rb"] = new Element
            {
                Symbol = "Rb",
                Name = "Rubidium",
                AtomicNumber = 37,
                AtomicMass = 85.468,
                Electronegativity = 0.82,
                OxidationStates = new[] { 1 },
                Group = 1,
                Period = 5,
                ElectronConfiguration = "[Kr] 5s¹",
                IonizationEnergy = 403.0,
                AtomicRadius = 248,
                Block = "s"
            },
            ["Sr"] = new Element
            {
                Symbol = "Sr",
                Name = "Strontium",
                AtomicNumber = 38,
                AtomicMass = 87.62,
                Electronegativity = 0.95,
                OxidationStates = new[] { 2 },
                Group = 2,
                Period = 5,
                ElectronConfiguration = "[Kr] 5s²",
                IonizationEnergy = 549.5,
                AtomicRadius = 215,
                Block = "s"
            },
            ["Y"] = new Element
            {
                Symbol = "Y",
                Name = "Yttrium",
                AtomicNumber = 39,
                AtomicMass = 88.906,
                Electronegativity = 1.22,
                OxidationStates = new[] { 3 },
                Group = 3,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹ 5s²",
                IonizationEnergy = 600.0,
                AtomicRadius = 180,
                Block = "d"
            },
            ["Zr"] = new Element
            {
                Symbol = "Zr",
                Name = "Zirconium",
                AtomicNumber = 40,
                AtomicMass = 91.224,
                Electronegativity = 1.33,
                OxidationStates = new[] { 4 },
                Group = 4,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d² 5s²",
                IonizationEnergy = 640.1,
                AtomicRadius = 160,
                Block = "d"
            },
            ["Nb"] = new Element
            {
                Symbol = "Nb",
                Name = "Niobium",
                AtomicNumber = 41,
                AtomicMass = 92.906,
                Electronegativity = 1.6,
                OxidationStates = new[] { 5 },
                Group = 5,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d⁴ 5s¹",
                IonizationEnergy = 652.1,
                AtomicRadius = 146,
                Block = "d"
            },
            ["Mo"] = new Element
            {
                Symbol = "Mo",
                Name = "Molybdenum",
                AtomicNumber = 42,
                AtomicMass = 95.95,
                Electronegativity = 2.16,
                OxidationStates = new[] { 6 },
                Group = 6,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d⁵ 5s¹",
                IonizationEnergy = 684.3,
                AtomicRadius = 139,
                Block = "d"
            },
            ["Tc"] = new Element
            {
                Symbol = "Tc",
                Name = "Technetium",
                AtomicNumber = 43,
                AtomicMass = 98,
                Electronegativity = 1.9,
                OxidationStates = new[] { 7 },
                Group = 7,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d⁵ 5s²",
                IonizationEnergy = 702.0,
                AtomicRadius = 136,
                Block = "d"
            },
            ["Ru"] = new Element
            {
                Symbol = "Ru",
                Name = "Ruthenium",
                AtomicNumber = 44,
                AtomicMass = 101.07,
                Electronegativity = 2.2,
                OxidationStates = new[] { 3, 4 },
                Group = 8,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d⁷ 5s¹",
                IonizationEnergy = 710.2,
                AtomicRadius = 134,
                Block = "d"
            },
            ["Rh"] = new Element
            {
                Symbol = "Rh",
                Name = "Rhodium",
                AtomicNumber = 45,
                AtomicMass = 102.91,
                Electronegativity = 2.28,
                OxidationStates = new[] { 3 },
                Group = 9,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d⁸ 5s¹",
                IonizationEnergy = 719.7,
                AtomicRadius = 134,
                Block = "d"
            },
            ["Pd"] = new Element
            {
                Symbol = "Pd",
                Name = "Palladium",
                AtomicNumber = 46,
                AtomicMass = 106.42,
                Electronegativity = 2.20,
                OxidationStates = new[] { 2, 4 },
                Group = 10,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰",
                IonizationEnergy = 804.4,
                AtomicRadius = 137,
                Block = "d"
            },
            ["Ag"] = new Element
            {
                Symbol = "Ag",
                Name = "Silver",
                AtomicNumber = 47,
                AtomicMass = 107.868,
                Electronegativity = 1.93,
                OxidationStates = new[] { 1 },
                Group = 11,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s¹",
                IonizationEnergy = 731.0,
                AtomicRadius = 144,
                ElectronAffinity = 125.6,
                Block = "d"
            },
            ["Cd"] = new Element
            {
                Symbol = "Cd",
                Name = "Cadmium",
                AtomicNumber = 48,
                AtomicMass = 112.414,
                Electronegativity = 1.69,
                OxidationStates = new[] { 2 },
                Group = 12,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s²",
                IonizationEnergy = 867.8,
                AtomicRadius = 151,
                Block = "d"
            },
            ["In"] = new Element
            {
                Symbol = "In",
                Name = "Indium",
                AtomicNumber = 49,
                AtomicMass = 114.818,
                Electronegativity = 1.78,
                OxidationStates = new[] { 3 },
                Group = 13,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s² 5p¹",
                IonizationEnergy = 558.3,
                AtomicRadius = 167,
                Block = "p"
            },
            ["Sn"] = new Element
            {
                Symbol = "Sn",
                Name = "Tin",
                AtomicNumber = 50,
                AtomicMass = 118.710,
                Electronegativity = 1.96,
                OxidationStates = new[] { 2, 4 },
                Group = 14,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s² 5p²",
                IonizationEnergy = 708.6,
                AtomicRadius = 162,
                Block = "p"
            },
            ["Sb"] = new Element
            {
                Symbol = "Sb",
                Name = "Antimony",
                AtomicNumber = 51,
                AtomicMass = 121.760,
                Electronegativity = 2.05,
                OxidationStates = new[] { -3, 3, 5 },
                Group = 15,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s² 5p³",
                IonizationEnergy = 834.0,
                AtomicRadius = 159,
                Block = "p"
            },
            ["Te"] = new Element
            {
                Symbol = "Te",
                Name = "Tellurium",
                AtomicNumber = 52,
                AtomicMass = 127.60,
                Electronegativity = 2.1,
                OxidationStates = new[] { -2, 4, 6 },
                Group = 16,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s² 5p⁴",
                IonizationEnergy = 869.3,
                AtomicRadius = 142,
                Block = "p"
            },
            ["I"] = new Element
            {
                Symbol = "I",
                Name = "Iodine",
                AtomicNumber = 53,
                AtomicMass = 126.904,
                Electronegativity = 2.66,
                OxidationStates = new[] { -1, 1, 5, 7 },
                Group = 17,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s² 5p⁵",
                IonizationEnergy = 1008.4,
                AtomicRadius = 140,
                ElectronAffinity = 295.2,
                Block = "p"
            },
            ["Xe"] = new Element
            {
                Symbol = "Xe",
                Name = "Xenon",
                AtomicNumber = 54,
                AtomicMass = 131.293,
                Electronegativity = 2.60,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 5,
                ElectronConfiguration = "[Kr] 4d¹⁰ 5s² 5p⁶",
                IonizationEnergy = 1170.4,
                AtomicRadius = 140,
                Block = "p"
            },

            // Период 6
            ["Cs"] = new Element
            {
                Symbol = "Cs",
                Name = "Cesium",
                AtomicNumber = 55,
                AtomicMass = 132.905,
                Electronegativity = 0.79,
                OxidationStates = new[] { 1 },
                Group = 1,
                Period = 6,
                ElectronConfiguration = "[Xe] 6s¹",
                IonizationEnergy = 375.7,
                AtomicRadius = 265,
                Block = "s"
            },
            ["Ba"] = new Element
            {
                Symbol = "Ba",
                Name = "Barium",
                AtomicNumber = 56,
                AtomicMass = 137.327,
                Electronegativity = 0.89,
                OxidationStates = new[] { 2 },
                Group = 2,
                Period = 6,
                ElectronConfiguration = "[Xe] 6s²",
                IonizationEnergy = 503.0,
                AtomicRadius = 222,
                Block = "s"
            },

            // Лантаноиды
            ["La"] = new Element
            {
                Symbol = "La",
                Name = "Lanthanum",
                AtomicNumber = 57,
                AtomicMass = 138.905,
                Electronegativity = 1.10,
                OxidationStates = new[] { 3 },
                Group = 3,
                Period = 6,
                ElectronConfiguration = "[Xe] 5d¹ 6s²",
                IonizationEnergy = 538.1,
                AtomicRadius = 187,
                Block = "f"
            },
            ["Ce"] = new Element
            {
                Symbol = "Ce",
                Name = "Cerium",
                AtomicNumber = 58,
                AtomicMass = 140.116,
                Electronegativity = 1.12,
                OxidationStates = new[] { 3, 4 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹ 5d¹ 6s²",
                IonizationEnergy = 534.4,
                AtomicRadius = 181,
                Block = "f"
            },
            ["Pr"] = new Element
            {
                Symbol = "Pr",
                Name = "Praseodymium",
                AtomicNumber = 59,
                AtomicMass = 140.908,
                Electronegativity = 1.13,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f³ 6s²",
                IonizationEnergy = 527.0,
                AtomicRadius = 182,
                Block = "f"
            },
            ["Nd"] = new Element
            {
                Symbol = "Nd",
                Name = "Neodymium",
                AtomicNumber = 60,
                AtomicMass = 144.242,
                Electronegativity = 1.14,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f⁴ 6s²",
                IonizationEnergy = 533.1,
                AtomicRadius = 181,
                Block = "f"
            },
            ["Pm"] = new Element
            {
                Symbol = "Pm",
                Name = "Promethium",
                AtomicNumber = 61,
                AtomicMass = 145,
                Electronegativity = 1.13,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f⁵ 6s²",
                IonizationEnergy = 540.0,
                AtomicRadius = 183,
                Block = "f"
            },
            ["Sm"] = new Element
            {
                Symbol = "Sm",
                Name = "Samarium",
                AtomicNumber = 62,
                AtomicMass = 150.36,
                Electronegativity = 1.17,
                OxidationStates = new[] { 2, 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f⁶ 6s²",
                IonizationEnergy = 544.5,
                AtomicRadius = 180,
                Block = "f"
            },
            ["Eu"] = new Element
            {
                Symbol = "Eu",
                Name = "Europium",
                AtomicNumber = 63,
                AtomicMass = 151.964,
                Electronegativity = 1.20,
                OxidationStates = new[] { 2, 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f⁷ 6s²",
                IonizationEnergy = 547.1,
                AtomicRadius = 180,
                Block = "f"
            },
            ["Gd"] = new Element
            {
                Symbol = "Gd",
                Name = "Gadolinium",
                AtomicNumber = 64,
                AtomicMass = 157.25,
                Electronegativity = 1.20,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f⁷ 5d¹ 6s²",
                IonizationEnergy = 593.4,
                AtomicRadius = 180,
                Block = "f"
            },
            ["Tb"] = new Element
            {
                Symbol = "Tb",
                Name = "Terbium",
                AtomicNumber = 65,
                AtomicMass = 158.925,
                Electronegativity = 1.20,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f⁹ 6s²",
                IonizationEnergy = 565.8,
                AtomicRadius = 177,
                Block = "f"
            },
            ["Dy"] = new Element
            {
                Symbol = "Dy",
                Name = "Dysprosium",
                AtomicNumber = 66,
                AtomicMass = 162.500,
                Electronegativity = 1.22,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁰ 6s²",
                IonizationEnergy = 573.0,
                AtomicRadius = 178,
                Block = "f"
            },
            ["Ho"] = new Element
            {
                Symbol = "Ho",
                Name = "Holmium",
                AtomicNumber = 67,
                AtomicMass = 164.930,
                Electronegativity = 1.23,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹¹ 6s²",
                IonizationEnergy = 581.0,
                AtomicRadius = 176,
                Block = "f"
            },
            ["Er"] = new Element
            {
                Symbol = "Er",
                Name = "Erbium",
                AtomicNumber = 68,
                AtomicMass = 167.259,
                Electronegativity = 1.24,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹² 6s²",
                IonizationEnergy = 589.3,
                AtomicRadius = 176,
                Block = "f"
            },
            ["Tm"] = new Element
            {
                Symbol = "Tm",
                Name = "Thulium",
                AtomicNumber = 69,
                AtomicMass = 168.934,
                Electronegativity = 1.25,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹³ 6s²",
                IonizationEnergy = 596.7,
                AtomicRadius = 176,
                Block = "f"
            },
            ["Yb"] = new Element
            {
                Symbol = "Yb",
                Name = "Ytterbium",
                AtomicNumber = 70,
                AtomicMass = 173.045,
                Electronegativity = 1.10,
                OxidationStates = new[] { 2, 3 },
                Group = 0,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 6s²",
                IonizationEnergy = 603.4,
                AtomicRadius = 176,
                Block = "f"
            },
            ["Lu"] = new Element
            {
                Symbol = "Lu",
                Name = "Lutetium",
                AtomicNumber = 71,
                AtomicMass = 174.967,
                Electronegativity = 1.27,
                OxidationStates = new[] { 3 },
                Group = 3,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹ 6s²",
                IonizationEnergy = 523.5,
                AtomicRadius = 174,
                Block = "d"
            },

            // Продолжение периода 6
            ["Hf"] = new Element
            {
                Symbol = "Hf",
                Name = "Hafnium",
                AtomicNumber = 72,
                AtomicMass = 178.49,
                Electronegativity = 1.3,
                OxidationStates = new[] { 4 },
                Group = 4,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d² 6s²",
                IonizationEnergy = 658.5,
                AtomicRadius = 159,
                Block = "d"
            },
            ["Ta"] = new Element
            {
                Symbol = "Ta",
                Name = "Tantalum",
                AtomicNumber = 73,
                AtomicMass = 180.948,
                Electronegativity = 1.5,
                OxidationStates = new[] { 5 },
                Group = 5,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d³ 6s²",
                IonizationEnergy = 761.0,
                AtomicRadius = 146,
                Block = "d"
            },
            ["W"] = new Element
            {
                Symbol = "W",
                Name = "Tungsten",
                AtomicNumber = 74,
                AtomicMass = 183.84,
                Electronegativity = 2.36,
                OxidationStates = new[] { 6 },
                Group = 6,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d⁴ 6s²",
                IonizationEnergy = 770.0,
                AtomicRadius = 139,
                Block = "d"
            },
            ["Re"] = new Element
            {
                Symbol = "Re",
                Name = "Rhenium",
                AtomicNumber = 75,
                AtomicMass = 186.207,
                Electronegativity = 1.9,
                OxidationStates = new[] { 7 },
                Group = 7,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d⁵ 6s²",
                IonizationEnergy = 760.0,
                AtomicRadius = 137,
                Block = "d"
            },
            ["Os"] = new Element
            {
                Symbol = "Os",
                Name = "Osmium",
                AtomicNumber = 76,
                AtomicMass = 190.23,
                Electronegativity = 2.2,
                OxidationStates = new[] { 4 },
                Group = 8,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d⁶ 6s²",
                IonizationEnergy = 840.0,
                AtomicRadius = 135,
                Block = "d"
            },
            ["Ir"] = new Element
            {
                Symbol = "Ir",
                Name = "Iridium",
                AtomicNumber = 77,
                AtomicMass = 192.217,
                Electronegativity = 2.20,
                OxidationStates = new[] { 3, 4 },
                Group = 9,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d⁷ 6s²",
                IonizationEnergy = 880.0,
                AtomicRadius = 136,
                Block = "d"
            },
            ["Pt"] = new Element
            {
                Symbol = "Pt",
                Name = "Platinum",
                AtomicNumber = 78,
                AtomicMass = 195.084,
                Electronegativity = 2.28,
                OxidationStates = new[] { 2, 4 },
                Group = 10,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d⁹ 6s¹",
                IonizationEnergy = 870.0,
                AtomicRadius = 139,
                Block = "d"
            },
            ["Au"] = new Element
            {
                Symbol = "Au",
                Name = "Gold",
                AtomicNumber = 79,
                AtomicMass = 196.967,
                Electronegativity = 2.54,
                OxidationStates = new[] { 1, 3 },
                Group = 11,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s¹",
                IonizationEnergy = 890.1,
                AtomicRadius = 144,
                ElectronAffinity = 222.8,
                Block = "d"
            },
            ["Hg"] = new Element
            {
                Symbol = "Hg",
                Name = "Mercury",
                AtomicNumber = 80,
                AtomicMass = 200.592,
                Electronegativity = 2.00,
                OxidationStates = new[] { 1, 2 },
                Group = 12,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s²",
                IonizationEnergy = 1007.1,
                AtomicRadius = 151,
                Block = "d"
            },
            ["Tl"] = new Element
            {
                Symbol = "Tl",
                Name = "Thallium",
                AtomicNumber = 81,
                AtomicMass = 204.38,
                Electronegativity = 1.62,
                OxidationStates = new[] { 1, 3 },
                Group = 13,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p¹",
                IonizationEnergy = 589.4,
                AtomicRadius = 170,
                Block = "p"
            },
            ["Pb"] = new Element
            {
                Symbol = "Pb",
                Name = "Lead",
                AtomicNumber = 82,
                AtomicMass = 207.2,
                Electronegativity = 2.33,
                OxidationStates = new[] { 2, 4 },
                Group = 14,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p²",
                IonizationEnergy = 715.6,
                AtomicRadius = 175,
                Block = "p"
            },
            ["Bi"] = new Element
            {
                Symbol = "Bi",
                Name = "Bismuth",
                AtomicNumber = 83,
                AtomicMass = 208.980,
                Electronegativity = 2.02,
                OxidationStates = new[] { 3, 5 },
                Group = 15,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p³",
                IonizationEnergy = 703.0,
                AtomicRadius = 156,
                Block = "p"
            },
            ["Po"] = new Element
            {
                Symbol = "Po",
                Name = "Polonium",
                AtomicNumber = 84,
                AtomicMass = 209,
                Electronegativity = 2.0,
                OxidationStates = new[] { -2, 2, 4 },
                Group = 16,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p⁴",
                IonizationEnergy = 812.1,
                AtomicRadius = 167,
                Block = "p"
            },
            ["At"] = new Element
            {
                Symbol = "At",
                Name = "Astatine",
                AtomicNumber = 85,
                AtomicMass = 210,
                Electronegativity = 2.2,
                OxidationStates = new[] { -1, 1 },
                Group = 17,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p⁵",
                IonizationEnergy = 920.0,
                AtomicRadius = 202,
                Block = "p"
            },
            ["Rn"] = new Element
            {
                Symbol = "Rn",
                Name = "Radon",
                AtomicNumber = 86,
                AtomicMass = 222,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 6,
                ElectronConfiguration = "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p⁶",
                IonizationEnergy = 1037.0,
                AtomicRadius = 220,
                Block = "p"
            },

            // Период 7
            ["Fr"] = new Element
            {
                Symbol = "Fr",
                Name = "Francium",
                AtomicNumber = 87,
                AtomicMass = 223,
                Electronegativity = 0.7,
                OxidationStates = new[] { 1 },
                Group = 1,
                Period = 7,
                ElectronConfiguration = "[Rn] 7s¹",
                IonizationEnergy = 380.0,
                AtomicRadius = 348,
                Block = "s"
            },
            ["Ra"] = new Element
            {
                Symbol = "Ra",
                Name = "Radium",
                AtomicNumber = 88,
                AtomicMass = 226,
                Electronegativity = 0.9,
                OxidationStates = new[] { 2 },
                Group = 2,
                Period = 7,
                ElectronConfiguration = "[Rn] 7s²",
                IonizationEnergy = 509.3,
                AtomicRadius = 283,
                Block = "s"
            },

            // Актиноиды
            ["Ac"] = new Element
            {
                Symbol = "Ac",
                Name = "Actinium",
                AtomicNumber = 89,
                AtomicMass = 227,
                Electronegativity = 1.1,
                OxidationStates = new[] { 3 },
                Group = 3,
                Period = 7,
                ElectronConfiguration = "[Rn] 6d¹ 7s²",
                IonizationEnergy = 499.0,
                AtomicRadius = 188,
                Block = "f"
            },
            ["Th"] = new Element
            {
                Symbol = "Th",
                Name = "Thorium",
                AtomicNumber = 90,
                AtomicMass = 232.038,
                Electronegativity = 1.3,
                OxidationStates = new[] { 4 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 6d² 7s²",
                IonizationEnergy = 587.0,
                AtomicRadius = 179,
                Block = "f"
            },
            ["Pa"] = new Element
            {
                Symbol = "Pa",
                Name = "Protactinium",
                AtomicNumber = 91,
                AtomicMass = 231.036,
                Electronegativity = 1.5,
                OxidationStates = new[] { 5 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f² 6d¹ 7s²",
                IonizationEnergy = 568.0,
                AtomicRadius = 180,
                Block = "f"
            },
            ["U"] = new Element
            {
                Symbol = "U",
                Name = "Uranium",
                AtomicNumber = 92,
                AtomicMass = 238.029,
                Electronegativity = 1.38,
                OxidationStates = new[] { 3, 4, 5, 6 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f³ 6d¹ 7s²",
                IonizationEnergy = 597.6,
                AtomicRadius = 156,
                Block = "f"
            },
            ["Np"] = new Element
            {
                Symbol = "Np",
                Name = "Neptunium",
                AtomicNumber = 93,
                AtomicMass = 237,
                Electronegativity = 1.36,
                OxidationStates = new[] { 3, 4, 5, 6 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f⁴ 6d¹ 7s²",
                IonizationEnergy = 604.5,
                AtomicRadius = 155,
                Block = "f"
            },
            ["Pu"] = new Element
            {
                Symbol = "Pu",
                Name = "Plutonium",
                AtomicNumber = 94,
                AtomicMass = 244,
                Electronegativity = 1.28,
                OxidationStates = new[] { 3, 4, 5, 6 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f⁶ 7s²",
                IonizationEnergy = 584.7,
                AtomicRadius = 159,
                Block = "f"
            },
            ["Am"] = new Element
            {
                Symbol = "Am",
                Name = "Americium",
                AtomicNumber = 95,
                AtomicMass = 243,
                Electronegativity = 1.13,
                OxidationStates = new[] { 3, 4, 5, 6 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f⁷ 7s²",
                IonizationEnergy = 578.0,
                AtomicRadius = 173,
                Block = "f"
            },
            ["Cm"] = new Element
            {
                Symbol = "Cm",
                Name = "Curium",
                AtomicNumber = 96,
                AtomicMass = 247,
                Electronegativity = 1.28,
                OxidationStates = new[] { 3, 4 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f⁷ 6d¹ 7s²",
                IonizationEnergy = 581.0,
                AtomicRadius = 174,
                Block = "f"
            },
            ["Bk"] = new Element
            {
                Symbol = "Bk",
                Name = "Berkelium",
                AtomicNumber = 97,
                AtomicMass = 247,
                Electronegativity = 1.3,
                OxidationStates = new[] { 3, 4 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f⁹ 7s²",
                IonizationEnergy = 601.0,
                AtomicRadius = 170,
                Block = "f"
            },
            ["Cf"] = new Element
            {
                Symbol = "Cf",
                Name = "Californium",
                AtomicNumber = 98,
                AtomicMass = 251,
                Electronegativity = 1.3,
                OxidationStates = new[] { 3, 4 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁰ 7s²",
                IonizationEnergy = 608.0,
                AtomicRadius = 169,
                Block = "f"
            },
            ["Es"] = new Element
            {
                Symbol = "Es",
                Name = "Einsteinium",
                AtomicNumber = 99,
                AtomicMass = 252,
                Electronegativity = 1.3,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹¹ 7s²",
                IonizationEnergy = 619.0,
                AtomicRadius = 165,
                Block = "f"
            },
            ["Fm"] = new Element
            {
                Symbol = "Fm",
                Name = "Fermium",
                AtomicNumber = 100,
                AtomicMass = 257,
                Electronegativity = 1.3,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹² 7s²",
                IonizationEnergy = 627.0,
                AtomicRadius = 167,
                Block = "f"
            },
            ["Md"] = new Element
            {
                Symbol = "Md",
                Name = "Mendelevium",
                AtomicNumber = 101,
                AtomicMass = 258,
                Electronegativity = 1.3,
                OxidationStates = new[] { 3 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹³ 7s²",
                IonizationEnergy = 635.0,
                AtomicRadius = 173,
                Block = "f"
            },
            ["No"] = new Element
            {
                Symbol = "No",
                Name = "Nobelium",
                AtomicNumber = 102,
                AtomicMass = 259,
                Electronegativity = 1.3,
                OxidationStates = new[] { 2, 3 },
                Group = 0,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 7s²",
                IonizationEnergy = 642.0,
                AtomicRadius = 176,
                Block = "f"
            },
            ["Lr"] = new Element
            {
                Symbol = "Lr",
                Name = "Lawrencium",
                AtomicNumber = 103,
                AtomicMass = 266,
                Electronegativity = 1.3,
                OxidationStates = new[] { 3 },
                Group = 3,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 7s² 7p¹",
                IonizationEnergy = 470.0,
                AtomicRadius = 161,
                Block = "d"
            },

            // Период 7 - продолжение (трансактиноиды)
            ["Rf"] = new Element
            {
                Symbol = "Rf",
                Name = "Rutherfordium",
                AtomicNumber = 104,
                AtomicMass = 267,
                Electronegativity = 0,
                OxidationStates = new[] { 4 },
                Group = 4,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d² 7s²",
                IonizationEnergy = 580.0,
                AtomicRadius = 157,
                Block = "d"
            },
            ["Db"] = new Element
            {
                Symbol = "Db",
                Name = "Dubnium",
                AtomicNumber = 105,
                AtomicMass = 268,
                Electronegativity = 0,
                OxidationStates = new[] { 5 },
                Group = 5,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d³ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 149,
                Block = "d"
            },
            ["Sg"] = new Element
            {
                Symbol = "Sg",
                Name = "Seaborgium",
                AtomicNumber = 106,
                AtomicMass = 269,
                Electronegativity = 0,
                OxidationStates = new[] { 6 },
                Group = 6,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d⁴ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 143,
                Block = "d"
            },
            ["Bh"] = new Element
            {
                Symbol = "Bh",
                Name = "Bohrium",
                AtomicNumber = 107,
                AtomicMass = 270,
                Electronegativity = 0,
                OxidationStates = new[] { 7 },
                Group = 7,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d⁵ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 141,
                Block = "d"
            },
            ["Hs"] = new Element
            {
                Symbol = "Hs",
                Name = "Hassium",
                AtomicNumber = 108,
                AtomicMass = 277,
                Electronegativity = 0,
                OxidationStates = new[] { 8 },
                Group = 8,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d⁶ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 134,
                Block = "d"
            },
            ["Mt"] = new Element
            {
                Symbol = "Mt",
                Name = "Meitnerium",
                AtomicNumber = 109,
                AtomicMass = 278,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 9,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d⁷ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 129,
                Block = "d"
            },
            ["Ds"] = new Element
            {
                Symbol = "Ds",
                Name = "Darmstadtium",
                AtomicNumber = 110,
                AtomicMass = 281,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 10,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d⁸ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 128,
                Block = "d"
            },
            ["Rg"] = new Element
            {
                Symbol = "Rg",
                Name = "Roentgenium",
                AtomicNumber = 111,
                AtomicMass = 282,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 11,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d⁹ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 121,
                Block = "d"
            },
            ["Cn"] = new Element
            {
                Symbol = "Cn",
                Name = "Copernicium",
                AtomicNumber = 112,
                AtomicMass = 285,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 12,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s²",
                IonizationEnergy = 0,
                AtomicRadius = 122,
                Block = "d"
            },
            ["Nh"] = new Element
            {
                Symbol = "Nh",
                Name = "Nihonium",
                AtomicNumber = 113,
                AtomicMass = 286,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 13,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p¹",
                IonizationEnergy = 0,
                AtomicRadius = 136,
                Block = "p"
            },
            ["Fl"] = new Element
            {
                Symbol = "Fl",
                Name = "Flerovium",
                AtomicNumber = 114,
                AtomicMass = 289,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 14,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p²",
                IonizationEnergy = 0,
                AtomicRadius = 143,
                Block = "p"
            },
            ["Mc"] = new Element
            {
                Symbol = "Mc",
                Name = "Moscovium",
                AtomicNumber = 115,
                AtomicMass = 290,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 15,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p³",
                IonizationEnergy = 0,
                AtomicRadius = 162,
                Block = "p"
            },
            ["Lv"] = new Element
            {
                Symbol = "Lv",
                Name = "Livermorium",
                AtomicNumber = 116,
                AtomicMass = 293,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 16,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p⁴",
                IonizationEnergy = 0,
                AtomicRadius = 175,
                Block = "p"
            },
            ["Ts"] = new Element
            {
                Symbol = "Ts",
                Name = "Tennessine",
                AtomicNumber = 117,
                AtomicMass = 294,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 17,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p⁵",
                IonizationEnergy = 0,
                AtomicRadius = 165,
                Block = "p"
            },
            ["Og"] = new Element
            {
                Symbol = "Og",
                Name = "Oganesson",
                AtomicNumber = 118,
                AtomicMass = 294,
                Electronegativity = 0,
                OxidationStates = new[] { 0 },
                Group = 18,
                Period = 7,
                ElectronConfiguration = "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p⁶",
                IonizationEnergy = 0,
                AtomicRadius = 157,
                Block = "p"
            }
        };
    }

    private void InitializeCommonCompounds()
    {
        _compounds = new Dictionary<string, Compound>
        {
            // ═══ НЕОРГАНИЧЕСКИЕ СОЕДИНЕНИЯ ═══

            // Оксиды
            ["H2O"] = new Compound
            {
                Formula = "H2O",
                CommonName = "Water",
                SMILES = "O",
                MolarMass = 18.015,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 0,
                    BoilingPoint = 100,
                    Density = 1.0,
                    DeltaHf = -285.8
                }
            },
            ["CO2"] = new Compound
            {
                Formula = "CO2",
                CommonName = "Carbon dioxide",
                SMILES = "C(=O)=O",
                MolarMass = 44.01,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -78.5,
                    BoilingPoint = -56.6,
                    Density = 1.98,
                    DeltaHf = -393.5
                }
            },
            ["CO"] = new Compound
            {
                Formula = "CO",
                CommonName = "Carbon monoxide",
                SMILES = "[C-]#[O+]",
                MolarMass = 28.01,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -205,
                    BoilingPoint = -191.5,
                    DeltaHf = -110.5
                }
            },
            ["SO2"] = new Compound
            {
                Formula = "SO2",
                CommonName = "Sulfur dioxide",
                SMILES = "O=S=O",
                MolarMass = 64.066,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -72,
                    BoilingPoint = -10,
                    DeltaHf = -296.8
                }
            },
            ["SO3"] = new Compound
            {
                Formula = "SO3",
                CommonName = "Sulfur trioxide",
                SMILES = "O=S(=O)=O",
                MolarMass = 80.066,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 16.9,
                    BoilingPoint = 44.8,
                    DeltaHf = -395.7
                }
            },
            ["NO"] = new Compound
            {
                Formula = "NO",
                CommonName = "Nitric oxide",
                SMILES = "[N]=O",
                MolarMass = 30.006,
                Properties = new PhysicalProperties { DeltaHf = 91.3 }
            },
            ["NO2"] = new Compound
            {
                Formula = "NO2",
                CommonName = "Nitrogen dioxide",
                SMILES = "N(=O)=O",
                MolarMass = 46.006,
                Properties = new PhysicalProperties { DeltaHf = 33.2 }
            },
            ["N2O"] = new Compound
            {
                Formula = "N2O",
                CommonName = "Nitrous oxide",
                SMILES = "N#N=O",
                MolarMass = 44.013,
                Properties = new PhysicalProperties { DeltaHf = 82.1 }
            },
            ["N2O5"] = new Compound
            {
                Formula = "N2O5",
                CommonName = "Dinitrogen pentoxide",
                MolarMass = 108.01,
                Properties = new PhysicalProperties { DeltaHf = 11.3 }
            },
            ["P2O5"] = new Compound
            {
                Formula = "P2O5",
                CommonName = "Phosphorus pentoxide",
                MolarMass = 141.94,
                Properties = new PhysicalProperties { DeltaHf = -2984.0 }
            },
            ["Fe2O3"] = new Compound
            {
                Formula = "Fe2O3",
                CommonName = "Iron(III) oxide",
                MolarMass = 159.69,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 1566,
                    Density = 5.24,
                    DeltaHf = -824.2
                }
            },
            ["FeO"] = new Compound
            {
                Formula = "FeO",
                CommonName = "Iron(II) oxide",
                MolarMass = 71.844,
                Properties = new PhysicalProperties { DeltaHf = -272.0 }
            },
            ["Fe3O4"] = new Compound
            {
                Formula = "Fe3O4",
                CommonName = "Iron(II,III) oxide",
                MolarMass = 231.533,
                Properties = new PhysicalProperties { DeltaHf = -1118.4 }
            },
            ["CuO"] = new Compound
            {
                Formula = "CuO",
                CommonName = "Copper(II) oxide",
                MolarMass = 79.545,
                Properties = new PhysicalProperties { DeltaHf = -157.3 }
            },
            ["Cu2O"] = new Compound
            {
                Formula = "Cu2O",
                CommonName = "Copper(I) oxide",
                MolarMass = 143.09,
                Properties = new PhysicalProperties { DeltaHf = -168.6 }
            },
            ["Al2O3"] = new Compound
            {
                Formula = "Al2O3",
                CommonName = "Aluminum oxide",
                MolarMass = 101.96,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 2072,
                    Density = 3.95,
                    DeltaHf = -1675.7
                }
            },
            ["ZnO"] = new Compound
            {
                Formula = "ZnO",
                CommonName = "Zinc oxide",
                MolarMass = 81.406,
                Properties = new PhysicalProperties { DeltaHf = -350.5 }
            },
            ["CaO"] = new Compound
            {
                Formula = "CaO",
                CommonName = "Calcium oxide",
                MolarMass = 56.077,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 2613,
                    Density = 3.34,
                    DeltaHf = -635.1
                }
            },
            ["MgO"] = new Compound
            {
                Formula = "MgO",
                CommonName = "Magnesium oxide",
                MolarMass = 40.304,
                Properties = new PhysicalProperties { DeltaHf = -601.6 }
            },

            // Кислоты
            ["HCl"] = new Compound
            {
                Formula = "HCl",
                CommonName = "Hydrochloric acid",
                SMILES = "Cl",
                MolarMass = 36.461,
                Properties = new PhysicalProperties
                {
                    BoilingPoint = -85.05,
                    DeltaHf = -92.3,
                    PKa = -6.3
                }
            },
            ["HBr"] = new Compound
            {
                Formula = "HBr",
                CommonName = "Hydrobromic acid",
                SMILES = "Br",
                MolarMass = 80.912,
                Properties = new PhysicalProperties { DeltaHf = -36.3, PKa = -9 }
            },
            ["HI"] = new Compound
            {
                Formula = "HI",
                CommonName = "Hydroiodic acid",
                SMILES = "I",
                MolarMass = 127.904,
                Properties = new PhysicalProperties { DeltaHf = 26.5, PKa = -10 }
            },
            ["HF"] = new Compound
            {
                Formula = "HF",
                CommonName = "Hydrofluoric acid",
                SMILES = "F",
                MolarMass = 20.006,
                Properties = new PhysicalProperties { DeltaHf = -273.3, PKa = 3.2 }
            },
            ["H2SO4"] = new Compound
            {
                Formula = "H2SO4",
                CommonName = "Sulfuric acid",
                SMILES = "OS(=O)(=O)O",
                MolarMass = 98.079,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 10.4,
                    BoilingPoint = 337,
                    Density = 1.84,
                    DeltaHf = -814.0,
                    PKa = -3
                }
            },
            ["HNO3"] = new Compound
            {
                Formula = "HNO3",
                CommonName = "Nitric acid",
                SMILES = "O[N+](=O)[O-]",
                MolarMass = 63.012,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -42,
                    BoilingPoint = 83,
                    Density = 1.51,
                    DeltaHf = -174.1,
                    PKa = -1.4
                }
            },
            ["HNO2"] = new Compound
            {
                Formula = "HNO2",
                CommonName = "Nitrous acid",
                SMILES = "ON=O",
                MolarMass = 47.013,
                Properties = new PhysicalProperties { DeltaHf = -79.5, PKa = 3.3 }
            },
            ["H3PO4"] = new Compound
            {
                Formula = "H3PO4",
                CommonName = "Phosphoric acid",
                SMILES = "OP(=O)(O)O",
                MolarMass = 97.994,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 42.4,
                    Density = 1.88,
                    DeltaHf = -1279.0,
                    PKa = 2.15
                }
            },
            ["H2CO3"] = new Compound
            {
                Formula = "H2CO3",
                CommonName = "Carbonic acid",
                SMILES = "OC(=O)O",
                MolarMass = 62.024,
                Properties = new PhysicalProperties { DeltaHf = -699.7, PKa = 6.35 }
            },
            ["H2S"] = new Compound
            {
                Formula = "H2S",
                CommonName = "Hydrogen sulfide",
                SMILES = "S",
                MolarMass = 34.081,
                Properties = new PhysicalProperties
                {
                    BoilingPoint = -60,
                    DeltaHf = -20.6,
                    PKa = 7.0
                }
            },

            // Основания
            ["NaOH"] = new Compound
            {
                Formula = "NaOH",
                CommonName = "Sodium hydroxide",
                MolarMass = 39.997,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 323,
                    BoilingPoint = 1388,
                    Density = 2.13,
                    DeltaHf = -425.8
                }
            },
            ["KOH"] = new Compound
            {
                Formula = "KOH",
                CommonName = "Potassium hydroxide",
                MolarMass = 56.105,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 406,
                    Density = 2.04,
                    DeltaHf = -424.7
                }
            },
            ["Ca(OH)2"] = new Compound
            {
                Formula = "CaH2O2",
                CommonName = "Calcium hydroxide",
                MolarMass = 74.093,
                Properties = new PhysicalProperties { DeltaHf = -986.1 }
            },
            ["Mg(OH)2"] = new Compound
            {
                Formula = "MgH2O2",
                CommonName = "Magnesium hydroxide",
                MolarMass = 58.319,
                Properties = new PhysicalProperties { DeltaHf = -924.5 }
            },
            ["NH3"] = new Compound
            {
                Formula = "NH3",
                CommonName = "Ammonia",
                SMILES = "N",
                MolarMass = 17.031,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -77.7,
                    BoilingPoint = -33.3,
                    DeltaHf = -45.9,
                    PKa = 9.25
                }
            },
            ["NH4OH"] = new Compound
            {
                Formula = "NH5O",
                CommonName = "Ammonium hydroxide",
                MolarMass = 35.046,
                Properties = new PhysicalProperties { PKa = 9.25 }
            },

            // Соли
            ["NaCl"] = new Compound
            {
                Formula = "NaCl",
                CommonName = "Sodium chloride",
                MolarMass = 58.443,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 801,
                    BoilingPoint = 1413,
                    Density = 2.165,
                    DeltaHf = -411.2
                }
            },
            ["KCl"] = new Compound
            {
                Formula = "KCl",
                CommonName = "Potassium chloride",
                MolarMass = 74.551,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 770,
                    Density = 1.98,
                    DeltaHf = -436.7
                }
            },
            ["CaCl2"] = new Compound
            {
                Formula = "CaCl2",
                CommonName = "Calcium chloride",
                MolarMass = 110.98,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 772,
                    Density = 2.15,
                    DeltaHf = -795.8
                }
            },
            ["MgCl2"] = new Compound
            {
                Formula = "MgCl2",
                CommonName = "Magnesium chloride",
                MolarMass = 95.211,
                Properties = new PhysicalProperties { DeltaHf = -641.3 }
            },
            ["AgCl"] = new Compound
            {
                Formula = "AgCl",
                CommonName = "Silver chloride",
                MolarMass = 143.321,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 455,
                    Density = 5.56,
                    DeltaHf = -127.0
                }
            },
            ["Na2SO4"] = new Compound
            {
                Formula = "Na2SO4",
                CommonName = "Sodium sulfate",
                MolarMass = 142.04,
                Properties = new PhysicalProperties { DeltaHf = -1387.1 }
            },
            ["K2SO4"] = new Compound
            {
                Formula = "K2SO4",
                CommonName = "Potassium sulfate",
                MolarMass = 174.26,
                Properties = new PhysicalProperties { DeltaHf = -1437.8 }
            },
            ["CaSO4"] = new Compound
            {
                Formula = "CaSO4",
                CommonName = "Calcium sulfate",
                MolarMass = 136.14,
                Properties = new PhysicalProperties { DeltaHf = -1434.5 }
            },
            ["BaSO4"] = new Compound
            {
                Formula = "BaSO4",
                CommonName = "Barium sulfate",
                MolarMass = 233.39,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 1580,
                    Density = 4.50,
                    DeltaHf = -1473.2
                }
            },
            ["CuSO4"] = new Compound
            {
                Formula = "CuSO4",
                CommonName = "Copper(II) sulfate",
                MolarMass = 159.609,
                Properties = new PhysicalProperties { DeltaHf = -771.4 }
            },
            ["FeSO4"] = new Compound
            {
                Formula = "FeSO4",
                CommonName = "Iron(II) sulfate",
                MolarMass = 151.908,
                Properties = new PhysicalProperties { DeltaHf = -928.4 }
            },
            ["ZnSO4"] = new Compound
            {
                Formula = "ZnSO4",
                CommonName = "Zinc sulfate",
                MolarMass = 161.472,
                Properties = new PhysicalProperties { DeltaHf = -982.8 }
            },
            ["NaNO3"] = new Compound
            {
                Formula = "NaNO3",
                CommonName = "Sodium nitrate",
                MolarMass = 84.995,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 308,
                    Density = 2.26,
                    DeltaHf = -467.9
                }
            },
            ["KNO3"] = new Compound
            {
                Formula = "KNO3",
                CommonName = "Potassium nitrate",
                MolarMass = 101.103,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 334,
                    Density = 2.11,
                    DeltaHf = -494.6
                }
            },
            ["AgNO3"] = new Compound
            {
                Formula = "AgNO3",
                CommonName = "Silver nitrate",
                MolarMass = 169.872,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 212,
                    Density = 4.35,
                    DeltaHf = -124.4
                }
            },
            ["Ca(NO3)2"] = new Compound
            {
                Formula = "CaN2O6",
                CommonName = "Calcium nitrate",
                MolarMass = 164.088,
                Properties = new PhysicalProperties { DeltaHf = -938.2 }
            },
            ["NH4Cl"] = new Compound
            {
                Formula = "NH4Cl",
                CommonName = "Ammonium chloride",
                MolarMass = 53.491,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 338,
                    Density = 1.53,
                    DeltaHf = -314.4
                }
            },
            ["(NH4)2SO4"] = new Compound
            {
                Formula = "N2H8SO4",
                CommonName = "Ammonium sulfate",
                MolarMass = 132.14,
                Properties = new PhysicalProperties { DeltaHf = -1180.9 }
            },
            ["NH4NO3"] = new Compound
            {
                Formula = "NH4NO3",
                CommonName = "Ammonium nitrate",
                MolarMass = 80.043,
                Properties = new PhysicalProperties { DeltaHf = -365.6 }
            },
            ["CaCO3"] = new Compound
            {
                Formula = "CaCO3",
                CommonName = "Calcium carbonate",
                MolarMass = 100.087,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 1339,
                    Density = 2.71,
                    DeltaHf = -1206.9
                }
            },
            ["Na2CO3"] = new Compound
            {
                Formula = "Na2CO3",
                CommonName = "Sodium carbonate",
                MolarMass = 105.988,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 851,
                    Density = 2.54,
                    DeltaHf = -1130.7
                }
            },
            ["K2CO3"] = new Compound
            {
                Formula = "K2CO3",
                CommonName = "Potassium carbonate",
                MolarMass = 138.205,
                Properties = new PhysicalProperties { DeltaHf = -1151.0 }
            },
            ["NaHCO3"] = new Compound
            {
                Formula = "NaHCO3",
                CommonName = "Sodium bicarbonate",
                MolarMass = 84.007,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 50,
                    Density = 2.20,
                    DeltaHf = -950.8
                }
            },
            ["KMnO4"] = new Compound
            {
                Formula = "KMnO4",
                CommonName = "Potassium permanganate",
                MolarMass = 158.034,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 240,
                    Density = 2.70,
                    DeltaHf = -837.2
                }
            },
            ["K2Cr2O7"] = new Compound
            {
                Formula = "K2Cr2O7",
                CommonName = "Potassium dichromate",
                MolarMass = 294.185,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 398,
                    Density = 2.68,
                    DeltaHf = -2061.5
                }
            },

            // ═══ ОРГАНИЧЕСКИЕ СОЕДИНЕНИЯ ═══

            // Алканы
            ["CH4"] = new Compound
            {
                Formula = "CH4",
                CommonName = "Methane",
                SMILES = "C",
                MolarMass = 16.043,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -182.5,
                    BoilingPoint = -161.5,
                    DeltaHf = -74.6
                }
            },
            ["C2H6"] = new Compound
            {
                Formula = "C2H6",
                CommonName = "Ethane",
                SMILES = "CC",
                MolarMass = 30.069,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -183.3,
                    BoilingPoint = -88.6,
                    DeltaHf = -84.0
                }
            },
            ["C3H8"] = new Compound
            {
                Formula = "C3H8",
                CommonName = "Propane",
                SMILES = "CCC",
                MolarMass = 44.096,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -187.7,
                    BoilingPoint = -42.1,
                    DeltaHf = -104.7
                }
            },
            ["C4H10"] = new Compound
            {
                Formula = "C4H10",
                CommonName = "Butane",
                SMILES = "CCCC",
                MolarMass = 58.122,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -138.3,
                    BoilingPoint = -0.5,
                    DeltaHf = -125.6
                }
            },
            ["C5H12"] = new Compound
            {
                Formula = "C5H12",
                CommonName = "Pentane",
                SMILES = "CCCCC",
                MolarMass = 72.149,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -129.7,
                    BoilingPoint = 36.1,
                    DeltaHf = -146.8
                }
            },
            ["C6H14"] = new Compound
            {
                Formula = "C6H14",
                CommonName = "Hexane",
                SMILES = "CCCCCC",
                MolarMass = 86.175,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -95,
                    BoilingPoint = 68.7,
                    DeltaHf = -167.2
                }
            },
            ["C7H16"] = new Compound
            {
                Formula = "C7H16",
                CommonName = "Heptane",
                SMILES = "CCCCCCC",
                MolarMass = 100.202,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -90.6,
                    BoilingPoint = 98.4,
                    DeltaHf = -187.8
                }
            },
            ["C8H18"] = new Compound
            {
                Formula = "C8H18",
                CommonName = "Octane",
                SMILES = "CCCCCCCC",
                MolarMass = 114.229,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -56.8,
                    BoilingPoint = 125.7,
                    DeltaHf = -208.5
                }
            },

            // Алкены
            ["C2H4"] = new Compound
            {
                Formula = "C2H4",
                CommonName = "Ethylene",
                SMILES = "C=C",
                MolarMass = 28.053,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -169.2,
                    BoilingPoint = -103.7,
                    DeltaHf = 52.4
                }
            },
            ["C3H6"] = new Compound
            {
                Formula = "C3H6",
                CommonName = "Propylene",
                SMILES = "CC=C",
                MolarMass = 42.080,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -185.2,
                    BoilingPoint = -47.6,
                    DeltaHf = 20.0
                }
            },
            ["C4H8"] = new Compound
            {
                Formula = "C4H8",
                CommonName = "1-Butene",
                SMILES = "CCC=C",
                MolarMass = 56.107,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -185.3,
                    BoilingPoint = -6.3,
                    DeltaHf = -0.1
                }
            },

            // Алкины
            ["C2H2"] = new Compound
            {
                Formula = "C2H2",
                CommonName = "Acetylene",
                SMILES = "C#C",
                MolarMass = 26.037,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -80.8,
                    BoilingPoint = -84,
                    DeltaHf = 227.4
                }
            },

            // Циклические углеводороды
            ["C6H12"] = new Compound
            {
                Formula = "C6H12",
                CommonName = "Cyclohexane",
                SMILES = "C1CCCCC1",
                MolarMass = 84.159,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 6.5,
                    BoilingPoint = 80.7,
                    DeltaHf = -156.4
                }
            },

            // Ароматические
            ["C6H6"] = new Compound
            {
                Formula = "C6H6",
                CommonName = "Benzene",
                SMILES = "c1ccccc1",
                MolarMass = 78.112,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 5.5,
                    BoilingPoint = 80.1,
                    Density = 0.88,
                    DeltaHf = 49.1
                }
            },
            ["C7H8"] = new Compound
            {
                Formula = "C7H8",
                CommonName = "Toluene",
                SMILES = "Cc1ccccc1",
                MolarMass = 92.138,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -95,
                    BoilingPoint = 110.6,
                    Density = 0.87,
                    DeltaHf = 12.0
                }
            },
            ["C8H10"] = new Compound
            {
                Formula = "C8H10",
                CommonName = "Xylene",
                SMILES = "Cc1ccccc1C",
                MolarMass = 106.165,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -25.2,
                    BoilingPoint = 144.4,
                    DeltaHf = -24.4
                }
            },
            ["C10H8"] = new Compound
            {
                Formula = "C10H8",
                CommonName = "Naphthalene",
                SMILES = "c1ccc2ccccc2c1",
                MolarMass = 128.171,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 80.2,
                    BoilingPoint = 218,
                    DeltaHf = 78.5
                }
            },

            // Спирты
            ["CH3OH"] = new Compound
            {
                Formula = "CH4O",
                CommonName = "Methanol",
                SMILES = "CO",
                MolarMass = 32.042,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -97.6,
                    BoilingPoint = 64.7,
                    Density = 0.79,
                    DeltaHf = -238.4,
                    PKa = 15.5
                }
            },
            ["C2H5OH"] = new Compound
            {
                Formula = "C2H6O",
                CommonName = "Ethanol",
                SMILES = "CCO",
                MolarMass = 46.068,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -114.1,
                    BoilingPoint = 78.4,
                    Density = 0.79,
                    DeltaHf = -277.0,
                    PKa = 15.9
                }
            },
            ["C3H7OH"] = new Compound
            {
                Formula = "C3H8O",
                CommonName = "Propanol",
                SMILES = "CCCO",
                MolarMass = 60.095,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -126.5,
                    BoilingPoint = 97.2,
                    DeltaHf = -302.6
                }
            },
            ["C4H9OH"] = new Compound
            {
                Formula = "C4H10O",
                CommonName = "Butanol",
                SMILES = "CCCCO",
                MolarMass = 74.121,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -89.8,
                    BoilingPoint = 117.7,
                    DeltaHf = -327.3
                }
            },
            ["C6H5OH"] = new Compound
            {
                Formula = "C6H6O",
                CommonName = "Phenol",
                SMILES = "Oc1ccccc1",
                MolarMass = 94.111,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 40.5,
                    BoilingPoint = 181.7,
                    DeltaHf = -165.0,
                    PKa = 9.95
                }
            },
            ["C2H6O2"] = new Compound
            {
                Formula = "C2H6O2",
                CommonName = "Ethylene glycol",
                SMILES = "OCCO",
                MolarMass = 62.068,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -12.9,
                    BoilingPoint = 197.3,
                    Density = 1.11,
                    DeltaHf = -454.8
                }
            },
            ["C3H8O3"] = new Compound
            {
                Formula = "C3H8O3",
                CommonName = "Glycerol",
                SMILES = "OCC(O)CO",
                MolarMass = 92.094,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 18,
                    BoilingPoint = 290,
                    Density = 1.26,
                    DeltaHf = -669.6
                }
            },

            // Эфиры
            ["C2H6O"] = new Compound
            {
                Formula = "C2H6O",
                CommonName = "Dimethyl ether",
                SMILES = "COC",
                MolarMass = 46.068,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -141.5,
                    BoilingPoint = -24.8,
                    DeltaHf = -184.1
                }
            },
            ["C4H10O"] = new Compound
            {
                Formula = "C4H10O",
                CommonName = "Diethyl ether",
                SMILES = "CCOCC",
                MolarMass = 74.121,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -116.3,
                    BoilingPoint = 34.6,
                    Density = 0.71,
                    DeltaHf = -279.5
                }
            },

            // Альдегиды
            ["HCHO"] = new Compound
            {
                Formula = "CH2O",
                CommonName = "Formaldehyde",
                SMILES = "C=O",
                MolarMass = 30.026,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -92,
                    BoilingPoint = -19.1,
                    DeltaHf = -108.6
                }
            },
            ["CH3CHO"] = new Compound
            {
                Formula = "C2H4O",
                CommonName = "Acetaldehyde",
                SMILES = "CC=O",
                MolarMass = 44.053,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -123.5,
                    BoilingPoint = 20.2,
                    DeltaHf = -166.2
                }
            },
            ["C6H5CHO"] = new Compound
            {
                Formula = "C7H6O",
                CommonName = "Benzaldehyde",
                SMILES = "O=Cc1ccccc1",
                MolarMass = 106.122,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -26,
                    BoilingPoint = 179,
                    DeltaHf = -87.0
                }
            },

            // Кетоны
            ["CH3COCH3"] = new Compound
            {
                Formula = "C3H6O",
                CommonName = "Acetone",
                SMILES = "CC(=O)C",
                MolarMass = 58.080,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -94.7,
                    BoilingPoint = 56.1,
                    Density = 0.79,
                    DeltaHf = -248.4
                }
            },
            ["C4H8O"] = new Compound
            {
                Formula = "C4H8O",
                CommonName = "Butanone",
                SMILES = "CCC(=O)C",
                MolarMass = 72.106,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -86.6,
                    BoilingPoint = 79.6,
                    DeltaHf = -273.3
                }
            },

            // Карбоновые кислоты
            ["HCOOH"] = new Compound
            {
                Formula = "CH2O2",
                CommonName = "Formic acid",
                SMILES = "C(=O)O",
                MolarMass = 46.025,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 8.4,
                    BoilingPoint = 100.8,
                    Density = 1.22,
                    DeltaHf = -425.0,
                    PKa = 3.75
                }
            },
            ["CH3COOH"] = new Compound
            {
                Formula = "C2H4O2",
                CommonName = "Acetic acid",
                SMILES = "CC(=O)O",
                MolarMass = 60.052,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 16.6,
                    BoilingPoint = 118.1,
                    Density = 1.05,
                    DeltaHf = -484.3,
                    PKa = 4.76
                }
            },
            ["C3H6O2"] = new Compound
            {
                Formula = "C3H6O2",
                CommonName = "Propionic acid",
                SMILES = "CCC(=O)O",
                MolarMass = 74.079,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -20.5,
                    BoilingPoint = 141,
                    DeltaHf = -510.7,
                    PKa = 4.87
                }
            },
            ["C4H8O2"] = new Compound
            {
                Formula = "C4H8O2",
                CommonName = "Butyric acid",
                SMILES = "CCCC(=O)O",
                MolarMass = 88.106,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -5.5,
                    BoilingPoint = 163.5,
                    DeltaHf = -533.9,
                    PKa = 4.82
                }
            },
            ["C6H5COOH"] = new Compound
            {
                Formula = "C7H6O2",
                CommonName = "Benzoic acid",
                SMILES = "O=C(O)c1ccccc1",
                MolarMass = 122.121,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 122.4,
                    BoilingPoint = 249,
                    DeltaHf = -385.2,
                    PKa = 4.20
                }
            },
            ["C2H4O2"] = new Compound
            {
                Formula = "C2H4O2",
                CommonName = "Oxalic acid",
                SMILES = "C(=O)(C(=O)O)O",
                MolarMass = 90.035,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 189.5,
                    PKa = 1.25
                }
            },

            // Сложные эфиры
            ["CH3COOCH3"] = new Compound
            {
                Formula = "C3H6O2",
                CommonName = "Methyl acetate",
                SMILES = "CC(=O)OC",
                MolarMass = 74.079,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -98.1,
                    BoilingPoint = 56.9,
                    DeltaHf = -445.9
                }
            },
            ["CH3COOC2H5"] = new Compound
            {
                Formula = "C4H8O2",
                CommonName = "Ethyl acetate",
                SMILES = "CC(=O)OCC",
                MolarMass = 88.106,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -83.6,
                    BoilingPoint = 77.1,
                    Density = 0.90,
                    DeltaHf = -479.3
                }
            },

            // Амины
            ["CH3NH2"] = new Compound
            {
                Formula = "CH5N",
                CommonName = "Methylamine",
                SMILES = "CN",
                MolarMass = 31.057,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -93.5,
                    BoilingPoint = -6.3,
                    DeltaHf = -22.5,
                    PKa = 10.66
                }
            },
            ["C2H5NH2"] = new Compound
            {
                Formula = "C2H7N",
                CommonName = "Ethylamine",
                SMILES = "CCN",
                MolarMass = 45.084,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -81,
                    BoilingPoint = 16.6,
                    DeltaHf = -47.5,
                    PKa = 10.81
                }
            },
            ["C6H5NH2"] = new Compound
            {
                Formula = "C6H7N",
                CommonName = "Aniline",
                SMILES = "Nc1ccccc1",
                MolarMass = 93.127,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -6.3,
                    BoilingPoint = 184.1,
                    Density = 1.02,
                    DeltaHf = 31.3,
                    PKa = 4.87
                }
            },
            ["(CH3)2NH"] = new Compound
            {
                Formula = "C2H7N",
                CommonName = "Dimethylamine",
                SMILES = "CNC",
                MolarMass = 45.084,
                Properties = new PhysicalProperties
                {
                    BoilingPoint = 7.4,
                    PKa = 10.73
                }
            },
            ["(CH3)3N"] = new Compound
            {
                Formula = "C3H9N",
                CommonName = "Trimethylamine",
                SMILES = "CN(C)C",
                MolarMass = 59.110,
                Properties = new PhysicalProperties
                {
                    BoilingPoint = 2.9,
                    PKa = 9.80
                }
            },

            // Амиды
            ["CH3CONH2"] = new Compound
            {
                Formula = "C2H5NO",
                CommonName = "Acetamide",
                SMILES = "CC(=O)N",
                MolarMass = 59.067,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 81,
                    BoilingPoint = 222,
                    DeltaHf = -317.0
                }
            },
            ["H2NCONH2"] = new Compound
            {
                Formula = "CH4N2O",
                CommonName = "Urea",
                SMILES = "NC(=O)N",
                MolarMass = 60.055,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 133,
                    DeltaHf = -333.1
                }
            },

            // Нитрилы
            ["CH3CN"] = new Compound
            {
                Formula = "C2H3N",
                CommonName = "Acetonitrile",
                SMILES = "CC#N",
                MolarMass = 41.052,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -45.7,
                    BoilingPoint = 81.6,
                    Density = 0.79,
                    DeltaHf = 40.6
                }
            },

            // Нитросоединения
            ["CH3NO2"] = new Compound
            {
                Formula = "CH3NO2",
                CommonName = "Nitromethane",
                SMILES = "C[N+](=O)[O-]",
                MolarMass = 61.040,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -28.6,
                    BoilingPoint = 101.2,
                    DeltaHf = -113.1
                }
            },
            ["C6H5NO2"] = new Compound
            {
                Formula = "C6H5NO2",
                CommonName = "Nitrobenzene",
                SMILES = "[O-][N+](=O)c1ccccc1",
                MolarMass = 123.110,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 5.7,
                    BoilingPoint = 210.9,
                    DeltaHf = 12.5
                }
            },

            // Галогенпроизводные
            ["CH3Cl"] = new Compound
            {
                Formula = "CH3Cl",
                CommonName = "Methyl chloride",
                SMILES = "CCl",
                MolarMass = 50.488,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -97.4,
                    BoilingPoint = -24.1,
                    DeltaHf = -81.9
                }
            },
            ["CH2Cl2"] = new Compound
            {
                Formula = "CH2Cl2",
                CommonName = "Dichloromethane",
                SMILES = "ClCCl",
                MolarMass = 84.933,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -96.7,
                    BoilingPoint = 39.6,
                    Density = 1.33,
                    DeltaHf = -121.5
                }
            },
            ["CHCl3"] = new Compound
            {
                Formula = "CHCl3",
                CommonName = "Chloroform",
                SMILES = "ClC(Cl)Cl",
                MolarMass = 119.378,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -63.5,
                    BoilingPoint = 61.2,
                    Density = 1.48,
                    DeltaHf = -134.1
                }
            },
            ["CCl4"] = new Compound
            {
                Formula = "CCl4",
                CommonName = "Carbon tetrachloride",
                SMILES = "ClC(Cl)(Cl)Cl",
                MolarMass = 153.823,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -22.9,
                    BoilingPoint = 76.7,
                    Density = 1.59,
                    DeltaHf = -95.7
                }
            },
            ["CH3Br"] = new Compound
            {
                Formula = "CH3Br",
                CommonName = "Methyl bromide",
                SMILES = "CBr",
                MolarMass = 94.939,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -93.7,
                    BoilingPoint = 3.6,
                    DeltaHf = -35.4
                }
            },
            ["CH2Br2"] = new Compound
            {
                Formula = "CH2Br2",
                CommonName = "Dibromomethane",
                SMILES = "BrCBr",
                MolarMass = 173.835,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -52.5,
                    BoilingPoint = 97
                }
            },
            ["CH3I"] = new Compound
            {
                Formula = "CH3I",
                CommonName = "Methyl iodide",
                SMILES = "CI",
                MolarMass = 141.939,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -66.4,
                    BoilingPoint = 42.4,
                    Density = 2.28,
                    DeltaHf = 14.4
                }
            },
            ["C2H5Cl"] = new Compound
            {
                Formula = "C2H5Cl",
                CommonName = "Ethyl chloride",
                SMILES = "CCCl",
                MolarMass = 64.514,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -136.4,
                    BoilingPoint = 12.3,
                    DeltaHf = -112.2
                }
            },
            ["C6H5Cl"] = new Compound
            {
                Formula = "C6H5Cl",
                CommonName = "Chlorobenzene",
                SMILES = "Clc1ccccc1",
                MolarMass = 112.557,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -45.2,
                    BoilingPoint = 131.7,
                    DeltaHf = 11.3
                }
            },

            // Аминокислоты
            ["C2H5NO2"] = new Compound
            {
                Formula = "C2H5NO2",
                CommonName = "Glycine",
                SMILES = "NCC(=O)O",
                MolarMass = 75.067,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 233,
                    DeltaHf = -537.2
                }
            },
            ["C3H7NO2"] = new Compound
            {
                Formula = "C3H7NO2",
                CommonName = "Alanine",
                SMILES = "CC(N)C(=O)O",
                MolarMass = 89.093,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 297,
                    DeltaHf = -604.0
                }
            },

            // Углеводы
            ["C6H12O6"] = new Compound
            {
                Formula = "C6H12O6",
                CommonName = "Glucose",
                SMILES = "OC[C@H]1OC(O)[C@H](O)[C@@H](O)[C@@H]1O",
                MolarMass = 180.156,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 146,
                    DeltaHf = -1268.0
                }
            },
            ["C12H22O11"] = new Compound
            {
                Formula = "C12H22O11",
                CommonName = "Sucrose",
                MolarMass = 342.297,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 186,
                    DeltaHf = -2222.0
                }
            },

            // Биологически активные
            ["C9H8O4"] = new Compound
            {
                Formula = "C9H8O4",
                CommonName = "Aspirin",
                SMILES = "CC(=O)Oc1ccccc1C(=O)O",
                MolarMass = 180.158,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 135,
                    DeltaHf = -1000.0
                }
            },
            ["C8H9NO2"] = new Compound
            {
                Formula = "C8H9NO2",
                CommonName = "Paracetamol",
                SMILES = "CC(=O)Nc1ccc(O)cc1",
                MolarMass = 151.163,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = 169
                }
            },

            // Дополнительные важные соединения
            ["CS2"] = new Compound
            {
                Formula = "CS2",
                CommonName = "Carbon disulfide",
                SMILES = "S=C=S",
                MolarMass = 76.141,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -111.6,
                    BoilingPoint = 46.3,
                    Density = 1.26,
                    DeltaHf = 89.0
                }
            },
            ["COCl2"] = new Compound
            {
                Formula = "COCl2",
                CommonName = "Phosgene",
                SMILES = "ClC(=O)Cl",
                MolarMass = 98.916,
                Properties = new PhysicalProperties
                {
                    MeltingPoint = -118,
                    BoilingPoint = 8.3,
                    DeltaHf = -223.0
                }
            }
        };
    }

    private void InitializeThermodynamicData()
    {
        // Стандартные энтальпии образования (кДж/моль)
        _standardEnthalpies = new Dictionary<string, double>
        {
            ["H2O(l)"] = -285.8,
            ["H2O(g)"] = -241.8,
            ["CO2(g)"] = -393.5,
            ["CH4(g)"] = -74.6,
            ["C2H6(g)"] = -84.0,
            ["C2H4(g)"] = 52.4,
            ["C2H2(g)"] = 227.4,
            ["NH3(g)"] = -45.9,
            ["NO(g)"] = 91.3,
            ["NO2(g)"] = 33.2,
            ["SO2(g)"] = -296.8,
            ["H2SO4(l)"] = -814.0,
            ["HCl(g)"] = -92.3,
            ["NaCl(s)"] = -411.2,
            ["CaCO3(s)"] = -1206.9,
            ["CaO(s)"] = -635.1,
            ["Fe2O3(s)"] = -824.2,
            ["Al2O3(s)"] = -1675.7
        };
    }

    private void InitializeElectrochemicalData()
    {
        _standardPotentials = new Dictionary<string, double>
        {
            ["Zn2+/Zn"] = -0.76,
            ["Fe2+/Fe"] = -0.44,
            ["Ni2+/Ni"] = -0.25,
            ["Sn2+/Sn"] = -0.14,
            ["Pb2+/Pb"] = -0.13,
            ["H+/H2"] = 0.00,
            ["Cu2+/Cu"] = 0.34,
            ["I2/I-"] = 0.54,
            ["Ag+/Ag"] = 0.80,
            ["Br2/Br-"] = 1.07,
            ["Cl2/Cl-"] = 1.36,
            ["MnO4-/Mn2+"] = 1.51,
            ["F2/F-"] = 2.87
        };
    }

    public Element GetElement(string symbol)
    {
        return _elements.TryGetValue(symbol, out var element) ? element : null;
    }

    public Compound LookupCompound(string identifier)
    {
        // Поиск по формуле
        if (_compounds.TryGetValue(identifier, out var compound))
            return compound;

        // Поиск по названию
        return _compounds.Values.FirstOrDefault(c =>
            c.CommonName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
    }

    public double? GetStandardEnthalpy(string formula)
    {
        return _standardEnthalpies.TryGetValue(formula, out var value) ? value : null;
    }

    public double? GetStandardPotential(string halfReaction)
    {
        return _standardPotentials.TryGetValue(halfReaction, out var value) ? value : null;
    }

    public void LoadFromJson(string path)
    {
        // Загрузка дополнительных данных из JSON
    }
}


using AI.DataStructs.Algebraic;
using AI.Solvers.Chem.Quantum;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Метод Хюккеля: спектр орбиталей, заряды, порядки связей, ароматичность.</summary>
public class QuantumTests
{
    /// <summary>Этилен: две орбитали с энергиями alpha +- beta, выигрыша делокализации нет.</summary>
    [Fact]
    public void Huckel_Ethylene()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Chain(2));

        Assert.Equal(1.0, solution.OrbitalCoefficients[0], 9);
        Assert.Equal(-1.0, solution.OrbitalCoefficients[1], 9);
        Assert.Equal(2.0, solution.TotalEnergy, 9);
        Assert.Equal(0.0, solution.DelocalizationEnergy, 9);
        Assert.Equal(1.0, solution.BondOrder(0, 1), 9);
    }

    /// <summary>Бутадиен: спектр по золотому сечению, порядки связей 0.894 и 0.447.</summary>
    [Fact]
    public void Huckel_Butadiene()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Chain(4));

        Assert.Equal(1.618034, solution.OrbitalCoefficients[0], 6);
        Assert.Equal(0.618034, solution.OrbitalCoefficients[1], 6);
        Assert.Equal(-0.618034, solution.OrbitalCoefficients[2], 6);
        Assert.Equal(-1.618034, solution.OrbitalCoefficients[3], 6);

        Assert.Equal(4.472136, solution.TotalEnergy, 6);
        Assert.Equal(0.472136, solution.DelocalizationEnergy, 6);

        // Крайние связи короче средней: порядок 0.894 против 0.447
        Assert.Equal(0.894427, solution.BondOrder(0, 1), 6);
        Assert.Equal(0.447214, solution.BondOrder(1, 2), 6);
        Assert.Equal(0.894427, solution.BondOrder(2, 3), 6);

        foreach (double density in solution.ChargeDensities)
            Assert.Equal(1.0, density, 9);
    }

    /// <summary>Бензол: спектр 2, 1, 1, -1, -1, -2 и энергия делокализации 2·beta.</summary>
    [Fact]
    public void Huckel_Benzene()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Ring(6));

        var expected = new[] { 2.0, 1.0, 1.0, -1.0, -1.0, -2.0 };

        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], solution.OrbitalCoefficients[i], 6);

        Assert.Equal(8.0, solution.TotalEnergy, 6);
        Assert.Equal(2.0, solution.DelocalizationEnergy, 6);
        Assert.Equal(2.0, solution.Gap, 6);
        Assert.True(solution.ObeysHuckelRule);

        for (int atom = 0; atom < 6; atom++)
        {
            Assert.Equal(1.0, solution.ChargeDensities[atom], 6);
            Assert.Equal(0.0, solution.Charges[atom], 6);
            Assert.Equal(2.0 / 3, solution.BondOrder(atom, (atom + 1) % 6), 6);
            Assert.Equal(Math.Sqrt(3) - (4.0 / 3), solution.FreeValences[atom], 6);
        }
    }

    /// <summary>
    /// Циклобутадиен: четыре электрона, из них два на несвязывающих орбиталях,
    /// выигрыша делокализации нет - система антиароматична.
    /// </summary>
    [Fact]
    public void Huckel_CyclobutadieneIsAntiaromatic()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Ring(4));

        Assert.Equal(2.0, solution.OrbitalCoefficients[0], 6);
        Assert.Equal(0.0, solution.OrbitalCoefficients[1], 6);
        Assert.Equal(0.0, solution.OrbitalCoefficients[2], 6);
        Assert.Equal(-2.0, solution.OrbitalCoefficients[3], 6);

        Assert.Equal(4.0, solution.TotalEnergy, 6);
        Assert.Equal(0.0, solution.DelocalizationEnergy, 6);
        Assert.Equal(0.0, solution.Gap, 6);
        Assert.False(solution.ObeysHuckelRule);
    }

    /// <summary>Аллильный катион: два электрона на связывающей орбитали, спектр по корню из двух.</summary>
    [Fact]
    public void Huckel_AllylCation()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Ring(3, charge: 1));

        Assert.Equal(2, solution.Electrons);
        Assert.Equal(2.0, solution.OrbitalCoefficients[0], 6);
        Assert.Equal(4.0, solution.TotalEnergy, 6);

        // Заряд поровну распределён по трём центрам
        foreach (double charge in solution.Charges)
            Assert.Equal(1.0 / 3, charge, 6);
    }

    /// <summary>Заселённость подчиняется принципу заполнения снизу.</summary>
    [Fact]
    public void Huckel_FillsOrbitalsFromBottom()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Chain(5));

        Assert.Equal(new[] { 2, 2, 1, 0, 0 }, solution.Occupations);
        Assert.Equal(2, solution.HomoIndex);
        Assert.Equal(3, solution.LumoIndex);
    }

    /// <summary>Оценка длинноволновой полосы: щель бензола 2·beta даёт около 230 нм.</summary>
    [Fact]
    public void Huckel_EstimatesAbsorptionBand()
    {
        HuckelSolution solution = Huckel.Solve(PiSystem.Ring(6));

        Assert.Equal(5.4, solution.ExcitationEnergy(), 6);
        Assert.Equal(229.6, solution.AbsorptionWavelength(), 1);
        Assert.Equal(2.7, solution.Hardness(), 6);

        // С ростом цепи щель сужается, а полоса уходит в длинные волны
        HuckelSolution longer = Huckel.Solve(PiSystem.Chain(10));

        Assert.True(longer.AbsorptionWavelength() > solution.AbsorptionWavelength());
    }

    /// <summary>Гетероатом с положительной поправкой h стягивает на себя электронную плотность.</summary>
    [Fact]
    public void Huckel_HeteroatomAttractsDensity()
    {
        var system = new PiSystem();
        int carbon = system.AddCenter("C");
        int nitrogen = system.AddCenter("N", 1, 0.5);
        system.AddBond(carbon, nitrogen);

        HuckelSolution solution = Huckel.Solve(system);

        Assert.True(solution.ChargeDensities[nitrogen] > 1, "плотность должна смещаться к азоту");
        Assert.True(solution.Charges[nitrogen] < 0);
        Assert.Equal(0, solution.Charges[carbon] + solution.Charges[nitrogen], 9);
    }

    /// <summary>Сопряжённая система выделяется из SMILES: бензол даёт шесть центров.</summary>
    [Fact]
    public void PiSystem_ExtractsBenzeneFromSmiles()
    {
        PiSystem system = PiSystem.FromSmiles("c1ccccc1");

        Assert.Equal(6, system.Count);
        Assert.Equal(6, system.Electrons);

        HuckelSolution solution = Huckel.Solve(system);

        Assert.Equal(8.0, solution.TotalEnergy, 6);
    }

    /// <summary>Пиридиновый азот отдаёт один электрон, пиррольный - два.</summary>
    [Fact]
    public void PiSystem_CountsHeteroatomElectrons()
    {
        PiSystem pyridine = PiSystem.FromSmiles("c1ccncc1");
        PiSystem pyrrole = PiSystem.FromSmiles("c1cc[nH]c1");

        Assert.Equal(6, pyridine.Count);
        Assert.Equal(6, pyridine.Electrons);

        Assert.Equal(5, pyrrole.Count);
        Assert.Equal(6, pyrrole.Electrons);

        HuckelSolution solution = Huckel.Solve(pyridine);
        int nitrogen = pyridine.Centers.ToList().FindIndex(c => c.Element == "N");

        Assert.True(solution.Charges[nitrogen] < 0, "на пиридиновом азоте должен быть избыток плотности");
    }

    /// <summary>Насыщенная молекула не имеет сопряжённой системы.</summary>
    [Fact]
    public void PiSystem_RejectsSaturatedMolecule()
    {
        Assert.Throws<ArgumentException>(() => PiSystem.FromSmiles("CCCC"));
        Assert.Throws<ArgumentException>(() => PiSystem.FromSmiles(" "));
    }

    /// <summary>Числа электронов больше, чем мест на орбиталях, быть не может.</summary>
    [Fact]
    public void Huckel_RejectsImpossibleOccupation()
    {
        PiSystem system = PiSystem.Ring(6);
        system.Charge = -10;

        Assert.Throws<ArgumentException>(() => Huckel.Solve(system));
    }

    /// <summary>При единичном перекрывании обобщённая задача совпадает с обычной.</summary>
    [Fact]
    public void Huckel_GeneralizedProblemWithUnitOverlap()
    {
        Matrix hamiltonian = PiSystem.Ring(6).TopologicalMatrix();
        var overlap = new Matrix(6, 6);

        for (int i = 0; i < 6; i++)
            overlap[i, i] = 1;

        var (energies, _) = Huckel.SolveGeneralized(hamiltonian, overlap);

        // Обобщённое решение упорядочено по возрастанию энергии в единицах beta
        Assert.Equal(-2.0, energies[0], 6);
        Assert.Equal(2.0, energies[5], 6);
    }

    /// <summary>Перекрывание сдвигает уровни: связывающая орбиталь опускается сильнее разрыхляющей.</summary>
    [Fact]
    public void Huckel_OverlapSplitsLevelsAsymmetrically()
    {
        var hamiltonian = new Matrix(2, 2);
        hamiltonian[0, 1] = 1;
        hamiltonian[1, 0] = 1;

        var overlap = new Matrix(2, 2);
        overlap[0, 0] = 1;
        overlap[1, 1] = 1;
        overlap[0, 1] = 0.25;
        overlap[1, 0] = 0.25;

        var (energies, _) = Huckel.SolveGeneralized(hamiltonian, overlap);

        // Уровни идут как +-1/(1+-S): 0.8 и -1.3333
        Assert.Equal(-1.0 / 0.75, energies[0], 6);
        Assert.Equal(1.0 / 1.25, energies[1], 6);
    }

    /// <summary>Вырожденная матрица перекрывания отвергается.</summary>
    [Fact]
    public void Huckel_RejectsSingularOverlap()
    {
        var hamiltonian = new Matrix(2, 2);
        var overlap = new Matrix(2, 2);

        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
                overlap[i, j] = 1;
        }

        Assert.Throws<ArgumentException>(() => Huckel.SolveGeneralized(hamiltonian, overlap));
    }

    /// <summary>Отчёт содержит основные величины решения.</summary>
    [Fact]
    public void Huckel_ReportMentionsKeyNumbers()
    {
        string report = Huckel.Solve(PiSystem.Ring(6)).Report();

        Assert.Contains("Метод Хюккеля", report);
        Assert.Contains("делокализации", report);
        Assert.Contains("ВЗМО", report);
    }
}

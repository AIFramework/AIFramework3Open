using AI.Solvers.Chem.Crystallography;
using AI.Solvers.Chem.Structures;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Кристаллография: размножение симметрией, дифрактограмма, индицирование, размер кристаллитов.</summary>
public class CrystallographyTests
{
    private const double NaClEdge = 5.6402;
    private const double CopperKAlpha = 1.5406;

    // Линии хлорида натрия на медном излучении: 111, 200, 220, 311, 222
    private static readonly double[] SodiumChlorideLines = { 27.37, 31.71, 45.45, 53.87, 56.47 };

    /// <summary>Асимметричная часть размножается трансляциями гранецентрированной решётки.</summary>
    [Fact]
    public void Crystal_ExpandsAsymmetricUnit()
    {
        Crystal crystal = Crystal.FromCif(StructureTests.SodiumChlorideCif);

        Assert.Equal(2, crystal.AsymmetricUnit.Count);
        Assert.Equal(8, crystal.AtomsInCell);
        Assert.Equal(4, crystal.FormulaUnits(2), 9);
        Assert.Equal(225, crystal.SpaceGroupNumber);
        Assert.Equal("Cl4Na4", crystal.Contents.Formula);
    }

    /// <summary>Рентгеновская плотность галита равна 2.16 г/см3.</summary>
    [Fact]
    public void Crystal_ComputesXRayDensity()
    {
        Crystal crystal = Crystal.FromCif(StructureTests.SodiumChlorideCif);

        Assert.Equal(2.163, crystal.Density(ChemTestContext.Database), 2);
    }

    /// <summary>Атом в частной позиции не размножается: совпавшие образы отбрасываются.</summary>
    [Fact]
    public void Crystal_DropsCoincidingImages()
    {
        var unit = new MolecularStructure { Cell = UnitCell.Cubic(4.0) };
        unit.AddFractional("Po", 0, 0, 0);

        var symmetry = new[]
        {
            SymmetryOperation.Parse("x,y,z"),
            SymmetryOperation.Parse("-x,-y,-z"),
            SymmetryOperation.Parse("-x,y,-z")
        };

        var crystal = new Crystal(unit, symmetry);

        Assert.Equal(1, crystal.AtomsInCell);
    }

    /// <summary>Кристаллу нужна ячейка: структура без неё отвергается.</summary>
    [Fact]
    public void Crystal_RequiresUnitCell()
    {
        var unit = new MolecularStructure();
        unit.Add("C", 0, 0, 0);

        Assert.Throws<ArgumentException>(() => new Crystal(unit));
    }

    /// <summary>Расчётная дифрактограмма галита воспроизводит табличные линии.</summary>
    [Fact]
    public void PowderPattern_ReproducesSodiumChlorideLines()
    {
        Crystal crystal = Crystal.FromCif(StructureTests.SodiumChlorideCif);
        PowderPattern pattern = PowderPattern.Calculate(crystal, ChemTestContext.Database, CopperKAlpha, 60);

        foreach (double expected in SodiumChlorideLines)
        {
            Assert.True(pattern.Reflections.Any(r => Math.Abs(r.TwoTheta - expected) < 0.05),
                $"нет линии около {expected} градусов: "
                + string.Join(", ", pattern.Reflections.Select(r => r.TwoTheta.ToString("F2"))));
        }
    }

    /// <summary>
    /// Гранецентрированная решётка гасит отражения со смешанной чётностью индексов:
    /// линий 100 и 110 в расчёте быть не должно.
    /// </summary>
    [Fact]
    public void PowderPattern_ObeysFaceCentredExtinctions()
    {
        Crystal crystal = Crystal.FromCif(StructureTests.SodiumChlorideCif);
        PowderPattern pattern = PowderPattern.Calculate(crystal, ChemTestContext.Database, CopperKAlpha, 60);

        Assert.DoesNotContain(pattern.Reflections, r => Math.Abs(r.Spacing - NaClEdge) < 0.01);
        Assert.DoesNotContain(pattern.Reflections, r => Math.Abs(r.Spacing - (NaClEdge / Math.Sqrt(2))) < 0.01);
    }

    /// <summary>Фактор повторяемости отражения 111 кубической решётки равен восьми.</summary>
    [Fact]
    public void PowderPattern_CountsMultiplicity()
    {
        Crystal crystal = Crystal.FromCif(StructureTests.SodiumChlorideCif);
        PowderPattern pattern = PowderPattern.Calculate(crystal, ChemTestContext.Database, CopperKAlpha, 60);

        Reflection first = pattern.Reflections.First(r => Math.Abs(r.TwoTheta - 27.37) < 0.05);
        Reflection second = pattern.Reflections.First(r => Math.Abs(r.TwoTheta - 31.71) < 0.05);

        Assert.Equal(8, first.Multiplicity);
        Assert.Equal(6, second.Multiplicity);
    }

    /// <summary>Синтезированный профиль даёт максимум в положении сильнейшей линии.</summary>
    [Fact]
    public void PowderPattern_ProfilePeaksAtStrongestLine()
    {
        Crystal crystal = Crystal.FromCif(StructureTests.SodiumChlorideCif);
        PowderPattern pattern = PowderPattern.Calculate(crystal, ChemTestContext.Database, CopperKAlpha, 60);

        var (angles, intensity) = pattern.Profile(20, 60, 0.02, 0.1);

        int peak = 0;

        for (int i = 1; i < intensity.Length; i++)
        {
            if (intensity[i] > intensity[peak])
                peak = i;
        }

        Reflection strongest = pattern.Reflections.OrderByDescending(r => r.Intensity).First();

        Assert.Equal(strongest.TwoTheta, angles[peak], 1);
    }

    /// <summary>Переход угол - межплоскостное расстояние обратим.</summary>
    [Fact]
    public void PowderAnalysis_SpacingRoundTrip()
    {
        double spacing = PowderAnalysis.SpacingFromAngle(31.71, CopperKAlpha);

        Assert.Equal(2.8201, spacing, 3);
        Assert.Equal(31.71, PowderAnalysis.AngleFromSpacing(spacing, CopperKAlpha), 6);
    }

    /// <summary>Дифрактограмма галита индицируется в кубической гранецентрированной решётке.</summary>
    [Fact]
    public void PowderAnalysis_IndexesSodiumChloride()
    {
        IndexingResult result = PowderAnalysis.IndexCubic(SodiumChlorideLines, CopperKAlpha);

        Assert.NotNull(result);
        Assert.Equal(NaClEdge, result.Cell.A, 2);
        Assert.Equal(LatticeCentering.FaceCentred, result.Centering);
        Assert.Equal("111", result.Lines[0].Indices);
        Assert.Equal("200", result.Lines[1].Indices);
        Assert.Equal("220", result.Lines[2].Indices);
        Assert.True(result.MaxDeviation < 0.05, $"расхождение {result.MaxDeviation:F3} градуса слишком велико");
    }

    /// <summary>Объёмноцентрированная решётка отличается от гранецентрированной по набору линий.</summary>
    [Fact]
    public void PowderAnalysis_RecognizesBodyCentredLattice()
    {
        UnitCell iron = UnitCell.Cubic(2.8665);
        var lines = new[]
        {
            2 * iron.BraggAngle(1, 1, 0, CopperKAlpha),
            2 * iron.BraggAngle(2, 0, 0, CopperKAlpha),
            2 * iron.BraggAngle(2, 1, 1, CopperKAlpha)
        };

        IndexingResult result = PowderAnalysis.IndexCubic(lines, CopperKAlpha);

        Assert.NotNull(result);
        Assert.Equal(2.8665, result.Cell.A, 3);
        Assert.Equal(LatticeCentering.BodyCentred, result.Centering);
    }

    /// <summary>Уточнение по методу наименьших квадратов подтягивает грубое приближение ячейки.</summary>
    [Fact]
    public void PowderAnalysis_RefinesCellEdge()
    {
        IndexingResult indexed = PowderAnalysis.IndexCubic(SodiumChlorideLines, CopperKAlpha);

        var (cell, fit) = PowderAnalysis.RefineCell(indexed.Lines, UnitCell.Cubic(5.5), CopperKAlpha);

        Assert.Equal(NaClEdge, cell.A, 2);
        Assert.True(fit.ResidualStd < 0.05, $"остаточное СКО {fit.ResidualStd:F4} градуса слишком велико");
    }

    /// <summary>Уточнение тетрагональной ячейки восстанавливает оба параметра.</summary>
    [Fact]
    public void PowderAnalysis_RefinesTetragonalCell()
    {
        UnitCell truth = UnitCell.Tetragonal(3.7852, 9.5139);
        var reflections = new[] { (1, 0, 1), (1, 1, 0), (2, 0, 0), (1, 0, 5), (2, 1, 1) };

        var lines = reflections
            .Select(r => new IndexedLine(
                2 * truth.BraggAngle(r.Item1, r.Item2, r.Item3, CopperKAlpha),
                truth.InterplanarSpacing(r.Item1, r.Item2, r.Item3),
                r.Item1, r.Item2, r.Item3, 0))
            .ToArray();

        var (cell, _) = PowderAnalysis.RefineCell(lines, UnitCell.Tetragonal(3.7, 9.3), CopperKAlpha,
            CrystalSystem.Tetragonal);

        Assert.Equal(3.7852, cell.A, 2);
        Assert.Equal(9.5139, cell.C, 2);
    }

    /// <summary>Размер кристаллитов по Шерреру: ширина 0.2 градуса при 31.7 даёт около 41 нм.</summary>
    [Fact]
    public void PowderAnalysis_ScherrerSize()
    {
        double size = PowderAnalysis.ScherrerSize(31.71, 0.2, CopperKAlpha);

        Assert.Equal(412.8, size, 0);
    }

    /// <summary>Инструментальная ширина вычитается квадратично и увеличивает найденный размер.</summary>
    [Fact]
    public void PowderAnalysis_ScherrerRemovesInstrumentalWidth()
    {
        double narrow = PowderAnalysis.ScherrerSize(31.71, 0.2, CopperKAlpha, 0.9, 0.1);
        double wide = PowderAnalysis.ScherrerSize(31.71, 0.2, CopperKAlpha);

        Assert.True(narrow > wide);
        Assert.Equal(wide * 0.2 / Math.Sqrt(0.03), narrow, 6);
        Assert.True(double.IsPositiveInfinity(PowderAnalysis.ScherrerSize(31.71, 0.1, CopperKAlpha, 0.9, 0.1)));
    }

    /// <summary>Метод Вильямсона-Холла разделяет размер кристаллитов и микродеформацию.</summary>
    [Fact]
    public void PowderAnalysis_WilliamsonHallSeparatesSizeAndStrain()
    {
        const double size = 200.0;
        const double strain = 0.002;

        var angles = new[] { 20.0, 35.0, 50.0, 65.0, 80.0 };
        var widths = new double[angles.Length];

        for (int i = 0; i < angles.Length; i++)
        {
            double theta = angles[i] * Math.PI / 360;
            double beta = (0.9 * CopperKAlpha / (size * Math.Cos(theta))) + (4 * strain * Math.Tan(theta));

            widths[i] = beta * 180 / Math.PI;
        }

        var (foundSize, foundStrain, r2) = PowderAnalysis.WilliamsonHall(angles, widths, CopperKAlpha);

        Assert.Equal(size, foundSize, 0);
        Assert.Equal(strain, foundStrain, 6);
        Assert.True(r2 > 0.999, $"линеаризация должна быть точной, R2 = {r2:F4}");
    }

    /// <summary>Количественный анализ по корундовым числам нормирует доли на сотню.</summary>
    [Fact]
    public void PowderAnalysis_QuantifiesPhasesByRir()
    {
        var phases = PowderAnalysis.QuantifyByRir(
            new[] { "кварц", "кальцит", "корунд" },
            new[] { 100.0, 50.0, 25.0 },
            new[] { 3.41, 3.41, 1.0 });

        Assert.Equal(100, phases.Sum(p => p.MassFraction), 9);

        // Приведённые интенсивности 100/3.41, 50/3.41 и 25/1 дают в сумме 68.99
        Assert.Equal(42.508, phases[0].MassFraction, 2);
        Assert.Equal(21.254, phases[1].MassFraction, 2);
        Assert.Equal(36.238, phases[2].MassFraction, 2);

        // Кальцит вдвое слабее кварца при том же корундовом числе - и доля вдвое меньше
        Assert.Equal(2.0, phases[0].MassFraction / phases[1].MassFraction, 9);
    }

    /// <summary>Несогласованные данные фазового анализа отвергаются.</summary>
    [Fact]
    public void PowderAnalysis_RejectsInconsistentInput()
    {
        Assert.Throws<ArgumentException>(() => PowderAnalysis.QuantifyByRir(
            new[] { "кварц" }, new[] { 100.0, 50.0 }, new[] { 3.41, 3.41 }));

        Assert.Throws<ArgumentException>(() => PowderAnalysis.QuantifyByRir(
            new[] { "кварц" }, new[] { 100.0 }, new[] { 0.0 }));

        Assert.Throws<ArgumentException>(() => PowderAnalysis.IndexCubic(new[] { 31.71 }, CopperKAlpha));
    }
}

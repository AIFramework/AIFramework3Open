using AI.Solvers.Chem.Dynamics;
using AI.Solvers.Chem.Structures;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Молекулярная динамика: совмещение структур, функция распределения, диффузия, водородные связи.</summary>
public class DynamicsTests
{
    /// <summary>Повёрнутая и сдвинутая структура совмещается с образцом точно.</summary>
    [Fact]
    public void Alignment_RecoversRigidBodyMotion()
    {
        MolecularStructure reference = Methane();
        MolecularStructure moved = Rotate(reference, 30, new Vector3(2.5, -1.0, 0.7));

        Assert.True(StructureAlignment.Rmsd(moved, reference) > 1, "до совмещения структуры не совпадают");

        AlignmentResult result = StructureAlignment.Align(moved, reference);

        Assert.Equal(0, result.Rmsd, 9);

        for (int i = 0; i < reference.Count; i++)
        {
            Assert.Equal(reference.Atoms[i].Position.X, result.Aligned.Atoms[i].Position.X, 9);
            Assert.Equal(reference.Atoms[i].Position.Y, result.Aligned.Atoms[i].Position.Y, 9);
            Assert.Equal(reference.Atoms[i].Position.Z, result.Aligned.Atoms[i].Position.Z, 9);
        }
    }

    /// <summary>
    /// Зеркальный образ не совмещается поворотом: алгоритм не должен подменять
    /// поворот отражением ради меньшего отклонения.
    /// </summary>
    [Fact]
    public void Alignment_DoesNotReflectStructure()
    {
        MolecularStructure reference = Methane();
        var mirrored = new MolecularStructure();

        foreach (AtomSite atom in reference.Atoms)
        {
            Vector3 position = atom.Position;
            mirrored.Add(atom.WithPosition(new Vector3(position.X, position.Y, -position.Z)));
        }

        AlignmentResult result = StructureAlignment.Align(mirrored, reference);

        Assert.True(result.Rmsd > 0.5, $"отражение не должно давать нулевого отклонения, получено {result.Rmsd:F3}");
    }

    /// <summary>Совмещение требует не меньше трёх общих атомов.</summary>
    [Fact]
    public void Alignment_RejectsTooFewAtoms()
    {
        var first = new MolecularStructure();
        var second = new MolecularStructure();

        first.Add("C", 0, 0, 0);
        first.Add("C", 1, 0, 0);
        second.Add("C", 0, 0, 0);
        second.Add("C", 1, 0, 0);

        Assert.Throws<ArgumentException>(() => StructureAlignment.Align(first, second));
        Assert.Throws<ArgumentException>(() => StructureAlignment.Rmsd(first, Methane()));
    }

    /// <summary>Среднеквадратичная флуктуация выделяет подвижный атом.</summary>
    [Fact]
    public void Alignment_FluctuationsFindMobileAtom()
    {
        var frames = new List<MolecularStructure>();

        for (int step = 0; step < 4; step++)
        {
            var frame = new MolecularStructure();

            frame.Add("C", 0, 0, 0);
            frame.Add("C", 5, 0, 0);
            frame.Add("C", 0, 5, 0);
            frame.Add("H", 0, 0, step % 2 == 0 ? 0.5 : -0.5);

            frames.Add(frame);
        }

        double[] fluctuations = StructureAlignment.Fluctuations(new Trajectory(frames), align: false);

        Assert.Equal(0, fluctuations[0], 9);
        Assert.Equal(0, fluctuations[1], 9);
        Assert.Equal(0.5, fluctuations[3], 9);
    }

    /// <summary>Функция распределения простой кубической решётки даёт пик на расстоянии соседей.</summary>
    [Fact]
    public void Rdf_FindsFirstShellOfSimpleCubicLattice()
    {
        Trajectory trajectory = SimpleCubicLattice(4, 3.0);

        var (distances, g) = TrajectoryAnalysis.RadialDistribution(trajectory, "Ar", "Ar", 10, 200);

        int first = Array.FindIndex(g, value => value > 0);

        Assert.True(first >= 0, "функция распределения не должна быть пустой");
        Assert.Equal(3.0, distances[first], 1);

        // Между координационными сферами решётки соседей нет
        int between = Array.FindIndex(distances, d => d > 3.5);

        Assert.Equal(0, g[between], 9);

        // В простой кубической решётке у каждого атома шесть ближайших соседей
        Assert.Equal(6, TrajectoryAnalysis.CoordinationNumber(trajectory, distances, g, 3.2, "Ar"), 2);

        // Вторая сфера - двенадцать соседей по диагоналям граней
        Assert.Equal(18, TrajectoryAnalysis.CoordinationNumber(trajectory, distances, g, 4.5, "Ar"), 2);

        // Плотность без поправки на сам атом завышает координационное число
        double naive = TrajectoryAnalysis.CoordinationNumber(
            distances, g, trajectory.AtomCount / trajectory.Cell.Volume, 3.2);

        Assert.True(naive > 6.05);
    }

    /// <summary>Функция распределения считается только для периодической системы.</summary>
    [Fact]
    public void Rdf_RequiresPeriodicCell()
    {
        var frames = new List<MolecularStructure> { Methane() };

        Assert.Throws<ArgumentException>(() =>
            TrajectoryAnalysis.RadialDistribution(new Trajectory(frames), "H"));
    }

    /// <summary>Равномерное движение даёт квадратичный рост смещения.</summary>
    [Fact]
    public void Msd_QuadraticForUniformMotion()
    {
        var frames = new List<MolecularStructure>();

        for (int step = 0; step < 10; step++)
        {
            var frame = new MolecularStructure();
            frame.Add("Ar", 0.1 * step, 0, 0);
            frames.Add(frame);
        }

        var (time, displacement) = TrajectoryAnalysis.MeanSquareDisplacement(new Trajectory(frames, 1.0));

        for (int lag = 1; lag < time.Length; lag++)
            Assert.Equal(0.01 * lag * lag, displacement[lag], 9);
    }

    /// <summary>Случайное блуждание даёт линейное смещение и коэффициент диффузии s^2/(2·dt).</summary>
    [Fact]
    public void Diffusion_RecoversRandomWalkCoefficient()
    {
        const double step = 0.1;
        const double timeStep = 0.5;

        Trajectory trajectory = RandomWalk(60, 240, step);

        var (time, displacement) = TrajectoryAnalysis.MeanSquareDisplacement(
            new Trajectory(trajectory.Frames, timeStep), maxLag: 60);

        DiffusionResult diffusion = TrajectoryAnalysis.Diffusion(time, displacement);

        double expected = step * step / (2 * timeStep);

        Assert.Equal(expected, diffusion.Value, 3);
        Assert.True(diffusion.R2 > 0.99, $"смещение должно расти линейно, R2 = {diffusion.R2:F4}");
        Assert.Equal(diffusion.Value * 1e-4, diffusion.SquareCentimetresPerSecond, 12);
    }

    /// <summary>
    /// Разворачивание координат убирает скачок при переходе через границу ячейки:
    /// без него смещение упирается в размер ячейки.
    /// </summary>
    [Fact]
    public void Trajectory_UnwrapRemovesBoundaryJumps()
    {
        var frames = new List<MolecularStructure>();
        UnitCell cell = UnitCell.Cubic(10);

        for (int step = 0; step < 6; step++)
        {
            var frame = new MolecularStructure { Cell = cell };
            frame.Add("Ar", (8.0 + step) % 10, 0, 0);
            frames.Add(frame);
        }

        var trajectory = new Trajectory(frames);
        Trajectory unwrapped = trajectory.Unwrapped();

        Assert.Equal(13.0, unwrapped.Position(5, 0).X, 9);

        var (_, wrappedDisplacement) = TrajectoryAnalysis.MeanSquareDisplacement(trajectory, maxLag: 3);
        var (_, unwrappedDisplacement) = TrajectoryAnalysis.MeanSquareDisplacement(unwrapped, maxLag: 3);

        // За три шага частица уходит на 3 ангстрема, то есть смещение равно девяти
        Assert.Equal(9.0, unwrappedDisplacement[3], 9);
        Assert.True(wrappedDisplacement[3] > 30,
            $"свёрнутые координаты дают ложное смещение, получено {wrappedDisplacement[3]:F1}");
    }

    /// <summary>Автокорреляция периодического ряда меняет знак на половине периода.</summary>
    [Fact]
    public void Autocorrelation_FollowsPeriodOfSignal()
    {
        var values = new double[200];

        for (int i = 0; i < values.Length; i++)
            values[i] = Math.Cos(2 * Math.PI * i / 20);

        double[] correlation = TrajectoryAnalysis.Autocorrelation(values, 40);

        Assert.Equal(1.0, correlation[0], 6);
        Assert.True(correlation[10] < -0.5, $"на половине периода ожидалась антикорреляция, получено {correlation[10]:F3}");
        Assert.True(correlation[20] > 0.5, $"через период корреляция должна вернуться, получено {correlation[20]:F3}");

        double correlationTime = TrajectoryAnalysis.CorrelationTime(correlation, 0.1);

        Assert.True(correlationTime is > 0 and < 0.6, $"время корреляции {correlationTime:F3} пс выходит за четверть периода");
    }

    /// <summary>Димер воды содержит одну водородную связь.</summary>
    [Fact]
    public void HydrogenBonds_FindsWaterDimer()
    {
        MolecularStructure dimer = WaterDimer();

        var bonds = TrajectoryAnalysis.HydrogenBonds(dimer);

        Assert.Single(bonds);
        Assert.Equal(0, bonds[0].Donor);
        Assert.Equal(1, bonds[0].Hydrogen);
        Assert.Equal(3, bonds[0].Acceptor);
        Assert.Equal(1.8928, bonds[0].Distance, 3);
        Assert.Equal(180, bonds[0].Angle, 6);
    }

    /// <summary>Слишком тупой угол или большое расстояние водородной связью не считаются.</summary>
    [Fact]
    public void HydrogenBonds_RespectsGeometricCriteria()
    {
        MolecularStructure dimer = WaterDimer();

        Assert.Empty(TrajectoryAnalysis.HydrogenBonds(dimer, maxDistance: 1.5));
        Assert.Empty(TrajectoryAnalysis.HydrogenBonds(dimer, maxDistance: 2.5, minAngle: 181));

        var (average, perFrame) = TrajectoryAnalysis.HydrogenBondCount(
            new Trajectory(new[] { dimer, dimer }));

        Assert.Equal(1.0, average, 9);
        Assert.Equal(2, perFrame.Length);
    }

    /// <summary>Отчёт по траектории перечисляет её основные параметры.</summary>
    [Fact]
    public void Trajectory_ReportMentionsGeometry()
    {
        string report = TrajectoryAnalysis.Report(SimpleCubicLattice(3, 3.0));

        Assert.Contains("Кадров", report);
        Assert.Contains("Ячейка", report);
        Assert.Contains("Ar27", report);
    }

    /// <summary>Кадры траектории должны содержать одни и те же атомы.</summary>
    [Fact]
    public void Trajectory_RejectsInconsistentFrames()
    {
        var first = new MolecularStructure();
        first.Add("C", 0, 0, 0);

        Assert.Throws<ArgumentException>(() => new Trajectory(new[] { first, Methane() }));
        Assert.Throws<ArgumentException>(() => new Trajectory(Array.Empty<MolecularStructure>()));
    }

    private static MolecularStructure Methane()
    {
        var methane = new MolecularStructure();

        methane.Add("C", 0, 0, 0);
        methane.Add("H", 0.629, 0.629, 0.629);
        methane.Add("H", -0.629, -0.629, 0.629);
        methane.Add("H", -0.629, 0.629, -0.629);
        methane.Add("H", 0.629, -0.629, -0.629);

        return methane;
    }

    private static MolecularStructure WaterDimer()
    {
        var dimer = new MolecularStructure();

        dimer.Add("O", 0, 0, 0);
        dimer.Add("H", 0.9572, 0, 0);
        dimer.Add("H", -0.2400, 0.9266, 0);
        dimer.Add("O", 2.85, 0, 0);
        dimer.Add("H", 3.2, 0.9, 0);
        dimer.Add("H", 3.2, -0.9, 0);

        return dimer;
    }

    private static MolecularStructure Rotate(MolecularStructure structure, double degrees, Vector3 shift)
    {
        double angle = degrees * Math.PI / 180;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        var result = new MolecularStructure();

        foreach (AtomSite atom in structure.Atoms)
        {
            Vector3 p = atom.Position;

            result.Add(atom.WithPosition(new Vector3(
                (cos * p.X) - (sin * p.Y),
                (sin * p.X) + (cos * p.Y),
                p.Z) + shift));
        }

        return result;
    }

    private static Trajectory SimpleCubicLattice(int cells, double spacing)
    {
        var frame = new MolecularStructure { Cell = UnitCell.Cubic(cells * spacing) };

        for (int x = 0; x < cells; x++)
        {
            for (int y = 0; y < cells; y++)
            {
                for (int z = 0; z < cells; z++)
                    frame.Add("Ar", x * spacing, y * spacing, z * spacing);
            }
        }

        return new Trajectory(new[] { frame });
    }

    // Блуждание по решётке с воспроизводимым генератором: шаг +-step по каждой оси
    private static Trajectory RandomWalk(int particles, int steps, double step)
    {
        var frames = new List<MolecularStructure>(steps);
        var positions = new Vector3[particles];
        uint state = 20240101;

        for (int frame = 0; frame < steps; frame++)
        {
            var structure = new MolecularStructure();

            for (int particle = 0; particle < particles; particle++)
            {
                if (frame > 0)
                {
                    positions[particle] += new Vector3(
                        Next(ref state) ? step : -step,
                        Next(ref state) ? step : -step,
                        Next(ref state) ? step : -step);
                }

                structure.Add("Ar", positions[particle].X, positions[particle].Y, positions[particle].Z);
            }

            frames.Add(structure);
        }

        return new Trajectory(frames);
    }

    private static bool Next(ref uint state)
    {
        // Линейный конгруэнтный генератор из стандарта: воспроизводимость важнее качества
        state = (state * 1664525) + 1013904223;

        return (state & 0x10000) != 0;
    }
}

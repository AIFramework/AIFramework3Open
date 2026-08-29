using AI.Solvers.Chem.Structures;

namespace AI.Solvers.Chem.Dynamics;

/// <summary>
/// Траектория молекулярной динамики: последовательность кадров с общим шагом по времени
/// </summary>
/// <remarks>
/// Кадры должны содержать одни и те же атомы в одном и том же порядке: без этого
/// нельзя проследить ни смещение отдельной частицы, ни совмещение структур.
/// </remarks>
public sealed class Trajectory
{
    private readonly MolecularStructure[] _frames;

    /// <summary>Кадры траектории</summary>
    public IReadOnlyList<MolecularStructure> Frames => _frames;

    /// <summary>Шаг по времени между кадрами, пикосекунды</summary>
    public double TimeStep { get; }

    /// <summary>Число кадров</summary>
    public int Count => _frames.Length;

    /// <summary>Число атомов в кадре</summary>
    public int AtomCount => _frames[0].Count;

    /// <summary>Ячейка траектории; null для непериодической системы</summary>
    public UnitCell Cell => _frames[0].Cell;

    /// <summary>Периодична ли система</summary>
    public bool IsPeriodic => Cell != null;

    /// <summary>Полная длительность траектории, пикосекунды</summary>
    public double Duration => (Count - 1) * TimeStep;

    /// <summary>Создаёт траекторию</summary>
    /// <param name="frames">Кадры</param>
    /// <param name="timeStep">Шаг по времени между кадрами, пикосекунды</param>
    public Trajectory(IReadOnlyList<MolecularStructure> frames, double timeStep = 1.0)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
            throw new ArgumentException("Траектория не содержит кадров", nameof(frames));

        if (timeStep <= 0)
            throw new ArgumentException("Шаг по времени должен быть положительным", nameof(timeStep));

        int atoms = frames[0].Count;

        for (int i = 1; i < frames.Count; i++)
        {
            if (frames[i].Count != atoms)
                throw new ArgumentException($"Кадр {i + 1} содержит другое число атомов", nameof(frames));
        }

        _frames = frames.ToArray();
        TimeStep = timeStep;
    }

    /// <summary>Читает траекторию из многокадрового XYZ</summary>
    /// <param name="text">Содержимое файла</param>
    /// <param name="timeStep">Шаг по времени между кадрами, пикосекунды</param>
    public static Trajectory FromXyz(string text, double timeStep = 1.0)
        => new(StructureFormats.ReadXyzTrajectory(text), timeStep);

    /// <summary>Положение атома в кадре</summary>
    /// <param name="frame">Номер кадра</param>
    /// <param name="atom">Номер атома</param>
    public Vector3 Position(int frame, int atom) => _frames[frame].Atoms[atom].Position;

    /// <summary>Время кадра, пикосекунды</summary>
    /// <param name="frame">Номер кадра</param>
    public double Time(int frame) => frame * TimeStep;

    /// <summary>Номера атомов заданного элемента</summary>
    /// <param name="element">Символ элемента</param>
    public IReadOnlyList<int> IndicesOf(string element)
    {
        var result = new List<int>();
        MolecularStructure first = _frames[0];

        for (int i = 0; i < first.Count; i++)
        {
            if (string.Equals(first.Atoms[i].Element, element, StringComparison.OrdinalIgnoreCase))
                result.Add(i);
        }

        return result;
    }

    /// <summary>Все номера атомов</summary>
    public IReadOnlyList<int> AllIndices() => Enumerable.Range(0, AtomCount).ToArray();

    /// <summary>
    /// Разворачивает координаты периодической системы: убирает скачки,
    /// возникающие при возврате частицы в ячейку
    /// </summary>
    /// <remarks>
    /// Без разворачивания среднеквадратичное смещение выходит на постоянную
    /// величину порядка размера ячейки, и коэффициент диффузии получается заниженным.
    /// </remarks>
    public Trajectory Unwrapped()
    {
        if (!IsPeriodic || Count < 2)
            return this;

        var result = new List<MolecularStructure> { _frames[0] };
        var previous = new Vector3[AtomCount];
        var shifted = new Vector3[AtomCount];

        for (int atom = 0; atom < AtomCount; atom++)
        {
            previous[atom] = Position(0, atom);
            shifted[atom] = previous[atom];
        }

        for (int frame = 1; frame < Count; frame++)
        {
            var unwrapped = new MolecularStructure { Cell = Cell, Name = _frames[frame].Name };

            for (int atom = 0; atom < AtomCount; atom++)
            {
                Vector3 current = Position(frame, atom);
                Vector3 step = Cell.MinimumImage(previous[atom], current);

                shifted[atom] += step;
                previous[atom] = current;

                unwrapped.Add(_frames[frame].Atoms[atom].WithPosition(shifted[atom]));
            }

            result.Add(unwrapped);
        }

        return new Trajectory(result, TimeStep);
    }
}

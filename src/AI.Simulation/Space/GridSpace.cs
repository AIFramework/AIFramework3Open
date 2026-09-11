namespace AI.Simulation.Space;

/// <summary>
/// Двумерная решётка для агентной модели: где стоит каждый агент и кто его соседи.
/// </summary>
/// <remarks>
/// <para>
/// Решётку можно замкнуть в тор: тогда правый край соседствует с левым, верхний — с нижним,
/// и у каждой клетки одинаковое число соседей. Без замыкания клетки у края беднее соседями,
/// и в моделях, где важна плотность контактов, край даёт систематический перекос.
/// </para>
/// <para>
/// В одной клетке может стоять несколько агентов. Правило «не больше одного» — часть модели,
/// а не решётки: оно выражается через <see cref="IsEmpty"/> и <see cref="RandomEmptyCell"/>.
/// </para>
/// </remarks>
/// <typeparam name="TAgent">Тип агента</typeparam>
public sealed class GridSpace<TAgent> where TAgent : notnull
{
    private readonly Dictionary<TAgent, Cell> _positions;
    private readonly List<TAgent>?[] _cells;

    /// <summary>Создаёт решётку</summary>
    /// <param name="width">Число столбцов</param>
    /// <param name="height">Число строк</param>
    /// <param name="torus">Замкнуть ли края</param>
    /// <param name="neighbourhood">Окрестность клетки</param>
    /// <param name="comparer">Сравнение агентов; по умолчанию стандартное</param>
    public GridSpace(
        int width,
        int height,
        bool torus = true,
        Neighbourhood neighbourhood = Neighbourhood.Moore,
        IEqualityComparer<TAgent>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Torus = torus;
        Neighbourhood = neighbourhood;

        _positions = new Dictionary<TAgent, Cell>(comparer);
        _cells = new List<TAgent>?[width * height];
    }

    /// <summary>Число столбцов</summary>
    public int Width { get; }

    /// <summary>Число строк</summary>
    public int Height { get; }

    /// <summary>Замкнуты ли края в тор</summary>
    public bool Torus { get; }

    /// <summary>Окрестность клетки</summary>
    public Neighbourhood Neighbourhood { get; }

    /// <summary>Число размещённых агентов</summary>
    public int Count => _positions.Count;

    /// <summary>Лежит ли клетка в пределах решётки</summary>
    /// <param name="cell">Клетка</param>
    public bool Contains(Cell cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

    /// <summary>Размещает агента в клетке</summary>
    /// <param name="agent">Агент</param>
    /// <param name="cell">Клетка</param>
    public void Place(TAgent agent, Cell cell)
    {
        Require(cell);

        if (_positions.ContainsKey(agent))
            throw new ArgumentException("Агент уже размещён на решётке; для перемещения есть Move", nameof(agent));

        _positions[agent] = cell;
        Slot(cell).Add(agent);
    }

    /// <summary>Перемещает агента в другую клетку</summary>
    /// <param name="agent">Агент</param>
    /// <param name="cell">Новая клетка</param>
    public void Move(TAgent agent, Cell cell)
    {
        Require(cell);
        Cell from = CellOf(agent);

        if (from == cell)
            return;

        _ = _cells[Index(from)]!.Remove(agent);
        _positions[agent] = cell;
        Slot(cell).Add(agent);
    }

    /// <summary>Убирает агента с решётки</summary>
    /// <param name="agent">Агент</param>
    /// <returns><c>false</c>, если агента на решётке не было</returns>
    public bool Remove(TAgent agent)
    {
        if (!_positions.Remove(agent, out Cell cell))
            return false;

        _ = _cells[Index(cell)]!.Remove(agent);

        return true;
    }

    /// <summary>Клетка, в которой стоит агент</summary>
    /// <param name="agent">Агент</param>
    public Cell CellOf(TAgent agent)
        => _positions.TryGetValue(agent, out Cell cell)
            ? cell
            : throw new KeyNotFoundException("Агент не размещён на решётке");

    /// <summary>Агенты в клетке</summary>
    /// <remarks>
    /// Возвращается снимок, а не внутренний список: обходя его, агентов можно перемещать
    /// и убирать — обычный шаг агентной модели.
    /// </remarks>
    /// <param name="cell">Клетка</param>
    public IReadOnlyList<TAgent> AgentsAt(Cell cell)
    {
        Require(cell);

        return _cells[Index(cell)] is { Count: > 0 } occupants ? occupants.ToArray() : Array.Empty<TAgent>();
    }

    /// <summary>Свободна ли клетка</summary>
    /// <param name="cell">Клетка</param>
    public bool IsEmpty(Cell cell)
    {
        Require(cell);

        return _cells[Index(cell)] is not { Count: > 0 };
    }

    /// <summary>Соседние клетки</summary>
    /// <param name="cell">Клетка</param>
    public IReadOnlyList<Cell> Neighbours(Cell cell)
    {
        Require(cell);

        var result = new List<Cell>(8);

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (Neighbourhood == Neighbourhood.VonNeumann && dx != 0 && dy != 0)
                    continue;

                int x = cell.X + dx;
                int y = cell.Y + dy;

                if (Torus)
                {
                    x = Wrap(x, Width);
                    y = Wrap(y, Height);
                }
                else if (x < 0 || x >= Width || y < 0 || y >= Height)
                {
                    continue;
                }

                var neighbour = new Cell(x, y);

                // На узкой замкнутой решётке разные сдвиги приводят в одну и ту же клетку,
                // а то и в исходную — соседей тогда меньше, чем в окрестности
                if (neighbour != cell && !result.Contains(neighbour))
                    result.Add(neighbour);
            }
        }

        return result;
    }

    /// <summary>Агенты в соседних клетках</summary>
    /// <param name="agent">Агент</param>
    public IReadOnlyList<TAgent> NeighbourAgents(TAgent agent)
    {
        var result = new List<TAgent>();

        foreach (Cell cell in Neighbours(CellOf(agent)))
        {
            if (_cells[Index(cell)] is { } occupants)
                result.AddRange(occupants);
        }

        return result;
    }

    /// <summary>
    /// Расстояние между клетками в шагах окрестности: сумма сдвигов для окрестности фон Неймана,
    /// наибольший сдвиг для окрестности Мура; на торе — по кратчайшему пути через край
    /// </summary>
    /// <param name="a">Первая клетка</param>
    /// <param name="b">Вторая клетка</param>
    public int Distance(Cell a, Cell b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);

        if (Torus)
        {
            dx = Math.Min(dx, Width - dx);
            dy = Math.Min(dy, Height - dy);
        }

        return Neighbourhood == Neighbourhood.Moore ? Math.Max(dx, dy) : dx + dy;
    }

    /// <summary>
    /// Случайная свободная клетка; <c>null</c>, если свободных нет
    /// </summary>
    /// <remarks>
    /// Выбор равновероятен среди свободных и делается за один проход, без повторных попыток:
    /// на почти заполненной решётке метод проб промахивался бы почти всегда.
    /// </remarks>
    /// <param name="random">Генератор случайных чисел модели</param>
    public Cell? RandomEmptyCell(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        int free = 0;

        foreach (List<TAgent>? occupants in _cells)
        {
            if (occupants is not { Count: > 0 })
                free++;
        }

        if (free == 0)
            return null;

        int target = random.Next(free);

        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] is { Count: > 0 })
                continue;

            if (target-- == 0)
                return new Cell(i % Width, i / Width);
        }

        return null;
    }

    private List<TAgent> Slot(Cell cell) => _cells[Index(cell)] ??= [];

    private int Index(Cell cell) => (cell.Y * Width) + cell.X;

    private void Require(Cell cell)
    {
        if (!Contains(cell))
            throw new ArgumentOutOfRangeException(nameof(cell), $"Клетка {cell} вне решётки {Width}×{Height}");
    }

    private static int Wrap(int value, int size) => ((value % size) + size) % size;
}

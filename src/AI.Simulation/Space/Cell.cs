namespace AI.Simulation.Space;

/// <summary>Клетка решётки</summary>
/// <param name="X">Номер столбца</param>
/// <param name="Y">Номер строки</param>
public readonly record struct Cell(int X, int Y)
{
    /// <summary>Запись клетки</summary>
    public override string ToString() => $"({X}, {Y})";
}

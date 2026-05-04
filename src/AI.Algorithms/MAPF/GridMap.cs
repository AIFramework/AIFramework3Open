using System;
using System.Collections.Generic;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Двумерная сеточная карта для задач многоагентного поиска пути.
/// Поддерживает 4- и 8-связность, хранение заблокированных ячеек.
/// </summary>
[Serializable]
public class GridMap
{
    internal bool[,] _blocked;

    /// <summary>Ширина карты (число столбцов).</summary>
    public int Width { get; }

    /// <summary>Высота карты (число строк).</summary>
    public int Height { get; }

    /// <summary>
    /// Создаёт сеточную карту заданного размера без препятствий.
    /// </summary>
    /// <param name="width">Ширина карты.</param>
    /// <param name="height">Высота карты.</param>
    public GridMap(int width, int height)
    {
        Width = width;
        Height = height;
        _blocked = new bool[width, height];
    }

    /// <summary>
    /// Устанавливает или снимает блокировку ячейки.
    /// </summary>
    /// <param name="x">Координата X.</param>
    /// <param name="y">Координата Y.</param>
    /// <param name="blocked">Заблокирована ли ячейка.</param>
    public void SetBlocked(int x, int y, bool blocked)
    {
        _blocked[x, y] = blocked;
    }

    /// <summary>
    /// Проверяет, заблокирована ли ячейка.
    /// </summary>
    public bool IsBlocked(int x, int y)
    {
        return _blocked[x, y];
    }

    /// <summary>
    /// Проверяет, находятся ли координаты в пределах карты.
    /// </summary>
    public bool InBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    /// <summary>
    /// Возвращает список соседних проходимых ячеек.
    /// </summary>
    /// <param name="x">Координата X текущей ячейки.</param>
    /// <param name="y">Координата Y текущей ячейки.</param>
    /// <param name="eightConnected">Если true — 8-связность (с диагоналями).</param>
    public List<(int X, int Y)> Neighbors(int x, int y, bool eightConnected = false)
    {
        var result = new List<(int X, int Y)>(eightConnected ? 8 : 4);

        if (InBounds(x + 1, y) && !_blocked[x + 1, y]) result.Add((x + 1, y));
        if (InBounds(x - 1, y) && !_blocked[x - 1, y]) result.Add((x - 1, y));
        if (InBounds(x, y + 1) && !_blocked[x, y + 1]) result.Add((x, y + 1));
        if (InBounds(x, y - 1) && !_blocked[x, y - 1]) result.Add((x, y - 1));

        if (eightConnected)
        {
            if (InBounds(x + 1, y + 1) && !_blocked[x + 1, y + 1]) result.Add((x + 1, y + 1));
            if (InBounds(x + 1, y - 1) && !_blocked[x + 1, y - 1]) result.Add((x + 1, y - 1));
            if (InBounds(x - 1, y + 1) && !_blocked[x - 1, y + 1]) result.Add((x - 1, y + 1));
            if (InBounds(x - 1, y - 1) && !_blocked[x - 1, y - 1]) result.Add((x - 1, y - 1));
        }

        return result;
    }
}

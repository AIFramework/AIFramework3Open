using System;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Описание агента: идентификатор, стартовая и целевая позиции.
/// </summary>
[Serializable]
public class MAPFAgent
{
    /// <summary>Уникальный идентификатор агента.</summary>
    public int Id { get; set; }

    /// <summary>Стартовая координата X.</summary>
    public int StartX { get; set; }

    /// <summary>Стартовая координата Y.</summary>
    public int StartY { get; set; }

    /// <summary>Целевая координата X.</summary>
    public int GoalX { get; set; }

    /// <summary>Целевая координата Y.</summary>
    public int GoalY { get; set; }
}

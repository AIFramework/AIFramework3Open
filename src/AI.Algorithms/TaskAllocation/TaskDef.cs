using System;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Определение задачи для распределения
/// </summary>
[Serializable]
public class TaskDef
{
    /// <summary>
    /// Идентификатор задачи
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// X-координата местоположения задачи
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y-координата местоположения задачи
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Вектор затрат для каждого агента
    /// </summary>
    public double[] CostVector { get; set; }

    /// <summary>
    /// Ценность/награда за выполнение задачи
    /// </summary>
    public double Value { get; set; } = 1.0;
}

using System;
using System.Collections.Generic;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Результат распределения задач между агентами
/// </summary>
[Serializable]
public class AllocationResult
{
    /// <summary>
    /// Список назначений (IdАгента, IdЗадачи)
    /// </summary>
    public List<(int AgentId, int TaskId)> Assignments { get; set; } = new List<(int, int)>();

    /// <summary>
    /// Суммарная стоимость распределения
    /// </summary>
    public double TotalCost { get; set; }

    /// <summary>
    /// Суммарная ценность выполненных задач
    /// </summary>
    public double TotalValue { get; set; }

    /// <summary>
    /// Количество нераспределённых задач
    /// </summary>
    public int UnassignedTasks { get; set; }
}

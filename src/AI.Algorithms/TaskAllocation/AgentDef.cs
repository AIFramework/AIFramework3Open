using System;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Определение агента для задачи распределения
/// </summary>
[Serializable]
public class AgentDef
{
    /// <summary>
    /// Идентификатор агента
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// X-координата местоположения агента
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y-координата местоположения агента
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Максимальное количество задач, которые может выполнить агент
    /// </summary>
    public int Capacity { get; set; } = 1;

    /// <summary>
    /// Уровни навыков/способностей агента
    /// </summary>
    public double[] Capabilities { get; set; }
}

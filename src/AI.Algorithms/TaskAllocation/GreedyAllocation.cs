using System;
using System.Collections.Generic;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Жадный алгоритм распределения задач (ближайший первый).
/// Назначает каждую задачу ближайшему свободному агенту.
/// </summary>
[Serializable]
public class GreedyAllocation
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;

    /// <summary>
    /// Создаёт экземпляр жадного алгоритма распределения
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    public GreedyAllocation(List<AgentDef> agents, List<TaskDef> tasks)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    /// <summary>
    /// Выполняет жадное распределение задач
    /// </summary>
    public AllocationResult Solve()
    {
        var result = new AllocationResult();
        int[] agentLoad = new int[_agents.Count];

        var pairs = new List<(double cost, int agentIdx, int taskIdx)>();
        for (int a = 0; a < _agents.Count; a++)
            for (int t = 0; t < _tasks.Count; t++)
                pairs.Add((GetCost(a, t), a, t));

        pairs.Sort((x, y) => x.cost.CompareTo(y.cost));

        bool[] taskAssigned = new bool[_tasks.Count];

        foreach (var (cost, a, t) in pairs)
        {
            if (taskAssigned[t]) continue;
            if (agentLoad[a] >= _agents[a].Capacity) continue;

            result.Assignments.Add((_agents[a].Id, _tasks[t].Id));
            result.TotalCost += cost;
            result.TotalValue += _tasks[t].Value;
            taskAssigned[t] = true;
            agentLoad[a]++;
        }

        for (int t = 0; t < _tasks.Count; t++)
            if (!taskAssigned[t]) result.UnassignedTasks++;

        return result;
    }

    private double GetCost(int agentIdx, int taskIdx)
    {
        var task = _tasks[taskIdx];
        if (task.CostVector != null && agentIdx < task.CostVector.Length)
            return task.CostVector[agentIdx];

        var agent = _agents[agentIdx];
        double dx = agent.X - task.X;
        double dy = agent.Y - task.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

using System;
using System.Collections.Generic;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Последовательный одноэлементный аукцион (Sequential Single-Item Auction)
/// </summary>
[Serializable]
public class SSIAuction
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;

    /// <summary>
    /// Создаёт экземпляр последовательного одноэлементного аукциона
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    public SSIAuction(List<AgentDef> agents, List<TaskDef> tasks)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    /// <summary>
    /// Выполняет распределение задач последовательным аукционом
    /// </summary>
    public AllocationResult Solve()
    {
        var result = new AllocationResult();
        int[] agentLoad = new int[_agents.Count];
        bool[] taskAssigned = new bool[_tasks.Count];

        for (int t = 0; t < _tasks.Count; t++)
        {
            var task = _tasks[t];

            var bids = new List<(int agentIdx, double bid)>();
            for (int a = 0; a < _agents.Count; a++)
            {
                if (agentLoad[a] >= _agents[a].Capacity) continue;
                double bid = GetCost(a, t);
                bids.Add((a, bid));
            }

            if (bids.Count == 0)
            {
                result.UnassignedTasks++;
                continue;
            }

            bids.Sort((x, y) => x.bid.CompareTo(y.bid));

            int winner = bids[0].agentIdx;
            result.Assignments.Add((_agents[winner].Id, task.Id));
            result.TotalCost += bids[0].bid;
            result.TotalValue += task.Value;
            agentLoad[winner]++;
            taskAssigned[t] = true;
        }

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

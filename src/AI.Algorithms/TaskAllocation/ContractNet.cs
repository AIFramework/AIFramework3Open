using System;
using System.Collections.Generic;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Протокол сети контрактов (Contract Net Protocol).
/// Менеджер рассылает объявления, агенты делают ставки, менеджер назначает задачи.
/// </summary>
[Serializable]
public class ContractNet
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;

    /// <summary>
    /// Создаёт экземпляр протокола сети контрактов
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    public ContractNet(List<AgentDef> agents, List<TaskDef> tasks)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    /// <summary>
    /// Выполняет распределение задач по протоколу CNP
    /// </summary>
    public AllocationResult Solve()
    {
        var result = new AllocationResult();
        int[] agentLoad = new int[_agents.Count];
        bool[] taskAssigned = new bool[_tasks.Count];

        for (int t = 0; t < _tasks.Count; t++)
        {
            var task = _tasks[t];
            double bestBid = double.MaxValue;
            int bestAgent = -1;

            for (int a = 0; a < _agents.Count; a++)
            {
                if (agentLoad[a] >= _agents[a].Capacity) continue;

                double bid = GetCost(a, t);
                if (bid < bestBid)
                {
                    bestBid = bid;
                    bestAgent = a;
                }
            }

            if (bestAgent >= 0)
            {
                result.Assignments.Add((_agents[bestAgent].Id, task.Id));
                result.TotalCost += bestBid;
                result.TotalValue += task.Value;
                agentLoad[bestAgent]++;
                taskAssigned[t] = true;
            }
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

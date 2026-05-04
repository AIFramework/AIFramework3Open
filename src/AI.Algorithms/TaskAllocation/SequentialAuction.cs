using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Последовательный аукцион (по Кёнигу) для распределения задач.
/// Задачи выставляются на аукцион по убыванию ценности; агенты делают ставки
/// с учётом маргинальной стоимости.
/// </summary>
[Serializable]
public class SequentialAuction
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;

    /// <summary>
    /// Создаёт экземпляр последовательного аукциона
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    public SequentialAuction(List<AgentDef> agents, List<TaskDef> tasks)
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
        var agentAssigned = new List<int>[_agents.Count];
        for (int a = 0; a < _agents.Count; a++)
            agentAssigned[a] = new List<int>();

        var taskOrder = Enumerable.Range(0, _tasks.Count)
            .OrderByDescending(t => _tasks[t].Value)
            .ToList();

        foreach (int t in taskOrder)
        {
            var task = _tasks[t];
            double bestBid = double.MaxValue;
            int bestAgent = -1;

            for (int a = 0; a < _agents.Count; a++)
            {
                if (agentLoad[a] >= _agents[a].Capacity) continue;

                double marginalCost = ComputeMarginalCost(a, t, agentAssigned[a]);
                if (marginalCost < bestBid)
                {
                    bestBid = marginalCost;
                    bestAgent = a;
                }
            }

            if (bestAgent >= 0)
            {
                result.Assignments.Add((_agents[bestAgent].Id, task.Id));
                result.TotalCost += bestBid;
                result.TotalValue += task.Value;
                agentLoad[bestAgent]++;
                agentAssigned[bestAgent].Add(t);
            }
            else
            {
                result.UnassignedTasks++;
            }
        }

        return result;
    }

    private double ComputeMarginalCost(int agentIdx, int taskIdx, List<int> currentTasks)
    {
        double baseCost = GetCost(agentIdx, taskIdx);

        if (currentTasks.Count > 0)
        {
            int lastTask = currentTasks[currentTasks.Count - 1];
            double dx = _tasks[lastTask].X - _tasks[taskIdx].X;
            double dy = _tasks[lastTask].Y - _tasks[taskIdx].Y;
            double seqCost = Math.Sqrt(dx * dx + dy * dy);
            return Math.Min(baseCost, seqCost);
        }

        return baseCost;
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

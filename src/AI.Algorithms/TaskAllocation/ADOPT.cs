using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Алгоритм ADOPT (Asynchronous Distributed OPTimization) для
/// распределённого распределения задач.
/// Имитирует асинхронную передачу сообщений в синхронном цикле.
/// </summary>
[Serializable]
public class ADOPT
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;
    private readonly int _maxCycles;

    /// <summary>
    /// Создаёт экземпляр алгоритма ADOPT
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    /// <param name="maxCycles">Максимальное число циклов</param>
    public ADOPT(List<AgentDef> agents, List<TaskDef> tasks, int maxCycles = 200)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _maxCycles = maxCycles;
    }

    /// <summary>
    /// Решает задачу распределения методом ADOPT
    /// </summary>
    public AllocationResult Solve()
    {
        int nA = _agents.Count;
        int nT = _tasks.Count;

        int[] currentValue = new int[nA];
        for (int a = 0; a < nA; a++)
            currentValue[a] = -1;

        double[] lb = new double[nA];
        double[] ub = new double[nA];
        double[] threshold = new double[nA];
        for (int a = 0; a < nA; a++) ub[a] = double.MaxValue;

        double bestGlobalCost = double.MaxValue;
        int[] bestAssignment = new int[nA];
        Array.Fill(bestAssignment, -1);

        for (int cycle = 0; cycle < _maxCycles; cycle++)
        {
            for (int a = 0; a < nA; a++)
            {
                int bestTask = -1;
                double bestCost = double.MaxValue;

                for (int t = 0; t < nT; t++)
                {
                    double localCost = GetCost(a, t);

                    double conflictCost = 0;
                    for (int a2 = 0; a2 < nA; a2++)
                    {
                        if (a2 == a && currentValue[a2] == t)
                            continue;
                        if (a2 != a && currentValue[a2] == t)
                            conflictCost += GetCost(a2, t) * 0.5;
                    }

                    double totalCost = localCost + conflictCost;
                    if (totalCost < bestCost)
                    {
                        bestCost = totalCost;
                        bestTask = t;
                    }
                }

                currentValue[a] = bestTask;
                lb[a] = bestCost;
            }

            double totalGlobalCost = 0;
            bool valid = true;
            for (int a = 0; a < nA; a++)
            {
                if (currentValue[a] < 0) { valid = false; break; }
                totalGlobalCost += GetCost(a, currentValue[a]);
            }

            bool hasConflict = false;
            if (valid)
            {
                for (int a1 = 0; a1 < nA && !hasConflict; a1++)
                    for (int a2 = a1 + 1; a2 < nA && !hasConflict; a2++)
                        if (currentValue[a1] == currentValue[a2])
                            hasConflict = true;
            }

            if (valid && !hasConflict && totalGlobalCost < bestGlobalCost)
            {
                bestGlobalCost = totalGlobalCost;
                Array.Copy(currentValue, bestAssignment, nA);
            }

            if (valid && !hasConflict) break;
        }

        ResolveConflicts(bestAssignment, nA, nT);

        var result = new AllocationResult();
        var assignedTasks = new HashSet<int>();

        for (int a = 0; a < nA; a++)
        {
            if (bestAssignment[a] >= 0 && bestAssignment[a] < nT)
            {
                int t = bestAssignment[a];
                result.Assignments.Add((_agents[a].Id, _tasks[t].Id));
                result.TotalCost += GetCost(a, t);
                result.TotalValue += _tasks[t].Value;
                assignedTasks.Add(t);
            }
        }

        result.UnassignedTasks = nT - assignedTasks.Count;
        return result;
    }

    private void ResolveConflicts(int[] assignment, int nA, int nT)
    {
        bool[] taskTaken = new bool[nT];

        var order = Enumerable.Range(0, nA)
            .Where(a => assignment[a] >= 0)
            .OrderBy(a => GetCost(a, assignment[a]))
            .ToList();

        foreach (int a in order)
        {
            int t = assignment[a];
            if (!taskTaken[t])
            {
                taskTaken[t] = true;
            }
            else
            {
                assignment[a] = -1;
                double bestCost = double.MaxValue;
                int bestTask = -1;
                for (int t2 = 0; t2 < nT; t2++)
                {
                    if (taskTaken[t2]) continue;
                    double c = GetCost(a, t2);
                    if (c < bestCost) { bestCost = c; bestTask = t2; }
                }
                if (bestTask >= 0)
                {
                    assignment[a] = bestTask;
                    taskTaken[bestTask] = true;
                }
            }
        }
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

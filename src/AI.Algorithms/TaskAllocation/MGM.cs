using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Алгоритм MGM (Maximum Gain Message) для распределённого распределения задач.
/// На каждой итерации каждый агент вычисляет потенциальный выигрыш от смены
/// назначения и обменивается сообщениями; меняет назначение тот,
/// у кого максимальный выигрыш.
/// </summary>
[Serializable]
public class MGM
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;
    private readonly int _maxIterations;

    /// <summary>
    /// Создаёт экземпляр алгоритма MGM
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    public MGM(List<AgentDef> agents, List<TaskDef> tasks, int maxIterations = 100)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Решает задачу распределения методом MGM
    /// </summary>
    public AllocationResult Solve()
    {
        int nA = _agents.Count;
        int nT = _tasks.Count;

        int[] assignment = new int[nA];
        for (int a = 0; a < nA; a++)
            assignment[a] = a < nT ? a : -1;

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            double[] gains = new double[nA];
            int[] bestAlternatives = new int[nA];
            Array.Fill(bestAlternatives, -1);

            for (int a = 0; a < nA; a++)
            {
                double currentCost = assignment[a] >= 0 ? GetCost(a, assignment[a]) : double.MaxValue;

                double bestAltCost = currentCost;
                int bestAltTask = assignment[a];

                for (int t = 0; t < nT; t++)
                {
                    if (t == assignment[a]) continue;

                    bool taken = false;
                    for (int a2 = 0; a2 < nA; a2++)
                    {
                        if (a2 != a && assignment[a2] == t) { taken = true; break; }
                    }
                    if (taken) continue;

                    double cost = GetCost(a, t);
                    if (cost < bestAltCost)
                    {
                        bestAltCost = cost;
                        bestAltTask = t;
                    }
                }

                gains[a] = currentCost - bestAltCost;
                bestAlternatives[a] = bestAltTask;
            }

            bool changed = false;
            for (int a = 0; a < nA; a++)
            {
                if (gains[a] <= 1e-10) continue;

                bool isMax = true;
                for (int a2 = 0; a2 < nA; a2++)
                {
                    if (a2 == a) continue;
                    bool neighbors = AreNeighbors(a, a2, assignment);
                    if (neighbors && gains[a2] > gains[a])
                    {
                        isMax = false;
                        break;
                    }
                }

                if (isMax && bestAlternatives[a] >= 0)
                {
                    assignment[a] = bestAlternatives[a];
                    changed = true;
                }
            }

            if (!changed) break;
        }

        var result = new AllocationResult();
        var assignedTasks = new HashSet<int>();

        for (int a = 0; a < nA; a++)
        {
            int t = assignment[a];
            if (t >= 0 && t < nT && !assignedTasks.Contains(t))
            {
                result.Assignments.Add((_agents[a].Id, _tasks[t].Id));
                result.TotalCost += GetCost(a, t);
                result.TotalValue += _tasks[t].Value;
                assignedTasks.Add(t);
            }
        }

        result.UnassignedTasks = nT - assignedTasks.Count;
        return result;
    }

    private bool AreNeighbors(int a1, int a2, int[] assignment)
    {
        if (assignment[a1] < 0 || assignment[a2] < 0) return false;
        double dx = _agents[a1].X - _agents[a2].X;
        double dy = _agents[a1].Y - _agents[a2].Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        return dist < double.MaxValue;
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

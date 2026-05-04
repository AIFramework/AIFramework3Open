using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Консенсусный пучковый алгоритм (Consensus-Based Bundle Algorithm).
/// Каждый агент формирует пучок задач, затем агенты достигают консенсуса
/// путём обмена сообщениями.
/// </summary>
[Serializable]
public class CBBA
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;
    private readonly int _maxIterations;

    /// <summary>
    /// Создаёт экземпляр алгоритма CBBA
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    /// <param name="maxIterations">Максимальное число итераций консенсуса</param>
    public CBBA(List<AgentDef> agents, List<TaskDef> tasks, int maxIterations = 100)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Выполняет распределение задач алгоритмом CBBA
    /// </summary>
    public AllocationResult Solve()
    {
        int nA = _agents.Count;
        int nT = _tasks.Count;

        var bundles = new List<int>[nA];
        var paths = new List<int>[nA];
        double[] winningBids = new double[nT];
        int[] winningAgents = new int[nT];
        double[,] timestamps = new double[nA, nA];

        for (int a = 0; a < nA; a++)
        {
            bundles[a] = new List<int>();
            paths[a] = new List<int>();
        }
        Array.Fill(winningAgents, -1);

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            bool changed = false;

            for (int a = 0; a < nA; a++)
            {
                while (bundles[a].Count < _agents[a].Capacity)
                {
                    double bestScore = double.MinValue;
                    int bestTask = -1;

                    for (int t = 0; t < nT; t++)
                    {
                        if (bundles[a].Contains(t)) continue;

                        double marginal = ComputeScore(a, t, bundles[a]);

                        if (marginal > winningBids[t] && marginal > bestScore)
                        {
                            bestScore = marginal;
                            bestTask = t;
                        }
                    }

                    if (bestTask < 0) break;

                    bundles[a].Add(bestTask);
                    paths[a].Add(bestTask);
                    winningBids[bestTask] = bestScore;
                    winningAgents[bestTask] = a;
                    changed = true;
                }
            }

            for (int a = 0; a < nA; a++)
            {
                for (int b = 0; b < nA; b++)
                {
                    if (a == b) continue;

                    for (int idx = bundles[a].Count - 1; idx >= 0; idx--)
                    {
                        int t = bundles[a][idx];
                        if (winningAgents[t] != a)
                        {
                            bundles[a].RemoveAt(idx);
                            paths[a].Remove(t);
                            changed = true;
                        }
                    }
                }
            }

            if (!changed) break;
        }

        var result = new AllocationResult();
        var assigned = new HashSet<int>();

        for (int t = 0; t < nT; t++)
        {
            if (winningAgents[t] >= 0)
            {
                int a = winningAgents[t];
                result.Assignments.Add((_agents[a].Id, _tasks[t].Id));
                result.TotalCost += GetCost(a, t);
                result.TotalValue += _tasks[t].Value;
                assigned.Add(t);
            }
        }

        result.UnassignedTasks = nT - assigned.Count;
        return result;
    }

    private double ComputeScore(int agentIdx, int taskIdx, List<int> currentBundle)
    {
        double cost = GetCost(agentIdx, taskIdx);
        double value = _tasks[taskIdx].Value;
        return value - cost;
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

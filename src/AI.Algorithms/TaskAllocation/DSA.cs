using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Распределённый стохастический алгоритм (DSA) для распределения задач.
/// На каждой итерации каждый агент с заданной вероятностью меняет
/// своё назначение на более выгодное.
/// </summary>
[Serializable]
public class DSA
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;
    private readonly double _activationProb;
    private readonly int _maxIterations;
    private readonly Random _rng;

    /// <summary>
    /// Создаёт экземпляр алгоритма DSA
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    /// <param name="activationProb">Вероятность активации агента</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    /// <param name="seed">Начальное значение генератора случайных чисел</param>
    public DSA(List<AgentDef> agents, List<TaskDef> tasks,
        double activationProb = 0.7, int maxIterations = 100, int seed = 42)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _activationProb = activationProb;
        _maxIterations = maxIterations;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Решает задачу распределения методом DSA
    /// </summary>
    public AllocationResult Solve()
    {
        int nA = _agents.Count;
        int nT = _tasks.Count;

        int[] assignment = new int[nA];
        for (int a = 0; a < nA; a++)
            assignment[a] = a < nT ? a : -1;

        double bestCost = EvaluateCost(assignment);
        int[] bestAssignment = (int[])assignment.Clone();

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            for (int a = 0; a < nA; a++)
            {
                if (_rng.NextDouble() > _activationProb) continue;

                double currentLocalCost = assignment[a] >= 0 ? GetCost(a, assignment[a]) : 1e6;

                double bestLocalCost = currentLocalCost;
                int bestTask = assignment[a];

                for (int t = 0; t < nT; t++)
                {
                    bool taken = false;
                    for (int a2 = 0; a2 < nA; a2++)
                    {
                        if (a2 != a && assignment[a2] == t) { taken = true; break; }
                    }
                    if (taken) continue;

                    double cost = GetCost(a, t);
                    if (cost < bestLocalCost)
                    {
                        bestLocalCost = cost;
                        bestTask = t;
                    }
                }

                assignment[a] = bestTask;
            }

            double cost2 = EvaluateCost(assignment);
            if (cost2 < bestCost)
            {
                bestCost = cost2;
                bestAssignment = (int[])assignment.Clone();
            }
        }

        var result = new AllocationResult();
        var assignedTasks = new HashSet<int>();

        for (int a = 0; a < nA; a++)
        {
            int t = bestAssignment[a];
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

    private double EvaluateCost(int[] assignment)
    {
        double cost = 0;
        for (int a = 0; a < assignment.Length; a++)
        {
            if (assignment[a] >= 0)
                cost += GetCost(a, assignment[a]);
            else
                cost += 1e6;
        }
        return cost;
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

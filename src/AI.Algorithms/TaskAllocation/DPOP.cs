using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Алгоритм DPOP (Dynamic Programming for distributed Optimization)
/// для распределённого распределения задач.
/// Строит дерево агентов и выполняет фазы UTIL и VALUE.
/// </summary>
[Serializable]
public class DPOP
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;

    /// <summary>
    /// Создаёт экземпляр алгоритма DPOP
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    public DPOP(List<AgentDef> agents, List<TaskDef> tasks)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    /// <summary>
    /// Решает задачу распределения методом DPOP
    /// </summary>
    public AllocationResult Solve()
    {
        int nA = _agents.Count;
        int nT = _tasks.Count;

        if (nA == 0 || nT == 0)
            return new AllocationResult { UnassignedTasks = nT };

        int[] parent = new int[nA];
        var children = new List<int>[nA];
        for (int a = 0; a < nA; a++) children[a] = new List<int>();
        parent[0] = -1;
        for (int a = 1; a < nA; a++)
        {
            parent[a] = a - 1;
            children[a - 1].Add(a);
        }

        var leafOrder = new List<int>();
        BuildLeafOrder(0, children, leafOrder);

        // UTIL фаза: снизу вверх
        // utilMsg[a][parentChoice] = минимальная стоимость поддерева a при заданном выборе родителя
        var utilMsg = new double[nA][];
        var bestChoice = new int[nA][];

        foreach (int a in leafOrder)
        {
            utilMsg[a] = new double[nT + 1];
            bestChoice[a] = new int[nT + 1];

            for (int pt = 0; pt <= nT; pt++)
            {
                double bestCost = double.MaxValue;
                int bestT = -1;

                for (int t = -1; t < nT; t++)
                {
                    if (t >= 0 && t == pt - 1) continue;

                    double cost = (t >= 0) ? GetCost(a, t) : 0;

                    foreach (int child in children[a])
                    {
                        int childParentChoice = t + 1;
                        if (childParentChoice < utilMsg[child].Length)
                            cost += utilMsg[child][childParentChoice];
                    }

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestT = t;
                    }
                }

                utilMsg[a][pt] = bestCost;
                bestChoice[a][pt] = bestT;
            }
        }

        // VALUE фаза: сверху вниз
        int[] assignment = new int[nA];
        assignment[0] = bestChoice[0][0];

        var bfsOrder = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int a = queue.Dequeue();
            bfsOrder.Add(a);
            foreach (int c in children[a]) queue.Enqueue(c);
        }

        foreach (int a in bfsOrder)
        {
            if (a == 0) continue;
            int parentChoice = assignment[parent[a]] + 1;
            if (parentChoice < bestChoice[a].Length)
                assignment[a] = bestChoice[a][parentChoice];
            else
                assignment[a] = -1;
        }

        ResolveConflicts(assignment, nA, nT);

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

    private void BuildLeafOrder(int node, List<int>[] children, List<int> order)
    {
        foreach (int c in children[node])
            BuildLeafOrder(c, children, order);
        order.Add(node);
    }

    private void ResolveConflicts(int[] assignment, int nA, int nT)
    {
        bool[] taken = new bool[nT];
        for (int a = 0; a < nA; a++)
        {
            int t = assignment[a];
            if (t < 0 || t >= nT) { assignment[a] = -1; continue; }
            if (taken[t])
            {
                assignment[a] = -1;
                for (int t2 = 0; t2 < nT; t2++)
                {
                    if (!taken[t2])
                    {
                        assignment[a] = t2;
                        taken[t2] = true;
                        break;
                    }
                }
            }
            else
            {
                taken[t] = true;
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TaskAllocation;

/// <summary>
/// Алгоритм Max-Sum (передача сообщений на факторном графе)
/// для распределённого распределения задач
/// </summary>
[Serializable]
public class MaxSum
{
    private readonly List<AgentDef> _agents;
    private readonly List<TaskDef> _tasks;
    private readonly int _maxIterations;

    /// <summary>
    /// Создаёт экземпляр алгоритма Max-Sum
    /// </summary>
    /// <param name="agents">Список агентов</param>
    /// <param name="tasks">Список задач</param>
    /// <param name="maxIterations">Максимальное число итераций</param>
    public MaxSum(List<AgentDef> agents, List<TaskDef> tasks, int maxIterations = 50)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Решает задачу распределения методом Max-Sum
    /// </summary>
    public AllocationResult Solve()
    {
        int nA = _agents.Count;
        int nT = _tasks.Count;

        // q[a,t] — сообщение от переменной (агент a) к фактору (задача t)
        double[,] qMsg = new double[nA, nT];
        // r[t,a] — сообщение от фактора (задача t) к переменной (агент a)
        double[,] rMsg = new double[nA, nT];

        for (int iter = 0; iter < _maxIterations; iter++)
        {
            var newR = new double[nA, nT];
            for (int t = 0; t < nT; t++)
            {
                for (int a = 0; a < nA; a++)
                {
                    double utility = _tasks[t].Value - GetCost(a, t);

                    double maxOther = double.MinValue;
                    for (int a2 = 0; a2 < nA; a2++)
                    {
                        if (a2 == a) continue;
                        double u2 = _tasks[t].Value - GetCost(a2, t) + qMsg[a2, t];
                        if (u2 > maxOther) maxOther = u2;
                    }

                    double rNoAssign = maxOther > 0 ? maxOther : 0;
                    newR[a, t] = utility - rNoAssign;
                }
            }
            Array.Copy(newR, rMsg, newR.Length);

            var newQ = new double[nA, nT];
            for (int a = 0; a < nA; a++)
            {
                double sumR = 0;
                for (int t2 = 0; t2 < nT; t2++)
                    sumR += Math.Max(rMsg[a, t2], 0);

                for (int t = 0; t < nT; t++)
                {
                    newQ[a, t] = sumR - Math.Max(rMsg[a, t], 0);
                }
            }
            Array.Copy(newQ, qMsg, newQ.Length);
        }

        int[] assignment = new int[nA];
        Array.Fill(assignment, -1);
        bool[] taskTaken = new bool[nT];

        var agentTaskPairs = new List<(double score, int a, int t)>();
        for (int a = 0; a < nA; a++)
            for (int t = 0; t < nT; t++)
                agentTaskPairs.Add((qMsg[a, t] + rMsg[a, t], a, t));

        agentTaskPairs.Sort((x, y) => y.score.CompareTo(x.score));

        int[] agentCount = new int[nA];
        foreach (var (score, a, t) in agentTaskPairs)
        {
            if (score <= 0) continue;
            if (taskTaken[t]) continue;
            if (agentCount[a] >= _agents[a].Capacity) continue;

            assignment[a] = t;
            taskTaken[t] = true;
            agentCount[a]++;
        }

        var result = new AllocationResult();
        for (int a = 0; a < nA; a++)
        {
            if (assignment[a] >= 0)
            {
                int t = assignment[a];
                result.Assignments.Add((_agents[a].Id, _tasks[t].Id));
                result.TotalCost += GetCost(a, t);
                result.TotalValue += _tasks[t].Value;
            }
        }

        result.UnassignedTasks = nT - result.Assignments.Count;
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

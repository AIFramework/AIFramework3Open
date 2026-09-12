using AI.Algorithms.TransportTask.PlanBuilders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.TransportTask.Methods;

/// <summary>
/// Метод потенциалов
/// </summary>
/// <remarks>
/// <para>
/// Базис хранится явно: m + n − 1 клеток, образующих остовное дерево на поставщиках
/// и потребителях. Вырожденный план дополняется нулевыми клетками, поэтому потенциалы
/// определены всегда. Входящая клетка — с наименьшей отрицательной оценкой, цикл пересчёта —
/// путь между её строкой и столбцом в дереве базиса, выходящая клетка — та из «вычитаемых»,
/// где перевозка меньше всего.
/// </para>
/// <para>
/// Прежняя реализация помечала стартовую клетку посещённой до поиска цикла, цикл не замыкался,
/// и метод возвращал начальный план без единого улучшения.
/// </para>
/// <para>
/// Несбалансированная задача решается с фиктивным поставщиком или потребителем нулевой стоимости:
/// в <see cref="BaseTransportTask.Allocation"/> остаются только настоящие клетки, а недовоз
/// или остаток виден как разность сумм плана и запасов.
/// </para>
/// </remarks>
[Serializable]
public class PotentialMethod : BaseTransportTask
{

    /// <summary>
    /// Метод построения начального опорного плана
    /// </summary>
    private readonly IInitialPlanBuilder _initialPlanBuilder;

    /// <summary>
    /// Метод потенциалов
    /// </summary>
    public PotentialMethod(double[,] costs, double[] supply, double[] demand, IInitialPlanBuilder initialPlanBuilder)
    {
        Costs = costs;
        Supply = supply;
        Demand = demand;
        Rows = supply.Length;
        Cols = demand.Length;
        _initialPlanBuilder = initialPlanBuilder;
        Allocation = new double[Rows, Cols];
    }

    /// <summary>
    /// Число выполненных улучшений плана
    /// </summary>
    public int Iterations { get; private set; }

    /// <summary>
    /// Достигнут ли оптимум: ложь, только если исчерпан предел итераций
    /// </summary>
    public bool ReachedOptimum { get; private set; }

    /// <summary>
    /// Решение задачи методом потенциалов
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Начальный план не выполняет запасы и потребности или содержит цикл занятых клеток
    /// </exception>
    public void Solve()
    {
        double supplyTotal = Supply.Sum();
        double demandTotal = Demand.Sum();
        double balanceTolerance = 1e-9 * Math.Max(1, Math.Max(supplyTotal, demandTotal));

        int m = Rows;
        int n = Cols;
        double[] supply = Supply;
        double[] demand = Demand;

        if (supplyTotal > demandTotal + balanceTolerance)
        {
            n++;
            demand = Demand.Append(supplyTotal - demandTotal).ToArray();
        }
        else if (demandTotal > supplyTotal + balanceTolerance)
        {
            m++;
            supply = Supply.Append(demandTotal - supplyTotal).ToArray();
        }

        double[,] costs = new double[m, n];
        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
                costs[i, j] = Costs[i, j];

        double[,] plan = _initialPlanBuilder.BuildInitialPlan(costs, supply, demand);
        double planTolerance = PlanTolerance(supply);

        RequireFeasible(plan, supply, demand, m, n, planTolerance);

        bool[,] basis = BuildBasis(plan, costs, m, n, planTolerance, throwOnCycle: true);
        double costTolerance = CostTolerance(costs);
        int maxIterations = (50 * m * n) + 100;

        Iterations = 0;
        ReachedOptimum = false;

        while (Iterations < maxIterations)
        {
            (double[] u, double[] v) = ComputePotentials(basis, costs, m, n);

            int enterI = -1;
            int enterJ = -1;
            double best = -costTolerance;

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (basis[i, j])
                        continue;

                    double delta = costs[i, j] - u[i] - v[j];
                    if (delta < best)
                    {
                        best = delta;
                        enterI = i;
                        enterJ = j;
                    }
                }
            }

            if (enterI < 0)
            {
                ReachedOptimum = true;
                break;
            }

            List<(int I, int J)> cycle = FindCycle(basis, m, n, enterI, enterJ);

            // Нечётные клетки цикла теряют перевозку; выходит та, где её меньше всего
            double theta = double.PositiveInfinity;
            int leave = -1;

            for (int k = 1; k < cycle.Count; k += 2)
            {
                double amount = plan[cycle[k].I, cycle[k].J];
                if (amount < theta)
                {
                    theta = amount;
                    leave = k;
                }
            }

            for (int k = 0; k < cycle.Count; k++)
                plan[cycle[k].I, cycle[k].J] += (k % 2 == 0 ? 1 : -1) * theta;

            basis[enterI, enterJ] = true;
            basis[cycle[leave].I, cycle[leave].J] = false;
            plan[cycle[leave].I, cycle[leave].J] = 0;
            Iterations++;
        }

        Allocation = new double[Rows, Cols];
        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
                Allocation[i, j] = Math.Max(0, plan[i, j]);
    }

    private static void RequireFeasible(double[,] plan, double[] supply, double[] demand, int m, int n, double tolerance)
    {
        if (plan.GetLength(0) != m || plan.GetLength(1) != n)
            throw new InvalidOperationException($"Начальный план имеет размер {plan.GetLength(0)}×{plan.GetLength(1)} вместо {m}×{n}");

        double slack = Math.Max(tolerance, 1e-7 * Math.Max(1, supply.Sum()));

        for (int i = 0; i < m; i++)
        {
            double row = 0;
            for (int j = 0; j < n; j++)
            {
                if (plan[i, j] < -tolerance)
                    throw new InvalidOperationException("Начальный план содержит отрицательную перевозку");

                row += plan[i, j];
            }

            if (Math.Abs(row - supply[i]) > slack)
                throw new InvalidOperationException($"Начальный план вывозит от поставщика {i} {row} вместо {supply[i]}");
        }

        for (int j = 0; j < n; j++)
        {
            double column = 0;
            for (int i = 0; i < m; i++)
                column += plan[i, j];

            if (Math.Abs(column - demand[j]) > slack)
                throw new InvalidOperationException($"Начальный план привозит потребителю {j} {column} вместо {demand[j]}");
        }
    }

    // Цикл: входящая клетка, затем путь в дереве базиса от её столбца обратно к её строке
    private static List<(int I, int J)> FindCycle(bool[,] basis, int m, int n, int enterI, int enterJ)
    {
        int[] previous = new int[m + n];
        Array.Fill(previous, -1);
        previous[enterI] = enterI;

        var queue = new Queue<int>();
        queue.Enqueue(enterI);
        int target = m + enterJ;

        while (queue.Count > 0 && previous[target] < 0)
        {
            int node = queue.Dequeue();

            if (node < m)
            {
                for (int j = 0; j < n; j++)
                {
                    if (basis[node, j] && previous[m + j] < 0)
                    {
                        previous[m + j] = node;
                        queue.Enqueue(m + j);
                    }
                }
            }
            else
            {
                int j = node - m;
                for (int i = 0; i < m; i++)
                {
                    if (basis[i, j] && previous[i] < 0)
                    {
                        previous[i] = node;
                        queue.Enqueue(i);
                    }
                }
            }
        }

        var cycle = new List<(int I, int J)> { (enterI, enterJ) };

        for (int node = target; node != enterI; node = previous[node])
        {
            int other = previous[node];
            cycle.Add(node < m ? (node, other - m) : (other, node - m));
        }

        return cycle;
    }
}

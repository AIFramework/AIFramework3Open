using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Algorithms.TransportTask;

/// <summary>
/// Базовый класс для решения транспортной задачи
/// </summary>
[Serializable]
public class BaseTransportTask
{
    /// <summary>
    /// Матрица стоимостей
    /// </summary>
    public double[,] Costs { get; protected set; }

    /// <summary>
    /// Объемы у поставщиков
    /// </summary>
    public double[] Supply { get; protected set; }

    /// <summary>
    /// Потребности потребителей
    /// </summary>
    public double[] Demand { get; protected set; }

    /// <summary>
    /// Матрица распределения ресурсов
    /// </summary>
    public double[,] Allocation { get; protected set; }

    /// <summary>
    /// Число строк в таблице
    /// </summary>
    public int Rows { get; protected set; }

    /// <summary>
    /// Число строк в таблице
    /// </summary>
    public int Cols { get; protected set; }


    /// <summary>
    /// Проверка решения на оптимальность
    /// </summary>
    /// <remarks>
    /// Базис — занятые клетки, дополненные до m + n − 1 нулевыми клетками без циклов: без этого
    /// у вырожденного плана часть потенциалов оставалась нулевой, и проверка находила мнимые
    /// отрицательные оценки. При вырожденном плане ответ «не оптимален» возможен и для оптимального
    /// плана — оценки зависят от того, какие нулевые клетки вошли в базис.
    /// </remarks>
    /// <param name="potentials">Потенциалы: строка 0 — поставщики, строка 1 — потребители</param>
    /// <param name="minDeltaI">Строка клетки с наименьшей отрицательной оценкой; −1, если план оптимален</param>
    /// <param name="minDeltaJ">Столбец этой клетки</param>
    public bool IsOptimal(out double[,] potentials, out int minDeltaI, out int minDeltaJ)
    {
        potentials = new double[2, Math.Max(Rows, Cols)];
        minDeltaI = -1;
        minDeltaJ = -1;

        bool[,] basis = BuildBasis(Allocation, Costs, Rows, Cols, PlanTolerance(Supply), throwOnCycle: false);
        (double[] u, double[] v) = ComputePotentials(basis, Costs, Rows, Cols);

        for (int i = 0; i < Rows; i++)
            potentials[0, i] = u[i];

        for (int j = 0; j < Cols; j++)
            potentials[1, j] = v[j];

        double minDelta = -CostTolerance(Costs);
        bool isOptimal = true;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Cols; j++)
            {
                if (basis[i, j])
                    continue;

                double delta = Costs[i, j] - (u[i] + v[j]);
                if (delta < minDelta)
                {
                    minDelta = delta;
                    minDeltaI = i;
                    minDeltaJ = j;
                    isOptimal = false;
                }
            }
        }

        return isOptimal;
    }

    /// <summary>
    /// Получение полной стоимости
    /// </summary>
    /// <returns></returns>
    public double GetTotalCost()
    {
        double totalCost = 0;
        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
                totalCost += Allocation[i, j] * Costs[i, j];

        return totalCost;
    }

    /// <summary>
    /// Получение средней стоимости по клеткам с ненулевыми затратами; нуль, если таких нет
    /// </summary>
    /// <returns></returns>
    public double GetMeanCost()
    {
        double totalCost = 0;
        int n = 0;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Cols; j++)
            {
                double costEl = Allocation[i, j] * Costs[i, j];
                totalCost += costEl;
                if (costEl > 0) n++;
            }
        }

        return n == 0 ? 0 : totalCost / n;
    }

    /// <summary>
    /// Отображение решения
    /// </summary>
    public void PrintSolution()
    {
        Console.WriteLine(ToString());
    }

    /// <summary>
    /// Перевод в строку
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("Оптимизированное распределение ресурсов:\n");
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Cols; j++)
                stringBuilder.Append($"{Allocation[i, j]:0.00}\t");

            stringBuilder.AppendLine();
        }

        stringBuilder.AppendLine($"\n\nОбщая стоимость решения: {GetTotalCost():0.00}");

        return stringBuilder.ToString();
    }

    /// <summary>Порог, ниже которого перевозка считается нулевой</summary>
    protected static double PlanTolerance(double[] supply)
    {
        double total = 0;
        foreach (double s in supply)
            total += Math.Abs(s);

        return 1e-9 * Math.Max(1, total);
    }

    /// <summary>Порог, ниже которого оценка клетки считается отрицательной</summary>
    protected static double CostTolerance(double[,] costs)
    {
        double largest = 0;
        foreach (double c in costs)
            largest = Math.Max(largest, Math.Abs(c));

        return 1e-9 * Math.Max(1, largest);
    }

    /// <summary>
    /// Базис опорного плана: занятые клетки плюс самые дешёвые нулевые клетки, соединяющие
    /// оставшиеся части, пока клеток не станет m + n − 1
    /// </summary>
    /// <param name="plan">План перевозок</param>
    /// <param name="costs">Стоимости</param>
    /// <param name="m">Число поставщиков</param>
    /// <param name="n">Число потребителей</param>
    /// <param name="tolerance">Порог нулевой перевозки</param>
    /// <param name="throwOnCycle">Бросать ли исключение, если занятые клетки образуют цикл</param>
    protected static bool[,] BuildBasis(double[,] plan, double[,] costs, int m, int n, double tolerance, bool throwOnCycle)
    {
        bool[,] basis = new bool[m, n];
        int[] parent = new int[m + n];
        for (int k = 0; k < parent.Length; k++)
            parent[k] = k;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        int count = 0;

        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (plan[i, j] <= tolerance)
                    continue;

                int a = Find(i);
                int b = Find(m + j);

                if (a == b)
                {
                    if (throwOnCycle)
                        throw new InvalidOperationException(
                            "Занятые клетки плана образуют цикл: метод потенциалов требует опорного плана");

                    continue;
                }

                parent[a] = b;
                basis[i, j] = true;
                count++;
            }
        }

        // Вырожденный план: недостающие базисные клетки — нулевые, каждая соединяет две части
        while (count < m + n - 1)
        {
            int bestI = -1;
            int bestJ = -1;
            double bestCost = double.PositiveInfinity;

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (!basis[i, j] && Find(i) != Find(m + j) && costs[i, j] < bestCost)
                    {
                        bestCost = costs[i, j];
                        bestI = i;
                        bestJ = j;
                    }
                }
            }

            parent[Find(bestI)] = Find(m + bestJ);
            basis[bestI, bestJ] = true;
            count++;
        }

        return basis;
    }

    /// <summary>
    /// Потенциалы по базису: <c>uᵢ + vⱼ = cᵢⱼ</c> на базисных клетках, <c>u₀ = 0</c>
    /// </summary>
    protected static (double[] U, double[] V) ComputePotentials(bool[,] basis, double[,] costs, int m, int n)
    {
        double[] u = new double[m];
        double[] v = new double[n];
        bool[] uKnown = new bool[m];
        bool[] vKnown = new bool[n];
        var queue = new Queue<int>();

        // Базис — остовное дерево на строках и столбцах, поэтому обход из строки 0 находит все потенциалы
        uKnown[0] = true;
        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            int node = queue.Dequeue();

            if (node < m)
            {
                for (int j = 0; j < n; j++)
                {
                    if (basis[node, j] && !vKnown[j])
                    {
                        v[j] = costs[node, j] - u[node];
                        vKnown[j] = true;
                        queue.Enqueue(m + j);
                    }
                }
            }
            else
            {
                int j = node - m;

                for (int i = 0; i < m; i++)
                {
                    if (basis[i, j] && !uKnown[i])
                    {
                        u[i] = costs[i, j] - v[j];
                        uKnown[i] = true;
                        queue.Enqueue(i);
                    }
                }
            }
        }

        return (u, v);
    }
}

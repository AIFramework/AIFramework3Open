using System;

namespace AI.Algorithms.Matching;

/// <summary>
/// Венгерский алгоритм (Куна-Манкреса) для решения задачи о назначениях.
/// Работает для квадратных и прямоугольных матриц стоимости.
/// </summary>
[Serializable]
public class Hungarian
{
    /// <summary>
    /// Результат назначения: Assignment[i] — столбец, назначенный строке i (-1, если не назначена)
    /// </summary>
    public int[] Assignment { get; private set; }

    /// <summary>
    /// Суммарная стоимость оптимального назначения
    /// </summary>
    public double TotalCost { get; private set; }

    /// <summary>
    /// Решает задачу о назначениях для данной матрицы стоимости
    /// </summary>
    /// <param name="costMatrix">Матрица стоимости (строки — работники, столбцы — задачи)</param>
    public Hungarian(double[,] costMatrix)
    {
        int rows = costMatrix.GetLength(0);
        int cols = costMatrix.GetLength(1);
        int n = Math.Max(rows, cols);

        double[,] c = new double[n, n];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                c[i, j] = costMatrix[i, j];

        double[] u = new double[n + 1];
        double[] v = new double[n + 1];
        int[] p = new int[n + 1];
        int[] way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            double[] minv = new double[n + 1];
            bool[] used = new bool[n + 1];

            for (int j = 0; j <= n; j++)
            {
                minv[j] = double.MaxValue;
                used[j] = false;
            }

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                double delta = double.MaxValue;
                int j1 = -1;

                for (int j = 1; j <= n; j++)
                {
                    if (!used[j])
                    {
                        double cur = c[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }
                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                }

                for (int j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            }
            while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            }
            while (j0 != 0);
        }

        Assignment = new int[rows];
        TotalCost = 0;

        for (int j = 1; j <= n; j++)
        {
            if (p[j] > 0 && p[j] <= rows && j <= cols)
            {
                Assignment[p[j] - 1] = j - 1;
                TotalCost += costMatrix[p[j] - 1, j - 1];
            }
        }

        for (int i = 0; i < rows; i++)
        {
            if (i >= n || Assignment[i] >= cols)
                Assignment[i] = -1;
        }
    }
}

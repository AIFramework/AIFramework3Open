using System;

namespace AI.Algorithms.Matching;

/// <summary>
/// Аукционный алгоритм Бертсекаса для задачи о назначениях (максимизация выгоды)
/// </summary>
[Serializable]
public class AuctionAlgorithm
{
    /// <summary>
    /// Результат назначения: Assignment[i] — объект, назначенный агенту i (-1, если не назначен)
    /// </summary>
    public int[] Assignment { get; private set; }

    /// <summary>
    /// Суммарная выгода оптимального назначения
    /// </summary>
    public double TotalBenefit { get; private set; }

    /// <summary>
    /// Решает задачу о назначениях аукционным методом
    /// </summary>
    /// <param name="benefitMatrix">Матрица выгод (строки — агенты, столбцы — объекты)</param>
    /// <param name="epsilon">Параметр epsilon аукциона</param>
    public AuctionAlgorithm(double[,] benefitMatrix, double epsilon = 1.0)
    {
        int n = benefitMatrix.GetLength(0);
        int m = benefitMatrix.GetLength(1);
        int size = Math.Max(n, m);

        double[,] b = new double[size, size];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                b[i, j] = benefitMatrix[i, j];

        double[] prices = new double[size];
        int[] personToObject = new int[size];
        int[] objectToPerson = new int[size];

        for (int i = 0; i < size; i++)
        {
            personToObject[i] = -1;
            objectToPerson[i] = -1;
        }

        double eps = epsilon;
        while (eps >= 1.0 / (size + 1))
        {
            for (int i = 0; i < size; i++)
            {
                personToObject[i] = -1;
                objectToPerson[i] = -1;
            }

            bool unassigned = true;
            int maxIter = size * size * 10;
            int iter = 0;

            while (unassigned && iter < maxIter)
            {
                unassigned = false;
                iter++;

                for (int i = 0; i < size; i++)
                {
                    if (personToObject[i] != -1) continue;
                    unassigned = true;

                    double bestValue = double.NegativeInfinity;
                    double secondBestValue = double.NegativeInfinity;
                    int bestJ = 0;

                    for (int j = 0; j < size; j++)
                    {
                        double val = b[i, j] - prices[j];
                        if (val > bestValue)
                        {
                            secondBestValue = bestValue;
                            bestValue = val;
                            bestJ = j;
                        }
                        else if (val > secondBestValue)
                        {
                            secondBestValue = val;
                        }
                    }

                    double bidIncrement = bestValue - secondBestValue + eps;
                    prices[bestJ] += bidIncrement;

                    if (objectToPerson[bestJ] != -1)
                    {
                        personToObject[objectToPerson[bestJ]] = -1;
                    }

                    personToObject[i] = bestJ;
                    objectToPerson[bestJ] = i;
                }
            }

            eps /= 2.0;
        }

        Assignment = new int[n];
        TotalBenefit = 0;

        for (int i = 0; i < n; i++)
        {
            if (personToObject[i] < m)
            {
                Assignment[i] = personToObject[i];
                TotalBenefit += benefitMatrix[i, personToObject[i]];
            }
            else
            {
                Assignment[i] = -1;
            }
        }
    }
}

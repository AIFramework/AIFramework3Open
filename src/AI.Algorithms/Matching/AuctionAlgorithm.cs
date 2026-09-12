using System;

namespace AI.Algorithms.Matching;

/// <summary>
/// Аукционный алгоритм Бертсекаса для задачи о назначениях (максимизация выгоды)
/// </summary>
/// <remarks>
/// <para>
/// Агенты по очереди торгуются за объекты, поднимая цену на разность лучшей и второй выгоды
/// плюс ε. Итог отличается от оптимума не больше чем на n·ε, поэтому ε уменьшается вдвое
/// до 1/(n + 1): при целых выгодах назначение тогда точно оптимально, при дробных — в пределах n·ε.
/// </para>
/// <para>
/// Каждая фаза идёт до полного назначения: число ставок конечно, потому что каждая поднимает
/// цену хотя бы на ε. Прежде фаза обрывалась по числу проходов, и в затяжной ценовой войне
/// агент оставался без объекта, а при малом начальном ε фаз не было вовсе.
/// </para>
/// </remarks>
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
    /// <param name="epsilon">Начальный шаг ставки; уменьшается до 1/(n + 1)</param>
    public AuctionAlgorithm(double[,] benefitMatrix, double epsilon = 1.0)
    {
        ArgumentNullException.ThrowIfNull(benefitMatrix);

        if (!(epsilon > 0) || double.IsInfinity(epsilon))
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Шаг ставки — конечное положительное число");

        int n = benefitMatrix.GetLength(0);
        int m = benefitMatrix.GetLength(1);
        int size = Math.Max(n, m);

        double[,] b = new double[size, size];
        double low = 0;
        double high = 0;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                if (!double.IsFinite(benefitMatrix[i, j]))
                    throw new ArgumentException("Выгоды должны быть конечными числами", nameof(benefitMatrix));

                b[i, j] = benefitMatrix[i, j];
                low = Math.Min(low, b[i, j]);
                high = Math.Max(high, b[i, j]);
            }
        }

        Assignment = new int[n];
        TotalBenefit = 0;

        if (size == 0)
            return;

        double[] prices = new double[size];
        int[] personToObject = new int[size];
        int[] objectToPerson = new int[size];

        double finalEps = 1.0 / (size + 1);
        double eps = epsilon;

        while (true)
        {
            RunPhase(b, size, eps, prices, personToObject, objectToPerson, high - low);

            if (eps <= finalEps)
                break;

            eps = Math.Max(eps / 2.0, finalEps);
        }

        for (int i = 0; i < n; i++)
        {
            int j = personToObject[i];

            if (j < m)
            {
                Assignment[i] = j;
                TotalBenefit += benefitMatrix[i, j];
            }
            else
            {
                Assignment[i] = -1;
            }
        }
    }

    private static void RunPhase(
        double[,] b, int size, double eps, double[] prices, int[] personToObject, int[] objectToPerson, double range)
    {
        for (int i = 0; i < size; i++)
        {
            personToObject[i] = -1;
            objectToPerson[i] = -1;
        }

        // Страховка от ошибки в данных, а не рабочий предел: каждая ставка поднимает цену хотя бы
        // на ε, и цены не могут расти дольше, чем на размах выгод плюс n·ε
        long limit = (long)(size * (size + 2) * ((range / eps) + size + 2)) + 1000;
        long bids = 0;
        int unassigned = size;
        int cursor = 0;

        while (unassigned > 0)
        {
            while (personToObject[cursor] != -1)
                cursor = (cursor + 1) % size;

            int i = cursor;
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

            // С одним объектом второй выгоды нет — ставка поднимает цену ровно на ε
            double increment = double.IsNegativeInfinity(secondBestValue) ? eps : bestValue - secondBestValue + eps;
            prices[bestJ] += increment;

            if (objectToPerson[bestJ] != -1)
                personToObject[objectToPerson[bestJ]] = -1;
            else
                unassigned--;

            personToObject[i] = bestJ;
            objectToPerson[bestJ] = i;

            if (++bids > limit)
                throw new InvalidOperationException("Аукцион не сошёлся: проверьте, что выгоды конечны");
        }
    }
}

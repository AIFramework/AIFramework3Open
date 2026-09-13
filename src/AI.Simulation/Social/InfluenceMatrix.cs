using AI.DataStructs.Algebraic;
using AI.Simulation.Space;

namespace AI.Simulation.Social;

/// <summary>
/// Матрица влияния: строка i — с какими весами агент i усредняет мнения, свои и чужие.
/// </summary>
/// <remarks>
/// Веса строки неотрицательны и в сумме дают единицу — матрица стохастическая по строкам. Вес w_ij
/// показывает, насколько агент i прислушивается к агенту j, и в общем случае w_ij ≠ w_ji: начальник
/// влияет на подчинённого сильнее, чем наоборот.
/// </remarks>
public static class InfluenceMatrix
{
    /// <summary>
    /// Матрица влияния по сети контактов: агент сохраняет долю своего мнения, а остаток делит поровну
    /// между контактами. У агента без контактов остаётся только своё мнение
    /// </summary>
    /// <param name="network">Сеть контактов</param>
    /// <param name="selfWeight">Доля своего мнения, от 0 до 1</param>
    public static Matrix FromNetwork(ContactNetwork network, double selfWeight = 0.5)
    {
        ArgumentNullException.ThrowIfNull(network);

        if (!(selfWeight >= 0 && selfWeight <= 1))
            throw new ArgumentOutOfRangeException(nameof(selfWeight), "Доля своего мнения лежит на [0; 1]");

        int n = network.NodeCount;
        var influence = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            int[] contacts = network.Contacts(i);

            if (contacts.Length == 0)
            {
                influence[i, i] = 1;
                continue;
            }

            influence[i, i] = selfWeight;
            double share = (1 - selfWeight) / contacts.Length;

            foreach (int j in contacts)
                influence[i, j] += share;
        }

        return influence;
    }

    /// <summary>Стохастическая ли матрица по строкам: квадратная, веса неотрицательны, строки дают единицу</summary>
    /// <param name="influence">Матрица</param>
    /// <param name="tolerance">Допуск суммы строки</param>
    public static bool IsRowStochastic(Matrix influence, double tolerance = 1e-9)
    {
        ArgumentNullException.ThrowIfNull(influence);

        if (influence.Height != influence.Width || influence.Height == 0)
            return false;

        for (int i = 0; i < influence.Height; i++)
        {
            double sum = 0;

            for (int j = 0; j < influence.Width; j++)
            {
                double weight = influence[i, j];

                if (!(weight >= 0) || double.IsInfinity(weight))
                    return false;

                sum += weight;
            }

            if (Math.Abs(sum - 1) > tolerance)
                return false;
        }

        return true;
    }

    /// <summary>Проверяет матрицу и возвращает её копию, чтобы модель не зависела от чужих изменений</summary>
    internal static Matrix Require(Matrix influence, string name)
    {
        ArgumentNullException.ThrowIfNull(influence, name);

        return IsRowStochastic(influence)
            ? influence.Copy()
            : throw new ArgumentException("Матрица влияния должна быть квадратной, с неотрицательными весами и суммой строки 1", name);
    }

    /// <summary>Следующее мнение каждого агента: x′ = W·x</summary>
    internal static double[] Apply(Matrix influence, IReadOnlyList<double> opinions)
    {
        int n = influence.Height;
        var next = new double[n];

        for (int i = 0; i < n; i++)
        {
            double sum = 0;

            for (int j = 0; j < n; j++)
                sum += influence[i, j] * opinions[j];

            next[i] = sum;
        }

        return next;
    }

    /// <summary>Проверяет вектор мнений: нужной длины и из конечных чисел</summary>
    internal static void RequireOpinions(IReadOnlyList<double> opinions, int count, string name)
    {
        ArgumentNullException.ThrowIfNull(opinions, name);

        if (opinions.Count != count)
            throw new ArgumentException($"Мнений {opinions.Count}, а агентов {count}", name);

        if (opinions.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Мнения — конечные числа", name);
    }
}

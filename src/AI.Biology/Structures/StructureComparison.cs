using AI.Solvers.Chem.Dynamics;
using AI.Solvers.Chem.Structures;

namespace AI.Biology.Structures;

/// <summary>Результат сравнения структур мерой TM</summary>
/// <param name="Score">TM-мера от нуля до единицы</param>
/// <param name="D0">Масштаб расстояния для длины белка, ангстремы</param>
/// <param name="ResiduesWithinD0">Число остатков ближе d₀ при лучшем совмещении</param>
/// <param name="Rmsd">Среднеквадратичное отклонение всех Cα при этом совмещении, ангстремы</param>
public readonly record struct TmScoreResult(double Score, double D0, int ResiduesWithinD0, double Rmsd);

/// <summary>
/// Сравнение пространственных структур белков с известным соответствием остатков.
/// </summary>
/// <remarks>
/// <para>
/// RMSD после совмещения по Кабшу — самая привычная мера и самая обманчивая: одна сдвинутая
/// петля раздувает её так же, как неверная укладка целиком. TM-мера Чжана и Сколника (2004)
/// суммирует <c>1/(1 + (dᵢ/d₀)²)</c> по остаткам, и далёкие остатки почти ничего в неё не вносят.
/// Масштаб <c>d₀ = 1,24·∛(L − 15) − 1,8</c> убирает зависимость от длины: у случайных пар белков
/// мера около 0,17 при любой длине, а выше 0,5 — почти всегда одна укладка.
/// </para>
/// <para>
/// Совмещение, дающее наибольшую TM-меру, в общем случае не совпадает с совмещением по всем
/// остаткам. Оно ищется, как в программе TM-score: совмещение по фрагментам длины L, L/2, L/4…
/// и итерации «оставить остатки ближе d₀ — совместить по ним заново». Поиск эвристический, и
/// найденное значение — оценка максимума снизу. Совмещение и RMSD берутся из химического модуля.
/// </para>
/// <para>
/// Остатки сопоставляются по порядку: структуры одного белка или предварительно выровненные.
/// Выравнивания структур без известного соответствия (TM-align) здесь нет.
/// </para>
/// </remarks>
public static class StructureComparison
{
    /// <summary>RMSD атомов Cα после наилучшего совмещения, ангстремы</summary>
    /// <param name="model">Сравниваемая структура</param>
    /// <param name="reference">Образец</param>
    public static double Rmsd(ProteinStructure model, ProteinStructure reference)
    {
        (MolecularStructure mobile, MolecularStructure target) = Traces(model, reference);

        return StructureAlignment.Align(mobile, target).Rmsd;
    }

    /// <summary>TM-мера модели относительно образца</summary>
    /// <param name="model">Сравниваемая структура</param>
    /// <param name="reference">Образец; его длина задаёт нормировку</param>
    public static TmScoreResult TmScore(ProteinStructure model, ProteinStructure reference)
    {
        (MolecularStructure mobile, MolecularStructure target) = Traces(model, reference);

        int length = target.Count;
        double d0 = length > 21 ? (1.24 * Math.Cbrt(length - 15)) - 1.8 : 0.5;

        double bestScore = double.NegativeInfinity;
        double[] bestDistances = [];

        void Refine(IReadOnlyList<int> seed)
        {
            IReadOnlyList<int> current = seed;

            for (int iteration = 0; iteration < 20; iteration++)
            {
                double[] distances = Distances(mobile, target, current);
                double score = distances.Sum(d => 1 / (1 + ((d / d0) * (d / d0)))) / length;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDistances = distances;
                }

                double cutoff = d0;
                List<int> next;

                do
                {
                    double limit = cutoff;
                    next = Enumerable.Range(0, length).Where(k => distances[k] < limit).ToList();
                    cutoff += 0.5;
                }
                while (next.Count < 3);

                if (next.SequenceEqual(current))
                    break;

                current = next;
            }
        }

        for (int size = length; ; size /= 2)
        {
            int step = Math.Max(1, size / 2);

            for (int start = 0; start + size <= length; start += step)
                Refine(Enumerable.Range(start, size).ToArray());

            if (size / 2 < 4)
                break;
        }

        return new TmScoreResult(
            bestScore,
            d0,
            bestDistances.Count(d => d < d0),
            Math.Sqrt(bestDistances.Average(d => d * d)));
    }

    private static double[] Distances(MolecularStructure mobile, MolecularStructure target, IReadOnlyList<int> indices)
    {
        MolecularStructure aligned = StructureAlignment.Align(mobile, target, indices).Aligned;

        return Enumerable.Range(0, target.Count)
            .Select(k => aligned.Atoms[k].Position.DistanceTo(target.Atoms[k].Position))
            .ToArray();
    }

    private static (MolecularStructure Mobile, MolecularStructure Target) Traces(ProteinStructure model, ProteinStructure reference)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reference);

        MolecularStructure mobile = model.CAlphaTrace();
        MolecularStructure target = reference.CAlphaTrace();

        if (mobile.Count != target.Count)
            throw new ArgumentException(
                $"Число остатков с Cα различается: {mobile.Count} и {target.Count}. Остатки сопоставляются по порядку");

        if (target.Count < 3)
            throw new ArgumentException("Для совмещения нужно не меньше трёх остатков");

        return (mobile, target);
    }
}

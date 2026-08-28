namespace AI.Script.Semantics;

/// <summary>
/// Подбор ближайшего имени для подсказки «возможно, имелось в виду».
/// </summary>
/// <remarks>
/// Расстояние Дамерау—Левенштейна, а не Левенштейна: транспозиция соседних символов
/// (<c>kmenas</c> вместо <c>kmeans</c>) — самая частая опечатка и человека, и модели, и она
/// должна стоить один шаг, а не два.
/// </remarks>
public static class Suggestions
{
    /// <summary>Ближайшее имя из набора либо <c>null</c>, если ничего похожего нет.</summary>
    /// <param name="target">Имя, которое не нашлось.</param>
    /// <param name="candidates">Набор известных имён.</param>
    public static string? Closest(string target, IEnumerable<string> candidates)
    {
        if (string.IsNullOrEmpty(target)) return null;

        int limit = Math.Max(1, target.Length / 3 + 1);
        string? best = null;
        int bestDistance = int.MaxValue;

        foreach (string candidate in candidates)
        {
            if (string.Equals(candidate, target, StringComparison.Ordinal)) continue;

            if (Math.Abs(candidate.Length - target.Length) > limit) continue;

            int distance = Distance(target, candidate, limit);

            if (distance > limit || distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
        }

        return best;
    }

    /// <summary>До трёх ближайших имён, от самого похожего.</summary>
    public static IReadOnlyList<string> Nearest(string target, IEnumerable<string> candidates, int count = 3)
    {
        if (string.IsNullOrEmpty(target)) return [];

        int limit = Math.Max(2, (target.Length / 2) + 1);
        var scored = new List<(string Name, int Distance)>();

        foreach (string candidate in candidates)
        {
            if (string.Equals(candidate, target, StringComparison.Ordinal)) continue;

            int distance = Distance(target, candidate, limit);
            if (distance <= limit) scored.Add((candidate, distance));
        }

        scored.Sort((left, right) => left.Distance != right.Distance
            ? left.Distance.CompareTo(right.Distance)
            : string.CompareOrdinal(left.Name, right.Name));

        var result = new List<string>(count);

        for (int i = 0; i < scored.Count && result.Count < count; i++) result.Add(scored[i].Name);

        return result;
    }

    /// <summary>Расстояние Дамерау—Левенштейна с ранним выходом по пределу.</summary>
    public static int Distance(string left, string right, int limit = int.MaxValue)
    {
        int rows = left.Length + 1;
        int columns = right.Length + 1;
        var previous = new int[columns];
        var current = new int[columns];
        var beforePrevious = new int[columns];

        for (int j = 0; j < columns; j++) previous[j] = j;

        for (int i = 1; i < rows; i++)
        {
            current[0] = i;
            int rowBest = current[0];

            for (int j = 1; j < columns; j++)
            {
                int cost = char.ToLowerInvariant(left[i - 1]) == char.ToLowerInvariant(right[j - 1]) ? 0 : 1;

                int value = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);

                if (i > 1 && j > 1
                    && char.ToLowerInvariant(left[i - 1]) == char.ToLowerInvariant(right[j - 2])
                    && char.ToLowerInvariant(left[i - 2]) == char.ToLowerInvariant(right[j - 1]))
                {
                    value = Math.Min(value, beforePrevious[j - 2] + cost);
                }

                current[j] = value;
                if (value < rowBest) rowBest = value;
            }

            if (rowBest > limit) return limit + 1;

            Array.Copy(previous, beforePrevious, columns);
            Array.Copy(current, previous, columns);
        }

        return previous[columns - 1];
    }
}

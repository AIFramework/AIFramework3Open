using AI.Extensions;

namespace AiFramework.Tools.Atlas;

/// <summary>Пара выходов на одном входе, развёрнутых в числа.</summary>
/// <param name="A">Выход первого метода.</param>
/// <param name="B">Выход второго метода.</param>
/// <param name="N">Длина рядов входа.</param>
public sealed record OutputPair(double[] A, double[] B, int N);

/// <summary>Связь выходов: как из выхода A получить выход B.</summary>
/// <param name="Name">Запись для человека: «равны», «множитель n/(n−1)», «сдвиг 0,5».</param>
/// <param name="Holds">Выполняется ли связь на паре.</param>
public sealed record Relation(string Name, Func<OutputPair, bool> Holds)
{
    /// <summary>Выходы совпадают.</summary>
    public bool IsIdentity => Name == OutputRelation.Equal;
}

/// <summary>
/// Подбор связи между выходами двух методов по выборке входов: равны, знак, постоянный
/// множитель, множитель от длины входа (n/(n−1) у дисперсии со смещением и без), сдвиг,
/// нормировка.
/// </summary>
/// <remarks>
/// Связь принимается, только если выполняется на всех парах. Допуск относительный 1e-6 и
/// абсолютный 1e-9: две верные реализации одной формулы расходятся на ошибки округления,
/// а не на миллионные доли. Для вычислений во <c>float</c> вызывающий передаёт допуск
/// <see cref="FloatRelTol"/>.
/// </remarks>
public static class OutputRelation
{
    /// <summary>Относительный допуск.</summary>
    public const double RelTol = 1e-6;

    /// <summary>Относительный допуск, если хотя бы один метод считает во <c>float</c>.</summary>
    public const double FloatRelTol = 1e-4;

    /// <summary>Абсолютный допуск.</summary>
    public const double AbsTol = 1e-9;

    /// <summary>Имя тождественной связи.</summary>
    public const string Equal = "равны";

    private static readonly (string Name, Func<int, double> Factor)[] LengthFactors =
    [
        ("n/(n−1)", n => n / (n - 1.0)),
        ("(n−1)/n", n => (n - 1.0) / n),
        ("√(n/(n−1))", n => Math.Sqrt(n / (n - 1.0))),
        ("√((n−1)/n)", n => Math.Sqrt((n - 1.0) / n)),
        ("n", n => n),
        ("1/n", n => 1.0 / n),
    ];

    /// <summary>Тождество с допуском <paramref name="relTol"/>.</summary>
    public static Relation Identity(double relTol = RelTol) => new(Equal, pair => Same(pair.A, pair.B, relTol));

    /// <summary>Связь, которая выполняется на всех парах; <c>null</c> — связи нет.</summary>
    public static Relation? Fit(IReadOnlyList<OutputPair> pairs, double relTol = RelTol)
    {
        if (pairs.Count == 0) return null;
        if (Identity(relTol) is var identity && pairs.All(identity.Holds)) return identity;
        if (pairs.Any(pair => pair.A.Length != pair.B.Length)) return null;

        var candidates = new List<Relation> { new("знак: B = −A", pair => Same(Scale(pair.A, -1), pair.B, relTol)) };

        if (pairs.All(pair => pair.N >= 2))
            candidates.AddRange(LengthFactors.Select(f => new Relation($"множитель {f.Name}", pair => Same(Scale(pair.A, f.Factor(pair.N)), pair.B, relTol))));

        (double ab, double aa) = pairs.SelectMany(pair => pair.A.Zip(pair.B)).Where(v => double.IsFinite(v.First) && double.IsFinite(v.Second))
            .Aggregate((Ab: 0.0, Aa: 0.0), (sum, v) => (sum.Ab + v.First * v.Second, sum.Aa + v.First * v.First));
        if (aa > 0 && ab / aa is double c && !AlgebraicStructsExtensions.ApproxEquals(c, 1, relTol) && Math.Abs(c) > AbsTol)
            candidates.Add(new Relation($"множитель {c:G6}", pair => Same(Scale(pair.A, c), pair.B, relTol)));

        double[] shifts = [.. pairs.SelectMany(pair => pair.A.Zip(pair.B, (a, b) => b - a)).Where(double.IsFinite)];
        if (shifts.Length > 0 && shifts.Average() is double d && Math.Abs(d) > AbsTol)
            candidates.Add(new Relation($"сдвиг {d:G6}", pair => Same(pair.A.Select(v => v + d).ToArray(), pair.B, relTol)));

        Relation? fitted = candidates.FirstOrDefault(relation => pairs.All(relation.Holds));
        if (fitted != null) return fitted;

        // Два числа пропорциональны всегда: нормировку видно только на рядах.
        double[] ratios = [.. pairs.Select(Ratio).Where(double.IsFinite)];
        bool Proportional(OutputPair pair) =>
            pair.A.Length == pair.B.Length && Ratio(pair) is double k && double.IsFinite(k) && Same(Scale(pair.A, k), pair.B, relTol);

        return pairs.All(pair => pair.A.Length >= 2) && ratios.Length == pairs.Count && ratios.Distinct().Count() > 1 && pairs.All(Proportional)
            ? new Relation("нормировка: B = A·k, k зависит от входа", Proportional)
            : null;
    }

    /// <summary>Наибольшее относительное отклонение B от A по конечным значениям.</summary>
    public static double Deviation(OutputPair pair) =>
        pair.A.Zip(pair.B)
            .Where(v => double.IsFinite(v.First) && double.IsFinite(v.Second))
            .Select(v => Math.Abs(v.First - v.Second) / Math.Max(Math.Max(Math.Abs(v.First), Math.Abs(v.Second)), 1))
            .DefaultIfEmpty(0)
            .Max();

    private static bool Same(double[] a, double[] b, double relTol)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!AlgebraicStructsExtensions.ApproxEquals(a[i], b[i], relTol, AbsTol)) return false;
        return true;
    }

    private static double[] Scale(double[] values, double factor) => [.. values.Select(v => v * factor)];

    private static double Ratio(OutputPair pair)
    {
        double ab = 0, aa = 0;
        for (int i = 0; i < pair.A.Length; i++)
        {
            if (!double.IsFinite(pair.A[i]) || !double.IsFinite(pair.B[i])) continue;
            ab += pair.A[i] * pair.B[i];
            aa += pair.A[i] * pair.A[i];
        }
        return aa > 0 ? ab / aa : double.NaN;
    }

}

using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Согласие двух оценщиков: ранговая корреляция и каппа Коэна.
/// </summary>
/// <remarks>
/// Нужны там, где одну и ту же выборку оценили двое: человек и судья-модель, два разметчика.
/// Корреляция Пирсона здесь обманывает: судья, который ставит всем на балл больше человека,
/// коррелирует с ним идеально, а согласен ли он с человеком, по ней не видно.
/// </remarks>
public static partial class StatModule
{
    [ScriptFn("spearman", "Ранговая корреляция Спирмена: согласие порядков двух выборок",
        Example = "stat.spearman(x, y)")]
    public static double Spearman(
        [ScriptParam("первая выборка")] Vector x,
        [ScriptParam("вторая выборка")] Vector y)
    {
        RequirePair(x, y, "spearman");

        return Correlation(Ranks(x), Ranks(y));
    }

    /// <summary>
    /// Каппа Коэна для оценок по шкале.
    /// </summary>
    /// <remarks>
    /// Квадратичные веса по умолчанию: оценки по шкале упорядочены, и расхождение «4 против 5»
    /// не должно стоить столько же, сколько «1 против 5». Без весов каппа годится для меток без
    /// порядка. Оценки округляются до целых: каппа считается по категориям, а не по числам.
    /// </remarks>
    [ScriptFn("kappa", "Каппа Коэна: согласие оценок сверх случайного, от -1 до 1",
        Example = "stat.kappa(x, y, weights: \"linear\")")]
    public static double Kappa(
        [ScriptParam("оценки первого")] Vector a,
        [ScriptParam("оценки второго")] Vector b,
        [ScriptParam("веса расхождений: quadratic, linear либо none")] string weights = "quadratic")
    {
        RequirePair(a, b, "kappa");

        int[] left = Categories(a, "kappa");
        int[] right = Categories(b, "kappa");

        // Шкала это встреченные значения, а не весь диапазон между ними: оценки 1 и 100000 не
        // должны требовать матрицы на сто тысяч строк. Вес расхождения считается по самим
        // значениям, поэтому пропуски на шкале ничего не меняют.
        int[] levels = [.. left.Concat(right).Distinct().Order()];
        var index = new Dictionary<int, int>(levels.Length);

        for (int i = 0; i < levels.Length; i++) index[levels[i]] = i;

        int size = levels.Length;
        Func<int, int, double> weight = Weight(weights, levels);
        var observed = new double[size, size];
        var rows = new double[size];
        var columns = new double[size];

        for (int i = 0; i < left.Length; i++)
        {
            int row = index[left[i]];
            int column = index[right[i]];

            observed[row, column] += 1.0 / left.Length;
            rows[row] += 1.0 / left.Length;
            columns[column] += 1.0 / left.Length;
        }

        double disagreement = 0, chance = 0;

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                disagreement += weight(i, j) * observed[i, j];
                chance += weight(i, j) * rows[i] * columns[j];
            }
        }

        // Оба оценщика поставили всем одно и то же: согласие полное, случайному взяться неоткуда.
        return chance == 0 ? 1 : 1 - (disagreement / chance);
    }

    /// <summary>Ранги со средним рангом у равных значений.</summary>
    private static Vector Ranks(Vector values)
    {
        int[] order = [.. Enumerable.Range(0, values.Count).OrderBy(i => values[i])];
        var ranks = new double[values.Count];

        for (int start = 0; start < order.Length;)
        {
            int end = start;

            while (end + 1 < order.Length && values[order[end + 1]] == values[order[start]]) end++;

            double rank = ((start + end) / 2.0) + 1;

            for (int k = start; k <= end; k++) ranks[order[k]] = rank;

            start = end + 1;
        }

        return new Vector(ranks);
    }

    private static int[] Categories(Vector values, string name)
    {
        var categories = new int[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            categories[i] = double.IsFinite(values[i]) && Math.Abs(values[i]) < int.MaxValue
                ? (int)Math.Round(values[i])
                : throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"stat.{name}: оценка {values[i]} не число шкалы",
                    "уберите пропуски из обеих выборок: table.drop_na");
        }

        return categories;
    }

    /// <summary>Вес расхождения по значениям шкалы; размах шкалы только масштабирует веса.</summary>
    private static Func<int, int, double> Weight(string weights, int[] levels)
    {
        double span = Math.Max(1, levels[^1] - levels[0]);

        return weights switch
        {
            "quadratic" => (i, j) => (levels[i] - levels[j]) * (double)(levels[i] - levels[j]) / (span * span),
            "linear" => (i, j) => Math.Abs(levels[i] - levels[j]) / span,
            "none" => (i, j) => i == j ? 0 : 1,
            _ => throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"stat.kappa: неизвестные веса «{weights}»",
                "известны quadratic, linear и none"),
        };
    }
}

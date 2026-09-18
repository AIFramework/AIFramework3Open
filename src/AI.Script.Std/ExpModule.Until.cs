using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Остановка опыта по точности и по бюджету.
/// </summary>
/// <remarks>
/// Потолки расходов прогона срывают его отказом — и вместе с отказом пропадает всё, что опыт
/// уже посчитал и оплатил. Опыту нужно другое: повторять, пока интервал не станет достаточно
/// узким, и остановиться с частичным итогом, когда кончились деньги.
/// <para>
/// Повторы идут кругами: круг — одно испытание на каждую точку плана. Проверка после круга, а не
/// после испытания: иначе точки в конце плана систематически получали бы меньше повторов, чем в
/// начале, и сравнение между ними перекашивалось бы.
/// </para>
/// </remarks>
public static partial class ExpModule
{
    /// <summary>Колонка причины остановки в итогах опыта с <c>until</c>.</summary>
    public const string StoppedColumn = "stopped";

    /// <summary>Сделаны все заказанные повторы.</summary>
    public const string StoppedByRepeat = "repeat";

    /// <summary>Интервал сузился до заказанного.</summary>
    public const string StoppedByPrecision = "precision";

    /// <summary>Кончился бюджет опыта либо потолок расходов прогона.</summary>
    public const string StoppedByBudget = "budget";

    /// <summary>Сколько повторов нужно, прежде чем судить о ширине интервала: по двум — рано.</summary>
    public const int MinimumRounds = 3;

    /// <summary>Множитель нормального интервала 95%.</summary>
    private const double Z95 = 1.96;

    private static readonly string[] s_untilKeys = ["ci_width", "metric", "cost", "tokens"];

    private static async Task<ScriptTable> RunUntilAsync(
        IScriptContext context,
        ScriptTable plan,
        ScriptCallable trial,
        int repeat,
        bool parallel,
        ScriptRecord until)
    {
        Stop stop = Stop.Of(until);
        ExternalUsage before = context.Usage;

        // Без запаса по repeat: с until это лишь верхняя граница, и опыт обычно кончается раньше.
        var points = new List<ScriptValue>(plan.RowCount);
        var numbers = new List<int>(plan.RowCount);
        var outcomes = new List<ScriptValue>(plan.RowCount);
        string reason = StoppedByRepeat;

        for (int round = 0; round < repeat; round++)
        {
            var roundPoints = new List<ScriptValue>(plan.RowCount);
            var roundNumbers = new List<int>(plan.RowCount);

            for (int row = 0; row < plan.RowCount; row++)
            {
                roundPoints.Add(ScriptValue.Record(plan.Row(row)));
                roundNumbers.Add(round);
            }

            ScriptValue[] results;

            try
            {
                results = await RoundAsync(context, trial, roundPoints, roundNumbers, parallel).ConfigureAwait(false);
            }
            catch (ScriptError error) when (error.Code == DiagnosticCodes.CostLimit)
            {
                // Потолок прогона сработал посреди круга: круг не засчитывается целиком, а всё
                // посчитанное до него остаётся итогом — ради этого остановка и заведена.
                reason = StoppedByBudget;
                break;
            }

            points.AddRange(roundPoints);
            numbers.AddRange(roundNumbers);
            outcomes.AddRange(results);

            if (stop.Spent(context.Usage, before))
            {
                reason = StoppedByBudget;
                break;
            }

            if (round + 1 >= MinimumRounds && stop.Width is double width
                && Narrow(plan.RowCount, outcomes, stop.Metric, width))
            {
                reason = StoppedByPrecision;
                break;
            }
        }

        if (outcomes.Count == 0)
        {
            throw new ScriptError(
                DiagnosticCodes.CostLimit,
                "exp.run: бюджет кончился раньше, чем посчитался первый круг испытаний",
                "уменьшите план либо поднимите бюджет опыта");
        }

        ScriptTable table = Outcomes(plan, points, numbers, outcomes);
        var marks = new ScriptValue[table.RowCount];

        Array.Fill(marks, ScriptValue.Str(reason));

        return table.With(ScriptColumn.Own(StoppedColumn, marks));
    }

    /// <summary>
    /// Сузился ли интервал среднего метрики у КАЖДОЙ точки плана.
    /// </summary>
    /// <remarks>
    /// Всех точек, а не в среднем: сравнение держится на самой широкой из них, и узкий интервал у
    /// девяти точек не спасает вывод, если десятая всё ещё пляшет.
    /// </remarks>
    private static bool Narrow(int pointsPerRound, IReadOnlyList<ScriptValue> outcomes, string? metric, double width)
    {
        for (int point = 0; point < pointsPerRound; point++)
        {
            var values = new List<double>();

            for (int i = point; i < outcomes.Count; i += pointsPerRound)
            {
                ScriptRecord record = Metrics(outcomes[i]);
                ScriptValue value = metric is null
                    ? record.Values.FirstOrDefault(v => v.Type is ScriptType.Num or ScriptType.Dec)
                    : record.TryGet(metric, out ScriptValue found) ? found : ScriptValue.None;

                if (value.Type is ScriptType.Num or ScriptType.Dec) values.Add(TableModule.Numeric(value));
            }

            if (values.Count < 2) return false;

            double mean = values.Average();
            double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);

            if (2 * Z95 * Math.Sqrt(variance / values.Count) > width) return false;
        }

        return true;
    }

    /// <summary>Условия остановки, разобранные из записи <c>until</c>.</summary>
    private sealed record Stop(double? Width, string? Metric, decimal? Cost, long? Tokens)
    {
        public static Stop Of(ScriptRecord until)
        {
            foreach (string key in until.Keys)
            {
                if (Array.IndexOf(s_untilKeys, key) < 0)
                {
                    throw new ScriptError(
                        DiagnosticCodes.UnknownArgument,
                        $"exp.run: неизвестное условие остановки «{key}»",
                        $"известны: {string.Join(", ", s_untilKeys)}");
                }
            }

            double? width = until.TryGet("ci_width", out ScriptValue w) ? w.AsNumber("exp.run: ci_width") : null;
            string? metric = until.TryGet("metric", out ScriptValue m) ? m.AsString("exp.run: metric") : null;
            decimal? cost = until.TryGet("cost", out ScriptValue c)
                ? c.Type == ScriptType.Dec ? c.AsDecimal() : (decimal)c.AsNumber("exp.run: cost")
                : null;
            long? tokens = until.TryGet("tokens", out ScriptValue k) ? (long)k.AsNumber("exp.run: tokens") : null;

            if (width is <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, "exp.run: ci_width должна быть больше нуля");

            return new Stop(width, metric, cost, tokens);
        }

        /// <summary>Потрачен ли бюджет ОПЫТА — считается от начала этого вызова, а не прогона.</summary>
        public bool Spent(ExternalUsage now, ExternalUsage before) =>
            (Cost is decimal cost && now.Cost - before.Cost >= cost)
            || (Tokens is long tokens && now.Tokens - before.Tokens >= tokens);
    }
}

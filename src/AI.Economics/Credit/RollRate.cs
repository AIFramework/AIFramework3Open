using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Economics.Credit;

/// <summary>Переход между соседними корзинами просрочки.</summary>
/// <param name="FromBucket">Исходная корзина.</param>
/// <param name="ToBucket">Следующая корзина.</param>
/// <param name="AverageRollRate">Средняя доля перетекающего остатка.</param>
/// <param name="StandardDeviation">Разброс доли по периодам.</param>
/// <param name="Observations">Число периодов в оценке.</param>
/// <param name="LatestRollRate">Доля в последнем наблюдённом периоде.</param>
public sealed record RollRateStep(
    string FromBucket, string ToBucket, double AverageRollRate,
    double StandardDeviation, int Observations, double LatestRollRate);

/// <summary>Итог анализа скорости перетекания просрочки.</summary>
public sealed record RollRateResult : IInterpretable
{
    /// <summary>Корзины просрочки от текущей к списанию.</summary>
    public IReadOnlyList<string> Buckets { get; init; } = [];

    /// <summary>Переходы между соседними корзинами.</summary>
    public IReadOnlyList<RollRateStep> Steps { get; init; } = [];

    /// <summary>Доля остатка каждой корзины, доходящая до списания.</summary>
    public IReadOnlyList<double> RollToLoss { get; init; } = [];

    /// <summary>Остатки по корзинам на последнюю дату.</summary>
    public IReadOnlyList<double> LatestBalances { get; init; } = [];

    /// <summary>Ожидаемые потери из текущих остатков.</summary>
    public double ImpliedLoss { get; init; }

    /// <summary>Ожидаемые потери в долях от портфеля.</summary>
    public double ImpliedLossRate { get; init; }

    /// <summary>Число периодов наблюдения.</summary>
    public int Periods { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        RollRateStep? worst = Steps.OrderByDescending(s => s.AverageRollRate).FirstOrDefault();
        RollRateStep? unstable = Steps
            .Where(s => s.AverageRollRate > 0)
            .OrderByDescending(s => s.StandardDeviation / s.AverageRollRate)
            .FirstOrDefault();

        RollRateStep? deteriorating = Steps
            .Where(s => s.Observations > 1)
            .OrderByDescending(s => s.LatestRollRate - s.AverageRollRate)
            .FirstOrDefault();

        double entryRoll = Steps.Count > 0 ? Steps[0].AverageRollRate : 0;
        double currentRollToLoss = RollToLoss.Count > 0 ? RollToLoss[0] : 0;

        var builder = new InterpretationBuilder("Анализ скорости перетекания просрочки")
            .Summary($"По {Periods} периодам оценены переходы между {Buckets.Count} корзинами. " +
                     $"Из текущей задолженности до списания доходит {Fmt.Pct(currentRollToLoss, 3)}; " +
                     $"ожидаемые потери из сложившихся остатков — {Fmt.Money(ImpliedLoss)} " +
                     $"({Fmt.Pct(ImpliedLossRate, 2)} портфеля).")
            .Metric("Ожидаемые потери", Fmt.Money(ImpliedLoss), null,
                $"{Fmt.Pct(ImpliedLossRate, 2)} от текущих остатков",
                ImpliedLossRate > 0.05 ? MetricQuality.Critical
                    : ImpliedLossRate > 0.02 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Вход в просрочку", entryRoll, null,
                "доля текущей задолженности, уходящая в первую корзину просрочки",
                entryRoll > 0.05 ? MetricQuality.Warning : MetricQuality.Neutral, 4)
            .Metric("Путь до списания", currentRollToLoss, null,
                "доля текущей задолженности, доходящая до списания",
                MetricQuality.Neutral, 4);

        for (int i = 0; i < Steps.Count; i++)
        {
            RollRateStep step = Steps[i];
            builder.Metric($"{step.FromBucket} -> {step.ToBucket}", step.AverageRollRate, null,
                $"разброс {Fmt.Pct(step.StandardDeviation, 1)}, последний период {Fmt.Pct(step.LatestRollRate, 1)}",
                MetricQuality.Unknown, 3);
        }

        return builder
            .FindingIf(worst is not null,
                $"Быстрее всего просрочка перетекает на шаге «{worst?.FromBucket} -> {worst?.ToBucket}»: " +
                $"{Fmt.Pct(worst?.AverageRollRate ?? 0, 1)} остатка за период. Это узкое место " +
                "процесса взыскания и главная точка приложения усилий.")
            .FindingIf(deteriorating is not null && deteriorating.LatestRollRate > deteriorating.AverageRollRate,
                $"На шаге «{deteriorating?.FromBucket} -> {deteriorating?.ToBucket}» последний период " +
                $"хуже среднего: {Fmt.Pct(deteriorating?.LatestRollRate ?? 0, 1)} против " +
                $"{Fmt.Pct(deteriorating?.AverageRollRate ?? 0, 1)}. Ранний сигнал ухудшения сбора.")
            .Finding("Метод перетекания отвечает на вопрос, который не решает средняя доля " +
                     "просрочки: сколько из сегодняшних остатков превратится в убыток. " +
                     "Он же даёт быструю проверку резервов, посчитанных другими методами.")
            .WarningIf(Periods < 6,
                $"Оценка построена всего по {Periods} периодам. Ставки перетекания сезонны, " +
                "и короткий ряд легко принять за тренд.")
            .WarningIf(unstable is not null && unstable.StandardDeviation > unstable.AverageRollRate * 0.5,
                $"Шаг «{unstable?.FromBucket} -> {unstable?.ToBucket}» нестабилен: разброс " +
                "сопоставим со средним значением, поэтому прогноз по нему ненадёжен.")
            .Warning("Метод предполагает, что остаток корзины движется только вперёд по шкале. " +
                     "Реструктуризации, частичные платежи и продажи портфеля нарушают эту " +
                     "предпосылку и занижают расчётные потери.")
            .Recommendation("Считайте ставки перетекания отдельно по продуктам и каналам выдачи: " +
                            "усреднение по портфелю скрывает проблемные сегменты.")
            .Recommendation("Сопоставьте ожидаемые потери по этому методу с резервом по МСФО 9. " +
                            "Расхождение больше четверти обычно указывает на ошибку в стадировании.")
            .Build();
    }
}

/// <summary>
/// Анализ скорости перетекания просрочки между корзинами (roll rate).
/// </summary>
/// <remarks>
/// <para>
/// Портфель разбивается на корзины по числу дней просрочки — от текущей
/// задолженности до списания. Ставка перетекания показывает, какая доля
/// остатка корзины за период переходит в следующую:
/// </para>
/// <code>
/// rollRate(i -&gt; i+1) = balance(t+1, i+1) / balance(t, i)
/// </code>
/// <para>
/// Произведение ставок вдоль всей цепочки даёт долю остатка, которая в итоге
/// станет убытком. Умножив её на текущие остатки, получаем ожидаемые потери —
/// оценку, полностью выводимую из наблюдаемых движений портфеля, без моделей
/// вероятности дефолта.
/// </para>
/// <para>
/// Ценность метода в прозрачности: каждая цифра проверяется по оборотной
/// ведомости. Поэтому его используют и как самостоятельный расчёт резерва
/// для коротких розничных портфелей, и как независимую проверку моделей.
/// </para>
/// </remarks>
public static class RollRate
{
    /// <summary>Стандартные корзины розничной просрочки.</summary>
    /// <returns>Названия корзин от текущей задолженности до списания.</returns>
    public static IReadOnlyList<string> DefaultBuckets() =>
        ["Текущий", "1-30", "31-60", "61-90", "91-120", "Списание"];

    /// <summary>Оценивает ставки перетекания по истории остатков.</summary>
    /// <param name="buckets">Корзины в порядке ухудшения, последняя — списание.</param>
    /// <param name="balances">Остатки: строка — период, столбец — корзина.</param>
    /// <returns>Ставки перетекания, доля дохода до списания и ожидаемые потери.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или периодов меньше двух.</exception>
    public static RollRateResult Analyze(IReadOnlyList<string> buckets, Matrix balances)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(balances);

        if (buckets.Count < 2)
            throw new ArgumentException("Нужно как минимум две корзины.", nameof(buckets));
        if (balances.Width != buckets.Count)
            throw new ArgumentException("Число столбцов должно совпадать с числом корзин.", nameof(balances));
        if (balances.Height < 2)
            throw new ArgumentException("Нужно как минимум два периода наблюдения.", nameof(balances));

        int periods = balances.Height;
        int k = buckets.Count;
        var steps = new List<RollRateStep>(k - 1);

        for (int i = 0; i < k - 1; i++)
        {
            var observed = new List<double>(periods - 1);

            for (int t = 0; t + 1 < periods; t++)
            {
                double from = balances[t, i];
                if (from <= 0) continue;

                observed.Add(balances[t + 1, i + 1] / from);
            }

            double average = observed.Count > 0 ? observed.Average() : 0;
            double variance = observed.Count > 1
                ? observed.Sum(v => (v - average) * (v - average)) / (observed.Count - 1)
                : 0;

            steps.Add(new RollRateStep(
                buckets[i], buckets[i + 1], average, Math.Sqrt(variance),
                observed.Count, observed.Count > 0 ? observed[^1] : 0));
        }

        var rollToLoss = new double[k];
        rollToLoss[k - 1] = 1;

        for (int i = k - 2; i >= 0; i--)
            rollToLoss[i] = steps[i].AverageRollRate * rollToLoss[i + 1];

        var latest = new double[k];
        for (int i = 0; i < k; i++) latest[i] = balances[periods - 1, i];

        double impliedLoss = 0;
        for (int i = 0; i < k - 1; i++) impliedLoss += latest[i] * rollToLoss[i];

        double portfolio = latest.Take(k - 1).Sum();

        return new RollRateResult
        {
            Buckets = buckets,
            Steps = steps,
            RollToLoss = rollToLoss,
            LatestBalances = latest,
            ImpliedLoss = impliedLoss,
            ImpliedLossRate = portfolio > 0 ? impliedLoss / portfolio : 0,
            Periods = periods,
        };
    }
}

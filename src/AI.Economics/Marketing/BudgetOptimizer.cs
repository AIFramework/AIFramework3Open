using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Marketing;

/// <summary>Рекомендация по бюджету одного канала.</summary>
public sealed record ChannelBudget
{
    /// <summary>Название канала.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Текущие затраты за период.</summary>
    public double CurrentSpend { get; init; }

    /// <summary>Рекомендованные затраты за период.</summary>
    public double OptimalSpend { get; init; }

    /// <summary>Относительное изменение бюджета.</summary>
    public double Change => CurrentSpend > 0
        ? (OptimalSpend - CurrentSpend) / CurrentSpend
        : OptimalSpend > 0 ? double.PositiveInfinity : 0;

    /// <summary>Ожидаемый вклад в продажи при текущем бюджете.</summary>
    public double CurrentResponse { get; init; }

    /// <summary>Ожидаемый вклад в продажи при рекомендованном бюджете.</summary>
    public double OptimalResponse { get; init; }

    /// <summary>Предельная отдача в точке оптимума — у всех каналов она выравнивается.</summary>
    public double MarginalReturnAtOptimum { get; init; }
}

/// <summary>Результат оптимизации распределения бюджета.</summary>
public sealed record BudgetAllocationResult : IInterpretable
{
    /// <summary>Распределение по каналам, по убыванию бюджета.</summary>
    public IReadOnlyList<ChannelBudget> Channels { get; init; } = [];

    /// <summary>Общий бюджет, который распределялся.</summary>
    public double TotalBudget { get; init; }

    /// <summary>Ожидаемые продажи от рекламы при текущем распределении.</summary>
    public double CurrentResponse { get; init; }

    /// <summary>Ожидаемые продажи от рекламы при оптимальном распределении.</summary>
    public double OptimalResponse { get; init; }

    /// <summary>Прирост продаж без увеличения бюджета.</summary>
    public double ResponseGain => OptimalResponse - CurrentResponse;

    /// <summary>Относительный прирост продаж.</summary>
    public double ResponseGainRate => CurrentResponse > 0 ? ResponseGain / CurrentResponse : 0;

    /// <summary>Общая предельная отдача в оптимуме.</summary>
    public double MarginalReturn { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var increased = Channels.Where(c => c.Change > 0.05).ToList();
        var decreased = Channels.Where(c => c.Change < -0.05).ToList();
        var zeroed = Channels.Where(c => c.OptimalSpend < c.CurrentSpend * 0.05 && c.CurrentSpend > 0).ToList();

        return new InterpretationBuilder("Оптимальное распределение бюджета")
            .Summary($"Перераспределение того же бюджета {Fmt.Money(TotalBudget)} даёт " +
                     $"{Fmt.Money(ResponseGain)} дополнительных продаж ({Fmt.Pct(ResponseGainRate)}). " +
                     $"Увеличить долю стоит у {increased.Count} каналов, сократить — у {decreased.Count}.")
            .Metric("Прирост продаж", Fmt.Money(ResponseGain), null,
                "без увеличения общего бюджета",
                ResponseGain > 0 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Относительный прирост", Fmt.Pct(ResponseGainRate), null,
                "к текущему рекламному вкладу")
            .Metric("Предельная отдача в оптимуме", MarginalReturn, null,
                "в оптимуме она одинакова у всех каналов с ненулевым бюджетом")
            .Metric("Каналов увеличено", increased.Count, null, null, MetricQuality.Unknown, 0)
            .Metric("Каналов сокращено", decreased.Count, null, null, MetricQuality.Unknown, 0)
            .Finding("В оптимуме предельная отдача выровнена по всем каналам. Пока она различается, " +
                     "перенос рубля из канала с меньшей отдачей в канал с большей увеличивает продажи.")
            .FindingIf(zeroed.Count > 0,
                $"Модель предлагает почти обнулить каналы: {string.Join(", ", zeroed.Select(c => c.Name))}. " +
                "Их предельная отдача ниже, чем у остальных, даже при нулевом бюджете.")
            .FindingIf(ResponseGainRate < 0.02,
                "Текущее распределение уже близко к оптимальному: выигрыш от перераспределения " +
                "меньше погрешности самой модели.")
            .WarningIf(zeroed.Count > 0,
                "Полное отключение канала выходит за диапазон данных, на которых оценена модель. " +
                "Сокращайте бюджет постепенно и следите за фактическим эффектом.")
            .Warning("Оптимизация опирается на кривые насыщения из маркетинг-микс модели. " +
                     "Их форма за пределами исторического диапазона затрат не проверена.")
            .Warning("Расчёт не учитывает минимальные бюджеты входа в канал, контрактные " +
                     "обязательства и время на перестройку закупки.")
            .Recommendation("Двигайтесь к рекомендованному распределению за два-три цикла, " +
                            "проверяя фактический отклик на каждом шаге.")
            .Build();
    }
}

/// <summary>
/// Оптимальное распределение маркетингового бюджета по кривым насыщения.
/// </summary>
/// <remarks>
/// <para>
/// Задача: распределить фиксированный бюджет между каналами так, чтобы
/// суммарный отклик был максимальным. Отклик канала в установившемся режиме
/// равен <c>beta * Hill(s / (1 - lambda))</c>.
/// </para>
/// <para>
/// Алгоритм — последовательный перенос малых долей бюджета из канала
/// с наименьшей предельной отдачей в канал с наибольшей, с проверкой
/// суммарного отклика на каждом шаге. Он монотонно улучшает решение и
/// сходится к выравниванию предельных отдач там, где кривая отклика вогнута.
/// </para>
/// <para>
/// Замкнутое решение через множитель Лагранжа здесь непригодно: кривая Хилла
/// при показателе больше единицы S-образна, на начальном участке отдача
/// растёт с бюджетом, и уравнение равенства предельных отдач имеет два корня.
/// </para>
/// <para>
/// Практический смысл: типичная ошибка — распределять бюджет по среднему ROI.
/// Средний показатель включает уже сделанные вложения и потому систематически
/// завышает привлекательность больших каналов, которые давно вышли на плато.
/// </para>
/// </remarks>
public static class BudgetOptimizer
{
    /// <summary>Распределяет бюджет между каналами оценённой модели.</summary>
    /// <param name="model">Результат маркетинг-микс модели.</param>
    /// <param name="totalBudget">
    /// Бюджет на период; при значении <c>NaN</c> берётся текущий суммарный.
    /// </param>
    /// <param name="periods">Число периодов, на которые считается отклик.</param>
    /// <returns>Рекомендованное распределение и прирост отклика.</returns>
    /// <exception cref="ArgumentNullException">Модель не задана.</exception>
    /// <exception cref="ArgumentException">Каналов нет.</exception>
    public static BudgetAllocationResult Allocate(MmmResult model, double totalBudget = double.NaN, int periods = 1)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Channels.Count == 0) throw new ArgumentException("В модели нет каналов.", nameof(model));

        int channels = model.Channels.Count;
        var current = new double[channels];

        for (int c = 0; c < channels; c++)
        {
            ChannelEffect effect = model.Channels[c];
            int observed = Math.Max(effect.Contribution.Count, 1);
            current[c] = effect.TotalSpend / observed;
        }

        double budget = double.IsNaN(totalBudget) ? current.Sum() : totalBudget;
        if (budget <= 0) budget = current.Sum();

        // Отправная точка — текущее распределение, пропорционально приведённое
        // к заданному бюджету. Так гарантируется, что результат не хуже того,
        // что компания делает сейчас
        var start = new double[channels];
        double currentSum = current.Sum();
        for (int c = 0; c < channels; c++)
            start[c] = currentSum > 0 ? current[c] * budget / currentSum : budget / channels;

        double[] optimal = Improve(model.Channels, start, budget);

        var rows = new List<ChannelBudget>(channels);
        double currentResponse = 0, optimalResponse = 0;
        double marginalSum = 0;
        int active = 0;

        for (int c = 0; c < channels; c++)
        {
            ChannelEffect effect = model.Channels[c];
            double currentValue = Response(effect, start[c]) * periods;
            double optimalValue = Response(effect, optimal[c]) * periods;
            double marginal = Marginal(effect, optimal[c]);

            currentResponse += currentValue;
            optimalResponse += optimalValue;

            if (optimal[c] > budget * 1e-4)
            {
                marginalSum += marginal;
                active++;
            }

            rows.Add(new ChannelBudget
            {
                Name = effect.Name,
                CurrentSpend = start[c],
                OptimalSpend = optimal[c],
                CurrentResponse = currentValue,
                OptimalResponse = optimalValue,
                MarginalReturnAtOptimum = marginal,
            });
        }

        return new BudgetAllocationResult
        {
            Channels = [.. rows.OrderByDescending(r => r.OptimalSpend)],
            TotalBudget = budget,
            CurrentResponse = currentResponse,
            OptimalResponse = optimalResponse,
            MarginalReturn = active > 0 ? marginalSum / active : 0,
        };
    }

    /// <summary>
    /// Улучшает распределение переносом бюджета из канала с наименьшей
    /// предельной отдачей в канал с наибольшей.
    /// </summary>
    /// <remarks>
    /// Выравнивание предельных отдач через множитель Лагранжа здесь неприменимо:
    /// кривая Хилла при показателе больше единицы S-образна, и на начальном
    /// участке отдача <b>растёт</b> с бюджетом. Уравнение «предельная отдача
    /// равна заданному уровню» имеет на такой кривой два корня, и водоналивной
    /// алгоритм находит не тот. Перенос малыми долями с проверкой суммарного
    /// отклика монотонно улучшает решение независимо от формы кривой и
    /// сходится к выравниванию там, где кривая вогнута.
    /// </remarks>
    private static double[] Improve(IReadOnlyList<ChannelEffect> channels, double[] start, double budget)
    {
        int n = channels.Count;
        var spend = (double[])start.Clone();
        double step = budget / 50.0;

        for (int iteration = 0; iteration < 4000 && step > budget * 1e-6; iteration++)
        {
            int best = -1, worst = -1;
            double bestMarginal = double.NegativeInfinity, worstMarginal = double.PositiveInfinity;

            for (int c = 0; c < n; c++)
            {
                double marginal = Marginal(channels[c], spend[c]);
                if (marginal > bestMarginal) { bestMarginal = marginal; best = c; }
                if (spend[c] > 1e-9 && marginal < worstMarginal) { worstMarginal = marginal; worst = c; }
            }

            if (best < 0 || worst < 0 || best == worst) break;

            double move = Math.Min(step, spend[worst]);
            if (move <= 0) break;

            double before = Total(channels, spend);
            spend[worst] -= move;
            spend[best] += move;
            double after = Total(channels, spend);

            if (after <= before)
            {
                spend[worst] += move;
                spend[best] -= move;
                step *= 0.5;
            }
        }

        return spend;
    }

    private static double Total(IReadOnlyList<ChannelEffect> channels, double[] spend)
    {
        double sum = 0;
        for (int c = 0; c < channels.Count; c++) sum += Response(channels[c], spend[c]);
        return sum;
    }

    /// <summary>Отклик канала в установившемся режиме при постоянных затратах.</summary>
    /// <param name="effect">Оценённый канал.</param>
    /// <param name="spendPerPeriod">Затраты за период.</param>
    /// <returns>Вклад в продажи за период.</returns>
    public static double Response(ChannelEffect effect, double spendPerPeriod)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (spendPerPeriod <= 0) return 0;

        double steadyState = spendPerPeriod / Math.Max(1.0 - effect.Decay, 1e-6);
        return effect.Coefficient * MarketingMixModel.Hill(steadyState, effect.SaturationPoint, effect.SaturationShape);
    }

    /// <summary>Производная отклика по затратам, посчитанная численно.</summary>
    private static double Marginal(ChannelEffect effect, double spendPerPeriod)
    {
        double delta = Math.Max(spendPerPeriod * 0.001, 1e-6);
        return (Response(effect, spendPerPeriod + delta) - Response(effect, spendPerPeriod)) / delta;
    }

}

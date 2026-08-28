using System;
using System.Collections.Generic;
using System.Linq;

using AI.Economics.Insights;

namespace AI.Economics.Saas;

/// <summary>Разложение изменения регулярной выручки за один период.</summary>
public sealed partial record MrrBridgeResult
{
    /// <summary>Выручка на начало периода.</summary>
    public double StartingMrr { get; init; }

    /// <summary>Выручка от новых клиентов.</summary>
    public double NewMrr { get; init; }

    /// <summary>Прирост выручки существующих клиентов.</summary>
    public double ExpansionMrr { get; init; }

    /// <summary>Выручка вернувшихся клиентов.</summary>
    public double ReactivationMrr { get; init; }

    /// <summary>Снижение выручки оставшихся клиентов, положительное число.</summary>
    public double ContractionMrr { get; init; }

    /// <summary>Потерянная выручка полностью ушедших клиентов, положительное число.</summary>
    public double ChurnedMrr { get; init; }

    /// <summary>Выручка на конец периода.</summary>
    public double EndingMrr { get; init; }

    /// <summary>Чистый прирост выручки за период.</summary>
    public double NetNewMrr { get; init; }

    /// <summary>
    /// Gross Revenue Retention: удержание выручки без учёта расширений.
    /// Не может превышать единицу — потолок честности когорты.
    /// </summary>
    public double GrossRevenueRetention { get; init; }

    /// <summary>
    /// Net Dollar Retention: удержание с учётом расширений, но без новых клиентов.
    /// Значение выше единицы означает рост даже при полной остановке продаж.
    /// </summary>
    public double NetDollarRetention { get; init; }

    /// <summary>Отток клиентов по числу логотипов.</summary>
    public double LogoChurnRate { get; init; }

    /// <summary>
    /// Quick Ratio: отношение прироста выручки к её потерям. Ниже единицы —
    /// компания теряет быстрее, чем набирает.
    /// </summary>
    public double QuickRatio { get; init; }

    /// <summary>Клиентов на начало периода.</summary>
    public int StartingCustomers { get; init; }

    /// <summary>Новых клиентов за период.</summary>
    public int NewCustomers { get; init; }

    /// <summary>Ушедших клиентов за период.</summary>
    public int ChurnedCustomers { get; init; }

    /// <summary>Клиентов на конец периода.</summary>
    public int EndingCustomers { get; init; }
}

/// <summary>
/// MRR-мостик: разложение изменения регулярной выручки на пять компонент.
/// </summary>
/// <remarks>
/// <para>
/// Одно число «выручка выросла на 8 %» не отвечает на главный вопрос: за счёт
/// чего. Рост на 8 % при оттоке 15 % и продажах 23 % — совсем не то же самое,
/// что рост на 8 % при оттоке 2 %. Первый вариант требует всё больше денег на
/// привлечение просто чтобы стоять на месте.
/// </para>
/// <para>
/// Компоненты считаются по двум срезам «клиент — выручка», а не по агрегатам:
/// иначе расширение и отток взаимозачитываются и исчезают из отчёта.
/// </para>
/// </remarks>
public static class MrrBridge
{
    /// <summary>Строит мостик между двумя срезами выручки по клиентам.</summary>
    /// <param name="start">Выручка по клиентам на начало периода.</param>
    /// <param name="end">Выручка по клиентам на конец периода.</param>
    /// <param name="previouslyChurned">
    /// Клиенты, ушедшие ранее: их возврат учитывается как реактивация,
    /// а не как новая продажа.
    /// </param>
    /// <returns>Разложение изменения выручки.</returns>
    /// <exception cref="ArgumentNullException">Срезы не заданы.</exception>
    public static MrrBridgeResult Build(
        IReadOnlyDictionary<string, double> start,
        IReadOnlyDictionary<string, double> end,
        IReadOnlyCollection<string>? previouslyChurned = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        var returning = previouslyChurned is null
            ? new HashSet<string>()
            : new HashSet<string>(previouslyChurned);

        double startingMrr = start.Values.Where(v => v > 0).Sum();
        double expansion = 0, contraction = 0, churned = 0, newMrr = 0, reactivation = 0;
        int churnedCustomers = 0, newCustomers = 0;

        foreach ((string id, double before) in start)
        {
            if (before <= 0) continue;

            double after = end.TryGetValue(id, out double v) ? v : 0;

            if (after <= 0)
            {
                churned += before;
                churnedCustomers++;
            }
            else if (after > before) expansion += after - before;
            else if (after < before) contraction += before - after;
        }

        foreach ((string id, double after) in end)
        {
            if (after <= 0) continue;
            if (start.TryGetValue(id, out double before) && before > 0) continue;

            if (returning.Contains(id)) reactivation += after;
            else newMrr += after;

            newCustomers++;
        }

        double endingMrr = end.Values.Where(v => v > 0).Sum();
        int startingCustomers = start.Count(kv => kv.Value > 0);
        int endingCustomers = end.Count(kv => kv.Value > 0);

        return new MrrBridgeResult
        {
            StartingMrr = startingMrr,
            NewMrr = newMrr,
            ExpansionMrr = expansion,
            ReactivationMrr = reactivation,
            ContractionMrr = contraction,
            ChurnedMrr = churned,
            EndingMrr = endingMrr,
            NetNewMrr = endingMrr - startingMrr,
            GrossRevenueRetention = startingMrr > 0
                ? (startingMrr - contraction - churned) / startingMrr
                : double.NaN,
            NetDollarRetention = startingMrr > 0
                ? (startingMrr + expansion - contraction - churned) / startingMrr
                : double.NaN,
            LogoChurnRate = startingCustomers > 0 ? (double)churnedCustomers / startingCustomers : double.NaN,
            QuickRatio = churned + contraction > 0
                ? (newMrr + expansion + reactivation) / (churned + contraction)
                : double.PositiveInfinity,
            StartingCustomers = startingCustomers,
            NewCustomers = newCustomers,
            ChurnedCustomers = churnedCustomers,
            EndingCustomers = endingCustomers,
        };
    }

    /// <summary>
    /// Строит мостики для последовательности срезов: результат <c>i</c>
    /// описывает переход от среза <c>i</c> к срезу <c>i + 1</c>.
    /// </summary>
    /// <param name="snapshots">Срезы «клиент — выручка» по периодам.</param>
    /// <returns>Список мостиков длиной на единицу меньше числа срезов.</returns>
    /// <exception cref="ArgumentNullException">Срезы не заданы.</exception>
    public static IReadOnlyList<MrrBridgeResult> BuildSeries(
        IReadOnlyList<IReadOnlyDictionary<string, double>> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count < 2) return [];

        var churnedSoFar = new HashSet<string>();
        var results = new List<MrrBridgeResult>(snapshots.Count - 1);

        for (int i = 1; i < snapshots.Count; i++)
        {
            results.Add(Build(snapshots[i - 1], snapshots[i], churnedSoFar));

            foreach ((string id, double before) in snapshots[i - 1])
            {
                if (before <= 0) continue;
                bool active = snapshots[i].TryGetValue(id, out double after) && after > 0;
                if (!active) churnedSoFar.Add(id);
                else churnedSoFar.Remove(id);
            }
        }

        return results;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;

using AI.Economics.Insights;

namespace AI.Economics.Survival;

/// <summary>Кумулятивная функция инцидентности для одной причины ухода.</summary>
public sealed record CumulativeIncidence
{
    /// <summary>Номер причины.</summary>
    public int Cause { get; init; }

    /// <summary>Название причины.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Моменты событий.</summary>
    public Vector Times { get; init; } = new Vector(0);

    /// <summary>Оценка Аалена — Йохансена: доля ушедших по этой причине к моменту t.</summary>
    public Vector Incidence { get; init; } = new Vector(0);

    /// <summary>
    /// Наивная оценка <c>1 - KM</c>, где события других причин считаются
    /// цензурированием. Приводится для сравнения: она систематически завышает.
    /// </summary>
    public Vector NaiveIncidence { get; init; } = new Vector(0);

    /// <summary>Итоговая доля ушедших по этой причине на горизонте наблюдения.</summary>
    public double FinalIncidence { get; init; }

    /// <summary>Итоговая наивная оценка — величина завышения видна из разницы.</summary>
    public double FinalNaiveIncidence { get; init; }
}

/// <summary>
/// Анализ конкурирующих рисков: клиент уходит по одной из нескольких
/// взаимоисключающих причин.
/// </summary>
/// <remarks>
/// <para>
/// Типичная задача: клиент может отвалиться из-за цены, из-за качества
/// продукта или потому, что его компания закрылась. Причины конкурируют —
/// наступившая первой лишает возможности наступить остальные.
/// </para>
/// <para>
/// Почему нельзя обойтись обычным Капланом — Мейером на каждую причину:
/// считая уход по другим причинам цензурированием, мы неявно предполагаем,
/// что такой клиент «мог бы» уйти по нашей причине позже. Он не мог — его
/// уже нет. Оценка <c>1 - KM</c> из-за этого завышает долю причины, и сумма
/// оценок по всем причинам превышает единицу. Оценка Аалена — Йохансена
/// свободна от этого дефекта: сумма её значений по причинам равна общей доле
/// ушедших.
/// </para>
/// </remarks>
public static class CompetingRisks
{
    /// <summary>
    /// Считает кумулятивные функции инцидентности по всем причинам.
    /// </summary>
    /// <param name="data">
    /// Наблюдения; поле <see cref="SurvivalRecord.Cause"/> хранит номер причины,
    /// 0 означает цензурирование.
    /// </param>
    /// <param name="causeNames">Названия причин по номерам.</param>
    /// <returns>Список функций инцидентности по возрастанию номера причины.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Данные пусты.</exception>
    public static IReadOnlyList<CumulativeIncidence> Analyze(
        IReadOnlyList<SurvivalRecord> data,
        IReadOnlyDictionary<int, string>? causeNames = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Count == 0) throw new ArgumentException("Пустая выборка.", nameof(data));

        List<SurvivalRecord> ordered = [.. data.OrderBy(r => r.Time)];
        int[] causes = [.. ordered.Where(r => r.Cause > 0).Select(r => r.Cause).Distinct().OrderBy(c => c)];
        double[] eventTimes = [.. ordered.Where(r => r.Cause > 0).Select(r => r.Time).Distinct().OrderBy(t => t)];

        var result = new List<CumulativeIncidence>(causes.Length);

        foreach (int cause in causes)
        {
            var times = new List<double>(eventTimes.Length);
            var cif = new List<double>(eventTimes.Length);
            var naive = new List<double>(eventTimes.Length);

            double overallSurvival = 1.0;
            double naiveSurvival = 1.0;
            double incidence = 0;

            foreach (double t in eventTimes)
            {
                int n = ordered.Count(r => r.Time >= t);
                if (n == 0) continue;

                int dCause = ordered.Count(r => r.Cause == cause && Math.Abs(r.Time - t) < 1e-12);
                int dAll = ordered.Count(r => r.Cause > 0 && Math.Abs(r.Time - t) < 1e-12);

                // Аален — Йохансен: прирост взвешивается общей выживаемостью
                // ДО момента t, поэтому конкурирующие уходы учитываются честно
                incidence += overallSurvival * dCause / n;
                overallSurvival *= 1.0 - ((double)dAll / n);

                if (dCause > 0) naiveSurvival *= 1.0 - ((double)dCause / n);

                times.Add(t);
                cif.Add(incidence);
                naive.Add(1.0 - naiveSurvival);
            }

            result.Add(new CumulativeIncidence
            {
                Cause = cause,
                Name = causeNames is not null && causeNames.TryGetValue(cause, out string? name)
                    ? name
                    : $"Причина {cause}",
                Times = new Vector([.. times]),
                Incidence = new Vector([.. cif]),
                NaiveIncidence = new Vector([.. naive]),
                FinalIncidence = cif.Count > 0 ? cif[^1] : 0,
                FinalNaiveIncidence = naive.Count > 0 ? naive[^1] : 0,
            });
        }

        return result;
    }
}

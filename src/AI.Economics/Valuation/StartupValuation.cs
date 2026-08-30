using System;
using System.Collections.Generic;
using System.Linq;

using AI.Insights;

namespace AI.Economics.Valuation;

/// <summary>Вход метода венчурного капитала.</summary>
public sealed record VcMethodInput
{
    /// <summary>Сумма инвестиции.</summary>
    public double Investment { get; init; }

    /// <summary>Выручка на горизонте выхода.</summary>
    public double ExitRevenue { get; init; }

    /// <summary>Мультипликатор к выручке при выходе.</summary>
    public double ExitMultiple { get; init; }

    /// <summary>
    /// Готовая стоимость выхода. Если задана, выручка и мультипликатор
    /// не используются.
    /// </summary>
    public double ExitValueOverride { get; init; } = double.NaN;

    /// <summary>Число лет до выхода.</summary>
    public double YearsToExit { get; init; } = 5;

    /// <summary>Требуемая инвестором годовая доходность, доля (0,5 — 50 % годовых).</summary>
    public double TargetIrr { get; init; } = 0.5;

    /// <summary>
    /// Ожидаемое разводнение будущими раундами, доля. 0,4 означает, что доля
    /// инвестора к выходу уменьшится на 40 % от текущей.
    /// </summary>
    public double ExpectedFutureDilution { get; init; }
}

/// <summary>Результат метода венчурного капитала.</summary>
public sealed partial record VcMethodResult
{
    /// <summary>Стоимость компании при выходе.</summary>
    public double ExitValue { get; init; }

    /// <summary>Доля инвестора на момент выхода, необходимая для целевой доходности.</summary>
    public double OwnershipAtExit { get; init; }

    /// <summary>Доля инвестора сегодня с поправкой на будущее разводнение.</summary>
    public double OwnershipNow { get; init; }

    /// <summary>Оценка после денег.</summary>
    public double PostMoneyValuation { get; init; }

    /// <summary>Оценка до денег.</summary>
    public double PreMoneyValuation { get; init; }

    /// <summary>Во сколько раз вырастет вложение при выходе.</summary>
    public double MoneyMultiple { get; init; }
}

/// <summary>Фактор оценки по методу Беркуса.</summary>
/// <param name="Name">Название фактора.</param>
/// <param name="Score">Оценка от 0 до 1.</param>
/// <param name="MaxValue">Максимальный вклад фактора в стоимость.</param>
public sealed record BerkusFactor(string Name, double Score, double MaxValue);

/// <summary>Фактор оценки по методу Scorecard.</summary>
/// <param name="Name">Название фактора.</param>
/// <param name="Weight">Вес фактора, доли суммируются в единицу.</param>
/// <param name="Ratio">Отношение к среднему по рынку: 1,0 — как у всех.</param>
public sealed record ScorecardFactor(string Name, double Weight, double Ratio);

/// <summary>Сценарий метода First Chicago.</summary>
/// <param name="Name">Название сценария.</param>
/// <param name="Probability">Вероятность сценария.</param>
/// <param name="Valuation">Оценка компании в этом сценарии.</param>
public sealed record ValuationScenario(string Name, double Probability, double Valuation);

/// <summary>Итог сценарной оценки.</summary>
public sealed partial record ScenarioValuationResult
{
    /// <summary>Взвешенная по вероятностям оценка.</summary>
    public double ExpectedValuation { get; init; }

    /// <summary>Вклад каждого сценария в итоговую оценку.</summary>
    public IReadOnlyList<(string Name, double Probability, double Valuation, double Contribution)> Breakdown { get; init; } = [];

    /// <summary>Стандартное отклонение оценки по сценариям.</summary>
    public double StandardDeviation { get; init; }

    /// <summary>
    /// Доля ожидаемой стоимости, создаваемая лучшим сценарием. Высокое
    /// значение означает, что оценка держится на одном исходе.
    /// </summary>
    public double BestCaseShare { get; init; }
}

/// <summary>
/// Классические методы оценки стартапа на ранних стадиях.
/// </summary>
/// <remarks>
/// Ни один из этих методов не даёт «правильной» цифры — у компании без
/// выручки её не существует. Они задают рамку переговоров, и практическая
/// ценность в том, чтобы считать сразу несколькими: расхождение результатов
/// показывает, какие допущения на самом деле определяют цену.
/// </remarks>
public static class StartupValuation
{
    /// <summary>
    /// Метод венчурного капитала: от стоимости выхода назад к сегодняшней
    /// оценке через требуемую доходность.
    /// </summary>
    /// <param name="input">Параметры расчёта.</param>
    /// <returns>Оценка до и после денег, требуемая доля инвестора.</returns>
    /// <exception cref="ArgumentNullException">Вход не задан.</exception>
    /// <exception cref="ArgumentException">Стоимость выхода неположительна.</exception>
    public static VcMethodResult VcMethod(VcMethodInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double exitValue = double.IsNaN(input.ExitValueOverride)
            ? input.ExitRevenue * input.ExitMultiple
            : input.ExitValueOverride;

        if (exitValue <= 0)
            throw new ArgumentException("Стоимость выхода должна быть положительной.", nameof(input));

        double growth = Math.Pow(1.0 + input.TargetIrr, input.YearsToExit);
        double requiredAtExit = input.Investment * growth;
        double ownershipAtExit = requiredAtExit / exitValue;

        double retention = 1.0 - input.ExpectedFutureDilution;
        double ownershipNow = retention > 0 ? ownershipAtExit / retention : ownershipAtExit;

        double postMoney = ownershipNow > 0 ? input.Investment / ownershipNow : 0;

        return new VcMethodResult
        {
            ExitValue = exitValue,
            OwnershipAtExit = ownershipAtExit,
            OwnershipNow = ownershipNow,
            PostMoneyValuation = postMoney,
            PreMoneyValuation = postMoney - input.Investment,
            MoneyMultiple = growth,
        };
    }

    /// <summary>Факторы Беркуса по умолчанию с равным весом.</summary>
    /// <param name="maxPerFactor">Максимальный вклад одного фактора.</param>
    /// <param name="scores">Оценки факторов от 0 до 1 в каноническом порядке.</param>
    /// <returns>Набор факторов.</returns>
    public static IReadOnlyList<BerkusFactor> BerkusDefaults(double maxPerFactor, params double[] scores)
    {
        string[] names =
        [
            "Здравая идея (базовая стоимость)",
            "Прототип (технологический риск)",
            "Команда (риск исполнения)",
            "Стратегические связи (рыночный риск)",
            "Продажи (производственный риск)",
        ];

        return [.. names.Select((n, i) => new BerkusFactor(n, i < scores.Length ? scores[i] : 0, maxPerFactor))];
    }

    /// <summary>
    /// Метод Беркуса: пять качественных факторов, каждый с денежным потолком.
    /// </summary>
    /// <param name="factors">Факторы с оценками.</param>
    /// <returns>Суммарная оценка до денег.</returns>
    /// <exception cref="ArgumentNullException">Факторы не заданы.</exception>
    public static double Berkus(IReadOnlyList<BerkusFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        return factors.Sum(f => Math.Clamp(f.Score, 0, 1) * f.MaxValue);
    }

    /// <summary>Факторы Scorecard по умолчанию (веса Билла Пейна).</summary>
    /// <param name="ratios">Отношения к среднему по рынку в каноническом порядке.</param>
    /// <returns>Набор факторов.</returns>
    public static IReadOnlyList<ScorecardFactor> ScorecardDefaults(params double[] ratios)
    {
        (string Name, double Weight)[] template =
        [
            ("Команда", 0.30),
            ("Размер рынка", 0.25),
            ("Продукт и технология", 0.15),
            ("Конкурентное окружение", 0.10),
            ("Продажи и маркетинг", 0.10),
            ("Потребность в доп. инвестициях", 0.05),
            ("Прочее", 0.05),
        ];

        return [.. template.Select((t, i) => new ScorecardFactor(t.Name, t.Weight, i < ratios.Length ? ratios[i] : 1.0))];
    }

    /// <summary>
    /// Метод Scorecard: средняя оценка по рынку, скорректированная взвешенной
    /// суммой отклонений компании от типичной.
    /// </summary>
    /// <param name="averageMarketPreMoney">Средняя оценка до денег по сопоставимым сделкам.</param>
    /// <param name="factors">Факторы сравнения.</param>
    /// <returns>Оценка до денег.</returns>
    /// <exception cref="ArgumentNullException">Факторы не заданы.</exception>
    public static double Scorecard(double averageMarketPreMoney, IReadOnlyList<ScorecardFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        return averageMarketPreMoney * factors.Sum(f => f.Weight * f.Ratio);
    }

    /// <summary>
    /// Метод First Chicago: оценка как математическое ожидание по сценариям.
    /// </summary>
    /// <param name="scenarios">Сценарии с вероятностями и оценками.</param>
    /// <returns>Ожидаемая оценка и вклад сценариев.</returns>
    /// <exception cref="ArgumentNullException">Сценарии не заданы.</exception>
    /// <exception cref="ArgumentException">Список сценариев пуст.</exception>
    public static ScenarioValuationResult FirstChicago(IReadOnlyList<ValuationScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        if (scenarios.Count == 0)
            throw new ArgumentException("Нужен хотя бы один сценарий.", nameof(scenarios));

        double weight = scenarios.Sum(s => s.Probability);
        if (weight <= 0) weight = 1;

        var breakdown = scenarios
            .Select(s => (s.Name, Probability: s.Probability / weight, s.Valuation,
                Contribution: s.Probability / weight * s.Valuation))
            .ToList();

        double expected = breakdown.Sum(b => b.Contribution);
        double variance = breakdown.Sum(b => b.Probability * Math.Pow(b.Valuation - expected, 2));
        double best = breakdown.Count > 0 ? breakdown.Max(b => b.Contribution) : 0;

        return new ScenarioValuationResult
        {
            ExpectedValuation = expected,
            Breakdown = [.. breakdown],
            StandardDeviation = Math.Sqrt(variance),
            BestCaseShare = expected > 0 ? best / expected : 0,
        };
    }
}

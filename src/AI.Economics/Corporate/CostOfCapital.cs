using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Corporate;

/// <summary>Входные данные для расчёта стоимости капитала.</summary>
public sealed record CostOfCapitalInput
{
    /// <summary>Название компании или проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Безрисковая ставка.</summary>
    public double RiskFreeRate { get; init; } = 0.08;

    /// <summary>Премия за рыночный риск.</summary>
    public double EquityRiskPremium { get; init; } = 0.06;

    /// <summary>Бета без учёта долга, взятая по отрасли.</summary>
    public double UnleveredBeta { get; init; } = 1.0;

    /// <summary>Страновая премия за риск.</summary>
    public double CountryRiskPremium { get; init; }

    /// <summary>Премия за размер компании.</summary>
    public double SizePremium { get; init; }

    /// <summary>Специфическая премия: страновая специфика, ликвидность, зависимость от собственника.</summary>
    public double SpecificPremium { get; init; }

    /// <summary>Рыночная стоимость собственного капитала.</summary>
    public double EquityValue { get; init; } = 100;

    /// <summary>Рыночная стоимость долга.</summary>
    public double DebtValue { get; init; }

    /// <summary>Стоимость долга до налога.</summary>
    public double CostOfDebt { get; init; } = 0.12;

    /// <summary>Эффективная ставка налога на прибыль.</summary>
    public double TaxRate { get; init; } = 0.2;
}

/// <summary>Результат расчёта стоимости капитала.</summary>
public sealed record CostOfCapitalResult : IInterpretable
{
    /// <summary>Название компании или проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Бета с учётом финансового рычага.</summary>
    public double LeveredBeta { get; init; }

    /// <summary>Стоимость собственного капитала.</summary>
    public double CostOfEquity { get; init; }

    /// <summary>Стоимость долга после налога.</summary>
    public double AfterTaxCostOfDebt { get; init; }

    /// <summary>Средневзвешенная стоимость капитала.</summary>
    public double Wacc { get; init; }

    /// <summary>Доля собственного капитала в структуре финансирования.</summary>
    public double EquityWeight { get; init; }

    /// <summary>Доля долга в структуре финансирования.</summary>
    public double DebtWeight => 1 - EquityWeight;

    /// <summary>Разложение стоимости собственного капитала по слагаемым.</summary>
    public IReadOnlyList<(string Component, double Value)> EquityBuildUp { get; init; } = [];

    /// <summary>Экономия на налоге за счёт долга в процентных пунктах ставки.</summary>
    public double TaxShieldBenefit { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (string Component, double Value) largest = EquityBuildUp
            .Where(c => c.Component != "Безрисковая ставка")
            .OrderByDescending(c => c.Value)
            .FirstOrDefault();

        var builder = new InterpretationBuilder($"Стоимость капитала: {Name}")
            .Summary($"Средневзвешенная стоимость капитала {Fmt.Pct(Wacc, 2)} при структуре " +
                     $"{Fmt.Pct(EquityWeight, 0)} собственного и {Fmt.Pct(DebtWeight, 0)} заёмного. " +
                     $"Стоимость собственного капитала {Fmt.Pct(CostOfEquity, 2)} при бете с рычагом " +
                     $"{Fmt.Num(LeveredBeta, 2)}, стоимость долга после налога " +
                     $"{Fmt.Pct(AfterTaxCostOfDebt, 2)}.")
            .Metric("WACC", Wacc, null, "ставка дисконтирования свободного денежного потока фирмы",
                MetricQuality.Neutral, 4)
            .Metric("Стоимость капитала", CostOfEquity, null,
                $"бета с рычагом {Fmt.Num(LeveredBeta, 2)}", MetricQuality.Neutral, 4)
            .Metric("Стоимость долга после налога", AfterTaxCostOfDebt, null,
                $"налоговый щит снижает ставку на {Fmt.Pct(TaxShieldBenefit, 2)}",
                MetricQuality.Neutral, 4)
            .Metric("Доля собственного капитала", EquityWeight, null,
                "по рыночным, а не балансовым величинам", MetricQuality.Neutral, 3);

        foreach ((string component, double value) in EquityBuildUp)
            builder.Metric(component, value, null, "слагаемое стоимости собственного капитала",
                MetricQuality.Unknown, 4);

        return builder
            .Finding("Ставка дисконтирования должна соответствовать потоку. Свободный поток " +
                     "фирмы дисконтируется по средневзвешенной стоимости капитала, поток " +
                     "на собственный капитал — по его стоимости. Смешение этих двух пар — " +
                     "самая частая ошибка в оценке.")
            .FindingIf(largest.Component is not null,
                $"Наибольшее слагаемое сверх безрисковой ставки — «{largest.Component}» " +
                $"({Fmt.Pct(largest.Value, 2)}).")
            .FindingIf(DebtWeight > 0.01,
                $"Долг снижает средневзвешенную ставку на {Fmt.Pct(TaxShieldBenefit * DebtWeight, 2)} " +
                "за счёт налогового щита. Этот эффект конечен: с ростом долга растут " +
                "и стоимость капитала через бету, и стоимость самого долга.")
            .WarningIf(EquityWeight > 0.99,
                "Структура капитала принята без долга. Если компания планирует его привлекать, " +
                "ставку нужно считать по целевой, а не по текущей структуре.")
            .WarningIf(LeveredBeta > 2,
                $"Бета с рычагом {Fmt.Num(LeveredBeta, 2)} очень велика. Проверьте отраслевую " +
                "бету и структуру капитала: при таком уровне оценка крайне чувствительна " +
                "к ставке дисконтирования.")
            .Warning("Премии за страну, размер и специфику — экспертные величины. Они часто " +
                     "составляют половину ставки и полностью определяют результат оценки, " +
                     "поэтому их нужно раскрывать отдельной строкой, а не прятать в итоговую цифру.")
            .Recommendation("Считайте веса по рыночной стоимости капитала и долга. Балансовые " +
                            "веса систематически завышают долю долга и занижают ставку.")
            .Recommendation("Проверяйте устойчивость оценки к ставке в пределах плюс-минус " +
                            "два процентных пункта: если вывод меняется, решение принимается " +
                            "не по модели, а по предпосылке о ставке.")
            .Build();
    }
}

/// <summary>
/// Стоимость капитала: модель оценки капитальных активов с премиями и
/// средневзвешенная стоимость капитала.
/// </summary>
/// <remarks>
/// <para>
/// Стоимость собственного капитала строится наращиванием премий поверх
/// безрисковой ставки:
/// </para>
/// <code>
/// beta_D = (Kd - Rf) / ERP
/// beta_L = beta_U + (beta_U - beta_D) * (1 - tax) * D / E
/// Ke = Rf + beta_L * ERP + CRP + SP + Specific
/// WACC = Ke * E / (D + E) + Kd * (1 - tax) * D / (D + E)
/// </code>
/// <para>
/// Формула Хамады связывает отраслевую бету без долга с бетой конкретной
/// компании: долг усиливает чувствительность прибыли акционера к рынку.
/// Налоговый щит входит дважды — он снижает и бету, и стоимость долга.
/// </para>
/// <para>
/// Практическая честность расчёта определяется раскрытием премий. Страновая
/// премия, премия за размер и специфическая премия часто дают половину ставки,
/// и решение по проекту фактически принимается выбором этих величин, а не
/// расчётом денежного потока.
/// </para>
/// </remarks>
public static class CostOfCapital
{
    /// <summary>Рассчитывает стоимость капитала и средневзвешенную ставку.</summary>
    /// <param name="input">Ставки, премии и структура капитала.</param>
    /// <returns>Стоимость собственного капитала, долга и их взвешенная величина.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    /// <exception cref="ArgumentException">Стоимость капитала неположительна.</exception>
    public static CostOfCapitalResult Compute(CostOfCapitalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.EquityValue <= 0)
            throw new ArgumentException("Стоимость собственного капитала должна быть положительной.", nameof(input));
        if (input.DebtValue < 0)
            throw new ArgumentException("Стоимость долга не может быть отрицательной.", nameof(input));

        double total = input.EquityValue + input.DebtValue;
        double equityWeight = input.EquityValue / total;
        double tax = Math.Clamp(input.TaxRate, 0, 0.6);

        // Бета долга выводится из его стоимости: без этой поправки формула
        // Хамады неявно считает долг безрисковым и завышает рост беты с рычагом
        double debtBeta = input.EquityRiskPremium > 1e-9
            ? Math.Max(0, (input.CostOfDebt - input.RiskFreeRate) / input.EquityRiskPremium)
            : 0;

        double leveredBeta = input.UnleveredBeta
            + ((input.UnleveredBeta - debtBeta) * (1 - tax) * input.DebtValue / input.EquityValue);

        double marketPremium = leveredBeta * input.EquityRiskPremium;

        double costOfEquity = input.RiskFreeRate + marketPremium
            + input.CountryRiskPremium + input.SizePremium + input.SpecificPremium;

        double afterTaxDebt = input.CostOfDebt * (1 - tax);

        var buildUp = new List<(string, double)>
        {
            ("Безрисковая ставка", input.RiskFreeRate),
            ("Рыночная премия с учётом беты", marketPremium),
        };

        if (input.CountryRiskPremium > 0) buildUp.Add(("Страновая премия", input.CountryRiskPremium));
        if (input.SizePremium > 0) buildUp.Add(("Премия за размер", input.SizePremium));
        if (input.SpecificPremium > 0) buildUp.Add(("Специфическая премия", input.SpecificPremium));

        return new CostOfCapitalResult
        {
            Name = input.Name,
            LeveredBeta = leveredBeta,
            CostOfEquity = costOfEquity,
            AfterTaxCostOfDebt = afterTaxDebt,
            Wacc = (costOfEquity * equityWeight) + (afterTaxDebt * (1 - equityWeight)),
            EquityWeight = equityWeight,
            EquityBuildUp = buildUp,
            TaxShieldBenefit = input.CostOfDebt * tax,
        };
    }

    /// <summary>Снимает финансовый рычаг с наблюдаемой беты компании.</summary>
    /// <param name="leveredBeta">Наблюдаемая бета.</param>
    /// <param name="debtToEquity">Отношение долга к собственному капиталу.</param>
    /// <param name="taxRate">Ставка налога на прибыль.</param>
    /// <param name="debtBeta">Бета долга; ноль означает безрисковый долг.</param>
    /// <returns>Бета без учёта долга.</returns>
    public static double Unlever(
        double leveredBeta, double debtToEquity, double taxRate, double debtBeta = 0)
    {
        double factor = (1 - Math.Clamp(taxRate, 0, 0.6)) * Math.Max(debtToEquity, 0);
        return (leveredBeta + (debtBeta * factor)) / (1 + factor);
    }

    /// <summary>
    /// Строит кривую средневзвешенной ставки по доле долга.
    /// </summary>
    /// <remarks>
    /// Рост стоимости долга сам по себе минимума не создаёт: он полностью
    /// компенсируется падением беты собственного капитала, и получается
    /// линейно убывающая ставка Модильяни — Миллера. Минимум появляется только
    /// при явном учёте безвозвратных издержек финансовых затруднений — потери
    /// клиентов, поставщиков и управленческого внимания при высокой нагрузке.
    /// Они задаются отдельной премией и растут быстрее линейной.
    /// </remarks>
    /// <param name="input">Базовые параметры.</param>
    /// <param name="steps">Число точек кривой.</param>
    /// <param name="creditSpreadSlope">Рост стоимости долга на квадрат доли долга.</param>
    /// <param name="distressSlope">Масштаб издержек финансовых затруднений.</param>
    /// <returns>Пары «доля долга — средневзвешенная ставка».</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    public static IReadOnlyList<(double DebtShare, double Wacc)> WaccCurve(
        CostOfCapitalInput input, int steps = 19,
        double creditSpreadSlope = 0.12, double distressSlope = 0.2)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 2);

        double capital = input.EquityValue + input.DebtValue;
        var curve = new List<(double, double)>(steps);

        for (int i = 0; i < steps; i++)
        {
            double share = i * 0.9 / (steps - 1);
            double debt = capital * share;
            double equity = capital - debt;

            if (equity <= 1e-9) continue;

            CostOfCapitalResult point = Compute(input with
            {
                EquityValue = equity,
                DebtValue = debt,
                CostOfDebt = input.CostOfDebt + (creditSpreadSlope * share * share),
            });

            // Издержки затруднений уменьшают стоимость всей фирмы, а не только
            // доли акционеров, поэтому добавляются к итоговой ставке целиком
            curve.Add((share, point.Wacc + (distressSlope * share * share * share)));
        }

        return curve;
    }
}

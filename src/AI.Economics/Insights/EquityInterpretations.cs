using System;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Equity;

/// <summary>Разбор итогов ценового раунда.</summary>
public sealed partial record RoundResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        DilutionRow? hardest = Dilution
            .Where(d => d.Before > 0)
            .OrderByDescending(d => d.RelativeDilution)
            .FirstOrDefault();

        double declared = PostMoneyValuation - (InvestorOwnership * PostMoneyValuation);
        double hiddenCost = declared > 0 ? 1 - (EffectivePreMoneyForFounders / declared) : 0;
        bool hasNotes = Conversions.Count > 0;

        var builder = new InterpretationBuilder("Ценовой раунд и разводнение")
            .Summary($"Инвестор получает {Fmt.Pct(InvestorOwnership)} за {Fmt.Money(PostMoneyValuation * InvestorOwnership)} " +
                     $"при оценке после денег {Fmt.Money(PostMoneyValuation)}; цена акции " +
                     $"{Fmt.Num(PricePerShare, 4)}. Существующие акционеры получили оценку " +
                     $"{Fmt.Money(EffectivePreMoneyForFounders)} вместо заявленной " +
                     $"{Fmt.Money(declared)} — разница {Fmt.Pct(hiddenCost)}.")
            .Metric("Доля инвестора", Fmt.Pct(InvestorOwnership), null,
                "ровно инвестиция делить на оценку после денег")
            .Metric("Цена акции", Fmt.Num(PricePerShare, 4), null,
                "оценка после денег делить на полностью разводнённые акции")
            .Metric("Эффективная оценка", Fmt.Money(EffectivePreMoneyForFounders), null,
                $"против заявленной {Fmt.Money(declared)}",
                hiddenCost > 0.15 ? MetricQuality.Critical
                    : hiddenCost > 0.05 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Скрытая часть разводнения", Fmt.Pct(hiddenCost), null,
                "пул и конвертируемые инструменты",
                hiddenCost > 0.15 ? MetricQuality.Critical : MetricQuality.Warning)
            .Metric("Новых опционов", Fmt.Int(NewPoolShares), "акций",
                NewPoolShares > 0 ? "создано до денег, размывают только существующих" : "пул не пополнялся",
                NewPoolShares > 0 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Акций после раунда", Fmt.Int(TotalSharesAfter), null,
                "полностью разводнённый капитал");

        foreach (NoteConversion conversion in Conversions)
        {
            builder.Metric($"SAFE: {conversion.Holder}", Fmt.Pct(conversion.OwnershipAfter), null,
                $"{conversion.PriceDriver}, цена {Fmt.Num(conversion.ConversionPrice, 4)} " +
                $"против раундовой {Fmt.Num(PricePerShare, 4)}");
        }

        return builder
            .Finding("Опционный пул создаётся до денег и потому оплачивается только существующими " +
                     "акционерами: доля инвестора остаётся ровно расчётной. Это и есть pool shuffle — " +
                     "самая дорогая строка term sheet, о которой в нём не сказано ни слова.")
            .FindingIf(hardest is not null,
                $"Сильнее всех разводнён «{hardest?.Holder}»: с {Fmt.Pct(hardest?.Before ?? 0)} " +
                $"до {Fmt.Pct(hardest?.After ?? 0)}, то есть на {Fmt.Pct(hardest?.RelativeDilution ?? 0)} " +
                "от своей доли.")
            .FindingIf(hasNotes,
                $"Конвертируемые инструменты вошли по цене ниже раундовой, добавив " +
                $"{Fmt.Pct(Conversions.Sum(c => c.OwnershipAfter))} к разводнению. " +
                "Число их акций зависит от цены раунда, а цена раунда — от числа акций: " +
                "прикидка на салфетке систематически занижает эффект.")
            .WarningIf(hiddenCost > 0.15,
                $"Заявленная и фактическая оценка расходятся на {Fmt.Pct(hiddenCost)}. " +
                "Обсуждать в term sheet надо не только цифру pre-money, но и размер пула " +
                "и то, до или после денег он создаётся.")
            .WarningIf(NewPoolShares > 0 && InvestorOwnership > 0.25,
                "Крупная доля инвестора вместе с пополнением пула — сочетание, при котором " +
                "основатели теряют контроль быстрее, чем ожидают по одной цифре оценки.")
            .Warning("Антиразводняющие оговорки не моделируются: они срабатывают на понижающем " +
                     "раунде и требуют отдельного расчёта.")
            .Recommendation("Считайте эффективную оценку до подписания: она и есть настоящая " +
                            "цена сделки для существующих акционеров.")
            .Build();
    }
}

/// <summary>Разбор каскада выплат при выходе.</summary>
public sealed partial record ExitWaterfallResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var founders = Payouts
            .Where(p => !p.ShareClass.Contains("Series", StringComparison.OrdinalIgnoreCase)
                        && !p.ShareClass.Contains("SAFE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        double foundersPayout = founders.Sum(p => p.Payout);
        double foundersOwnership = founders.Sum(p => p.Ownership);
        double foundersShare = ExitValue > 0 ? foundersPayout / ExitValue : 0;
        double gap = foundersOwnership - foundersShare;

        var converted = Classes.Where(c => c.Converted).ToList();
        double preferenceShare = ExitValue > 0 ? TotalPreferences / ExitValue : 0;

        var builder = new InterpretationBuilder("Каскад выплат при выходе")
            .Summary($"При продаже за {Fmt.Money(ExitValue)} на ликвидационные преференции уходит " +
                     $"{Fmt.Money(TotalPreferences)} ({Fmt.Pct(preferenceShare)}), остаток " +
                     $"{Fmt.Money(Residual)} делится между участвующими акциями. Держатели " +
                     $"обыкновенных акций владеют {Fmt.Pct(foundersOwnership)} капитала, " +
                     $"а получают {Fmt.Pct(foundersShare)} денег.")
            .Metric("Цена сделки", Fmt.Money(ExitValue), null, "сумма к распределению")
            .Metric("Преференции", Fmt.Money(TotalPreferences), null,
                $"{Fmt.Pct(preferenceShare)} суммы сделки уходит раньше всех",
                preferenceShare > 0.5 ? MetricQuality.Critical
                    : preferenceShare > 0.2 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Обыкновенные: доля в капитале", Fmt.Pct(foundersOwnership), null,
                "то, что написано в таблице долей")
            .Metric("Обыкновенные: доля в деньгах", Fmt.Pct(foundersShare), null,
                "то, что будет фактически",
                gap > 0.1 ? MetricQuality.Critical : gap > 0.03 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Цена обыкновенной акции", Fmt.Num(CommonPerShare, 4), null,
                "столько стоит акция сотрудника при этой сделке",
                CommonPerShare > 0 ? MetricQuality.Neutral : MetricQuality.Critical)
            .Metric("Конвертировалось классов", converted.Count, null,
                converted.Count > 0
                    ? "им выгоднее доля, чем преференция"
                    : "всем привилегированным выгоднее преференция",
                MetricQuality.Unknown, 0);

        foreach (ClassOutcome outcome in Classes)
        {
            builder.Metric(outcome.ClassName, Fmt.Money(outcome.TotalPaid), null,
                outcome.Decision + (double.IsNaN(outcome.MultipleOnInvested)
                    ? string.Empty
                    : $", возврат {Fmt.Num(outcome.MultipleOnInvested)}x"));
        }

        return builder
            .Finding("Таблица долей не отвечает на вопрос «сколько это в деньгах». " +
                     "При наличии преференций зависимость выплаты от цены сделки нелинейна, " +
                     "и на умеренных суммах доля основателей может оказаться нулевой.")
            .FindingIf(gap > 0.05,
                $"Разрыв между долей в капитале и долей в деньгах составляет " +
                $"{Fmt.Pct(gap)} — это прямая цена ликвидационных преференций при данной сумме сделки.")
            .FindingIf(converted.Count > 0,
                $"Классы {string.Join(", ", converted.Select(c => c.ClassName))} выбрали конвертацию: " +
                "сделка достаточно крупная, чтобы доля была выгоднее преференции. " +
                "Это и есть та точка, после которой рост цены достаётся всем поровну.")
            .FindingIf(converted.Count == 0 && preferenceShare > 0.3,
                "Ни один привилегированный класс не конвертировался: сделка слишком мала, " +
                "и весь прирост цены до точки конвертации достаётся инвесторам.")
            .WarningIf(CommonPerShare <= 0,
                "Обыкновенная акция ничего не стоит при этой сумме сделки. Опционы сотрудников " +
                "в таком сценарии обнуляются — об этом стоит знать до того, как их выдавать.")
            .WarningIf(preferenceShare > 0.5,
                "Более половины суммы сделки уходит на преференции. Мотивация команды при таком " +
                "раскладе требует отдельного инструмента — например, плана удержания при продаже.")
            .Warning("Нераспределённые опционы учтены как обыкновенные акции без цены исполнения. " +
                     "На суммах, где она значима, опционы не исполняются, и расчёт по ним оптимистичен.")
            .Warning("Escrow, earn-out и налоги не учитываются: распределяется сумма, " +
                     "фактически поступившая акционерам в момент сделки.")
            .Recommendation("Постройте кривую выплат по диапазону цен, а не по одной точке: " +
                            "переломы на ней показывают, где интересы сторон расходятся.")
            .Build();
    }
}

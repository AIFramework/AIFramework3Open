using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Credit;

/// <summary>Профиль контрагента для оценки коммерческого кредита или факторинга.</summary>
public sealed record CounterpartyProfile
{
    /// <summary>Название контрагента.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Годовая выручка.</summary>
    public double Revenue { get; init; }

    /// <summary>Прибыль до процентов, налогов и амортизации.</summary>
    public double Ebitda { get; init; }

    /// <summary>Собственный капитал.</summary>
    public double Equity { get; init; }

    /// <summary>Совокупный долг.</summary>
    public double TotalDebt { get; init; }

    /// <summary>Оборотные активы.</summary>
    public double CurrentAssets { get; init; }

    /// <summary>Краткосрочные обязательства.</summary>
    public double CurrentLiabilities { get; init; }

    /// <summary>Темп роста выручки за год.</summary>
    public double RevenueGrowth { get; init; }

    /// <summary>Срок работы компании в годах.</summary>
    public double YearsInBusiness { get; init; }

    /// <summary>Средняя просрочка оплаты счетов в днях.</summary>
    public double AveragePaymentDelayDays { get; init; }

    /// <summary>Доля поставок, по которым возникали споры.</summary>
    public double DisputeRate { get; init; }

    /// <summary>Доля крупнейшего покупателя в выручке.</summary>
    public double BuyerConcentration { get; init; }

    /// <summary>Наличие налоговой задолженности.</summary>
    public bool HasTaxArrears { get; init; }

    /// <summary>Наличие существенных судебных исков.</summary>
    public bool HasLitigation { get; init; }

    /// <summary>Запрошенный лимит отгрузки или финансирования.</summary>
    public double RequestedLimit { get; init; }
}

/// <summary>Вклад отдельного фактора в скоринговый балл контрагента.</summary>
/// <param name="Name">Название фактора.</param>
/// <param name="Value">Наблюдённое значение.</param>
/// <param name="Score">Нормированная оценка от нуля до единицы.</param>
/// <param name="Weight">Вес фактора.</param>
/// <param name="Contribution">Вклад в итоговый балл.</param>
/// <param name="Comment">Пояснение оценки.</param>
public sealed record CounterpartyFactor(
    string Name, double Value, double Score, double Weight, double Contribution, string Comment);

/// <summary>Итог скоринга контрагента.</summary>
public sealed record CounterpartyScore : IInterpretable
{
    /// <summary>Название контрагента.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Итоговый балл от нуля до ста.</summary>
    public double Score { get; init; }

    /// <summary>Присвоенный класс от A до E.</summary>
    public string Grade { get; init; } = "E";

    /// <summary>Годовая вероятность дефолта, соответствующая баллу.</summary>
    public double ProbabilityOfDefault { get; init; }

    /// <summary>Рекомендованный лимит отгрузки или финансирования.</summary>
    public double RecommendedLimit { get; init; }

    /// <summary>Запрошенный лимит.</summary>
    public double RequestedLimit { get; init; }

    /// <summary>Доля суммы поставки, которую можно профинансировать в факторинге.</summary>
    public double AdvanceRate { get; init; }

    /// <summary>Ожидаемые потери при работе на рекомендованном лимите.</summary>
    public double ExpectedLoss { get; init; }

    /// <summary>Решение по заявке.</summary>
    public string Decision { get; init; } = string.Empty;

    /// <summary>Вклады факторов, отсортированные по влиянию.</summary>
    public IReadOnlyList<CounterpartyFactor> Factors { get; init; } = [];

    /// <summary>Сработавшие стоп-факторы.</summary>
    public IReadOnlyList<string> StopFactors { get; init; } = [];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        CounterpartyFactor? strongest = Factors.OrderByDescending(f => f.Contribution).FirstOrDefault();
        CounterpartyFactor? weakest = Factors
            .OrderBy(f => f.Score)
            .ThenByDescending(f => f.Weight)
            .FirstOrDefault();

        double coverage = RequestedLimit > 0 ? RecommendedLimit / RequestedLimit : 1;
        double lostPoints = Factors.Sum(f => f.Weight * (1 - f.Score)) * 100;

        var builder = new InterpretationBuilder($"Скоринг контрагента: {Name}")
            .Summary($"Балл {Fmt.Num(Score, 1)} из 100, класс {Grade}, вероятность дефолта " +
                     $"{Fmt.Pct(ProbabilityOfDefault, 2)} за год. Рекомендованный лимит " +
                     $"{Fmt.Money(RecommendedLimit)} при запрошенных {Fmt.Money(RequestedLimit)}. " +
                     $"Решение: {Decision}.")
            .Metric("Балл", Score, "из 100", $"класс {Grade}",
                Score >= 65 ? MetricQuality.Good : Score >= 50 ? MetricQuality.Neutral
                    : Score >= 35 ? MetricQuality.Warning : MetricQuality.Critical, 1)
            .Metric("Вероятность дефолта", ProbabilityOfDefault, null, "за год работы с лимитом",
                ProbabilityOfDefault > 0.1 ? MetricQuality.Critical
                    : ProbabilityOfDefault > 0.03 ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Рекомендованный лимит", Fmt.Money(RecommendedLimit), null,
                $"{Fmt.Pct(coverage, 0)} от запрошенного",
                coverage >= 1 ? MetricQuality.Good : coverage >= 0.5 ? MetricQuality.Warning
                    : MetricQuality.Critical)
            .Metric("Ставка финансирования", AdvanceRate, null,
                "доля поставки, финансируемая в факторинге",
                AdvanceRate >= 0.8 ? MetricQuality.Good : MetricQuality.Neutral, 2)
            .Metric("Ожидаемые потери", Fmt.Money(ExpectedLoss), null,
                "на рекомендованном лимите с учётом возвратности")
            .Metric("Потеряно баллов", lostPoints, null,
                "суммарный недобор по всем факторам", MetricQuality.Neutral, 1);

        foreach (CounterpartyFactor factor in Factors)
        {
            builder.Metric(factor.Name, factor.Contribution, "балл.",
                $"{factor.Comment}; оценка {Fmt.Pct(factor.Score, 0)} при весе {Fmt.Pct(factor.Weight, 0)}",
                factor.Score >= 0.7 ? MetricQuality.Good
                    : factor.Score >= 0.4 ? MetricQuality.Neutral : MetricQuality.Warning, 1);
        }

        foreach (string stop in StopFactors) builder.Warning($"Стоп-фактор: {stop}.");

        return builder
            .FindingIf(strongest is not null,
                $"Больше всего баллов даёт фактор «{strongest?.Name}» — {Fmt.Num(strongest?.Contribution ?? 0, 1)}.")
            .FindingIf(weakest is not null,
                $"Слабое место — «{weakest?.Name}»: оценка {Fmt.Pct(weakest?.Score ?? 0, 0)} " +
                $"при весе {Fmt.Pct(weakest?.Weight ?? 0, 0)}. Именно здесь имеет смысл " +
                "запрашивать дополнительные документы или обеспечение.")
            .Finding("Решение по коммерческому кредиту принимается не по вероятности дефолта, " +
                     "а по соотношению маржи сделки и ожидаемых потерь. Лимит здесь — " +
                     "инструмент управления риском, а не следствие балла.")
            .FindingIf(coverage < 1,
                $"Запрошенный лимит превышает рекомендованный на " +
                $"{Fmt.Money(RequestedLimit - RecommendedLimit)}. Разницу разумно закрывать " +
                "предоплатой, обеспечением или страхованием дебиторской задолженности.")
            .WarningIf(StopFactors.Count > 0,
                $"Сработало стоп-факторов: {StopFactors.Count}. При любом из них решение " +
                "принимается кредитным комитетом вручную, вне зависимости от балла.")
            .Warning("Скоринг контрагента опирается на отчётность, которая обычно отстаёт " +
                     "на квартал и более. Платёжная дисциплина по вашим собственным отгрузкам " +
                     "предсказывает дефолт лучше любого коэффициента из баланса.")
            .Recommendation("Пересматривайте лимит по факту платёжного поведения: первые " +
                            "три-четыре оплаченные поставки дают больше информации, чем вся " +
                            "финансовая отчётность контрагента.")
            .Recommendation("Сопоставьте ожидаемые потери с маржой по сделке: если маржа " +
                            "не покрывает потери с запасом, лимит нужно снижать даже при " +
                            "формально приемлемом классе.")
            .Build();
    }
}

/// <summary>Настройки скоринга контрагентов.</summary>
public sealed record CounterpartyOptions
{
    /// <summary>Потери при дефолте контрагента.</summary>
    public double LossGivenDefault { get; init; } = 0.6;

    /// <summary>Предельная доля собственного капитала контрагента, отдаваемая в лимит.</summary>
    public double EquityShareCap { get; init; } = 0.15;

    /// <summary>Предельное число месяцев выручки контрагента в лимите.</summary>
    public double RevenueMonthsCap { get; init; } = 1.5;

    /// <summary>Минимальный балл для положительного решения.</summary>
    public double ApprovalScore { get; init; } = 50;
}

/// <summary>
/// Скоринг контрагентов для коммерческого кредита и факторинга.
/// </summary>
/// <remarks>
/// <para>
/// Задача отличается от банковского скоринга: решается не «выдать или нет», а
/// «на какую сумму отгружать без предоплаты». Поэтому модель строится как
/// взвешенная оценка факторов с явными весами, а её выход — лимит, ставка
/// финансирования и ожидаемые потери, а не только вероятность дефолта.
/// </para>
/// <para>
/// Факторы делятся на три группы: финансовая устойчивость (капитал, ликвидность,
/// долговая нагрузка, рентабельность), масштаб и история (выручка, срок работы,
/// динамика) и поведение (просрочки оплат, споры по поставкам, концентрация
/// покупателей). Каждый фактор нормируется в отрезок от нуля до единицы
/// кусочно-линейным отображением и складывается с весом.
/// </para>
/// <para>
/// Лимит ограничивается сверху двумя независимыми правилами — долей капитала
/// контрагента и месяцами его выручки — и масштабируется баллом. Смысл двойного
/// ограничения в том, что ни один поставщик не должен становиться основным
/// кредитором покупателя: даже отличный балл не оправдывает лимит, сопоставимый
/// с собственным капиталом контрагента.
/// </para>
/// </remarks>
public static class CounterpartyScoring
{
    /// <summary>Оценивает контрагента и рассчитывает лимит.</summary>
    /// <param name="profile">Финансовые и поведенческие данные контрагента.</param>
    /// <param name="options">Настройки лимитов; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Балл, класс, вероятность дефолта, лимит и решение.</returns>
    /// <exception cref="ArgumentNullException">Профиль не задан.</exception>
    /// <exception cref="ArgumentException">Выручка контрагента неположительна.</exception>
    public static CounterpartyScore Score(CounterpartyProfile profile, CounterpartyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Revenue <= 0)
            throw new ArgumentException("Выручка контрагента должна быть положительной.", nameof(profile));

        options ??= new CounterpartyOptions();

        double equityRatio = profile.Equity + profile.TotalDebt > 0
            ? profile.Equity / (profile.Equity + profile.TotalDebt)
            : 0;
        double currentRatio = profile.CurrentLiabilities > 0
            ? profile.CurrentAssets / profile.CurrentLiabilities
            : 3;
        double debtToEbitda = profile.Ebitda > 0
            ? profile.TotalDebt / profile.Ebitda
            : profile.TotalDebt > 0 ? 99 : 0;
        double margin = profile.Ebitda / profile.Revenue;

        var factors = new List<CounterpartyFactor>
        {
            Factor("Достаточность капитала", equityRatio, 0.18,
                Ramp(equityRatio, 0.05, 0.45),
                $"собственный капитал покрывает {Fmt.Pct(equityRatio, 0)} пассивов"),
            Factor("Текущая ликвидность", currentRatio, 0.15,
                Ramp(currentRatio, 0.8, 2.0),
                $"оборотные активы к краткосрочным обязательствам {Fmt.Num(currentRatio, 2)}"),
            Factor("Долговая нагрузка", debtToEbitda, 0.15,
                1 - Ramp(debtToEbitda, 1.0, 5.0),
                debtToEbitda >= 99
                    ? "прибыль не покрывает долг"
                    : $"долг к прибыли {Fmt.Num(debtToEbitda, 1)}"),
            Factor("Рентабельность", margin, 0.12,
                Ramp(margin, 0.0, 0.20),
                $"рентабельность по прибыли до амортизации {Fmt.Pct(margin, 1)}"),
            Factor("Масштаб бизнеса", profile.Revenue, 0.08,
                Ramp(Math.Log10(Math.Max(profile.Revenue, 1)), 6, 9),
                $"годовая выручка {Fmt.Money(profile.Revenue)}"),
            Factor("Срок работы", profile.YearsInBusiness, 0.07,
                Ramp(profile.YearsInBusiness, 1, 7),
                $"на рынке {Fmt.Num(profile.YearsInBusiness, 1)} г."),
            Factor("Динамика выручки", profile.RevenueGrowth, 0.05,
                Ramp(profile.RevenueGrowth, -0.15, 0.15),
                $"выручка изменилась на {Fmt.Pct(profile.RevenueGrowth, 1)} за год"),
            Factor("Платёжная дисциплина", profile.AveragePaymentDelayDays, 0.12,
                1 - Ramp(profile.AveragePaymentDelayDays, 0, 45),
                $"средняя просрочка {Fmt.Num(profile.AveragePaymentDelayDays, 0)} дн."),
            Factor("Споры по поставкам", profile.DisputeRate, 0.04,
                1 - Ramp(profile.DisputeRate, 0.0, 0.10),
                $"споры по {Fmt.Pct(profile.DisputeRate, 1)} поставок"),
            Factor("Концентрация покупателей", profile.BuyerConcentration, 0.04,
                1 - Ramp(profile.BuyerConcentration, 0.2, 0.7),
                $"крупнейший покупатель даёт {Fmt.Pct(profile.BuyerConcentration, 0)} выручки"),
        };

        double score = factors.Sum(f => f.Contribution);

        var stopFactors = new List<string>();
        if (profile.HasTaxArrears) stopFactors.Add("налоговая задолженность");
        if (profile.HasLitigation) stopFactors.Add("существенные судебные иски");
        if (profile.Equity < 0) stopFactors.Add("отрицательный собственный капитал");
        if (profile.AveragePaymentDelayDays > 60) stopFactors.Add("систематическая просрочка платежей");

        // Стоп-факторы не обнуляют балл, но снижают его: решение остаётся за комитетом.
        score = EconMath.Clamp(score - (stopFactors.Count * 8), 0, 100);

        string grade = score switch
        {
            >= 80 => "A",
            >= 65 => "B",
            >= 50 => "C",
            >= 35 => "D",
            _ => "E",
        };

        double pd = EconMath.Clamp(0.5 * Math.Exp(-0.055 * score), 0.0005, 0.5);

        double baseLimit = Math.Min(
            options.EquityShareCap * Math.Max(profile.Equity, 0),
            options.RevenueMonthsCap * profile.Revenue / 12);

        double recommended = baseLimit * Math.Pow(EconMath.Clamp(score / 100, 0, 1), 1.5);
        if (stopFactors.Count > 0) recommended *= 0.5;
        if (profile.RequestedLimit > 0) recommended = Math.Min(recommended, profile.RequestedLimit);

        double advance = EconMath.Clamp(
            0.9 - (pd * 2) - (profile.DisputeRate * 1.5) - (stopFactors.Count * 0.05), 0.3, 0.9);

        string decision =
            stopFactors.Count > 0 ? "решение кредитного комитета: сработали стоп-факторы"
            : score < options.ApprovalScore ? "отказ: балл ниже порога одобрения"
            : profile.RequestedLimit > 0 && recommended < profile.RequestedLimit * 0.95
                ? "одобрить с уменьшенным лимитом"
                : "одобрить в запрошенном объёме";

        return new CounterpartyScore
        {
            Name = profile.Name,
            Score = score,
            Grade = grade,
            ProbabilityOfDefault = pd,
            RecommendedLimit = recommended,
            RequestedLimit = profile.RequestedLimit,
            AdvanceRate = advance,
            ExpectedLoss = recommended * pd * options.LossGivenDefault,
            Decision = decision,
            Factors = [.. factors.OrderByDescending(f => f.Contribution)],
            StopFactors = stopFactors,
        };
    }

    /// <summary>Оценивает пул контрагентов и упорядочивает по баллу.</summary>
    /// <param name="profiles">Контрагенты.</param>
    /// <param name="options">Настройки лимитов; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Оценки, отсортированные по убыванию балла.</returns>
    /// <exception cref="ArgumentNullException">Список не задан.</exception>
    public static IReadOnlyList<CounterpartyScore> ScoreAll(
        IReadOnlyList<CounterpartyProfile> profiles, CounterpartyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return [.. profiles.Select(p => Score(p, options)).OrderByDescending(s => s.Score)];
    }

    /// <summary>Собирает фактор с рассчитанным вкладом в балл.</summary>
    private static CounterpartyFactor Factor(
        string name, double value, double weight, double score, string comment) =>
        new(name, value, EconMath.Clamp(score, 0, 1), weight,
            EconMath.Clamp(score, 0, 1) * weight * 100, comment);

    /// <summary>Кусочно-линейное отображение значения в отрезок от нуля до единицы.</summary>
    private static double Ramp(double value, double low, double high) =>
        high <= low ? 0 : EconMath.Clamp((value - low) / (high - low), 0, 1);
}

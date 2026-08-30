using System;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Credit;

/// <summary>Входные данные структурной модели кредитного риска.</summary>
public sealed record MertonInput
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Рыночная капитализация.</summary>
    public double EquityValue { get; init; }

    /// <summary>Годовая волатильность доходности акции.</summary>
    public double EquityVolatility { get; init; }

    /// <summary>Краткосрочный долг: погашение в пределах года.</summary>
    public double ShortTermDebt { get; init; }

    /// <summary>Долгосрочный долг.</summary>
    public double LongTermDebt { get; init; }

    /// <summary>Безрисковая ставка, годовая.</summary>
    public double RiskFreeRate { get; init; } = 0.05;

    /// <summary>Ожидаемая доходность активов для расчёта реальной вероятности дефолта.</summary>
    /// <remarks>При <c>null</c> берётся безрисковая ставка, то есть риск-нейтральная мера.</remarks>
    public double? AssetDrift { get; init; }

    /// <summary>Горизонт оценки в годах.</summary>
    public double Horizon { get; init; } = 1.0;
}

/// <summary>Результат оценки вероятности дефолта по модели Мертона.</summary>
public sealed record MertonResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Рыночная стоимость активов, восстановленная из капитализации.</summary>
    public double AssetValue { get; init; }

    /// <summary>Годовая волатильность стоимости активов.</summary>
    public double AssetVolatility { get; init; }

    /// <summary>Точка дефолта по правилу KMV.</summary>
    public double DefaultPoint { get; init; }

    /// <summary>Расстояние до дефолта в стандартных отклонениях активов.</summary>
    public double DistanceToDefault { get; init; }

    /// <summary>Вероятность дефолта на горизонте при ожидаемой доходности активов.</summary>
    public double ProbabilityOfDefault { get; init; }

    /// <summary>Риск-нейтральная вероятность дефолта: то, что заложено в цены облигаций.</summary>
    public double RiskNeutralProbability { get; init; }

    /// <summary>Кредитный спред, вытекающий из модели, в долях годовых.</summary>
    public double ImpliedCreditSpread { get; init; }

    /// <summary>Долговая нагрузка: точка дефолта к стоимости активов.</summary>
    public double Leverage { get; init; }

    /// <summary>Рыночная стоимость долга по модели.</summary>
    public double DebtValue { get; init; }

    /// <summary>Горизонт оценки в годах.</summary>
    public double Horizon { get; init; }

    /// <summary>Признак сходимости итерационной схемы.</summary>
    public bool Converged { get; init; }

    /// <summary>Число выполненных итераций.</summary>
    public int Iterations { get; init; }

    /// <summary>Волатильность акции, поданная на вход и воспроизводимая моделью.</summary>
    public double EquityVolatilityImplied { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string grade = DistanceToDefault switch
        {
            > 6 => "инвестиционный уровень с большим запасом",
            > 4 => "устойчивый инвестиционный уровень",
            > 2.5 => "пограничная зона между инвестиционным и спекулятивным уровнем",
            > 1.5 => "спекулятивный уровень",
            _ => "зона высокого риска дефолта",
        };

        double volatilityGap = EquityVolatilityImplied - AssetVolatility;

        return new InterpretationBuilder("Оценка вероятности дефолта по модели Мертона")
            .Summary($"Рыночная стоимость активов {Fmt.Money(AssetValue)} при точке дефолта " +
                     $"{Fmt.Money(DefaultPoint)}. Расстояние до дефолта {Fmt.Num(DistanceToDefault, 2)} " +
                     $"стандартных отклонения, вероятность дефолта на горизонте " +
                     $"{Fmt.Num(Horizon, 1)} г. — {Fmt.Pct(ProbabilityOfDefault, 3)}. " +
                     $"Это {grade}.")
            .Metric("Стоимость активов", Fmt.Money(AssetValue), null,
                "восстановлена из капитализации и структуры долга")
            .Metric("Волатильность активов", AssetVolatility, null,
                $"против {Fmt.Pct(EquityVolatilityImplied, 1)} у акции — разница создаётся рычагом",
                MetricQuality.Neutral, 4)
            .Metric("Точка дефолта", Fmt.Money(DefaultPoint), null,
                "краткосрочный долг плюс половина долгосрочного")
            .Metric("Расстояние до дефолта", DistanceToDefault, "σ", grade,
                DistanceToDefault > 4 ? MetricQuality.Good
                    : DistanceToDefault > 2.5 ? MetricQuality.Neutral
                    : DistanceToDefault > 1.5 ? MetricQuality.Warning : MetricQuality.Critical, 2)
            .Metric("Вероятность дефолта", ProbabilityOfDefault, null,
                $"на горизонте {Fmt.Num(Horizon, 1)} г.",
                ProbabilityOfDefault > 0.05 ? MetricQuality.Critical
                    : ProbabilityOfDefault > 0.01 ? MetricQuality.Warning : MetricQuality.Good, 5)
            .Metric("Риск-нейтральная вероятность", RiskNeutralProbability, null,
                "оценка, сопоставимая с ценами облигаций", MetricQuality.Neutral, 5)
            .Metric("Кредитный спред", ImpliedCreditSpread, null,
                "премия за риск, вытекающая из модели",
                ImpliedCreditSpread > 0.05 ? MetricQuality.Warning : MetricQuality.Neutral, 4)
            .Metric("Долговая нагрузка", Leverage, null,
                "точка дефолта к стоимости активов",
                Leverage > 0.7 ? MetricQuality.Critical
                    : Leverage > 0.5 ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Finding("Модель извлекает кредитный риск из рынка акций, а не из отчётности. " +
                     "Поэтому она реагирует на новости в тот же день, тогда как рейтинг и " +
                     "коэффициенты отчётности отстают на кварталы.")
            .Finding($"Рычаг превращает волатильность активов {Fmt.Pct(AssetVolatility, 1)} " +
                     $"в волатильность акции {Fmt.Pct(EquityVolatilityImplied, 1)}: разница в " +
                     $"{Fmt.Pct(volatilityGap, 1)} и есть вклад долга в риск акционера.")
            .FindingIf(RiskNeutralProbability > ProbabilityOfDefault,
                "Риск-нейтральная вероятность выше реальной — так и должно быть: разница " +
                "составляет премию за риск, которую требуют держатели долга.")
            .WarningIf(!Converged,
                "Итерационная схема не сошлась. Результат ненадёжен, проверьте входные данные: " +
                "чаще всего проблема в нулевой или завышенной волатильности акции.")
            .WarningIf(DistanceToDefault < 2,
                $"Расстояние до дефолта {Fmt.Num(DistanceToDefault, 2)} — компания в зоне, где " +
                "модель особенно чувствительна к точке дефолта. Проверьте структуру долга " +
                "по срочности, а не только его общую сумму.")
            .Warning("Модель предполагает единственную выплату долга в конце горизонта и " +
                     "логнормальную динамику активов. Она системно недооценивает вероятность " +
                     "дефолта качественных заёмщиков на коротких горизонтах, поскольку " +
                     "не допускает скачков стоимости активов.")
            .Recommendation("Используйте расстояние до дефолта как относительную меру: " +
                            "ранжирование компаний по нему устойчиво, а абсолютная вероятность " +
                            "требует калибровки на исторической частоте дефолтов.")
            .Recommendation("Пересчитывайте оценку при каждом существенном изменении структуры " +
                            "долга: точка дефолта влияет на результат сильнее, чем волатильность.")
            .Build();
    }
}

/// <summary>
/// Структурная модель кредитного риска Мертона в реализации KMV.
/// </summary>
/// <remarks>
/// <para>
/// Собственный капитал компании — это опцион колл на её активы с ценой
/// исполнения, равной долгу. Если стоимость активов в момент погашения ниже
/// долга, акционеры не исполняют опцион и компания уходит в дефолт. Отсюда
/// система из двух уравнений, связывающая наблюдаемые величины (капитализацию
/// и волатильность акции) с ненаблюдаемыми (стоимостью и волатильностью
/// активов):
/// </para>
/// <code>
/// E = V * N(d1) - D * exp(-r * T) * N(d2)
/// sigmaE * E = N(d1) * sigmaV * V
/// d1 = (ln(V / D) + (r + sigmaV^2 / 2) * T) / (sigmaV * sqrt(T))
/// d2 = d1 - sigmaV * sqrt(T)
/// </code>
/// <para>
/// Система решается итерационно. По найденным активам считается расстояние до
/// дефолта — на сколько стандартных отклонений стоимость активов превышает
/// точку дефолта, — а из него вероятность дефолта.
/// </para>
/// <para>
/// Точка дефолта берётся по правилу KMV: краткосрочный долг плюс половина
/// долгосрочного. Эмпирически компании уходят в дефолт именно на этом уровне,
/// а не при падении активов ниже полного долга, поскольку долгосрочные
/// обязательства обычно удаётся рефинансировать.
/// </para>
/// </remarks>
public static class MertonModel
{
    private const int MaxIterations = 200;
    private const double Tolerance = 1e-10;

    /// <summary>Оценивает вероятность дефолта публичной компании.</summary>
    /// <param name="input">Капитализация, волатильность акции и структура долга.</param>
    /// <returns>Стоимость активов, расстояние до дефолта и вероятность дефолта.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    /// <exception cref="ArgumentException">Капитализация, волатильность или горизонт неположительны.</exception>
    public static MertonResult Estimate(MertonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.EquityValue <= 0)
            throw new ArgumentException("Капитализация должна быть положительной.", nameof(input));
        if (input.EquityVolatility <= 0)
            throw new ArgumentException("Волатильность акции должна быть положительной.", nameof(input));
        if (input.Horizon <= 0)
            throw new ArgumentException("Горизонт должен быть положительным.", nameof(input));

        double defaultPoint = input.ShortTermDebt + (0.5 * input.LongTermDebt);
        double t = input.Horizon;
        double r = input.RiskFreeRate;
        double sqrtT = Math.Sqrt(t);

        if (defaultPoint <= 0)
        {
            // Компания без долга: дефолт по модели невозможен.
            return new MertonResult
            {
                Company = input.Company,
                AssetValue = input.EquityValue,
                AssetVolatility = input.EquityVolatility,
                EquityVolatilityImplied = input.EquityVolatility,
                DefaultPoint = 0,
                DistanceToDefault = double.PositiveInfinity,
                ProbabilityOfDefault = 0,
                RiskNeutralProbability = 0,
                ImpliedCreditSpread = 0,
                Leverage = 0,
                DebtValue = 0,
                Horizon = t,
                Converged = true,
                Iterations = 0,
            };
        }

        double assetVolatility = input.EquityVolatility * input.EquityValue /
                                 (input.EquityValue + defaultPoint);
        double assetValue = input.EquityValue + defaultPoint;
        bool converged = false;
        int iterations = 0;

        for (; iterations < MaxIterations; iterations++)
        {
            assetValue = SolveAssetValue(input.EquityValue, defaultPoint, r, t, assetVolatility);

            double d1 = D1(assetValue, defaultPoint, r, t, assetVolatility);
            double delta = EconMath.NormalCdf(d1);
            double updated = delta > 1e-12
                ? input.EquityVolatility * input.EquityValue / (delta * assetValue)
                : assetVolatility;

            updated = EconMath.Clamp(updated, 1e-6, 5);

            if (Math.Abs(updated - assetVolatility) < Tolerance)
            {
                assetVolatility = updated;
                converged = true;
                iterations++;
                break;
            }

            assetVolatility = updated;
        }

        double finalD1 = D1(assetValue, defaultPoint, r, t, assetVolatility);
        double finalD2 = finalD1 - (assetVolatility * sqrtT);
        double drift = input.AssetDrift ?? r;

        double distanceToDefault =
            (Math.Log(assetValue / defaultPoint) + ((drift - (assetVolatility * assetVolatility / 2)) * t)) /
            (assetVolatility * sqrtT);

        double debtValue = assetValue - input.EquityValue;
        double spread = debtValue > 0 && defaultPoint > 0
            ? (-Math.Log(debtValue / defaultPoint) / t) - r
            : 0;

        return new MertonResult
        {
            Company = input.Company,
            AssetValue = assetValue,
            AssetVolatility = assetVolatility,
            EquityVolatilityImplied = input.EquityVolatility,
            DefaultPoint = defaultPoint,
            DistanceToDefault = distanceToDefault,
            ProbabilityOfDefault = EconMath.NormalCdf(-distanceToDefault),
            RiskNeutralProbability = EconMath.NormalCdf(-finalD2),
            ImpliedCreditSpread = Math.Max(0, spread),
            Leverage = assetValue > 0 ? defaultPoint / assetValue : 0,
            DebtValue = Math.Max(debtValue, 0),
            Horizon = t,
            Converged = converged,
            Iterations = iterations,
        };
    }

    /// <summary>Значение d1 формулы Блэка-Шоулза для активов компании.</summary>
    private static double D1(double assetValue, double defaultPoint, double r, double t, double sigma) =>
        (Math.Log(assetValue / defaultPoint) + ((r + (sigma * sigma / 2)) * t)) / (sigma * Math.Sqrt(t));

    /// <summary>Находит стоимость активов, воспроизводящую наблюдаемую капитализацию.</summary>
    /// <remarks>
    /// Стоимость колла монотонно растёт по стоимости базового актива, поэтому
    /// уравнение решается делением отрезка пополам без риска разойтись.
    /// </remarks>
    private static double SolveAssetValue(
        double equityValue, double defaultPoint, double r, double t, double sigma)
    {
        double Equity(double v)
        {
            double d1 = D1(v, defaultPoint, r, t, sigma);
            double d2 = d1 - (sigma * Math.Sqrt(t));
            return (v * EconMath.NormalCdf(d1)) - (defaultPoint * Math.Exp(-r * t) * EconMath.NormalCdf(d2));
        }

        double low = equityValue;
        double high = equityValue + defaultPoint;

        for (int i = 0; i < 100 && Equity(high) < equityValue; i++)
            high += defaultPoint > 0 ? defaultPoint : equityValue;

        for (int i = 0; i < 200; i++)
        {
            double mid = (low + high) / 2;
            if (Equity(mid) < equityValue) low = mid;
            else high = mid;
        }

        return (low + high) / 2;
    }
}

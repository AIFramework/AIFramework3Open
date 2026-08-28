using AI.DataStructs.Algebraic;
using AI.Economics.Forecasting;
using AI.Economics.Portfolio;
using AI.Economics.Pricing;
using AI.Economics.Projects;
using AI.Economics.Risk;
using AI.Economics.Runway;
using AI.Economics.Saas;
using AI.Economics.UnitEconomics;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>econ</c>: юнит-экономика, инвестиционные расчёты, риск и прогноз.
/// </summary>
/// <remarks>
/// Из библиотеки взята та её часть, которая отвечает на вопросы, задаваемые словами: «окупится
/// ли проект», «на сколько хватит денег», «какая цена выгоднее». Эконометрика с панельными
/// данными и оценка деривативов остались снаружи — не потому, что хуже, а потому, что вопрос к
/// ним ставится не одной строкой, и обёртка из одной строки его бы исказила.
/// <para>
/// Результаты возвращаются записями с русскими именами полей: их читает и человек, и модель,
/// а <c>ltv_to_cac</c> посреди русского скрипта заставляет держать в голове перевод.
/// </para>
/// </remarks>
[ScriptModule("econ", "Экономика: юнит-экономика, инвестиции, кредиты, риск, прогноз", Version = "0.1")]
public static class EconModule
{
    // --- юнит-экономика ---

    /// <summary>
    /// Полный расчёт юнит-экономики.
    /// </summary>
    /// <remarks>
    /// Одна функция с десятком необязательных аргументов, а не десяток функций: все эти числа
    /// считаются друг через друга, и разнесённые по вызовам они разошлись бы между собой при
    /// первой же правке одного из них.
    /// </remarks>
    [ScriptFn("unit", "Юнит-экономика: CAC, ARPU, LTV, окупаемость привлечения", Returns = "record",
        Example = "econ.unit(marketing: 300000, customers: 150, revenue: 4000, margin: 0.7, churn: 0.08)")]
    public static ScriptRecord Unit(
        IScriptContext context,
        [ScriptParam("расходы на маркетинг за период")] double marketing,
        [ScriptParam("привлечено клиентов за период")] double customers,
        [ScriptParam("выручка с клиента за период")] double revenue,
        [ScriptParam("доля валовой маржи от 0 до 1")] double margin = 1,
        [ScriptParam("отток клиентов за период от 0 до 1")] double churn = 0,
        [ScriptParam("расходы на продажи за период")] double sales = 0,
        [ScriptParam("переменные затраты на клиента за период")] double variable_cost = 0,
        [ScriptParam("ставка дисконтирования за период")] double discount = 0,
        [ScriptParam("горизонт в периодах; 0 — по оттоку")] int horizon = 0)
    {
        Require(customers > 0, "econ.unit: число привлечённых клиентов должно быть больше нуля");
        Require(margin is > 0 and <= 1, "econ.unit: доля маржи лежит в (0, 1]");
        Require(churn is >= 0 and <= 1, "econ.unit: отток лежит в [0, 1]");

        UnitEconomicsResult result = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
        {
            MarketingSpend = marketing,
            SalesSpend = sales,
            NewCustomers = customers,
            RevenuePerPeriod = revenue,
            GrossMarginRate = margin,
            VariableCostPerPeriod = variable_cost,
            ChurnRate = churn,
            DiscountRate = discount,
            Horizon = horizon,
        });

        context.CountAllocation(result.Survival.Count + result.CumulativeNet.Count);

        return Record(
            ("cac", result.Cac),
            ("arpu", result.Arpu),
            ("вклад_за_период", result.ContributionPerPeriod),
            ("маржа_вклада", result.ContributionMarginRate),
            ("ltv", result.Ltv),
            ("ltv_без_дисконта", result.UndiscountedLtv),
            ("ltv_к_cac", result.LtvToCac),
            ("чистый_вклад", result.NetContribution),
            ("окупаемость_периодов", result.CacPaybackPeriods),
            ("срок_жизни_периодов", result.ExpectedLifetimePeriods),
            ("горизонт", result.HorizonUsed));
    }

    [ScriptFn("ltv", "Пожизненная ценность клиента по оттоку",
        Example = "econ.ltv(contribution: 2800, churn: 0.08, discount: 0.01)")]
    public static double Ltv(
        [ScriptParam("вклад клиента за период")] double contribution,
        [ScriptParam("отток за период от 0 до 1")] double churn,
        [ScriptParam("ставка дисконтирования за период")] double discount = 0)
    {
        Require(churn is > 0 and <= 1, "econ.ltv: отток лежит в (0, 1]");

        return UnitEconomicsCalculator.LtvFromChurn(contribution, churn, discount);
    }

    [ScriptFn("rule_of_40", "Правило сорока: рост плюс маржа в процентах",
        Example = "econ.rule_of_40(growth: 60, margin: -15)")]
    public static double RuleOfForty(
        [ScriptParam("рост выручки в процентах")] double growth,
        [ScriptParam("маржа в процентах")] double margin) => SaasMetrics.RuleOf40(growth, margin);

    [ScriptFn("magic_number", "Отдача от вложений в продажи и маркетинг",
        Example = "econ.magic_number(arr_start: 12000000, arr_end: 15000000, spend: 4000000)")]
    public static double MagicNumber(
        [ScriptParam("годовая выручка на начало")] double arr_start,
        [ScriptParam("годовая выручка на конец")] double arr_end,
        [ScriptParam("расходы на продажи и маркетинг")] double spend) =>
        SaasMetrics.MagicNumber(arr_start, arr_end, spend);

    [ScriptFn("burn_multiple", "Сколько денег сожжено на рубль прироста выручки",
        Example = "econ.burn_multiple(net_burn: 5000000, net_new_arr: 3000000)")]
    public static double BurnMultiple(
        [ScriptParam("чистое сжигание денег")] double net_burn,
        [ScriptParam("прирост годовой выручки")] double net_new_arr) =>
        SaasMetrics.BurnMultiple(net_burn, net_new_arr);

    [ScriptFn("cac_payback", "За сколько месяцев окупается привлечение клиента",
        Example = "econ.cac_payback(cac: 2000, arpa: 400, margin: 0.75)")]
    public static double CacPayback(
        [ScriptParam("стоимость привлечения")] double cac,
        [ScriptParam("выручка с клиента в месяц")] double arpa,
        [ScriptParam("доля валовой маржи")] double margin = 1) =>
        SaasMetrics.CacPaybackMonths(cac, arpa, margin);

    /// <summary>
    /// Сколько месяцев хватит денег.
    /// </summary>
    /// <remarks>
    /// Считает и детерминированный срок, и распределение по имитациям: срок «двенадцать
    /// месяцев» при разбросе от шести до тридцати — это не то же самое, что двенадцать месяцев
    /// с разбросом в месяц, а среднее у них одинаковое.
    /// </remarks>
    [ScriptFn("runway", "На сколько месяцев хватит денег: детерминированный срок и имитации",
        Returns = "record",
        Example = "econ.runway(cash: 30000000, revenue: 4000000, costs: 7000000, growth: 0.08)")]
    public static ScriptRecord Runway(
        IScriptContext context,
        [ScriptParam("денег на счетах")] double cash,
        [ScriptParam("выручка в месяц")] double revenue,
        [ScriptParam("расходы в месяц")] double costs,
        [ScriptParam("средний рост выручки в месяц")] double growth = 0,
        [ScriptParam("разброс роста выручки")] double growth_sigma = 0,
        [ScriptParam("средний рост расходов в месяц")] double cost_growth = 0,
        [ScriptParam("разброс роста расходов")] double cost_sigma = 0,
        [ScriptParam("доля валовой маржи")] double margin = 0.8,
        [ScriptParam("горизонт в месяцах")] int horizon = 36,
        [ScriptParam("число имитаций")] int simulations = 2000)
    {
        Require(cash > 0, "econ.runway: денег на счетах должно быть больше нуля");
        Require(horizon > 0, "econ.runway: горизонт должен быть больше нуля");

        context.CountAllocation((long)Math.Max(1, simulations) * horizon);

        RunwayResult result = RunwaySimulator.Simulate(new RunwayInput
        {
            Cash = cash,
            MonthlyRevenue = revenue,
            RevenueGrowthMean = growth,
            RevenueGrowthVolatility = growth_sigma,
            GrossMarginRate = margin,
            MonthlyCosts = costs,
            CostGrowthMean = cost_growth,
            CostGrowthVolatility = cost_sigma,
            Horizon = horizon,
            Simulations = Math.Max(1, simulations),

            // Зерно берётся из прогона: две одинаковые имитации обязаны дать один ответ,
            // а собственное зерно библиотеки сделало бы результат независимым от options.seed.
            Seed = context.Seed,
        });

        return Record(
            ("месяцев", result.DeterministicRunwayMonths),
            ("месяцев_p10", result.CashOutP10),
            ("месяцев_p50", result.CashOutP50),
            ("месяцев_p90", result.CashOutP90),
            ("вероятность_выжить", result.SurvivalProbability),
            ("риск_6_месяцев", result.ProbabilityCashOutIn6),
            ("риск_12_месяцев", result.ProbabilityCashOutIn12),
            ("вероятность_выхода_в_ноль", result.ProbabilityBreakEven));
    }

    // --- проекты и инвестиции ---

    [ScriptFn("npv", "Чистая приведённая стоимость потока платежей",
        Example = "econ.npv(<-1000, 300, 400, 500>, rate: 0.1)")]
    public static double Npv(
        [ScriptParam("платежи по периодам: отток отрицателен")] Vector flows,
        [ScriptParam("ставка дисконтирования за период")] double rate) =>
        InvestmentCriteria.NetPresentValue(flows, rate);

    [ScriptFn("irr", "Внутренняя норма доходности", Example = "econ.irr(<-1000, 300, 400, 500>)")]
    public static double Irr(
        [ScriptParam("платежи по периодам")] Vector flows) =>
        InvestmentCriteria.InternalRateOfReturn(flows);

    [ScriptFn("mirr", "Модифицированная внутренняя норма доходности",
        Example = "econ.mirr(<-1000, 300, 400, 500>, finance: 0.1, reinvest: 0.08)")]
    public static double Mirr(
        [ScriptParam("платежи по периодам")] Vector flows,
        [ScriptParam("ставка привлечения")] double finance,
        [ScriptParam("ставка реинвестирования")] double reinvest) =>
        InvestmentCriteria.ModifiedInternalRateOfReturn(flows, finance, reinvest);

    /// <summary>
    /// Оценка проекта целиком.
    /// </summary>
    /// <remarks>
    /// Число смен знака отдаётся наружу не для красоты: при двух и более сменах внутренняя
    /// норма доходности имеет несколько корней, и одно возвращённое число перестаёт что-либо
    /// значить. Скрипт, который об этом не знает, хотя бы увидит признак в результате.
    /// </remarks>
    [ScriptFn("appraise", "Оценка проекта: NPV, IRR, MIRR, индекс прибыльности, окупаемость",
        Returns = "record",
        Example = "econ.appraise(<-5000, 1500, 2000, 2500, 1200>, rate: 0.12)")]
    public static ScriptRecord Appraise(
        [ScriptParam("платежи по периодам")] Vector flows,
        [ScriptParam("ставка дисконтирования за период")] double rate,
        [ScriptParam("ставка реинвестирования; меньше нуля — как ставка дисконтирования")] double reinvest = -1)
    {
        Require(flows.Count >= 2, "econ.appraise: нужно хотя бы два платежа");

        InvestmentAppraisal result = InvestmentCriteria.Appraise(flows, rate, reinvest);

        return Record(
            ("npv", result.NetPresentValue),
            ("irr", result.InternalRateOfReturn),
            ("mirr", result.ModifiedIrr),
            ("индекс_прибыльности", result.ProfitabilityIndex),
            ("окупаемость", result.PaybackPeriod),
            ("окупаемость_дисконт", result.DiscountedPayback),
            ("вложения", result.InitialInvestment),
            ("смен_знака", result.SignChanges),
            ("принимается", result.IsAccepted ? 1 : 0));
    }

    [ScriptFn("break_even", "Точка безубыточности и запас прочности", Returns = "record",
        Example = "econ.break_even(price: 1200, variable_cost: 700, fixed_costs: 2000000, volume: 5000)")]
    public static ScriptRecord BreakEvenPoint(
        [ScriptParam("цена единицы")] double price,
        [ScriptParam("переменные затраты на единицу")] double variable_cost,
        [ScriptParam("постоянные затраты за период")] double fixed_costs,
        [ScriptParam("фактический объём продаж")] double volume,
        [ScriptParam("проценты по долгу за период")] double interest = 0,
        [ScriptParam("целевая прибыль")] double target_profit = 0,
        [ScriptParam("ставка налога на прибыль")] double tax = 0.2)
    {
        Require(price > variable_cost, "econ.break_even: цена должна превышать переменные затраты");

        BreakEvenResult result = BreakEven.Analyze(
            price, variable_cost, fixed_costs, volume, interest, target_profit, tax);

        return Record(
            ("точка_единиц", result.BreakEvenUnits),
            ("точка_выручки", result.BreakEvenRevenue),
            ("целевой_объём", result.TargetUnits),
            ("вклад_на_единицу", result.ContributionPerUnit),
            ("маржа_вклада", result.ContributionMargin),
            ("запас_прочности", result.MarginOfSafety),
            ("операционный_рычаг", result.OperatingLeverage),
            ("финансовый_рычаг", result.FinancialLeverage),
            ("операционная_прибыль", result.OperatingProfit),
            ("чистая_прибыль", result.NetProfit));
    }

    [ScriptFn("loan", "График погашения кредита: переплата, эффективная ставка, платежи",
        Returns = "record",
        Example = "econ.loan(principal: 3000000, rate: 0.18, periods: 60)")]
    public static ScriptRecord Loan(
        IScriptContext context,
        [ScriptParam("сумма кредита")] double principal,
        [ScriptParam("годовая ставка долей единицы")] double rate,
        [ScriptParam("число периодов")] int periods,
        [ScriptParam("вид погашения: \"annuity\", \"differentiated\" либо \"interest_only\"")] string kind = "annuity",
        [ScriptParam("периодов в году")] int periods_per_year = 12,
        [ScriptParam("единовременная комиссия")] double fee = 0)
    {
        Require(principal > 0, "econ.loan: сумма кредита должна быть положительной");
        Require(periods >= 1, "econ.loan: срок должен быть не меньше периода");

        RepaymentType type = kind switch
        {
            "annuity" => RepaymentType.Annuity,
            "differentiated" => RepaymentType.Differentiated,
            "interest_only" => RepaymentType.InterestOnly,
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"econ.loan: неизвестный вид погашения '{kind}'",
                "известны: \"annuity\" — равный платёж, \"differentiated\" — равное тело, " +
                "\"interest_only\" — тело в конце"),
        };

        LoanScheduleResult result = LoanSchedule.Build(principal, rate, periods, type, periods_per_year, fee);

        context.CountAllocation(result.Payments.Count * 3L);

        var payments = new Vector(result.Payments.Count);
        var interest = new Vector(result.Payments.Count);
        var balance = new Vector(result.Payments.Count);

        for (int i = 0; i < result.Payments.Count; i++)
        {
            payments[i] = result.Payments[i].Payment;
            interest[i] = result.Payments[i].Interest;
            balance[i] = result.Payments[i].ClosingBalance;
        }

        return Record(
            ("платёж", ScriptValue.Vec(payments)),
            ("проценты", ScriptValue.Vec(interest)),
            ("остаток", ScriptValue.Vec(balance)),
            ("всего_процентов", ScriptValue.Num(result.TotalInterest)),
            ("всего_выплат", ScriptValue.Num(result.TotalPaid)),
            ("переплата", ScriptValue.Num(result.Overpayment)),
            ("эффективная_ставка", ScriptValue.Num(result.EffectiveAnnualRate)),
            ("полная_стоимость", ScriptValue.Num(result.AnnualPercentageRate)));
    }

    [ScriptFn("annuity", "Аннуитетный платёж по кредиту",
        Example = "econ.annuity(principal: 3000000, rate: 0.015, periods: 60)")]
    public static double Annuity(
        [ScriptParam("сумма кредита")] double principal,
        [ScriptParam("ставка за период")] double rate,
        [ScriptParam("число периодов")] int periods) =>
        LoanSchedule.AnnuityPayment(principal, rate, periods);

    [ScriptFn("effective_rate", "Эффективная годовая ставка при капитализации",
        Example = "econ.effective_rate(nominal: 0.18, per_year: 12)")]
    public static double EffectiveRate(
        [ScriptParam("номинальная годовая ставка")] double nominal,
        [ScriptParam("начислений в году")] int per_year) =>
        LoanSchedule.EffectiveRate(nominal, per_year);

    // --- риск и портфель ---

    [ScriptFn("var", "Историческая стоимость под риском по доходностям",
        Example = "econ.var(returns, confidence: 0.99)")]
    public static double ValueAtRiskOf(
        [ScriptParam("доходности за период")] Vector returns,
        [ScriptParam("уровень доверия от 0 до 1")] double confidence = 0.95)
    {
        Require(confidence is > 0 and < 1, "econ.var: уровень доверия лежит в (0, 1)");

        return ValueAtRisk.HistoricalVar(returns, confidence);
    }

    /// <summary>
    /// Ожидаемые потери за пределами порога.
    /// </summary>
    /// <remarks>
    /// Отвечает на вопрос, на который не отвечает <c>econ.var</c>: та говорит, какой убыток не
    /// будет превышен с заданной вероятностью, но молчит о том, насколько он окажется велик,
    /// когда всё же будет превышен.
    /// </remarks>
    [ScriptFn("cvar", "Ожидаемые потери в хвосте распределения",
        Example = "econ.cvar(returns, confidence: 0.99)")]
    public static double ConditionalValueAtRisk(
        [ScriptParam("доходности за период")] Vector returns,
        [ScriptParam("уровень доверия от 0 до 1")] double confidence = 0.95)
    {
        Require(confidence is > 0 and < 1, "econ.cvar: уровень доверия лежит в (0, 1)");

        return ValueAtRisk.HistoricalShortfall(returns, confidence);
    }

    [ScriptFn("drawdown", "Просадки по доходностям: величина, длительность, восстановление",
        Returns = "record", Example = "econ.drawdown(returns)")]
    public static ScriptRecord Drawdown(
        IScriptContext context,
        [ScriptParam("доходности за период")] Vector returns)
    {
        (Vector drawdowns, double max, int length, int recovery) = PortfolioMetrics.DrawdownProfile(returns);

        context.CountAllocation(drawdowns.Count);

        return Record(
            ("просадки", ScriptValue.Vec(drawdowns)),
            ("максимальная", ScriptValue.Num(max)),
            ("длительность", ScriptValue.Num(length)),
            ("восстановление", ScriptValue.Num(recovery)));
    }

    [ScriptFn("performance", "Качество портфеля: доходность, риск, Шарп, Сортино, просадка",
        Returns = "record",
        Example = "econ.performance(returns, periods_per_year: 12)")]
    public static ScriptRecord Performance(
        [ScriptParam("доходности за период")] Vector returns,
        [ScriptParam("безрисковая ставка за год")] double risk_free = 0,
        [ScriptParam("периодов в году")] int periods_per_year = 12,
        [ScriptParam("доходности эталона; пусто — без сравнения")] Vector? benchmark = null)
    {
        Require(returns.Count >= 6, "econ.performance: нужно хотя бы шесть наблюдений");

        PerformanceMetrics result = PortfolioMetrics.Compute(
            returns,
            benchmark is { Count: > 0 } ? benchmark : null,
            risk_free,
            periods_per_year);

        return Record(
            ("доходность", result.AnnualReturn),
            ("волатильность", result.Volatility),
            ("шарп", result.Sharpe),
            ("сортино", result.Sortino),
            ("калмар", result.Calmar),
            ("омега", result.Omega),
            ("максимальная_просадка", result.MaxDrawdown),
            ("бета", result.Beta),
            ("альфа", result.Alpha),
            ("ошибка_слежения", result.TrackingError));
    }

    [ScriptFn("portfolio_returns", "Доходности портфеля по весам и доходностям активов",
        Example = "econ.portfolio_returns(<0.6, 0.4>, assets)")]
    public static Vector PortfolioReturnsOf(
        IScriptContext context,
        [ScriptParam("веса активов")] Vector weights,
        [ScriptParam("матрица период × актив")] Matrix assets)
    {
        Require(weights.Count == assets.Width,
            $"econ.portfolio_returns: весов {weights.Count}, а активов в матрице {assets.Width}");

        context.CountAllocation(assets.Height);

        return PortfolioMetrics.PortfolioReturns(weights, assets);
    }

    // --- прогноз ---

    /// <summary>
    /// Прогноз ряда с доверительным интервалом.
    /// </summary>
    /// <remarks>
    /// Модель подбирается автоматически, и её имя возвращается вместе с прогнозом: без него
    /// нельзя понять, учтена ли сезонность, а по одному ряду чисел это не видно.
    /// </remarks>
    [ScriptFn("forecast", "Прогноз ряда экспоненциальным сглаживанием с интервалом", Returns = "record",
        Example = "econ.forecast(выручка, horizon: 6, season: 12)")]
    public static ScriptRecord Forecast(
        IScriptContext context,
        [ScriptParam("исторический ряд")] Vector series,
        [ScriptParam("на сколько периодов вперёд")] int horizon,
        [ScriptParam("длина сезона; 1 — без сезонности")] int season = 1,
        [ScriptParam("уровень доверия интервала")] double confidence = 0.9)
    {
        Require(horizon > 0, "econ.forecast: горизонт должен быть больше нуля");
        Require(series.Count > season, "econ.forecast: ряд короче сезона");

        ForecastResult result = ExponentialSmoothing.AutoFit(series, horizon, season, confidence);

        context.CountAllocation(horizon * 3L);

        return ForecastRecord(result);
    }

    [ScriptFn("theta", "Прогноз ряда методом тета: устойчив на коротких рядах", Returns = "record",
        Example = "econ.theta(выручка, horizon: 4)")]
    public static ScriptRecord Theta(
        IScriptContext context,
        [ScriptParam("исторический ряд")] Vector series,
        [ScriptParam("на сколько периодов вперёд")] int horizon,
        [ScriptParam("длина сезона; 1 — без сезонности")] int season = 1,
        [ScriptParam("уровень доверия интервала")] double confidence = 0.9)
    {
        Require(horizon > 0, "econ.theta: горизонт должен быть больше нуля");

        ForecastResult result = ThetaMethod.Fit(series, horizon, season, confidence);

        context.CountAllocation(horizon * 3L);

        return ForecastRecord(result);
    }

    // --- цены ---

    /// <summary>
    /// Эластичность спроса по цене.
    /// </summary>
    /// <remarks>
    /// Возвращается не только сама эластичность, но и её ошибка с доверительным интервалом:
    /// оценка «−1.4» по семи наблюдениям и по семи сотням — это разные утверждения, и решение
    /// о цене по ним принимается разное.
    /// </remarks>
    [ScriptFn("elasticity", "Эластичность спроса по цене с доверительным интервалом", Returns = "record",
        Example = "econ.elasticity(prices, quantities)")]
    public static ScriptRecord Elasticity(
        [ScriptParam("цены наблюдений")] Vector prices,
        [ScriptParam("объёмы продаж наблюдений")] Vector quantities)
    {
        Require(prices.Count == quantities.Count,
            $"econ.elasticity: цен {prices.Count}, а объёмов {quantities.Count}");
        Require(prices.Count >= 5, "econ.elasticity: нужно хотя бы пять наблюдений");

        var observations = new List<PriceObservation>(prices.Count);

        for (int i = 0; i < prices.Count; i++)
            observations.Add(new PriceObservation { Price = prices[i], Quantity = quantities[i] });

        ElasticityResult result = DemandElasticity.Estimate(observations);

        return Record(
            ("эластичность", result.Elasticity),
            ("ошибка", result.StandardError),
            ("t", result.TStatistic),
            ("p", result.PValue),
            ("интервал_низ", result.ConfidenceLow),
            ("интервал_верх", result.ConfidenceHigh),
            ("r2", result.RSquared),
            ("наблюдений", result.Observations));
    }

    [ScriptFn("demand_at", "Спрос при новой цене по известной эластичности",
        Example = "econ.demand_at(price: 1000, quantity: 500, elasticity: -1.4, new_price: 1100)")]
    public static double DemandAt(
        [ScriptParam("текущая цена")] double price,
        [ScriptParam("текущий объём")] double quantity,
        [ScriptParam("эластичность спроса")] double elasticity,
        [ScriptParam("новая цена")] double new_price) =>
        DemandElasticity.DemandAt(price, quantity, elasticity, new_price);

    // --- внутреннее ---

    private static ScriptRecord ForecastRecord(ForecastResult result) => Record(
        ("модель", ScriptValue.Str(result.Model)),
        ("прогноз", ScriptValue.Vec(result.PointForecast)),
        ("низ", ScriptValue.Vec(result.Lower)),
        ("верх", ScriptValue.Vec(result.Upper)),
        ("сигма", ScriptValue.Num(result.Sigma)),
        ("aic", ScriptValue.Num(result.Aic)),
        ("mase", ScriptValue.Num(result.InSampleMase)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ScriptError(DiagnosticCodes.BadOperand, message);
    }

    private static ScriptRecord Record(params (string Name, double Value)[] fields)
    {
        var built = new List<KeyValuePair<string, ScriptValue>>(fields.Length);

        foreach ((string name, double value) in fields)
            built.Add(new KeyValuePair<string, ScriptValue>(name, ScriptValue.Num(value)));

        return ScriptRecord.From(built);
    }

    private static ScriptRecord Record(params (string Name, ScriptValue Value)[] fields)
    {
        var built = new List<KeyValuePair<string, ScriptValue>>(fields.Length);

        foreach ((string name, ScriptValue value) in fields)
            built.Add(new KeyValuePair<string, ScriptValue>(name, value));

        return ScriptRecord.From(built);
    }
}

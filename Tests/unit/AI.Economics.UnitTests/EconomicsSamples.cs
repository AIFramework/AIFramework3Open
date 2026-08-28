using AI.DataStructs.Algebraic;
using AI.Economics.Credit;
using AI.Economics.Statements;
using AI.Statistics;

namespace AI.Economics.UnitTests;

/// <summary>
/// Синтетические данные для тестов кредитного риска и анализа отчётности.
/// Все генераторы детерминированы: зерно передаётся явно.
/// </summary>
internal static class EconomicsSamples
{
    /// <summary>Названия признаков скоринговой выборки.</summary>
    public static string[] ScoreVariables => ["доход", "срок_работы", "нагрузка"];

    /// <summary>Выборка заявок с известным исходом.</summary>
    /// <param name="count">Число заявок.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Матрица признаков и метки дефолта.</returns>
    public static (Matrix Values, List<bool> Defaults) Applications(int count = 2000, int seed = 7)
    {
        Random rng = RandomEngine.Create(seed);
        var values = new Matrix(count, 3);
        var defaults = new List<bool>(count);

        for (int i = 0; i < count; i++)
        {
            double income = Math.Exp(RandomEngine.NextGaussian(rng, 11, 0.5));
            double tenure = Math.Max(0, RandomEngine.NextGaussian(rng, 5, 3));
            double burden = Math.Clamp(RandomEngine.NextGaussian(rng, 0.35, 0.15), 0.01, 0.95);

            values[i, 0] = income;
            values[i, 1] = tenure;
            values[i, 2] = burden;

            double logit = -1.6 - (1.5 * (Math.Log(income) - 11)) - (0.22 * tenure) + (4.5 * burden);
            double probability = 1.0 / (1.0 + Math.Exp(-logit));

            defaults.Add(rng.NextDouble() < probability);
        }

        return (values, defaults);
    }

    /// <summary>Кредитный портфель для расчёта резерва по МСФО 9.</summary>
    /// <returns>Экспозиции с разными стадиями обесценения.</returns>
    public static List<CreditExposure> Portfolio() =>
    [
        new CreditExposure
        {
            Id = "K-001", Segment = "Розница", ExposureAtDefault = 1_200_000,
            ProbabilityOfDefault = 0.02, ProbabilityOfDefaultAtOrigination = 0.018,
            LossGivenDefault = 0.4, EffectiveInterestRate = 0.14, RemainingMonths = 36,
        },
        new CreditExposure
        {
            Id = "K-002", Segment = "Розница", ExposureAtDefault = 800_000,
            ProbabilityOfDefault = 0.09, ProbabilityOfDefaultAtOrigination = 0.02,
            LossGivenDefault = 0.45, EffectiveInterestRate = 0.16, RemainingMonths = 24,
        },
        new CreditExposure
        {
            Id = "K-003", Segment = "МСБ", ExposureAtDefault = 5_000_000,
            ProbabilityOfDefault = 0.05, ProbabilityOfDefaultAtOrigination = 0.045,
            LossGivenDefault = 0.5, EffectiveInterestRate = 0.18, RemainingMonths = 48,
            DaysPastDue = 45,
        },
        new CreditExposure
        {
            Id = "K-004", Segment = "МСБ", ExposureAtDefault = 2_500_000,
            ProbabilityOfDefault = 0.7, ProbabilityOfDefaultAtOrigination = 0.05,
            LossGivenDefault = 0.6, EffectiveInterestRate = 0.2, RemainingMonths = 12,
            DaysPastDue = 120, IsCreditImpaired = true,
        },
        new CreditExposure
        {
            Id = "K-005", Segment = "Корпоративный", ExposureAtDefault = 15_000_000,
            ProbabilityOfDefault = 0.012, ProbabilityOfDefaultAtOrigination = 0.011,
            LossGivenDefault = 0.35, EffectiveInterestRate = 0.11, RemainingMonths = 60,
        },
    ];

    /// <summary>Рейтинговая шкала с дефолтом в конце.</summary>
    public static string[] Ratings => ["AAA", "A", "BBB", "BB", "D"];

    /// <summary>Наблюдённые переходы рейтингов, порождённые известной матрицей.</summary>
    /// <param name="observations">Число наблюдений.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Список переходов.</returns>
    public static List<RatingTransition> Transitions(int observations = 4000, int seed = 11)
    {
        Random rng = RandomEngine.Create(seed);

        double[][] truth =
        [
            [0.90, 0.08, 0.015, 0.004, 0.001],
            [0.05, 0.85, 0.08, 0.015, 0.005],
            [0.01, 0.07, 0.83, 0.08, 0.010],
            [0.00, 0.02, 0.10, 0.82, 0.060],
            [0.00, 0.00, 0.00, 0.00, 1.000],
        ];

        var transitions = new List<RatingTransition>(observations);

        for (int i = 0; i < observations; i++)
        {
            int from = i % 4;
            double draw = rng.NextDouble();
            double cumulative = 0;
            int to = 4;

            for (int j = 0; j < 5; j++)
            {
                cumulative += truth[from][j];
                if (draw <= cumulative) { to = j; break; }
            }

            transitions.Add(new RatingTransition(Ratings[from], Ratings[to]));
        }

        return transitions;
    }

    /// <summary>История остатков по корзинам просрочки.</summary>
    /// <param name="periods">Число периодов.</param>
    /// <returns>Матрица «период x корзина».</returns>
    public static Matrix DelinquencyBalances(int periods = 12)
    {
        var buckets = RollRate.DefaultBuckets();
        var balances = new Matrix(periods, buckets.Count);
        double[] level = [100_000_000, 6_000_000, 2_400_000, 1_200_000, 700_000, 400_000];

        for (int t = 0; t < periods; t++)
            for (int b = 0; b < buckets.Count; b++)
                balances[t, b] = level[b] * (1 + (0.01 * ((t % 3) - 1)));

        return balances;
    }

    /// <summary>Винтажи выдач с разной зрелостью и трендом качества.</summary>
    /// <returns>Когорты в хронологическом порядке.</returns>
    public static List<VintageCohort> Vintages()
    {
        var cohorts = new List<VintageCohort>();
        int[] ages = [24, 21, 18, 15, 12, 9];

        for (int v = 0; v < ages.Length; v++)
        {
            double terminal = 0.05 + (0.004 * v);
            var curve = new List<double>(ages[v]);

            for (int age = 1; age <= ages[v]; age++)
                curve.Add(terminal * (1 - Math.Exp(-age / 8.0)));

            cohorts.Add(new VintageCohort($"2023-{v + 1:00}", 100_000_000 * (1 + (0.05 * v)), curve));
        }

        return cohorts;
    }

    /// <summary>Публичная компания для структурной модели кредитного риска.</summary>
    /// <returns>Входные данные модели Мертона.</returns>
    public static MertonInput PublicCompany() => new()
    {
        Company = "Публичная компания",
        EquityValue = 5_000_000_000,
        EquityVolatility = 0.35,
        ShortTermDebt = 1_200_000_000,
        LongTermDebt = 2_800_000_000,
        RiskFreeRate = 0.07,
        AssetDrift = 0.09,
        Horizon = 1,
    };

    /// <summary>Контрагент для коммерческого кредита.</summary>
    /// <param name="strong">Сильный или слабый профиль.</param>
    /// <returns>Профиль контрагента.</returns>
    public static CounterpartyProfile Counterparty(bool strong = true) => strong
        ? new CounterpartyProfile
        {
            Name = "Надёжный дистрибьютор",
            Revenue = 1_200_000_000, Ebitda = 150_000_000, Equity = 400_000_000,
            TotalDebt = 250_000_000, CurrentAssets = 500_000_000, CurrentLiabilities = 250_000_000,
            RevenueGrowth = 0.14, YearsInBusiness = 12, AveragePaymentDelayDays = 3,
            DisputeRate = 0.01, BuyerConcentration = 0.25, RequestedLimit = 40_000_000,
        }
        : new CounterpartyProfile
        {
            Name = "Проблемный поставщик",
            Revenue = 180_000_000, Ebitda = 4_000_000, Equity = 12_000_000,
            TotalDebt = 90_000_000, CurrentAssets = 70_000_000, CurrentLiabilities = 85_000_000,
            RevenueGrowth = -0.2, YearsInBusiness = 1.5, AveragePaymentDelayDays = 38,
            DisputeRate = 0.08, BuyerConcentration = 0.7, HasTaxArrears = true,
            RequestedLimit = 25_000_000,
        };

    /// <summary>Синтетическая финансовая отчётность внутренне согласованного вида.</summary>
    /// <param name="company">Название компании.</param>
    /// <param name="period">Период.</param>
    /// <param name="revenue">Выручка.</param>
    /// <param name="quality">Качество бизнеса от нуля до единицы.</param>
    /// <returns>Отчётность.</returns>
    public static FinancialStatement Statement(
        string company, string period, double revenue = 1_000_000_000, double quality = 0.7)
    {
        double q = Math.Clamp(quality, 0, 1);
        double r = revenue;

        double cogs = r * 0.6;
        double opex = r * (0.22 - (0.06 * q));
        double depreciation = r * 0.05;
        double operatingIncome = r - cogs - opex - depreciation;

        double currentAssets = r * 0.45;
        double ppe = r * 0.5;
        double intangibles = r * 0.05;
        double assets = currentAssets + ppe + intangibles;

        double currentLiabilities = r * (0.30 - (0.08 * q));
        double payables = r * 0.15;
        double shortTermDebt = currentLiabilities - payables;
        double longTermDebt = r * (0.35 - (0.20 * q));
        double liabilities = currentLiabilities + longTermDebt;
        double equity = assets - liabilities;

        double interest = (shortTermDebt + longTermDebt) * 0.12;
        double pretax = operatingIncome - interest;
        double tax = Math.Max(0, pretax * 0.2);
        double netIncome = pretax - tax;

        // Слабые компании показывают прибыль, не подтверждённую деньгами
        double accrualGap = r * 0.06 * (1 - q);
        double operatingCashFlow = netIncome + depreciation - accrualGap;

        return new FinancialStatement
        {
            Company = company,
            Period = period,
            TotalAssets = assets,
            CurrentAssets = currentAssets,
            Cash = r * 0.08,
            ShortTermInvestments = r * 0.02,
            AccountsReceivable = r * 0.18,
            Inventory = r * 0.15,
            PropertyPlantEquipment = ppe,
            IntangibleAssets = intangibles,
            TotalLiabilities = liabilities,
            CurrentLiabilities = currentLiabilities,
            AccountsPayable = payables,
            ShortTermDebt = shortTermDebt,
            LongTermDebt = longTermDebt,
            RetainedEarnings = equity * 0.6,
            Revenue = r,
            CostOfGoodsSold = cogs,
            OperatingExpenses = opex,
            Depreciation = depreciation,
            InterestExpense = interest,
            IncomeTax = tax,
            NetIncome = netIncome,
            OperatingCashFlow = operatingCashFlow,
            CapitalExpenditures = r * 0.06,
            DividendsPaid = Math.Max(0, netIncome * 0.2),
            MarketCapitalization = equity * (1 + (2 * q)),
        };
    }

    /// <summary>Пара отчётностей: предыдущий и текущий периоды.</summary>
    /// <param name="growth">Рост выручки за период.</param>
    /// <param name="quality">Качество текущего периода.</param>
    /// <param name="previousQuality">Качество предыдущего периода.</param>
    /// <returns>Отчётность предыдущего и текущего периодов.</returns>
    public static (FinancialStatement Previous, FinancialStatement Current) StatementPair(
        double growth = 0.15, double quality = 0.7, double previousQuality = 0.65)
    {
        FinancialStatement previous = Statement("Компания", "2023", 1_000_000_000, previousQuality);
        FinancialStatement current = Statement(
            "Компания", "2024", 1_000_000_000 * (1 + growth), quality);

        return (previous, current);
    }

    /// <summary>Обучающая выборка для предсказания банкротства.</summary>
    /// <param name="count">Число компаний.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Отчётность компаний с известным исходом.</returns>
    public static List<BankruptcyObservation> BankruptcySample(int count = 300, int seed = 21)
    {
        Random rng = RandomEngine.Create(seed);
        var sample = new List<BankruptcyObservation>(count);

        for (int i = 0; i < count; i++)
        {
            double quality = Math.Clamp(RandomEngine.NextGaussian(rng, 0.55, 0.25), 0.02, 0.98);
            double revenue = Math.Exp(RandomEngine.NextGaussian(rng, 20, 1.0));

            FinancialStatement statement = Statement($"Компания {i}", "2024", revenue, quality);
            double probability = 1.0 / (1.0 + Math.Exp(6 * (quality - 0.45)));

            sample.Add(new BankruptcyObservation(statement, rng.NextDouble() < probability));
        }

        return sample;
    }

    /// <summary>Суммы платежей, подчиняющиеся закону Бенфорда.</summary>
    /// <param name="count">Число платежей.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Суммы платежей.</returns>
    public static List<double> NaturalPayments(int count = 3000, int seed = 31)
    {
        Random rng = RandomEngine.Create(seed);
        var payments = new List<double>(count);

        for (int i = 0; i < count; i++)
            payments.Add(Math.Exp(RandomEngine.NextGaussian(rng, 10, 2.5)));

        return payments;
    }

    /// <summary>Суммы платежей с признаками ручного подбора.</summary>
    /// <param name="count">Число платежей.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Суммы платежей.</returns>
    public static List<double> FabricatedPayments(int count = 3000, int seed = 37)
    {
        Random rng = RandomEngine.Create(seed);
        var payments = new List<double>(count);

        for (int i = 0; i < count; i++)
            payments.Add(Math.Round(rng.NextDouble() * 900_000) + 100_000);

        return payments;
    }
}

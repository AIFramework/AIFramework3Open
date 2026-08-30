using System;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Clv;

/// <summary>Разбор портфеля пожизненной ценности.</summary>
public sealed partial record ClvPortfolio : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int worthless = Customers.Count(c => c.Clv < MeanClv * 0.05);
        double worthlessShare = Customers.Count > 0 ? (double)worthless / Customers.Count : 0;
        double discountCost = Customers.Sum(c => c.UndiscountedClv) is var raw && raw > 0
            ? 1 - (TotalClv / raw)
            : 0;

        return new InterpretationBuilder("Пожизненная ценность клиентской базы")
            .Summary($"Суммарная ценность базы {Fmt.Money(TotalClv)}, в среднем {Fmt.Money(MeanClv)} " +
                     $"на клиента. Верхние 10 % клиентов дают {Fmt.Pct(Top10PercentShare)} всей " +
                     $"ценности; средняя вероятность того, что клиент ещё активен, — " +
                     $"{Fmt.Pct(MeanProbabilityAlive)}.")
            .Metric("CLV базы", Fmt.Money(TotalClv), null, "дисконтированная маржа на горизонте")
            .Metric("Средний CLV", Fmt.Money(MeanClv), null, "на одного клиента")
            .Metric("Доля верхнего дециля", Fmt.Pct(Top10PercentShare), null,
                "чем выше, тем адреснее должен быть бюджет удержания",
                Top10PercentShare > 0.5 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Средняя P(активен)", Fmt.Pct(MeanProbabilityAlive), null,
                "по всему портфелю",
                MeanProbabilityAlive > 0.5 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Клиентов без ценности", worthless, null,
                $"{Fmt.Pct(worthlessShare)} базы почти наверняка не вернётся",
                worthlessShare > 0.5 ? MetricQuality.Warning : MetricQuality.Neutral, 0)
            .Metric("Цена дисконтирования", Fmt.Pct(discountCost), null,
                "насколько дисконт уменьшил ценность против недисконтированной оценки")
            .FindingIf(Top10PercentShare > 0.4,
                $"Ценность сильно сконцентрирована: {Fmt.Pct(Top10PercentShare)} у одного дециля. " +
                "Равномерная скидка всей базе оплачивается прибылью этой небольшой группы.")
            .FindingIf(worthlessShare > 0.4,
                $"{Fmt.Pct(worthlessShare)} базы имеет околонулевую ценность. Это не повод " +
                "их удалять, но повод исключить из платных коммуникаций.")
            .Finding("Ценность собрана из трёх независимо оценённых частей: вероятности " +
                     "активности, ожидаемого числа покупок и ожидаемого чека. Ошибка в любой " +
                     "из них переносится в итог мультипликативно.")
            .WarningIf(Customers.Count < 200,
                $"Клиентов всего {Customers.Count}: параметры вероятностных моделей на такой " +
                "выборке оценены грубо.")
            .Warning("Оценка отвечает на вопрос «сколько принесёт база, если ничего не делать». " +
                     "Она не учитывает будущие маркетинговые воздействия и служит базой сравнения, " +
                     "а не планом.")
            .Recommendation("Стройте программу удержания по верхним децилям ценности, " +
                            "а не по вероятности оттока: удерживать дёшево уходящего клиента " +
                            "с нулевым CLV невыгодно.")
            .Build();
    }
}

/// <summary>Разбор обученной модели BG/NBD.</summary>
public sealed partial class BgNbdModel : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double meanRate = Alpha > 0 ? R / Alpha : double.NaN;
        double meanDropout = A + B > 0 ? A / (A + B) : double.NaN;
        double heterogeneity = R > 0 ? 1 / Math.Sqrt(R) : double.NaN;

        return new InterpretationBuilder("Модель покупок BG/NBD")
            .Summary($"Средняя интенсивность покупок активного клиента — {Fmt.Num(meanRate, 3)} " +
                     $"за период, вероятность уйти после очередной покупки — {Fmt.Pct(meanDropout)}. " +
                     $"Разброс интенсивностей по популяции {Fmt.Num(heterogeneity)}: " +
                     (heterogeneity > 1
                         ? "клиенты сильно различаются, средние показатели по базе вводят в заблуждение."
                         : "клиенты относительно однородны."))
            .Metric("r", R, null, "форма гаммы интенсивности покупок", MetricQuality.Unknown, 4)
            .Metric("alpha", Alpha, null, "масштаб гаммы интенсивности покупок", MetricQuality.Unknown, 4)
            .Metric("a", A, null, "первый параметр беты вероятности ухода", MetricQuality.Unknown, 4)
            .Metric("b", B, null, "второй параметр беты вероятности ухода", MetricQuality.Unknown, 4)
            .Metric("Покупок за период", meanRate, null, "у среднего активного клиента")
            .Metric("Вероятность ухода", Fmt.Pct(meanDropout), null, "после очередной покупки")
            .Metric("lnL", LogLikelihood, null, "логарифм правдоподобия", MetricQuality.Unknown, 1)
            .Metric("Клиентов", SampleSize, null, null, MetricQuality.Unknown, 0)
            .Finding("Модель различает «замолчал и ушёл» и «просто редко покупает» по частоте " +
                     "прошлых покупок. У частого покупателя месяц молчания почти доказывает уход, " +
                     "у редкого не значит ничего.")
            .FindingIf(heterogeneity > 1,
                "Высокая неоднородность базы: любые средние по всем клиентам метрики " +
                "(средний чек, средняя частота) описывают несуществующего клиента.")
            .WarningIf(A <= 1,
                $"Параметр a равен {Fmt.Num(A, 3)}, что не превышает единицу. Формула условного " +
                "ожидания числа покупок в этой области численно неустойчива, результаты " +
                "ограничиваются нулём снизу.")
            .WarningIf(SampleSize < 300,
                $"Выборка {SampleSize} клиентов: правдоподобие плоское, оценки параметров " +
                "имеют широкие доверительные границы.")
            .Warning("Время должно отсчитываться от первой покупки клиента, а частота — " +
                     "считать только повторные покупки. Нарушение соглашения смещает все прогнозы.")
            .Recommendation("Сравните с Pareto/NBD: если оценки заметно расходятся, " +
                            "механизм ухода в вашей категории ближе к непрерывному.")
            .Build();
    }
}

/// <summary>Разбор обученной модели Gamma-Gamma.</summary>
public sealed partial class GammaGammaModel : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double dispersion = P > 0 ? 1 / Math.Sqrt(P) : double.NaN;

        return new InterpretationBuilder("Модель среднего чека Gamma-Gamma")
            .Summary($"Средний чек по популяции — {Fmt.Money(PopulationMean)}. Коэффициент вариации " +
                     $"чека внутри клиента {Fmt.Num(dispersion)}: " +
                     (dispersion > 1
                         ? "чеки одного клиента сильно разбросаны, поэтому наблюдённое среднее " +
                           "по нескольким покупкам почти не несёт информации."
                         : "чеки клиента устойчивы, наблюдённому среднему можно доверять " +
                           "уже после нескольких покупок."))
            .Metric("Средний чек популяции", Fmt.Money(PopulationMean), null, "p * gamma / (q - 1)")
            .Metric("p", P, null, "форма распределения чека внутри клиента", MetricQuality.Unknown, 4)
            .Metric("q", Q, null, "форма распределения масштаба по популяции",
                Q > 1 ? MetricQuality.Good : MetricQuality.Critical, 4)
            .Metric("gamma", Gamma, null, "масштаб распределения по популяции", MetricQuality.Unknown, 2)
            .Metric("Клиентов в подгонке", SampleSize, null,
                "только с повторными покупками", MetricQuality.Unknown, 0)
            .Finding("Модель смешивает индивидуальное среднее с популяционным, причём вес " +
                     "индивидуального растёт с числом покупок. Клиент с одной дорогой покупкой " +
                     "получает оценку, близкую к среднему по базе, а не к своему чеку.")
            .WarningIf(Q <= 1,
                $"Параметр q равен {Fmt.Num(Q, 3)} и не превышает единицу: популяционное " +
                "среднее не существует. Обычно это признак слишком короткой истории " +
                "или загрязнённых данных.")
            .WarningIf(SampleSize < 200,
                $"В подгонке участвовало {SampleSize} клиентов: параметры оценены грубо.")
            .Warning("Модель предполагает независимость чека и частоты покупок. Проверьте " +
                     "их корреляцию — допустимой обычно считают величину до 0,1.")
            .Recommendation("Отфильтруйте возвраты и нулевые чеки до подгонки: плотность " +
                            "определена только на положительной полуоси.")
            .Build();
    }
}

/// <summary>Разбор обученной модели Pareto/NBD.</summary>
public sealed partial class ParetoNbdModel : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double meanRate = Alpha > 0 ? R / Alpha : double.NaN;
        double meanLifetime = S > 1 ? Beta / (S - 1) : double.NaN;

        return new InterpretationBuilder("Модель покупок Pareto/NBD")
            .Summary($"Средняя интенсивность покупок — {Fmt.Num(meanRate, 3)} за период. " +
                     (double.IsNaN(meanLifetime)
                         ? "Средняя длительность жизни клиента не определена: параметр s не превышает единицу."
                         : $"Средняя длительность активной жизни клиента — {Fmt.Num(meanLifetime, 1)} периодов."))
            .Metric("r", R, null, "форма гаммы интенсивности покупок", MetricQuality.Unknown, 4)
            .Metric("alpha", Alpha, null, "масштаб гаммы интенсивности покупок", MetricQuality.Unknown, 4)
            .Metric("s", S, null, "форма гаммы интенсивности ухода",
                S > 1 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("beta", Beta, null, "масштаб гаммы интенсивности ухода", MetricQuality.Unknown, 4)
            .Metric("lnL", LogLikelihood, null, "логарифм правдоподобия", MetricQuality.Unknown, 1)
            .Metric("Клиентов", SampleSize, null, null, MetricQuality.Unknown, 0)
            .Finding("В отличие от BG/NBD уход возможен в любой момент, а не только после покупки. " +
                     "Модель поэтому консервативнее оценивает шансы вернуть надолго замолчавшего клиента.")
            .WarningIf(S <= 1,
                $"Параметр s равен {Fmt.Num(S, 3)}: прогноз числа будущих покупок не определён " +
                "и возвращается нулём. Это признак того, что в выборке слишком мало наблюдаемых уходов.")
            .WarningIf(SampleSize < 300,
                $"Выборка {SampleSize} клиентов: оптимизация правдоподобия с гипергеометрической " +
                "функцией на малых данных капризна, проверьте устойчивость к зерну генератора.")
            .Warning("Оценка требовательнее к данным, чем BG/NBD. Сравнивайте логарифмы " +
                     "правдоподобия обеих моделей на одной выборке, прежде чем выбирать.")
            .Build();
    }
}

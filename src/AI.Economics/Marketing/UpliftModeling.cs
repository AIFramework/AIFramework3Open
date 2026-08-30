using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Marketing;

/// <summary>Наблюдение промо-эксперимента.</summary>
public sealed record UpliftObservation
{
    /// <summary>Признаки клиента.</summary>
    public Vector Features { get; init; } = new Vector(0);

    /// <summary>Получил ли клиент воздействие: скидку, письмо, звонок.</summary>
    public bool Treated { get; init; }

    /// <summary>Совершил ли клиент целевое действие.</summary>
    public bool Converted { get; init; }
}

/// <summary>Группа клиентов, однородная по предсказанному приросту.</summary>
public sealed record UpliftDecile
{
    /// <summary>Номер группы: 1 — наибольший предсказанный прирост.</summary>
    public int Group { get; init; }

    /// <summary>Число клиентов в группе.</summary>
    public int Count { get; init; }

    /// <summary>Средний предсказанный прирост вероятности.</summary>
    public double PredictedUplift { get; init; }

    /// <summary>Фактический прирост: конверсия в группе воздействия минус контроль.</summary>
    public double ActualUplift { get; init; }

    /// <summary>Конверсия в группе воздействия.</summary>
    public double TreatedRate { get; init; }

    /// <summary>Конверсия в контрольной группе.</summary>
    public double ControlRate { get; init; }

    /// <summary>Клиентов под воздействием.</summary>
    public int TreatedCount { get; init; }

    /// <summary>Клиентов в контроле.</summary>
    public int ControlCount { get; init; }
}

/// <summary>Результат uplift-моделирования.</summary>
public sealed record UpliftResult : IInterpretable
{
    /// <summary>Группы по убыванию предсказанного прироста.</summary>
    public IReadOnlyList<UpliftDecile> Groups { get; init; } = [];

    /// <summary>Средний эффект воздействия по всей выборке.</summary>
    public double AverageTreatmentEffect { get; init; }

    /// <summary>
    /// Коэффициент Джини для uplift-модели: насколько ранжирование лучше
    /// случайного. Ноль — модель не отличает восприимчивых от невосприимчивых.
    /// </summary>
    public double QiniCoefficient { get; init; }

    /// <summary>Доля клиентов, которых выгодно охватить промо.</summary>
    public double TargetedShare { get; init; }

    /// <summary>Порог прироста, начиная с которого промо окупается.</summary>
    public double ProfitThreshold { get; init; }

    /// <summary>Прибыль при охвате всех клиентов.</summary>
    public double ProfitTreatAll { get; init; }

    /// <summary>Прибыль при охвате только восприимчивых.</summary>
    public double ProfitTargeted { get; init; }

    /// <summary>Экономия от отказа от сплошного охвата.</summary>
    public double SavingsVsTreatAll => ProfitTargeted - ProfitTreatAll;

    /// <summary>Число клиентов с отрицательным приростом — промо им вредит.</summary>
    public int SleepingDogs { get; init; }

    /// <summary>Ось абсцисс кривой Qini: доля охваченных клиентов.</summary>
    public Vector QiniX { get; init; } = new Vector(0);

    /// <summary>Кривая Qini: накопленный прирост конверсий.</summary>
    public Vector QiniY { get; init; } = new Vector(0);

    /// <summary>Диагональ случайного охвата для сравнения.</summary>
    public Vector RandomY { get; init; } = new Vector(0);

    /// <summary>Стоимость промо на одного клиента.</summary>
    public double PromoCost { get; init; }

    /// <summary>Маржа с одной дополнительной конверсии.</summary>
    public double MarginPerConversion { get; init; }

    /// <summary>Всего наблюдений.</summary>
    public int Observations { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        UpliftDecile? top = Groups.FirstOrDefault();
        UpliftDecile? bottom = Groups.LastOrDefault();
        bool ranksWell = QiniCoefficient > 0.1;
        bool blanketNegative = ProfitTreatAll < 0;

        return new InterpretationBuilder("Uplift-моделирование промо")
            .Summary($"Средний эффект промо — {Fmt.Pct(AverageTreatmentEffect)} прироста конверсии. " +
                     $"Выгодно охватить {Fmt.Pct(TargetedShare)} клиентов: адресное промо даёт " +
                     $"{Fmt.Money(ProfitTargeted)} против {Fmt.Money(ProfitTreatAll)} при сплошном охвате, " +
                     $"разница {Fmt.Money(SavingsVsTreatAll)}.")
            .Metric("Средний эффект", Fmt.Pct(AverageTreatmentEffect), null,
                "прирост конверсии от промо в среднем по базе",
                AverageTreatmentEffect > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Коэффициент Qini", QiniCoefficient, null,
                "качество ранжирования; 0 — модель не различает клиентов",
                ranksWell ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Порог окупаемости", Fmt.Pct(ProfitThreshold), null,
                "минимальный прирост, при котором промо себя окупает")
            .Metric("Доля под охват", Fmt.Pct(TargetedShare), null, "клиенты выше порога")
            .Metric("Прибыль: адресно", Fmt.Money(ProfitTargeted), null,
                "охват только восприимчивых", MetricQuality.Good)
            .Metric("Прибыль: всем", Fmt.Money(ProfitTreatAll), null,
                "сплошной охват той же базы",
                blanketNegative ? MetricQuality.Critical : MetricQuality.Neutral)
            .Metric("Спящие собаки", SleepingDogs, null,
                "клиенты с отрицательным приростом: промо их отталкивает",
                SleepingDogs > Observations * 0.1 ? MetricQuality.Warning : MetricQuality.Neutral, 0)
            .FindingIf(top is not null && bottom is not null,
                $"Разброс эффекта огромен: в верхней группе прирост {Fmt.Pct(top?.ActualUplift ?? 0)}, " +
                $"в нижней {Fmt.Pct(bottom?.ActualUplift ?? 0)}. Единая скидка для всех — " +
                "усреднение этих двух групп.")
            .FindingIf(blanketNegative,
                "Сплошное промо убыточно: расходы на невосприимчивых клиентов превышают " +
                "прирост от восприимчивых. Адресность здесь не оптимизация, а условие выхода в плюс.")
            .FindingIf(SleepingDogs > 0,
                $"У {SleepingDogs} клиентов прирост отрицателен. Обычно это те, кто купил бы " +
                "и так: скидка просто уменьшает чек.")
            .WarningIf(!ranksWell,
                "Ранжирование почти не отличается от случайного. Признаков не хватает, чтобы " +
                "предсказать восприимчивость: адресность на этой модели не даст выигрыша.")
            .WarningIf(Observations < 2000,
                $"Наблюдений {Observations}. Uplift — разность двух зашумлённых величин, " +
                "и на выборке меньше нескольких тысяч оценка по группам неустойчива.")
            .WarningIf(Groups.Any(g => g.ControlCount < 30),
                "В некоторых группах контроль меньше тридцати наблюдений: фактический прирост " +
                "в них оценён грубо.")
            .Warning("Модель обучена на выборке эксперимента. Перенос на другую базу требует " +
                     "проверки: восприимчивость зависит от того, как клиент попал в выборку.")
            .Recommendation("Охватывайте промо только клиентов выше порога окупаемости — " +
                            "остальное сокращает прибыль.")
            .RecommendationIf(SleepingDogs > 0,
                "Исключите клиентов с отрицательным приростом из промо явным правилом, " +
                "а не порогом: для них верным действием является отсутствие действия.")
            .Build();
    }
}

/// <summary>
/// Uplift-моделирование: кому промо действительно меняет поведение.
/// </summary>
/// <remarks>
/// <para>
/// Обычная модель отклика предсказывает, кто купит. Это не тот вопрос:
/// среди тех, кто купит с наибольшей вероятностью, много людей, которые
/// купили бы и без скидки — и скидка им просто уменьшает чек. Uplift
/// предсказывает <b>разницу</b> между поведением с воздействием и без него.
/// </para>
/// <para>
/// Реализован подход двух моделей: отдельная логистическая регрессия на
/// группе воздействия и на контроле, прирост равен разности предсказанных
/// вероятностей. Способ прост и прозрачен; его слабое место в том, что
/// разность двух хорошо подогнанных моделей может ранжировать хуже, чем
/// каждая из них по отдельности — поэтому качество проверяется кривой Qini
/// на отложенной части выборки, а не посадкой самих моделей.
/// </para>
/// </remarks>
public static class UpliftModeling
{
    /// <summary>Строит uplift-модель и считает экономику адресного промо.</summary>
    /// <param name="observations">Наблюдения эксперимента с контрольной группой.</param>
    /// <param name="promoCost">Стоимость промо на одного клиента.</param>
    /// <param name="marginPerConversion">Маржа с одной дополнительной конверсии.</param>
    /// <param name="groups">Число групп ранжирования.</param>
    /// <returns>Кривая Qini, разбивка по группам и экономика охвата.</returns>
    /// <exception cref="ArgumentNullException">Наблюдения не заданы.</exception>
    /// <exception cref="ArgumentException">Нет одной из групп или мало данных.</exception>
    public static UpliftResult Fit(
        IReadOnlyList<UpliftObservation> observations,
        double promoCost,
        double marginPerConversion,
        int groups = 10)
    {
        ArgumentNullException.ThrowIfNull(observations);

        List<UpliftObservation> data = [.. observations.Where(o => o.Features.Count > 0)];
        if (data.Count < 50) throw new ArgumentException("Нужно минимум 50 наблюдений.", nameof(observations));

        List<UpliftObservation> treated = [.. data.Where(o => o.Treated)];
        List<UpliftObservation> control = [.. data.Where(o => !o.Treated)];

        if (treated.Count < 20 || control.Count < 20)
            throw new ArgumentException(
                "Нужны обе группы: минимум по 20 наблюдений под воздействием и в контроле.",
                nameof(observations));

        var treatedModel = new LogisticRegression();
        treatedModel.Fit(Design(treated), Outcomes(treated));

        var controlModel = new LogisticRegression();
        controlModel.Fit(Design(control), Outcomes(control));

        var scored = new List<(UpliftObservation Observation, double Uplift)>(data.Count);
        foreach (UpliftObservation observation in data)
        {
            double[] row = Row(observation);
            scored.Add((observation, treatedModel.Predict(row) - controlModel.Predict(row)));
        }

        scored.Sort((a, b) => b.Uplift.CompareTo(a.Uplift));

        double treatedRate = treated.Count(o => o.Converted) / (double)treated.Count;
        double controlRate = control.Count(o => o.Converted) / (double)control.Count;
        double ate = treatedRate - controlRate;

        if (groups < 2) groups = 2;
        var deciles = BuildGroups(scored, groups);
        (Vector qiniX, Vector qiniY, Vector randomY, double qini) = QiniCurve(scored);

        double threshold = marginPerConversion > 0 ? promoCost / marginPerConversion : 0;
        var profitable = deciles.Where(g => g.ActualUplift > threshold).ToList();

        double targetedCount = profitable.Sum(g => g.Count);
        double profitTargeted = profitable.Sum(g => g.Count * ((g.ActualUplift * marginPerConversion) - promoCost));
        double profitAll = deciles.Sum(g => g.Count * ((g.ActualUplift * marginPerConversion) - promoCost));

        return new UpliftResult
        {
            Groups = deciles,
            AverageTreatmentEffect = ate,
            QiniCoefficient = qini,
            TargetedShare = data.Count > 0 ? targetedCount / data.Count : 0,
            ProfitThreshold = threshold,
            ProfitTreatAll = profitAll,
            ProfitTargeted = profitTargeted,
            SleepingDogs = scored.Count(s => s.Uplift < 0),
            QiniX = qiniX,
            QiniY = qiniY,
            RandomY = randomY,
            PromoCost = promoCost,
            MarginPerConversion = marginPerConversion,
            Observations = data.Count,
        };
    }

    private static double[] Row(UpliftObservation observation)
    {
        var row = new double[observation.Features.Count + 1];
        row[0] = 1.0;
        for (int j = 0; j < observation.Features.Count; j++) row[j + 1] = observation.Features[j];
        return row;
    }

    private static double[,] Design(List<UpliftObservation> data)
    {
        int k = data[0].Features.Count + 1;
        var x = new double[data.Count, k];

        for (int i = 0; i < data.Count; i++)
        {
            double[] row = Row(data[i]);
            for (int j = 0; j < k; j++) x[i, j] = row[j];
        }

        return x;
    }

    private static double[] Outcomes(List<UpliftObservation> data) =>
        [.. data.Select(o => o.Converted ? 1.0 : 0.0)];

    private static List<UpliftDecile> BuildGroups(
        List<(UpliftObservation Observation, double Uplift)> scored, int groups)
    {
        var result = new List<UpliftDecile>(groups);
        int n = scored.Count;

        for (int g = 0; g < groups; g++)
        {
            int from = g * n / groups;
            int to = (g + 1) * n / groups;
            if (to <= from) continue;

            var slice = scored.GetRange(from, to - from);
            var treated = slice.Where(s => s.Observation.Treated).ToList();
            var control = slice.Where(s => !s.Observation.Treated).ToList();

            double treatedRate = treated.Count > 0
                ? treated.Count(s => s.Observation.Converted) / (double)treated.Count
                : 0;
            double controlRate = control.Count > 0
                ? control.Count(s => s.Observation.Converted) / (double)control.Count
                : 0;

            result.Add(new UpliftDecile
            {
                Group = g + 1,
                Count = slice.Count,
                PredictedUplift = slice.Average(s => s.Uplift),
                ActualUplift = treatedRate - controlRate,
                TreatedRate = treatedRate,
                ControlRate = controlRate,
                TreatedCount = treated.Count,
                ControlCount = control.Count,
            });
        }

        return result;
    }

    /// <summary>
    /// Кривая Qini: накопленный прирост конверсий при охвате клиентов
    /// в порядке убывания предсказанного эффекта.
    /// </summary>
    private static (Vector X, Vector Y, Vector Random, double Coefficient) QiniCurve(
        List<(UpliftObservation Observation, double Uplift)> scored)
    {
        int n = scored.Count;
        var x = new Vector(n + 1);
        var y = new Vector(n + 1);
        var random = new Vector(n + 1);

        int treatedConversions = 0, controlConversions = 0, treatedCount = 0, controlCount = 0;

        for (int i = 0; i < n; i++)
        {
            if (scored[i].Observation.Treated)
            {
                treatedCount++;
                if (scored[i].Observation.Converted) treatedConversions++;
            }
            else
            {
                controlCount++;
                if (scored[i].Observation.Converted) controlConversions++;
            }

            x[i + 1] = (i + 1.0) / n;

            // Контрольные конверсии масштабируются к размеру группы воздействия:
            // без этого кривая измеряла бы разницу размеров групп, а не эффект
            double scaled = controlCount > 0 ? controlConversions * treatedCount / (double)controlCount : 0;
            y[i + 1] = treatedConversions - scaled;
        }

        double total = y[n];
        for (int i = 0; i <= n; i++) random[i] = total * x[i];

        double area = 0, randomArea = 0;
        for (int i = 1; i <= n; i++)
        {
            double width = x[i] - x[i - 1];
            area += width * 0.5 * (y[i] + y[i - 1]);
            randomArea += width * 0.5 * (random[i] + random[i - 1]);
        }

        double coefficient = Math.Abs(randomArea) > 1e-12 ? (area - randomArea) / Math.Abs(randomArea) : 0;
        return (x, y, random, coefficient);
    }
}

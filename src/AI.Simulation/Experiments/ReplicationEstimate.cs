using AI.Insights;

namespace AI.Simulation.Experiments;

/// <summary>
/// Оценка показателя по серии независимых прогонов
/// </summary>
/// <param name="Name">Название показателя</param>
/// <param name="Observations">Значение показателя в каждом прогоне</param>
/// <param name="Mean">Среднее по прогонам</param>
/// <param name="StandardDeviation">Выборочное стандартное отклонение между прогонами</param>
/// <param name="Lower">Нижняя граница доверительного интервала</param>
/// <param name="Upper">Верхняя граница доверительного интервала</param>
/// <param name="Confidence">Доверительная вероятность</param>
public sealed record ReplicationEstimate(
    string Name,
    IReadOnlyList<double> Observations,
    double Mean,
    double StandardDeviation,
    double Lower,
    double Upper,
    double Confidence) : IInterpretable
{
    /// <summary>Число прогонов</summary>
    public int Replications => Observations.Count;

    /// <summary>Полуширина доверительного интервала</summary>
    public double HalfWidth => (Upper - Lower) / 2;

    /// <summary>Относительная точность: полуширина, делённая на модуль среднего</summary>
    public double RelativePrecision => Mean == 0 ? double.PositiveInfinity : HalfWidth / Math.Abs(Mean);

    /// <summary>Попадает ли значение в доверительный интервал</summary>
    /// <param name="value">Значение</param>
    public bool Contains(double value) => value >= Lower && value <= Upper;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool precise = RelativePrecision <= 0.05;
        bool few = Replications < 10;

        return new InterpretationBuilder($"Серия прогонов: {Name}")
            .Summary($"По {Replications} независимым прогонам среднее {Fmt.Num(Mean, 4)}; "
                + $"доверительный интервал с вероятностью {Fmt.Pct(Confidence, 0)}: "
                + $"[{Fmt.Num(Lower, 4)}; {Fmt.Num(Upper, 4)}].")
            .Metric("Среднее", Fmt.Num(Mean, 5), null, "по прогонам")
            .Metric("Полуширина", Fmt.Num(HalfWidth, 5), null, "половина доверительного интервала")
            .Metric("Относительная точность", Fmt.Pct(RelativePrecision), null, "полуширина к среднему",
                precise ? MetricQuality.Good : RelativePrecision <= 0.15 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Прогонов", Replications, null, "независимых, с разными зёрнами", MetricQuality.Unknown, 0)
            .Metric("Разброс между прогонами", Fmt.Num(StandardDeviation, 5), null, "выборочное стандартное отклонение")
            .Finding("Интервал построен по средним отдельных прогонов, а не по наблюдениям внутри прогона. "
                + "Соседние заявки одного прогона зависимы — очередь, выросшая у одной, достаётся и следующей, — "
                + "и интервал по ним вышел бы в разы уже истинного.")
            .FindingIf(!precise,
                $"Точность {Fmt.Pct(RelativePrecision)} хуже пяти процентов. Полуширина убывает как 1/√n: "
                + "чтобы сузить интервал вдвое, прогонов нужно вчетверо больше.")
            .WarningIf(few,
                "Прогонов меньше десяти: t-интервал опирается на нормальность средних по прогонам, "
                + "а на такой выборке это допущение почти не проверить.")
            .Warning("Интервал учитывает случайную погрешность, но не смещение модели. Если начальный участок "
                + "прогона не отброшен, среднее смещено к пустой системе, и никакое число прогонов этого не исправит.")
            .Build();
    }
}

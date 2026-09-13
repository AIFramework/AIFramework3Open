using AI.Insights;
using AI.Psychology.Internal;
using AI.Statistics;

namespace AI.Psychology.Psychophysics;

/// <summary>Поправка долей попаданий и ложных тревог, равных нулю или единице</summary>
public enum RateCorrection
{
    /// <summary>
    /// Лог-линейная поправка Хаутуса (1995): к числу ответов «да» добавляется 0,5, к числу проб — 1,
    /// во всех ячейках. Смещение оценки d′ меньше, чем у замены крайних долей
    /// </summary>
    LogLinear,

    /// <summary>Замена 0 на 1/(2N) и 1 на 1 − 1/(2N) только у крайних долей (Макмиллан и Крилман)</summary>
    HalfCount,

    /// <summary>Без поправки: крайняя доля даёт бесконечную d′</summary>
    None
}

/// <summary>Показатели теории обнаружения сигнала для одного наблюдателя</summary>
public sealed class SignalDetectionResult : IInterpretable
{
    internal SignalDetectionResult(double hitRate, double falseAlarmRate, RateCorrection correction, bool corrected)
    {
        HitRate = hitRate;
        FalseAlarmRate = falseAlarmRate;
        Correction = correction;
        Corrected = corrected;

        double zHit = Z(hitRate);
        double zFalse = Z(falseAlarmRate);

        Sensitivity = zHit - zFalse;
        Criterion = -(zHit + zFalse) / 2;
        LogBeta = double.IsFinite(Sensitivity) && double.IsFinite(Criterion) ? Criterion * Sensitivity : double.NaN;
        AreaUnderCurve = double.IsNaN(Sensitivity) ? double.NaN : StatInference.NormalCdf(Sensitivity / Math.Sqrt(2));
        APrime = NonparametricSensitivity(hitRate, falseAlarmRate);
    }

    /// <summary>Доля попаданий: «да» при сигнале</summary>
    public double HitRate { get; }

    /// <summary>Доля ложных тревог: «да» при шуме</summary>
    public double FalseAlarmRate { get; }

    /// <summary>Чувствительность d′ = z(H) − z(F): расстояние между распределениями сигнала и шума в СКО</summary>
    public double Sensitivity { get; }

    /// <summary>Критерий c = −(z(H) + z(F))/2: положительный — осторожный наблюдатель, отрицательный — склонный говорить «да»</summary>
    public double Criterion { get; }

    /// <summary>Натуральный логарифм отношения правдоподобия в точке критерия: ln β = c·d′</summary>
    public double LogBeta { get; }

    /// <summary>Площадь под кривой ROC: Φ(d′/√2) — доля верных ответов в задаче двух альтернатив</summary>
    public double AreaUnderCurve { get; }

    /// <summary>Непараметрическая чувствительность A′ (Поллак и Норман, формула Гриера)</summary>
    public double APrime { get; }

    /// <summary>Какая поправка крайних долей выбрана</summary>
    public RateCorrection Correction { get; }

    /// <summary>Изменила ли поправка хотя бы одну долю</summary>
    public bool Corrected { get; }

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Обнаружение сигнала")
            .Summary(double.IsFinite(Sensitivity)
                ? $"Чувствительность d′ = {Fmt.Num(Sensitivity, 2)}, критерий c = {Fmt.Num(Criterion, 2)}: "
                  + (Math.Abs(Criterion) < 0.1 ? "наблюдатель не смещён в сторону «да» или «нет»."
                     : Criterion > 0 ? "наблюдатель осторожен и чаще отвечает «нет»."
                     : "наблюдатель склонен отвечать «да».")
                : "Доля попаданий или ложных тревог равна нулю или единице, а поправка выключена: d′ бесконечна.")
            .Metric("Попадания", Fmt.Pct(HitRate), null, "«да» при сигнале")
            .Metric("Ложные тревоги", Fmt.Pct(FalseAlarmRate), null, "«да» при шуме")
            .Metric("d′", Sensitivity, null, "различимость сигнала и шума в СКО", MetricQuality.Unknown, 3)
            .Metric("c", Criterion, null, "смещение ответа; нуль — нейтральный наблюдатель", MetricQuality.Unknown, 3)
            .Metric("Площадь под ROC", AreaUnderCurve, null, "доля верных ответов при выборе из двух", MetricQuality.Unknown, 3)
            .Metric("A′", APrime, null, "непараметрическая чувствительность", MetricQuality.Unknown, 3)
            .FindingIf(double.IsFinite(Sensitivity) && Sensitivity < 0.3,
                "Чувствительность близка к нулю: ответы почти не зависят от того, был ли сигнал.")
            .FindingIf(Corrected,
                "Крайняя доля исправлена поправкой: без неё d′ была бы бесконечной, а с ней зависит от числа проб.")
            .Warning("d′ и c предполагают нормальные распределения сигнала и шума с равными дисперсиями. "
                + "Если наклон кривой ROC в z-координатах заметно отличается от единицы, допущение нарушено и "
                + "d′ зависит от критерия.")
            .Build();

    /// <summary>Доля попаданий при заданных d′ и c: Φ(d′/2 − c)</summary>
    /// <param name="sensitivity">d′</param>
    /// <param name="criterion">c</param>
    public static double PredictedHitRate(double sensitivity, double criterion)
        => StatInference.NormalCdf((sensitivity / 2) - criterion);

    /// <summary>Доля ложных тревог при заданных d′ и c: Φ(−d′/2 − c)</summary>
    /// <param name="sensitivity">d′</param>
    /// <param name="criterion">c</param>
    public static double PredictedFalseAlarmRate(double sensitivity, double criterion)
        => StatInference.NormalCdf((-sensitivity / 2) - criterion);

    private static double Z(double rate)
        => rate <= 0 ? double.NegativeInfinity : rate >= 1 ? double.PositiveInfinity : StatInference.NormalQuantile(rate);

    private static double NonparametricSensitivity(double hit, double falseAlarm)
    {
        if (Math.Abs(hit - falseAlarm) < 1e-15)
            return 0.5;

        return hit > falseAlarm
            ? 0.5 + ((hit - falseAlarm) * (1 + hit - falseAlarm) / (4 * hit * (1 - falseAlarm)))
            : 0.5 - ((falseAlarm - hit) * (1 + falseAlarm - hit) / (4 * falseAlarm * (1 - hit)));
    }
}

/// <summary>
/// Теория обнаружения сигнала: чувствительность отдельно от склонности отвечать «да».
/// </summary>
/// <remarks>
/// <para>
/// Доля верных ответов смешивает две разные вещи: насколько хорошо наблюдатель различает сигнал и
/// шум и насколько он склонен говорить «да». Врач, который находит у всех опухоль, не пропустит ни
/// одной — и ошибётся на каждом здоровом. Модель равных дисперсий разделяет их: d′ — расстояние между
/// распределениями, c — положение порога решения.
/// </para>
/// <para>
/// Доли 0 и 1 дают бесконечные z-оценки, а при конечном числе проб они случаются постоянно. Поэтому
/// по умолчанию действует лог-линейная поправка; её можно выбрать или отключить.
/// </para>
/// </remarks>
public static class SignalDetection
{
    /// <summary>Показатели по числам ответов</summary>
    /// <param name="hits">Попадания: «да» при сигнале</param>
    /// <param name="misses">Пропуски: «нет» при сигнале</param>
    /// <param name="falseAlarms">Ложные тревоги: «да» при шуме</param>
    /// <param name="correctRejections">Верные отказы: «нет» при шуме</param>
    /// <param name="correction">Поправка крайних долей</param>
    public static SignalDetectionResult FromCounts(
        int hits, int misses, int falseAlarms, int correctRejections, RateCorrection correction = RateCorrection.LogLinear)
    {
        if (hits < 0 || misses < 0 || falseAlarms < 0 || correctRejections < 0)
            throw new ArgumentOutOfRangeException(nameof(hits), "Числа ответов не могут быть отрицательными");

        int signal = hits + misses;
        int noise = falseAlarms + correctRejections;

        if (signal == 0 || noise == 0)
            throw new ArgumentException("Нужны пробы и с сигналом, и с шумом");

        double hit = (double)hits / signal;
        double falseAlarm = (double)falseAlarms / noise;
        bool corrected = false;

        switch (correction)
        {
            case RateCorrection.LogLinear:
                hit = (hits + 0.5) / (signal + 1.0);
                falseAlarm = (falseAlarms + 0.5) / (noise + 1.0);
                corrected = hits == 0 || hits == signal || falseAlarms == 0 || falseAlarms == noise;
                break;
            case RateCorrection.HalfCount:
                (hit, bool fixedHit) = Clip(hit, signal);
                (falseAlarm, bool fixedFalse) = Clip(falseAlarm, noise);
                corrected = fixedHit || fixedFalse;
                break;
        }

        return new SignalDetectionResult(hit, falseAlarm, correction, corrected);
    }

    /// <summary>Показатели по готовым долям</summary>
    /// <param name="hitRate">Доля попаданий</param>
    /// <param name="falseAlarmRate">Доля ложных тревог</param>
    public static SignalDetectionResult FromRates(double hitRate, double falseAlarmRate)
    {
        Numerics.RequireProbability(hitRate, nameof(hitRate));
        Numerics.RequireProbability(falseAlarmRate, nameof(falseAlarmRate));

        return new SignalDetectionResult(hitRate, falseAlarmRate, RateCorrection.None, false);
    }

    /// <summary>
    /// Кривая ROC модели равных дисперсий: точки (ложные тревоги, попадания) при движении критерия
    /// </summary>
    /// <param name="sensitivity">d′</param>
    /// <param name="points">Число внутренних точек</param>
    public static IReadOnlyList<(double FalseAlarmRate, double HitRate)> RocCurve(double sensitivity, int points = 101)
    {
        Numerics.RequireFinite(sensitivity, nameof(sensitivity));
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 2);

        double span = Math.Abs(sensitivity / 2) + 8;
        var curve = new List<(double, double)>(points + 2) { (0, 0) };

        for (int i = 0; i < points; i++)
        {
            double criterion = span - (2 * span * i / (points - 1));
            curve.Add((SignalDetectionResult.PredictedFalseAlarmRate(sensitivity, criterion),
                SignalDetectionResult.PredictedHitRate(sensitivity, criterion)));
        }

        curve.Add((1, 1));

        return curve;
    }

    private static (double Rate, bool Changed) Clip(double rate, int trials)
        => rate <= 0 ? (1.0 / (2 * trials), true)
            : rate >= 1 ? (1 - (1.0 / (2 * trials)), true)
            : (rate, false);
}

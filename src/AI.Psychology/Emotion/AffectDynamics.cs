using AI.Simulation.Stochastic;

namespace AI.Psychology.Emotion;

/// <summary>
/// Динамика аффекта: состояние колеблется вокруг своей базовой линии и после встряски возвращается к ней.
/// </summary>
/// <remarks>
/// <para>
/// Так устроена модель DynAffect Куппенса, Оравец и Туэрлинкса (2010): у каждого человека есть «дом»
/// аффекта — базовая линия, сила притяжения к нему и разброс вокруг. Математически это процесс
/// Орнштейна — Уленбека по каждой оси PAD: встряска от события затухает с характерным временем, а шум
/// не даёт состоянию застыть. Процесс берётся готовым из <c>AI.Simulation</c>; здесь только три его
/// экземпляра и граница куба [−1; 1]³.
/// </para>
/// <para>
/// Оси считаются независимыми и с одним временем возврата. В данных приятность и возбуждение
/// связаны, а времена у разных людей и эмоций различаются на порядки: печаль держится дни, испуг —
/// минуты. Разные времена для разных эмоций задаются разными экземплярами.
/// </para>
/// </remarks>
public sealed class AffectDynamics
{
    private readonly OrnsteinUhlenbeckProcess _pleasure;
    private readonly OrnsteinUhlenbeckProcess _arousal;
    private readonly OrnsteinUhlenbeckProcess _dominance;

    /// <summary>Создаёт динамику</summary>
    /// <param name="baseline">Базовая линия — привычное состояние человека</param>
    /// <param name="relaxationTime">Время возврата: за него отклонение уменьшается в e раз</param>
    /// <param name="spread">Стационарный разброс по каждой оси; нуль — без случайных колебаний</param>
    public AffectDynamics(Affect baseline, double relaxationTime, double spread)
    {
        Affect.Require(baseline, nameof(baseline));

        Baseline = baseline;
        _pleasure = OrnsteinUhlenbeckProcess.FromRelaxation(baseline.Pleasure, relaxationTime, spread);
        _arousal = OrnsteinUhlenbeckProcess.FromRelaxation(baseline.Arousal, relaxationTime, spread);
        _dominance = OrnsteinUhlenbeckProcess.FromRelaxation(baseline.Dominance, relaxationTime, spread);
    }

    /// <summary>Базовая линия</summary>
    public Affect Baseline { get; }

    /// <summary>Время возврата</summary>
    public double RelaxationTime => _pleasure.RelaxationTime;

    /// <summary>Стационарный разброс по каждой оси</summary>
    public double Spread => _pleasure.StationaryDeviation;

    /// <summary>Период полураспада отклонения</summary>
    public double HalfLife => _pleasure.HalfLife;

    /// <summary>Встряска: к состоянию прибавляется толчок от события, результат прижат к кубу</summary>
    /// <param name="current">Текущее состояние</param>
    /// <param name="impulse">Толчок</param>
    public static Affect Stimulate(Affect current, Affect impulse) => (current + impulse).Clamp();

    /// <summary>Ожидаемое состояние через время: отклонение от базовой линии убывает как e^(−t/τ)</summary>
    /// <param name="current">Текущее состояние</param>
    /// <param name="time">Время</param>
    public Affect Expected(Affect current, double time) => new Affect(
        _pleasure.ExpectedValue(current.Pleasure, time),
        _arousal.ExpectedValue(current.Arousal, time),
        _dominance.ExpectedValue(current.Dominance, time)).Clamp();

    /// <summary>Случайное состояние через шаг — точный переход по каждой оси, затем граница куба</summary>
    /// <param name="current">Текущее состояние</param>
    /// <param name="step">Шаг по времени</param>
    /// <param name="random">Генератор</param>
    public Affect Next(Affect current, double step, Random random) => new Affect(
        _pleasure.Next(current.Pleasure, step, random),
        _arousal.Next(current.Arousal, step, random),
        _dominance.Next(current.Dominance, step, random)).Clamp();

    /// <summary>
    /// Время, за которое ожидаемое отклонение от базовой линии уменьшится до заданной доли начального
    /// </summary>
    /// <param name="fraction">Доля, от 0 до 1</param>
    public double TimeToFraction(double fraction)
    {
        if (!(fraction > 0 && fraction < 1))
            throw new ArgumentOutOfRangeException(nameof(fraction), "Доля лежит на (0; 1)");

        return -Math.Log(fraction) * RelaxationTime;
    }
}

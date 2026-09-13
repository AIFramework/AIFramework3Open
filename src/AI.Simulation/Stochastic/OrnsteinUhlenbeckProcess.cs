using AI.Statistics;

namespace AI.Simulation.Stochastic;

/// <summary>
/// Процесс Орнштейна — Уленбека: случайное блуждание, которое тянется к своему среднему.
/// </summary>
/// <remarks>
/// <para>
/// dx = θ(μ − x)·dt + σ·dW. Отклонение от среднего затухает с характерным временем 1/θ, а шум не даёт
/// ему угаснуть совсем: процесс колеблется вокруг μ со стационарной дисперсией σ²/(2θ), и соседние по
/// времени значения связаны автокорреляцией e^(−θτ). Это непрерывный аналог авторегрессии первого
/// порядка. Так описывают процентные ставки (модель Васичека), скорость броуновской частицы и колебания
/// настроения вокруг привычного уровня.
/// </para>
/// <para>
/// Переход за любой шаг известен точно: x(t + Δ) распределено нормально со средним μ + (x − μ)·e^(−θΔ)
/// и дисперсией σ²·(1 − e^(−2θΔ))/(2θ). Поэтому имитация здесь не накапливает ошибки дискретизации
/// при любом шаге, в отличие от метода Эйлера — Маруямы в <see cref="SystemDynamics.StockFlowModel"/>,
/// и служит для него эталоном.
/// </para>
/// </remarks>
public sealed class OrnsteinUhlenbeckProcess
{
    /// <summary>Создаёт процесс</summary>
    /// <param name="mean">Среднее μ, к которому тянется процесс</param>
    /// <param name="reversionRate">Скорость возврата θ, 1/время</param>
    /// <param name="volatility">Интенсивность шума σ, единица величины на √время; нуль — без шума</param>
    public OrnsteinUhlenbeckProcess(double mean, double reversionRate, double volatility)
    {
        if (!double.IsFinite(mean))
            throw new ArgumentOutOfRangeException(nameof(mean), "Среднее — конечное число");

        if (!(reversionRate > 0) || double.IsInfinity(reversionRate))
            throw new ArgumentOutOfRangeException(nameof(reversionRate), "Скорость возврата — конечное положительное число");

        if (!(volatility >= 0) || double.IsInfinity(volatility))
            throw new ArgumentOutOfRangeException(nameof(volatility), "Интенсивность шума — конечное неотрицательное число");

        Mean = mean;
        ReversionRate = reversionRate;
        Volatility = volatility;
    }

    /// <summary>
    /// Процесс по величинам, которые проще назвать: характерное время возврата и разброс вокруг среднего
    /// </summary>
    /// <param name="mean">Среднее μ</param>
    /// <param name="relaxationTime">Время возврата 1/θ: за него отклонение уменьшается в e раз</param>
    /// <param name="stationaryDeviation">Стационарное СКО: разброс вокруг среднего за долгое время</param>
    public static OrnsteinUhlenbeckProcess FromRelaxation(double mean, double relaxationTime, double stationaryDeviation)
    {
        if (!(relaxationTime > 0) || double.IsInfinity(relaxationTime))
            throw new ArgumentOutOfRangeException(nameof(relaxationTime), "Время возврата — конечное положительное число");

        if (!(stationaryDeviation >= 0) || double.IsInfinity(stationaryDeviation))
            throw new ArgumentOutOfRangeException(nameof(stationaryDeviation), "Разброс — конечное неотрицательное число");

        // σ²/(2θ) = s² при θ = 1/τ даёт σ = s·√(2/τ)
        return new OrnsteinUhlenbeckProcess(mean, 1 / relaxationTime, stationaryDeviation * Math.Sqrt(2 / relaxationTime));
    }

    /// <summary>Среднее μ</summary>
    public double Mean { get; }

    /// <summary>Скорость возврата θ</summary>
    public double ReversionRate { get; }

    /// <summary>Интенсивность шума σ</summary>
    public double Volatility { get; }

    /// <summary>Время возврата 1/θ: отклонение от среднего уменьшается в e раз</summary>
    public double RelaxationTime => 1 / ReversionRate;

    /// <summary>Период полураспада отклонения: ln 2/θ</summary>
    public double HalfLife => Math.Log(2) / ReversionRate;

    /// <summary>Стационарная дисперсия σ²/(2θ)</summary>
    public double StationaryVariance => Volatility * Volatility / (2 * ReversionRate);

    /// <summary>Стационарное СКО</summary>
    public double StationaryDeviation => Math.Sqrt(StationaryVariance);

    /// <summary>Ожидаемое значение через время t от известного начального: μ + (x₀ − μ)·e^(−θt)</summary>
    /// <param name="start">Начальное значение</param>
    /// <param name="time">Время, неотрицательное</param>
    public double ExpectedValue(double start, double time)
    {
        RequireTime(time);

        return Mean + ((start - Mean) * Math.Exp(-ReversionRate * time));
    }

    /// <summary>Дисперсия значения через время t от известного начального: σ²·(1 − e^(−2θt))/(2θ)</summary>
    /// <param name="time">Время, неотрицательное</param>
    public double Variance(double time)
    {
        RequireTime(time);

        return -StationaryVariance * double.ExpM1(-2 * ReversionRate * time);
    }

    /// <summary>Автокорреляция стационарного процесса на сдвиге τ: e^(−θ|τ|)</summary>
    /// <param name="lag">Сдвиг по времени</param>
    public double Autocorrelation(double lag) => Math.Exp(-ReversionRate * Math.Abs(lag));

    /// <summary>
    /// Время, за которое ожидаемое значение дойдёт от начального до уровня; +∞ — не дойдёт никогда,
    /// потому что уровень лежит за средним или по другую сторону от начального
    /// </summary>
    /// <param name="start">Начальное значение</param>
    /// <param name="level">Уровень</param>
    /// <remarks>
    /// Это время для ожидания, а не для отдельной траектории: с шумом уровень пересекается раньше или
    /// позже, и распределение времени первого достижения даёт только имитация.
    /// </remarks>
    public double TimeToExpected(double start, double level)
    {
        if (start == level)
            return 0;

        double ratio = (level - Mean) / (start - Mean);

        return ratio > 0 && ratio < 1 ? -Math.Log(ratio) / ReversionRate : double.PositiveInfinity;
    }

    /// <summary>Точный переход за шаг: значение через время step после current</summary>
    /// <param name="current">Текущее значение</param>
    /// <param name="step">Шаг по времени, неотрицательный</param>
    /// <param name="random">Генератор</param>
    public double Next(double current, double step, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        double spread = Math.Sqrt(Variance(step));

        return ExpectedValue(current, step) + (spread > 0 ? spread * RandomEngine.NextGaussian(random) : 0);
    }

    /// <summary>Траектория с постоянным шагом: начальное значение и ещё steps значений</summary>
    /// <param name="start">Начальное значение</param>
    /// <param name="step">Шаг по времени</param>
    /// <param name="steps">Число шагов</param>
    /// <param name="random">Генератор</param>
    public double[] Path(double start, double step, int steps, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        RequireTime(step);

        var path = new double[steps + 1];
        path[0] = start;

        for (int k = 1; k <= steps; k++)
            path[k] = Next(path[k - 1], step, random);

        return path;
    }

    private static void RequireTime(double time)
    {
        if (!(time >= 0) || double.IsInfinity(time))
            throw new ArgumentOutOfRangeException(nameof(time), "Время — конечное неотрицательное число");
    }
}

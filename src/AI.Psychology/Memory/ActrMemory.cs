using AI.Psychology.Internal;

namespace AI.Psychology.Memory;

/// <summary>
/// Декларативная память ACT-R (Андерсон): активация следа, вероятность и время извлечения.
/// </summary>
/// <remarks>
/// <para>
/// Каждое предъявление оставляет след, который гаснет как степень от возраста: базовая активация
/// B = ln Σ tⱼ^(−d), d = 0,5 — стандартное значение архитектуры. Из одной формулы следуют два закона:
/// степенной закон забывания (одно предъявление даёт B = −d·ln t) и степенной закон научения
/// (n предъявлений одного возраста дают B = ln n − d·ln t). Извлечение удаётся, если активация с
/// логистическим шумом выше порога τ: P = 1/(1 + e^(−(B − τ)/s)); время извлечения F·e^(−B).
/// </para>
/// <para>
/// Постоянное затухание не объясняет эффект интервала: сплошное повторение даёт следы моложе, и
/// стандартная формула предсказывает, что оно лучше распределённого, — вопреки опытам. Павлик и
/// Андерсон (2005) сделали затухание следа зависимым от активации в момент предъявления:
/// dᵢ = c·e^(mᵢ) + a. Повторение, когда материал ещё свеж, оставляет быстро гаснущий след, и на
/// длинных интервалах распределённое повторение выигрывает. Параметры c и a подбираются по данным;
/// порог, шум и множитель времени — тоже, у архитектуры для них нет универсальных значений.
/// </para>
/// </remarks>
public static class ActrMemory
{
    /// <summary>Стандартный показатель затухания ACT-R</summary>
    public const double StandardDecay = 0.5;

    /// <summary>Базовая активация: ln Σ tⱼ^(−d)</summary>
    /// <param name="ages">Возраст каждого предъявления — время с него</param>
    /// <param name="decay">Показатель затухания d</param>
    /// <returns>Активация; −∞, если предъявлений не было</returns>
    public static double BaseLevelActivation(IEnumerable<double> ages, double decay = StandardDecay)
    {
        ArgumentNullException.ThrowIfNull(ages);
        Numerics.RequirePositive(decay, nameof(decay));

        double sum = 0;

        foreach (double age in ages)
        {
            Numerics.RequirePositive(age, nameof(ages));
            sum += Math.Pow(age, -decay);
        }

        return sum == 0 ? double.NegativeInfinity : Math.Log(sum);
    }

    /// <summary>Вероятность извлечения: 1/(1 + e^(−(B − τ)/s))</summary>
    /// <param name="activation">Активация B</param>
    /// <param name="threshold">Порог извлечения τ</param>
    /// <param name="noise">Шум активации s</param>
    public static double RetrievalProbability(double activation, double threshold, double noise)
    {
        Numerics.RequireFinite(threshold, nameof(threshold));
        Numerics.RequirePositive(noise, nameof(noise));

        return double.IsNegativeInfinity(activation) ? 0 : Numerics.Logistic((activation - threshold) / noise);
    }

    /// <summary>Время извлечения: F·e^(−B)</summary>
    /// <param name="activation">Активация B</param>
    /// <param name="latencyFactor">Множитель времени F, с</param>
    public static double RetrievalLatency(double activation, double latencyFactor)
    {
        Numerics.RequirePositive(latencyFactor, nameof(latencyFactor));

        return latencyFactor * Math.Exp(-activation);
    }

    /// <summary>
    /// Активация по модели Павлика и Андерсона: затухание каждого следа зависит от активации
    /// в момент предъявления, dᵢ = c·e^(mᵢ) + a
    /// </summary>
    /// <param name="presentationTimes">Моменты предъявлений по возрастанию</param>
    /// <param name="now">Момент, для которого считается активация</param>
    /// <param name="decayScale">c — насколько свежесть материала ускоряет затухание</param>
    /// <param name="decayIntercept">a — затухание следа, оставленного забытым материалом</param>
    public static double SpacedActivation(IReadOnlyList<double> presentationTimes, double now, double decayScale, double decayIntercept)
    {
        ArgumentNullException.ThrowIfNull(presentationTimes);

        if (!(decayScale >= 0) || double.IsInfinity(decayScale))
            throw new ArgumentOutOfRangeException(nameof(decayScale), "c — конечное неотрицательное число");

        Numerics.RequirePositive(decayIntercept, nameof(decayIntercept));

        int n = presentationTimes.Count;
        var decays = new double[n];

        for (int i = 0; i < n; i++)
        {
            double time = presentationTimes[i];

            if (i > 0 && !(time > presentationTimes[i - 1]))
                throw new ArgumentException("Моменты предъявлений должны строго возрастать", nameof(presentationTimes));

            if (!(time < now))
                throw new ArgumentException("Все предъявления должны быть раньше момента расчёта", nameof(now));

            double activation = double.NegativeInfinity;

            if (i > 0)
            {
                double sum = 0;

                for (int j = 0; j < i; j++)
                    sum += Math.Pow(time - presentationTimes[j], -decays[j]);

                activation = Math.Log(sum);
            }

            decays[i] = (decayScale * Math.Exp(activation)) + decayIntercept;
        }

        double total = 0;

        for (int i = 0; i < n; i++)
            total += Math.Pow(now - presentationTimes[i], -decays[i]);

        return n == 0 ? double.NegativeInfinity : Math.Log(total);
    }
}

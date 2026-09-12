using AI.DataStructs.Algebraic;
using AI.Statistics;

namespace AI.Simulation.Inference;

/// <summary>
/// Облако частиц: убеждение о состоянии, представленное взвешенной выборкой.
/// </summary>
/// <remarks>
/// <para>
/// Частица — одна полная версия состояния: не вектор чисел, а любой объект, вплоть до целой версии мира.
/// Поэтому облако держит распределение любой формы — кольцо, дугу, два горба, — и закон заранее выбирать не нужно.
/// </para>
/// <para>
/// Цикл обычный: <see cref="Propagate"/> двигает частицы во времени, <see cref="Reweight"/> учитывает наблюдение,
/// <see cref="ResampleIfDegenerate"/> отбрасывает невероятные версии, <see cref="Rejuvenate"/> разводит
/// размноженные копии.
/// </para>
/// <para>
/// Веса хранятся нормированными логарифмами: правдоподобия десятков наблюдений перемножаются, и в обычной
/// шкале веса ушли бы в машинный ноль раньше, чем облако выродится.
/// </para>
/// <para>
/// Состояния должны быть неизменяемыми: после пересборки одна частица встречается в облаке несколько раз,
/// и правка одной копии задела бы остальные. Переходы и предложения возвращают новое состояние.
/// </para>
/// </remarks>
/// <typeparam name="TState">Тип состояния</typeparam>
public sealed class ParticleSet<TState>
{
    private TState[] _states;
    private double[] _logWeights;

    /// <summary>Создаёт облако из равновесных частиц</summary>
    /// <param name="states">Состояния частиц</param>
    public ParticleSet(IEnumerable<TState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        _states = states.ToArray();
        if (_states.Length == 0)
            throw new ArgumentException("Облако не может быть пустым", nameof(states));

        _logWeights = EqualLogWeights(_states.Length);
    }

    /// <summary>Число частиц</summary>
    public int Count => _states.Length;

    /// <summary>Состояния частиц</summary>
    public IReadOnlyList<TState> States => _states;

    /// <summary>Нормированные веса частиц: сумма равна единице</summary>
    public Vector Weights => new(Normalized());

    /// <summary>
    /// Эффективный размер выборки: N при равных весах, 1 — когда всё держит одна частица.
    /// Падение ниже половины N — обычный сигнал к пересборке
    /// </summary>
    public double EffectiveSampleSize => WeightedStatistics.EffectiveSampleSize(Weights);

    /// <summary>Самая весомая частица</summary>
    public TState MostProbable => _states[Array.IndexOf(_logWeights, _logWeights.Max())];

    /// <summary>
    /// Учитывает наблюдение: вес каждой частицы умножается на правдоподобие наблюдения при её состоянии
    /// </summary>
    /// <param name="logLikelihood">Логарифм правдоподобия; −∞ — состояние наблюдению противоречит</param>
    /// <returns>
    /// Логарифм правдоподобия наблюдения по всему облаку, ln Σ wᵢ·Lᵢ: чем он ниже,
    /// тем неожиданнее наблюдение для текущего убеждения
    /// </returns>
    /// <exception cref="InvalidOperationException">Наблюдение противоречит всем частицам; облако остаётся прежним</exception>
    public double Reweight(Func<TState, double> logLikelihood)
    {
        ArgumentNullException.ThrowIfNull(logLikelihood);

        double[] updated = new double[Count];
        for (int i = 0; i < Count; i++)
        {
            double value = logLikelihood(_states[i]);
            if (double.IsNaN(value) || double.IsPositiveInfinity(value))
                throw new ArgumentException($"Правдоподобие частицы {i} не определено: {value}", nameof(logLikelihood));

            updated[i] = _logWeights[i] + value;
        }

        if (updated.All(double.IsNegativeInfinity))
            throw new InvalidOperationException("Наблюдение противоречит всем частицам: облако не может его принять");

        double evidence = StatUtils.LogSumExp(updated);
        for (int i = 0; i < Count; i++)
            updated[i] -= evidence;

        _logWeights = updated;
        return evidence;
    }

    /// <summary>
    /// Прогноз: каждая частица переходит в следующее состояние — сдвиг во времени, движение, шум процесса.
    /// Веса не меняются
    /// </summary>
    /// <param name="transition">Переход: по состоянию и генератору — новое состояние</param>
    /// <param name="random">Генератор</param>
    public void Propagate(Func<TState, Random, TState> transition, Random random)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(random);

        for (int i = 0; i < Count; i++)
            _states[i] = transition(_states[i], random);
    }

    /// <summary>
    /// Систематическая пересборка: частицы размножаются пропорционально весам, веса выравниваются.
    /// </summary>
    /// <remarks>
    /// Одна равномерная величина на всё облако: число копий частицы отличается от N·w не больше чем на
    /// единицу, поэтому шума меньше, чем при независимом выборе, а работа линейна по N.
    /// </remarks>
    /// <param name="random">Генератор</param>
    public void Resample(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        int[] picks = SystematicIndices(Normalized(), Count, random);
        var states = new TState[Count];
        for (int k = 0; k < Count; k++)
            states[k] = _states[picks[k]];

        _states = states;
        _logWeights = EqualLogWeights(Count);
    }

    /// <summary>Пересобирает облако, если эффективный размер упал ниже доли от числа частиц</summary>
    /// <param name="random">Генератор</param>
    /// <param name="threshold">Доля числа частиц на (0; 1]</param>
    /// <returns>Была ли пересборка</returns>
    public bool ResampleIfDegenerate(Random random, double threshold = 0.5)
    {
        if (!(threshold > 0 && threshold <= 1))
            throw new ArgumentOutOfRangeException(nameof(threshold), "Порог — доля числа частиц на (0; 1]");

        if (EffectiveSampleSize >= threshold * Count)
            return false;

        Resample(random);
        return true;
    }

    /// <summary>
    /// Встряска: шаги Метрополиса — Гастингса для каждой частицы. После пересборки в облаке много копий
    /// одних и тех же состояний; шаги разводят их, не меняя целевого распределения. Применяется после пересборки
    /// </summary>
    /// <param name="proposal">
    /// Симметричное предложение — вероятность шага туда и обратно одинакова, как у случайного блуждания.
    /// Для несимметричного предложения критерий принятия был бы неверен
    /// </param>
    /// <param name="logTarget">
    /// Логарифм целевой плотности с точностью до константы: априорная плотность состояния плюс
    /// правдоподобия всех уже учтённых наблюдений
    /// </param>
    /// <param name="random">Генератор</param>
    /// <param name="steps">Шагов на частицу</param>
    /// <returns>Доля принятых шагов — ориентир для размера шага; обычная цель 0.2–0.5</returns>
    public double Rejuvenate(Func<TState, Random, TState> proposal, Func<TState, double> logTarget, Random random, int steps = 1)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(logTarget);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);

        int accepted = 0;
        for (int i = 0; i < Count; i++)
        {
            double current = logTarget(_states[i]);
            for (int step = 0; step < steps; step++)
            {
                TState candidate = proposal(_states[i], random);
                double candidateLog = logTarget(candidate);

                // Кандидат с NaN отвергается сам: сравнение с NaN ложно
                if (Math.Log(random.NextDouble()) < candidateLog - current)
                {
                    _states[i] = candidate;
                    current = candidateLog;
                    accepted++;
                }
            }
        }

        return accepted / (double)(Count * steps);
    }

    /// <summary>
    /// Представительные состояния, выбранные пропорционально весам, — например, несколько версий мира
    /// для рассказа LLM. Облако не меняется
    /// </summary>
    /// <param name="count">Сколько состояний выбрать</param>
    /// <param name="random">Генератор</param>
    public IReadOnlyList<TState> Draw(int count, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        int[] picks = SystematicIndices(Normalized(), count, random);
        var drawn = new TState[count];
        for (int k = 0; k < count; k++)
            drawn[k] = _states[picks[k]];

        return drawn;
    }

    /// <summary>Облако из априорного распределения: <paramref name="count"/> независимых выборок</summary>
    /// <param name="count">Число частиц</param>
    /// <param name="prior">Выборка одного состояния из априорного распределения</param>
    /// <param name="random">Генератор</param>
    public static ParticleSet<TState> FromPrior(int count, Func<Random, TState> prior, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(random);

        var states = new TState[count];
        for (int i = 0; i < count; i++)
            states[i] = prior(random);

        return new ParticleSet<TState>(states);
    }

    private double[] Normalized()
    {
        double[] weights = new double[Count];
        for (int i = 0; i < Count; i++)
            weights[i] = Math.Exp(_logWeights[i]);

        return weights;
    }

    private static double[] EqualLogWeights(int count)
    {
        double[] logWeights = new double[count];
        Array.Fill(logWeights, -Math.Log(count));
        return logWeights;
    }

    /// <summary>Систематический выбор: позиции с шагом 1/count от одной равномерной величины</summary>
    private static int[] SystematicIndices(double[] weights, int count, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        int[] picks = new int[count];
        double step = 1.0 / count;
        double position = random.NextDouble() * step;
        double cumulative = weights[0];
        int index = 0;

        for (int k = 0; k < count; k++)
        {
            // Нулевой вес не увеличивает накопленную сумму, поэтому частица без веса не выбирается
            while (cumulative <= position && index < weights.Length - 1)
            {
                index++;
                cumulative += weights[index];
            }

            picks[k] = index;
            position += step;
        }

        return picks;
    }
}

using AI.Extensions;
using System;

namespace AI.Statistics.MonteCarlo;

/// <summary>
/// Метод Монте-Карло Марковских Цепей (Метрополис) для одномерной
/// плотности, заданной логарифмом ненормированной плотности.
/// 
/// Потокобезопасность: каждый экземпляр владеет своим Random, так
/// что разные потоки должны работать со своими MCMC_1D.
/// </summary>
[Serializable]
public class MCMC_1D
{
    private readonly Func<double, double> _distrLog;
    private Random _random;
    private int _rndSeed = 0;
    private bool _useSeed = false;

    /// <summary>Длительность переходного процесса в отсчётах.</summary>
    public int StepsTrPro { get; set; }

    /// <summary>Seed ГПСЧ (применяется при <see cref="UseSeed"/> = true).</summary>
    public int Seed
    {
        get => _rndSeed;
        set { _rndSeed = value; InitRnd(); }
    }

    /// <summary>Использовать ли зафиксированный seed.</summary>
    public bool UseSeed
    {
        get => _useSeed;
        set { _useSeed = value; InitRnd(); }
    }

    /// <summary>
    /// Создаёт цепь для плотности, заданной через лог.
    /// </summary>
    /// <param name="distr_log">Логарифм ненормированной плотности</param>
    /// <param name="stepsTrPro">Длительность переходного процесса</param>
    public MCMC_1D(Func<double, double> distr_log, int stepsTrPro = 400)
    {
        _distrLog = distr_log ?? throw new ArgumentNullException(nameof(distr_log));
        StepsTrPro = stepsTrPro;
        InitRnd();
    }

    /// <summary>
    /// Вероятность принятия в Метрополисе = min(1, p(new)/p(old)).
    /// В лог-пространстве — min(0, Δlog) ⇒ exp(...). В коде ядра
    /// используется более экономичный сравнитель без явного min.
    /// </summary>
    public double AcceptProb(double old_value, double new_value)
        => Math.Exp(_distrLog(new_value) - _distrLog(old_value));

    /// <summary>
    /// Генерация выборки.
    /// </summary>
    /// <param name="len">Длина выборки</param>
    /// <param name="start">Начальное значение</param>
    /// <param name="decorelate">Перемешать ли результат</param>
    /// <param name="min">Минимум предложения (uniform proposal)</param>
    /// <param name="max">Максимум предложения</param>
    public double[] Generate(int len, double min = 0, double max = 1, double start = 0, bool decorelate = true)
    {
        if (len < 0) throw new ArgumentOutOfRangeException(nameof(len));

        double[] data = new double[len];
        double scale = max - min;
        double oldValue = start;
        double oldLog = _distrLog(oldValue);

        // Транзиент: даём цепи выйти на стационар
        for (int i = 0; i < StepsTrPro; i++)
            StepMetropolis(ref oldValue, ref oldLog, min, scale);

        // Основная выборка
        for (int i = 0; i < len; i++)
        {
            StepMetropolis(ref oldValue, ref oldLog, min, scale);
            data[i] = oldValue;
        }

        if (decorelate) data.Shuffle();
        return data;
    }

    // Один шаг Метрополиса. Кэшируем log p(old) — экономим одно
    // обращение к пользовательской плотности на итерацию.
    private void StepMetropolis(ref double oldValue, ref double oldLog, double min, double scale)
    {
        double cand = (_random.NextDouble() * scale) + min;
        double candLog = _distrLog(cand);

        // log α = candLog - oldLog; принимаем если log α > log u
        // (эквивалентно α > u, но устойчиво к переполнению).
        double logU = Math.Log(_random.NextDouble());
        if ((candLog - oldLog) > logU)
        {
            oldValue = cand;
            oldLog = candLog;
        }
    }

    private void InitRnd()
        => _random = _useSeed ? RandomEngine.Create(_rndSeed) : RandomEngine.Create();
}

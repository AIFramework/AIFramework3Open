using AI.Psychology.Internal;
using AI.Statistics;

namespace AI.Psychology.Decision;

/// <summary>Исход одной пробы модели диффузии</summary>
/// <param name="Upper">Достигнута верхняя граница — обычно верный ответ</param>
/// <param name="ResponseTime">Время ответа с учётом времени вне решения, с</param>
public readonly record struct DiffusionTrial(bool Upper, double ResponseTime);

/// <summary>
/// Модель дрейфа-диффузии Рэтклиффа: решение как накопление свидетельств до одной из двух границ.
/// </summary>
/// <remarks>
/// <para>
/// Свидетельство x накапливается с дрейфом v и шумом s: dx = v·dt + s·dW, начиная с z = w·a между
/// границами 0 и a. Ответ — та граница, которой x достигнет первой, время — момент достижения плюс
/// время вне решения t₀ (восприятие и движение). Одна модель объясняет сразу точность, среднее время
/// и форму распределения времён, и потому разделяет то, что по отдельным показателям не различить:
/// качество информации (v), осторожность (a), предвзятость (w) и моторную скорость (t₀).
/// </para>
/// <para>
/// Вероятность и среднее время известны в замкнутом виде: P(верх) = (1 − e^(−2vz/s²))/(1 − e^(−2va/s²)),
/// E[T] = (a·P(верх) − z)/v. Плотность времени достижения нижней границы — ряд Наварро и Фасса (2009)
/// для больших времён; у самых малых времён он сходится медленно, и число членов растёт.
/// </para>
/// <para>
/// Шум s задаёт масштаб: у Рэтклиффа принято s = 0,1, здесь по умолчанию 1. Параметры модели,
/// найденные при одном s, пересчитываются к другому умножением v и a на отношение шумов. Разброс
/// параметров от пробы к пробе (полная модель Рэтклиффа) не учитывается.
/// </para>
/// </remarks>
public sealed class DriftDiffusionModel
{
    /// <summary>Создаёт модель</summary>
    /// <param name="drift">Дрейф v: скорость накопления в пользу верхней границы</param>
    /// <param name="boundarySeparation">Расстояние между границами a</param>
    /// <param name="startingPoint">Относительная начальная точка w ∈ (0; 1); 0,5 — без предвзятости</param>
    /// <param name="nonDecisionTime">Время вне решения t₀, с</param>
    /// <param name="noise">Коэффициент шума s</param>
    public DriftDiffusionModel(
        double drift, double boundarySeparation, double startingPoint = 0.5, double nonDecisionTime = 0.3, double noise = 1.0)
    {
        Numerics.RequireFinite(drift, nameof(drift));
        Numerics.RequirePositive(boundarySeparation, nameof(boundarySeparation));
        Numerics.RequirePositive(noise, nameof(noise));

        if (!(startingPoint > 0 && startingPoint < 1))
            throw new ArgumentOutOfRangeException(nameof(startingPoint), "Начальная точка лежит строго между границами: w ∈ (0; 1)");

        if (!(nonDecisionTime >= 0) || double.IsInfinity(nonDecisionTime))
            throw new ArgumentOutOfRangeException(nameof(nonDecisionTime), "Время вне решения — конечное неотрицательное число");

        Drift = drift;
        BoundarySeparation = boundarySeparation;
        StartingPoint = startingPoint;
        NonDecisionTime = nonDecisionTime;
        Noise = noise;
    }

    /// <summary>Дрейф v</summary>
    public double Drift { get; }

    /// <summary>Расстояние между границами a</summary>
    public double BoundarySeparation { get; }

    /// <summary>Относительная начальная точка w</summary>
    public double StartingPoint { get; }

    /// <summary>Время вне решения t₀, с</summary>
    public double NonDecisionTime { get; }

    /// <summary>Коэффициент шума s</summary>
    public double Noise { get; }

    private double Start => StartingPoint * BoundarySeparation;

    /// <summary>Вероятность достичь верхней границы</summary>
    public double UpperProbability
    {
        get
        {
            double k = 2 * Drift / (Noise * Noise);
            double a = BoundarySeparation, z = Start;

            if (Math.Abs(k * a) < 1e-9)
                return StartingPoint;

            // Две формы одного выражения: каждая устойчива при своём знаке дрейфа
            return k > 0
                ? double.ExpM1(-k * z) / double.ExpM1(-k * a)
                : Math.Exp(k * (a - z)) * double.ExpM1(k * z) / double.ExpM1(k * a);
        }
    }

    /// <summary>Среднее время решения без учёта t₀ — по всем ответам вместе</summary>
    public double MeanDecisionTime
    {
        get
        {
            double a = BoundarySeparation, z = Start, s2 = Noise * Noise;

            return Math.Abs(2 * Drift * a / s2) < 1e-6
                ? z * (a - z) / s2
                : ((a * UpperProbability) - z) / Drift;
        }
    }

    /// <summary>Среднее время ответа: время решения плюс t₀</summary>
    public double MeanResponseTime => MeanDecisionTime + NonDecisionTime;

    /// <summary>
    /// Дисперсия времени решения — только для начала посередине, w = 0,5
    /// </summary>
    /// <remarks>
    /// При w = 0,5 времена верных и ошибочных ответов распределены одинаково, и дисперсия одна на оба
    /// исхода. Для смещённого начала замкнутая формула громоздка и здесь не приведена.
    /// </remarks>
    /// <exception cref="NotSupportedException">Начальная точка не посередине</exception>
    public double DecisionTimeVariance
    {
        get
        {
            if (Math.Abs(StartingPoint - 0.5) > 1e-12)
                throw new NotSupportedException("Дисперсия в замкнутом виде реализована только для начала посередине (w = 0,5)");

            double a = BoundarySeparation, v = Drift, s2 = Noise * Noise;
            double y = -v * a / s2;

            if (Math.Abs(y) < 1e-4)
                return Math.Pow(a, 4) / (24 * s2 * s2);

            // Та же дробь, делённая на e^(2y) при y > 0, чтобы не переполняться
            double ratio = y > 0
                ? ((2 * y * Math.Exp(-y)) - 1 + Math.Exp(-2 * y)) / Math.Pow(1 + Math.Exp(-y), 2)
                : ((2 * y * Math.Exp(y)) - Math.Exp(2 * y) + 1) / Math.Pow(Math.Exp(y) + 1, 2);

            return a * s2 / (2 * v * v * v) * ratio;
        }
    }

    /// <summary>Плотность времени решения у нижней границы в момент t (без t₀)</summary>
    /// <param name="decisionTime">Время решения, с</param>
    public double LowerDensity(double decisionTime) => Density(decisionTime, Drift, StartingPoint);

    /// <summary>Плотность времени решения у верхней границы в момент t (без t₀)</summary>
    /// <param name="decisionTime">Время решения, с</param>
    public double UpperDensity(double decisionTime) => Density(decisionTime, -Drift, 1 - StartingPoint);

    /// <summary>Разыгрывает одну пробу методом Эйлера — Маруямы</summary>
    /// <param name="random">Генератор</param>
    /// <param name="timeStep">Шаг по времени, с; у границ он даёт небольшое запаздывание порядка s·√dt</param>
    public DiffusionTrial Simulate(Random random, double timeStep = 1e-4)
    {
        ArgumentNullException.ThrowIfNull(random);
        Numerics.RequirePositive(timeStep, nameof(timeStep));

        double a = BoundarySeparation;
        double x = Start, t = 0;
        double diffusion = Noise * Math.Sqrt(timeStep);
        double drift = Drift * timeStep;

        while (x > 0 && x < a)
        {
            x += drift + (diffusion * RandomEngine.NextGaussian(random));
            t += timeStep;
        }

        return new DiffusionTrial(x >= a, t + NonDecisionTime);
    }

    /// <summary>Разыгрывает серию проб</summary>
    /// <param name="trials">Число проб</param>
    /// <param name="random">Генератор</param>
    /// <param name="timeStep">Шаг по времени, с</param>
    public IReadOnlyList<DiffusionTrial> Simulate(int trials, Random random, double timeStep = 1e-4)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trials);

        return Enumerable.Range(0, trials).Select(_ => Simulate(random, timeStep)).ToList();
    }

    private double Density(double time, double drift, double startingPoint)
    {
        if (!(time > 0))
            return 0;

        // Переход к единичному шуму: v и a делятся на s, время не меняется
        double a = BoundarySeparation / Noise, v = drift / Noise;
        double u = time / (a * a);
        double sum = 0;

        for (int k = 1; k <= 200_000; k++)
        {
            double envelope = k * Math.Exp(-k * k * Math.PI * Math.PI * u / 2);
            sum += envelope * Math.Sin(k * Math.PI * startingPoint);

            if (k * k * Math.PI * Math.PI * u > 2 && envelope < 1e-16 * Math.Max(Math.Abs(sum), 1e-300))
                break;
        }

        return Math.PI / (a * a) * Math.Exp((-v * a * startingPoint) - (v * v * time / 2)) * sum;
    }
}

/// <summary>
/// EZ-диффузия (Вагенмакерс, ван дер Маас и Грасман, 2007): параметры модели диффузии в замкнутом
/// виде по трём числам — точности, дисперсии и среднему времени верных ответов.
/// </summary>
/// <remarks>
/// Метод предполагает начало посередине и отсутствие разброса параметров от пробы к пробе. Точность
/// должна лежать строго между 0,5 и 1: при безошибочной работе нужна поправка доли, а при точности
/// не выше случайной дрейф не определён.
/// </remarks>
public static class EzDiffusion
{
    /// <summary>Оценивает дрейф, расстояние между границами и время вне решения</summary>
    /// <param name="accuracy">Доля верных ответов</param>
    /// <param name="correctTimeVariance">Дисперсия времени верных ответов, с²</param>
    /// <param name="correctTimeMean">Среднее время верных ответов, с</param>
    /// <param name="noise">Коэффициент шума s</param>
    public static DriftDiffusionModel Estimate(double accuracy, double correctTimeVariance, double correctTimeMean, double noise = 1.0)
    {
        if (!(accuracy > 0.5 && accuracy < 1))
            throw new ArgumentOutOfRangeException(nameof(accuracy), "EZ-диффузия требует точности строго между 0,5 и 1");

        Numerics.RequirePositive(correctTimeVariance, nameof(correctTimeVariance));
        Numerics.RequirePositive(correctTimeMean, nameof(correctTimeMean));
        Numerics.RequirePositive(noise, nameof(noise));

        double s2 = noise * noise;
        double logit = Math.Log(accuracy / (1 - accuracy));
        double x = logit * ((logit * accuracy * accuracy) - (logit * accuracy) + accuracy - 0.5) / correctTimeVariance;

        if (!(x > 0))
            throw new ArgumentException("Точность и дисперсия несовместимы с моделью диффузии");

        double drift = noise * Math.Pow(x, 0.25);
        double separation = s2 * logit / drift;
        double y = -drift * separation / s2;
        double decision = separation / (2 * drift) * (1 - Math.Exp(y)) / (1 + Math.Exp(y));
        double nonDecision = correctTimeMean - decision;

        if (nonDecision < 0)
            throw new ArgumentException("Среднее время меньше времени решения, которого требуют точность и дисперсия: данные несовместимы");

        return new DriftDiffusionModel(drift, separation, 0.5, nonDecision, noise);
    }
}

using AI.DataStructs.Algebraic;

namespace AI.Statistics.Distributions;

/// <summary>
/// «Связанное» распределение — параметры уже зафиксированы внутри
/// экземпляра. Используется для смесей, байесовского вывода и
/// любых композиций (MixtureModel сам реализует этот интерфейс —
/// это делает конструкцию фрактальной: смесь смесей валидна).
/// </summary>
public interface IDistributionWithoutParams
{
    /// <summary>Плотность в многомерной точке.</summary>
    double CulcProb(Vector x);

    /// <summary>Плотность в одномерной точке.</summary>
    double CulcProb(double x);

    /// <summary>Лог-плотность в одномерной точке.</summary>
    double CulcLogProb(double x);

    /// <summary>Лог-плотность в многомерной точке.</summary>
    double CulcLogProb(Vector x);

    // ---- Единообразные алиасы (default interface methods). ----

    /// <summary>Алиас <see cref="CulcProb(Vector)"/>.</summary>
    double CalcProb(Vector x) => CulcProb(x);

    /// <summary>Алиас <see cref="CulcProb(double)"/>.</summary>
    double CalcProb(double x) => CulcProb(x);

    /// <summary>Алиас <see cref="CulcLogProb(double)"/>.</summary>
    double CalcLogProb(double x) => CulcLogProb(x);

    /// <summary>Алиас <see cref="CulcLogProb(Vector)"/>.</summary>
    double CalcLogProb(Vector x) => CulcLogProb(x);
}

/// <summary>
/// Опциональный контракт: распределение умеет генерировать
/// собственную случайную выборку. Полезно для смесей (быстрый
/// sample без MCMC) и для прямого сэмплирования.
/// </summary>
public interface ISamplableDistribution
{
    /// <summary>
    /// Одна одномерная реализация. Для ND-распределений должен
    /// бросать <see cref="System.NotSupportedException"/>.
    /// </summary>
    double Sample1D(System.Random rng);

    /// <summary>
    /// Одна многомерная реализация. Для 1D-распределений должен
    /// бросать <see cref="System.NotSupportedException"/>.
    /// </summary>
    Vector SampleND(System.Random rng);

    /// <summary>
    /// true -> объект самплирует одномерно, false -> многомерно.
    /// </summary>
    bool IsOneDimensional { get; }
}

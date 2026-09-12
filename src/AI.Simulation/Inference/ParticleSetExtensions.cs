using AI.DataStructs.Algebraic;
using AI.Statistics;

namespace AI.Simulation.Inference;

/// <summary>
/// Сводки облака частиц — ответы на вопросы о состоянии: среднее, интервал, вероятность события, направление
/// </summary>
public static class ParticleSetExtensions
{
    /// <summary>Взвешенное среднее величины по облаку</summary>
    /// <param name="particles">Облако</param>
    /// <param name="quantity">Величина, вычисляемая по состоянию</param>
    public static double Mean<TState>(this ParticleSet<TState> particles, Func<TState, double> quantity) =>
        WeightedStatistics.Mean(Project(particles, quantity), particles.Weights);

    /// <summary>Взвешенная дисперсия величины по облаку</summary>
    /// <param name="particles">Облако</param>
    /// <param name="quantity">Величина, вычисляемая по состоянию</param>
    public static double Variance<TState>(this ParticleSet<TState> particles, Func<TState, double> quantity) =>
        WeightedStatistics.Variance(Project(particles, quantity), particles.Weights);

    /// <summary>Взвешенный квантиль величины; квантили 0.05 и 0.95 дают 90%-интервал</summary>
    /// <param name="particles">Облако</param>
    /// <param name="quantity">Величина, вычисляемая по состоянию</param>
    /// <param name="q">Уровень на [0; 1]</param>
    public static double Quantile<TState>(this ParticleSet<TState> particles, Func<TState, double> quantity, double q) =>
        WeightedStatistics.Quantile(Project(particles, quantity), particles.Weights, q);

    /// <summary>Вероятность события — доля веса частиц, в которых оно выполнено</summary>
    /// <param name="particles">Облако</param>
    /// <param name="predicate">Событие</param>
    public static double Probability<TState>(this ParticleSet<TState> particles, Func<TState, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(particles);
        ArgumentNullException.ThrowIfNull(predicate);

        Vector weights = particles.Weights;
        double probability = 0;
        for (int i = 0; i < particles.Count; i++)
        {
            if (predicate(particles.States[i]))
                probability += weights[i];
        }

        return probability;
    }

    /// <summary>Среднее направление угловой величины, радианы на [0; 2π)</summary>
    /// <param name="particles">Облако</param>
    /// <param name="angle">Угол, вычисляемый по состоянию, радианы</param>
    public static double MeanDirection<TState>(this ParticleSet<TState> particles, Func<TState, double> angle) =>
        CircularStatistics.MeanDirection(Project(particles, angle), particles.Weights);

    /// <summary>Сосредоточенность угловой величины R ∈ [0; 1]: около 0 — направление не определено</summary>
    /// <param name="particles">Облако</param>
    /// <param name="angle">Угол, вычисляемый по состоянию, радианы</param>
    public static double MeanResultantLength<TState>(this ParticleSet<TState> particles, Func<TState, double> angle) =>
        CircularStatistics.MeanResultantLength(Project(particles, angle), particles.Weights);

    private static Vector Project<TState>(ParticleSet<TState> particles, Func<TState, double> quantity)
    {
        ArgumentNullException.ThrowIfNull(particles);
        ArgumentNullException.ThrowIfNull(quantity);

        return new Vector(particles.States.Select(quantity));
    }
}

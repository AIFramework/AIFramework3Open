using System;

namespace AI.Statistics.MonteCarlo;

/// <summary>
/// Метод имитации отжига: принимает/отвергает кандидата с
/// вероятностью exp(ΔE/T), температура экспоненциально убывает по
/// коэффициенту <see cref="Kt"/>.
/// 
/// Потокобезопасность: каждый экземпляр хранит собственный RNG;
/// один экземпляр не может шариться между потоками.
/// </summary>
[Serializable]
public class SimulatedAnnealing
{
    private readonly Random _rnd;

    /// <summary>Лучший найденный лосс (или последний принятый).</summary>
    public double LastLoss { get; set; }

    /// <summary>Температура.</summary>
    public double T { get; set; } = 50;

    /// <summary>
    /// Множитель охлаждения (T <- T / Kt). Kt &gt; 1 -> температура
    /// падает, Kt = 1 -> замороженный отжиг.
    /// </summary>
    public double Kt { get; set; } = 1.7;

    /// <summary>Минимальная допустимая температура (от деления на 0).</summary>
    public double TMin { get; set; } = 1e-12;

    /// <summary>
    /// </summary>
    /// <param name="startLoss">Начальный лосс</param>
    /// <param name="seed">Зерно (−1 = случайное)</param>
    public SimulatedAnnealing(double startLoss, int seed = -1)
    {
        LastLoss = startLoss;
        _rnd = seed == -1 ? RandomEngine.Create() : RandomEngine.Create(seed);
    }

    /// <summary>
    /// Принимаем ли новое решение. Охлаждение выполняется после
    /// проверки.
    /// </summary>
    public bool IsAccept(double newLoss)
    {
        double dif = LastLoss - newLoss;
        double t = Math.Max(T, TMin);

        // всегда принимаем улучшение (dif > 0 -> exp > 1)
        bool isAccept = dif >= 0 || Math.Exp(dif / t) > _rnd.NextDouble();

        if (isAccept) LastLoss = newLoss;
        T = t / Kt;
        return isAccept;
    }
}

using System;

namespace AI.ControlSystems.Pid;

/// <summary>Ограничение скорости изменения сигнала (за шаг).</summary>
[Serializable]
public sealed class SlewRateLimiter
{
    /// <summary>Минимальное изменение за один вызов (обычно ≤ 0).</summary>
    public double MinDeltaPerStep { get; set; }

    /// <summary>Максимальное изменение за один вызов (≥ 0).</summary>
    public double MaxDeltaPerStep { get; set; }

    /// <summary>Последнее применённое значение.</summary>
    public double LastOutput { get; private set; }

    public SlewRateLimiter(double minDeltaPerStep, double maxDeltaPerStep)
    {
        if (minDeltaPerStep > maxDeltaPerStep)
            throw new ArgumentException("MinDeltaPerStep не должен превышать MaxDeltaPerStep.");
        MinDeltaPerStep = minDeltaPerStep;
        MaxDeltaPerStep = maxDeltaPerStep;
    }

    public void Reset(double initialOutput = 0) => LastOutput = initialOutput;

    /// <summary>Ограничивает желаемое значение относительно <see cref="LastOutput"/>.</summary>
    public double Limit(double desired)
    {
        double delta = desired - LastOutput;
        if (delta < MinDeltaPerStep)
            delta = MinDeltaPerStep;
        if (delta > MaxDeltaPerStep)
            delta = MaxDeltaPerStep;
        LastOutput += delta;
        return LastOutput;
    }
}

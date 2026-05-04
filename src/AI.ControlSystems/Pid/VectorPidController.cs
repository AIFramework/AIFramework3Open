using System;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Pid;

/// <summary>
/// Набор независимых SISO PID по каналам (многомерная уставка и измерение, раздельные коэффициенты на канал).
/// </summary>
[Serializable]
public sealed class VectorPidController
{
    private readonly PidController[] _channels;

    /// <param name="channelCount">Число каналов (размерность векторов).</param>
    public VectorPidController(int channelCount)
    {
        if (channelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(channelCount));

        _channels = new PidController[channelCount];
        for (int i = 0; i < channelCount; i++)
            _channels[i] = new PidController();
    }

    /// <summary>Доступ к регулятору канала для настройки Kp, Ki, Kd и флагов.</summary>
    public PidController this[int channel] => _channels[channel];

    /// <summary>Число каналов (размерность векторов уставки и измерения).</summary>
    public int ChannelCount => _channels.Length;

    /// <summary>Сброс интегралов и истории по всем каналам.</summary>
    public void Reset()
    {
        for (int i = 0; i < _channels.Length; i++)
            _channels[i].Reset();
    }

    /// <summary>Поканальный расчёт управления.</summary>
    public Vector Compute(Vector setpoint, Vector measured, double dt)
    {
        if (setpoint == null || measured == null)
            throw new ArgumentNullException();
        if (setpoint.Count != measured.Count || setpoint.Count != _channels.Length)
            throw new ArgumentException("Размерности setpoint, measured и числа каналов должны совпадать.");

        var output = new Vector(_channels.Length);
        for (int i = 0; i < _channels.Length; i++)
            output[i] = _channels[i].Compute(setpoint[i], measured[i], dt);

        return output;
    }
}

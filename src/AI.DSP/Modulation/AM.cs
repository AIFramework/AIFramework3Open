using AI.DataStructs.Algebraic;
using AI.DSP.DSPCore;
using System;

namespace AI.DSP.Modulation;

/// <summary>
/// Амплитудная модуляция
/// </summary>
[Serializable]
public class AM : IModulator
{
    private readonly double _dt, _f0, _m, _2pi;
    private readonly int _fd;

    /// <summary>
    /// Инициализация модулятора амплитудной модуляции
    /// </summary>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="f0">Несущая частота</param>
    /// <param name="m">Коэффициент модуляции</param>
    public AM(int fd, double f0, double m = 1)
    {
        if (fd <= 0) throw new ArgumentException("Частота дискретизации должна быть положительной", nameof(fd));
        _2pi = Math.PI * 2;
        _fd = fd;
        _dt = 1.0 / fd;
        _f0 = f0;
        _m = m;
    }

    /// <summary>
    /// Модуляция сигнала
    /// </summary>
    /// <param name="signalIn">Входной канал</param>
    /// <returns>Модулированный канал</returns>
    public Channel Modulate(Channel signalIn)
    {
        if (_fd != signalIn.Fd)
            throw new ArgumentException("Не совпадают частоты дискретизации");
        return ModulateSimple(signalIn);
    }

    /// <summary>
    /// Демодуляция АМ-сигнала
    /// </summary>
    /// <param name="channel">Канал с модулированным сигналом</param>
    /// <returns>Демодулированный канал</returns>
    public Channel Demodulate(Channel channel)
    {
        Vector dat = FastHilbert.EnvelopeIQ(channel.ChData, _fd, _f0);
        dat -= dat.Min();
        double max = dat.Max();
        if (max > 1e-30)
            dat /= max;
        return new Channel(dat, channel.Fd);
    }

    private Channel ModulateSimple(Channel signalIn)
    {
        Vector data = signalIn.ChData.Clone();
        double maxAbs = data.MaxAbs();
        if (maxAbs < 1e-30)
            return new Channel(new Vector(data.Count), _fd, signalIn.Name, signalIn.Description) { ScaleVolt = signalIn.ScaleVolt };

        double mDivDataMax = _m / maxAbs;
        double n = 1.0 + _m;

        Vector outp = new Vector(data.Count);

        for (int i = 0; i < outp.Count; i++)
            outp[i] = (1 + (data[i] * mDivDataMax)) * Math.Sin(_2pi * i * _dt * _f0) / n;

        return new Channel(outp, _fd, signalIn.Name, signalIn.Description) { ScaleVolt = signalIn.ScaleVolt };
    }
}

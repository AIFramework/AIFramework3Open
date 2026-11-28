using System;

namespace AI.DSP.Multirate;

/// <summary>
/// K каскадов скользящей суммы длины N, разностное уравнение (1.1): y[n] = y[n−1] + x[n] − x[n−N].
/// Форма ИГФ для вещественной арифметики: состояние каждого каскада ограничено, тогда как интеграторы
/// Хогенауэра в double растут как n^K и теряют точность. В целых числах по модулю 2^64 обе формы совпадают.
/// </summary>
internal sealed class MovingSumCascade<T>
    where T : struct
{
    private readonly ISampleArithmetic<T> _math;
    private readonly T[][] _delay;
    private readonly T[] _sum;
    private int _pos;

    public MovingSumCascade(ISampleArithmetic<T> math, int length, int stages)
    {
        _math = math;
        _delay = new T[stages][];
        for (int k = 0; k < stages; k++)
        {
            _delay[k] = new T[length];
        }

        _sum = new T[stages];
    }

    public T Push(T x)
    {
        for (int k = 0; k < _sum.Length; k++)
        {
            T old = _delay[k][_pos];
            _delay[k][_pos] = x;
            _sum[k] = _math.Subtract(_math.Add(_sum[k], x), old);
            x = _sum[k];
        }

        _pos = _pos + 1 < _delay[0].Length ? _pos + 1 : 0;
        return x;
    }

    public void Reset()
    {
        foreach (T[] d in _delay)
        {
            Array.Clear(d);
        }

        Array.Clear(_sum);
        _pos = 0;
    }
}

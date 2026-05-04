using AI.Statistics;
using System;
using System.Threading;

namespace AI.SignalLab.Generators;

/// <summary>
/// Базовый генератор цифрового сигнала
/// </summary>
public class BaseGenerator : IDisposable
{
    /// <summary>
    /// Период через который обновлять сигнал
    /// </summary>
    public int PeriodMS { get; set; } = 20;
    /// <summary>
    /// Функция генерации сигнала
    /// </summary>
    public Func<int, double> Signal = (i) => i / 1000.0;
    /// <summary>
    /// Коэф. аддитивного шума
    /// </summary>
    public double AdditivNoiseKoef { get; set; } = 0;
    /// <summary>
    /// Коэф. мультипликативного шума
    /// </summary>
    public double MultiplNoiseKoef { get; set; } = 0;
    /// <summary>
    /// Коэф. аддитивного тренда
    /// </summary>
    public double AdditivTrend { get; set; } = 0;
    /// <summary>
    /// Коэф. мультипликативного тренда
    /// </summary>
    public double MultiplTrend { get; set; } = 0;
    /// <summary>
    /// Генератор аддитивного шума
    /// </summary>
    public Func<Random, double> NoiseAdditivGenerator { get; set; } = Statistic.Gauss;
    /// <summary>
    /// Генератор мультипликативного шума
    /// </summary>
    public Func<Random, double> NoiseMultiplGenerator { get; set; } = Statistic.Gauss;


    int _counter = 0;
    Thread _thread;
    Random _random = new Random();

    public void SetPosition(int start) 
    {
        _counter = start;
    }

    /// <summary>
    /// Запуск генератора
    /// </summary>
    public void Start() 
    {
        SignalSemple += BaseGenerator_SignalSemple;
        _thread = new Thread(CalcSignal);
        _thread.Start();
    }


    public void Reset() 
    {
        SetPosition(0);
    }


    public void CalcSignal() 
    {
        while (true)
        {
            double t = _counter / 1000.0;
            double s = Signal(_counter)*(MultiplNoiseKoef* NoiseMultiplGenerator(_random)+1);
            s += AdditivNoiseKoef * NoiseAdditivGenerator(_random);
            s *= MultiplTrend * t + 1;
            s += AdditivTrend * t;
            _counter++;


            SignalSemple(s);
            Thread.Sleep(PeriodMS);
        }
    }

   

    public event Action<double> SignalSemple;


    private void BaseGenerator_SignalSemple(double obj) { }


    public void Dispose()
    {
        _thread.Abort();
    }
}

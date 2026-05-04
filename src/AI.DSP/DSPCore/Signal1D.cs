using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.DSP.Analyse;
using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using System.Collections.Generic;

namespace AI.DSP.DSPCore;

/// <summary>
/// Основной класс для одномерного многоканального сигнала
/// </summary>
[Serializable]
public class Signal1D : List<Channel>
{
    /// <summary>
    /// Имя сигнала
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Описание сигнала
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Частота дискретизации
    /// </summary>
    public int Fd { get; set; }
    private FFT fur;
    private int _n;
    /// <summary>
    /// Шаг по времени
    /// </summary>
    public double Dt => 1.0 / Fd;

    /// <summary>
    /// Масштаб в вольтах
    /// </summary>
    public TypeScaleVolt ScaleVolt
    {
        get => Count > 0 ? this[0].ScaleVolt : TypeScaleVolt.V;
        set
        {
            for (int i = 0; i < Count; i++)
                this[i].ScaleVolt = value;
        }
    }

    /// <summary>
    /// Инициализация многоканальным сигналом
    /// </summary>
    /// <param name="channels">Массив каналов</param>
    /// <param name="fd">Частота дискретизации</param>
    public Signal1D(Vector[] channels, int fd)
    {
        AddRange(Channel.GetChannels(channels, fd));
        fur = new FFT(channels[0].Count);
        _n = fur.SamplesCount;
        Fd = fd;
    }
    /// <summary>
    /// Создает пустой список каналов
    /// </summary>
    public Signal1D()
    {
    }
    /// <summary>
    /// Инициализация одноканальным сигналом
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    public Signal1D(Vector signal, int fd)
    {
        Add(new Channel(signal, fd));
    }
    /// <summary>
    /// Инициализация каналом
    /// </summary>
    /// <param name="signal">Канал</param>
    public Signal1D(Channel signal)
    {
        Add(signal);
    }

    /// <summary>
    /// Добавление канала
    /// </summary>
    /// <param name="signal">Канал</param>
    public new void Add(Channel signal)
    {
        if (Count == 0)
        {
            fur = new FFT(signal.ChData.Count);
            _n = fur.SamplesCount;
            Fd = signal.Fd;
            ScaleVolt = signal.ScaleVolt;
            base.Add(signal);
        }
        else
        {
            if (signal.ScaleVolt != ScaleVolt)
                _ = signal.ConvertVolt(ScaleVolt);

            base.Add(signal);
        }
    }
    /// <summary>
    /// Тренды сигнала
    /// </summary>
    public Signal1D Trends()
    {
        Vector[] vcs = new Vector[Count];
        Vector time = Time();

        for (int i = 0; i < vcs.Length; i++)
        {
            Trend lr = new Trend(time, this[i].ChData);
            vcs[i] = lr.Predict(time);
        }

        return new Signal1D(vcs, Fd);
    }
    /// <summary>
    /// Сигнал без тренда
    /// </summary>
    public Signal1D SignalWithoutTrend()
    {
        Signal1D trends = Trends();
        Vector[] vcs = new Vector[Count];

        for (int i = 0; i < Count; i++)
            vcs[i] = this[i].ChData - trends[i].ChData;

        return new Signal1D(vcs, Fd);
    }
    /// <summary>
    /// Сигнал с нулевым мат. ожиданием и СКО=1 (вычитается тренд)
    /// </summary>
    public Signal1D SignalWithM0Std1Trend()
    {
        Signal1D withOutTrends = SignalWithoutTrend();
        Vector[] vcs = new Vector[Count];

        for (int i = 0; i < Count; i++)
        {
            double std = Statistic.CalcStd(withOutTrends[i].ChData);
            vcs[i] = std < 1e-30 ? new Vector(withOutTrends[i].ChData.Count) : withOutTrends[i].ChData / std;
        }

        return new Signal1D(vcs, Fd);
    }
    /// <summary>
    /// Сигнал с нулевым мат. ожиданием и СКО=1 (вычитается среднее)
    /// </summary>
    public Signal1D SignalWithM0Std1()
    {
        Vector[] vcs = new Vector[Count];

        for (int i = 0; i < Count; i++)
        {
            vcs[i] = this[i].ChData - Statistic.ExpectedValue(this[i].ChData);
            double std = Statistic.CalcStd(vcs[i]);
            if (std < 1e-30) vcs[i] = new Vector(vcs[i].Count);
            else vcs[i] /= std;
        }

        return new Signal1D(vcs, Fd);
    }
    /// <summary>
    /// Рассчитывает спектр
    /// </summary>
    /// <param name="numCh">Номер канала</param>
    /// <returns>Амплитудный спектр частоты 0..fd/2</returns>
    public Vector GetSpectr(int numCh = 0)
    {
        ComplexVector cv = fur.CalcFFT(this[numCh].ChData);
        Vector sp = cv.MagnitudeVector / _n;
        sp *= 2;
        sp = sp.CutAndZero(_n / 2);
        return sp;
    }
    /// <summary>
    /// Рассчитывает спектр по всем каналам
    /// </summary>
    /// <returns>Массив амплитудных спектров</returns>
    public Vector[] GetSpectrAll()
    {
        Vector[] vcs = new Vector[Count];

        for (int i = 0; i < Count; i++)
            vcs[i] = GetSpectr(i);

        return vcs;
    }
    /// <summary>
    /// Корреляционная матрица по каналам
    /// </summary>
    /// <returns>Матрица корреляций</returns>
    public Matrix CorrelationMatrix()
    {
        return Matrix.GetCorrelationMatrixNorm(Channel.ChansToVects(ToArray()));
    }
    /// <summary>
    /// Корреляционная матрица амплитудных спектров
    /// </summary>
    /// <returns>Матрица корреляций</returns>
    public Matrix CorrelationMatrixSpectr()
    {
        return Matrix.GetCorrelationMatrixNorm(GetSpectrAll());
    }
    /// <summary>
    /// Коэффициент связи между каналами (1 - det(R))
    /// </summary>
    /// <returns>Коэффициент связи [0,1]: близко к 1 — сильная связь</returns>
    public double CouplingCoefficient()
    {
        return 1 - CorrelationMatrix().Determinant;
    }
    /// <summary>
    /// Коэффициент связи между спектрами каналов (1 - det(R))
    /// </summary>
    /// <returns>Коэффициент связи [0,1]: близко к 1 — сильная связь</returns>
    public double CouplingCoefficientSp()
    {
        return 1 - CorrelationMatrixSpectr().Determinant;
    }
    /// <summary>
    /// Генерация отсчетов времени
    /// </summary>
    /// <returns>Отсчеты времени</returns>
    public Vector Time()
    {
        double endT = this[0].ChData.Count / (double)Fd;
        return FunctionsForEachElements.GenerateTheSequence(0, Dt, endT).CutAndZero(this[0].ChData.Count);
    }
    /// <summary>
    /// Генерация отсчетов частоты
    /// </summary>
    /// <returns>Отсчеты частоты</returns>
    public Vector Freq()
    {
        return Signal.Frequency(_n, Fd).CutAndZero(_n / 2);
    }
    /// <summary>
    /// Конвертирование шкалы напряжения
    /// </summary>
    /// <param name="typeScaleVolt">Новый масштаб</param>
    public Signal1D ConvertVolt(TypeScaleVolt typeScaleVolt)
    {
        Signal1D retObj = new Signal1D();
        for (int i = 0; i < Count; i++)
            retObj.Add(this[i].ConvertVolt(typeScaleVolt));

        return retObj;
    }
    /// <summary>
    /// Фильтрация сигнала
    /// </summary>
    /// <param name="filter">Фильтр</param>
    public Signal1D Filtration(IFilter filter)
    {
        Signal1D retObj = new Signal1D();
        for (int i = 0; i < Count; i++)
            retObj.Add(this[i].Filtration(filter));

        return retObj;
    }
    /// <summary>
    /// Единица измерения шкалы Y
    /// </summary>
    /// <returns>Название единицы</returns>
    public string YName() => ScaleVolt switch
    {
        TypeScaleVolt.kV => "кВ",
        TypeScaleVolt.V => "В",
        TypeScaleVolt.mV => "мВ",
        TypeScaleVolt.uV => "мкВ",
        TypeScaleVolt.nV => "нВ",
        _ => "",
    };
    /// <summary>
    /// Список имен каналов
    /// </summary>
    /// <returns>Массив имён</returns>
    public string[] ChannelNames()
    {
        string[] names = new string[Count];

        for (int i = 0; i < names.Length; i++)
            names[i] = this[i].Name;

        return names;
    }
}

using AI.DataStructs.Algebraic;
using AI.DSP.IIR;
using System;

namespace AI.SignalLab.AGC;

/// <summary>
/// Прямая АРУ
/// </summary>
[Serializable]
public class DirectAGC : IAGC
{
    /// <summary>
    /// Фильтр среднего
    /// </summary>
    public IIRFilter IIRFilterMean { get; private set; }
    /// <summary>
    /// Фильтр STD
    /// </summary>
    public IIRFilter IIRFilterSTD { get; private set; }

    /// <summary>
    /// Ограничение уровня сигнала в фильтре
    /// </summary>
    public double TresholdFilter
    {
        get
        {
            return IIRFilterMean.Threshold;
        }
        set
        {
            IIRFilterMean.Threshold = value;
            IIRFilterSTD.Threshold = value;
        }
    }

    /// <summary>
    /// Ограничение уровня сигнала в АРУ
    /// </summary>
    public double TresholdAGC { get; set; } = 4;


    /// <summary>
    /// Прямая АРУ
    /// </summary>
    /// <param name="path">Путь до синтезированного фильтра</param>
    public DirectAGC(string path)
    {
        IIRFilterMean = IIRFilter.Load(path);
        IIRFilterSTD = IIRFilter.Load(path);
        //Treshold = 100;
    }

    /// <summary>
    /// Прямая АРУ
    /// </summary>
    public DirectAGC()
    {
        IIRFilterMean = IIRFilter.Load(filters.filter);
        IIRFilterSTD = IIRFilter.Load(filters.filter);
    }

    /// <summary>
    /// Прямая АРУ
    /// </summary>
    /// <param name="kefA">Коэфициенты А ФНЧ</param>
    /// <param name="kefB">Коэфициенты B ФНЧ</param>
    public DirectAGC(Vector kefA, Vector kefB)
    {
        IIRFilterMean = new IIRFilter(kefA, kefB);
        IIRFilterSTD = new IIRFilter(kefA, kefB);
    }

    /// <summary>
    /// Выход АРУ
    /// </summary>
    /// <param name="value">АРУ</param>
    public virtual double Calculate(double value)
    {
        var filterValue1 = IIRFilterMean.FilterOutp(value);
        var dif = (value - filterValue1);
        var s = dif * dif;
        var filterValue2 = Math.Abs(IIRFilterSTD.FilterOutp(s));
        var outp = dif / (Math.Sqrt(filterValue2) + AISettings.GlobalEps);


        return OutpClip(outp);
    }

    public virtual double OutpClip(double outp) 
    {
        double res = outp;
        //Ограничение сигнала
        if (outp > TresholdAGC) res = TresholdAGC;
        else if (outp < -TresholdAGC) res = -TresholdAGC;
        return res;
    }
}

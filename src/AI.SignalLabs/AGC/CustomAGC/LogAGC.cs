using System;

namespace AI.SignalLab.AGC.CustomAGC;

/// <summary>
/// Логарифмическая АРУ
/// </summary>
public class LogAGC : DirectAGC
{
    public LogAGC() : base() { }

    public override double Calculate(double value)
    {
        var filterValue1 = IIRFilterMean.FilterOutp(value);
        var dif = value - filterValue1;
        var sLog = Math.Log((dif * dif) + AISettings.GlobalEps);

        var filterValue2 = IIRFilterSTD.FilterOutp(sLog) / 2;
        var outp = dif / Math.Exp(filterValue2);


        return OutpClip(outp);
    }
}

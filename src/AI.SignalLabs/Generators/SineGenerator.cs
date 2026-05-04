using System;

namespace AI.SignalLab.Generators;

public class SineGenerator : BaseGenerator
{
    public double freq = 20;
    public double phi = 0;
    public double A = 1;

    public SineGenerator() 
    {
        Signal = GetSine;
    }

    double GetSine(int i) 
    {
        return A*Math.Sin(i/500.0*freq*Math.PI+phi);
    }
}

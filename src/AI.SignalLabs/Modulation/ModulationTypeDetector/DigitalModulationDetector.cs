using System;

namespace AI.SignalLab.Modulation.ModulationTypeDetector;

[Serializable]
public class DigitalModulationDetector
{
    int _maxBits;

    public DigitalModulationDetector(int maxBits) 
    {
        _maxBits= maxBits;
    }
}

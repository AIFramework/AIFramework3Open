using AI.DataStructs.Algebraic;
using AI.DSP.Multiray.Sources;
using System;
using System.Collections.Generic;

namespace AI.DSP.Multiray.Reflectors;

/// <summary>
/// Отражатель сигнала (не реализован полностью)
/// </summary>
public class Reflector : Source
{
    /// <summary>
    /// Создание отражателя по координатам
    /// </summary>
    public Reflector(params double[] coords) : base(coords) { }

    /// <summary>
    /// Генерация отражённого сигнала. 
    /// В текущей версии не реализовано — требуется добавить фазовый сдвиг/задержку.
    /// </summary>
    public override Vector GetSignal(double dist, double speed, IEnumerable<Source> sources)
    {
        throw new NotSupportedException("Reflector.GetSignal is not yet implemented. Phase shift logic needs to be added.");
    }
}

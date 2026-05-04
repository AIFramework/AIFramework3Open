using AI.DataStructs.Algebraic;
using System;

namespace AI.DSP.DSPCore;

/// <summary>
/// Равнобедренная трапеция (окно убирающее разрыв фазы)
/// </summary>
public static class PhaseCorrectingWindow
{
    /// <summary>
    /// Равнобедренная трапеция
    /// </summary>
    /// <param name="len">Длина основания</param>
    /// <param name="slope">Доля нарастания/спада (0..0.5)</param>
    /// <returns>Оконная функция в виде вектора</returns>
    public static Vector Trapezoid(int len, double slope = 0.03)
    {
        if (len <= 0)
            return new Vector(0);

        int up = Math.Max((int)(slope * len), 1);
        int down = len - up;
        double k = 1.0 / up;
        Vector outp = new Vector(len);

        for (int i = 0; i < len; i++)
        {
            if (i < up)
                outp[i] = k * i;
            else if (i > down)
                outp[i] = k * (len - i);
            else
                outp[i] = 1;
        }

        return outp;
    }
}

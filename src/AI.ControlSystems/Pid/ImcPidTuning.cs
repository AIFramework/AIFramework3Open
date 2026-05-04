using System;

namespace AI.ControlSystems.Pid;

/// <summary>
/// Настройка PI по IMC для аппроксимации первого порядка K/(τs+1) и фильтра IMC λ (без запаздывания).
/// Параллельная форма: Kp, Ki (Kd = 0).
/// </summary>
public static class ImcPidTuning
{
    /// <summary>
    /// PI-закон u = Kp e + Ki ∫e dt с параметрами IMC: Kp = τ/(K λ), Ki = Kp/τ.
    /// </summary>
    /// <param name="processGain">K (знак учитывает направление реакции).</param>
    /// <param name="timeConstant">τ &gt; 0.</param>
    /// <param name="imcFilterTimeConstant">λ &gt; 0 — желаемая «мягкость» замкнутого контура.</param>
    /// <param name="kp">Выходной параметр: пропорциональный коэффициент Kp.</param>
    /// <param name="ki">Выходной параметр: интегральный коэффициент Ki.</param>
    public static void FirstOrderPi(double processGain, double timeConstant, double imcFilterTimeConstant,
        out double kp, out double ki)
    {
        if (timeConstant <= 0 || imcFilterTimeConstant <= 0)
            throw new ArgumentOutOfRangeException();
        if (Math.Abs(processGain) < 1e-18)
            throw new ArgumentOutOfRangeException(nameof(processGain));

        kp = timeConstant / (processGain * imcFilterTimeConstant);
        ki = kp / timeConstant;
    }
}

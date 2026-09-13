using System;

namespace AI.ControlSystems.Nonlinear;

/// <summary>
/// Скользящий режим второго порядка — алгоритм «суперскручивания» (Левант, 1993) для
/// ṡ = u + d(t) с ограниченной скоростью возмущения |ḋ| ≤ L.
/// </summary>
/// <remarks>
/// <para>
/// Управление <c>u = −α·|s|^{1/2}·sign(s) + v</c>, <c>v̇ = −β·sign(s)</c> непрерывно: разрывен
/// только интегрируемый член, поэтому дребезга, как у реле, нет. При этом s и ṡ сходятся к нулю
/// за конечное время, а интегратор v сам находит и компенсирует возмущение. Достаточные
/// усиления — α = 1,5√L и β = 1,1L (<see cref="ForDisturbanceRate"/>).
/// </para>
/// <para>
/// s — выход относительной степени один: для объекта в пространстве состояний это поверхность
/// скольжения, для скалярного объекта — ошибка слежения. При дискретной реализации с шагом τ
/// точность |s| порядка τ², а не τ, как у реле с пограничным слоем.
/// </para>
/// </remarks>
[Serializable]
public sealed class SuperTwistingController
{
    private double _integral;

    /// <summary>Создаёт регулятор</summary>
    /// <param name="alpha">α &gt; 0 при корне</param>
    /// <param name="beta">β &gt; 0 при интегрируемом знаке</param>
    public SuperTwistingController(double alpha, double beta)
    {
        if (!(alpha > 0) || !(beta > 0))
            throw new ArgumentOutOfRangeException(nameof(alpha), "Усиления α и β должны быть положительными.");

        Alpha = alpha;
        Beta = beta;
    }

    /// <summary>Усиление при корне α</summary>
    public double Alpha { get; }

    /// <summary>Усиление интегратора β</summary>
    public double Beta { get; }

    /// <summary>Накопленная интегральная составляющая v — оценка возмущения со знаком минус</summary>
    public double Integral => _integral;

    /// <summary>Регулятор с достаточными усилениями для возмущения со скоростью не выше L</summary>
    /// <param name="lipschitz">Граница |ḋ|</param>
    public static SuperTwistingController ForDisturbanceRate(double lipschitz)
    {
        if (!(lipschitz > 0))
            throw new ArgumentOutOfRangeException(nameof(lipschitz));

        return new SuperTwistingController(1.5 * Math.Sqrt(lipschitz), 1.1 * lipschitz);
    }

    /// <summary>Сбрасывает интегратор</summary>
    public void Reset() => _integral = 0;

    /// <summary>Один шаг</summary>
    /// <param name="s">Значение поверхности скольжения</param>
    /// <param name="dt">Шаг</param>
    public double Compute(double s, double dt)
    {
        if (!(dt > 0))
            throw new ArgumentOutOfRangeException(nameof(dt));

        double sign = Math.Sign(s);
        double u = (-Alpha * Math.Sqrt(Math.Abs(s)) * sign) + _integral;
        _integral -= Beta * sign * dt;

        return u;
    }
}

using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.MathUtils.ODE;

/// <summary>
/// Solves an ordinary differential equation using the 4th order Runge-Kutta method.
/// </summary>
[Serializable]
public class RungeKutta
{

    public Vector Y { get; set; }
    public Vector X { get; set; }

    public Vector ErrorEstimate { get; set; }

    public RungeKutta() { }


    /// <summary>
    /// Solves an ordinary differential equation using the 4th order Runge-Kutta method.
    /// </summary>
    /// <param name="function">The function representing the right-hand side of the differential equation dy/dx = f(x, y).</param>
    /// <param name="initialX">The initial value of x.</param>
    /// <param name="initialY">The initial value of y.</param>
    /// <param name="finalX">The final value of x for which y is required.</param>
    /// <param name="stepSize">The step size for the iteration.</param>
    /// <returns>The approximate value of y at finalX.</returns>
    public static RungeKutta RungeKutta4(Func<double, double, double> function, double initialX, double initialY, double finalX, double stepSize, bool isHalfStep = false)
    {
        if (stepSize <= 0)
        {
            throw new ArgumentException("Step size must be positive.", nameof(stepSize));
        }

        Vector xGrid = new Vector();
        Vector yGrid = new Vector();
        int index = 0;

        double x = initialX;
        double y = initialY;
        while (x < finalX)
        {
            double k1 = stepSize * function(x, y);
            double k2 = stepSize * function(x + 0.5 * stepSize, y + 0.5 * k1);
            double k3 = stepSize * function(x + 0.5 * stepSize, y + 0.5 * k2);
            double k4 = stepSize * function(x + stepSize, y + k3);

            y += (k1 + 2 * k2 + 2 * k3 + k4) / 6;

            if (!isHalfStep || index % 2 != 0)
            {
                xGrid.Add(x);
                yGrid.Add(y);
            }

            index++;
            x += stepSize;
        }

        return new RungeKutta() { X = xGrid, Y = yGrid };
    }


    /// <summary>
    /// Решает систему обыкновенных дифференциальных уравнений dy/dx = f(x, y)
    /// методом Рунге-Кутты 4-го порядка и возвращает решение в заданных точках.
    /// </summary>
    /// <param name="function">Правая часть системы: (x, y) -> dy/dx</param>
    /// <param name="initialX">Начальное значение независимой переменной</param>
    /// <param name="initialY">Начальный вектор состояния</param>
    /// <param name="outputPoints">Точки, в которых нужно решение (возрастающие, начиная не раньше initialX)</param>
    /// <param name="stepsPerInterval">Число шагов интегрирования между соседними точками вывода</param>
    /// <remarks>
    /// В отличие от скалярной версии решение выдаётся ровно в запрошенных точках:
    /// шаг подбирается под каждый интервал, поэтому сетка вывода не обязана быть равномерной.
    /// </remarks>
    public static Vector[] SolveSystem(
        Func<double, Vector, Vector> function,
        double initialX,
        Vector initialY,
        IReadOnlyList<double> outputPoints,
        int stepsPerInterval = 20)
    {
        if (function == null) throw new ArgumentNullException(nameof(function));
        if (initialY == null) throw new ArgumentNullException(nameof(initialY));
        if (outputPoints == null) throw new ArgumentNullException(nameof(outputPoints));
        if (stepsPerInterval < 1) throw new ArgumentException("Steps per interval must be positive.", nameof(stepsPerInterval));

        var result = new Vector[outputPoints.Count];
        Vector y = initialY.Clone();
        double x = initialX;

        for (int point = 0; point < outputPoints.Count; point++)
        {
            double target = outputPoints[point];

            if (target < x)
                throw new ArgumentException("Output points must be sorted and not precede the initial value.", nameof(outputPoints));

            double span = target - x;

            if (span > 0)
            {
                double step = span / stepsPerInterval;

                for (int i = 0; i < stepsPerInterval; i++)
                {
                    Vector k1 = function(x, y) * step;
                    Vector k2 = function(x + (0.5 * step), y + (k1 * 0.5)) * step;
                    Vector k3 = function(x + (0.5 * step), y + (k2 * 0.5)) * step;
                    Vector k4 = function(x + step, y + k3) * step;

                    y += (k1 + (2 * k2) + (2 * k3) + k4) / 6.0;
                    x += step;
                }

                x = target; // накопленная ошибка шага не должна уводить сетку
            }

            result[point] = y.Clone();
        }

        return result;
    }

    /// <summary>
    /// Estimates the error of the Runge-Kutta 4 method using the Runge-Romberg rule.
    /// </summary>
    /// <param name="function">The function representing the right-hand side of the differential equation dy/dx = f(x, y).</param>
    /// <param name="initialX">The initial value of x.</param>
    /// <param name="initialY">The initial value of y.</param>
    /// <param name="finalX">The final value of x for which y is required.</param>
    /// <param name="stepSize">The step size for the iteration.</param>
    /// <returns>A tuple containing the corrected value of y and the estimated error.</returns>
    public static RungeKutta RungeRombergRK4(Func<double, double, double> function, double initialX, double initialY, double finalX, double stepSize)
    {
        int order = 4;

        var rk = RungeKutta4(function, initialX, initialY, finalX, stepSize / 2, true);
        Vector y1 = RungeKutta4(function, initialX, initialY, finalX, stepSize).Y;

        y1 = y1.CutAndZero(rk.Y.Count);

        rk.ErrorEstimate = (rk.Y - y1) / (Math.Pow(2, order) - 1);
        rk.Y += rk.ErrorEstimate;
        return rk;
    }
}

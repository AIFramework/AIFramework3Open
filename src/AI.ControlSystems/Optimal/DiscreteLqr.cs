using System;
using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;
using Complex = System.Numerics.Complex;

namespace AI.ControlSystems.Optimal;

/// <summary>Результат синтеза LQR</summary>
/// <param name="Gain">Усиление K (m×n) в законе u = −Kx</param>
/// <param name="CostToGo">
/// Матрица P решения Риккати: оптимальная стоимость из состояния x равна xᵀPx; она же — терминальный
/// вес, с которым MPC на любом горизонте совпадает с LQR, пока ограничения не активны
/// </param>
/// <param name="ClosedLoopPoles">Полюса замкнутой системы A − BK</param>
public sealed record LqrDesign(Matrix Gain, Matrix CostToGo, Complex[] ClosedLoopPoles);

/// <summary>
/// Дискретный регулятор LQR: минимизация ∑ (x' Q x + u' R u) при x[k+1] = A x[k] + B u[k].
/// Возвращает матрицу усиления K в законе u = −K x.
/// </summary>
/// <remarks>
/// Уравнение Риккати решает <see cref="RiccatiEquation"/> методом удвоения с проверкой невязки
/// и устойчивости. Прежде здесь были до 500 итераций Риккати, после которых возвращалось то,
/// что получилось, даже если итерации не сошлись.
/// </remarks>
public static class DiscreteLqr
{
    /// <summary>Вычисляет K</summary>
    /// <param name="a">A</param>
    /// <param name="b">B</param>
    /// <param name="q">Q ≥ 0</param>
    /// <param name="r">R &gt; 0</param>
    /// <param name="tolerance">Допустимая относительная невязка уравнения Риккати</param>
    /// <param name="maxIterations">Не используется: сохранён для совместимости, метод удвоения сходится за десятки шагов</param>
    /// <exception cref="InvalidOperationException">Стабилизирующего решения нет</exception>
    public static Matrix Solve(Matrix a, Matrix b, Matrix q, Matrix r, double tolerance = 1e-10, int maxIterations = 500)
        => Design(a, b, q, r, Math.Max(tolerance, 1e-12)).Gain;

    /// <summary>Синтез LQR: усиление, матрица стоимости и полюса замкнутой системы</summary>
    /// <param name="a">A</param>
    /// <param name="b">B</param>
    /// <param name="q">Q ≥ 0</param>
    /// <param name="r">R &gt; 0</param>
    /// <param name="tolerance">Допустимая относительная невязка уравнения Риккати</param>
    public static LqrDesign Design(Matrix a, Matrix b, Matrix q, Matrix r, double tolerance = 1e-9)
    {
        Matrix p = RiccatiEquation.SolveDiscrete(a, b, q, r, tolerance);
        Matrix k = RiccatiEquation.DiscreteGain(a, b, r, p);

        return new LqrDesign(k, p, Eigen.General(a - (b * k)));
    }
}

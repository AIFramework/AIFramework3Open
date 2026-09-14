using System;
using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;

namespace AI.Econometrics.Numerics;

/// <summary>
/// Переходник к публичному методу Нелдера — Мида на массивах <c>double[]</c>.
/// </summary>
/// <remarks>
/// Сам метод живёт в <see cref="AI.Solvers.Optimization.NelderMead"/>: реализация одна, а этот
/// класс оставлен ради сигнатуры, которой пользуются правдоподобия эконометрики и экономики, —
/// чтобы перенос не менял ни одного места вызова и ни одной цифры в их результатах.
/// Безградиентный метод выбран сознательно: правдоподобия BG/NBD, Pareto/NBD и кривых
/// удержания имеют аналитические градиенты, но их вывод хрупок, а размерность задач мала.
/// </remarks>
internal static class NelderMead
{
    /// <summary>Минимизация функции без ограничений.</summary>
    /// <param name="f">Целевая функция.</param>
    /// <param name="start">Начальная точка.</param>
    /// <param name="step">Масштаб начального симплекса.</param>
    /// <param name="maxIter">Максимум итераций.</param>
    /// <param name="tol">Порог сходимости по разбросу значений в симплексе.</param>
    /// <returns>Найденная точка минимума.</returns>
    public static double[] Minimize(
        Func<double[], double> f,
        double[] start,
        double step = 0.25,
        int maxIter = 4000,
        double tol = 1e-10)
        => AI.Solvers.Optimization.NelderMead.Minimize(
            x => f(x.ToArray()),
            new Vector(start),
            new NelderMeadOptions { Step = step, MaxIterations = maxIter, Tolerance = tol }).Point.ToArray();

    /// <summary>
    /// Минимизация по строго положительным параметрам: оптимизация ведётся
    /// по <c>u = ln(theta)</c>, поэтому граница <c>theta &gt; 0</c> недостижима.
    /// </summary>
    /// <param name="f">Целевая функция от исходных (положительных) параметров.</param>
    /// <param name="start">Начальное приближение в исходных координатах.</param>
    /// <param name="maxIter">Максимум итераций.</param>
    /// <returns>Точка минимума в исходных координатах.</returns>
    public static double[] MinimizePositive(Func<double[], double> f, double[] start, int maxIter = 4000)
        => AI.Solvers.Optimization.NelderMead.MinimizePositive(
            x => f(x.ToArray()),
            new Vector(start),
            new NelderMeadOptions { Step = 0.35, MaxIterations = maxIter }).Point.ToArray();
}

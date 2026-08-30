using System;

namespace AI.Econometrics.Numerics;

/// <summary>
/// Симплекс-метод Нелдера — Мида: безградиентная минимизация функции многих переменных.
/// </summary>
/// <remarks>
/// Выбран сознательно вместо градиентных методов: правдоподобия BG/NBD,
/// Pareto/NBD и кривых удержания имеют аналитические градиенты, но их вывод
/// хрупок, а размерность задач мала (2–4 параметра). Безградиентный метод
/// даёт тот же оптимум за микросекунды и не ломается на границе области.
/// Ограничения положительности параметров задаются заменой <c>theta = exp(u)</c>
/// на стороне вызывающего кода — см. <see cref="MinimizePositive"/>.
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
    {
        int n = start.Length;
        var simplex = new double[n + 1][];
        var values = new double[n + 1];

        simplex[0] = (double[])start.Clone();
        for (int i = 0; i < n; i++)
        {
            var p = (double[])start.Clone();
            p[i] += Math.Abs(p[i]) > 1e-8 ? step * Math.Abs(p[i]) : step;
            simplex[i + 1] = p;
        }

        for (int i = 0; i <= n; i++) values[i] = Safe(f, simplex[i]);

        for (int iter = 0; iter < maxIter; iter++)
        {
            Array.Sort(values, simplex);

            if (Math.Abs(values[n] - values[0]) <= tol * (Math.Abs(values[0]) + tol)) break;

            // Центр тяжести всех точек, кроме худшей
            var centroid = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < n; j++) s += simplex[j][i];
                centroid[i] = s / n;
            }

            var reflected = Combine(centroid, simplex[n], 1.0);
            double fr = Safe(f, reflected);

            if (fr < values[0])
            {
                var expanded = Combine(centroid, simplex[n], 2.0);
                double fe = Safe(f, expanded);
                if (fe < fr) { simplex[n] = expanded; values[n] = fe; }
                else { simplex[n] = reflected; values[n] = fr; }
                continue;
            }

            if (fr < values[n - 1])
            {
                simplex[n] = reflected;
                values[n] = fr;
                continue;
            }

            var contracted = Combine(centroid, simplex[n], -0.5);
            double fc = Safe(f, contracted);
            if (fc < values[n])
            {
                simplex[n] = contracted;
                values[n] = fc;
                continue;
            }

            // Сжатие всего симплекса к лучшей точке
            for (int i = 1; i <= n; i++)
            {
                for (int k = 0; k < n; k++)
                    simplex[i][k] = simplex[0][k] + (0.5 * (simplex[i][k] - simplex[0][k]));
                values[i] = Safe(f, simplex[i]);
            }
        }

        Array.Sort(values, simplex);
        return simplex[0];
    }

    /// <summary>
    /// Минимизация по строго положительным параметрам: оптимизация ведётся
    /// по <c>u = ln(theta)</c>, поэтому граница <c>theta &gt; 0</c> недостижима.
    /// </summary>
    /// <param name="f">Целевая функция от исходных (положительных) параметров.</param>
    /// <param name="start">Начальное приближение в исходных координатах.</param>
    /// <param name="maxIter">Максимум итераций.</param>
    /// <returns>Точка минимума в исходных координатах.</returns>
    public static double[] MinimizePositive(Func<double[], double> f, double[] start, int maxIter = 4000)
    {
        int n = start.Length;
        var u0 = new double[n];
        for (int i = 0; i < n; i++) u0[i] = Math.Log(Math.Max(start[i], 1e-8));

        double[] u = Minimize(u => f(Exp(u)), u0, 0.35, maxIter);
        return Exp(u);
    }

    private static double[] Exp(double[] u)
    {
        var x = new double[u.Length];
        for (int i = 0; i < u.Length; i++) x[i] = Math.Exp(u[i]);
        return x;
    }

    private static double[] Combine(double[] centroid, double[] worst, double coefficient)
    {
        var r = new double[centroid.Length];
        for (int i = 0; i < r.Length; i++)
            r[i] = centroid[i] + (coefficient * (centroid[i] - worst[i]));
        return r;
    }

    /// <summary>
    /// Значение функции с защитой от NaN: симплекс не должен «застревать»
    /// в недопустимой области, поэтому она штрафуется бесконечностью.
    /// </summary>
    private static double Safe(Func<double[], double> f, double[] x)
    {
        double v = f(x);
        return double.IsNaN(v) ? double.PositiveInfinity : v;
    }
}

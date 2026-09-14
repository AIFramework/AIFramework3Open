using System.Globalization;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Solvers.Optimization;

/// <summary>Настройки метода Нелдера — Мида</summary>
public sealed class NelderMeadOptions
{
    /// <summary>
    /// Масштаб начального симплекса: вершина сдвигается на эту долю координаты,
    /// а у нулевой координаты — на эту величину
    /// </summary>
    public double Step { get; init; } = 0.25;

    /// <summary>Предел итераций</summary>
    public int MaxIterations { get; init; } = 4000;

    /// <summary>Порог сходимости по относительному разбросу значений в симплексе</summary>
    public double Tolerance { get; init; } = 1e-10;
}

/// <summary>Результат минимизации методом Нелдера — Мида</summary>
public sealed class NelderMeadResult : IInterpretable
{
    internal NelderMeadResult(Vector point, double value, int iterations, int evaluations, bool converged)
    {
        Point = point;
        Value = value;
        Iterations = iterations;
        Evaluations = evaluations;
        Converged = converged;
    }

    /// <summary>Найденная точка минимума</summary>
    public Vector Point { get; }

    /// <summary>Значение функции в найденной точке</summary>
    public double Value { get; }

    /// <summary>Число выполненных итераций</summary>
    public int Iterations { get; }

    /// <summary>Число вычислений функции</summary>
    public int Evaluations { get; }

    /// <summary>Сошёлся ли симплекс, а не исчерпан предел итераций</summary>
    public bool Converged { get; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string value = Value.ToString("G6", CultureInfo.InvariantCulture);

        return new InterpretationBuilder("Минимизация Нелдера — Мида")
            .Summary(Converged
                ? $"Минимум {value} найден за {Iterations} итераций, функция вычислена {Evaluations} раз."
                : $"Предел итераций исчерпан: лучшее найденное значение {value}, сходимость не достигнута.")
            .Metric("Значение функции", Value, null, "в найденной точке", MetricQuality.Neutral, 6)
            .Metric("Итерации", Iterations, null, Converged ? "до сходимости" : "предел исчерпан",
                Converged ? MetricQuality.Good : MetricQuality.Warning, 0)
            .WarningIf(!Converged,
                "Сходимость не достигнута: увеличьте предел итераций или начните из другой точки.")
            .Warning("Метод находит локальный минимум, и какой именно — зависит от начальной точки. " +
                     "У многоэкстремальной функции стоит запустить его из нескольких точек.")
            .Build();
    }
}

/// <summary>
/// Симплекс-метод Нелдера — Мида: безградиентная минимизация функции многих переменных
/// </summary>
/// <remarks>
/// <para>
/// Нужен там, где градиента нет или его вывод хрупок: правдоподобия моделей с двумя-четырьмя
/// параметрами, подгонка кривой, подбор настроек по результату расчёта. На таких размерностях
/// безградиентный метод находит тот же оптимум за микросекунды и не ломается на границе области.
/// </para>
/// <para>
/// Недопустимую точку функция может обозначить значением NaN: оно считается бесконечностью,
/// и симплекс отходит от неё, а не застревает. Положительность параметров удобнее задавать
/// через <see cref="MinimizePositive"/>, чем штрафом.
/// </para>
/// <para>
/// Реализация перенесена из внутренней подложки AI.Econometrics, чтобы в репозитории остался
/// один метод Нелдера — Мида: эконометрика и экономика пользуются им через тот же код.
/// </para>
/// </remarks>
public static class NelderMead
{
    /// <summary>Минимизирует функцию без ограничений</summary>
    /// <param name="function">Целевая функция</param>
    /// <param name="start">Начальная точка</param>
    /// <param name="options">Настройки</param>
    /// <exception cref="ArgumentException">Пустая начальная точка или неположительные настройки</exception>
    public static NelderMeadResult Minimize(Func<Vector, double> function, Vector start, NelderMeadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(start);

        (double[] point, double value, int iterations, int evaluations, bool converged) =
            Run(x => function(new Vector(x)), start.ToArray(), Validate(options ?? new NelderMeadOptions(), start));

        return new NelderMeadResult(new Vector(point), value, iterations, evaluations, converged);
    }

    /// <summary>
    /// Минимизирует функцию строго положительных параметров
    /// </summary>
    /// <remarks>
    /// Поиск ведётся по <c>u = ln(x)</c>, поэтому граница <c>x &gt; 0</c> недостижима, а не
    /// штрафуется. По умолчанию начальный симплекс шире — 0.35 в логарифмах: шаг по логарифму
    /// соответствует относительному изменению параметра.
    /// </remarks>
    /// <param name="function">Целевая функция от исходных, положительных параметров</param>
    /// <param name="start">Начальная точка в исходных координатах</param>
    /// <param name="options">Настройки; шаг — в логарифмах</param>
    public static NelderMeadResult MinimizePositive(
        Func<Vector, double> function, Vector start, NelderMeadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(start);

        var logarithms = new double[start.Count];

        for (int i = 0; i < logarithms.Length; i++) logarithms[i] = Math.Log(Math.Max(start[i], 1e-8));

        (double[] point, double value, int iterations, int evaluations, bool converged) = Run(
            u => function(new Vector(Exp(u))),
            logarithms,
            Validate(options ?? new NelderMeadOptions { Step = 0.35 }, start));

        return new NelderMeadResult(new Vector(Exp(point)), value, iterations, evaluations, converged);
    }

    private static NelderMeadOptions Validate(NelderMeadOptions options, Vector start)
    {
        if (start.Count == 0)
            throw new ArgumentException("Нужна хотя бы одна переменная", nameof(start));

        if (!(options.Step > 0) || options.MaxIterations < 1 || !(options.Tolerance > 0))
            throw new ArgumentException("Шаг, предел итераций и порог сходимости должны быть положительными", nameof(options));

        return options;
    }

    private static (double[] Point, double Value, int Iterations, int Evaluations, bool Converged) Run(
        Func<double[], double> f, double[] start, NelderMeadOptions options)
    {
        int n = start.Length;
        var simplex = new double[n + 1][];
        var values = new double[n + 1];
        int evaluations = 0;

        double Safe(double[] x)
        {
            evaluations++;

            // Недопустимая точка штрафуется бесконечностью: симплекс не должен в ней застревать
            double v = f(x);
            return double.IsNaN(v) ? double.PositiveInfinity : v;
        }

        simplex[0] = (double[])start.Clone();

        for (int i = 0; i < n; i++)
        {
            var p = (double[])start.Clone();
            p[i] += Math.Abs(p[i]) > 1e-8 ? options.Step * Math.Abs(p[i]) : options.Step;
            simplex[i + 1] = p;
        }

        for (int i = 0; i <= n; i++) values[i] = Safe(simplex[i]);

        int iteration = 0;
        bool converged = false;

        for (; iteration < options.MaxIterations; iteration++)
        {
            Array.Sort(values, simplex);

            if (Math.Abs(values[n] - values[0]) <= options.Tolerance * (Math.Abs(values[0]) + options.Tolerance))
            {
                converged = true;
                break;
            }

            // Центр тяжести всех точек, кроме худшей
            var centroid = new double[n];

            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < n; j++) s += simplex[j][i];
                centroid[i] = s / n;
            }

            var reflected = Combine(centroid, simplex[n], 1.0);
            double fr = Safe(reflected);

            if (fr < values[0])
            {
                var expanded = Combine(centroid, simplex[n], 2.0);
                double fe = Safe(expanded);

                if (fe < fr)
                {
                    simplex[n] = expanded;
                    values[n] = fe;
                }
                else
                {
                    simplex[n] = reflected;
                    values[n] = fr;
                }

                continue;
            }

            if (fr < values[n - 1])
            {
                simplex[n] = reflected;
                values[n] = fr;
                continue;
            }

            var contracted = Combine(centroid, simplex[n], -0.5);
            double fc = Safe(contracted);

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

                values[i] = Safe(simplex[i]);
            }
        }

        Array.Sort(values, simplex);

        return (simplex[0], values[0], iteration, evaluations, converged);
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
}

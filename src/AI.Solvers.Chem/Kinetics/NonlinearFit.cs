using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Statistics;
using AI.Statistics.MonteCarlo;

namespace AI.Solvers.Chem.Kinetics;

/// <summary>
/// Настройки подгонки параметров
/// </summary>
public sealed class NonlinearFitOptions
{
    /// <summary>Предельное число итераций Левенберга-Марквардта</summary>
    public int MaxIterations { get; set; } = 200;

    /// <summary>Порог сходимости по относительному уменьшению суммы квадратов</summary>
    public double Tolerance { get; set; } = 1e-10;

    /// <summary>Относительный шаг численного дифференцирования</summary>
    public double DerivativeStep { get; set; } = 1e-5;

    /// <summary>Число проб глобального поиска отжигом; 0 - только локальный спуск</summary>
    public int AnnealingIterations { get; set; } = 300;

    /// <summary>Начальный размах случайного шага при отжиге</summary>
    public double AnnealingScale { get; set; } = 1.0;

    /// <summary>Зерно генератора для воспроизводимости; -1 - случайное</summary>
    public int Seed { get; set; } = 20240101;

    /// <summary>Доверительная вероятность для интервалов параметров</summary>
    public double Confidence { get; set; } = 0.95;
}

/// <summary>
/// Результат подгонки параметров модели
/// </summary>
public sealed class NonlinearFitResult
{
    /// <summary>Найденные параметры</summary>
    public double[] Parameters { get; init; }

    /// <summary>Стандартные ошибки параметров</summary>
    public double[] StandardErrors { get; init; }

    /// <summary>Границы доверительных интервалов</summary>
    public (double Lower, double Upper)[] Intervals { get; init; }

    /// <summary>Остатки модель - данные</summary>
    public double[] Residuals { get; init; }

    /// <summary>Сумма квадратов остатков</summary>
    public double ResidualSumOfSquares { get; init; }

    /// <summary>Оценка СКО измерения</summary>
    public double ResidualStd { get; init; }

    /// <summary>Коэффициент детерминации</summary>
    public double R2 { get; init; }

    /// <summary>Число итераций локального спуска</summary>
    public int Iterations { get; init; }

    /// <summary>Сошёлся ли алгоритм по порогу, а не по лимиту итераций</summary>
    public bool Converged { get; init; }

    /// <summary>Доверительная вероятность интервалов</summary>
    public double Confidence { get; init; }
}

/// <summary>
/// Подгонка параметров нелинейной модели методом наименьших квадратов:
/// глобальный поиск отжигом и локальное уточнение по Левенбергу-Марквардту.
/// </summary>
/// <remarks>
/// Ковариационная матрица параметров оценивается как s²·(JᵀJ)⁺ с численным якобианом;
/// отсюда стандартные ошибки и доверительные интервалы. Псевдообращение вместо
/// обычного обращения нужно потому, что при плохо обусловленной задаче
/// (например, две константы скорости различаются на порядки и данные их не разделяют)
/// матрица JᵀJ вырождена, и обычное обращение молча дало бы мусор.
/// </remarks>
public static class NonlinearFit
{
    /// <summary>
    /// Подгоняет параметры модели по остаткам
    /// </summary>
    /// <param name="residuals">Функция остатков: параметры -> вектор (модель - данные)</param>
    /// <param name="start">Начальное приближение параметров</param>
    /// <param name="observedVariance">
    /// Дисперсия наблюдений вокруг их среднего; нужна для R², может быть 0
    /// </param>
    /// <param name="options">Настройки</param>
    public static NonlinearFitResult Fit(
        Func<double[], double[]> residuals,
        double[] start,
        double observedVariance = 0,
        NonlinearFitOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentNullException.ThrowIfNull(start);

        options ??= new NonlinearFitOptions();

        double[] parameters = (double[])start.Clone();
        double[] current = residuals(parameters);
        double cost = SumOfSquares(current);

        if (options.AnnealingIterations > 0)
            (parameters, current, cost) = Anneal(residuals, parameters, cost, options);

        int iteration = 0;
        bool converged = false;
        double lambda = 1e-3;

        for (; iteration < options.MaxIterations; iteration++)
        {
            var jacobian = Jacobian(residuals, parameters, current, options.DerivativeStep);
            var step = TrySolveStep(jacobian, current, lambda);

            if (step == null)
                break;

            var candidate = new double[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
                candidate[i] = parameters[i] + step[i];

            double[] candidateResiduals = residuals(candidate);
            double candidateCost = SumOfSquares(candidateResiduals);

            if (candidateCost < cost)
            {
                double improvement = (cost - candidateCost) / Math.Max(cost, 1e-300);

                parameters = candidate;
                current = candidateResiduals;
                cost = candidateCost;
                lambda = Math.Max(lambda / 3, 1e-12);

                if (improvement < options.Tolerance)
                {
                    converged = true;
                    iteration++;
                    break;
                }
            }
            else
            {
                lambda *= 5;

                if (lambda > 1e12)
                {
                    converged = true;
                    iteration++;
                    break;
                }
            }
        }

        return Summarize(residuals, parameters, current, cost, observedVariance, iteration, converged, options);
    }

    // Глобальный этап: случайное блуждание с приёмкой по критерию отжига
    private static (double[] Parameters, double[] Residuals, double Cost) Anneal(
        Func<double[], double[]> residuals, double[] start, double startCost, NonlinearFitOptions options)
    {
        var random = options.Seed == -1 ? RandomEngine.Create() : RandomEngine.Create(options.Seed);
        var annealing = new SimulatedAnnealing(startCost, options.Seed)
        {
            T = Math.Max(startCost, 1e-12),
            Kt = Math.Pow(1e6, 1.0 / Math.Max(1, options.AnnealingIterations))
        };

        double[] currentParameters = (double[])start.Clone();
        double[] currentResiduals = residuals(currentParameters);
        double currentCost = startCost;

        double[] bestParameters = (double[])currentParameters.Clone();
        double[] bestResiduals = (double[])currentResiduals.Clone();
        double bestCost = currentCost;

        for (int i = 0; i < options.AnnealingIterations; i++)
        {
            double scale = options.AnnealingScale * (1.0 - ((double)i / options.AnnealingIterations));
            var candidate = new double[currentParameters.Length];

            for (int p = 0; p < candidate.Length; p++)
                candidate[p] = currentParameters[p] + (scale * ((2 * random.NextDouble()) - 1));

            double[] candidateResiduals = residuals(candidate);
            double candidateCost = SumOfSquares(candidateResiduals);

            if (!annealing.IsAccept(candidateCost))
                continue;

            currentParameters = candidate;
            currentResiduals = candidateResiduals;
            currentCost = candidateCost;

            if (candidateCost < bestCost)
            {
                bestParameters = (double[])candidate.Clone();
                bestResiduals = candidateResiduals;
                bestCost = candidateCost;
            }
        }

        return currentCost < bestCost
            ? (currentParameters, currentResiduals, currentCost)
            : (bestParameters, bestResiduals, bestCost);
    }

    private static NonlinearFitResult Summarize(
        Func<double[], double[]> residuals,
        double[] parameters,
        double[] current,
        double cost,
        double observedVariance,
        int iterations,
        bool converged,
        NonlinearFitOptions options)
    {
        int n = current.Length;
        int p = parameters.Length;
        int degreesOfFreedom = Math.Max(1, n - p);

        double variance = cost / degreesOfFreedom;
        double residualStd = Math.Sqrt(variance);

        var jacobian = Jacobian(residuals, parameters, current, options.DerivativeStep);
        var errors = new double[p];
        var intervals = new (double, double)[p];

        var normal = new Matrix(p, p);

        for (int i = 0; i < p; i++)
        {
            for (int j = 0; j < p; j++)
            {
                double sum = 0;

                for (int row = 0; row < n; row++)
                    sum += jacobian[row, i] * jacobian[row, j];

                normal[i, j] = sum;
            }
        }

        Matrix covariance = Pseudoinverse.Compute(normal);
        double t = StatInference.TQuantile(1 - ((1 - options.Confidence) / 2), degreesOfFreedom);

        for (int i = 0; i < p; i++)
        {
            double value = variance * covariance[i, i];
            errors[i] = value > 0 ? Math.Sqrt(value) : 0;
            intervals[i] = (parameters[i] - (t * errors[i]), parameters[i] + (t * errors[i]));
        }

        return new NonlinearFitResult
        {
            Parameters = parameters,
            StandardErrors = errors,
            Intervals = intervals,
            Residuals = current,
            ResidualSumOfSquares = cost,
            ResidualStd = residualStd,
            R2 = observedVariance > 0 ? 1 - (cost / observedVariance) : double.NaN,
            Iterations = iterations,
            Converged = converged,
            Confidence = options.Confidence
        };
    }

    // Численный якобиан центральными разностями
    private static Matrix Jacobian(Func<double[], double[]> residuals, double[] parameters, double[] current, double step)
    {
        int n = current.Length;
        int p = parameters.Length;
        var jacobian = new Matrix(n, p);

        for (int j = 0; j < p; j++)
        {
            double delta = step * Math.Max(Math.Abs(parameters[j]), 1.0);

            var forward = (double[])parameters.Clone();
            var backward = (double[])parameters.Clone();
            forward[j] += delta;
            backward[j] -= delta;

            double[] plus = residuals(forward);
            double[] minus = residuals(backward);

            for (int i = 0; i < n; i++)
                jacobian[i, j] = (plus[i] - minus[i]) / (2 * delta);
        }

        return jacobian;
    }

    // Шаг Левенберга-Марквардта: (JᵀJ + λ·diag(JᵀJ))·δ = -Jᵀr
    private static double[] TrySolveStep(Matrix jacobian, double[] residuals, double lambda)
    {
        int n = jacobian.Height;
        int p = jacobian.Width;

        var normal = new Matrix(p, p);
        var gradient = new double[p];

        for (int i = 0; i < p; i++)
        {
            for (int j = 0; j < p; j++)
            {
                double sum = 0;

                for (int row = 0; row < n; row++)
                    sum += jacobian[row, i] * jacobian[row, j];

                normal[i, j] = sum;
            }

            double gradientSum = 0;

            for (int row = 0; row < n; row++)
                gradientSum += jacobian[row, i] * residuals[row];

            gradient[i] = -gradientSum;
        }

        for (int i = 0; i < p; i++)
            normal[i, i] += lambda * Math.Max(normal[i, i], 1e-12);

        Matrix inverse = Pseudoinverse.Compute(normal);
        var step = new double[p];

        for (int i = 0; i < p; i++)
        {
            double sum = 0;

            for (int j = 0; j < p; j++)
                sum += inverse[i, j] * gradient[j];

            if (double.IsNaN(sum) || double.IsInfinity(sum))
                return null;

            step[i] = sum;
        }

        return step;
    }

    private static double SumOfSquares(double[] values)
    {
        double sum = 0;

        foreach (double value in values)
            sum += value * value;

        return sum;
    }
}

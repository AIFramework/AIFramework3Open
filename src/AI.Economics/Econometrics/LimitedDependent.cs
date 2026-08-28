using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Econometrics;

/// <summary>Модель с ограниченной зависимой переменной.</summary>
public enum LimitedDependentModel
{
    /// <summary>Логит: бинарный отклик, логистическая функция связи.</summary>
    Logit,

    /// <summary>Пробит: бинарный отклик, нормальная функция связи.</summary>
    Probit,

    /// <summary>Тобит: отклик цензурирован снизу.</summary>
    Tobit,

    /// <summary>Пуассоновская регрессия для счётных данных.</summary>
    Poisson,

    /// <summary>Отрицательная биномиальная регрессия: счётные данные со сверхдисперсией.</summary>
    NegativeBinomial,
}

/// <summary>Результат оценивания модели с ограниченной зависимой переменной.</summary>
public sealed record LimitedDependentResult : IInterpretable
{
    /// <summary>Тип модели.</summary>
    public LimitedDependentModel Model { get; init; }

    /// <summary>Оценки коэффициентов.</summary>
    public IReadOnlyList<Coefficient> Coefficients { get; init; } = [];

    /// <summary>Средние предельные эффекты регрессоров на отклик.</summary>
    public IReadOnlyList<(string Variable, double Effect)> MarginalEffects { get; init; } = [];

    /// <summary>Логарифм правдоподобия модели.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Логарифм правдоподобия модели только со свободным членом.</summary>
    public double NullLogLikelihood { get; init; }

    /// <summary>Псевдо-R² Макфаддена.</summary>
    public double McFaddenRSquared =>
        NullLogLikelihood < 0 ? 1 - (LogLikelihood / NullLogLikelihood) : 0;

    /// <summary>Информационный критерий Акаике.</summary>
    public double Aic { get; init; }

    /// <summary>Информационный критерий Шварца.</summary>
    public double Bic { get; init; }

    /// <summary>Расчётные значения отклика или вероятности.</summary>
    public Vector Fitted { get; init; } = new(0);

    /// <summary>Оценка масштабного параметра: сигма в тобите, альфа в отрицательной биномиальной.</summary>
    public double ScaleParameter { get; init; }

    /// <summary>Доля верно классифицированных наблюдений для бинарных моделей.</summary>
    public double Accuracy { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Доля цензурированных наблюдений в тобите.</summary>
    public double CensoredShare { get; init; }

    /// <summary>Сошёлся ли алгоритм оценивания.</summary>
    public bool Converged { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        Coefficient? strongest = Coefficients
            .Where(c => c.Name != "const")
            .OrderByDescending(c => Math.Abs(c.TStatistic))
            .FirstOrDefault();

        (string Variable, double Effect) largestEffect = MarginalEffects
            .OrderByDescending(e => Math.Abs(e.Effect))
            .FirstOrDefault();

        bool binary = Model is LimitedDependentModel.Logit or LimitedDependentModel.Probit;
        bool count = Model is LimitedDependentModel.Poisson or LimitedDependentModel.NegativeBinomial;

        var builder = new InterpretationBuilder($"Модель ограниченного отклика: {ModelName()}")
            .Summary($"Оценено по {Observations} наблюдениям. Псевдо-R² Макфаддена " +
                     $"{Fmt.Num(McFaddenRSquared, 3)}, логарифм правдоподобия " +
                     $"{Fmt.Num(LogLikelihood, 1)} против {Fmt.Num(NullLogLikelihood, 1)} " +
                     "у модели без регрессоров. " +
                     (binary ? $"Доля верных классификаций {Fmt.Pct(Accuracy, 1)}." : "") +
                     (Model == LimitedDependentModel.Tobit
                         ? $"Цензурировано {Fmt.Pct(CensoredShare, 1)} наблюдений."
                         : ""))
            .Metric("Псевдо-R²", McFaddenRSquared, null,
                "значения 0,2-0,4 у Макфаддена соответствуют хорошей подгонке",
                McFaddenRSquared > 0.2 ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Логарифм правдоподобия", LogLikelihood, null,
                $"AIC {Fmt.Num(Aic, 1)}, BIC {Fmt.Num(Bic, 1)}", MetricQuality.Neutral, 2)
            .Metric("Наблюдений", Observations, null,
                $"{Coefficients.Count} параметров", MetricQuality.Neutral, 0);

        if (binary)
        {
            builder.Metric("Точность классификации", Accuracy, null,
                "доля верных предсказаний при пороге 0,5",
                Accuracy > 0.7 ? MetricQuality.Good : MetricQuality.Neutral, 3);
        }

        if (Model == LimitedDependentModel.Tobit)
        {
            builder
                .Metric("Сигма", ScaleParameter, null, "разброс латентной переменной",
                    MetricQuality.Neutral, 4)
                .Metric("Доля цензурированных", CensoredShare, null,
                    "наблюдения на границе", MetricQuality.Neutral, 3);
        }

        if (Model == LimitedDependentModel.NegativeBinomial)
        {
            builder.Metric("Параметр сверхдисперсии", ScaleParameter, null,
                ScaleParameter > 0.1
                    ? "дисперсия заметно превышает среднее — Пуассон неприменим"
                    : "сверхдисперсия невелика",
                ScaleParameter > 0.1 ? MetricQuality.Warning : MetricQuality.Good, 4);
        }

        foreach (Coefficient coefficient in Coefficients)
        {
            builder.Metric(coefficient.Name, coefficient.Estimate, null,
                $"ст. ошибка {Fmt.Num(coefficient.StandardError, 4)}, p = {Fmt.Num(coefficient.PValue, 4)} " +
                coefficient.Stars,
                coefficient.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        foreach ((string variable, double effect) in MarginalEffects)
        {
            builder.Metric($"Предельный эффект: {variable}", effect, null,
                "среднее изменение отклика при единичном изменении регрессора",
                MetricQuality.Unknown, 5);
        }

        return builder
            .FindingIf(strongest is not null,
                $"Сильнее всего связан с откликом регрессор «{strongest?.Name}» " +
                $"(t = {Fmt.Num(strongest?.TStatistic ?? 0, 2)}).")
            .FindingIf(largestEffect.Variable is not null,
                $"Наибольший предельный эффект у «{largestEffect.Variable}»: " +
                $"{Fmt.Num(largestEffect.Effect, 5)}. Интерпретировать нужно именно " +
                "предельные эффекты, а не коэффициенты — последние измеряются " +
                "в шкале латентной переменной и напрямую несопоставимы.")
            .FindingIf(count,
                "В счётных моделях коэффициент означает изменение логарифма ожидаемого " +
                "числа событий: экспонента от него даёт мультипликатор.")
            .FindingIf(Model == LimitedDependentModel.NegativeBinomial && ScaleParameter > 0.1,
                $"Параметр сверхдисперсии {Fmt.Num(ScaleParameter, 3)} значимо отличен от нуля. " +
                "Пуассоновская модель на этих данных занижает стандартные ошибки.")
            .FindingIf(Model == LimitedDependentModel.Tobit,
                $"Цензурировано {Fmt.Pct(CensoredShare, 1)} наблюдений. Обычный МНК на таких " +
                "данных смещает коэффициенты к нулю тем сильнее, чем больше эта доля.")
            .WarningIf(!Converged,
                "Алгоритм оценивания не сошёлся. Результат ненадёжен: проверьте " +
                "коллинеарность регрессоров и масштаб переменных.")
            .WarningIf(binary && Accuracy > 0.99,
                "Почти идеальная классификация обычно означает полное разделение выборки. " +
                "В этом случае оценки максимального правдоподобия не существуют, " +
                "а численный алгоритм останавливается на произвольно больших значениях.")
            .Warning("Коэффициенты нелинейных моделей нельзя сравнивать между выборками " +
                     "и между моделями напрямую: они смешаны с масштабом ненаблюдаемой " +
                     "ошибки. Сравнивайте предельные эффекты.")
            .Recommendation("Приводите в отчёте предельные эффекты вместе с коэффициентами: " +
                            "содержательный вывод делается только по ним.")
            .Recommendation("Для счётных данных сначала проверяйте сверхдисперсию: " +
                            "выбор между Пуассоном и отрицательной биномиальной решается ею, " +
                            "а не качеством подгонки.")
            .Build();
    }

    /// <summary>Читаемое название модели.</summary>
    private string ModelName() => Model switch
    {
        LimitedDependentModel.Logit => "логит",
        LimitedDependentModel.Probit => "пробит",
        LimitedDependentModel.Tobit => "тобит",
        LimitedDependentModel.Poisson => "пуассоновская регрессия",
        _ => "отрицательная биномиальная регрессия",
    };
}

/// <summary>
/// Модели с ограниченной зависимой переменной: бинарный выбор, цензурированный
/// отклик и счётные данные.
/// </summary>
/// <remarks>
/// <para>
/// Обычный МНК на таких данных даёт предсказания вне допустимого множества —
/// отрицательные вероятности, дробные счётчики, значения ниже границы
/// цензурирования — и смещает оценки. Все модели здесь оцениваются методом
/// максимального правдоподобия.
/// </para>
/// <para>
/// Логит и пробит отличаются функцией связи: логистическая против нормальной.
/// Практически они дают почти одинаковые предельные эффекты; коэффициенты
/// различаются масштабом примерно в 1,6 раза, поэтому сравнивать нужно именно
/// предельные эффекты.
/// </para>
/// <para>
/// Тобит описывает отклик, наблюдаемый только выше порога:
/// </para>
/// <code>
/// y* = x'beta + e,   y = max(y*, c)
/// </code>
/// <para>
/// Пуассоновская модель предполагает равенство среднего и дисперсии. Когда оно
/// нарушено, оценки остаются состоятельными, но стандартные ошибки занижены, и
/// нужна отрицательная биномиальная с дополнительным параметром сверхдисперсии.
/// </para>
/// </remarks>
public static class LimitedDependent
{
    private const int MaxIterations = 200;
    private const double Tolerance = 1e-10;

    /// <summary>Оценивает модель с ограниченной зависимой переменной.</summary>
    /// <param name="x">Матрица регрессоров без свободного члена.</param>
    /// <param name="y">Отклик: доли нуля и единицы для бинарных моделей, счётчик или цензурированная величина.</param>
    /// <param name="model">Тип модели.</param>
    /// <param name="names">Названия регрессоров.</param>
    /// <param name="censorPoint">Точка цензурирования для тобита.</param>
    /// <returns>Коэффициенты, предельные эффекты и качество подгонки.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или отклик не подходит модели.</exception>
    public static LimitedDependentResult Fit(
        Matrix x, Vector y, LimitedDependentModel model,
        IReadOnlyList<string>? names = null, double censorPoint = 0)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Height != y.Count)
            throw new ArgumentException("Число строк матрицы должно совпадать с длиной отклика.", nameof(y));

        int n = x.Height, k = x.Width + 1;
        if (n <= k) throw new ArgumentException("Наблюдений должно быть больше числа параметров.", nameof(x));

        var design = new double[n, k];
        var response = new double[n];

        for (int i = 0; i < n; i++)
        {
            design[i, 0] = 1;
            for (int j = 0; j < x.Width; j++) design[i, j + 1] = x[i, j];
            response[i] = y[i];
        }

        var labels = new List<string> { "const" };
        for (int j = 0; j < x.Width; j++)
            labels.Add(names is not null && j < names.Count ? names[j] : $"x{j + 1}");

        return model switch
        {
            LimitedDependentModel.Logit or LimitedDependentModel.Probit =>
                FitBinary(design, response, labels, model),
            LimitedDependentModel.Tobit => FitTobit(design, response, labels, censorPoint),
            LimitedDependentModel.Poisson => FitCount(design, response, labels, alpha: 0),
            _ => FitNegativeBinomial(design, response, labels),
        };
    }

    /// <summary>Логит или пробит методом Ньютона.</summary>
    private static LimitedDependentResult FitBinary(
        double[,] design, double[] y, IReadOnlyList<string> names, LimitedDependentModel model)
    {
        int n = design.GetLength(0), k = design.GetLength(1);

        foreach (double value in y)
            if (value is not (0 or 1))
                throw new ArgumentException("Бинарный отклик должен состоять из нулей и единиц.", nameof(y));

        var beta = new double[k];
        double mean = y.Average();
        beta[0] = model == LimitedDependentModel.Logit
            ? Math.Log(Math.Clamp(mean, 1e-6, 1 - 1e-6) / (1 - Math.Clamp(mean, 1e-6, 1 - 1e-6)))
            : EconMath.NormalInv(Math.Clamp(mean, 1e-6, 1 - 1e-6));

        bool converged = false;
        double[,] hessianInverse = LinearAlgebra.Identity(k);

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            var gradient = new double[k];
            var hessian = new double[k, k];

            for (int i = 0; i < n; i++)
            {
                double index = 0;
                for (int j = 0; j < k; j++) index += design[i, j] * beta[j];

                double probability, weight;

                if (model == LimitedDependentModel.Logit)
                {
                    probability = 1.0 / (1.0 + Math.Exp(-Math.Clamp(index, -35, 35)));
                    weight = probability * (1 - probability);
                }
                else
                {
                    probability = Math.Clamp(EconMath.NormalCdf(index), 1e-12, 1 - 1e-12);
                    double density = EconMath.NormalPdf(index);
                    weight = density * density / (probability * (1 - probability));
                }

                double score = model == LimitedDependentModel.Logit
                    ? y[i] - probability
                    : (y[i] - probability) * EconMath.NormalPdf(index) / (probability * (1 - probability));

                for (int a = 0; a < k; a++)
                {
                    gradient[a] += design[i, a] * score;
                    for (int b = 0; b < k; b++) hessian[a, b] += weight * design[i, a] * design[i, b];
                }
            }

            for (int a = 0; a < k; a++) hessian[a, a] += 1e-8;

            double[,]? inverse = EconMath.Inverse(hessian);
            if (inverse is null) break;

            hessianInverse = inverse;
            double[] step = LinearAlgebra.Multiply(inverse, gradient);

            double magnitude = 0;
            for (int j = 0; j < k; j++)
            {
                // Демпфирование шага не даёт методу разойтись при почти полном
                // разделении выборки
                double bounded = Math.Clamp(step[j], -2, 2);
                beta[j] += bounded;
                magnitude += Math.Abs(bounded);
            }

            if (magnitude < Tolerance) { converged = true; break; }
        }

        var fitted = new Vector(n);
        double logLikelihood = 0;
        int correct = 0;

        for (int i = 0; i < n; i++)
        {
            double index = 0;
            for (int j = 0; j < k; j++) index += design[i, j] * beta[j];

            double probability = model == LimitedDependentModel.Logit
                ? 1.0 / (1.0 + Math.Exp(-Math.Clamp(index, -35, 35)))
                : Math.Clamp(EconMath.NormalCdf(index), 1e-12, 1 - 1e-12);

            fitted[i] = probability;
            logLikelihood += (y[i] * Math.Log(Math.Max(probability, 1e-300)))
                + ((1 - y[i]) * Math.Log(Math.Max(1 - probability, 1e-300)));

            if ((probability >= 0.5 ? 1 : 0) == (int)y[i]) correct++;
        }

        double share = Math.Clamp(y.Average(), 1e-9, 1 - 1e-9);
        double nullLogLikelihood = n * ((share * Math.Log(share)) + ((1 - share) * Math.Log(1 - share)));

        var effects = new List<(string, double)>();
        for (int j = 1; j < k; j++)
        {
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double index = 0;
                for (int c = 0; c < k; c++) index += design[i, c] * beta[c];

                sum += model == LimitedDependentModel.Logit
                    ? fitted[i] * (1 - fitted[i])
                    : EconMath.NormalPdf(index);
            }

            effects.Add((names[j], sum / n * beta[j]));
        }

        return new LimitedDependentResult
        {
            Model = model,
            Coefficients = BuildCoefficients(names, beta, hessianInverse, n - k),
            MarginalEffects = effects,
            LogLikelihood = logLikelihood,
            NullLogLikelihood = nullLogLikelihood,
            Aic = (-2 * logLikelihood) + (2 * k),
            Bic = (-2 * logLikelihood) + (k * Math.Log(n)),
            Fitted = fitted,
            Accuracy = (double)correct / n,
            Observations = n,
            Converged = converged,
        };
    }

    /// <summary>Тобит методом максимального правдоподобия.</summary>
    private static LimitedDependentResult FitTobit(
        double[,] design, double[] y, IReadOnlyList<string> names, double censorPoint)
    {
        int n = design.GetLength(0), k = design.GetLength(1);

        RegressionResult start = LinearRegression.FitDesign(
            design, y, names, new RegressionOptions { AddIntercept = false }, "начальное приближение");

        var initial = new double[k + 1];
        for (int j = 0; j < k; j++) initial[j] = start.Coefficients[j].Estimate;
        initial[k] = Math.Log(Math.Max(start.Sigma, 1e-6));

        double Negative(double[] parameters)
        {
            double sigma = Math.Exp(Math.Clamp(parameters[k], -20, 20));
            double total = 0;

            for (int i = 0; i < n; i++)
            {
                double index = 0;
                for (int j = 0; j < k; j++) index += design[i, j] * parameters[j];

                if (y[i] <= censorPoint + 1e-12)
                {
                    double probability = Math.Clamp(EconMath.NormalCdf((censorPoint - index) / sigma), 1e-300, 1);
                    total += Math.Log(probability);
                }
                else
                {
                    double standardized = (y[i] - index) / sigma;
                    total += Math.Log(Math.Max(EconMath.NormalPdf(standardized) / sigma, 1e-300));
                }
            }

            return -total;
        }

        double[] estimate = NelderMead.Minimize(Negative, initial, 4000);
        double sigmaHat = Math.Exp(Math.Clamp(estimate[k], -20, 20));

        var beta = new double[k];
        for (int j = 0; j < k; j++) beta[j] = estimate[j];

        double[,] covariance = NumericalCovariance(Negative, estimate, k);

        var fitted = new Vector(n);
        int censored = 0;

        for (int i = 0; i < n; i++)
        {
            double index = 0;
            for (int j = 0; j < k; j++) index += design[i, j] * beta[j];

            double lambda = EconMath.NormalCdf(index / sigmaHat);
            fitted[i] = lambda * index;
            if (y[i] <= censorPoint + 1e-12) censored++;
        }

        var effects = new List<(string, double)>();
        for (int j = 1; j < k; j++)
        {
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double index = 0;
                for (int c = 0; c < k; c++) index += design[i, c] * beta[c];
                sum += EconMath.NormalCdf(index / sigmaHat);
            }

            effects.Add((names[j], sum / n * beta[j]));
        }

        double logLikelihood = -Negative(estimate);

        return new LimitedDependentResult
        {
            Model = LimitedDependentModel.Tobit,
            Coefficients = BuildCoefficients(names, beta, covariance, n - k),
            MarginalEffects = effects,
            LogLikelihood = logLikelihood,
            NullLogLikelihood = logLikelihood - (start.RSquared * n / 2),
            Aic = (-2 * logLikelihood) + (2 * (k + 1)),
            Bic = (-2 * logLikelihood) + ((k + 1) * Math.Log(n)),
            Fitted = fitted,
            ScaleParameter = sigmaHat,
            CensoredShare = (double)censored / n,
            Observations = n,
            Converged = true,
        };
    }

    /// <summary>Пуассоновская или отрицательная биномиальная регрессия при заданной сверхдисперсии.</summary>
    private static LimitedDependentResult FitCount(
        double[,] design, double[] y, IReadOnlyList<string> names, double alpha)
    {
        int n = design.GetLength(0), k = design.GetLength(1);

        foreach (double value in y)
            if (value < 0) throw new ArgumentException("Счётный отклик не может быть отрицательным.", nameof(y));

        var beta = new double[k];
        beta[0] = Math.Log(Math.Max(y.Average(), 1e-6));

        bool converged = false;
        double[,] hessianInverse = LinearAlgebra.Identity(k);

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            var gradient = new double[k];
            var hessian = new double[k, k];

            for (int i = 0; i < n; i++)
            {
                double index = 0;
                for (int j = 0; j < k; j++) index += design[i, j] * beta[j];

                double mu = Math.Exp(Math.Clamp(index, -30, 30));
                double weight = alpha > 0 ? mu / (1 + (alpha * mu)) : mu;
                double score = alpha > 0 ? (y[i] - mu) / (1 + (alpha * mu)) : y[i] - mu;

                for (int a = 0; a < k; a++)
                {
                    gradient[a] += design[i, a] * score;
                    for (int b = 0; b < k; b++) hessian[a, b] += weight * design[i, a] * design[i, b];
                }
            }

            for (int a = 0; a < k; a++) hessian[a, a] += 1e-8;

            double[,]? inverse = EconMath.Inverse(hessian);
            if (inverse is null) break;

            hessianInverse = inverse;
            double[] step = LinearAlgebra.Multiply(inverse, gradient);

            double magnitude = 0;
            for (int j = 0; j < k; j++)
            {
                double bounded = Math.Clamp(step[j], -2, 2);
                beta[j] += bounded;
                magnitude += Math.Abs(bounded);
            }

            if (magnitude < Tolerance) { converged = true; break; }
        }

        var fitted = new Vector(n);
        double logLikelihood = 0;

        for (int i = 0; i < n; i++)
        {
            double index = 0;
            for (int j = 0; j < k; j++) index += design[i, j] * beta[j];

            double mu = Math.Exp(Math.Clamp(index, -30, 30));
            fitted[i] = mu;
            logLikelihood += CountLogLikelihood(y[i], mu, alpha);
        }

        double mean = Math.Max(y.Average(), 1e-9);
        double nullLogLikelihood = y.Sum(value => CountLogLikelihood(value, mean, alpha));

        var effects = new List<(string, double)>();
        double meanMu = fitted.Average();
        for (int j = 1; j < k; j++) effects.Add((names[j], meanMu * beta[j]));

        return new LimitedDependentResult
        {
            Model = alpha > 0 ? LimitedDependentModel.NegativeBinomial : LimitedDependentModel.Poisson,
            Coefficients = BuildCoefficients(names, beta, hessianInverse, n - k),
            MarginalEffects = effects,
            LogLikelihood = logLikelihood,
            NullLogLikelihood = nullLogLikelihood,
            Aic = (-2 * logLikelihood) + (2 * k),
            Bic = (-2 * logLikelihood) + (k * Math.Log(n)),
            Fitted = fitted,
            ScaleParameter = alpha,
            Observations = n,
            Converged = converged,
        };
    }

    /// <summary>Отрицательная биномиальная регрессия с оценкой сверхдисперсии по профилю правдоподобия.</summary>
    private static LimitedDependentResult FitNegativeBinomial(
        double[,] design, double[] y, IReadOnlyList<string> names)
    {
        double Profile(double[] parameters)
        {
            double alpha = Math.Exp(Math.Clamp(parameters[0], -12, 5));
            return -FitCount(design, y, names, alpha).LogLikelihood;
        }

        double[] optimum = NelderMead.Minimize(Profile, [Math.Log(0.5)], 200);
        double alphaHat = Math.Exp(Math.Clamp(optimum[0], -12, 5));

        return FitCount(design, y, names, alphaHat);
    }

    /// <summary>Логарифм правдоподобия одного счётного наблюдения.</summary>
    private static double CountLogLikelihood(double y, double mu, double alpha)
    {
        if (alpha <= 0)
            return (y * Math.Log(Math.Max(mu, 1e-300))) - mu - EconMath.LogGamma(y + 1);

        double theta = 1 / alpha;

        return EconMath.LogGamma(y + theta) - EconMath.LogGamma(theta) - EconMath.LogGamma(y + 1)
            + (theta * Math.Log(theta / (theta + mu)))
            + (y * Math.Log(Math.Max(mu / (theta + mu), 1e-300)));
    }

    /// <summary>Ковариационная матрица из численного гессиана.</summary>
    private static double[,] NumericalCovariance(Func<double[], double> negativeLogLikelihood, double[] point, int k)
    {
        var hessian = new double[k, k];
        double step = 1e-4;

        for (int a = 0; a < k; a++)
        {
            for (int b = a; b < k; b++)
            {
                double ha = step * Math.Max(1, Math.Abs(point[a]));
                double hb = step * Math.Max(1, Math.Abs(point[b]));

                double[] pp = (double[])point.Clone(); pp[a] += ha; pp[b] += hb;
                double[] pm = (double[])point.Clone(); pm[a] += ha; pm[b] -= hb;
                double[] mp = (double[])point.Clone(); mp[a] -= ha; mp[b] += hb;
                double[] mm = (double[])point.Clone(); mm[a] -= ha; mm[b] -= hb;

                double value = (negativeLogLikelihood(pp) - negativeLogLikelihood(pm)
                    - negativeLogLikelihood(mp) + negativeLogLikelihood(mm)) / (4 * ha * hb);

                hessian[a, b] = value;
                hessian[b, a] = value;
            }
        }

        for (int a = 0; a < k; a++) hessian[a, a] += 1e-10;

        return EconMath.Inverse(hessian) ?? LinearAlgebra.Identity(k);
    }

    /// <summary>Собирает записи коэффициентов из оценок и ковариационной матрицы.</summary>
    private static IReadOnlyList<Coefficient> BuildCoefficients(
        IReadOnlyList<string> names, double[] beta, double[,] covariance, int df)
    {
        var coefficients = new List<Coefficient>(beta.Length);
        int degrees = Math.Max(1, df);

        for (int j = 0; j < beta.Length; j++)
        {
            double error = j < covariance.GetLength(0) ? Math.Sqrt(Math.Max(covariance[j, j], 0)) : 0;
            double t = error > 0 ? beta[j] / error : 0;
            double p = error > 0 ? Distributions.TPValue(t, degrees) : 1;

            coefficients.Add(new Coefficient(
                names[j], beta[j], error, t, double.IsNaN(p) ? 1 : p,
                beta[j] - (1.96 * error), beta[j] + (1.96 * error)));
        }

        return coefficients;
    }
}

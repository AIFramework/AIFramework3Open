using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.ClassicMath.AlgorithmAnalysis;

/// <summary>
/// Метрики оценки регрессионных моделей
/// </summary>
[Serializable]
public static class MetricsForRegression
{
    private static readonly double _eps = 1e-100;

    /// <summary>Наименьшее число, заметное рядом с единицей; нижняя граница делителя в MAPE, как в sklearn.</summary>
    private const double MachineEpsilon = 2.220446049250313e-16;

    private static ReadOnlySpan<double> Span(IAlgebraicStructure<double> structure)
        => structure.Data.AsSpan(0, structure.Shape.Count);

    private static void RequireSameLength(ReadOnlySpan<double> target, ReadOnlySpan<double> output)
    {
        if (target.Length != output.Length)
            throw new ArgumentException($"Длины факта и прогноза различаются: {target.Length} и {output.Length}", nameof(output));
    }

    // Сумма целевых значений
    private static double TSum(IAlgebraicStructure<double> targ)
    {
        double ts = 0;
        double[] data = targ.Data;

        for (int i = 0; i < targ.Shape.Count; i++)
            ts += data[i];

        return ts;
    }

    /// <summary>
    /// Усредненная метрика
    /// </summary>
    private static double AvMetric(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output, Func<IAlgebraicStructure<double>, IAlgebraicStructure<double>, double> metric)
    {
        int len = target.Count();
        double ret = 0;
        IAlgebraicStructure<double>[] t = target.ToArray();
        IAlgebraicStructure<double>[] o = output.ToArray();
        for (int i = 0; i < len; i++)
        {
            ret += metric(t[i], o[i]);
        }

        return ret / len;
    }

    /// <summary>
    /// Средняя абсолютная ошибка: среднее |прогноз − факт|.
    /// </summary>
    /// <remarks>Единственная реализация; совпадает с <c>sklearn.metrics.mean_absolute_error</c>.</remarks>
    /// <param name="target">Истинные значения.</param>
    /// <param name="output">Прогноз той же длины.</param>
    /// <exception cref="ArgumentException">Длины различаются.</exception>
    public static double MAE(ReadOnlySpan<double> target, ReadOnlySpan<double> output)
    {
        RequireSameLength(target, output);

        double sum = 0;

        for (int i = 0; i < target.Length; i++) sum += Math.Abs(output[i] - target[i]);

        return sum / target.Length;
    }

    /// <inheritdoc cref="MAE(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double MAE(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => MAE(Span(target), Span(output));

    /// <summary>
    /// Cредняя абсолютная ошибка (В процентах)
    /// </summary>
    public static double MAEPercent(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
    {
        double ret = 0;
        double[] dataO = output.Data;
        double[] dataT = target.Data;

        for (int i = 0; i < output.Shape.Count; i++)
        {
            ret += Math.Abs(dataO[i] - dataT[i]);
        }

        return ret / (TSum(target) + _eps);
    }

    /// <summary>
    /// Средняя абсолютная относительная ошибка: среднее |прогноз − факт| / |факт|, долей (не в процентах).
    /// </summary>
    /// <remarks>
    /// Единственная реализация; совпадает с <c>sklearn.metrics.mean_absolute_percentage_error</c>.
    /// Правило нулевого факта — как там же: делитель не меньше машинного эпсилон, поэтому ошибка на
    /// нулевом факте даёт огромное значение и видна, а не теряется.
    /// </remarks>
    /// <param name="target">Истинные значения.</param>
    /// <param name="output">Прогноз той же длины.</param>
    /// <exception cref="ArgumentException">Длины различаются.</exception>
    public static double MAPE(ReadOnlySpan<double> target, ReadOnlySpan<double> output)
    {
        RequireSameLength(target, output);

        double sum = 0;

        for (int i = 0; i < target.Length; i++)
            sum += Math.Abs(output[i] - target[i]) / Math.Max(Math.Abs(target[i]), MachineEpsilon);

        return sum / target.Length;
    }

    /// <inheritdoc cref="MAPE(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double MAPE(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => MAPE(Span(target), Span(output));

    /// <summary>
    /// Средний квадрат ошибки.
    /// </summary>
    /// <remarks>Единственная реализация; совпадает с <c>sklearn.metrics.mean_squared_error</c>.</remarks>
    /// <param name="target">Истинные значения.</param>
    /// <param name="output">Прогноз той же длины.</param>
    /// <exception cref="ArgumentException">Длины различаются.</exception>
    public static double MSE(ReadOnlySpan<double> target, ReadOnlySpan<double> output)
    {
        RequireSameLength(target, output);

        double sum = 0;

        for (int i = 0; i < target.Length; i++)
        {
            double q = output[i] - target[i];
            sum += q * q;
        }

        return sum / target.Length;
    }

    /// <inheritdoc cref="MSE(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double MSE(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => MSE(Span(target), Span(output));

    /// <summary>
    /// Корень из среднего квадрата ошибки.
    /// </summary>
    /// <param name="target">Истинные значения.</param>
    /// <param name="output">Прогноз той же длины.</param>
    /// <exception cref="ArgumentException">Длины различаются.</exception>
    public static double RMSE(ReadOnlySpan<double> target, ReadOnlySpan<double> output) => Math.Sqrt(MSE(target, output));

    /// <inheritdoc cref="RMSE(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double RMSE(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => Math.Sqrt(MSE(target, output));

    /// <summary>
    /// RMLE (target[i]>-1, output[i]>-1 for all i \in [0; N-1])
    /// </summary>
    public static double RMSLE(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
    {
        Vector newTarg = new Vector(target.Shape.Count), newOut = new Vector(target.Shape.Count);
        double[] dataO = output.Data;
        double[] dataT = target.Data;

        for (int i = 0; i < target.Shape.Count; i++)
        {
            newOut[i] = Math.Log(dataO[i] + 1);
            newTarg[i] = Math.Log(dataT[i] + 1);
        }

        return RMSE(newTarg, newOut);
    }

    /// <summary>
    /// Корень из среднего квадрата ошибки в процентах
    /// </summary>
    public static double RMSEPercent(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
    {
        return target.Shape.Count * Math.Sqrt(MSE(target, output)) / (TSum(target) + _eps);
    }

    /// <summary>
    /// Коэффициент детерминации R² = 1 − SSres / SStot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Единственная реализация R²; совпадает с <c>sklearn.metrics.r2_score</c>. Доля разброса факта,
    /// объяснённая прогнозом: 1 — прогноз точен, 0 — не лучше среднего, меньше нуля — хуже среднего.
    /// </para>
    /// <para>
    /// Это не квадрат корреляции: прогноз <c>2y + 10</c> коррелирует с фактом идеально, а R² у него
    /// глубоко отрицателен. Квадрат корреляции — <see cref="PearsonSquared(ReadOnlySpan{double}, ReadOnlySpan{double})"/>.
    /// </para>
    /// <para>
    /// У постоянного факта SStot равна нулю, и доля не определена; как и sklearn, функция возвращает
    /// 1 при точном прогнозе и 0 иначе.
    /// </para>
    /// </remarks>
    /// <param name="target">Истинные значения.</param>
    /// <param name="output">Прогноз той же длины.</param>
    /// <exception cref="ArgumentException">Длины различаются.</exception>
    public static double RSquared(ReadOnlySpan<double> target, ReadOnlySpan<double> output)
    {
        RequireSameLength(target, output);

        double mean = 0;

        for (int i = 0; i < target.Length; i++) mean += target[i];

        mean /= target.Length;

        double residual = 0, total = 0;

        for (int i = 0; i < target.Length; i++)
        {
            double error = target[i] - output[i], deviation = target[i] - mean;

            residual += error * error;
            total += deviation * deviation;
        }

        if (total == 0) return residual == 0 ? 1.0 : 0.0;

        return 1 - (residual / total);
    }

    /// <inheritdoc cref="RSquared(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double RSquared(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => RSquared(Span(target), Span(output));

    /// <summary>
    /// Квадрат коэффициента корреляции Пирсона между фактом и прогнозом.
    /// </summary>
    /// <remarks>
    /// Показывает, насколько прогноз линейно связан с фактом, а не насколько он точен: смещённый и
    /// растянутый прогноз даёт единицу. С R² (<see cref="RSquared(ReadOnlySpan{double}, ReadOnlySpan{double})"/>)
    /// совпадает только у линейной регрессии со свободным членом на обучающей выборке.
    /// </remarks>
    /// <param name="target">Истинные значения.</param>
    /// <param name="output">Прогноз той же длины.</param>
    public static double PearsonSquared(ReadOnlySpan<double> target, ReadOnlySpan<double> output)
    {
        double correlation = Statistics.Statistic.Pearson(target, output);

        return correlation * correlation;
    }

    /// <inheritdoc cref="PearsonSquared(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double PearsonSquared(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => PearsonSquared(Span(target), Span(output));

    /// <summary>Квадрат корреляции Пирсона под прежним именем.</summary>
    [Obsolete("Это квадрат корреляции Пирсона, а не R². Коэффициент детерминации — RSquared (1 − SSres/SStot), квадрат корреляции — PearsonSquared.")]
    public static double R2(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
        => PearsonSquared(target, output);

    /// <summary>
    /// Средняя абсолютная масштабированная ошибка (MASE, Хайндман и Кёлер, 2006).
    /// </summary>
    /// <remarks>
    /// <para>
    /// MAE прогноза, делённая на MAE наивного сезонного прогноза на обучающем ряде:
    /// <c>mean|y − ŷ| / mean|x_t − x_{t−m}|</c>. Меньше единицы — прогноз лучше наивного.
    /// </para>
    /// <para>
    /// Правило окна одно: масштаб считается по всему обучающему ряду с шага <c>m</c>, ошибка — по
    /// всем переданным парам «факт — прогноз». Для ошибки на самой обучающей выборке вызывающий
    /// передаёт те точки, где у модели есть прогноз (после прогрева), и весь ряд как обучающий.
    /// Когда масштаб не определён (ряд короче периода или постоянен), результат — <c>NaN</c>.
    /// </para>
    /// </remarks>
    /// <param name="actual">Факт на проверяемом участке.</param>
    /// <param name="forecast">Прогноз на том же участке.</param>
    /// <param name="training">Обучающий ряд — по нему считается масштаб.</param>
    /// <param name="seasonalPeriod">Период сезонности m; 1 — наивный прогноз «как вчера».</param>
    /// <exception cref="ArgumentException">Длины факта и прогноза различаются.</exception>
    public static double MASE(
        ReadOnlySpan<double> actual, ReadOnlySpan<double> forecast, ReadOnlySpan<double> training, int seasonalPeriod = 1)
    {
        RequireSameLength(actual, forecast);

        int lag = Math.Max(seasonalPeriod, 1);

        if (training.Length <= lag || actual.Length == 0) return double.NaN;

        double scale = 0;

        for (int i = lag; i < training.Length; i++) scale += Math.Abs(training[i] - training[i - lag]);

        scale /= training.Length - lag;

        return scale > 1e-12 ? MAE(actual, forecast) / scale : double.NaN;
    }

    /// <summary>
    /// Псевдо-R² Макфаддена: 1 − ln L / ln L₀.
    /// </summary>
    /// <remarks>
    /// Для моделей, оценённых по правдоподобию (логит, пробит, счётные): <c>ln L</c> — правдоподобие
    /// модели, <c>ln L₀</c> — модели с одной константой. Совпадает с <c>prsquared</c> в statsmodels.
    /// Когда <c>ln L₀</c> не отрицателен (сравнивать не с чем), результат — 0.
    /// </remarks>
    /// <param name="logLikelihood">Логарифм правдоподобия модели.</param>
    /// <param name="nullLogLikelihood">Логарифм правдоподобия модели с одной константой.</param>
    public static double McFaddenRSquared(double logLikelihood, double nullLogLikelihood)
        => nullLogLikelihood < 0 ? 1.0 - (logLikelihood / nullLogLikelihood) : 0;

    /// <summary>
    /// Среднее смещение относительно 0
    /// </summary>
    /// <param name="target"></param>
    /// <param name="output"></param>
    /// <returns></returns>
    public static double Bias(IAlgebraicStructure<double> target, IAlgebraicStructure<double> output)
    {
        double ret = 0;
        double[] dataO = output.Data;
        double[] dataT = target.Data;

        for (int i = 0; i < output.Shape.Count; i++)
            ret += dataO[i] - dataT[i];


        return ret / output.Shape.Count;
    }

    /// <summary>
    /// Cредняя абсолютная ошибка
    /// </summary>
    public static double MAE(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, MAE);
    }
    /// <summary>
    /// Cредняя абсолютная ошибка (В процентах)
    /// </summary>
    public static double MAEPercent(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, MAEPercent);
    }
    /// <summary>
    /// Средняя абсолютная процентная ошибка
    /// </summary>
    public static double MAPE(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, MAPE);
    }
    /// <summary>
    /// Средний квадрат ошибки
    /// </summary>
    public static double MSE(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, MSE);
    }
    /// <summary>
    /// Корень из среднего квадрата ошибки
    /// </summary>
    public static double RMSE(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, RMSE);
    }
    /// <summary>
    /// Корень из среднего квадрата ошибки в процентах
    /// </summary>
    public static double RMSEPercent(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, RMSEPercent);
    }
    /// <summary>
    /// Корреляция Пирсона в квадрате
    /// </summary>
    [Obsolete("Это средний квадрат корреляции Пирсона, а не R². Используйте RSquared либо PearsonSquared по каждой паре.")]
    public static double R2(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, PearsonSquared);
    }
    /// <summary>
    /// Среднее смещение относительно 0
    /// </summary>
    public static double Bias(IEnumerable<IAlgebraicStructure<double>> target, IEnumerable<IAlgebraicStructure<double>> output)
    {
        return AvMetric(target, output, Bias);
    }
}

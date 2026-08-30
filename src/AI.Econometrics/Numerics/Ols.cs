using System;

namespace AI.Econometrics.Numerics;

/// <summary>Результат оценки линейной регрессии методом наименьших квадратов.</summary>
internal sealed class OlsFit
{
    /// <summary>Оценки коэффициентов, включая свободный член.</summary>
    public required double[] Beta { get; init; }

    /// <summary>Стандартные ошибки коэффициентов.</summary>
    public required double[] StandardErrors { get; init; }

    /// <summary>Остатки.</summary>
    public required double[] Residuals { get; init; }

    /// <summary>Обратная матрица <c>(X'X)^-1</c> — нужна для двухшагового МНК.</summary>
    public required double[,] XtxInverse { get; init; }

    /// <summary>Коэффициент детерминации.</summary>
    public double RSquared { get; init; }

    /// <summary>Скорректированный коэффициент детерминации.</summary>
    public double AdjustedRSquared { get; init; }

    /// <summary>Оценка дисперсии ошибки.</summary>
    public double SigmaSquared { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Число оценённых коэффициентов.</summary>
    public int Parameters => Beta.Length;

    /// <summary>Статистика Стьюдента для коэффициента.</summary>
    /// <param name="index">Номер коэффициента.</param>
    public double TStatistic(int index) =>
        StandardErrors[index] > 0 ? Beta[index] / StandardErrors[index] : double.NaN;

    /// <summary>
    /// Двустороннее p-значение по нормальному приближению.
    /// </summary>
    /// <param name="index">Номер коэффициента.</param>
    /// <remarks>
    /// Приближение нормальным законом вместо распределения Стьюдента даёт
    /// заниженное p-значение на выборках меньше трёх десятков наблюдений.
    /// Для маркетинговых панелей, где наблюдений сотни, разница несущественна.
    /// </remarks>
    public double PValue(int index)
    {
        double t = TStatistic(index);
        return double.IsNaN(t) ? double.NaN : 2.0 * (1.0 - EconMath.NormalCdf(Math.Abs(t)));
    }

    /// <summary>Нижняя граница 95-процентного доверительного интервала.</summary>
    /// <param name="index">Номер коэффициента.</param>
    public double ConfidenceLow(int index) => Beta[index] - (1.959963985 * StandardErrors[index]);

    /// <summary>Верхняя граница 95-процентного доверительного интервала.</summary>
    /// <param name="index">Номер коэффициента.</param>
    public double ConfidenceHigh(int index) => Beta[index] + (1.959963985 * StandardErrors[index]);

    /// <summary>Предсказание по вектору признаков той же структуры, что и строки X.</summary>
    /// <param name="row">Строка признаков.</param>
    public double Predict(double[] row)
    {
        double s = 0;
        for (int j = 0; j < Beta.Length && j < row.Length; j++) s += Beta[j] * row[j];
        return s;
    }
}

/// <summary>
/// Линейная регрессия методом наименьших квадратов и её расширения:
/// гребневая регрессия и двухшаговый МНК.
/// </summary>
internal static class Ols
{
    /// <summary>Оценивает регрессию <c>y = X beta + u</c>.</summary>
    /// <param name="x">Матрица признаков; свободный член должен быть добавлен явно.</param>
    /// <param name="y">Вектор отклика.</param>
    /// <param name="ridge">Коэффициент гребневой регуляризации; 0 — обычный МНК.</param>
    /// <returns>Оценка либо <c>null</c>, если матрица вырождена.</returns>
    public static OlsFit? Fit(double[,] x, double[] y, double ridge = 0)
    {
        int n = y.Length;
        int k = x.GetLength(1);
        if (n <= k) return null;

        var xtx = new double[k, k];
        var xty = new double[k];

        for (int i = 0; i < n; i++)
        {
            for (int a = 0; a < k; a++)
            {
                xty[a] += x[i, a] * y[i];
                for (int b = 0; b < k; b++) xtx[a, b] += x[i, a] * x[i, b];
            }
        }

        // Свободный член не штрафуется: иначе регуляризация смещает уровень ряда
        if (ridge > 0)
            for (int a = 1; a < k; a++) xtx[a, a] += ridge;

        double[,]? inverse = EconMath.Inverse(xtx);
        if (inverse is null) return null;

        var beta = new double[k];
        for (int a = 0; a < k; a++)
            for (int b = 0; b < k; b++) beta[a] += inverse[a, b] * xty[b];

        var residuals = new double[n];
        double rss = 0, tss = 0, mean = 0;
        for (int i = 0; i < n; i++) mean += y[i];
        mean /= n;

        for (int i = 0; i < n; i++)
        {
            double fitted = 0;
            for (int a = 0; a < k; a++) fitted += x[i, a] * beta[a];
            residuals[i] = y[i] - fitted;
            rss += residuals[i] * residuals[i];
            tss += (y[i] - mean) * (y[i] - mean);
        }

        double sigma2 = rss / (n - k);
        var se = new double[k];
        for (int a = 0; a < k; a++) se[a] = Math.Sqrt(Math.Max(sigma2 * inverse[a, a], 0));

        return new OlsFit
        {
            Beta = beta,
            StandardErrors = se,
            Residuals = residuals,
            XtxInverse = inverse,
            RSquared = tss > 0 ? 1.0 - (rss / tss) : 0,
            AdjustedRSquared = tss > 0 ? 1.0 - ((rss / (n - k)) / (tss / (n - 1))) : 0,
            SigmaSquared = sigma2,
            Observations = n,
        };
    }

    /// <summary>
    /// Двухшаговый метод наименьших квадратов.
    /// </summary>
    /// <param name="endogenous">Эндогенный регрессор, по одному столбцу на наблюдение.</param>
    /// <param name="exogenous">Экзогенные регрессоры, включая свободный член.</param>
    /// <param name="instruments">Исключённые инструменты.</param>
    /// <param name="y">Отклик.</param>
    /// <returns>
    /// Оценка второй ступени с корректной дисперсией и статистика первой
    /// ступени; <c>null</c> при вырождении.
    /// </returns>
    /// <remarks>
    /// Дисперсия второй ступени считается по остаткам с <b>фактическим</b>
    /// эндогенным регрессором, а не с его прогнозом. Наивная подстановка
    /// прогноза в готовую формулу МНК занижает стандартные ошибки.
    /// </remarks>
    public static (OlsFit Second, OlsFit First, double InstrumentF)? TwoStage(
        double[] endogenous, double[,] exogenous, double[,] instruments, double[] y)
    {
        int n = y.Length;
        int kExo = exogenous.GetLength(1);
        int kIv = instruments.GetLength(1);

        // Первая ступень: эндогенная переменная на все экзогенные и инструменты
        var firstX = new double[n, kExo + kIv];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < kExo; j++) firstX[i, j] = exogenous[i, j];
            for (int j = 0; j < kIv; j++) firstX[i, kExo + j] = instruments[i, j];
        }

        OlsFit? first = Fit(firstX, endogenous);
        if (first is null) return null;

        // Совместная значимость исключённых инструментов: при одном
        // инструменте это в точности квадрат его t-статистики
        double instrumentF = 0;
        for (int j = 0; j < kIv; j++)
        {
            double t = first.TStatistic(kExo + j);
            if (!double.IsNaN(t)) instrumentF += t * t;
        }
        instrumentF /= Math.Max(kIv, 1);

        var predicted = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int j = 0; j < kExo + kIv; j++) s += firstX[i, j] * first.Beta[j];
            predicted[i] = s;
        }

        // Вторая ступень: отклик на прогноз эндогенной и экзогенные
        var secondX = new double[n, kExo + 1];
        for (int i = 0; i < n; i++)
        {
            secondX[i, 0] = predicted[i];
            for (int j = 0; j < kExo; j++) secondX[i, j + 1] = exogenous[i, j];
        }

        OlsFit? second = Fit(secondX, y);
        if (second is null) return null;

        // Пересчёт остатков и стандартных ошибок по фактическому регрессору
        var trueResiduals = new double[n];
        double rss = 0;
        for (int i = 0; i < n; i++)
        {
            double fitted = second.Beta[0] * endogenous[i];
            for (int j = 0; j < kExo; j++) fitted += second.Beta[j + 1] * exogenous[i, j];
            trueResiduals[i] = y[i] - fitted;
            rss += trueResiduals[i] * trueResiduals[i];
        }

        double sigma2 = rss / (n - kExo - 1);
        var se = new double[kExo + 1];
        for (int a = 0; a < se.Length; a++)
            se[a] = Math.Sqrt(Math.Max(sigma2 * second.XtxInverse[a, a], 0));

        var corrected = new OlsFit
        {
            Beta = second.Beta,
            StandardErrors = se,
            Residuals = trueResiduals,
            XtxInverse = second.XtxInverse,
            RSquared = second.RSquared,
            AdjustedRSquared = second.AdjustedRSquared,
            SigmaSquared = sigma2,
            Observations = n,
        };

        return (corrected, first, instrumentF);
    }
}

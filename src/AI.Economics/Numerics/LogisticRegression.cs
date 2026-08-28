using System;

namespace AI.Economics.Numerics;

/// <summary>
/// Логистическая регрессия, оцениваемая итеративно перевзвешенным методом
/// наименьших квадратов.
/// </summary>
/// <remarks>
/// Нужна двум задачам сразу: моделям отклика в uplift-моделировании и
/// оценке вероятности покупки в исследованиях готовности платить.
/// Регуляризация включена по умолчанию: на маркетинговых данных признаки
/// часто почти коллинеарны, и без неё матрица вырождается.
/// </remarks>
internal sealed class LogisticRegression
{
    private double[] _beta = [];

    /// <summary>Оценки коэффициентов, включая свободный член.</summary>
    public double[] Beta => _beta;

    /// <summary>Логарифм правдоподобия в точке оптимума.</summary>
    public double LogLikelihood { get; private set; }

    /// <summary>Число выполненных итераций.</summary>
    public int Iterations { get; private set; }

    /// <summary>Сошёлся ли алгоритм до исчерпания лимита итераций.</summary>
    public bool Converged { get; private set; }

    /// <summary>Обучает модель.</summary>
    /// <param name="x">Матрица признаков со свободным членом в первом столбце.</param>
    /// <param name="y">Бинарный отклик: 0 или 1.</param>
    /// <param name="ridge">Коэффициент гребневой регуляризации.</param>
    /// <param name="maxIterations">Максимум итераций.</param>
    /// <param name="tolerance">Порог сходимости по изменению коэффициентов.</param>
    /// <returns>Признак сходимости.</returns>
    public bool Fit(double[,] x, double[] y, double ridge = 1e-4,
        int maxIterations = 60, double tolerance = 1e-9)
    {
        int n = y.Length;
        int k = x.GetLength(1);
        _beta = new double[k];

        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
            var xtwx = new double[k, k];
            var xtwz = new double[k];

            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int j = 0; j < k; j++) eta += x[i, j] * _beta[j];

                double p = 1.0 / (1.0 + Math.Exp(-eta));
                double w = Math.Max(p * (1 - p), 1e-8);
                double z = eta + ((y[i] - p) / w);

                for (int a = 0; a < k; a++)
                {
                    xtwz[a] += x[i, a] * w * z;
                    for (int b = 0; b < k; b++) xtwx[a, b] += x[i, a] * w * x[i, b];
                }
            }

            for (int a = 1; a < k; a++) xtwx[a, a] += ridge;

            double[,]? inverse = EconMath.Inverse(xtwx);
            if (inverse is null) break;

            var next = new double[k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++) next[a] += inverse[a, b] * xtwz[b];

            double shift = 0;
            for (int a = 0; a < k; a++) shift += Math.Abs(next[a] - _beta[a]);

            _beta = next;
            Iterations = iteration;

            if (shift < tolerance)
            {
                Converged = true;
                break;
            }
        }

        LogLikelihood = ComputeLogLikelihood(x, y);
        return Converged;
    }

    /// <summary>Предсказанная вероятность для строки признаков.</summary>
    /// <param name="row">Строка со свободным членом в первом элементе.</param>
    public double Predict(double[] row)
    {
        double eta = 0;
        for (int j = 0; j < _beta.Length && j < row.Length; j++) eta += _beta[j] * row[j];
        return 1.0 / (1.0 + Math.Exp(-eta));
    }

    private double ComputeLogLikelihood(double[,] x, double[] y)
    {
        int n = y.Length;
        int k = x.GetLength(1);
        double ll = 0;

        for (int i = 0; i < n; i++)
        {
            double eta = 0;
            for (int j = 0; j < k; j++) eta += x[i, j] * _beta[j];
            double p = EconMath.Clamp(1.0 / (1.0 + Math.Exp(-eta)), 1e-12, 1 - 1e-12);
            ll += (y[i] * Math.Log(p)) + ((1 - y[i]) * Math.Log(1 - p));
        }

        return ll;
    }
}

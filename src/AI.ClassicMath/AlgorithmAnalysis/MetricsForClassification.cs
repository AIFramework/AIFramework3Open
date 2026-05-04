using AI.DataStructs.Algebraic;
using System;
using System.Linq;

namespace AI.ClassicMath.AlgorithmAnalysis;

/// <summary>
/// Метрики качества классификатора
/// </summary>
[Serializable]
public static class MetricsForClassification
{
    /// <summary>
    /// Accuracy — доля верно классифицированных примеров.
    /// </summary>
    public static double Accuracy(int[] real, int[] outAlg)
    {
        if (real == null) throw new ArgumentNullException(nameof(real));
        if (outAlg == null) throw new ArgumentNullException(nameof(outAlg));
        if (real.Length == 0) throw new ArgumentException("Массив меток пуст.", nameof(real));
        if (real.Length != outAlg.Length) throw new ArgumentException("Длины массивов real и outAlg не совпадают.");

        double y = 0;
        for (int i = 0; i < real.Length; i++)
            if (real[i] == outAlg[i]) y++;

        return y / real.Length;
    }
    /// <summary>
    /// Точность (Precision) для каждого класса: TP[c] / (TP[c] + FP[c]).
    /// FP[c] — случаи, когда алгоритм предсказал класс c, а реальный — другой.
    /// </summary>
    public static Vector PrecisionForEachClass(int[] real, int[] outAlg)
    {
        if (real.Length == 0)
            throw new ArgumentException("Массив меток пуст.", nameof(real));
        if (real.Length != outAlg.Length)
            throw new ArgumentException("Длины массивов real и outAlg не совпадают.");

        int classes = GetClasses(real, outAlg);
        double[] tp = new double[classes];  // Истинно-положительные
        double[] fp = new double[classes];  // Ложно-положительные (предсказан c, а реально — другой)

        for (int i = 0; i < real.Length; i++)
        {
            if (real[i] == outAlg[i])
                tp[real[i]]++;
            else
                fp[outAlg[i]]++;
        }

        double[] precision = new double[classes];
        for (int i = 0; i < classes; i++)
        {
            double denom = tp[i] + fp[i];
            precision[i] = denom > 0 ? tp[i] / denom : 0.0;
        }

        return new Vector(precision);
    }
    /// <summary>
    /// Средняя точность (macro-average Precision)
    /// </summary>
    public static double AveragePrecision(int[] real, int[] outAlg)
    {
        return PrecisionForEachClass(real, outAlg).Mean();
    }
    /// <summary>
    /// Матрица ошибок (перепутывания)
    /// </summary>
    public static Matrix ConfusionMatrix(int[] real, int[] outAlg)
    {
        int classes = GetClasses(real, outAlg);
        Matrix matrix = new Matrix(classes, classes);

        for (int i = 0; i < real.Length; i++)
        {
            matrix[real[i], outAlg[i]]++;
        }

        return matrix;
    }
    /// <summary>
    /// Полнота (Recall) для каждого класса: TP[c] / (TP[c] + FN[c]).
    /// FN[c] — реальные примеры класса c, предсказанные как другой класс.
    /// </summary>
    public static Vector RecallForEachClass(int[] real, int[] outAlg)
    {
        if (real.Length == 0)
            throw new ArgumentException("Массив меток пуст.", nameof(real));
        if (real.Length != outAlg.Length)
            throw new ArgumentException("Длины массивов real и outAlg не совпадают.");

        int classes = GetClasses(real, outAlg);
        double[] tp = new double[classes];
        double[] fn = new double[classes];  // Ложно-отрицательные (реально c, предсказано другое)

        for (int i = 0; i < real.Length; i++)
        {
            if (real[i] == outAlg[i])
                tp[real[i]]++;
            else
                fn[real[i]]++;
        }

        double[] recall = new double[classes];
        for (int i = 0; i < classes; i++)
        {
            double denom = tp[i] + fn[i];
            recall[i] = denom > 0 ? tp[i] / denom : 0.0;
        }

        return new Vector(recall);
    }

    /// <summary>
    /// Средняя полнота (macro-average Recall)
    /// </summary>
    public static double AverageRecall(int[] real, int[] outAlg)
    {
        return RecallForEachClass(real, outAlg).Mean();
    }
    /// <summary>
    /// F-1 мера, формула: 2 * recall * precision / (recall + precision)
    /// </summary>
    public static double FMeasure(int[] real, int[] outAlg)
    {
        double pr = AveragePrecision(real, outAlg);
        double rec = AverageRecall(real, outAlg);

        return 2.0 * pr * rec / (pr + rec);
    }
    /// <summary>
    /// F мера, формула: (1+beta^2) * recall * precision / (recall + beta^2 * precision)
    /// </summary>
    public static double FMeasure(int[] real, int[] outAlg, double beta)
    {
        double pr = AveragePrecision(real, outAlg);
        double rec = AverageRecall(real, outAlg);
        double b2 = beta * beta;

        return (1 + b2) * pr * rec / (b2 * pr + rec);
    }
    /// <summary>
    /// Составляет отчет по всем метрикам
    /// </summary>
    public static string FullReport(int[] real, int[] outAlg, double betaForFMeasure = 1, bool isForEachClass = false)
    {
        string report = $"Средняя точность:             {GetElementReport(AveragePrecision(real, outAlg))}";
        report += $"\nСредняя полнота:      {GetElementReport(AverageRecall(real, outAlg))}";
        report += $"\nF-Мера:            {GetElementReport(FMeasure(real, outAlg, betaForFMeasure))}";
        report += $"\nАккуратность:            {GetElementReport(Accuracy(real, outAlg))}";

        if (isForEachClass)
        {
            report += "\n\n\n--Значение точности для каждого класса--\n";

            Vector pr = PrecisionForEachClass(real, outAlg);

            for (int i = 0; i < pr.Count; i++)
            {
                report += $"\nКласс № {i + 1}:  {GetElementReport(pr[i])}";
            }
        }

        return report;
    }

    private static string GetElementReport(double p)
    {
        double outpP = Math.Round(p, 4);
        return $"{outpP:N4}\t{outpP * 100:N2}%";
    }

    private static int GetClasses(int[] real, int[] outAlg)
    {
        if (real.Length == 0) throw new ArgumentException("Массив меток пуст.", nameof(real));
        return Math.Max(real.Max(), outAlg.Max()) + 1;
    }
}

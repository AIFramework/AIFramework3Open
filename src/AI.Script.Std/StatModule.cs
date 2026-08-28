using AI.ClassicMath.AlgorithmAnalysis;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>stat</c>: описательные статистики и метрики качества.
/// </summary>
[ScriptModule("stat", "Описательные статистики, корреляции и метрики качества", Version = "0.1")]
public static class StatModule
{
    [ScriptFn("mean", "Среднее", Example = "stat.mean(v)")]
    public static double Mean([ScriptParam("выборка")] Vector v) => Require(v, "mean").Mean();

    [ScriptFn("sum", "Сумма", Example = "stat.sum(v)")]
    public static double Sum([ScriptParam("выборка")] Vector v) => v.Sum();

    [ScriptFn("min", "Минимум", Example = "stat.min(v)")]
    public static double Min([ScriptParam("выборка")] Vector v) => Require(v, "min").Min();

    [ScriptFn("max", "Максимум", Example = "stat.max(v)")]
    public static double Max([ScriptParam("выборка")] Vector v) => Require(v, "max").Max();

    [ScriptFn("std", "Среднеквадратичное отклонение", Example = "stat.std(v)")]
    public static double Std([ScriptParam("выборка")] Vector v) => Require(v, "std").Std();

    [ScriptFn("var", "Дисперсия", Example = "stat.var(v)")]
    public static double Variance([ScriptParam("выборка")] Vector v) => Require(v, "var").Dispersion();

    [ScriptFn("median", "Медиана", Example = "stat.median(v)")]
    public static double Median([ScriptParam("выборка")] Vector v) => Quantile(v, 0.5);

    [ScriptFn("quantile", "Квантиль уровня q (линейная интерполяция)", Example = "stat.quantile(v, q: 0.9)")]
    public static double Quantile(
        [ScriptParam("выборка")] Vector v,
        [ScriptParam("уровень от 0 до 1")] double q = 0.5)
    {
        _ = Require(v, "quantile");

        if (q is < 0 or > 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "stat.quantile: уровень должен лежать в [0, 1]");

        double[] data = v.ToArray();
        Array.Sort(data);

        if (data.Length == 1) return data[0];

        double position = q * (data.Length - 1);
        int low = (int)Math.Floor(position);
        int high = (int)Math.Ceiling(position);

        return low == high ? data[low] : data[low] + ((data[high] - data[low]) * (position - low));
    }

    [ScriptFn("zscore", "Z-нормировка выборки", Example = "v |> stat.zscore()")]
    public static Vector ZScore([ScriptParam("выборка")] Vector v)
    {
        _ = Require(v, "zscore");

        double mean = v.Mean();
        double std = v.Std();

        return std == 0 ? v.Transform(x => x - mean) : v.Transform(x => (x - mean) / std);
    }

    [ScriptFn("minmax", "Нормировка выборки на отрезок [0, 1]", Example = "v |> stat.minmax()")]
    public static Vector MinMax([ScriptParam("выборка")] Vector v)
    {
        _ = Require(v, "minmax");

        double min = v.Min();
        double span = v.Max() - min;

        return span == 0 ? v.Transform(_ => 0.0) : v.Transform(x => (x - min) / span);
    }

    [ScriptFn("corr", "Коэффициент корреляции Пирсона", Example = "stat.corr(x, y)")]
    public static double Correlation(
        [ScriptParam("первая выборка")] Vector x,
        [ScriptParam("вторая выборка")] Vector y)
    {
        RequirePair(x, y, "corr");

        double meanX = x.Mean();
        double meanY = y.Mean();
        double covariance = 0, varianceX = 0, varianceY = 0;

        for (int i = 0; i < x.Count; i++)
        {
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;

            covariance += dx * dy;
            varianceX += dx * dx;
            varianceY += dy * dy;
        }

        double denominator = Math.Sqrt(varianceX * varianceY);

        return denominator == 0 ? 0 : covariance / denominator;
    }

    [ScriptFn("rmse", "Среднеквадратичная ошибка", Example = "stat.rmse(y, pred)")]
    public static double Rmse(
        [ScriptParam("истинные значения")] Vector y,
        [ScriptParam("предсказания")] Vector pred)
    {
        RequirePair(y, pred, "rmse");

        double sum = 0;
        for (int i = 0; i < y.Count; i++) sum += (y[i] - pred[i]) * (y[i] - pred[i]);

        return Math.Sqrt(sum / y.Count);
    }

    [ScriptFn("mae", "Средняя абсолютная ошибка", Example = "stat.mae(y, pred)")]
    public static double Mae(
        [ScriptParam("истинные значения")] Vector y,
        [ScriptParam("предсказания")] Vector pred)
    {
        RequirePair(y, pred, "mae");

        double sum = 0;
        for (int i = 0; i < y.Count; i++) sum += Math.Abs(y[i] - pred[i]);

        return sum / y.Count;
    }

    [ScriptFn("r2", "Коэффициент детерминации", Example = "stat.r2(y, pred)")]
    public static double R2(
        [ScriptParam("истинные значения")] Vector y,
        [ScriptParam("предсказания")] Vector pred)
    {
        RequirePair(y, pred, "r2");

        double mean = y.Mean();
        double residual = 0, total = 0;

        for (int i = 0; i < y.Count; i++)
        {
            residual += (y[i] - pred[i]) * (y[i] - pred[i]);
            total += (y[i] - mean) * (y[i] - mean);
        }

        return total == 0 ? 0 : 1 - (residual / total);
    }

    [ScriptFn("hist", "Гистограмма: границы и частоты", Example = "stat.hist(v, bins: 20)")]
    public static ScriptRecord Histogram(
        [ScriptParam("выборка")] Vector v,
        [ScriptParam("число интервалов")] int bins = 10)
    {
        _ = Require(v, "hist");

        if (bins < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "stat.hist: интервалов должно быть не меньше одного");

        double min = v.Min();
        double max = v.Max();
        double width = max - min;

        var edges = new Vector(bins + 1);
        var counts = new Vector(bins);

        for (int i = 0; i <= bins; i++) edges[i] = width == 0 ? min : min + (width * i / bins);

        for (int i = 0; i < v.Count; i++)
        {
            int bin = width == 0 ? 0 : (int)((v[i] - min) / width * bins);
            counts[Math.Clamp(bin, 0, bins - 1)]++;
        }

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("edges", ScriptValue.Vec(edges)),
            new KeyValuePair<string, ScriptValue>("counts", ScriptValue.Vec(counts)),
        ]);
    }

    // --- метрики классификации ---

    [ScriptFn("accuracy", "Доля верных предсказаний", Example = "stat.accuracy(y, pred)")]
    public static double Accuracy(
        [ScriptParam("истинные метки")] Vector y,
        [ScriptParam("предсказанные метки")] Vector pred)
    {
        RequirePair(y, pred, "accuracy");

        return MetricsForClassification.Accuracy(
            Datasets.Labels(y, "stat.accuracy"),
            Datasets.Labels(pred, "stat.accuracy"));
    }

    [ScriptFn("precision", "Средняя точность по классам", Example = "stat.precision(y, pred)")]
    public static double Precision(
        [ScriptParam("истинные метки")] Vector y,
        [ScriptParam("предсказанные метки")] Vector pred)
    {
        RequirePair(y, pred, "precision");

        return MetricsForClassification.AveragePrecision(
            Datasets.Labels(y, "stat.precision"),
            Datasets.Labels(pred, "stat.precision"));
    }

    [ScriptFn("recall", "Средняя полнота по классам", Example = "stat.recall(y, pred)")]
    public static double Recall(
        [ScriptParam("истинные метки")] Vector y,
        [ScriptParam("предсказанные метки")] Vector pred)
    {
        RequirePair(y, pred, "recall");

        return MetricsForClassification.AverageRecall(
            Datasets.Labels(y, "stat.recall"),
            Datasets.Labels(pred, "stat.recall"));
    }

    [ScriptFn("f1", "F-мера", Example = "stat.f1(y, pred, beta: 1)")]
    public static double F1(
        [ScriptParam("истинные метки")] Vector y,
        [ScriptParam("предсказанные метки")] Vector pred,
        [ScriptParam("вес полноты относительно точности")] double beta = 1)
    {
        RequirePair(y, pred, "f1");

        return MetricsForClassification.FMeasure(
            Datasets.Labels(y, "stat.f1"),
            Datasets.Labels(pred, "stat.f1"),
            beta);
    }

    [ScriptFn("confusion", "Матрица ошибок", Example = "show stat.confusion(y, pred)")]
    public static Matrix Confusion(
        [ScriptParam("истинные метки")] Vector y,
        [ScriptParam("предсказанные метки")] Vector pred)
    {
        RequirePair(y, pred, "confusion");

        return MetricsForClassification.ConfusionMatrix(
            Datasets.Labels(y, "stat.confusion"),
            Datasets.Labels(pred, "stat.confusion"));
    }

    [ScriptFn("report", "Текстовый отчёт по классам", Example = "print(stat.report(y, pred))")]
    public static string Report(
        [ScriptParam("истинные метки")] Vector y,
        [ScriptParam("предсказанные метки")] Vector pred,
        [ScriptParam("подробно по каждому классу")] bool byClass = true)
    {
        RequirePair(y, pred, "report");

        return MetricsForClassification.FullReport(
            Datasets.Labels(y, "stat.report"),
            Datasets.Labels(pred, "stat.report"),
            1,
            byClass);
    }

    /// <summary>
    /// Сколько раз встречается каждое значение.
    /// </summary>
    /// <remarks>
    /// Возвращает таблицу, а не запись: размеры классов почти всегда хочется отсортировать
    /// либо показать, а таблица умеет и то, и другое.
    /// </remarks>
    [ScriptFn("counts", "Частоты значений таблицей «значение → сколько»", Example = "show stat.counts(labels)")]
    public static ScriptTable Counts([ScriptParam("выборка")] ScriptList values)
    {
        var order = new List<ScriptValue>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var firsts = new Dictionary<string, ScriptValue>(StringComparer.Ordinal);

        foreach (ScriptValue value in values)
        {
            string key = ScriptFormatter.Format(value, quoteStrings: false);

            if (counts.TryGetValue(key, out int seen))
            {
                counts[key] = seen + 1;
                continue;
            }

            counts[key] = 1;
            firsts[key] = value;
            order.Add(value);
        }

        var labels = new ScriptValue[order.Count];
        var totals = new Vector(order.Count);

        for (int i = 0; i < order.Count; i++)
        {
            string key = ScriptFormatter.Format(order[i], quoteStrings: false);
            labels[i] = firsts[key];
            totals[i] = counts[key];
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("value", labels),
            ScriptColumn.FromVector("count", totals),
        ]);
    }

    /// <summary>
    /// Силуэт кластеризации: насколько объекты ближе к своему кластеру, чем к чужому.
    /// </summary>
    /// <remarks>
    /// Считается напрямую, а не через библиотеку: во фреймворке этой метрики нет, а без неё
    /// оценить кластеризацию нечем — число кластеров подбирают именно по ней.
    /// </remarks>
    [ScriptFn("silhouette", "Средний силуэт кластеризации: от -1 до 1", Example = "stat.silhouette(x, labels)")]
    public static double Silhouette(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки кластеров")] Vector labels)
    {
        if (data.Height != labels.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"stat.silhouette: {data.Height} объектов и {labels.Count} меток");
        }

        Vector[] rows = Datasets.Rows(data);
        int[] clusters = Datasets.Labels(labels, "stat.silhouette");

        if (new HashSet<int>(clusters).Count < 2) return 0;

        double total = 0;

        for (int i = 0; i < rows.Length; i++)
        {
            double own = MeanDistance(rows, clusters, i, clusters[i], skipSelf: true);
            double nearest = double.PositiveInfinity;

            foreach (int other in new HashSet<int>(clusters))
            {
                if (other == clusters[i]) continue;

                double distance = MeanDistance(rows, clusters, i, other, skipSelf: false);
                if (distance < nearest) nearest = distance;
            }

            double denominator = Math.Max(own, nearest);
            total += denominator == 0 ? 0 : (nearest - own) / denominator;
        }

        return total / rows.Length;
    }

    private static double MeanDistance(Vector[] rows, int[] clusters, int index, int cluster, bool skipSelf)
    {
        double sum = 0;
        int count = 0;

        for (int j = 0; j < rows.Length; j++)
        {
            if (clusters[j] != cluster) continue;
            if (skipSelf && j == index) continue;

            sum += Distance(rows[index], rows[j]);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static double Distance(Vector a, Vector b)
    {
        double sum = 0;

        for (int i = 0; i < a.Count && i < b.Count; i++) sum += (a[i] - b[i]) * (a[i] - b[i]);

        return Math.Sqrt(sum);
    }

    // --- метрики сигналов ---

    [ScriptFn("mse", "Среднеквадратичная ошибка без корня", Example = "stat.mse(y, pred)")]
    public static double Mse(
        [ScriptParam("истинные значения")] Vector y,
        [ScriptParam("предсказания")] Vector pred)
    {
        RequirePair(y, pred, "mse");
        return MetricsForRegression.MSE(y, pred);
    }

    [ScriptFn("mape", "Средняя абсолютная ошибка в процентах", Example = "stat.mape(y, pred)")]
    public static double Mape(
        [ScriptParam("истинные значения")] Vector y,
        [ScriptParam("предсказания")] Vector pred)
    {
        RequirePair(y, pred, "mape");
        return MetricsForRegression.MAPE(y, pred);
    }

    [ScriptFn("snr_db", "Отношение сигнал/шум в децибелах", Example = "stat.snr_db(noisy, clean)")]
    public static double SignalToNoise(
        [ScriptParam("наблюдаемый сигнал")] Vector signal,
        [ScriptParam("эталонный сигнал")] Vector clean)
    {
        RequirePair(signal, clean, "snr_db");

        double power = 0, noise = 0;

        for (int i = 0; i < clean.Count; i++)
        {
            power += clean[i] * clean[i];
            noise += (signal[i] - clean[i]) * (signal[i] - clean[i]);
        }

        if (noise == 0) return double.PositiveInfinity;

        return 10 * Math.Log10(power / noise);
    }

    private static Vector Require(Vector v, string name) =>
        v.Count > 0 ? v : throw new ScriptError(
            DiagnosticCodes.IndexOutOfRange,
            $"stat.{name}: выборка пуста",
            "проверьте длину заранее: len(v) > 0");

    private static void RequirePair(Vector a, Vector b, string name)
    {
        _ = Require(a, name);

        if (a.Count == b.Count) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"stat.{name}: несовместимые размеры {a.Count} и {b.Count}",
            "выборки должны быть одной длины");
    }
}

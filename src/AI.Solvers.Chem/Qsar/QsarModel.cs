using AI.ClassicMath.AlgorithmAnalysis;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.ML.Regression;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Qsar;

/// <summary>
/// Настройки построения модели свойства
/// </summary>
public sealed class QsarOptions
{
    /// <summary>Нормировать признаки перед обучением</summary>
    public bool Standardize { get; set; } = true;

    /// <summary>
    /// Число блоков перекрёстной проверки; 0 - без проверки,
    /// -1 - проверка с исключением по одному
    /// </summary>
    public int CrossValidationFolds { get; set; } = 5;

    /// <summary>Порог дисперсии, ниже которого признак отбрасывается</summary>
    public double VarianceThreshold { get; set; } = 1e-10;

    /// <summary>
    /// Названия используемых дескрипторов; null - все доступные
    /// </summary>
    /// <remarks>
    /// Отбор признаков здесь не роскошь: на выборке из полутора десятков веществ
    /// три десятка дескрипторов описывают шум, а не свойство.
    /// </remarks>
    public IReadOnlyList<string> Features { get; set; }
}

/// <summary>
/// Показатели качества модели
/// </summary>
/// <param name="R2">Коэффициент детерминации на обучающей выборке</param>
/// <param name="Rmse">Среднеквадратичная ошибка обучения</param>
/// <param name="Mae">Средняя абсолютная ошибка обучения</param>
/// <param name="Q2">Коэффициент детерминации перекрёстной проверки</param>
/// <param name="RmseCv">Среднеквадратичная ошибка перекрёстной проверки</param>
public readonly record struct QsarQuality(double R2, double Rmse, double Mae, double Q2, double RmseCv);

/// <summary>
/// Модель свойства по структуре: линейная регрессия на молекулярных дескрипторах
/// </summary>
/// <remarks>
/// Модель без проверки на новых объектах ничего не стоит, поэтому обучение сразу
/// сопровождается перекрёстной проверкой, а предсказание - оценкой области
/// применимости по рычагу: структура, далеко выходящая за облако обучающих точек,
/// получает предупреждение, а не молча посчитанное число.
/// </remarks>
public sealed partial class QsarModel
{
    private readonly MultipleRegression _regression;
    private readonly int[] _columns;
    private readonly Matrix _inverseNormal;
    private readonly int _trainingCount;

    /// <summary>Названия использованных дескрипторов</summary>
    public IReadOnlyList<string> DescriptorNames { get; }

    /// <summary>Показатели качества</summary>
    public QsarQuality Quality { get; }

    /// <summary>Название моделируемого свойства</summary>
    public string Property { get; init; } = string.Empty;

    /// <summary>Порог рычага, за которым структура выходит из области применимости</summary>
    public double LeverageThreshold { get; }

    private QsarModel(
        MultipleRegression regression,
        int[] columns,
        IReadOnlyList<string> names,
        Matrix inverseNormal,
        int trainingCount,
        QsarQuality quality)
    {
        _regression = regression;
        _columns = columns;
        _inverseNormal = inverseNormal;
        _trainingCount = trainingCount;

        DescriptorNames = names;
        Quality = quality;

        // Обычный порог области применимости: три средних рычага
        LeverageThreshold = 3.0 * (columns.Length + 1) / trainingCount;
    }

    /// <summary>
    /// Обучает модель по структурам и измеренным значениям свойства
    /// </summary>
    /// <param name="smiles">Структуры обучающей выборки</param>
    /// <param name="property">Измеренные значения свойства</param>
    /// <param name="options">Настройки обучения</param>
    public static QsarModel Train(
        IReadOnlyList<string> smiles,
        IReadOnlyList<double> property,
        QsarOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(smiles);

        var descriptors = smiles.Select(MolecularDescriptors.Compute).ToArray();

        return Train(descriptors, property, options);
    }

    /// <summary>
    /// Обучает модель по готовым наборам дескрипторов
    /// </summary>
    /// <param name="descriptors">Дескрипторы обучающей выборки</param>
    /// <param name="property">Измеренные значения свойства</param>
    /// <param name="options">Настройки обучения</param>
    public static QsarModel Train(
        IReadOnlyList<DescriptorSet> descriptors,
        IReadOnlyList<double> property,
        QsarOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(property);

        if (descriptors.Count != property.Count)
            throw new ArgumentException("Число структур и число значений свойства должно совпадать");

        if (descriptors.Count < 4)
            throw new ArgumentException("Для обучения нужно не менее четырёх структур", nameof(descriptors));

        options ??= new QsarOptions();

        int[] columns = SelectColumns(descriptors, options);

        if (columns.Length == 0)
            throw new ArgumentException("Все дескрипторы постоянны на обучающей выборке", nameof(descriptors));

        if (columns.Length >= descriptors.Count)
            throw new ArgumentException(
                "Признаков не меньше, чем структур: модель будет описывать шум. Нужна выборка больше",
                nameof(descriptors));

        Vector[] features = descriptors.Select(d => Select(d.Values, columns)).ToArray();
        var targets = new Vector(property.ToArray());

        MultipleRegression regression = Fit(features, targets, options);
        var predicted = new Vector(features.Select(regression.Predict).ToArray());

        double r2 = MetricsForRegression.R2(targets, predicted);
        double rmse = MetricsForRegression.RMSE(targets, predicted);
        double mae = MetricsForRegression.MAE(targets, predicted);
        var (q2, rmseCv) = CrossValidate(features, targets, options);

        var names = columns.Select(c => MolecularDescriptors.Names[c]).ToArray();

        return new QsarModel(regression, columns, names, NormalInverse(features, columns.Length),
            descriptors.Count, new QsarQuality(r2, rmse, mae, q2, rmseCv));
    }

    /// <summary>Предсказывает свойство по структуре</summary>
    /// <param name="smiles">Строка SMILES</param>
    public double Predict(string smiles) => Predict(MolecularDescriptors.Compute(smiles));

    /// <summary>Предсказывает свойство по набору дескрипторов</summary>
    /// <param name="descriptors">Дескрипторы структуры</param>
    public double Predict(DescriptorSet descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        return _regression.Predict(Select(descriptors.Values, _columns));
    }

    /// <summary>
    /// Рычаг структуры: мера её удалённости от центра обучающей выборки
    /// </summary>
    /// <param name="descriptors">Дескрипторы структуры</param>
    public double Leverage(DescriptorSet descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        Vector selected = Select(descriptors.Values, _columns);
        int size = _columns.Length + 1;
        var row = new double[size];

        row[0] = 1;

        for (int i = 0; i < _columns.Length; i++)
            row[i + 1] = selected[i];

        double leverage = 0;

        for (int i = 0; i < size; i++)
        {
            double sum = 0;

            for (int j = 0; j < size; j++)
                sum += _inverseNormal[i, j] * row[j];

            leverage += row[i] * sum;
        }

        return leverage;
    }

    /// <summary>Попадает ли структура в область применимости модели</summary>
    /// <param name="smiles">Строка SMILES</param>
    public bool InDomain(string smiles) => Leverage(MolecularDescriptors.Compute(smiles)) <= LeverageThreshold;

    /// <summary>Отчёт по модели</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"Модель свойства{(string.IsNullOrEmpty(Property) ? string.Empty : ": " + Property)}");
        text.AppendLine(string.Format(culture, "  Обучающая выборка: {0} структур, признаков: {1}",
            _trainingCount, _columns.Length));
        text.AppendLine("  Признаки: " + string.Join(", ", DescriptorNames));
        text.AppendLine(string.Format(culture, "  R2 = {0:F4}, RMSE = {1:F4}, MAE = {2:F4}",
            Quality.R2, Quality.Rmse, Quality.Mae));

        if (!double.IsNaN(Quality.Q2))
        {
            text.AppendLine(string.Format(culture, "  Перекрёстная проверка: Q2 = {0:F4}, RMSE = {1:F4}",
                Quality.Q2, Quality.RmseCv));
        }

        text.AppendLine(string.Format(culture, "  Порог рычага: {0:F3}", LeverageThreshold));

        return text.ToString();
    }

    private static MultipleRegression Fit(Vector[] features, Vector targets, QsarOptions options)
    {
        var regression = new MultipleRegression(options.Standardize);
        regression.Train(features, targets);

        return regression;
    }

    private static (double Q2, double Rmse) CrossValidate(Vector[] features, Vector targets, QsarOptions options)
    {
        int folds = options.CrossValidationFolds switch
        {
            0 => 0,
            < 0 => features.Length,
            _ => Math.Min(options.CrossValidationFolds, features.Length)
        };

        if (folds < 2)
            return (double.NaN, double.NaN);

        var predicted = new double[features.Length];

        for (int fold = 0; fold < folds; fold++)
        {
            var trainFeatures = new List<Vector>();
            var trainTargets = new List<double>();

            for (int i = 0; i < features.Length; i++)
            {
                if (i % folds != fold)
                {
                    trainFeatures.Add(features[i]);
                    trainTargets.Add(targets[i]);
                }
            }

            if (trainFeatures.Count <= features[0].Count)
                return (double.NaN, double.NaN);

            MultipleRegression regression = Fit(trainFeatures.ToArray(), new Vector(trainTargets.ToArray()), options);

            for (int i = 0; i < features.Length; i++)
            {
                if (i % folds == fold)
                    predicted[i] = regression.Predict(features[i]);
            }
        }

        var predictions = new Vector(predicted);

        return (MetricsForRegression.R2(targets, predictions), MetricsForRegression.RMSE(targets, predictions));
    }

    private static int[] SelectColumns(IReadOnlyList<DescriptorSet> descriptors, QsarOptions options)
    {
        int width = descriptors[0].Values.Count;
        var columns = new List<int>();
        var requested = options.Features == null
            ? null
            : new HashSet<string>(options.Features, StringComparer.OrdinalIgnoreCase);

        if (requested != null)
        {
            foreach (string name in requested)
            {
                if (!MolecularDescriptors.Names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException($"Дескриптор {name} не рассчитывается", nameof(options));
            }
        }

        for (int column = 0; column < width; column++)
        {
            if (requested != null && !requested.Contains(MolecularDescriptors.Names[column]))
                continue;

            double mean = 0;

            foreach (DescriptorSet set in descriptors)
                mean += set.Values[column];

            mean /= descriptors.Count;
            double variance = 0;

            foreach (DescriptorSet set in descriptors)
            {
                double delta = set.Values[column] - mean;
                variance += delta * delta;
            }

            variance /= descriptors.Count;

            if (variance > options.VarianceThreshold)
                columns.Add(column);
        }

        return columns.ToArray();
    }

    private static Vector Select(Vector values, int[] columns)
    {
        var selected = new double[columns.Length];

        for (int i = 0; i < columns.Length; i++)
            selected[i] = values[columns[i]];

        return new Vector(selected);
    }

    // Псевдообращение матрицы XᵀX со столбцом единиц: на нём считается рычаг.
    // Обычное обращение здесь не годится - дескрипторы часто почти коллинеарны
    private static Matrix NormalInverse(Vector[] features, int width)
    {
        int size = width + 1;
        var normal = new Matrix(size, size);

        foreach (Vector feature in features)
        {
            var row = new double[size];
            row[0] = 1;

            for (int i = 0; i < width; i++)
                row[i + 1] = feature[i];

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                    normal[i, j] += row[i] * row[j];
            }
        }

        return Pseudoinverse.Compute(normal);
    }
}

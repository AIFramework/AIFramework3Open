using AI.DataStructs.Algebraic;
using AI.Econometrics;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>Размеченная пара для калибровки.</summary>
/// <param name="Key">Ключ пары: по нему пары делятся на обучение и отложенную часть.</param>
/// <param name="Features">Признаки пары.</param>
/// <param name="Verdict">Вердикт по коду.</param>
/// <param name="Same">Метка: то же поведение (решение «дубль» или исполнение).</param>
public sealed record CalibrationExample(string Key, DupFeatures Features, string Verdict, bool Same);

/// <summary>
/// Калибровка вердикта: вероятность того, что пара ведёт себя одинаково, по её признакам.
/// Логистическая регрессия — <see cref="LimitedDependent"/> из AI.Econometrics.
/// </summary>
/// <remarks>
/// <para>
/// Метки берутся из журнала решений (дубль — да; перекрытие, нет связи — нет) и из исполнения
/// (поведенческий дубль, дубль кроме крайних случаев, дубль с поправкой — да; расхождение,
/// разные и связанные функции — нет). Решение человека важнее исполнения той же пары.
/// </para>
/// <para>
/// Нужна там, где исполнить нельзя (770 из 1 120 пар библиотеки): вероятность упорядочивает
/// серую зону. Веса лежат в <c>%LOCALAPPDATA%\Atlas\calibration.json</c>, переобучение — командой.
/// </para>
/// </remarks>
public sealed record Calibration(double[] Weights, int Examples, int Holdout, double RuleAccuracy, double MajorityAccuracy, double ModelAccuracy, DateTime TrainedAt)
{
    /// <summary>Имена признаков в порядке весов (после свободного члена).</summary>
    public static readonly string[] Names = ["код", "описание", "имя", "вызовы", "константы", "управление", "входы те же", "один тип"];

    private static readonly Lazy<Calibration?> Loaded = new(() => Load(DefaultPath));

    /// <summary>Файл весов.</summary>
    public static string DefaultPath => Path.Combine(AtlasSettings.CacheRoot, "calibration.json");

    /// <summary>Калибровка с диска; <c>null</c> — не обучалась.</summary>
    public static Calibration? Current => Loaded.Value;

    /// <summary>Вероятность того же поведения.</summary>
    public double Probability(DupFeatures features)
    {
        double[] x = Vector(features);
        double z = Weights[0];
        for (int i = 0; i < x.Length; i++) z += Weights[i + 1] * x[i];
        return 1 / (1 + Math.Exp(-z));
    }

    /// <summary>
    /// Обучает на двух третях примеров и меряет на отложенной трети (деление по отпечатку ключа,
    /// одно и то же при каждом запуске). Точность сравнивается с правилом «копия — дубль» и с
    /// ответом большинства.
    /// </summary>
    public static Calibration Train(IReadOnlyList<CalibrationExample> examples)
    {
        CalibrationExample[] train = [.. examples.Where(example => !IsHoldout(example.Key))];
        CalibrationExample[] holdout = [.. examples.Where(example => IsHoldout(example.Key))];
        if (train.Length <= Names.Length + 1 || holdout.Length == 0 || train.All(e => e.Same) || train.All(e => !e.Same))
            throw new InvalidOperationException($"Мало размеченных пар: {train.Length} для обучения, {holdout.Length} отложено; нужны оба класса.");

        // Признак без разброса вырождает матрицу информации: его вес — ноль.
        int[] used = [.. Enumerable.Range(0, Names.Length).Where(j => train.Select(e => Vector(e.Features)[j]).Distinct().Count() > 1)];
        var x = new Matrix(train.Length, used.Length);
        for (int i = 0; i < train.Length; i++)
        {
            double[] row = Vector(train[i].Features);
            for (int j = 0; j < used.Length; j++) x[i, j] = row[used[j]];
        }

        LimitedDependentResult fit = LimitedDependent.Fit(x, new Vector([.. train.Select(e => e.Same ? 1.0 : 0.0)]),
            LimitedDependentModel.Logit, [.. used.Select(j => Names[j])]);

        double[] weights = new double[Names.Length + 1];
        weights[0] = fit.Coefficients[0].Estimate;
        for (int j = 0; j < used.Length; j++) weights[used[j] + 1] = fit.Coefficients[j + 1].Estimate;

        bool majority = train.Count(e => e.Same) * 2 >= train.Length;
        var model = new Calibration(weights, examples.Count, holdout.Length, 0, 0, 0, DateTime.UtcNow);

        return model with
        {
            RuleAccuracy = holdout.Average(e => (e.Verdict is "копия" or "копия в перегрузках") == e.Same ? 1.0 : 0.0),
            MajorityAccuracy = holdout.Average(e => e.Same == majority ? 1.0 : 0.0),
            ModelAccuracy = holdout.Average(e => model.Probability(e.Features) >= 0.5 == e.Same ? 1.0 : 0.0),
        };
    }

    /// <summary>Записывает веса.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Читает веса; нет файла или он битый — <c>null</c>.</summary>
    public static Calibration? Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Calibration>(File.ReadAllText(path)) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Признаки пары числами в порядке <see cref="Names"/>.</summary>
    public static double[] Vector(DupFeatures f) =>
        [f.Code, f.Doc, f.Name, f.Calls, f.Constants, f.Control, f.SameInputs ? 1 : 0, f.SameType ? 1 : 0];

    private static bool IsHoldout(string key) =>
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))[0] % 3 == 0;
}

/// <summary>
/// Какие единицы модель выбрала после поиска — открыла карточку <c>api_describe</c>. Выбранные
/// поднимаются в выдаче (<see cref="ApiSearch.UseSelections"/>), как в журнале использования
/// KnowledgeGraph: что пригодилось однажды, скорее пригодится снова.
/// </summary>
public sealed class SelectionJournal(string path)
{
    private readonly Dictionary<string, int> _counts = Read(path);

    /// <summary>Сколько раз выбрана каждая единица, по идентификатору.</summary>
    public IReadOnlyDictionary<string, int> Counts => _counts;

    /// <summary>Отмечает выбор и сразу записывает журнал.</summary>
    public void Record(CodeUnit unit)
    {
        _counts[unit.Id] = _counts.GetValueOrDefault(unit.Id) + 1;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(_counts));
    }

    private static Dictionary<string, int> Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? [] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

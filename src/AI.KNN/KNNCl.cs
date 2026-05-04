using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.ML.Classification;
using AI.ML.DataHandling.DataSets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace AI.KNN;

/// <summary>
/// Метод k-ближайших соседей.
/// Хранение: плоские массивы float[] (признаки) + int[] (метки) — без оберток List/StructClasses.
/// Поиск: вычисление расстояний в одном цикле без Parallel.For (overhead блокировок > выигрыша),
/// затем heap-based partial sort O(n log K) вместо полного Sort O(n log n).
/// </summary>
[Serializable]
public class KNNCl : IClassifier
{
    #region Поля и свойства

    /// <summary>Количество соседей.</summary>
    public int K { get; set; } = 4;

    /// <summary>Ширина окна Парзена (фиксированная).</summary>
    public double H { get; set; } = 1.0;

    /// <summary>Использовать фиксированную ширину окна.</summary>
    public bool IsFixed { get; set; } = false;

    /// <summary>Использовать метод Парзена (взвешивать соседей ядром).</summary>
    public bool IsParsenMethod { get; set; } = false;

    /// <summary>Ядро окна Парзена. По умолчанию — RBF.</summary>
    public Func<double, double> KernelParsenWindow { get; set; }

    /// <summary>Функция расстояния. По умолчанию — квадрат евклидова.</summary>
    public Func<Vector, Vector, double> Dist { get; set; }

    // --- компактное хранение ---
    // _features[i * _dim + j] — j-й признак i-й точки
    private float[] _features = Array.Empty<float>();
    private int[]   _labels   = Array.Empty<int>();
    private int _count;   // число добавленных точек
    private int _dim;     // размерность признаков

    // уникальные классы для подсчёта _numClasses
    private readonly HashSet<int> _classSet = new HashSet<int>();
    private int _numClasses;

    // рабочие буферы (переиспользуются при предсказании, не аллоцируются каждый раз)
    [NonSerialized] private double[] _distBuf;
    [NonSerialized] private int[]    _idxBuf;
    [NonSerialized] private double[] _classBuf;

    // Совместимость с кодом, который читает Classes напрямую
    [NonSerialized] private StructClasses _compatView;

    /// <summary>
    /// Обратно-совместимый доступ к данным через StructClasses.
    /// Создаётся по требованию, не синхронизирован — только для чтения.
    /// </summary>
    public StructClasses Classes
    {
        get
        {
            if (_compatView == null) RebuildCompatView();
            return _compatView;
        }
    }

    #endregion

    #region Конструкторы

    /// <summary>Создаёт пустой классификатор с настройками по умолчанию.</summary>
    public KNNCl()
    {
        KernelParsenWindow = RbfK;
        Dist = Distances.BaseDist.SquareEucl;
    }

    /// <summary>Создаёт классификатор и обучает его на датасете.</summary>
    public KNNCl(VectorDataset vectorClasses) : this()
    {
        foreach (var item in vectorClasses)
            AddClass(item.Features, item.ClassMark);
    }

    /// <summary>Создаёт классификатор из StructClasses (обратная совместимость).</summary>
    public KNNCl(StructClasses classifikator) : this()
    {
        if (classifikator == null) throw new ArgumentNullException(nameof(classifikator));
        foreach (var item in classifikator)
            AddClass(item.Features, item.ClassMark);
    }

    #endregion

    /// <summary>Радиально-базисное ядро: exp(-2r²).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double RbfK(double r) => Math.Exp(-2.0 * r * r);

    #region Обучение

    /// <summary>Добавить точку в обучающую выборку.</summary>
    public void AddClass(Vector features, int label)
    {
        if (features == null) throw new ArgumentNullException(nameof(features));

        int d = features.Count;

        // Инициализация размерности при первом вызове
        if (_count == 0)
        {
            _dim = d;
        }
        else if (d != _dim)
        {
            throw new ArgumentException($"Ожидалась размерность {_dim}, получено {d}.");
        }

        // Расширение массивов при необходимости (стратегия удвоения)
        if (_count * _dim + _dim > _features.Length)
        {
            int newCap = Math.Max(16, _features.Length * 2);
            while (newCap < (_count + 1) * _dim) newCap *= 2;

            Array.Resize(ref _features, newCap);
            Array.Resize(ref _labels, newCap / _dim + 1);
        }

        // Копируем признаки как float (экономия памяти 2×)
        int baseIdx = _count * _dim;
        for (int j = 0; j < d; j++)
            _features[baseIdx + j] = (float)features[j];

        _labels[_count] = label;
        _count++;

        // Обновляем число уникальных классов через HashSet — O(1)
        if (_classSet.Add(label))
            _numClasses = _classSet.Count;

        // Инвалидируем кэши
        _compatView = null;
        _distBuf    = null;
    }

    /// <inheritdoc/>
    public void Train(Vector[] features, int[] classes)
    {
        if (features.Length != classes.Length)
            throw new InvalidOperationException("Размерности векторов признаков и классов не совпадают.");
        for (int i = 0; i < features.Length; i++)
            AddClass(features[i], classes[i]);
    }

    /// <inheritdoc/>
    public void Train(VectorDataset dataset)
    {
        foreach (var item in dataset)
            AddClass(item.Features, item.ClassMark);
    }

    #endregion

    #region Предсказание

    /// <inheritdoc/>
    public int Classify(Vector inp)
    {
        if (inp == null) throw new ArgumentNullException(nameof(inp));
        return ClassifyProbVector(inp).MaxElementIndex();
    }

    /// <inheritdoc/>
    public Vector ClassifyProbVector(Vector inp)
    {
        if (inp == null) throw new ArgumentNullException(nameof(inp));
        if (_count == 0) throw new InvalidOperationException("Классификатор не обучен.");

        EnsureBuffers();

        // 1. Вычисляем расстояния до всех точек — один проход без локов
        ComputeDistances(inp);

        // 2. Partial sort: находим K ближайших за O(n log K) вместо O(n log n)
        int limit = IsFixed && IsParsenMethod ? _count : Math.Min(K, _count);
        double h = IsFixed && IsParsenMethod ? H : GetKthDist(limit);

        // 3. Накапливаем голоса в _classBuf без аллокаций Vector
        Array.Clear(_classBuf, 0, _numClasses);
        double sumWeights = 0;

        for (int i = 0; i < limit; i++)
        {
            int idx   = _idxBuf[i];
            int label = _labels[idx];
            double weight;

            if (IsParsenMethod)
            {
                double r = _distBuf[idx] / h;
                weight = KernelParsenWindow(r);
            }
            else
            {
                weight = 1.0;
            }

            if (label < _numClasses)
            {
                _classBuf[label] += weight;
                sumWeights       += weight;
            }
        }

        // 4. Нормируем и возвращаем Vector
        double norm = sumWeights > 1e-12 ? sumWeights : 1.0;
        double[] result = new double[_numClasses];
        for (int c = 0; c < _numClasses; c++)
            result[c] = _classBuf[c] / norm;

        return new Vector(result);
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Вычисляет расстояния от inp до каждой обучающей точки (unsafe, без аллокаций).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ComputeDistances(Vector inp)
    {
        int n = _count, d = _dim;
        double[] dist = _distBuf;
        int[]    idx  = _idxBuf;

        // Копируем inp в локальный массив, чтобы получить pinnable double[]
        double[] inpArr = new double[d];
        for (int j = 0; j < d; j++) inpArr[j] = inp[j];

        fixed (float*  pFeat = _features)
        fixed (double* pDist = dist)
        fixed (double* pInp  = inpArr)
        {
            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                float* row = pFeat + i * d;
                for (int j = 0; j < d; j++)
                {
                    double diff = pInp[j] - row[j];
                    sum += diff * diff;
                }
                pDist[i] = sum;
                idx[i]   = i;
            }
        }
    }

    /// <summary>
    /// Частичная сортировка: переупорядочивает _idxBuf так, что первые k элементов —
    /// индексы k ближайших точек в произвольном порядке. O(n log k).
    /// Возвращает расстояние до k-го ближайшего соседа.
    /// </summary>
    private double GetKthDist(int k)
    {
        // Min-heap на k элементах -> быстрее полного Sort при k << n
        double[] dist = _distBuf;
        int[]    idx  = _idxBuf;
        int n = _count;

        // Инициализируем первые k элементов в max-heap (по расстоянию)
        // Heap: idx[0..k-1], ключ — dist[idx[i]]
        int[] heap = new int[k];
        for (int i = 0; i < k; i++) heap[i] = idx[i];
        BuildMaxHeap(heap, dist, k);

        // Обрабатываем оставшиеся n-k точек
        for (int i = k; i < n; i++)
        {
            if (dist[idx[i]] < dist[heap[0]])
            {
                heap[0] = idx[i];
                SiftDown(heap, dist, 0, k);
            }
        }

        // Перекладываем heap -> _idxBuf[0..k-1]
        for (int i = 0; i < k; i++) idx[i] = heap[i];

        // Находим максимальное расстояние среди k соседей (= k-е расстояние)
        double kthDist = 0;
        for (int i = 0; i < k; i++)
            if (dist[idx[i]] > kthDist) kthDist = dist[idx[i]];

        return kthDist < 1e-12 ? 1e-12 : kthDist;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildMaxHeap(int[] heap, double[] dist, int k)
    {
        for (int i = k / 2 - 1; i >= 0; i--)
            SiftDown(heap, dist, i, k);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SiftDown(int[] heap, double[] dist, int root, int k)
    {
        while (true)
        {
            int largest = root;
            int left    = 2 * root + 1;
            int right   = 2 * root + 2;

            if (left  < k && dist[heap[left]]  > dist[heap[largest]]) largest = left;
            if (right < k && dist[heap[right]] > dist[heap[largest]]) largest = right;

            if (largest == root) break;

            (heap[root], heap[largest]) = (heap[largest], heap[root]);
            root = largest;
        }
    }

    private void EnsureBuffers()
    {
        if (_distBuf == null || _distBuf.Length < _count)
        {
            _distBuf  = new double[_count];
            _idxBuf   = new int[_count];
            _classBuf = new double[_numClasses];
        }
        else if (_classBuf == null || _classBuf.Length < _numClasses)
        {
            _classBuf = new double[_numClasses];
        }
    }

    private void RebuildCompatView()
    {
        _compatView = new StructClasses();
        for (int i = 0; i < _count; i++)
        {
            double[] feat = new double[_dim];
            int baseIdx = i * _dim;
            for (int j = 0; j < _dim; j++)
                feat[j] = _features[baseIdx + j];

            _compatView.Add(new VectorDatasetItem
            {
                Features  = new Vector(feat),
                ClassMark = _labels[i]
            });
        }
    }

    // Bridge-свойства для SafeSerializer/KNNClJsonConverter (тот же сборка)
    internal float[] InternalFeatures { get => _features; set => _features = value ?? []; }
    internal int[]   InternalLabels   { get => _labels;   set => _labels   = value ?? []; }
    internal int     InternalCount    { get => _count;    set => _count    = value; }
    internal int     InternalDim      { get => _dim;      set => _dim      = value; }

    internal void RebuildClassStats()
    {
        _classSet.Clear();
        for (int i = 0; i < _count; i++)
            _classSet.Add(_labels[i]);
        _numClasses = _classSet.Count;
    }

    #endregion

    #region Сохранение / загрузка

    /// <inheritdoc/>
    public void Save(string path)   => AI.DataStructs.SafeSerializer.Save(path, this, AI.KNN.Json.AiKnnJsonOptions.Default);

    /// <inheritdoc/>
    public void Save(Stream stream) => AI.DataStructs.SafeSerializer.Save(stream, this, AI.KNN.Json.AiKnnJsonOptions.Default);

    /// <summary>Загрузить из файла.</summary>
    public static KNNCl Load(string path)   => AI.DataStructs.SafeSerializer.Load<KNNCl>(path, AI.KNN.Json.AiKnnJsonOptions.Default);

    /// <summary>Загрузить из потока.</summary>
    public static KNNCl Load(Stream stream) => AI.DataStructs.SafeSerializer.Load<KNNCl>(stream, AI.KNN.Json.AiKnnJsonOptions.Default);

    /// <summary>Загрузить из csv (признаки; метка класса).</summary>
    public static KNNCl GetKNN(string pathToEtallonClassCsv)
    {
        var ds = new VectorDataset(pathToEtallonClassCsv);
        return new KNNCl(ds);
    }

    #endregion
}

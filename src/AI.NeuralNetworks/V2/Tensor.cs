using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.V2;

/// <summary>
/// N-мерный тензор с поддержкой autograd. Аналог <c>torch.Tensor</c>.
/// </summary>
/// <remarks>
/// <para>
/// Тензор — это <i>view</i> поверх <see cref="TensorStorage"/>: shape/strides/offset
/// описывают, как интерпретировать линейный буфер. Reshape, transpose, slice не
/// копируют данные, а создают новый Tensor с тем же storage.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> Shape/Strides/DType/Device/Storage immutable после
/// конструирования. Чтение из множества потоков — без блокировок. Запись в storage
/// (например, оптимизатором) — caller-sync (см. документацию оптимизатора).
/// </para>
/// <para>
/// <b>Autograd:</b> если <see cref="RequiresGrad"/> = true, операции автоматически
/// записываются в текущий <see cref="TapeContext"/> и потом раскручиваются через
/// <see cref="Backward"/>. Tape — per-thread (через <see cref="AsyncLocal{T}"/>),
/// что даёт безопасное параллельное обучение разных моделей.
/// </para>
/// </remarks>
public sealed partial class Tensor
{
    private readonly TensorStorage _storage;
    private readonly int[] _shape;
    private readonly int[] _strides;
    private readonly int _offset;

    /// <summary>Хранилище данных (shared между view-ами).</summary>
    public TensorStorage Storage => _storage;

    /// <summary>Форма тензора (immutable).</summary>
    public Shape Shape { get; }

    /// <summary>Страйды по осям (immutable).</summary>
    public ReadOnlySpan<int> Strides => _strides;

    /// <summary>Базовый offset в storage (для view-тензоров после slice).</summary>
    public int Offset => _offset;

    /// <summary>Число осей.</summary>
    public int Rank => _shape.Length;

    /// <summary>Тип элемента.</summary>
    public DType DType => _storage.DType;

    /// <summary>Устройство.</summary>
    public Device Device => _storage.Device;

    /// <summary>Общее число элементов.</summary>
    public long NumElements => Shape.NumElements;

    /// <summary>Контигуозен ли тензор в row-major порядке.</summary>
    public bool IsContiguous => V2.Strides.IsContiguous(_shape, _strides);

    #region Autograd

    private bool _requiresGradLeaf;

    /// <summary>
    /// True, если градиент должен накапливаться по этому тензору. Для leaf-тензоров
    /// — определяется явно (<see cref="SetRequiresGrad"/>); для производных
    /// (<see cref="GradFn"/> != null) — всегда true (их градиент маршрутизируется
    /// к leaf-входам через граф).
    /// </summary>
    public bool RequiresGrad => _requiresGradLeaf || GradFn != null;

    /// <summary>
    /// True, если этот тензор — leaf-вход (явно требует grad), а не результат операции.
    /// Используется в backward-движке для записи градиента в <see cref="Grad"/>.
    /// </summary>
    public bool IsLeafRequiringGrad => _requiresGradLeaf && GradFn == null;

    /// <summary>
    /// Узел autograd-графа, создавший этот тензор (null для leaf-тензоров).
    /// </summary>
    public Function GradFn { get; internal set; }

    /// <summary>Накопленный градиент (заполняется в <see cref="Backward"/>).</summary>
    public Tensor Grad { get; internal set; }

    /// <summary>
    /// Установить флаг requires_grad. Возвращает сам тензор (fluent-API).
    /// Только для leaf-тензоров (без GradFn) — иначе ошибка.
    /// </summary>
    public Tensor SetRequiresGrad(bool value = true)
    {
        if (value && GradFn != null)
            throw new InvalidOperationException(
                "RequiresGrad можно изменить только у leaf-тензоров (GradFn == null).");
        _requiresGradLeaf = value;
        return this;
    }

    #endregion Autograd

    #region Конструктор

    /// <summary>
    /// Внутренний конструктор-view. Использовать через фабрики и view-методы.
    /// </summary>
    internal Tensor(TensorStorage storage, Shape shape, int[] strides, int offset = 0)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        if (strides == null || strides.Length != shape.Rank)
            throw new ArgumentException("strides должен иметь ту же длину, что и shape.Rank", nameof(strides));
        _shape = shape.ToArray();
        _strides = (int[])strides.Clone();
        _offset = offset;
    }

    #endregion Конструктор
}

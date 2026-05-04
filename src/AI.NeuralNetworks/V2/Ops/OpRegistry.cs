using System;
using System.Collections.Concurrent;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Кодовое имя операции — стабильный идентификатор для dispatch и сериализации.
/// </summary>
public enum OpCode : ushort
{
    Add, Sub, Mul, Div, Pow, Neg, Abs,
    Exp, Log, Sqrt, Sin, Cos,
    Relu, Sigmoid, Tanh, Gelu, Silu, Softmax, LogSoftmax,
    MatMul, BatchedMatMul, AddMM,
    Sum, Mean, Max, Min, ArgMax, ArgMin, Var, Std,
    Reshape, Transpose, Permute, Squeeze, Unsqueeze, Expand, Slice, Contiguous,
    Conv1d, Conv2d, Conv3d, MaxPool, AvgPool,
    LayerNorm, BatchNorm, GroupNorm, RMSNorm,
    Embedding, Dropout,
    CrossEntropy, BCE, MSE, L1, NLL, KL,
    /// <summary>Fused AdamW step (in-place update параметра): inputs=[p,g,m,v], attrs=<see cref="FusedAdamWAttrs"/>.</summary>
    FusedAdamW,
    /// <summary>Fused y = gelu(x · W^T + b): inputs=[x, W, b], outputs=[y].</summary>
    FusedLinearGelu,
    /// <summary>Fused y = relu(x + bias_broadcast): inputs=[x, bias], outputs=[y].</summary>
    FusedAddBiasRelu,
    /// <summary>y = x · s, где s — обычный скаляр (float). Backward: gx = s·gy. Атрибуты: <see cref="ScalarAttrs"/>.</summary>
    MulScalar,
    /// <summary>Конкатенация N тензоров вдоль одной оси. Атрибуты: <see cref="CatAttrs"/>.</summary>
    Cat,
    /// <summary>
    /// In-place scatter контигиозного <c>src</c> в slice <c>dst[..., start:start+len, ...]</c>
    /// вдоль указанной оси. inputs=[dst, src], attrs=<see cref="ScatterAttrs"/>; возвращает [dst].
    /// Используется для <c>NarrowFunction.Backward</c> и <c>SelectFunction.Backward</c> на GPU
    /// без D2H/H2D round-trip.
    /// </summary>
    ScatterSlice,
    /// <summary>
    /// Один fused-шаг LSTM на GPU (forward + autograd). inputs=[preact (B,4H), cPrev (B,H)];
    /// возвращает [packed (2,B,H)] (h_new = packed[0], c_new = packed[1]). Атрибуты: <see cref="LstmStepAttrs"/>.
    /// </summary>
    LstmStep,
    /// <summary>
    /// Один fused-шаг GRU на GPU (forward + autograd). inputs=[gx (B,3H), gh (B,3H), hPrev (B,H)];
    /// возвращает [hNew (B,H)]. Атрибуты: <see cref="GruStepAttrs"/>.
    /// </summary>
    GruStep,
    /// <summary>
    /// Полный fused forward LSTM-последовательности (T шагов в одном Function).
    /// inputs=[xProj (T,B,4H), wHhT (H,4H), h0 (B,H) ИЛИ null-как-Empty(0), c0 (B,H) ИЛИ null-как-Empty(0)];
    /// возвращает [outputs (T,B,H)]; hN/cN читаются как outputs[T-1] и view внутреннего cAll.
    /// Атрибуты: <see cref="LstmSeqAttrs"/>. Используется только на GPU.
    /// </summary>
    LstmSeq,
    /// <summary>
    /// Полный fused forward GRU-последовательности (T шагов в одном Function).
    /// inputs=[xProj (T,B,3H), wHhT (H,3H), bHh (3H) или Empty(0), h0 (B,H) или Empty(0)];
    /// возвращает [outputs (T,B,H)]. Атрибуты: <see cref="GruSeqAttrs"/>. Только GPU.
    /// </summary>
    GruSeq,
    /// <summary>
    /// Полный fused forward Vanilla-RNN-последовательности (T шагов в одном Function).
    /// inputs=[xProj (T,B,H), wHhT (H,H), h0 (B,H) или Empty(0)];
    /// возвращает [outputs (T,B,H)]. Атрибуты: <see cref="RnnSeqAttrs"/>. Только GPU.
    /// </summary>
    RnnSeq,
    Custom = 65535,
}

/// <summary>
/// Параметры reduction-op'ов (Sum/Mean/Max/Min): свёртка по конкретной оси либо по всему тензору.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Axis"/> = <c>null</c> — свёртка по ВСЕМ элементам (выход — скаляр shape <c>()</c>).
/// Иначе — по указанной оси, размерность сохраняется при <see cref="KeepDim"/>=true.
/// </para>
/// </remarks>
public sealed class ReduceAttrs
{
    /// <summary>Ось для свёртки; null — свёртка по всему тензору.</summary>
    public int? Axis { get; }
    /// <summary>Сохранять ли свёрнутую ось как dim=1 (PyTorch-стиль).</summary>
    public bool KeepDim { get; }
    /// <summary>Создать атрибуты для axis-reduce или all-reduce.</summary>
    public ReduceAttrs(int? axis, bool keepDim) { Axis = axis; KeepDim = keepDim; }
}

/// <summary>
/// Параметры softmax/log-softmax: ось нормализации.
/// </summary>
/// <remarks>
/// <para>
/// Backend разворачивает входной тензор в логическую (outer, dim, inner) разметку,
/// где <c>dim = shape[axis]</c>. Делается один thread на (outer, inner) пару,
/// а свёртка идёт по dim — это позволяет одному kernel'у обрабатывать любое количество
/// строк параллельно.
/// </para>
/// </remarks>
public sealed class SoftmaxAttrs
{
    /// <summary>Нормализованная (положительная) ось softmax.</summary>
    public int Axis { get; }
    /// <summary>Произведение размеров осей слева от <see cref="Axis"/>.</summary>
    public long Outer { get; }
    /// <summary>Размер по оси <see cref="Axis"/> (число элементов в softmax-векторе).</summary>
    public int Dim { get; }
    /// <summary>Произведение размеров осей справа от <see cref="Axis"/>.</summary>
    public long Inner { get; }
    /// <summary>Создать softmax-атрибуты.</summary>
    public SoftmaxAttrs(int axis, long outer, int dim, long inner)
    { Axis = axis; Outer = outer; Dim = dim; Inner = inner; }
}

/// <summary>
/// Скалярные атрибуты op'а (например, <see cref="OpCode.MulScalar"/>): один float.
/// </summary>
public sealed class ScalarAttrs
{
    /// <summary>Скаляр, умножаемый/добавляемый к каждому элементу.</summary>
    public float Value { get; }
    /// <summary>Создать атрибуты с указанным значением.</summary>
    public ScalarAttrs(float value) { Value = value; }
}

/// <summary>
/// Параметры конкатенации: ось и размеры по этой оси для каждого входа
/// (нужны бэквард-функции для разделения градиента на куски).
/// </summary>
public sealed class CatAttrs
{
    /// <summary>Нормализованная ось склейки (≥ 0).</summary>
    public int Axis { get; }
    /// <summary>Размер каждого входного тензора по оси <see cref="Axis"/>.</summary>
    public int[] Sizes { get; }
    /// <summary>Создать атрибуты Cat.</summary>
    public CatAttrs(int axis, int[] sizes) { Axis = axis; Sizes = sizes; }
}

/// <summary>
/// Параметры in-place scatter: куда копировать и сколько. Используется
/// для GPU-нативного backward Narrow/Select.
/// </summary>
public sealed class ScatterAttrs
{
    /// <summary>Нормализованная ось.</summary>
    public int Axis { get; }
    /// <summary>Начальный индекс по оси.</summary>
    public int Start { get; }
    /// <summary>Длина копируемого фрагмента (число элементов по оси).</summary>
    public int Length { get; }
    /// <summary>Создать атрибуты scatter.</summary>
    public ScatterAttrs(int axis, int start, int length) { Axis = axis; Start = start; Length = length; }
}

/// <summary>
/// Параметры fused-шага LSTM: B (batch) и H (hidden). Layout preact = (B, 4H)
/// в порядке гейтов <c>(i, f, g, o)</c>.
/// </summary>
public sealed class LstmStepAttrs
{
    /// <summary>Размер батча.</summary>
    public int B { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int H { get; }
    /// <summary>Создать атрибуты.</summary>
    public LstmStepAttrs(int b, int h) { B = b; H = h; }
}

/// <summary>
/// Параметры fused-шага GRU: B (batch) и H (hidden). Layout gx/gh = (B, 3H)
/// в порядке гейтов <c>(r, z, n)</c>.
/// </summary>
public sealed class GruStepAttrs
{
    /// <summary>Размер батча.</summary>
    public int B { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int H { get; }
    /// <summary>Создать атрибуты.</summary>
    public GruStepAttrs(int b, int h) { B = b; H = h; }
}

/// <summary>
/// Параметры fused-полной-LSTM-последовательности. T — длина, B — batch, H — hidden.
/// Поля <c>HasH0</c>/<c>HasC0</c> = false -> начальное состояние нулевое (без autograd).
/// </summary>
public sealed class LstmSeqAttrs
{
    /// <summary>Длина последовательности.</summary>
    public int T { get; }
    /// <summary>Размер батча.</summary>
    public int B { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int H { get; }
    /// <summary>true, если в inputs передан h0 (а не пустышка).</summary>
    public bool HasH0 { get; }
    /// <summary>true, если в inputs передан c0 (а не пустышка).</summary>
    public bool HasC0 { get; }
    /// <summary>Создать атрибуты.</summary>
    public LstmSeqAttrs(int t, int b, int h, bool hasH0, bool hasC0)
    { T = t; B = b; H = h; HasH0 = hasH0; HasC0 = hasC0; }
}

/// <summary>
/// Параметры fused-полной-GRU-последовательности.
/// </summary>
public sealed class GruSeqAttrs
{
    /// <summary>Длина последовательности.</summary>
    public int T { get; }
    /// <summary>Размер батча.</summary>
    public int B { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int H { get; }
    /// <summary>true, если в inputs передан bHh (а не пустышка).</summary>
    public bool HasBhh { get; }
    /// <summary>true, если в inputs передан h0 (а не пустышка).</summary>
    public bool HasH0 { get; }
    /// <summary>Создать атрибуты.</summary>
    public GruSeqAttrs(int t, int b, int h, bool hasBhh, bool hasH0)
    { T = t; B = b; H = h; HasBhh = hasBhh; HasH0 = hasH0; }
}

/// <summary>
/// Параметры fused-полной-RNN-последовательности (vanilla, активация tanh или relu).
/// </summary>
public sealed class RnnSeqAttrs
{
    /// <summary>Длина последовательности.</summary>
    public int T { get; }
    /// <summary>Размер батча.</summary>
    public int B { get; }
    /// <summary>Размер скрытого состояния.</summary>
    public int H { get; }
    /// <summary>0 = tanh, 1 = relu.</summary>
    public int Nonlinearity { get; }
    /// <summary>true, если в inputs передан h0 (а не пустышка).</summary>
    public bool HasH0 { get; }
    /// <summary>Создать атрибуты.</summary>
    public RnnSeqAttrs(int t, int b, int h, int nonlinearity, bool hasH0)
    { T = t; B = b; H = h; Nonlinearity = nonlinearity; HasH0 = hasH0; }
}

/// <summary>Параметры fused AdamW шага (используется как <c>attrs</c>).</summary>
public sealed class FusedAdamWAttrs
{
    /// <summary>Learning rate.</summary>
    public float Lr { get; }
    /// <summary>β₁.</summary>
    public float Beta1 { get; }
    /// <summary>β₂.</summary>
    public float Beta2 { get; }
    /// <summary>ε для знаменателя.</summary>
    public float Eps { get; }
    /// <summary>Decoupled weight decay.</summary>
    public float WeightDecay { get; }
    /// <summary>Bias correction №1: 1 − β₁ᵗ.</summary>
    public float Bc1 { get; }
    /// <summary>Bias correction №2: 1 − β₂ᵗ.</summary>
    public float Bc2 { get; }
    /// <summary>Создать набор параметров для одного fused-шага.</summary>
    public FusedAdamWAttrs(float lr, float beta1, float beta2, float eps, float wd, float bc1, float bc2)
    { Lr = lr; Beta1 = beta1; Beta2 = beta2; Eps = eps; WeightDecay = wd; Bc1 = bc1; Bc2 = bc2; }
}

/// <summary>
/// Делегат forward kernel-а.
/// </summary>
public delegate Tensor[] OpKernel(Tensor[] inputs, object attrs);

/// <summary>
/// Реестр kernel-ов для V2-операций. Ключ — <c>(OpCode, DType, DeviceType, DeviceIndex)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Используется для ONNX-импорта/экспорта, fused-kernels (Phase 8) и для случаев,
/// когда нужен generic-доступ по строковому/code-имени op'а. Большая часть
/// hot-path операций идёт через <see cref="ElementwiseDispatch"/> с компайл-тайм
/// инлайном — реестр для них не обязателен.
/// </para>
/// <para>
/// <b>Multi-device:</b> регистрация может быть с конкретным <see cref="Device"/>
/// (включая Index) либо «any» — для всех индексов данного <see cref="DeviceType"/>.
/// При поиске сначала возвращается ядро для конкретного <c>(deviceType, index)</c>,
/// затем — fallback на «any» (<c>index = -1</c>). Это нужно, чтобы кэш скомпилированных
/// ILGPU-ядер был привязан к каждому конкретному устройству, иначе диспатч на не то
/// устройство вызовет невалидную работу с памятью.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> регистрация и поиск через <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </para>
/// </remarks>
public static class OpRegistry
{
    private readonly struct Key : IEquatable<Key>
    {
        public OpCode Op { get; }
        public DType DType { get; }
        public DeviceType DeviceType { get; }
        /// <summary>-1 = any index (fallback).</summary>
        public int DeviceIndex { get; }
        public Key(OpCode op, DType dt, DeviceType d, int idx)
        { Op = op; DType = dt; DeviceType = d; DeviceIndex = idx; }
        public bool Equals(Key other)
            => Op == other.Op && DType == other.DType
               && DeviceType == other.DeviceType && DeviceIndex == other.DeviceIndex;
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public override int GetHashCode()
            => HashCode.Combine((int)Op, (int)DType, (int)DeviceType, DeviceIndex);
    }

    private static readonly ConcurrentDictionary<Key, OpKernel> _kernels = new();

    /// <summary>
    /// Зарегистрировать kernel для указанного op + dtype + device (включая <see cref="Device.Index"/>).
    /// </summary>
    public static void Register(OpCode op, DType dt, Device device, OpKernel kernel)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        _kernels[new Key(op, dt, device.Type, device.Index)] = kernel;
    }

    /// <summary>
    /// Зарегистрировать kernel для всех устройств данного типа (без привязки к Index).
    /// Полезно для CPU и для тестовых fallback'ов.
    /// </summary>
    public static void Register(OpCode op, DType dt, DeviceType dev, OpKernel kernel)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        _kernels[new Key(op, dt, dev, -1)] = kernel;
    }

    /// <summary>Найти kernel или null. Сначала — для конкретного device.Index, затем «any».</summary>
    public static OpKernel TryGet(OpCode op, DType dt, Device device)
    {
        if (_kernels.TryGetValue(new Key(op, dt, device.Type, device.Index), out var k)) return k;
        if (_kernels.TryGetValue(new Key(op, dt, device.Type, -1), out var any)) return any;
        return null;
    }

    /// <summary>Найти kernel или null (legacy — без учёта device.Index, fallback «any»).</summary>
    public static OpKernel TryGet(OpCode op, DType dt, DeviceType dev)
    {
        // Берём «any» (idx = -1). Если кто-то регистрировал только для конкретного
        // индекса — этот overload не найдёт; используйте overload с Device.
        return _kernels.TryGetValue(new Key(op, dt, dev, -1), out var k) ? k : null;
    }

    /// <summary>Найти kernel; бросить, если не зарегистрирован.</summary>
    public static OpKernel Get(OpCode op, DType dt, Device device)
    {
        var k = TryGet(op, dt, device);
        if (k == null)
            throw new NotSupportedException(
                $"Kernel для {op} ({dt}, {device}) не зарегистрирован. " +
                "Используйте OpRegistry.Register или TensorOps.* для напрямую инлайнируемых операций.");
        return k;
    }

    /// <summary>Найти kernel; бросить, если не зарегистрирован (legacy DeviceType).</summary>
    public static OpKernel Get(OpCode op, DType dt, DeviceType dev)
    {
        var k = TryGet(op, dt, dev);
        if (k == null)
            throw new NotSupportedException(
                $"Kernel для {op} ({dt}, {dev}) не зарегистрирован. " +
                "Используйте OpRegistry.Register или TensorOps.* для напрямую инлайнируемых операций.");
        return k;
    }
}

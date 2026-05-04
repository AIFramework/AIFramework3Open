using System;

namespace AI.ML.NeuralNetworks.V2;

/// <summary>
/// Утилиты для работы со страйдами тензора (шаг между соседними элементами по каждой оси).
/// </summary>
/// <remarks>
/// Страйды позволяют делать zero-copy reshape/permute/transpose/expand. Stride=0 на оси
/// означает, что измерение «вещается» (broadcast) — типичный numpy-трюк.
/// </remarks>
public static class Strides
{
    /// <summary>
    /// Вычислить row-major (C-order) страйды: последняя ось имеет stride=1,
    /// предыдущая — произведение всех правее, и так далее.
    /// </summary>
    public static int[] RowMajor(ReadOnlySpan<int> dims)
    {
        var s = new int[dims.Length];
        if (dims.Length == 0) return s;
        s[^1] = 1;
        for (int i = dims.Length - 2; i >= 0; i--)
            s[i] = s[i + 1] * dims[i + 1];
        return s;
    }

    /// <summary>
    /// Контигуозен ли тензор в row-major порядке: текущие страйды должны совпадать с C-order.
    /// </summary>
    public static bool IsContiguous(ReadOnlySpan<int> dims, ReadOnlySpan<int> strides)
    {
        if (dims.Length != strides.Length) return false;
        if (dims.Length == 0) return true;
        int expected = 1;
        for (int i = dims.Length - 1; i >= 0; i--)
        {
            // Размерности 1 — безразлично к страйду.
            if (dims[i] != 1 && strides[i] != expected) return false;
            expected *= dims[i];
        }
        return true;
    }

    /// <summary>
    /// Преобразовать N-мерный индекс в линейный offset с учётом страйдов и базового offset тензора.
    /// </summary>
    public static int OffsetOf(ReadOnlySpan<int> indices, ReadOnlySpan<int> strides, int baseOffset = 0)
    {
        if (indices.Length != strides.Length)
            throw new ArgumentException("Длина индексов должна совпадать с rank.", nameof(indices));
        int off = baseOffset;
        for (int i = 0; i < indices.Length; i++) off += indices[i] * strides[i];
        return off;
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Storage;

namespace AI.ML.NeuralNetworks.V2;

public sealed partial class Tensor
{
    #region Backward

    /// <summary>
    /// Запустить обратное распространение градиента от этого тензора.
    /// Тензор должен быть скаляром (rank=0 или 1 элемент) — иначе требуется
    /// явный <paramref name="gradient"/>.
    /// </summary>
    public void Backward(Tensor gradient = null)
    {
        if (gradient == null)
        {
            if (NumElements != 1)
                throw new InvalidOperationException(
                    "Backward без явного gradient допустим только для скаляра. " +
                    "Передайте gradient той же формы, что и тензор.");
            gradient = Tensor.Ones(Shape, DType, Device);
        }
        else
        {
            if (gradient.Shape != Shape)
                throw new ArgumentException(
                    $"Backward: gradient.Shape={gradient.Shape} не совпадает с tensor.Shape={Shape}.",
                    nameof(gradient));
            if (gradient.DType != DType)
                throw new ArgumentException(
                    $"Backward: gradient.DType={gradient.DType} не совпадает с tensor.DType={DType}.",
                    nameof(gradient));
            if (gradient.Device != Device)
                throw new ArgumentException(
                    $"Backward: gradient.Device={gradient.Device} не совпадает с tensor.Device={Device}.",
                    nameof(gradient));
        }
        Engine.Run(this, gradient);
    }

    /// <summary>
    /// Зануляет градиент (для leaf-тензоров; вызывается оптимизатором).
    /// </summary>
    public void ZeroGrad()
    {
        if (Grad == null) return;
        Fill(Grad, 0.0);
    }

    #endregion Backward

    #region Доступ к данным

    /// <summary>
    /// Прямой доступ к данным как <see cref="Span{T}"/>. Только для contiguous-тензоров;
    /// для view сначала вызовите <c>.Contiguous()</c>.
    /// </summary>
    public Span<T> AsSpan<T>() where T : unmanaged
    {
        if (!IsContiguous)
            throw new InvalidOperationException(
                "AsSpan требует contiguous-тензор. Вызовите .Contiguous() явно.");
        if (DTypes.FromManaged<T>() != DType)
            throw new InvalidOperationException(
                $"Тип {typeof(T)} не совпадает с DType {DType}.");
        long n = NumElements;
        if (n > int.MaxValue)
            throw new OverflowException(
                $"AsSpan: NumElements={n} превышает int.MaxValue. Используйте сегментированный доступ.");
        return _storage.AsSpan<T>().Slice(_offset, (int)n);
    }

    /// <summary>Read-only вариант <see cref="AsSpan{T}"/>.</summary>
    public ReadOnlySpan<T> AsReadOnlySpan<T>() where T : unmanaged
    {
        if (!IsContiguous)
            throw new InvalidOperationException(
                "AsReadOnlySpan требует contiguous-тензор. Вызовите .Contiguous() явно.");
        if (DTypes.FromManaged<T>() != DType)
            throw new InvalidOperationException(
                $"Тип {typeof(T)} не совпадает с DType {DType}.");
        long n = NumElements;
        if (n > int.MaxValue)
            throw new OverflowException(
                $"AsReadOnlySpan: NumElements={n} превышает int.MaxValue. Используйте сегментированный доступ.");
        return _storage.AsReadOnlySpan<T>().Slice(_offset, (int)n);
    }

    /// <summary>
    /// Получить элемент по N-D индексам (с учётом страйдов и offset).
    /// Эффективно только для редкого доступа; для горячего пути используйте AsSpan.
    /// Тензор должен быть на CPU; <c>float</c> — DType.Float32.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(params int[] indices)
    {
        if (DType != DType.Float32)
            throw new InvalidOperationException(
                $"GetFloat требует DType.Float32, а тензор имеет {DType}. Используйте Get<T>() или приведите тип.");
        int off = V2.Strides.OffsetOf(indices, _strides, _offset);
        return _storage.AsReadOnlySpan<float>()[off];
    }

    /// <summary>Установить элемент по N-D индексам (только Float32-тензор).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFloat(float value, params int[] indices)
    {
        if (DType != DType.Float32)
            throw new InvalidOperationException(
                $"SetFloat требует DType.Float32, а тензор имеет {DType}. Используйте Set<T>() или приведите тип.");
        int off = V2.Strides.OffsetOf(indices, _strides, _offset);
        _storage.AsSpan<float>()[off] = value;
    }

    /// <summary>
    /// Получить элемент по N-D индексам как заданный тип <typeparamref name="T"/>.
    /// Тип должен совпадать с <see cref="DType"/>. Только CPU.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>(params int[] indices) where T : unmanaged
    {
        if (DTypes.FromManaged<T>() != DType)
            throw new InvalidOperationException(
                $"Get<{typeof(T).Name}>: тип не совпадает с DType {DType}.");
        int off = V2.Strides.OffsetOf(indices, _strides, _offset);
        return _storage.AsReadOnlySpan<T>()[off];
    }

    /// <summary>Установить элемент по N-D индексам (тип должен совпадать с <see cref="DType"/>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<T>(T value, params int[] indices) where T : unmanaged
    {
        if (DTypes.FromManaged<T>() != DType)
            throw new InvalidOperationException(
                $"Set<{typeof(T).Name}>: тип не совпадает с DType {DType}.");
        int off = V2.Strides.OffsetOf(indices, _strides, _offset);
        _storage.AsSpan<T>()[off] = value;
    }

    #endregion Доступ к данным
}

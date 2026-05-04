using System;
using System.Runtime.CompilerServices;

namespace AI.ML.NeuralNetworks.V2;

/// <summary>
/// Immutable форма N-мерного тензора (последовательность размерностей по осям).
/// </summary>
/// <remarks>
/// Для эффективности и безопасности форма копирует входной массив и не разделяет
/// хранилище с источником. Сравнение по значению.
/// Аналог <c>torch.Size</c> / numpy <c>shape</c>.
/// </remarks>
public sealed class Shape : IEquatable<Shape>
{
    private readonly int[] _dims;

    /// <summary>Скалярная форма (rank=0, 1 элемент).</summary>
    public static readonly Shape Scalar = new(Array.Empty<int>());

    /// <summary>
    /// Создать форму из перечисления размерностей.
    /// </summary>
    /// <exception cref="ArgumentException">Если какой-либо размер отрицательный.</exception>
    public Shape(params int[] dims)
    {
        if (dims == null) throw new ArgumentNullException(nameof(dims));
        for (int i = 0; i < dims.Length; i++)
        {
            if (dims[i] < 0)
                throw new ArgumentException($"Размер по оси {i} не может быть отрицательным: {dims[i]}.", nameof(dims));
        }
        _dims = (int[])dims.Clone();
    }

    /// <summary>Количество осей (rank).</summary>
    public int Rank => _dims.Length;

    /// <summary>Размер по оси <paramref name="axis"/>.</summary>
    public int this[int axis] => _dims[axis];

    /// <summary>Общее число элементов (произведение всех осей; 1 для скаляра).</summary>
    /// <exception cref="OverflowException">Если произведение размерностей не помещается в <see cref="long"/>.</exception>
    public long NumElements
    {
        get
        {
            long total = 1;
            try
            {
                for (int i = 0; i < _dims.Length; i++)
                    total = checked(total * _dims[i]);
            }
            catch (OverflowException ex)
            {
                throw new OverflowException(
                    $"NumElements переполнен: произведение размерностей {this} не помещается в long.", ex);
            }
            return total;
        }
    }

    /// <summary>Вернуть копию массива размерностей.</summary>
    public int[] ToArray() => (int[])_dims.Clone();

    /// <summary>
    /// Прямой read-only доступ к внутреннему массиву (без копирования).
    /// Используется в hot-path; не модифицировать.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<int> AsSpan() => _dims;

    /// <summary>
    /// Резолвит отрицательные индексы оси (-1 = последняя).
    /// </summary>
    public int NormalizeAxis(int axis)
    {
        int a = axis < 0 ? Rank + axis : axis;
        if (a < 0 || a >= Rank)
            throw new ArgumentOutOfRangeException(nameof(axis), $"Ось {axis} за пределами rank={Rank}.");
        return a;
    }

    /// <inheritdoc/>
    public bool Equals(Shape other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_dims.Length != other._dims.Length) return false;
        for (int i = 0; i < _dims.Length; i++)
            if (_dims[i] != other._dims[i]) return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is Shape s && Equals(s);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hc = new HashCode();
        for (int i = 0; i < _dims.Length; i++) hc.Add(_dims[i]);
        return hc.ToHashCode();
    }

    /// <summary>Сравнение форм.</summary>
    public static bool operator ==(Shape a, Shape b) => a is null ? b is null : a.Equals(b);
    /// <summary>Сравнение форм.</summary>
    public static bool operator !=(Shape a, Shape b) => !(a == b);

    /// <inheritdoc/>
    public override string ToString() => "[" + string.Join(",", _dims) + "]";
}

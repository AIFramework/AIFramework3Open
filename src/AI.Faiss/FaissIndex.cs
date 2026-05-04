using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AI.Faiss.Base;
using AI.Faiss.Enums;
using static AI.Faiss.Base.FaissNative;

namespace AI.Faiss;

/// <summary>
/// Обёртка над индексом FAISS для поиска ближайших соседей в пространстве векторов.
/// Реализует IDisposable для корректного освобождения нативных ресурсов.
/// </summary>
public sealed partial class FaissIndex : IDisposable
{
    private readonly FaissIndexHandle _indexHandle;

    private FaissIndex(FaissIndexHandle indexHandle) => _indexHandle = indexHandle;

    #region Фабричные методы

    /// <summary>
    /// Создает индекс указанного типа, определенного параметром конструктора.
    /// См. <see href="https://github.com/facebookresearch/faiss/wiki/The-index-factory">The index factory</see> для синтаксиса.
    /// </summary>
    /// <param name="dimension">Размерность вектора</param>
    /// <param name="constructor">Строка конструктора индекса faiss (напр. "IDMap2,HNSW32")</param>
    /// <param name="metric">Метрика расстояния</param>
    public static FaissIndex Create(int dimension, string constructor, MetricType metric)
    {
        if (dimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimension), "Размерность должна быть положительной.");
        ArgumentException.ThrowIfNullOrWhiteSpace(constructor, nameof(constructor));

        return Run(() =>
        {
            var ptr = FN_Create(dimension, constructor, metric);
            ThrowIfInvalidPointer(ptr);
            return new FaissIndex(new FaissIndexHandle(ptr));
        });
    }

    /// <summary>
    /// Создает индекс стандартного типа "IDMap2,HNSW32".
    /// </summary>
    /// <param name="dimension">Размерность вектора</param>
    /// <param name="metric">Метрика расстояния</param>
    public static FaissIndex CreateDefault(int dimension, MetricType metric)
    {
        if (dimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimension), "Размерность должна быть положительной.");

        return Run(() =>
        {
            var ptr = FN_CreateDefault(dimension, metric);
            ThrowIfInvalidPointer(ptr);
            return new FaissIndex(new FaissIndexHandle(ptr));
        });
    }

    /// <summary>
    /// Загрузить сохраненный индекс из файла.
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    public static FaissIndex Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        return Run(() =>
        {
            var ptr = FN_ReadIndex(path);
            ThrowIfInvalidPointer(ptr);
            return new FaissIndex(new FaissIndexHandle(ptr));
        });
    }

    #endregion

    #region Свойства

    /// <summary>Количество элементов в индексе.</summary>
    public long Count => Run(() => FN_Count(_indexHandle.Pointer));

    /// <summary>Метрика расстояния, указанная при создании индекса.</summary>
    public MetricType MetricType => Run(() => FN_MetricType(_indexHandle.Pointer));

    /// <summary>Размерность векторов индекса, указанная при создании.</summary>
    public int Dimension => Run(() => FN_Dimension(_indexHandle.Pointer));

    #endregion

    #region Сохранение

    /// <summary>
    /// Сохранить индекс в файл.
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        Do(() => FN_WriteIndex(_indexHandle.Pointer, path));
    }

    #endregion

    #region Внутренние вспомогательные методы

    /// <summary>Получить последнее сообщение об ошибке нативной библиотеки.</summary>
    public static string LastError()
    {
        var ptr = FN_GetLastError();
        if (ptr == IntPtr.Zero)
            return "Сообщение о последней ошибке отсутствует";

        try
        {
            return Marshal.PtrToStringAnsi(ptr) ?? "Невозможно получить информацию об ошибке";
        }
        catch
        {
            return "Невозможно получить информацию об ошибке";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfInvalidPointer(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            throw new InvalidOperationException($"Ошибка FAISS: {LastError()}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateVectorCount(int n, int arrayLength)
    {
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n), "Количество векторов должно быть положительным.");

        var dim = Dimension;
        if (dim > 0 && arrayLength != n * dim)
            throw new ArgumentException(
                $"Длина массива ({arrayLength}) не соответствует ожидаемой ({n} * {dim} = {n * dim}).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Run<T>(Func<T> comp)
    {
        try
        {
            return comp();
        }
        catch (SEHException ex)
        {
            throw new InvalidOperationException($"Ошибка FAISS: {LastError()}", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Do(Action comp)
    {
        try
        {
            comp();
        }
        catch (SEHException ex)
        {
            throw new InvalidOperationException($"Ошибка FAISS: {LastError()}", ex);
        }
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose() => _indexHandle?.Dispose();
}

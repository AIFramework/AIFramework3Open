using System;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Extensions;
using static AI.Faiss.Base.FaissNative;

namespace AI.Faiss;

public sealed partial class FaissIndex
{
    #region Поиск

    /// <summary>
    /// Поиск ближайших соседей (плоский массив).
    /// </summary>
    /// <param name="n">Количество входных векторов</param>
    /// <param name="vectors">Одномерный массив размером n * dimension</param>
    /// <param name="k">Количество ближайших соседей</param>
    /// <returns>Кортеж: (расстояния [n*k], идентификаторы соседей [n*k])</returns>
    public (float[] Distances, long[] Labels) SearchFlat(int n, float[] vectors, int k)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ValidateVectorCount(n, vectors.Length);
        if (k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), "Количество соседей должно быть положительным.");

        var distances = new float[n * k];
        var labels    = new long[n * k];

        Do(() =>
        {
            unsafe
            {
                fixed (float* ptrDists = distances)
                fixed (float* ptrVecs  = vectors)
                fixed (long*  ptrLbls  = labels)
                    FN_Search(_indexHandle.Pointer, n, ptrVecs, k, ptrDists, ptrLbls);
            }
        });

        return (distances, labels);
    }

    /// <summary>
    /// Поиск ближайших соседей (двухмерный массив).
    /// </summary>
    /// <param name="vectors">Массив запросных векторов</param>
    /// <param name="k">Количество ближайших соседей</param>
    /// <returns>Кортеж: (расстояния [n][k], идентификаторы соседей [n][k])</returns>
    public (float[][] Distances, long[][] Labels) Search(float[][] vectors, int k)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        if (vectors.Length == 0)
            return (Array.Empty<float[]>(), Array.Empty<long[]>());

        var flat = vectors.SelectMany(r => r).ToArray();
        var (dists, lbls) = SearchFlat(vectors.Length, flat, k);
        return (dists.Chunk(k).ToArray(), lbls.Chunk(k).ToArray());
    }

    /// <summary>
    /// Поиск ближайших соседей для массива Vector (конвертация double -> float).
    /// </summary>
    /// <param name="vectors">Массив Vector из библиотеки AI.DataStructs</param>
    /// <param name="k">Количество ближайших соседей</param>
    /// <returns>Кортеж: (расстояния [n][k], идентификаторы соседей [n][k])</returns>
    public (float[][] Distances, long[][] Labels) Search(Vector[] vectors, int k)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        if (vectors.Length == 0)
            return (Array.Empty<float[]>(), Array.Empty<long[]>());

        var flat = vectors.SelectMany(v => v).Select(d => (float)d).ToArray();
        var (dists, lbls) = SearchFlat(vectors.Length, flat, k);
        return (dists.Chunk(k).ToArray(), lbls.Chunk(k).ToArray());
    }

    /// <summary>
    /// Поиск соседей с реконструкцией векторов (плоский массив).
    /// </summary>
    /// <param name="n">Количество входных векторов</param>
    /// <param name="vectors">Одномерный массив размером n * dimension</param>
    /// <param name="k">Количество соседей</param>
    /// <returns>Кортеж: (расстояния [n*k], идентификаторы [n*k], реконструированные векторы [n*k*dimension])</returns>
    public (float[] Distances, long[] Labels, float[] Reconstructed) SearchAndReconstruct(int n, float[] vectors, int k)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ValidateVectorCount(n, vectors.Length);
        if (k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), "Количество соседей должно быть положительным.");

        var dim       = Dimension;
        var distances = new float[n * k];
        var labels    = new long[n * k];
        var recons    = new float[n * k * dim];

        Do(() =>
        {
            unsafe
            {
                fixed (float* ptrDists  = distances)
                fixed (float* ptrVecs   = vectors)
                fixed (long*  ptrLbls   = labels)
                fixed (float* ptrRecons = recons)
                    FN_SearchAndReconstruct(_indexHandle.Pointer, n, ptrVecs, k, ptrDists, ptrLbls, ptrRecons);
            }
        });

        return (distances, labels, recons);
    }

    #endregion

    #region Реконструкция

    /// <summary>
    /// Реконструировать векторы по идентификаторам (плоский результат).
    /// </summary>
    /// <param name="ids">Массив идентификаторов</param>
    /// <returns>Плоский массив [ids.Length * dimension]</returns>
    public float[] ReconstructFlat(long[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids, nameof(ids));
        if (ids.Length == 0) return Array.Empty<float>();

        var dim   = Dimension;
        var recons = new float[ids.Length * dim];

        Do(() =>
        {
            unsafe
            {
                fixed (long*  ptrIds  = ids)
                fixed (float* ptrVecs = recons)
                    FN_ReconstructBatch(_indexHandle.Pointer, ids.Length, ptrIds, ptrVecs);
            }
        });

        return recons;
    }

    /// <summary>
    /// Реконструировать векторы по идентификаторам как массив Vector.
    /// </summary>
    /// <param name="ids">Массив идентификаторов</param>
    /// <returns>Массив Vector (double)</returns>
    public Vector[] Reconstruct(long[] ids)
    {
        var flat = ReconstructFlat(ids);
        var dim  = Dimension;
        return flat
            .Chunk(dim)
            .Select(chunk => new Vector(chunk.ToDoubleArray()))
            .ToArray();
    }

    #endregion

    #region Обучение

    /// <summary>
    /// Обучение индекса на представительном наборе векторов.
    /// </summary>
    /// <param name="n">Количество обучающих векторов</param>
    /// <param name="vectors">Одномерный массив [n * dimension]</param>
    public void Train(int n, float[] vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ValidateVectorCount(n, vectors.Length);

        Do(() =>
        {
            unsafe
            {
                fixed (float* ptrVecs = vectors)
                    FN_Train(_indexHandle.Pointer, n, ptrVecs);
            }
        });
    }

    /// <summary>
    /// Обучение индекса из массива Vector (конвертация double -> float).
    /// </summary>
    /// <param name="vectors">Массив Vector из библиотеки AI.DataStructs</param>
    public void Train(Vector[] vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        if (vectors.Length == 0) return;

        var flat = vectors.SelectMany(v => v).Select(d => (float)d).ToArray();
        Train(vectors.Length, flat);
    }

    #endregion

    #region Assign / Remove / Merge

    /// <summary>
    /// Находит идентификаторы ближайших центроидов для заданных векторов.
    /// </summary>
    /// <param name="n">Количество векторов</param>
    /// <param name="vectors">Одномерный массив [n * dimension]</param>
    /// <returns>Массив идентификаторов [n]</returns>
    public long[] Assign(int n, float[] vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ValidateVectorCount(n, vectors.Length);

        var ids = new long[n];

        Do(() =>
        {
            unsafe
            {
                fixed (float* ptrVecs = vectors)
                fixed (long*  ptrIds  = ids)
                    FN_Assign(_indexHandle.Pointer, n, ptrVecs, ptrIds, 1);
            }
        });

        return ids;
    }

    /// <summary>
    /// Удаление векторов из индекса по идентификаторам.
    /// </summary>
    /// <param name="ids">Массив идентификаторов для удаления</param>
    public void RemoveIds(long[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids, nameof(ids));
        if (ids.Length == 0) return;

        Do(() =>
        {
            unsafe
            {
                fixed (long* ptrIds = ids)
                    FN_RemoveIds(_indexHandle.Pointer, ids.Length, ptrIds);
            }
        });
    }

    /// <summary>
    /// Объединение данных из другого индекса. Другой индекс становится пустым.
    /// </summary>
    /// <param name="otherIndex">Индекс-источник</param>
    /// <param name="addId">Смещение для идентификаторов элементов из другого индекса</param>
    public void MergeFrom(FaissIndex otherIndex, long addId = 0L)
    {
        ArgumentNullException.ThrowIfNull(otherIndex, nameof(otherIndex));
        Do(() => FN_MergeFrom(_indexHandle.Pointer, otherIndex._indexHandle.Pointer, addId));
    }

    #endregion
}

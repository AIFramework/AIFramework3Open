using System;
using System.Linq;
using AI.DataStructs.Algebraic;
using static AI.Faiss.Base.FaissNative;

namespace AI.Faiss;

public sealed partial class FaissIndex
{
    #region Добавление

    /// <summary>
    /// Добавление векторов в индекс (плоский массив).
    /// </summary>
    /// <param name="n">Количество векторов</param>
    /// <param name="vectors">Вектора размером n * dimension</param>
    public void AddFlat(int n, float[] vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ValidateVectorCount(n, vectors.Length);

        Do(() =>
        {
            unsafe
            {
                fixed (float* ptr = vectors)
                    FN_Add(_indexHandle.Pointer, n, ptr);
            }
        });
    }

    /// <summary>
    /// Добавление векторов в индекс (двухмерный массив).
    /// </summary>
    /// <param name="vectors">Массив векторов, каждый размерности dimension</param>
    public void Add(float[][] vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        if (vectors.Length == 0) return;

        var flat = vectors.SelectMany(v => v).ToArray();
        AddFlat(vectors.Length, flat);
    }

    /// <summary>
    /// Добавление векторов в индекс из массива Vector (конвертация double -> float).
    /// </summary>
    /// <param name="vectors">Массив Vector из библиотеки AI.DataStructs</param>
    public void Add(Vector[] vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        if (vectors.Length == 0) return;

        var flat = vectors.SelectMany(v => v).Select(d => (float)d).ToArray();
        AddFlat(vectors.Length, flat);
    }

    /// <summary>
    /// Добавление векторов с идентификаторами (плоский массив).
    /// </summary>
    /// <param name="n">Количество векторов</param>
    /// <param name="vectors">Вектора размером n * dimension</param>
    /// <param name="ids">Идентификаторы</param>
    public void AddWithIdsFlat(int n, float[] vectors, long[] ids)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ArgumentNullException.ThrowIfNull(ids, nameof(ids));
        ValidateVectorCount(n, vectors.Length);
        if (ids.Length != n)
            throw new ArgumentException($"Длина ids ({ids.Length}) не соответствует количеству векторов ({n}).", nameof(ids));

        Do(() =>
        {
            unsafe
            {
                fixed (float* ptrVec = vectors)
                fixed (long* pIds = ids)
                    FN_AddWithIds(_indexHandle.Pointer, n, ptrVec, pIds);
            }
        });
    }

    /// <summary>
    /// Добавление векторов с идентификаторами (двухмерный массив).
    /// </summary>
    /// <param name="vectors">Массив векторов</param>
    /// <param name="ids">Идентификаторы</param>
    public void AddWithIds(float[][] vectors, long[] ids)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ArgumentNullException.ThrowIfNull(ids, nameof(ids));
        if (vectors.Length == 0) return;
        if (ids.Length != vectors.Length)
            throw new ArgumentException($"Длина ids ({ids.Length}) не соответствует количеству векторов ({vectors.Length}).", nameof(ids));

        var flat = vectors.SelectMany(v => v).ToArray();
        AddWithIdsFlat(vectors.Length, flat, ids);
    }

    /// <summary>
    /// Добавление векторов с идентификаторами из массива Vector (конвертация double -> float).
    /// </summary>
    /// <param name="vectors">Массив Vector из библиотеки AI.DataStructs</param>
    /// <param name="ids">Идентификаторы</param>
    public void AddWithIds(Vector[] vectors, long[] ids)
    {
        ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
        ArgumentNullException.ThrowIfNull(ids, nameof(ids));
        if (vectors.Length == 0) return;
        if (ids.Length != vectors.Length)
            throw new ArgumentException($"Длина ids ({ids.Length}) не соответствует количеству векторов ({vectors.Length}).", nameof(ids));

        var flat = vectors.SelectMany(v => v).Select(d => (float)d).ToArray();
        AddWithIdsFlat(vectors.Length, flat, ids);
    }

    #endregion
}

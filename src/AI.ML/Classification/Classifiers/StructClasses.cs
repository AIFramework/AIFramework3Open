using AI.DataStructs;
using AI.ML.DataHandling.DataSets;
using AI.ML.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace AI.ML.Classification;

/// <summary>
/// Структура классификатора
/// </summary>
[Serializable]
[JsonConverter(typeof(StructClassesJsonConverter))]
public class StructClasses : List<VectorDatasetItem>
{

    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    /// <summary>
    /// Добавляет объект <see cref="VectorDatasetItem"/> в конец списка.
    /// </summary>
    /// <param name="item">Объект <see cref="VectorDatasetItem"/>, который необходимо добавить в список.</param>
    /// <remarks>
    /// Этот метод является потокобезопасным и обеспечивает защиту от одновременного доступа из разных потоков.
    /// </remarks>
    public new void Add(VectorDatasetItem item)
    {
        _lock.EnterWriteLock();
        try
        {
            base.Add(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Удаляет первое вхождение указанного объекта <see cref="VectorDatasetItem"/> из списка.
    /// </summary>
    /// <param name="item">Объект <see cref="VectorDatasetItem"/>, который необходимо удалить из списка.</param>
    /// <remarks>
    /// Этот метод является потокобезопасным и обеспечивает защиту от одновременного доступа из разных потоков.
    /// </remarks>
    public new void Remove(VectorDatasetItem item)
    {
        _lock.EnterWriteLock();
        try
        {
            _ = base.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Получает или задает элемент по указанному индексу.
    /// </summary>
    /// <param name="index">Индекс элемента для получения или установки.</param>
    /// <returns>Элемент по указанному индексу.</returns>
    /// <remarks>
    /// Для получения элемента используется потокобезопасный доступ на чтение.
    /// При установке элемента используется потокобезопасный доступ на запись.
    /// </remarks>
    public new VectorDatasetItem this[int index]
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return base[index];
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        set
        {
            _lock.EnterWriteLock();
            try
            {
                base[index] = value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Сохранить в файл
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public void Save(string path)   => SafeSerializer.Save(path, this, AiMlJsonOptions.Default);

    /// <summary>
    /// Сохранить в поток
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream) => SafeSerializer.Save(stream, this, AiMlJsonOptions.Default);

    /// <summary>
    /// Загрузить из файла
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <returns></returns>
    public static StructClasses Load(string path)   => SafeSerializer.Load<StructClasses>(path, AiMlJsonOptions.Default);

    /// <summary>
    /// Загрузить из потока
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static StructClasses Load(Stream stream) => SafeSerializer.Load<StructClasses>(stream, AiMlJsonOptions.Default);
}

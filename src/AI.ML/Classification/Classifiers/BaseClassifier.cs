using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.ML.DataHandling.DataSets;
using AI.ML.Json;
using System;
using System.IO;

namespace AI.ML.Classification;

/// <summary>
/// Базовый классификатор
/// </summary>
/// <typeparam name="T">Тип классификатора</typeparam>
[Serializable]
public class BaseClassifier<T> : IClassifier
{
    /// <summary>
    /// Классификация
    /// </summary>
    /// <param name="inp">Вход</param>
    /// <exception cref="NotImplementedException"></exception>
    public virtual int Classify(Vector inp)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Классификация (вероятности)
    /// </summary>
    /// <param name="inp">Вход</param>
    public virtual Vector ClassifyProbVector(Vector inp)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Обучить
    /// </summary>
    public virtual void Train(Vector[] features, int[] classes)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Обучить
    /// </summary>
    public virtual void Train(VectorDataset dataset)
    {
        Vector[] features = new Vector[dataset.Count];
        int[] classes = new int[dataset.Count];

        for (int i = 0; i < features.Length; i++)
        {
            classes[i] = dataset[i].ClassMark;
            features[i] = dataset[i].Features;
        }

        Train(features, classes);
    }

    /// <summary>
    /// Сохранить
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public virtual void Save(string path)   => SafeSerializer.Save(path, this, AiMlJsonOptions.Default);

    /// <summary>
    /// Сохранить
    /// </summary>
    /// <param name="stream">Поток</param>
    public virtual void Save(Stream stream) => SafeSerializer.Save(stream, this, AiMlJsonOptions.Default);

    /// <summary>
    /// Загрузить
    /// </summary>
    /// <param name="path">Путь</param>
    public static T Load(string path)   => SafeSerializer.Load<T>(path, AiMlJsonOptions.Default);

    /// <summary>
    /// Загрузить
    /// </summary>
    /// <param name="stream">Поток</param>
    public static T Load(Stream stream) => SafeSerializer.Load<T>(stream, AiMlJsonOptions.Default);
}

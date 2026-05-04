using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.ML.DataHandling.DataSets;
using AI.ML.Json;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AI.ML.Classification;

/// <summary>
/// Корреляционный классификатор
/// </summary>
[Serializable]
public class CorrelationClassifier : IClassifier
{
    /// <summary>
    /// Классы
    /// </summary>
    public StructClasses Classes { get; set; }

    /// <summary>
    /// Корреляционный классификатор
    /// </summary>
    public CorrelationClassifier()
    {
        Classes = new StructClasses();
    }
    /// <summary>
    /// Корреляционный классификатор
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public CorrelationClassifier(string path)
    {
        var loaded = Load(path);
        Classes = loaded.Classes;
    }
    /// <summary>
    /// Корреляционный классификатор
    /// </summary>
    /// <param name="classifikator">Классы</param>
    public CorrelationClassifier(StructClasses classifikator)
    {
        Classes = classifikator;
    }

    /// <summary>
    /// Добавить класс
    /// </summary>
    /// <param name="features">Вектор признаков</param>
    /// <param name="num">Метка</param>
    public void AddClass(Vector features, int num)
    {
        VectorDatasetItem structClass = new VectorDatasetItem
        {
            Features = features.Clone(),
            ClassMark = num
        };
        Classes.Add(structClass);
    }


    /// <summary>
    /// Распознавание вектора — возвращает метку класса с наибольшей корреляцией.
    /// </summary>
    /// <param name="inp">Вход</param>
    public int Classify(Vector inp)
    {
        int bestClass = Classes[0].ClassMark;
        double bestCorr = double.NegativeInfinity;

        for (int i = 0; i < Classes.Count; i++)
        {
            double corr = Statistic.CorrelationCoefficient(inp, Classes[i].Features);
            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestClass = Classes[i].ClassMark;
            }
        }

        return bestClass;
    }

    /// <summary>
    /// Распознавание вектора: возвращает вектор корреляций для каждого класса (размером max_label+1).
    /// </summary>
    /// <param name="inp">Вектор входа</param>
    public Vector ClassifyProbVector(Vector inp)
    {
        int maxLabel = 0;
        for (int i = 0; i < Classes.Count; i++)
            if (Classes[i].ClassMark > maxLabel) maxLabel = Classes[i].ClassMark;

        Vector result = new Vector(maxLabel + 1);
        double[] counts = new double[maxLabel + 1];

        for (int i = 0; i < Classes.Count; i++)
        {
            double corr = Statistic.CorrelationCoefficient(inp, Classes[i].Features);
            // Накапливаем максимальную корреляцию по каждому классу
            if (corr > result[Classes[i].ClassMark])
                result[Classes[i].ClassMark] = corr;
        }

        // Сдвигаем в положительную область и нормируем для интерпретации как вероятности
        double minVal = result.Min();
        if (minVal < 0)
            for (int i = 0; i < result.Count; i++)
                result[i] -= minVal;

        double sum = result.Sum();
        if (sum > 1e-12)
            return result / sum;

        return result;
    }

    /// <summary>
    /// Обучение классификатора
    /// </summary>
    /// <param name="features">Признаки</param>
    /// <param name="classes">Метки классов</param>
    public void Train(Vector[] features, int[] classes)
    {
        if (features.Length != classes.Length)
        {
            throw new InvalidOperationException("Число вектров признаков и число меток классов не совпадают");
        }

        for (int i = 0; i < features.Length; i++)
        {
            AddClass(features[i], classes[i]);
        }
    }
    /// <summary>
    /// Обучение классификатора
    /// </summary>
    /// <param name="dataset">Набор данных признаки-метка</param>
    public void Train(VectorDataset dataset)
    {
        for (int i = 0; i < dataset.Count; i++)
        {
            AddClass(dataset[i].Features, dataset[i].ClassMark);
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
    public static CorrelationClassifier Load(string path)   => SafeSerializer.Load<CorrelationClassifier>(path, AiMlJsonOptions.Default);

    /// <summary>
    /// Загрузить из потока
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static CorrelationClassifier Load(Stream stream) => SafeSerializer.Load<CorrelationClassifier>(stream, AiMlJsonOptions.Default);
}

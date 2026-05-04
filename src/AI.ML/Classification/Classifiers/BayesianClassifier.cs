using AI.DataStructs.Algebraic;
using AI.ML.DataHandling.DataSets;
using AI.Statistics.Distributions;
using System;
using System.Collections.Generic;
using System.IO;

namespace AI.ML.Classification;

/// <summary>
/// Классификатор основанный на теореме Байеса
/// </summary>
[Serializable]
public class BayesianClassifier : IClassifier
{
    private readonly NonCorrelatedGaussian nonCorrelatedGaussian = new NonCorrelatedGaussian();
    private List<Dictionary<string, Vector>> classifiersParams = new List<Dictionary<string, Vector>>();
    private Vector w = new Vector();

    /// <summary>
    /// Классификатор основанный на теореме Байеса
    /// </summary>
    public BayesianClassifier(int nInp, int nOutp)
    {
        w = new Vector(nOutp) + 0.5;

        for (int i = 0; i < nOutp; i++)
        {
            var dat = new Dictionary<string, Vector>();
            dat.Add("std", new Vector(nInp) + 1);
            dat.Add("mean", new Vector(nInp));
            classifiersParams.Add(dat);
        }
    }

    /// <summary>
    /// Классификатор основанный на теореме Байеса
    /// </summary>
    public BayesianClassifier()
    {

    }

    /// <summary>
    /// Классификация
    /// </summary>
    public int Classify(Vector inp)
    {
        return ClassifyProbVector(inp).MaxElementIndex();
    }

    /// <summary>
    /// Классификация
    /// </summary>
    /// <param name="inp"></param>
    /// <returns></returns>
    public Vector ClassifyProbVector(Vector inp)
    {
        if (classifiersParams.Count == 0)
            throw new InvalidOperationException("Классификатор не обучен. Вызовите Train() перед классификацией.");

        Vector classes = new Vector(classifiersParams.Count);

        for (int i = 0; i < classifiersParams.Count; i++)
            classes[i] = w[i] * nonCorrelatedGaussian.CulcProb(inp, classifiersParams[i]);

        double sum = classes.Sum();
        if (sum < 1e-300)
        {
            // Все плотности близки к нулю (точка далеко от всех кластеров) — возвращаем равномерное распределение
            double uniform = 1.0 / classifiersParams.Count;
            return new Vector(classifiersParams.Count) + uniform;
        }

        return classes / sum;
    }

    /// <summary>
    /// Не реализовано
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <exception cref="NotImplementedException"></exception>
    public void Save(string path)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Не реализовано
    /// </summary>
    public void Save(Stream stream)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Обучение байесовского классификатора
    /// </summary>
    public void Train(Vector[] features, int[] classes)
    {
        VectorDataset vectorClasses = new VectorDataset();

        for (int i = 0; i < features.Length; i++)
        {
            vectorClasses.Add(new VectorDatasetItem(features[i], classes[i]));
        }

        Train(vectorClasses);
    }

    /// <summary>
    /// Обучение байесовского классификатора
    /// </summary>
    /// <param name="dataset"></param>
    public void Train(VectorDataset dataset)
    {
        var gr = dataset.GetGroupes();
        w = new Vector(gr.Length);
        classifiersParams = new List<Dictionary<string, Vector>>();

        for (int i = 0; i < gr.Length; i++)
        {
            var dat = new Dictionary<string, Vector>();
            dat.Add("std", gr[i].Std);
            dat.Add("mean", gr[i].Mean);

            w[i] = gr[i].GroupeFeatures.Count;
            classifiersParams.Add(dat);
        }
    }
}
